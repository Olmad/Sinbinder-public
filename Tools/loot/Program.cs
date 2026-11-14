using System;
using System.Collections.Generic;
using Sinbinder.Gameplay;
using Sinbinder.Core;

static class LootTests
{
    static int asserts;
    static void Check(bool ok, string text)
    {
        if (!ok) throw new Exception("FAIL " + text);
        asserts++;
    }

    public static void Main()
    {
        var items = new List<string> { "Sword", "Helmet", "Belt" };
        var capacity = new List<string>();
        int n = FallenBagTransfer.Move(items, x =>
        {
            if (capacity.Count >= 1) return false;
            capacity.Add(x);
            return true;
        });
        Check(n == 1 && items.Count == 2 && capacity.Count == 1,
            "full inventory must not delete the other two items");
        Check(items[0] == "Helmet" && items[1] == "Belt",
            "rejected items remain in their original order");
        Check(FallenBagTransfer.Move(items, x => false) == 0 && items.Count == 2,
            "rejected transfer remains recoverable");
        Check(FallenBagTransfer.Move(items, x => true) == 2 && items.Count == 0,
            "remaining loot is recoverable later");
        Check(FallenBagTransfer.Move<string>(null, x => true) == 0,
            "missing source safe");

        items = new List<string> { "A", "B", "C" };
        n = FallenBagTransfer.Move(items, x => x != "B");
        Check(n == 2 && items.Count == 1 && items[0] == "B",
            "selective receiver only consumes accepted gear");
        // Greedy soldiers act according to AOS even with the Lord nearby.
        // No player-proximity input exists in FallenBagChoice.Decide.
        Check(FallenBagChoice.Decide(SinType.Greed, MoralType.Neutral, 0f, false)
              == FallenBagIntent.Steal, "greedy soldier steals from stranger");
        Check(FallenBagChoice.Decide(SinType.Greed, MoralType.Vicious, -30f, false)
              == FallenBagIntent.Steal, "greedy soldier steals from rival");
        Check(FallenBagChoice.Decide(SinType.Greed, MoralType.Pious, 0f, false)
              == FallenBagIntent.Steal, "greed can prevail over piety");
        Check(FallenBagChoice.Decide(SinType.Greed, MoralType.Neutral, 25f, false)
              == FallenBagIntent.DeliverToLord, "loyalty to fallen friend takes priority");
        Check(FallenBagChoice.Decide(SinType.Greed, MoralType.Neutral, 0f, true)
              == FallenBagIntent.DeliverToLord, "brotherhood takes priority");
        Check(FallenBagChoice.Decide(SinType.Pride, MoralType.Pious, 0f, false)
              == FallenBagIntent.RespectDead, "pious bystander respects dead");
        Check(FallenBagChoice.Decide(SinType.Wrath, MoralType.Neutral, 0f, false)
              == FallenBagIntent.Leave, "uninvolved soldier leaves bag alone");
        Console.WriteLine($"PASS {asserts}: physical corpse bag transfer and AOS decisions");
    }
}
