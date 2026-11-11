using System;
using System.IO;
using Sinbinder.AOS;

internal static class Program
{
    private static int _passed;

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + description);
        _passed++;
        Console.WriteLine("PASS: " + description);
    }

    private static void Contains(string file, string needle, string description)
    {
        Check(File.ReadAllText(file).Contains(needle), description);
    }

    private static int Main(string[] args)
    {
        // This compiles the EXACT production gate. A voice's repeated vote
        // is not a fresh event and must not rewrite the same memory forever.
        var gate = new RefusalEventGate();
        const int soulA = 1001;
        const int soulB = 1002;
        Check(gate.TryRecord(soulA, 0u, 0f, 4f),
            "initial command loaded from a scene is eligible");
        Check(!gate.TryRecord(soulA, 0u, 1f, 4f),
            "same command cannot write a second refusal in the next tick");
        Check(!gate.TryRecord(soulA, 0u, 4f, 4f),
            "same command cannot write a second refusal at cooldown boundary");
        Check(!gate.TryRecord(soulA, 0u, 120f, 4f),
            "same command cannot repeatedly poison memory minutes later");
        Check(!gate.TryRecord(soulA, 1u, 2f, 4f),
            "spamming distinct orders inside four seconds has no reward");
        Check(gate.TryRecord(soulA, 1u, 4f, 4f),
            "new legitimate order is credited after cooldown");
        Check(!gate.TryRecord(soulA, 1u, 100f, 4f),
            "duplicate rejection persists after successful new refusal");
        Check(gate.TryRecord(soulB, 1u, 4.01f, 4f),
            "different warrior may refuse independently");
        Check(!gate.TryRecord(soulA, 2u, 3f, 4f),
            "clock rollback cannot evade event throttle");
        Check(!gate.TryRecord(soulA, 2u, float.NaN, 4f),
            "NaN does not create fictitious refusal");
        Check(!gate.TryRecord(soulA, 2u, float.PositiveInfinity, 4f),
            "infinite timestamps do not create fictitious refusal");
        Check(!gate.TryRecord(0, 1u, 9f, 4f),
            "unidentified warrior cannot produce a refusal event");
        Check(gate.TryRecord(soulA, 2u, 8f, 4f),
            "new order works after a valid time interval");
        gate.Clear();
        Check(gate.TryRecord(soulA, 0u, 0f, 4f),
            "new runtime session starts with clean event identity");

        // A refusal pause must never launch an old vote after the
        // commander changes the order in the same Unity frame.
        Check(OrderIntegrity.MayExecuteDelayed(5u, 5u),
            "unchanged order permits pending act");
        Check(!OrderIntegrity.MayExecuteDelayed(5u, 6u),
            "new order cancels old pending act");
        Check(!OrderIntegrity.MayExecuteDelayed(5u, 7u),
            "multiple same-frame orders invalidate old act");
        Check(!OrderIntegrity.MayExecuteDelayed(5u, 0u),
            "cancel or reset invalidates stale act");
        Check(OrderIntegrity.MayExecuteDelayed(0u, 0u),
            "initial order identity remains usable");

        Check(OrderIntegrity.ShouldPresentRefusal(false, 0u, 0u),
            "first refusal gets a visible pause");
        Check(!OrderIntegrity.ShouldPresentRefusal(true, 5u, 5u),
            "repeated vote on same order does not spam the refusal overlay");
        Check(OrderIntegrity.ShouldPresentRefusal(true, 5u, 6u),
            "next order gets its own visible refusal");
        Check(!OrderIntegrity.ShouldPresentRefusal(true, 6u, 6u),
            "same-frame repeated vote cannot cause a second overlay");

        // Idle is a real AOS decision, not an unbounded leftover NavMesh
        // journey and not a reason to wander off to the campfire mid-order.
        Check(OrderIntegrity.MayRoamCamp(true, false),
            "autonomous idle can use genuine camp-life routine");
        Check(!OrderIntegrity.MayRoamCamp(true, true),
            "hesitation while commanded cannot start camp movement");
        Check(!OrderIntegrity.MayRoamCamp(false, false),
            "an attack/escape decision cannot start idle camp routine");
        Check(!OrderIntegrity.MayRoamCamp(false, true),
            "active command and non-idle action never trigger camp routine");

        // A whisper is a temporary purchase of obedience to ONE order,
        // not a universal obedience buff lasting twenty seconds.
        Check(OrderIntegrity.WhisperApplies(12u, 12u, true, 25f, 20f),
            "accepted whisper applies to its exact active order");
        Check(OrderIntegrity.WhisperApplies(12u, 12u, true, 25f, 25f),
            "accepted whisper expires at the documented boundary");
        Check(!OrderIntegrity.WhisperApplies(12u, 13u, true, 25f, 21f),
            "changing the order cancels whisper compulsion");
        Check(!OrderIntegrity.WhisperApplies(12u, 12u, false, 25f, 21f),
            "cancelling the order cancels whisper compulsion");
        Check(!OrderIntegrity.WhisperApplies(12u, 12u, true, 25f, 25.001f),
            "old whisper cannot compel after expiration");
        Check(!OrderIntegrity.WhisperApplies(12u, 12u, true, 25f, float.NaN),
            "invalid current time cannot extend a whisper");
        Check(!OrderIntegrity.WhisperApplies(12u, 12u, true, float.PositiveInfinity, 20f),
            "invalid expiration cannot grant eternal obedience");

        // Wire checks are intentionally supplemental to executable tests:
        // they prevent a future refactor from silently bypassing the tested
        // production gate. They do NOT substitute for Unity Play Mode.
        string root = args.Length > 0 ? args[0] : ".";
        string gameplay = Path.Combine(root, "Assets/Scripts/Gameplay/Warrior.cs");
        string hub = Path.Combine(root, "Assets/Scripts/AOS Engine/AOSEventHub.cs");
        string tick = Path.Combine(root, "Assets/Scripts/AOS Engine/AutoAttackAOS.cs");
        Contains(gameplay, "public uint CommandRevision", "Warrior carries a command revision");
        Contains(gameplay, "unchecked { CommandRevision++; }", "issuing and clearing commands update revision");
        Contains(hub, "_refusalEvents.TryRecord(", "real memory event uses checked per-command identity");
        Contains(hub, "warrior.GetInstanceID(), warrior.CommandRevision", "event keyed by runtime identity and revision");
        Contains(tick, "OrderIntegrity.MayExecuteDelayed(", "real AOS tick checks paused command freshness");
        Contains(tick, "yield break;", "stale decision can be discarded without executing");
        Contains(tick, "OrderIntegrity.ShouldPresentRefusal(", "production tick uses single-refusal-pause rule");
        string whisper = Path.Combine(root, "Assets/Scripts/Gameplay/Whisper.cs");
        string aosWrapper = Path.Combine(root, "Assets/Scripts/AOS Engine/AOSWarriorWrapper.cs");
        string ui = Path.Combine(root, "Assets/Scripts/UI/SelectedUnitPanelUI.cs");
        Contains(whisper, "OrderIntegrity.WhisperApplies(", "whisper checks the production per-order guard");
        Contains(whisper, "OrderRevision = agreedOrder", "accepted whisper records exact order identity");
        Contains(aosWrapper, "NaturalDecisionDetail = decision;", "true 13-voice vote is preserved");
        // Source-level wiring guards: no old refusal can be displayed while
        // a new command waits for its first genuine AOS decision.
        string tooltip = Path.Combine(root, "Assets/Scripts/UI/WarriorTooltipUI.cs");
        Contains(aosWrapper, "public uint LastDecisionCommandRevision", "AOS verdict records the order identity");
        Contains(aosWrapper, "LastDecisionCommandRevision = _warrior.CommandRevision;", "each real vote stamps the current command");
        Contains(aosWrapper, "LastDecisionCommandRevision == _warrior.CommandRevision", "verdict freshness compares exact revision");
        Contains(ui, "if (who.HasCommand && !wrapper.HasCurrentCommandDecision)", "selected unit shows pending verdict for a new order");
        Contains(ui, "if (d == null || !d.HasCurrentCommandDecision) return false;", "group refuses only for current order");
        Contains(tooltip, "if (hasDecision && !waitingForCommand)", "tooltip hides old decision while order pending");
        Contains(tooltip, "Новый приказ отдан. Ожидаем решения воина.", "tooltip explains pending status without promising obedience");
        Contains(aosWrapper, "OrderIntegrity.MayRoamCamp(", "real AOS executor uses camp movement gate");
        Contains(aosWrapper, "case ActionType.Idle:", "idle has an explicit execution path");
        Contains(aosWrapper, "GetComponent<UnitMover>()?.Stop();", "idle stops stale NavMesh path");
        Contains(aosWrapper, "ExecuteDefenseAtPost();", "Defend uses the no-pursuit executor");
        Contains(aosWrapper, "distance <= aa.AttackRange", "defensive attack requires an actually reachable enemy");
        Contains(aosWrapper, "LastWhisperCompelled = context.HasCommand", "effective action is labelled as whisper-driven");
        Contains(aosWrapper, "decision.RefusedCommand = false;", "successful persuasion cannot generate fictitious refusal memory");
        Contains(aosWrapper, "_obeyedOrderRevision != _warrior.CommandRevision", "obedience credit distinguishes two same-frame commands");
        Contains(aosWrapper, "if (!LastWhisperCompelled && !string.IsNullOrEmpty(cmd.Lord)", "whisper compulsion cannot farm earned loyalty");
        Contains(whisper, "void ResolveBargain()", "whisper settles only after the soul speaks");
        Contains(whisper, "w.CommandRevision == agreedOrder", "cancelled or changed orders cannot be paid for");
        Contains(whisper, "if (applied)", "false agreement cannot consume money or corrupt the soul");
        string tooltip = Path.Combine(root, "Assets/Scripts/UI/WarriorTooltipUI.cs");
        Contains(tooltip, "wrapper.NaturalDecisionDetail", "court tooltip retains natural thirteen-voice verdict");
        Contains(tooltip, "wrapper.LastWhisperCompelled", "tooltip distinguishes external persuasion from AOS vote");
        Contains(ui, "wrapper.LastWhisperCompelled", "selected warrior panel distinguishes compulsion from loyalty");

        // AOS is a vote of thirteen distinct personality voices, not a
        // tutorial-friendly script or a substitute AI. Guard the actual
        // resolver's constructor registry against accidental truncation.
        string resolver = File.ReadAllText(Path.Combine(root, "Assets/Scripts/AOS Engine/BehaviorResolver.cs"));
        string registry = resolver.Split(new[] { "public BehaviourResolver()" }, StringSplitOptions.None)[1]
            .Split(new[] { "/// <summary>" }, StringSplitOptions.None)[0];
        var voices = new[]
        {
            "GreedModule", "PrideModule", "WrathModule", "EnvyModule",
            "LustModule", "GluttonyModule", "SlothModule", "PatienceModule",
            "FearModule", "LoyaltyModule", "MoralityModule", "MemoryModule", "VirtueModule"
        };
        foreach (var voice in voices)
            Check(registry.Contains("new " + voice + "()"), voice + " is present in production AOS");
        int registrations = System.Text.RegularExpressions.Regex.Matches(registry,
            @"new\s+[A-Za-z]+Module\s*\(\s*\)").Count;
        Check(registrations == 13, "AOS keeps exactly thirteen registered voices");

        Console.WriteLine("PASS: " + _passed + " AOS order-integrity assertions");
        return 0;
    }
}
