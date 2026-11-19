#!/usr/bin/env python3
"""Contract tests for the five-ledger QA gate. Pure stdlib."""
import importlib.util
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

MODULE = Path(__file__).resolve().parents[1] / "audit_team_gate.py"
spec = importlib.util.spec_from_file_location("audit_team_gate", MODULE)
gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gate)

SHA = "a" * 40
FIELDS = gate.FIELDS


def row(path, status="UNINSPECTED", *, found=0, checked=0, fail=0, unknown=0, evidence=""):
    values = {
        "path": path, "blob_sha": SHA, "bytes": "44", "audit_class": "CS",
        "found_text_candidates": str(found),
        "checked_text_candidates": str(checked),
        "failed_text_candidates": str(fail),
        "unknown_text_candidates": str(unknown),
        "status": status, "evidence": evidence,
    }
    return "\t".join(values[key] for key in FIELDS)


def ledger(number, rows):
    return (f"# INDEPENDENT_TEXT_AUDIT_PASS={number} BASE_SHA={SHA}\n"
            + "\t".join(FIELDS) + "\n" + "\n".join(rows) + "\n")


class GateTests(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.TemporaryDirectory()
        self.addCleanup(self.dir.cleanup)
        self.root = Path(self.dir.name)
        self.paths = ["Assets/Scripts/A.cs", "Assets/Resources/Lang/en.txt"]
        self.payload = [row(path) for path in self.paths]
        for idx in range(1, 6):
            self.write(idx, self.payload)

    def write(self, number, rows):
        path = self.root / gate.ledger_path(number)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(ledger(number, rows), encoding="utf-8")

    def check(self, **kwargs):
        with patch.object(gate, "SOURCE_COUNT", 2):
            return gate.validate(self.root, **kwargs)

    def test_clean_five_way_baseline(self):
        errors, warnings, report = self.check()
        self.assertEqual(errors, [])
        self.assertEqual(warnings, [])
        self.assertEqual(report["untouched_by_all"], 2)
        self.assertEqual(report["inspected_independently_by_five"], 0)
        self.assertFalse(report["semantic_audit_accepted"])

    def test_excluded_and_unknown_do_not_count_as_pass(self):
        for i in range(1, 6):
            self.write(i, [
                row(self.paths[0], "EXCLUDED_TECH", evidence="Full code read blob: aaaa line 12; no literals"),
                row(self.paths[1], "UNKNOWN", found=1, checked=1, unknown=1,
                    evidence="Located phrase at en.txt line 1; real condition UNKNOWN"),
            ])
        errors, warnings, report = self.check()
        self.assertEqual(errors, [])
        self.assertEqual(report["inspected_independently_by_five"], 2)
        self.assertEqual(report["semantic_pass_source_count"], 0)

    def test_unsubstantiated_pass_rejected(self):
        self.write(2, [row(self.paths[0], "PASS", found=1, checked=1,
                           evidence="read file line 7, looks good"),
                       self.payload[1]])
        errors, _, report = self.check()
        self.assertTrue(any("PASS has no QA2" in msg for msg in errors))
        self.assertEqual(report["semantic_pass_source_count"], 0)

    def test_wrong_inventory_is_error(self):
        self.write(3, [self.payload[0]])
        errors, _, _ = self.check()
        self.assertTrue(any("source list diverges" in msg for msg in errors))

    def test_duplicate_path_rejected(self):
        self.write(4, [self.payload[0], self.payload[0]])
        errors, _, _ = self.check()
        self.assertTrue(any("duplicate path" in msg for msg in errors))

    def test_invalid_legacy_counts_warn_but_preserve_history(self):
        self.write(1, [
            row(self.paths[0], "UNKNOWN", found=1, checked=3, unknown=1,
                evidence="read Assets/Scripts/A.cs line 12; context pending"),
            self.payload[1],
        ])
        errors, warnings, _ = self.check()
        self.assertEqual(errors, [])
        self.assertTrue(any("LEGACY checked > found" in w for w in warnings))

    def test_new_invalid_metrics_rejected(self):
        old = ledger(1, self.payload)
        self.write(1, [
            row(self.paths[0], "UNKNOWN", found=1, checked=3, unknown=1,
                evidence="QA2: source Assets/Scripts/A.cs blob aaa L12; context pending"),
            self.payload[1],
        ])
        with patch.object(gate, "previous_content", side_effect=lambda root, ref, path:
                          old if path == gate.ledger_path(1) else
                          (self.root / path).read_text(encoding="utf-8")):
            errors, _, _ = self.check(base_ref="HEAD^", enforce_changed=True)
        self.assertTrue(any("checked > found" in msg for msg in errors))

    def test_changed_row_must_have_qa2_evidence(self):
        old = ledger(1, self.payload)
        self.write(1, [
            row(self.paths[0], "UNKNOWN", found=1, checked=1, unknown=1,
                evidence="Read Assets/Scripts/A.cs line 12, context not known"),
            self.payload[1],
        ])
        with patch.object(gate, "previous_content", side_effect=lambda root, ref, path:
                          old if path == gate.ledger_path(1) else
                          (self.root / path).read_text(encoding="utf-8")):
            errors, _, _ = self.check(base_ref="HEAD^", enforce_changed=True)
        self.assertTrue(any("lacks QA2" in msg for msg in errors))

    def test_new_qa2_unknown_accepted(self):
        old = ledger(1, self.payload)
        self.write(1, [
            row(self.paths[0], "UNKNOWN", found=1, checked=1, unknown=1,
                evidence="QA2: source=Assets/Scripts/A.cs L12 blob=aaa; "
                         "context=referent unknown; negative=missing target"),
            self.payload[1],
        ])
        with patch.object(gate, "previous_content", side_effect=lambda root, ref, path:
                          old if path == gate.ledger_path(1) else
                          (self.root / path).read_text(encoding="utf-8")):
            errors, warnings, _ = self.check(base_ref="HEAD^", enforce_changed=True)
        self.assertEqual(errors, [])

    def test_independent_disagreement_is_reported(self):
        self.write(1, [
            row(self.paths[0], "FAIL", found=1, checked=1, fail=1,
                evidence="Read source Assets/Scripts/A.cs line 12, Build(null) false claim"),
            self.payload[1],
        ])
        self.write(2, [
            row(self.paths[0], "UNKNOWN", found=1, checked=1, unknown=1,
                evidence="Read source Assets/Scripts/A.cs line 12, conditions unknown"),
            self.payload[1],
        ])
        errors, warnings, report = self.check()
        self.assertEqual(errors, [])
        self.assertEqual(len(report["disagreements"]), 1)
        self.assertEqual(report["unique_source_fails"], 1)


if __name__ == "__main__":
    unittest.main()
