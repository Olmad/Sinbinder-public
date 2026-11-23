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
        "new List<AOS.Vote>(mind.LastDecisionBallot)" in source,
        re.search(r"Judge\(AOS.Decision d, AOS.DecisionContext c,\s*IReadOnlyList<AOS.Vote> originalVotes\)", source),
        re.search(r"Convene\(_zombie, d, c, _courtOrder, brief: false,\s*originalVotes: originalVotes, force: true\)", source),
        re.search(r"OfferShortCourtPreview\(d, c, originalVotes\)", source),
        re.search(r"brief: true, originalVotes: originalVotes, force: true", source),
        ".Decide(" not in source[source.index("private IEnumerator Judge("):source.index("private void StartBattle(")],
    ))


class DreamCourtBallotReplayTests(unittest.TestCase):
    def test_both_courts_use_original_snapshot(self):
        self.assertTrue(valid_replay(SOURCE.read_text(encoding="utf-8")))

    def test_missing_short_court_ballot_is_detected(self):
        source = SOURCE.read_text(encoding="utf-8")
        changed = source.replace("brief: true, originalVotes: originalVotes, force: true",
                                 "brief: true, force: true")
        self.assertNotEqual(source, changed)
        self.assertFalse(valid_replay(changed))

    def test_missing_snapshot_is_detected(self):
        source = SOURCE.read_text(encoding="utf-8")
        changed = source.replace("new List<AOS.Vote>(mind.LastDecisionBallot)",
                                 "null")
        self.assertNotEqual(source, changed)
        self.assertFalse(valid_replay(changed))



    def test_overhead_scales_use_the_original_aos_vote(self):
        # The brief scales preceding the court must agree with the court.
        # No second Tally after HP, emotion or squad modifiers change.
        dream = SOURCE.read_text(encoding="utf-8")
        scales = (ROOT / "Assets/Scripts/UI/ScalesOverhead.cs").read_text(encoding="utf-8")
        court = (ROOT / "Assets/Scripts/Gameplay/SoulCourt.cs").read_text(encoding="utf-8")
        wrapper = (ROOT / "Assets/Scripts/AOS Engine/AOSWarriorWrapper.cs").read_text(encoding="utf-8")

        self.assertRegex(dream,
            r"ScalesOverhead\.Show\(_zombie, d, c, force: true,\s*originalVotes: originalVotes\)")
        self.assertIn("LastDecisionBallot = ballot;", wrapper)
        self.assertIn("object.ReferenceEquals(mind.LastContext, c)", scales)
        self.assertIn("mind.LastDecisionDetail.Equals(d)", scales)
        self.assertIn("originalVotes = mind.LastDecisionBallot;", scales)
        self.assertIn("SoulCourt.Lean(w, d, c, out float lean, out string voice, out Color colour,",
                      scales)
        self.assertIn("originalVotes)) return;", scales)
        self.assertIn("Sums(w, d, c, originalVotes)", court)
        self.assertIn("originalVotes ?? (IReadOnlyList<Vote>)new BehaviourResolver().Ballot(w, c)",
                      court)

    def test_missing_overhead_snapshot_is_detected(self):
        source = SOURCE.read_text(encoding="utf-8")
        original = "originalVotes: originalVotes);"
        # This mutation removes only the small-scales snapshot, leaving the
        # full court's originalVotes intact.
        anchor = "UI.ScalesOverhead.Show(_zombie, d, c, force: true,"
        self.assertIn(anchor, source)
        mutated = source.replace(anchor + "\n                " + original,
                                 "UI.ScalesOverhead.Show(_zombie, d, c, force: true);")
        self.assertNotEqual(source, mutated)
        self.assertNotRegex(mutated,
            r"ScalesOverhead\.Show\(_zombie, d, c, force: true,\s*originalVotes: originalVotes\)")

if __name__ == "__main__":
    unittest.main()
