using System;
using Sinbinder.Gameplay;

static class Program
{
    private static int asserts;
    static void Check(bool condition, string reason)
    {
        if (!condition) throw new Exception("FAIL: " + reason);
        asserts++;
    }
    static void Main()
    {
        var bag = new FallenBagRetention();
        Check(bag.OnHomeMap && !bag.Expired && bag.Remaining(999) == 60,
            "bag never times out while player is on original map");
        bag.ChangeMap(false, 100);
        Check(!bag.OnHomeMap && bag.Remaining(135) == 25, "35 seconds away leaves 25");
        bag.ChangeMap(true, 159.9f);
        Check(!bag.Expired && bag.OnHomeMap, "return before 60s preserves bag");
        bag.Tick(1000000);
        Check(!bag.Expired && bag.Remaining(1000000) == 60,
            "returning early means bag will not vanish in front of player");

        // Departing again grants a fresh 60s retrieval window.
        bag.ChangeMap(false, 1000001);
        Check(bag.Remaining(1000010) == 51, "new departure restarts minute");
        bag.ChangeMap(true, 1000060);
        Check(!bag.Expired, "59s after second exit still recoverable");

        bag.ChangeMap(false, 200);
        bag.Tick(259.9f);
        Check(!bag.Expired, "expires at 60 seconds, not 59.9");
        bag.Tick(260);
        Check(bag.Expired, "expires while away after exactly 60 seconds");
        bag.ChangeMap(true, 261);
        Check(bag.Expired && bag.Remaining(261) == 0,
            "cannot be resurrected by returning late");

        var lateReturn = new FallenBagRetention();
        lateReturn.ChangeMap(false, 10);
        lateReturn.ChangeMap(true, 70);
        Check(lateReturn.Expired && !lateReturn.OnHomeMap,
            "late return is rejected before displaying object");

        var resumed = FallenBagRetention.FromSave(false, 12, 1000);
        Check(resumed.Remaining(1000) == 12 && !resumed.Expired,
            "remaining away time survives serialised save");
        resumed.ChangeMap(true, 1011);
        Check(resumed.OnHomeMap && !resumed.Expired,
            "return within remaining saved window preserves bag");
        resumed.Tick(2000);
        Check(!resumed.Expired, "no expiry on map after resumed return");
        var tooLate = FallenBagRetention.FromSave(false, 0, 1000);
        Check(tooLate.Expired, "expired saved bag is not re-created");
        var savedAtHome = FallenBagRetention.FromSave(true, 1, 1000);
        savedAtHome.Tick(8000);
        Check(savedAtHome.OnHomeMap && !savedAtHome.Expired,
            "bag from occupied map does not expire across save/load");
        Console.WriteLine($"PASS {asserts} persistent corpse bag timer assertions");
    }
}
