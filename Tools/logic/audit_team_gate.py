#!/usr/bin/env python3
"""Sinbinder: validate five *independent* text-audit ledgers.

This tool does NOT infer that a source is semantically correct from a scan,
a read, or a passing CI run. It enforces traceability and reports audit debt.

Usage:
 python3 Tools/logic/audit_team_gate.py --output-dir /tmp/audit-report
 python3 Tools/logic/audit_team_gate.py --base-ref HEAD^ --enforce-changed
"""
from __future__ import annotations

import argparse
import collections
import csv
import io
import json
import re
import subprocess
import sys
from pathlib import Path

PASS_COUNT = 5
SOURCE_COUNT = 582
FIELDS = (
    "path", "blob_sha", "bytes", "audit_class",
    "found_text_candidates", "checked_text_candidates",
    "failed_text_candidates", "unknown_text_candidates",
    "status", "evidence",
)
NUMERIC = FIELDS[4:8]
KNOWN = {
    "UNINSPECTED", "EXCLUDED_TECH", "PASS", "FAIL", "PARTIAL_FAIL",
    "UNKNOWN", "UNKNOWN_CONTEXT", "NEEDS_CONTEXT",
}
CONTEXT = {"UNKNOWN", "UNKNOWN_CONTEXT", "NEEDS_CONTEXT"}
SHA_RE = re.compile(r"^[a-f0-9]{40}$")
META_RE = re.compile(r"^# INDEPENDENT_TEXT_AUDIT_PASS=(\d+) BASE_SHA=([a-f0-9]{40})$")
GOOD_EVIDENCE = re.compile(r"(?:blob|SHA|line|строк|method|метод|ключ|key|L\d+)", re.I)
SOURCE_REF = re.compile(r"(?:\.(?:cs|txt|json|asset|prefab|unity)|L\d+|lines? \d+|строк)", re.I)
QA2_RE = re.compile(r"(?<!\w)QA2:", re.I)

QA2_FIELD_RE = re.compile(
    r"(?i)(?<![A-Za-z_])(source|blob|lines|key|context|negative|verified|total|RU_EN|outcome)"
    r"\s*=\s*([^;\r\n]*)"
)


def qa2_values(evidence: str):
    return {name.lower(): value.strip()
            for name, value in QA2_FIELD_RE.findall(evidence)}


def verify_qa2(path: str, status: str, row: dict, metrics: dict):
    errors = []
    if not QA2_RE.search(row["evidence"]):
        return ["missing QA2 evidence"]
    values = qa2_values(row["evidence"])
    required = {"source", "blob", "context", "negative", "verified", "total",
                "ru_en", "outcome"}
    if not ({"lines", "key"} & values.keys()):
        errors.append("missing lines/key source locator")
    for field in sorted(required - values.keys()):
        errors.append(f"missing QA2 {field}=")
    for field in sorted(required & values.keys()):
        if not values[field]:
            errors.append(f"empty QA2 {field}=")
    source = values.get("source", "")
    # One ledger row can audit a locale file together with the C# caller.
    # The first path must still be the precise ledger path; appended
    # cross-references do not change ownership of the ledger source.
    if source != path and not (
        source.startswith(path + " + ") and SOURCE_REF.search(source[len(path) + 3:])
    ):
        errors.append("QA2 source does not match ledger path")
    if values.get("blob") and not SHA_RE.fullmatch(values["blob"]):
        errors.append("QA2 blob requires full 40-character SHA")
    raw_outcome = values.get("outcome", "")
    # Legacy evidence can append an explanation after outcome=UNKNOWN.
    # Match the exact leading enum token, never a substring of UNKNOWNISH.
    token = re.match(r"^(" + "|".join(sorted(KNOWN, key=len, reverse=True)) +
                     r")(?=$|[.;\s])", raw_outcome)
    outcome = token.group(1) if token else raw_outcome
    # The row is an aggregate. A newly reviewed batch may remain UNKNOWN
    # even if earlier reviewed batches already make the whole file PARTIAL_FAIL.
    # This DOES NOT clear the recorded failures or accept any new PASS.
    incremental_context = (
        status == "PARTIAL_FAIL" and outcome in CONTEXT and
        metrics["failed_text_candidates"] > 0
    )
    if outcome and outcome != status and not incremental_context:
        errors.append("QA2 outcome and status disagree")
    try:
        verified = int(values["verified"])
        total = int(values["total"])
        if verified < 0 or total < 0 or verified > total:
            errors.append("QA2 verified/total totals inconsistent")
        if verified > metrics["checked_text_candidates"]:
            errors.append("QA2 verified exceeds checked")
        if total > metrics["found_text_candidates"]:
            errors.append("QA2 total exceeds found")
        if status == "PASS":
            if verified != total or total != metrics["found_text_candidates"]:
                errors.append("PASS not all source candidates semantically verified")
            if (metrics["failed_text_candidates"] != 0 or
                    metrics["unknown_text_candidates"] != 0 or
                    metrics["checked_text_candidates"] != total):
                errors.append("PASS still has failures/unknown or unread candidates")
            if values.get("ru_en", "").upper() == "UNKNOWN":
                errors.append("PASS has unverified RU/EN")
            if values.get("negative", "").upper() == "UNKNOWN":
                errors.append("PASS has no tested counterexample")
        elif status in {"FAIL", "PARTIAL_FAIL"}:
            if not metrics["failed_text_candidates"]:
                errors.append("FAIL/PARTIAL_FAIL with zero failed candidates")
            if values.get("negative", "").upper() == "UNKNOWN":
                errors.append("FAIL has no reproducible counterexample")
    except (KeyError, ValueError):
        errors.append("invalid QA2 verified= or total=")
    return errors



