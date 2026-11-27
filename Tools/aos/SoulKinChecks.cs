using System;
using System.IO;
using Sinbinder.Core;

internal static class SoulKinChecks
{
    internal static void Run(Action<bool, string> check, string root)
    {
        // Exercise the real SoulDecay.Harvest and SoulData.Kindred, not copies.
        var first = new SoulData("Same name", SinType.Wrath, MoralType.Neutral, 1, 80f);
        var brother = new SoulData("Brother", SinType.Greed, MoralType.Neutral, 1);
        var unrelated = new SoulData("Same name", SinType.Wrath, MoralType.Neutral, 1, 80f);
        var secondBrother = new SoulData("Other brother", SinType.Greed, MoralType.Neutral, 1);
        var unmarked = new SoulData("Brother", SinType.Greed, MoralType.Neutral, 1);
        first.SetKin("pair-a");
        brother.SetKin("pair-a");
        unrelated.SetKin("pair-b");
        secondBrother.SetKin("pair-b");
        check(SoulData.Kindred(first, brother) && SoulData.Kindred(unrelated, secondBrother)
              && !SoulData.Kindred(first, unrelated), "two distinct authored kin groups before harvest");

        foreach (var quality in new[] { SoulQuality.Shock, SoulQuality.Acceptance })
        {
            var harvested = SoulDecay.Harvest(first, quality);
            var otherHarvested = SoulDecay.Harvest(unrelated, quality);
            check(!ReferenceEquals(harvested, first) && harvested.Kin == first.Kin,
                quality + " harvest preserves known kin marker in a new soul instance");
            check(SoulData.Kindred(harvested, brother) &&
                  SoulData.Kindred(otherHarvested, secondBrother) &&
                  !SoulData.Kindred(harvested, otherHarvested) &&
                  !SoulData.Kindred(harvested, unmarked),
                quality + " harvested soul matches only its actual kin group");
            check(first.Kin == "pair-a" && unrelated.Kin == "pair-b",
                quality + " harvest does not mutate source kin");
            check(harvested.Get(SinType.Wrath) == (quality == SoulQuality.Shock ? 80f : 68f),
                quality + " existing spectrum decay is unchanged");
        }

        // Do not define new late-decay semantics in this narrow fix.
        foreach (var quality in new[] { SoulQuality.Fading, SoulQuality.Dissolved })
            check(SoulDecay.Harvest(first, quality).Kin == "",
                quality + " keeps previous late-decay kin behaviour pending author decision");

        check(SoulDecay.Harvest(unmarked, SoulQuality.Shock).Kin == "",
            "unknown legacy kin is not invented");
        check(SoulDecay.Harvest(null, SoulQuality.Shock) == null, "null harvest remains null");
        first.SetKin(null);
        check(first.Kin == "" && !SoulData.Kindred(first, brother), "missing kin normalizes to unknown");

        // Source wiring checks ONLY: SaveSystem is not compiled by this shim.
        // Unity JsonUtility roundtrip and real shelf/hands/device remain runtime gates.
        string dto = File.ReadAllText(Path.Combine(root, "Assets/Scripts/Core/SaveGame.cs"));
        int dtoStart = dto.IndexOf("public class SavedSoul", StringComparison.Ordinal);
        int dtoEnd = dto.IndexOf("public class SavedMember", StringComparison.Ordinal);
        check(dtoStart >= 0 && dtoEnd > dtoStart &&
              dto.Substring(dtoStart, dtoEnd - dtoStart).Contains("public string Kin;"),
            "SavedSoul declares the serialized kin field (source wiring)");
        string save = File.ReadAllText(Path.Combine(root, "Assets/Scripts/Core/SaveSystem.cs"));
        int recordStart = save.IndexOf("public static SavedSoul Record(", StringComparison.Ordinal);
        int soulStart = save.IndexOf("public static SoulData Soul(", StringComparison.Ordinal);
        int soulEnd = save.IndexOf("internal static bool IsValidSoul(", StringComparison.Ordinal);
        check(recordStart >= 0 && soulStart > recordStart && soulEnd > soulStart,
            "actual save mapping methods exist");
        check(save.Substring(recordStart, soulStart - recordStart).Contains("Kin = soul.Kin,"),
            "actual record mapping writes kin (source wiring)");
        check(save.Substring(soulStart, soulEnd - soulStart).Contains("soul.SetKin(s.Kin);"),
            "actual restore mapping reads kin (source wiring)");
    }
}
