#!/usr/bin/env python3
"""Auditable source-level checklist for all seven Whisper offer/answer pairs.

Default is a truthful report (exit 0). --strict is a BLOCKING acceptance
gate: exits 1 until every pair has an approved source-linked semantic review,
and no known unsupported claim remains. This is NOT a general NLP validator
or a Unity/Player test.

Run: python Tools/logic/check_whisper_semantics.py --strict
"""
from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Assets/Scripts/Gameplay/Whisper.cs"
LOCALE = ROOT / "Assets/Resources/Lang/en.txt"
APPROVALS = ROOT / "Tools/logic/whisper_semantics_approved.json"

KEYS = ("Greed", "Pride", "Wrath", "Envy", "Lust", "Gluttony", "Sloth")
# Confirmed referent/known candidate problems from author + source review.
# Other non-grounded claims require human review even when not on this list.
KNOWN_UNSUPPORTED = {
    "Lust": ("Она смотрит.", "absent referent in demo (author confirmed)"),
}
KNOWN_REVIEW = {
    "Greed": "promises half loot, while implementation charges GreedCoins",
    "Pride": "witnesses are not checked; may be rhetoric",
    "Wrath": "claims a specific mockery event without verifying memory",
    "Envy": "references Kargan without verifying relationship/knowledge",
    "Gluttony": "promises a pot as reward without verifying its existence",
    "Sloth": "guarantees safety until morning without verification",
}
PAIR = re.compile(
    r'^\s*(?:SinType\.(\w+)|(_))\s*=>\s*'
    r'\(Loc\.T\("([^"]+)"\),\s*Loc\.T\("([^"]+)"\)\),?\s*$',
    re.MULTILINE,
)


def extract(source: str) -> dict[str, tuple[str, str, int]]:
    """Fail closed if source format changes: checker must be updated explicitly."""
    start = source.find("private static (string offer, string yes) Words(")
    end = source.find("private IEnumerator Moment(", start + 1)
    if start < 0 or end < 0:
        raise ValueError("Words block not found; review the parser and semantics")
    part = source[start:end]
    out: dict[str, tuple[str, str, int]] = {}
    for m in PAIR.finditer(part):
        key = m.group(1) or ("Sloth" if m.group(2) else "")
        if key not in KEYS or key in out:
            raise ValueError("Unknown/duplicated sin branch: " + key)
        line = source[:start + m.start()].count("\n") + 1
        out[key] = (m.group(3), m.group(4), line)
    if set(out) != set(KEYS):
        raise ValueError("Expected exactly seven branches; found " + repr(sorted(out)))
    # A new dynamic context path needs a new reviewer and an updated checker;
    # do not silently treat an unparsed selector as reviewed.
    return out


def valid_translation(locale: str, phrase: str) -> bool:
    # Do not confuse a translated string with a semantically correct one.
    return bool(re.search(
        r'^msgid "' + re.escape(phrase) + r'"\r?\nmsgstr "[^"]+"',
        locale,
        re.MULTILINE
    ))


def scan(source: str, locale: str, approvals: dict) -> list[dict]:
    rows = []
    for sin, (offer, reply, line) in extract(source).items():
        record = approvals.get(sin, {})
        signature_matches = (
            record.get("offer") == offer and record.get("reply") == reply
        )
        documented = signature_matches and all(
            isinstance(record.get(k), str)
            and len(record[k].strip()) >= 18
            and "TODO" not in record[k].upper()
            for k in ("grounding", "negative_case", "reviewer")
        )
        unsupported = (
            sin in KNOWN_UNSUPPORTED
            and offer == KNOWN_UNSUPPORTED[sin][0]
        )
        translation_ok = valid_translation(locale, offer) and valid_translation(locale, reply)
        if unsupported:
            status = "FAIL_REFERENT"
            note = KNOWN_UNSUPPORTED[sin][1]
        elif not translation_ok:
            status = "FAIL_TRANSLATION"
            note = "RU/EN msgid->msgstr not found for both lines"
        elif not documented:
            status = "UNREVIEWED"
            note = KNOWN_REVIEW.get(sin, "needs grounded semantic reviewer")
        else:
            status = "SOURCE_REVIEWED"
            note = "reviewed source claims; NOT Unity or player validated"
        rows.append({
            "sin": sin, "line": line, "status": status,
            "offer": offer, "reply": reply, "note": note
        })
    return rows


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--strict", action="store_true",
                        help="Fail until every sin is source reviewed and no known lie remains")
    parser.add_argument("--json", action="store_true", help="Emit machine-readable audit")
    args = parser.parse_args()

    try:
        source = SOURCE.read_text(encoding="utf-8")
        locale = LOCALE.read_text(encoding="utf-8")
        document = json.loads(APPROVALS.read_text(encoding="utf-8"))
        if document.get("schema") != 1 or not isinstance(document.get("approved"), dict):
            raise ValueError("Approval manifest must contain schema=1 and approved object")
        rows = scan(source, locale, document["approved"])
    except (OSError, ValueError, json.JSONDecodeError) as e:
        print("SEMANTIC AUDIT FAIL: " + str(e))
        return 2

    if args.json:
        print(json.dumps(rows, ensure_ascii=False, indent=2))
    else:
        print("WHISPER SEMANTIC COVERAGE (source level, NOT Unity verified)")
        for x in rows:
            print(f"{x['sin']:<10} {x['status']:<18} Whisper.cs:{x['line']}: "
                  f"«{x['offer']}» -> «{x['reply']}»")
            if x["status"] != "SOURCE_REVIEWED":
                print("    cause: " + x["note"])
        ok = sum(x["status"] == "SOURCE_REVIEWED" for x in rows)
        print(f"SUMMARY: {ok}/7 SOURCE_REVIEWED, {7 - ok}/7 OPEN; "
              "do not claim dialogue audit complete.")
    if args.strict and any(x["status"] != "SOURCE_REVIEWED" for x in rows):
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