def ledger_path(index: int) -> str:
    return f"Docs/AgentTeam/88-INDEPENDENT-AUDITOR-{index}-PROGRESS.tsv"


def read_ledger(content: str, number: int):
    lines = content.splitlines()
    if len(lines) < 3:
        raise ValueError("missing ledger header or rows")
    match = META_RE.fullmatch(lines[0].strip())
    if not match or int(match.group(1)) != number:
        raise ValueError("incorrect auditor index / BASE_SHA metadata")
    reader = csv.DictReader(io.StringIO("\n".join(lines[1:])), delimiter="\t")
    if tuple(reader.fieldnames or ()) != FIELDS:
        raise ValueError("wrong TSV header (required ten columns)")
    rows = {}
    for line_no, row in enumerate(reader, 3):
        if None in row or any(value is None for value in row.values()):
            raise ValueError(f"row {line_no}: unescaped tab or missing column")
        path = row["path"]
        if not path or path in rows:
            raise ValueError(f"row {line_no}: missing/duplicate path {path!r}")
        rows[path] = row
    return match.group(2), rows


def previous_content(root: Path, ref: str, path: str):
    try:
        proc = subprocess.run(
            ["git", "show", f"{ref}:{path}"], cwd=root, check=False,
            capture_output=True, text=True, encoding="utf-8",
        )
    except OSError:
        return None
    return proc.stdout if proc.returncode == 0 else None


