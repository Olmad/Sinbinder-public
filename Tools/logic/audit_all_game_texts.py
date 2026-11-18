#!/usr/bin/env python3
"""Whole-repository candidate game-text extraction, not semantic certification.

Extracts string-bearing candidates from C#, Unity YAML, gettext-like TXT, JSON,
CSV/TSV, XML and other source text. Produces immutable artifact row IDs with SHA
of file content.  Every row is UNREVIEWED until a human/independent agent checks
its context, provenance, and what it tells the player.

Run from repository root:
 python3 Tools/logic/audit_all_game_texts.py --output-dir /tmp/sinbinder-texts
"""
from __future__ import annotations
import argparse
import collections
import csv
import hashlib
import json
import re
import subprocess
from pathlib import Path

SCAN_EXT = {".cs", ".txt", ".json", ".csv", ".tsv", ".xml", ".yaml", ".yml",
            ".asset", ".prefab", ".unity", ".uxml", ".uss", ".ink", ".yarn",
            ".po", ".strings", ".bytes"}
SCAN_ROOTS = ("Assets/", "StreamingAssets/", "Packages/", "ProjectSettings/")
CS_LITERAL = re.compile(
    r'(?:\$@|@\$|\$|@)?"(?:\\.|""|[^"\\])*"|(?:\$+)?"""[\s\S]*?"""',
    re.MULTILINE,
)
YAML_LINE = re.compile(r'^\s*([\w.-]+):\s*(.*)$')
YAML_HUMAN_KEY = re.compile(
    r'(?:^m_Text$|text$|label$|description$|summary$|title$|tooltip$|'
    r'subtitle$|dialogue$|dialog$|caption$|story$|narration$|'
    r'displayName$|localiz|speech$|body$|content$|choice$)',
    re.IGNORECASE,
)
SUSPECT = [
    ("FALSE_REFERENT_DEMO", re.compile(r"Она смотрит\.", re.I),
     "Whisper Lust refers to an unverified woman absent from the demo"),
    ("INVENTED_MOCKERY", re.compile(r"Он смеялся над тобой у огня", re.I),
     "Who mocked this soul, and does the memory actually exist?"),
    ("UNKNOWN_KARGAN", re.compile(r"Карган бы смог", re.I),
     "Does this warrior know Kargan?"),
    ("PROMISED_POT", re.compile(r"После — котёл твой", re.I),
     "Is a pot or reward actually available?"),
    ("PROMISED_LOOT", re.compile(r"Половина добычи — твоя", re.I),
     "Is half of the spoils actually transferred or merely gold spent?"),
    ("PROMISED_SAFETY", re.compile(r"до утра никто не тронет", re.I),
     "Can the game truthfully guarantee safety until morning?"),
]
INTERP = re.compile(r'(?<!\{)\{(\d+)(?:[^{}]*)\}(?!\})')
LOCALE_ID = re.compile(r'^msgid "((?:\\.|[^"])*)"\s*$')
LOCALE_STR = re.compile(r'^msgstr "((?:\\.|[^"])*)"\s*$')


