"""Self-tests of the Whisper semantic review gate (not a Unity player test).

These tests verify that the checker detects an explicit false referent, stale
approvals and incomplete branch inventory. They deliberately do NOT assert
that the current gameplay dialogue has passed semantic review.
"""
import sys
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

from check_whisper_semantics import (  # noqa: E402
    ROOT, SOURCE, LOCALE, KEYS, extract, scan,
)


class WhisperSemanticsGateTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.source = SOURCE.read_text(encoding="utf-8")
        cls.locale = LOCALE.read_text(encoding="utf-8")
        cls.pairs = extract(cls.source)

    def test_seven_sin_routes_are_inventoried(self):
        self.assertEqual(set(KEYS), set(self.pairs))

    def test_no_review_is_not_silently_accepted(self):
        self.assertTrue(all(
            item["status"] != "SOURCE_REVIEWED"
            for item in scan(self.source, self.locale, {})
        ))

    def test_confirmed_false_referent_cannot_be_self_approved(self):
        if self.pairs["Lust"][0] != "Она смотрит.":
            # The production fix may already have replaced the old text.
            legacy = self.source.replace(
                self.pairs["Lust"][0], "Она смотрит.", 1
            )
        else:
            legacy = self.source
        entries = extract(legacy)
        fake = {
            k: {
                "offer": v[0], "reply": v[1],
                "grounding": "Claim reviewed against active scene and facts.",
                "negative_case": "This string lacks a visible referent in demo.",
                "reviewer": "Independent QA reviewer"
            }
            for k, v in entries.items()
        }
        statuses = {x["sin"]: x["status"] for x in scan(legacy, self.locale, fake)}
        self.assertEqual("FAIL_REFERENT", statuses["Lust"])

    def test_stale_approval_cannot_pass_after_copy_change(self):
        good = {
            k: {
                "offer": v[0], "reply": v[1],
                "grounding": "Verified against currently available world state.",
                "negative_case": "No corresponding entity in current scene.",
                "reviewer": "Independent QA reviewer"
            }
            for k, v in self.pairs.items()
        }
        altered = self.source.replace(
            self.pairs["Lust"][0], "Новая недокументированная реплика", 1
        )
        items = scan(altered, self.locale, good)
        lust = next(x for x in items if x["sin"] == "Lust")
        self.assertNotEqual("SOURCE_REVIEWED", lust["status"])

    def test_new_sin_route_cannot_escape_inventory(self):
        altered = self.source.replace(
            "SinType.Lust     =>", "SinType.Unknown  =>", 1
        )
        self.assertNotEqual(altered, self.source)
        with self.assertRaises(ValueError):
            extract(altered)


if __name__ == "__main__":
    unittest.main()