def validate(root: Path, base_ref: str | None = None, enforce_changed=False):
    issues, warnings = [], []
    ledgers, base_shas, previous = {}, set(), {}
    for number in range(1, PASS_COUNT + 1):
        path = ledger_path(number)
        try:
            raw = (root / path).read_text(encoding="utf-8")
            sha, rows = read_ledger(raw, number)
            ledgers[number] = rows
            base_shas.add(sha)
        except (OSError, UnicodeError, ValueError) as exc:
            issues.append(f"Auditor {number}: {exc}")
            continue
        if base_ref:
            old = previous_content(root, base_ref, path)
            if old is not None:
                try:
                    _, previous[number] = read_ledger(old, number)
                except ValueError as exc:
                    issues.append(f"Auditor {number}: invalid previous ledger: {exc}")
    if len(base_shas) != 1:
        issues.append("all five ledgers must share one immutable BASE_SHA")
    if len(ledgers) != PASS_COUNT:
        return issues, warnings, {}

    all_paths = set(ledgers[1])
    if len(all_paths) != SOURCE_COUNT:
        issues.append(f"inventory path count {len(all_paths)} != {SOURCE_COUNT}")
    for number, rows in ledgers.items():
        if set(rows) != all_paths:
            issues.append(f"Auditor {number}: source list diverges from auditor 1")
    for path in all_paths:
        identities = {
            (ledger[path]["blob_sha"], ledger[path]["bytes"], ledger[path]["audit_class"])
            for ledger in ledgers.values() if path in ledger
        }
        if len(identities) != 1:
            issues.append(f"{path}: inventory identity differs between auditors")

    report = {
        "baseline_sha": next(iter(base_shas)) if len(base_shas) == 1 else None,
        "inventory_sources": len(all_paths), "auditors": {},
    }
    checked_by_any = set()
    checked_by_all = set(all_paths)
    hard_fail_paths = set()
    semantics_passed = set()
    mismatch = []
    for number in range(1, PASS_COUNT + 1):
        rows = ledgers[number]
        counts = collections.Counter(row["status"] for row in rows.values())
        inspected = {path for path, row in rows.items() if row["status"] != "UNINSPECTED"}
        checked_by_any |= inspected
        checked_by_all &= inspected
        report["auditors"][str(number)] = {
            "counts": dict(sorted(counts.items())),
            "inspected_sources": len(inspected),
            "candidate_sources": len(rows),
            "semantic_pass_sources": counts["PASS"],
            "confirmed_or_partial_fail_sources": counts["FAIL"] + counts["PARTIAL_FAIL"],
        }
        for path, row in rows.items():
            status, evidence = row["status"], row["evidence"].strip()
            if status not in KNOWN:
                issues.append(f"Auditor {number}, {path}: unknown status {status!r}")
                continue
            if not SHA_RE.fullmatch(row["blob_sha"]):
                issues.append(f"Auditor {number}, {path}: invalid blob SHA")
            try:
                metrics = {name: int(row[name]) for name in NUMERIC}
                if any(v < 0 for v in metrics.values()):
                    raise ValueError("negative metric")
                if int(row["bytes"]) < 0:
                    raise ValueError("negative source size")
            except (ValueError, TypeError) as exc:
                issues.append(f"Auditor {number}, {path}: invalid numeric field ({exc})")
                continue
            if status == "UNINSPECTED":
                if evidence or any(metrics.values()):
                    issues.append(f"Auditor {number}, {path}: UNINSPECTED has progress/evidence")
                continue
            if not evidence:
                issues.append(f"Auditor {number}, {path}: reviewed source has empty evidence")
            elif not GOOD_EVIDENCE.search(evidence):
                # Existing auditors sometimes recorded real license/source reads
                # without a locator. Preserve history; enforce QA2 on new edits.
                warnings.append(f"Auditor {number}, {path}: LEGACY evidence lacks source locator")
            if status == "PASS":
                # Never grandfather an unsubstantiated semantic PASS.
                proof_errors = verify_qa2(path, status, row, metrics)
                if proof_errors:
                    issues.extend(f"Auditor {number}, {path}: PASS has no QA2 semantic proof: {msg}"
                                  for msg in proof_errors)
                else:
                    semantics_passed.add(path)
            if status in {"FAIL", "PARTIAL_FAIL"}:
                hard_fail_paths.add(path)
            if status == "EXCLUDED_TECH" and metrics["found_text_candidates"] > 0:
                warnings.append(f"Auditor {number}, {path}: excluded despite candidates")
            # Legacy rows are retained, not silently rewritten. New edits need QA2 proof.
            old_row = previous.get(number, {}).get(path)
            changed = bool(old_row is None or old_row != row)
            qa2 = QA2_RE.search(evidence) is not None
            if base_ref and enforce_changed and changed and not qa2:
                issues.append(f"Auditor {number}, {path}: changed row lacks QA2 evidence")
            if qa2:
                if status != "PASS":
                    issues.extend(f"Auditor {number}, {path}: QA2: {msg}"
                                  for msg in verify_qa2(path, status, row, metrics))
                if metrics["checked_text_candidates"] > metrics["found_text_candidates"]:
                    issues.append(f"Auditor {number}, {path}: checked > found")
                if metrics["failed_text_candidates"] > metrics["checked_text_candidates"]:
                    issues.append(f"Auditor {number}, {path}: failed > checked")
                if metrics["unknown_text_candidates"] > metrics["found_text_candidates"]:
                    issues.append(f"Auditor {number}, {path}: unknown > found")
                if status == "EXCLUDED_TECH" and any(metrics.values()):
                    issues.append(f"Auditor {number}, {path}: technical exclusion has text metrics")
            elif metrics["checked_text_candidates"] > metrics["found_text_candidates"]:
                warnings.append(f"Auditor {number}, {path}: LEGACY checked > found")
            elif metrics["unknown_text_candidates"] > metrics["found_text_candidates"]:
                warnings.append(f"Auditor {number}, {path}: LEGACY unknown > found")
    for path in sorted(all_paths):
        active = {number: ledger[path]["status"]
                  for number, ledger in ledgers.items()
                  if path in ledger and ledger[path]["status"] != "UNINSPECTED"}
        if len(active) > 1:
            normalized = {("UNKNOWN" if s in CONTEXT else s) for s in active.values()}
            if len(normalized) > 1:
                mismatch.append({"path": path, "statuses": active})
    report.update({
        "unique_inspected_sources": len(checked_by_any),
        "untouched_by_all": len(all_paths - checked_by_any),
        "inspected_independently_by_five": len(checked_by_all),
        "unique_source_fails": len(hard_fail_paths),
        "fail_source_paths": sorted(hard_fail_paths),
        "disagreements": mismatch,
        "semantic_pass_source_count": len(semantics_passed),
        "semantic_audit_accepted": False,
        "ux_sprint_unblocked": False,
    })
    return issues, warnings, report


