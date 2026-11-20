"""Source-linked guard: dream's two courts replay one captured AOS ballot.

Run: python -m unittest Tools/logic/test_dream_court_ballot_replay.py
Does not replace Unity Editor / PlayMode testing.
"""
import pathlib
import re
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Assets/Scripts/Gameplay/DreamTutorialDirector.cs"


def valid_replay(source):
    return all((
        "StartCoroutine(Judge(mind.NaturalDecisionDetail, mind.LastContext," in source,
        "new List<AOS.Vote>(mind.LastDecisionBallot)" in source,
        re.search(r"Judge\(AOS.Decision d, AOS.DecisionContext c,\s*IReadOnlyList<AOS.Vote> originalVotes, bool compelledByWhisper\)", source),
        re.search(r"Convene\(_zombie, d, c, _courtOrder, brief: false,\s*originalVotes: originalVotes, compelledByWhisper: compelledByWhisper, force: true\)", source),
        "OfferShortCourtPreview(d, c, originalVotes, compelledByWhisper)" in source,
        "brief: true, originalVotes: originalVotes, compelledByWhisper: compelledByWhisper, force: true" in source,
        ".Decide(" not in source[source.index("private IEnumerator Judge("):source.index("private void StartBattle(")],
    ))


class DreamCourtBallotReplayTests(unittest.TestCase):
    def test_both_courts_use_original_snapshot(self):
        self.assertTrue(valid_replay(SOURCE.read_text(encoding="utf-8")))

    def test_missing_short_court_ballot_is_detected(self):
        source = SOURCE.read_text(encoding="utf-8")
        changed = source.replace("brief: true, originalVotes: originalVotes, compelledByWhisper:",
                                 "brief: true, compelledByWhisper:")
        self.assertNotEqual(source, changed)
        self.assertFalse(valid_replay(changed))

    def test_missing_snapshot_is_detected(self):
        source = SOURCE.read_text(encoding="utf-8")
        changed = source.replace("new List<AOS.Vote>(mind.LastDecisionBallot)",
                                 "null")
        self.assertNotEqual(source, changed)
        self.assertFalse(valid_replay(changed))


if __name__ == "__main__":
    unittest.main()
