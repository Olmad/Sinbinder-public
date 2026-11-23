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

    def test_tooltip_uses_captured_ballot_not_current_soul(self):
        tooltip = (ROOT / "Assets/Scripts/UI/WarriorTooltipUI.cs").read_text(encoding="utf-8")
        court = (ROOT / "Assets/Scripts/Gameplay/SoulCourt.cs").read_text(encoding="utf-8")
        self.assertIn("originalVotes: wrapper.LastDecisionBallot", tooltip)
        self.assertIn("Sums(w, d, c, originalVotes)", court)
        self.assertRegex(court,
            r"ScalesLine\(Warrior w, Decision d, DecisionContext c, int most = 3,\s*IReadOnlyList<Vote> originalVotes = null\)")

    def test_automatic_court_uses_same_vote_for_importance_and_seats(self):
        caller = (ROOT / "Assets/Scripts/Gameplay/CourtCaller.cs").read_text(encoding="utf-8")
        court = (ROOT / "Assets/Scripts/Gameplay/SoulCourt.cs").read_text(encoding="utf-8")
        hub = (ROOT / "Assets/Scripts/AOS Engine/AOSEventHub.cs").read_text(encoding="utf-8")
        wrapper = (ROOT / "Assets/Scripts/AOS Engine/AOSWarriorWrapper.cs").read_text(encoding="utf-8")

        # The original vote is recorded before a refusal's anger/memory events.
        self.assertLess(wrapper.index("LastDecisionBallot = ballot;"),
                        wrapper.index("AOSEventHub.Instance?.OnCommandRefused"))
        event = hub.split("public void OnCommandRefused(", 1)[1].split(
            "public const float RefusalCooldown", 1)[0]
        self.assertLess(event.index("TriggerEmotion(warrior"),
                        event.index("OnRefusal?.Invoke"))
        self.assertLess(event.index('CreateMemory(\n                warrior, "RefusedCommand"'),
                        event.index("OnRefusal?.Invoke"))

        # A mismatched or unavailable historical ballot must not be faked.
        self.assertIn("mind.HasCurrentCommandDecision", caller)
        self.assertIn("mind.LastDecisionDetail.Equals(d)", caller)
        self.assertIn("new List<Vote>(mind.LastDecisionBallot)", caller)
        self.assertIn("Importance(w, d, c, out string why, originalVotes)", caller)
        self.assertIn("Convene(w, d, c.Copy(), originalVotes: originalVotes)", caller)
        importance = court.split("public static float Importance(", 1)[1].split(
            "private static ActionType Rival(", 1)[0]
        self.assertIn("IReadOnlyList<Vote> originalVotes = null", importance)
        self.assertIn("originalVotes ?? (IReadOnlyList<Vote>)new BehaviourResolver().Ballot(w, c)",
                      importance)

    def test_causal_regressions_detect_missing_provenance(self):
        caller = (ROOT / "Assets/Scripts/Gameplay/CourtCaller.cs").read_text(encoding="utf-8")
        tooltip = (ROOT / "Assets/Scripts/UI/WarriorTooltipUI.cs").read_text(encoding="utf-8")
        self.assertIn("Importance(w, d, c, out string why, originalVotes)", caller)
        self.assertNotIn("Importance(w, d, c, out string why, originalVotes)",
                         caller.replace("Importance(w, d, c, out string why, originalVotes)",
                                        "Importance(w, d, c, out string why)"))
        self.assertIn("originalVotes: wrapper.LastDecisionBallot", tooltip)
        self.assertNotIn("originalVotes: wrapper.LastDecisionBallot",
                         tooltip.replace("originalVotes: wrapper.LastDecisionBallot",
                                         "originalVotes: null"))

if __name__ == "__main__":
    unittest.main()