def markdown(report: dict, issues: list[str], warnings: list[str]) -> str:
    if not report:
        return "# Sinbinder text audit: data unavailable\n"
    lines = [
        "# Sinbinder: five independent text audits",
        "",
        "**Not accepted.** A file read, a technical exclusion or CI success is not",
        "a semantic certification. The UX sprint remains paused until direct author approval.",
        "",
        f"- Baseline commit: \`{report['baseline_sha']}\`",
        f"- Sources inventoried: **{report['inventory_sources']}**",
        f"- Touched by at least one auditor: **{report['unique_inspected_sources']}**",
        f"- Touched independently by all five: **{report['inspected_independently_by_five']}**",
        f"- Untouched by anyone: **{report['untouched_by_all']}**",
        f"- Sources with reported FAIL/PARTIAL_FAIL: **{report['unique_source_fails']}**",
        f"- Sources with at least one claimed semantic PASS: **{report['semantic_pass_source_count']}**",
        "",
        "| Auditor | Inspected / 582 | EXCLUDED_TECH | Context pending | FAIL/PARTIAL | PASS |",
        "|---|---:|---:|---:|---:|---:|",
    ]
    for number, a in report["auditors"].items():
        counts = a["counts"]
        context = sum(counts.get(k, 0) for k in CONTEXT)
        lines.append(
            f"| #{number} | {a['inspected_sources']} | {counts.get('EXCLUDED_TECH',0)} "
            f"| {context} | {a['confirmed_or_partial_fail_sources']} "
            f"| {a['semantic_pass_sources']} |"
        )
    if report["fail_source_paths"]:
        lines.extend(["", "## Sources with reported defects", ""])
        lines.extend(f"- \`{path}\`" for path in report["fail_source_paths"])
    if report["disagreements"]:
        lines.extend(["", "## Disagreements for independent review", ""])
        lines.extend(
            f"- \`{item['path']}\`: {item['statuses']}"
            for item in report["disagreements"][:50]
        )
    lines += ["", f"CI errors: **{len(issues)}**; legacy/other warnings: **{len(warnings)}**."]
    return "\n".join(lines) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--output-dir", type=Path, default=Path("/tmp/sinbinder-audit-team"))
    parser.add_argument("--base-ref", default=None)
    parser.add_argument("--enforce-changed", action="store_true")
    args = parser.parse_args()
    issues, warnings, report = validate(args.root, args.base_ref, args.enforce_changed)
    args.output_dir.mkdir(parents=True, exist_ok=True)
    (args.output_dir / "report.json").write_text(
        json.dumps({"report": report, "errors": issues, "warnings": warnings},
                   ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    md = markdown(report, issues, warnings)
    (args.output_dir / "report.md").write_text(md, encoding="utf-8")
    print(md)
    for warning in warnings[:30]:
        print("WARNING:", warning, file=sys.stderr)
    for issue in issues[:50]:
        print("ERROR:", issue, file=sys.stderr)
    if len(issues) > 50:
        print(f"... and {len(issues) - 50} more", file=sys.stderr)
    return 1 if issues else 0


if __name__ == "__main__":
    sys.exit(main())
