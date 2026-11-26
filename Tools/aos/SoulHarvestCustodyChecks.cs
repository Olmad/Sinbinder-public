using System;
using System.IO;
using Sinbinder.Core;

internal static class SoulHarvestCustodyChecks
{
    internal static void Run(Action<bool, string> check, string root)
    {
        Satchel.Forget();
        try
        {
            var first = new SoulData("Same name", SinType.Wrath, MoralType.Neutral, 1);
            var second = new SoulData("Same name", SinType.Greed, MoralType.Neutral, 1);
            check(first.Id != second.Id, "same-name harvest fixtures are distinct");
            check(Satchel.Fill(0, first, SoulQuality.Shock), "reserve the exact first soul");
            check(!Satchel.Fill(0, second, SoulQuality.Acceptance) &&
                  Satchel.At(0).Soul.Id == first.Id && Satchel.At(0).Quality == SoulQuality.Shock,
                "failed destination cannot overwrite another soul");
            check(!Satchel.Fill(-1, second, SoulQuality.Acceptance) &&
                  !Satchel.Fill(Satchel.Jars, second, SoulQuality.Acceptance),
                "nonexistent destination cannot acquire a soul");
            check(Satchel.Fill(1, second, SoulQuality.Acceptance) &&
                  Satchel.At(1).Soul.Id == second.Id && Satchel.At(1).Quality == SoulQuality.Acceptance,
                "second harvest belongs in its chosen jar, not by name or queue position");
            var taken = Satchel.TakeSoul(1);
            check(taken.Soul.Id == second.Id && Satchel.At(1).EmptyJar &&
                  Satchel.At(0).Soul.Id == first.Id,
                "transfer out preserves exact identity and leaves other jar untouched");
        }
        finally { Satchel.Forget(); }

        // Unity manager and harvester are source wiring checks here; Satchel
        // itself is compiled above. No claim of running callback integration.
        string manager = File.ReadAllText(Path.Combine(root, "Assets/Scripts/Gameplay/SoulManager.cs"));
        int begin = manager.IndexOf("private FadingSoul TryHarvestSoul(Vector3 harvesterPosition, System.Func<Kept, bool> store)", StringComparison.Ordinal);
        int end = begin < 0 ? -1 : manager.IndexOf("internal void RemoveFadingSoul", begin, StringComparison.Ordinal);
        string body = begin >= 0 && end > begin ? manager.Substring(begin, end - begin) : "";
        int commit = body.IndexOf("if (kept == null || !store(new Kept(kept, closest.SoulQuality))) return null;", StringComparison.Ordinal);
        int remove = body.IndexOf("_fadingSouls.Remove(closest);", StringComparison.Ordinal);
        int indicator = body.IndexOf("RemoveIndicator(closest);", StringComparison.Ordinal);
        int publish = body.IndexOf("OnSoulHarvested?.Invoke(closest);", StringComparison.Ordinal);
        check(commit >= 0 && remove > commit && indicator > remove && publish > indicator,
            "failed destination preserves fading entry; success commits before event (source)");
        check(manager.Contains("kept => Core.Satchel.Fill(jar, kept.Soul, kept.Quality)"),
            "direct harvest commits exact payload to requested jar (source)");
        string harvester = File.ReadAllText(Path.Combine(root, "Assets/Scripts/Gameplay/SoulHarvester.cs"));
        check(harvester.Contains("souls.TryHarvestIntoJar(transform.position, jar)") &&
              !harvester.Contains("TakeHarvested(souls.Harvested.Count - 1)") &&
              !harvester.Contains("Core.Satchel.Fill("),
            "player harvest cannot take the last shelf entry after callbacks (source)");
        int result = harvester.IndexOf("if (soul == null) return;", StringComparison.Ordinal);
        int credit = harvester.IndexOf("mine.Reputation.Deeds.Add(", StringComparison.Ordinal);
        check(result >= 0 && credit > result, "failed harvest cannot credit a deed (source)");
    }
}
