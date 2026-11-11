using System;
using Sinbinder.Gameplay;

internal static class Program
{
    private static int _checks;

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        _checks++;
        Console.WriteLine("PASS: " + name);
    }

    public static int Main()
    {
        // Two-plate crypt puzzle. A distant ally must never activate a seal.
        Check(DreamTutorialChecks.OnSeal(-1.8f, 25.8f, -1.8f, 25.8f), "warrior stands on seal center");
        Check(DreamTutorialChecks.OnSeal(-0.45f, 25.8f, -1.8f, 25.8f), "seal includes 1.35m edge");
        Check(!DreamTutorialChecks.OnSeal(-0.44f, 25.8f, -1.8f, 25.8f), "seal excludes beyond radius");
        Check(!DreamTutorialChecks.OnSeal(-5f, 17f, -1.8f, 25.8f), "distant warrior cannot fake seal occupancy");
        Check(!DreamTutorialChecks.OnSeal(1.8f, 25.8f, -1.8f, 25.8f), "player's separate seal is not warrior's");

        float held = 0f;
        Check(!DreamTutorialChecks.AdvanceSeals(ref held, false, true, true, 4f) && held == 0f,
            "counterweight lock blocks both correct occupants");
        Check(!DreamTutorialChecks.AdvanceSeals(ref held, true, true, false, 0.8f),
            "single occupant cannot count as both");
        Check(!DreamTutorialChecks.AdvanceSeals(ref held, true, true, true, 0.9f),
            "partial hold not enough");
        Check(!DreamTutorialChecks.AdvanceSeals(ref held, true, false, true, 0.1f) && held == 0f,
            "stepping off resets hold");
        Check(!DreamTutorialChecks.AdvanceSeals(ref held, true, true, true, 0.7f),
            "hold must be continuous");
        Check(DreamTutorialChecks.AdvanceSeals(ref held, true, true, true, 0.5f),
            "both held for total 1.2 seconds opens puzzle");
        Check(!DreamTutorialChecks.AdvanceSeals(ref held, false, true, true, 100f),
            "relocking mechanism clears pre-unlock hold");

        // New AOS decision is required after the *specific* order.
        Check(DreamTutorialChecks.FreshOrderedDecision(true, 10f, 10f, true, true),
            "same-frame order is accepted after a new context");
        Check(!DreamTutorialChecks.FreshOrderedDecision(true, 9f, 10f, true, true),
            "old order rejected");
        Check(!DreamTutorialChecks.FreshOrderedDecision(false, 11f, 10f, true, true),
            "wrong target or action rejected");
        Check(!DreamTutorialChecks.FreshOrderedDecision(true, 11f, 10f, false, true),
            "old AOS context rejected");
        Check(!DreamTutorialChecks.FreshOrderedDecision(true, 11f, 10f, true, false),
            "idle vote without command rejected");

        // AOS real protection and cooldown. No grant occurs in the approach path.
        var credit = new RescueCreditGate();
        Check(credit.TryCredit("guard-a:victim-a", 10f), "first verified interception earns credit");
        Check(!credit.TryCredit("guard-a:victim-a", 10.1f), "repeated blow cannot farm rescue credit");
        Check(!credit.TryCredit("guard-a:victim-a", 24.99f), "cooldown enforced to final fraction");
        Check(credit.TryCredit("guard-a:victim-b", 11f), "another victim is a different event");
        Check(credit.TryCredit("guard-b:victim-a", 11f), "another guard is a different event");
        Check(credit.TryCredit("guard-a:victim-a", 25f), "credit allowed at cooldown boundary");
        Check(!credit.TryCredit("guard-a:victim-a", 23f), "clock rollback cannot bypass cooldown");
        Check(!credit.TryCredit("", 30f), "empty incident key rejected");
        Check(!credit.TryCredit("guard-a:victim-a", float.NaN), "invalid time rejected");
        credit.Clear();
        Check(credit.TryCredit("guard-a:victim-a", 0f), "session reset clears earlier credits");

        Console.WriteLine("PASS: " + _checks + " production tutorial rule assertions");
        return 0;
    }
}
