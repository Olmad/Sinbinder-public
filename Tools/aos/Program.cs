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

        Console.WriteLine("PASS: " + _passed + " AOS order-integrity assertions");
        return 0;
    }
}