def tracked(root: Path):
    try:
        r = subprocess.run(["git", "ls-files", "-z"], cwd=root, check=True,
                           stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        return [x.decode("utf-8", "replace") for x in r.stdout.split(b"\0") if x]
    except (OSError, subprocess.CalledProcessError):
        return [str(x.relative_to(root)) for x in root.rglob("*") if x.is_file()]


def bucket(p: str) -> str:
    if p.endswith(".cs"):
        return "CS"
    if p.endswith("/en.txt"):
        return "LOCALE"
    if p.endswith(".txt") or p.endswith(".po") or p.endswith(".strings"):
        return "PLAIN"
    if p.endswith(".json"):
        return "JSON"
    if p.endswith((".asset", ".prefab", ".unity", ".yaml", ".yml")):
        return "UNITY_SERIALIZED"
    if p.endswith((".csv", ".tsv")):
        return "TABLE"
    return "OTHER"


def humanish(value: str) -> bool:
    v = value.strip().strip('"')
    if re.search(r"[А-Яа-яЁё]", v):
        return True
    if len(v) >= 6 and len(re.findall(r"[A-Za-z]{2,}", v)) >= 2:
        return True
    return False


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--output-dir", default="text-audit-results")
    args = ap.parse_args()
    root = Path(__file__).resolve().parents[2]
    dest = (root / args.output_dir).resolve()
    dest.mkdir(parents=True, exist_ok=True)
    paths = tracked(root)
    candidates = sorted(p for p in paths if p.startswith(SCAN_ROOTS)
                        and not p.endswith(".meta")
                        and Path(p).suffix.lower() in SCAN_EXT)
    counts = collections.Counter()
    statuses = []
    records = []
    findings = []
    translations = []
    locale_pairs = collections.defaultdict(set)
    next_id = 1

    def add(path, line, kind, text, source_code=False):
        nonlocal next_id
        if text is None or not isinstance(text, str) or not text.strip():
            return
        value = text.replace("\r", r"\r").replace("\n", r"\n").replace("\t", r"\t")
        row = {"id": next_id, "path": path, "line": line, "kind": kind,
               "lang": "RU" if re.search(r"[А-Яа-яЁё]", value)
               else "OTHER", "candidate_player_text": humanish(value),
               "text": value[:1200], "audit_status": "UNREVIEWED"}
        next_id += 1
        records.append(row)
        counts["strings_extracted"] += 1
        if row["candidate_player_text"]:
            counts["human_like"] += 1
        if row["lang"] == "RU":
            counts["russian"] += 1
        for code, rx, reason in SUSPECT:
            if rx.search(value):
                findings.append({"severity": "CONFIRMED_IN_CONTEXT"
                                 if code == "FALSE_REFERENT_DEMO"
                                 and path.endswith("/Whisper.cs")
                                 else "REVIEW_REQUIRED",
                                 "pattern": code, "path": path, "line": line,
                                 "row_id": row["id"], "text": row["text"],
                                 "question": reason})

    for p in candidates:
        path = root / p
        start = len(records)
        error = None
        try:
            b = path.read_bytes()
            try:
                s = b.decode("utf-8-sig")
            except UnicodeDecodeError:
                s = b.decode("utf-8")
            kind = bucket(p)
            if kind == "CS":
                for m in CS_LITERAL.finditer(s):
                    line = s.count("\n", 0, m.start()) + 1
                    lit = m.group(0)
                    context = s[max(0, m.start() - 100):m.start()]
                    label = "CS_LOC" if re.search(r"Loc\.(?:T|F)\s*\(\s*$", context) \
                        else ("CS_UI" if re.search(r"(?:Say|Write|GUIContent|Label|Button|Tooltip|Speech)\s*\(\s*$", context)
                              else "CS_LITERAL")
                    if lit.startswith('@"') or lit.startswith('$@"') or lit.startswith('@$"'):
                        value = lit[lit.find('"') + 1:-1].replace('""', '"')
                    else:
                        value = lit[lit.find('"') + 1:-1]
                    add(p, line, label, value)
            elif kind == "LOCALE":
                last_id = None
                for line, item in enumerate(s.splitlines(), 1):
                    match_id = LOCALE_ID.match(item)
                    match_en = LOCALE_STR.match(item)
                    if match_id:
                        last_id = (match_id.group(1), line)
                        add(p, line, "LOCALE_RU_KEY", match_id.group(1))
                    elif match_en:
                        en = match_en.group(1)
                        add(p, line, "LOCALE_EN_TEXT", en)
                        if last_id:
                            ru, src_line = last_id
                            locale_pairs[ru].add(en)
                            reasons = []
                            if not en.strip():
                                reasons.append("EMPTY_TRANSLATION")
                            if set(INTERP.findall(ru)) != set(INTERP.findall(en)):
                                reasons.append("PLACEHOLDER_MISMATCH")
                            if re.search(r"[А-Яа-яЁё]", en):
                                reasons.append("CYRILLIC_IN_EN")
                            if reasons:
                                translations.append({
                                    "path": p, "line": line, "ru_line": src_line,
                                    "ru": ru[:300], "en": en[:300],
                                    "flags": reasons
                                })
                            last_id = None
            elif kind == "JSON":
                def traverse(node, pathkey=""):
                    if isinstance(node, dict):
                        for key, val in node.items():
                            traverse(val, pathkey + "." + str(key))
                    elif isinstance(node, list):
                        for i, val in enumerate(node):
                            traverse(val, pathkey + "[" + str(i) + "]")
                    elif isinstance(node, str):
                        add(p, 0, "JSON" + pathkey[-95:], node)
                try:
                    traverse(json.loads(s))
                except json.JSONDecodeError:
                    for line, item in enumerate(s.splitlines(), 1):
                        for m in re.finditer(r'"((?:\\.|[^"\\])*)"', item):
                            add(p, line, "JSON_FALLBACK", m.group(1))
            elif kind == "UNITY_SERIALIZED":
                for line, item in enumerate(s.splitlines(), 1):
                    m = YAML_LINE.match(item)
                    if not m:
                        continue
                    key, value = m.groups()
                    if YAML_HUMAN_KEY.search(key):
                        add(p, line, "YAML_" + key, value)
                    elif key in ("m_Name", "name", "id"):
                        # These are discoverable object names. They are NOT
                        # automatically player-facing, but may become labels.
                        add(p, line, "YAML_OBJECT_NAME_UNCONFIRMED", value)
            elif kind == "TABLE":
                delimiter = "\t" if p.lower().endswith(".tsv") else ","
                for line, row in enumerate(csv.reader(s.splitlines(), delimiter=delimiter), 1):
                    for i, value in enumerate(row):
                        add(p, line, "TABLE_COL_" + str(i), value)
            else:
                for line, item in enumerate(s.splitlines(), 1):
                    if kind == "PLAIN":
                        add(p, line, "PLAIN_LINE", item)
                    else:
                        for m in re.finditer(r'>([^<>]{3,})<|[Tt]ext="([^"]+)"', item):
                            add(p, line, "XML_OR_OTHER", m.group(1) or m.group(2))
        except (OSError, UnicodeError, csv.Error) as exc:
            error = type(exc).__name__ + ": " + str(exc)[:180]
            counts["read_errors"] += 1
        statuses.append({"path": p, "source_sha256":
                         hashlib.sha256(path.read_bytes()).hexdigest()[:20]
                         if path.is_file() else "", "bucket": bucket(p),
                         "source_bytes": path.stat().st_size if path.exists() else 0,
                         "extracted_candidates": len(records) - start,
                         "parse_status": "ERROR" if error else "EXTRACTED",
                         "semantic_status": "UNREVIEWED", "error": error})
        counts["source_files"] += 1

    dup = {k: sorted(v) for k, v in locale_pairs.items() if len(v) > 1}
    for key, translations_for_key in list(dup.items())[:500]:
        translations.append({"flags": ["DUPLICATE_LOCALIZATION_KEY"], "ru": key[:250],
                             "en_variants": translations_for_key})
    counts["localization_keys"] = len(locale_pairs)
    counts["localization_duplicates"] = len(dup)
    counts["localization_warnings"] = len(translations)
    counts["suspect_occurrences"] = len(findings)

    def dump(name, obj):
        (dest / name).write_text(json.dumps(obj, ensure_ascii=False, indent=2),
                                 encoding="utf-8")

    dump("text-source-coverage.json", statuses)
    dump("all-candidate-texts.json", records)
    dump("semantic-suspects.json", findings)
    dump("localization-issues.json", translations)

    report = [
        "# Sinbinder: COMPLETE MACHINE INVENTORY, NOT COMPLETE SEMANTIC AUDIT",
        "", "No sentence in this report has been certified true merely by extraction.",
        "Every extracted text remains UNREVIEWED until context/character/event is checked.",
        "", "## Extraction statistics",
        f"- Tracked files total: {len(paths)}",
        f"- Candidate text-bearing sources inspected: {len(candidates)}",
        f"- Candidate files read without errors: {len(candidates) - counts['read_errors']}",
        f"- Candidate files not readable: {counts['read_errors']}",
        f"- Extracted string candidates: {counts['strings_extracted']}",
        f"- Human-like candidates (heuristic): {counts['human_like']}",
        f"- Russian-containing strings: {counts['russian']}",
        f"- Localization keys (one en.txt source): {counts['localization_keys']}",
        f"- Localization warnings (incl duplicate keys): {counts['localization_warnings']}",
        f"- Semantic suspect occurrences: {counts['suspect_occurrences']}",
        "",
        "## Scope and limitations",
        "- Source extraction counts are not a measure of checked/approved meaning.",
        "- C# regular and verbatim strings are extracted, but complex raw interpolation",
        "  and dynamically composed lines need independent manual/runtime evaluation.",
        "- Unity YAML and serialized files are searched for likely display fields;",
        "  scene bindings and source-generated texts still need independent inspection.",
        "- Text baked into image/audio assets cannot be read by this scanner.",
        "- External/remote/generated content needs separate discovery.",
        "",
        "## Initial flagged candidate assertions",
        "| Severity | Path:line | Claim | Why inspect |",
        "|---|---|---|---|"
    ]
    for f in findings[:100]:
        value = f["text"].replace("|", r"\|")[:110]
        report.append(f"| {f['severity']} | {f['path']}:{f['line']} | "
                      f"{value} | {f['question']} |")
    report += ["", "## Important", "Any uncovered, binary, conditional, or "
               "source-only claim remains UNKNOWN until checked independently."
               " Five independent semantic audit passes are still REQUIRED."]
    (dest / "text-audit-machine-summary.md").write_text(
        "\n".join(report) + "\n", encoding="utf-8")

    print("TEXT_AUDIT_RUN_COMPLETE")
    for key in ("source_files", "strings_extracted", "human_like", "russian",
                "localization_keys", "localization_warnings",
                "suspect_occurrences", "read_errors"):
        print(f"{key}: {counts[key]}")
    print("semantic_pass_count: 0 (not a semantic validator)")
    for status in statuses:
        if status["error"]:
            print(f"UNREADABLE_SOURCE: {status['path']} : {status['error']}")
    flags = collections.Counter(flag for item in translations
                                for flag in item.get("flags", []))
    print("localization_warning_categories: " + json.dumps(flags, ensure_ascii=False))
    for item in translations[:30]:
        print("LOCALIZATION_REVIEW: " + json.dumps(item, ensure_ascii=False)[:400])

    print("known_suspect_details:")
    for f in findings[:60]:
        print(f"[{f['severity']}] {f['path']}:{f['line']} "
              f"{f['pattern']}: {f['text'][:130]}")
    if counts["read_errors"]:
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
