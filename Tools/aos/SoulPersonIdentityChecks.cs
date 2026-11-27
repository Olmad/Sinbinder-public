using System;
using System.IO;
using Sinbinder.Core;

internal static class SoulPersonIdentityChecks
{
    internal static void Run(Action<bool, string> check, string root)
    {
        var first = new SoulData("Same name", SinType.Wrath, MoralType.Neutral, 1, 80f);
        var other = new SoulData("Same name", SinType.Wrath, MoralType.Neutral, 1, 80f);
        check(first.PersonId == first.Id && !SoulData.SamePerson(first, other),
            "new same-name people have distinct identities");
        first.SetKin("pair");
        var death = SoulData.CaptureDeath(first, "");
        var harvested = SoulDecay.Harvest(death, SoulQuality.Shock);
        var secondHarvest = SoulDecay.Harvest(SoulData.CaptureDeath(harvested, ""), SoulQuality.Dissolved);
        check(death.Id == first.Id && harvested.Id != death.Id && secondHarvest.Id != harvested.Id &&
              SoulData.SamePerson(first, death) && SoulData.SamePerson(first, harvested) &&
              SoulData.SamePerson(first, secondHarvest),
            "distinct custody instances retain the original personal identity through repeated deaths");
        check(secondHarvest.Memory == null, "lineage does not invent memories after dissolution");
        check(death.Kin == "pair" && harvested.Kin == "pair",
            "early harvest preserves kin without manual reassignment");
        check(!SoulData.Kindred(first, harvested), "one person with two instance IDs is not two relatives");
        var brother = new SoulData("Brother", SinType.Wrath, MoralType.Neutral, 1);
        brother.SetKin("pair");
        check(SoulData.Kindred(first, brother) && SoulData.Kindred(harvested, brother),
            "genuine distinct relatives still count after harvest");
        other.SetKin("other-pair");
        var otherBrother = new SoulData("Brother", SinType.Wrath, MoralType.Neutral, 1);
        otherBrother.SetKin("other-pair");
        var unmarked = new SoulData("Brother", SinType.Wrath, MoralType.Neutral, 1);
        foreach (var quality in new[] { SoulQuality.Shock, SoulQuality.Acceptance })
        {
            var a = SoulDecay.Harvest(death, quality);
            var b = SoulDecay.Harvest(other, quality);
            check(SoulData.Kindred(a, brother) && SoulData.Kindred(b, otherBrother) &&
                  !SoulData.Kindred(a, b) && !SoulData.Kindred(a, otherBrother) &&
                  !SoulData.Kindred(a, unmarked) && !SoulData.Kindred(a, first),
                quality + " retains exact kin group, rejects namesakes and the same person");
            check(a.Kin == "pair" && b.Kin == "other-pair" &&
                  death.Kin == "pair" && other.Kin == "other-pair",
                quality + " kin transfer leaves original souls unchanged");
            check(a.Get(SinType.Wrath) == (quality == SoulQuality.Shock ? 80f : 68f),
                quality + " spectrum decay unchanged");
        }
        foreach (var quality in new[] { SoulQuality.Fading, SoulQuality.Dissolved })
            check(SoulDecay.Harvest(death, quality).Kin == "",
                quality + " preserves existing late-decay policy pending author decision");
        check(SoulDecay.Harvest(unmarked, SoulQuality.Shock).Kin == "",
            "legacy unknown kin is not invented");
        var restored = SoulData.FromRoster(harvested.Name, harvested.Sin, harvested.Moral,
            harvested.SinIntensity, harvested.Level, harvested.CopySpectra(), null, harvested.Gender,
            harvested.Id, harvested.Kin, harvested.PersonId);
        check(restored.Id == harvested.Id && SoulData.SamePerson(restored, first) &&
              SoulData.Kindred(restored, brother) && !SoulData.Kindred(restored, otherBrother),
            "roster restore keeps custody instance, lineage and kin independently");
        var saved = new SoulData(harvested.Name, harvested.Moral, harvested.Level,
            harvested.CopySpectra(), null, harvested.Gender, harvested.Id, harvested.PersonId);
        check(saved.Id == harvested.Id && SoulData.SamePerson(saved, first), "custody constructor restores both keys");
        var old = new SoulData("Old", MoralType.Neutral, 1, new float[7], soulId: "legacy-id");
        check(old.PersonId == "legacy-id", "legacy ID seeds identity without guessing by name");
        var copy = new SoulData(first);
        check(SoulData.SamePerson(copy, first) && copy.Kin == first.Kin &&
              copy.Id != first.Id && !SoulData.Kindred(first, copy),
            "same-person copy preserves kin mark but never counts as a separate brother");

        Satchel.Forget();
        try
        {
            Satchel.Fill(0, other, SoulQuality.Shock);
            check(Satchel.FindPerson(first) == -1 && !Satchel.HoldsPerson(0, first),
                "a full jar with a namesake cannot satisfy the lesson");
            Satchel.Fill(1, harvested, SoulQuality.Shock);
            check(Satchel.FindPerson(first) == 1 && Satchel.HoldsPerson(1, first) &&
                  !Satchel.HoldsPerson(0, first) && !Satchel.HoldsPerson(-1, first),
                "only the correct harvested person satisfies the lesson");
            Satchel.TakeSoul(1);
            check(Satchel.FindPerson(first) == -1 && Satchel.At(0).FullJar,
                "removing the correct soul cannot turn a foreign jar into a match");
        }
        finally { Satchel.Forget(); }

        string Read(string path) => File.ReadAllText(Path.Combine(root, path));
        string save = Read("Assets/Scripts/Core/SaveSystem.cs");
        check(save.Contains("PersonId = soul.PersonId,") &&
              save.Contains("s.Id, s.PersonId);") &&
              save.Split(new[] { "SoulPersonId = m.SoulPersonId," }, StringSplitOptions.None).Length == 3,
            "custody and roster save mappings retain lineage (source)");
        string dto = Read("Assets/Scripts/Core/SaveGame.cs");
        int dtoStart = dto.IndexOf("public class SavedSoul", StringComparison.Ordinal);
        int dtoEnd = dto.IndexOf("public class SavedMember", StringComparison.Ordinal);
        int writeStart = save.IndexOf("public static SavedSoul Record(", StringComparison.Ordinal);
        int readStart = save.IndexOf("public static SoulData Soul(", StringComparison.Ordinal);
        int readEnd = save.IndexOf("internal static bool IsValidSoul(", StringComparison.Ordinal);
        check(dtoStart >= 0 && dtoEnd > dtoStart &&
              dto.Substring(dtoStart, dtoEnd - dtoStart).Contains("public string Kin;") &&
              writeStart >= 0 && readStart > writeStart && readEnd > readStart &&
              save.Substring(writeStart, readStart - writeStart).Contains("Kin = soul.Kin,") &&
              save.Substring(readStart, readEnd - readStart).Contains("soul.SetKin(s.Kin);"),
            "actual unbound soul DTO and both mappings preserve kin (source only)");
        string lesson = Read("Assets/Scripts/Gameplay/DreamTutorialDirector.cs");
        check(lesson.Contains("Satchel.HoldsPerson(i, RequiredSoul)") &&
              lesson.Contains("Satchel.FindPerson(RequiredSoul)"),
            "tutorial requires exact soul for both progression and selected jar (source)");
    }
}
