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

        // Bodyguard's one-shot protection must survive a zero-damage probe
        // for BOTH ordinary Guard and AOS GuardForRescue. These are source
        // wiring assertions, not Unity execution or proof of a real hit.
        string bodyguardSource = File.ReadAllText(Path.Combine(args.Length > 0 ? args[0] : ".",
            "Assets/Scripts/Gameplay/Bodyguard.cs"));
        int absorbStart = bodyguardSource.IndexOf("public static bool Absorb(", StringComparison.Ordinal);
        int absorbEnd = bodyguardSource.IndexOf("public static int Blocks", absorbStart, StringComparison.Ordinal);
        Check(absorbStart >= 0 && absorbEnd > absorbStart,
            "production bodyguard interception path exists");
        string absorbSource = bodyguardSource.Substring(absorbStart, absorbEnd - absorbStart);
        int realHitGate = absorbSource.IndexOf("if (!RescueCreditGate.IsRealAttack(damage, attacker != null)) return false;", StringComparison.Ordinal);
        int consumeWatch = absorbSource.IndexOf("Watched.Remove(target);", StringComparison.Ordinal);
        Check(realHitGate >= 0 && consumeWatch > realHitGate,
            "zero damage or missing attacker cannot consume ordinary or rescue watch");
        Check(!absorbSource.Contains("if (w.Rescue && !RescueCreditGate.IsRealAttack"),
            "real-hit guard is not limited to AOS rescue");
        Check(absorbSource.Contains("guardBody.TakeDamage(damage * Blocked, attacker);") &&
              absorbSource.Contains("Blocks++;") && absorbSource.Contains("return true;"),
            "real attack still follows the original interception and credit path");

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
        // Group UI must distinguish an undeliberated order from a genuine
        // non-refusal; otherwise "in formation" implies obedience prematurely.
        string panelSource = File.ReadAllText(ui);
        int groupStart = panelSource.IndexOf("private void Group(Warrior shown)", StringComparison.Ordinal);
        int groupEnd = panelSource.IndexOf("private static bool Disagrees(Warrior w)", groupStart, StringComparison.Ordinal);
        Check(groupStart >= 0 && groupEnd > groupStart, "group verdict summary exists in production panel");
        string groupSource = panelSource.Substring(groupStart, groupEnd - groupStart);
        int pendingBranch = groupSource.IndexOf("if (w.HasCommand && (d == null || !d.HasCurrentCommandDecision))", StringComparison.Ordinal);
        int disagreementBranch = groupSource.IndexOf("else if (d != null && d.HasCurrentCommandDecision && Disagrees(w))", StringComparison.Ordinal);
        int calmBranch = groupSource.IndexOf("else calm++;", StringComparison.Ordinal);
        Check(pendingBranch >= 0 && disagreementBranch > pendingBranch && calmBranch > disagreementBranch,
            "pending AOS decisions are counted separately before refused and settled squad members");
        Check(groupSource.Contains("pending++;") && groupSource.Contains("ожидают решения") &&
              groupSource.Contains("if (calm > 0)") && groupSource.Contains("в строю"),
            "pending and settled squad summaries have distinct readable labels");
        Contains(tooltip, "if (hasDecision && !waitingForCommand)", "tooltip hides old decision while order pending");
        Contains(tooltip, "Новый приказ отдан. Ожидаем решения воина.", "tooltip explains pending status without promising obedience");
        // Neutral match warriors have an attitude prefix. It must not hide
        // the first pending AOS verdict when no LastContext exists yet.
        Contains(tooltip, "hasDecision || waitingForCommand ? \"\\n\" + _text.text : string.Empty",
            "neutral warrior tooltip retains first-order pending feedback alongside attitude");
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
        // Positive: the existing refusal/hesitation branch still permits a
        // current, genuinely evaluated order. Negative: no whisper while a
        // new command has not yet received its own thirteen-voice verdict.
        string whisperSource = File.ReadAllText(whisper);
        int canStart = whisperSource.IndexOf("private static bool Can(Warrior w, bool anyDistance)", StringComparison.Ordinal);
        int canEnd = whisperSource.IndexOf("private static Whisper _one;", canStart, StringComparison.Ordinal);
        Check(canStart >= 0 && canEnd > canStart, "whisper eligibility production method exists");
        string canSource = whisperSource.Substring(canStart, canEnd - canStart);
        int freshness = canSource.IndexOf("if (!mind.HasCurrentCommandDecision) return false;", StringComparison.Ordinal);
        int refusal = canSource.IndexOf("return d.Hesitated || d.RefusedCommand;", StringComparison.Ordinal);
        Check(freshness >= 0 && refusal > freshness,
            "stale command is rejected before considering refusal or hesitation");
        Check(canSource.Contains("if (mind == null || !w.HasCommand) return false;"),
            "current command and AOS wrapper remain mandatory for whisper eligibility");
        // The same tooltip file also exposes the natural verdict versus whisper compulsion.
        // Hovering a door/throne must use the real cursor even if QA has pinned a warrior.
        // Positive: sturdy tooltip bypasses the pinned-warrior projection.
        // Negative: warrior tooltip keeps the pinned placement used for QA.
        string tooltipSource = File.ReadAllText(tooltip);
        int sturdyMethod = tooltipSource.IndexOf("private void ShowSturdy(Sturdy sturdy)", StringComparison.Ordinal);
        int refreshMethod = tooltipSource.IndexOf("private void Refresh(Warrior warrior)", sturdyMethod, StringComparison.Ordinal);
        Check(sturdyMethod >= 0 && refreshMethod > sturdyMethod, "sturdy tooltip method exists");
        string sturdySource = tooltipSource.Substring(sturdyMethod, refreshMethod - sturdyMethod);
        Check(sturdySource.Contains("PlaceNearPointer(actualMouse: true);"),
            "sturdy tooltip uses actual hovered cursor despite a pinned warrior");
        Check(tooltipSource.Contains("Vector2 pointer = actualMouse ? (Vector2)Input.mousePosition : Pointer();"),
            "tooltip placement supports separate real cursor and pinned QA anchor");
        Check(tooltipSource.Contains("void Update()") &&
              tooltipSource.Substring(tooltipSource.IndexOf("void Update()", StringComparison.Ordinal),
                  tooltipSource.IndexOf("private void PlaceNearPointer(", StringComparison.Ordinal) -
                  tooltipSource.IndexOf("void Update()", StringComparison.Ordinal))
                  .Contains("PlaceNearPointer();"),
            "warrior tooltip retains default pinned positioning");

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
