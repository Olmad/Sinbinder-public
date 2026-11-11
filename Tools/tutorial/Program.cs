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

        // A rescue requires an actual positive hit from a real attacker.
        Check(RescueCreditGate.IsRealAttack(2.1f, true), "real attacker and positive hit qualify");
        Check(!RescueCreditGate.IsRealAttack(0f, true), "zero-damage VFX cannot earn rescue credit");
        Check(!RescueCreditGate.IsRealAttack(2.1f, false), "missing attacker cannot earn rescue credit");
        Check(!RescueCreditGate.IsRealAttack(-1f, true), "negative healing cannot earn rescue credit");
        Check(!RescueCreditGate.IsRealAttack(float.NaN, true), "invalid damage cannot earn rescue credit");
        Check(!RescueCreditGate.IsRealAttack(float.PositiveInfinity, true), "infinite damage cannot earn rescue credit");

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

        // Explain an AOS refusal in a way a player can act on. These are
        // actual gameplay copy strings, not CI-only duplicates.
        Check(OrderAdvice.Next("Distance").Contains("ближе"), "distance refusal suggests moving closer");
        Check(OrderAdvice.Next("Debt").Contains("долг"), "unpaid wages suggest resolving the debt");
        Check(OrderAdvice.Next("Temptation").Contains("снаряжение"), "tempting item suggests changing equipment");
        Check(OrderAdvice.Next("AllyInDanger").Contains("товарища"), "ally in danger suggests rescue first");
        Check(OrderAdvice.Next("Fatigue").Contains("передышку"), "fatigue suggests a rest or replacement");
        Check(OrderAdvice.Next("Surrounded").Contains("окружения"), "encirclement suggests reducing danger");
        Check(OrderAdvice.Next("Murderer").Contains("убийц"), "revenge suggests removing remembered threat");
        Check(OrderAdvice.Next(null, "Fear").Contains("опасность"), "fear voice suggests safer circumstances");
        Check(OrderAdvice.Next(null, "Memory").Contains("прошлом"), "memory voice suggests changing reminder");
        Check(OrderAdvice.Next(null, "Morality").Contains("убеждений"), "conscience voice suggests another order");
        Check(OrderAdvice.Next(null, "Pride").Contains("задачу"), "pride voice suggests a different assignment");
        Check(!string.IsNullOrWhiteSpace(OrderAdvice.Next("Unknown", "Unknown")),
            "unrecognized refusal still gives a non-guaranteed alternative");
        Check(OrderAdvice.Short("Distance") == "Подойдите ближе",
            "distance has a readable one-line command-panel hint");
        Check(OrderAdvice.Short("Debt") == "Выплатите долг",
            "debt has a readable one-line command-panel hint");
        Check(OrderAdvice.Short("Temptation").Contains("снаряжение"),
            "temptation short hint exposes a real player lever");
        Check(OrderAdvice.Short(null, "Memory").Length <= 40,
            "memory hint fits the one-line lower strip");
        Check(OrderAdvice.Short("Unknown", "Unknown").Length <= 40,
            "fallback hint is short enough for the lower strip");

        Console.WriteLine("PASS: " + _checks + " production tutorial rule assertions");
        return 0;
    }
}
