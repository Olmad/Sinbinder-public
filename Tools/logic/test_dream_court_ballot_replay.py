"""Source-linked guard: dream's two courts replay one captured AOS ballot.

Run: python -m unittest Tools/logic/test_dream_court_ballot_replay.py
Does not replace Unity Editor / PlayMode testing.
"""
import pathlib
import re
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Assets/Scripts/Gameplay/DreamTutorialDirector.cs"


# Game branch (24.11): one Convene call serves both dream courts — the first is full,
# later ones brief (`briefCourt = _courts > 0`) — and both replay the same captured ballot.
SHARED_COURT = r"Convene\(_zombie, d, c, _courtOrder, brief: briefCourt,\s*originalVotes: originalVotes, force: true\)"


def valid_replay(source):
    return all((
        "new List<AOS.Vote>(mind.LastDecisionBallot)" in source,
        re.search(r"Judge\(AOS.Decision d, AOS.DecisionContext c,\s*(IReadOnly)?List<AOS.Vote> originalVotes\)", source),
        re.search(SHARED_COURT, source),
        "bool briefCourt = _courts > 0;" in source,
        ".Decide(" not in source[source.index("private IEnumerator Judge("):source.index("private void StartBattle(")],
    ))


class DreamCourtBallotReplayTests(unittest.TestCase):
    def test_both_courts_use_original_snapshot(self):
        self.assertTrue(valid_replay(SOURCE.read_text(encoding="utf-8")))

    def test_missing_short_court_ballot_is_detected(self):
        source = SOURCE.read_text(encoding="utf-8")
        changed = re.sub(r"brief: briefCourt,\s*originalVotes: originalVotes, force: true",
                         "brief: briefCourt, force: true", source)
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
        self.assertIn("mind.IsSourceOfLastCommandDecision(d, c)", caller)
        self.assertIn("private DecisionContext _lastDecisionSourceContext;", wrapper)
        self.assertIn("object.ReferenceEquals(_lastDecisionSourceContext, sourceContext)", wrapper)
        self.assertIn("HasCurrentCommandDecision &&", wrapper)
        self.assertIn("LastDecisionDetail.Equals(decision)", wrapper)
        self.assertLess(wrapper.index("_lastDecisionSourceContext = context;"),
                        wrapper.index("AOSEventHub.Instance?.OnCommandRefused"))
        self.assertIn("new List<Vote>(mind.LastDecisionBallot)", caller)
        self.assertIn("Importance(w, d, c, out string why, originalVotes)", caller)
        self.assertIn("given: mind.LastDecisionOrderKind, originalVotes: originalVotes)", caller)
        self.assertIn("LastDecisionOrderKind = cmd.Kind;", wrapper)
        self.assertIn("CommandKind orderKind = given ?? (System.Enum.TryParse(c.CommandType, out CommandKind recorded)", court)
        self.assertIn("OrderWords(orderKind)", court)
        importance = court.split("public static float Importance(", 1)[1].split(
            "private static ActionType Rival(", 1)[0]
        self.assertIn("IReadOnlyList<Vote> originalVotes = null", importance)
        self.assertIn("originalVotes ?? (IReadOnlyList<Vote>)new BehaviourResolver().Ballot(w, c)",
                      importance)

    def test_automatic_court_keeps_original_order_during_camera_wait(self):
        caller = (ROOT / "Assets/Scripts/Gameplay/CourtCaller.cs").read_text(encoding="utf-8")
        wrapper = (ROOT / "Assets/Scripts/AOS Engine/AOSWarriorWrapper.cs").read_text(encoding="utf-8")
        court = (ROOT / "Assets/Scripts/Gameplay/SoulCourt.cs").read_text(encoding="utf-8")
        self.assertIn("LastDecisionOrderKind = cmd.Kind;", wrapper)
        self.assertIn("given: mind.LastDecisionOrderKind, originalVotes: originalVotes", caller)
        self.assertIn("yield return camera.WaitUntilFree(15f);", court)
        # Game branch: the order is fixed once (given, else the recorded decision's command),
        # never re-read from the live w.Command after the camera wait.
        self.assertIn("CommandKind orderKind = given ?? (System.Enum.TryParse(c.CommandType, out CommandKind recorded)", court)
        self.assertIn("Summon(w, verdict, orderKind)", court)
        self.assertIn("OrderWords(orderKind)", court)
        self.assertNotIn("w.Command.Kind)", court.split("public static IEnumerator Convene(", 1)[1].split(
            "private static string Summon(", 1)[0])
        missing = caller.replace("given: mind.LastDecisionOrderKind, ",
                                 "")
        self.assertNotIn("given: mind.LastDecisionOrderKind, ", missing)

    def test_refusal_event_rejects_same_valued_stale_decisions(self):
        # Two ticks may produce exactly the same Decision on one command.
        # The event's exact context object must match the saved evaluation.
        caller = (ROOT / "Assets/Scripts/Gameplay/CourtCaller.cs").read_text(encoding="utf-8")
        wrapper = (ROOT / "Assets/Scripts/AOS Engine/AOSWarriorWrapper.cs").read_text(encoding="utf-8")
        guard = "mind.IsSourceOfLastCommandDecision(d, c)"
        self.assertIn(guard, caller)
        self.assertNotIn(guard, caller.replace(guard,
            "mind.LastDecisionDetail.Equals(d)"))
        reference = "object.ReferenceEquals(_lastDecisionSourceContext, sourceContext)"
        self.assertIn(reference, wrapper)
        self.assertNotIn(reference,
                         wrapper.replace(reference, "LastContext != null"))

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
