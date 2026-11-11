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
        Console.WriteLine($"PASS: {assertions} mission reward permissions");
    }
}
