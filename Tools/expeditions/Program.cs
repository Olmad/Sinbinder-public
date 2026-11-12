using System;
using Sinbinder.Gameplay;
static class Program
{
    static int assertions;
    static void Check(bool yes, string why)
    {
        if (!yes) throw new Exception("FAIL: " + why);
        assertions++;
    }
    static void Main()
    {
        Check(ExpeditionCollectionPolicy.RemoteSquadCanCollect(ExpeditionFind.Ordinary),
            "auto squads can carry common goods");
        Check(!ExpeditionCollectionPolicy.RemoteSquadCanCollect(ExpeditionFind.Soul),
            "auto squads cannot harvest souls at launch");
        Check(!ExpeditionCollectionPolicy.RemoteSquadCanCollect(ExpeditionFind.Secret),
            "auto squads cannot identify unique secrets at launch");
        Check(ExpeditionCollectionPolicy.CanCollect(ExpeditionFind.Soul, true, false, false),
            "personally present Sinbinder may collect souls");
        Check(ExpeditionCollectionPolicy.CanCollect(ExpeditionFind.Secret, true, false, false),
            "personally present Sinbinder can find secrets");
        Check(!ExpeditionCollectionPolicy.CanCollect(ExpeditionFind.Soul, false, false, true),
            "secret scout does not magically acquire soul collection");
        Check(!ExpeditionCollectionPolicy.CanCollect(ExpeditionFind.Secret, false, true, false),
            "soul collector does not magically discover secrets");
        Check(ExpeditionCollectionPolicy.CanCollect(ExpeditionFind.Soul, false, true, false),
            "future explicitly trained collector can be supported");
        Check(ExpeditionCollectionPolicy.CanCollect(ExpeditionFind.Secret, false, false, true),
            "future explicit scout capability can be supported");
        // The battle occurs at the destination. Cargo is not granted just
        // because one warrior survived the guards.
        Check(ExpeditionObjectiveRules.Secured(false,0,2,2),
            "peaceful AOS resolution can secure the cargo without killing guards");
        Check(!ExpeditionObjectiveRules.Secured(true,1,2,2),
            "survivors do not imply the armed guards were defeated");
        Check(ExpeditionObjectiveRules.Secured(true,2,2,1),
            "actual guarded victory allows ordinary cargo");
        Check(!ExpeditionObjectiveRules.Secured(true,2,2,0),
            "dead squad never delivers defeated-enemy loot");
        Check(!ExpeditionObjectiveRules.Secured(true,0,2,0),
            "total defeat does not fabricate successful assault");
        Check(ExpeditionObjectiveRules.DeliveredCoins(false,50,55)==0,
            "failed mission cannot magic 105 coins into the crypt");
        Check(ExpeditionObjectiveRules.DeliveredCoins(true,15,55)==70,
            "secured mission conserves its original 70 coins");
        Check(ExpeditionObjectiveRules.DeliveredCoins(true,0,0)==0,
            "peaceful passage yields no coins when the cargo rule says zero");
        Console.WriteLine($"PASS: {assertions} mission reward permissions and causal outcomes");
    }
}
