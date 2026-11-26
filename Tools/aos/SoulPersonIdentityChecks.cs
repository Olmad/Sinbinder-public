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
        harvested.SetKin("pair");
        check(!SoulData.Kindred(first, harvested), "one person with two instance IDs is not two relatives");
        var brother = new SoulData("Brother", SinType.Wrath, MoralType.Neutral, 1);
        brother.SetKin("pair");
        check(SoulData.Kindred(first, brother), "genuine distinct relatives still count");
        var restored = SoulData.FromRoster(harvested.Name, harvested.Sin, harvested.Moral,
            harvested.SinIntensity, harvested.Level, harvested.CopySpectra(), null, harvested.Gender,
            harvested.Id, harvested.Kin, harvested.PersonId);
        check(restored.Id == harvested.Id && SoulData.SamePerson(restored, first),
            "roster restore keeps custody instance and lineage independently");
        var saved = new SoulData(harvested.Name, harvested.Moral, harvested.Level,
            harvested.CopySpectra(), null, harvested.Gender, harvested.Id, harvested.PersonId);
        check(saved.Id == harvested.Id && SoulData.SamePerson(saved, first), "custody constructor restores both keys");
        var old = new SoulData("Old", MoralType.Neutral, 1, new float[7], soulId: "legacy-id");
        check(old.PersonId == "legacy-id", "legacy ID seeds identity without guessing by name");
        check(SoulData.SamePerson(new SoulData(first), first), "same-person copy retains lineage");

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
        string lesson = Read("Assets/Scripts/Gameplay/DreamTutorialDirector.cs");
        check(lesson.Contains("Satchel.HoldsPerson(i, RequiredSoul)") &&
              lesson.Contains("Satchel.FindPerson(RequiredSoul)"),
            "tutorial requires exact soul for both progression and selected jar (source)");
    }
}
