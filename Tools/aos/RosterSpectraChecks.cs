using System;
using System.IO;
using Sinbinder.Core;

internal static class RosterSpectraChecks
{
    internal static void Run(Action<bool, string> check, string root)
    {
        float[] values = { -100f, 82f, 0f, -16.5f, 100f, 27f, -4f };
        var original = new SoulData("Fixture", MoralType.Neutral, 4, values);
        float[] captured = original.CopySpectra();
        var restored = SoulData.FromRoster("Fixture", original.Sin, original.Moral,
            original.SinIntensity, original.Level, captured, null, original.Gender);
        check(restored.Level == 4 && restored.CopySpectra().AsSpan().SequenceEqual(values),
            "all seven signed spectra and level survive roster reconstruction");
        original.Change(SinType.Greed, 20f);
        restored.Change(SinType.Pride, -15f);
        check(captured.AsSpan().SequenceEqual(values),
            "original and reconstructed soul cannot mutate the captured vector");
        var repeat = SoulData.FromRoster("Fixture", SinType.Greed, MoralType.Neutral,
            -100f, 4, captured, null, Gender.Male);
        check(repeat.CopySpectra().AsSpan().SequenceEqual(values),
            "repeat reconstruction starts from exact saved vector");
        // Distinguish body reconstruction from a genuinely new soul.
        original.SetKin("pair-a");
        var sameSoul = SoulData.FromRoster(original.Name, original.Sin, original.Moral,
            original.SinIntensity, original.Level, captured, null, original.Gender, original.Id, original.Kin);
        var anotherBody = SoulData.FromRoster(original.Name, original.Sin, original.Moral,
            original.SinIntensity, original.Level, captured, null, original.Gender, original.Id, original.Kin);
        check(sameSoul.Id == original.Id && anotherBody.Id == original.Id &&
              !ReferenceEquals(sameSoul, original) && !ReferenceEquals(sameSoul, anotherBody),
            "separate reconstructed objects retain the captured soul identity");
        check(sameSoul.Kin == "pair-a" && anotherBody.Kin == "pair-a",
            "captured kin survives body reconstruction");
        check(!SoulData.Kindred(sameSoul, anotherBody),
            "two objects of the same soul cannot count as two relatives");
        var unrelated = SoulData.FromRoster(original.Name, original.Sin, original.Moral,
            original.SinIntensity, original.Level, captured, null, original.Gender, "unrelated-id", "");
        check(unrelated.Id != sameSoul.Id && !SoulData.Kindred(unrelated, sameSoul),
            "same-name souls do not acquire identity or kin from one another");
        var brother = SoulData.FromRoster("Brother", SinType.Wrath, MoralType.Neutral,
            0f, 1, new float[7], null, Gender.Male, "brother-id", "pair-a");
        check(brother.Id != sameSoul.Id && SoulData.Kindred(brother, sameSoul),
            "two distinct captured souls retain their real kin link");
        var legacyA = SoulData.FromRoster("Same name", SinType.Greed, MoralType.Neutral,
            5f, 0, null, null, Gender.Male);
        var legacyB = SoulData.FromRoster("Same name", SinType.Greed, MoralType.Neutral,
            5f, 0, null, null, Gender.Male);
        check(!string.IsNullOrEmpty(legacyA.Id) && legacyA.Id != legacyB.Id &&
              legacyA.Kin.Length == 0 && legacyB.Kin.Length == 0,
            "legacy missing identity creates distinct souls without guessed kin");
        var neutral = SoulData.FromRoster("Neutral", SinType.Wrath, MoralType.Neutral,
            88f, 1, new float[7], null, Gender.Male);
        check(neutral.CopySpectra().AsSpan().SequenceEqual(new float[7]) && neutral.Sin == SinType.Wrath,
            "captured zero vector is not legacy intensity");
        var legacy = SoulData.FromRoster("Legacy", SinType.Wrath, MoralType.Neutral,
            -42f, 0, null, null, Gender.Male);
        check(legacy.Level == 1 && legacy.Get(SinType.Wrath) == -42f && legacy.Get(SinType.Greed) == 0f,
            "legacy entries retain original single-spectrum creation");
        check(!SoulData.IsValidRosterSpectra(null) && !SoulData.IsValidRosterSpectra(new float[6]) &&
              !SoulData.IsValidRosterSpectra(new float[8]), "missing or wrong-length captured vectors rejected");
        foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, 101f, -101f })
        {
            var invalid = new float[7];
            invalid[3] = bad;
            check(!SoulData.IsValidRosterSpectra(invalid), "reject malformed captured spectrum " + bad);
        }

        // The actual C# soul helper is compiled above; Unity entry points below
        // are source wiring checks, not a simulated scene or JSON round trip.
        string Read(string path) => File.ReadAllText(Path.Combine(root, path)).Replace("\r\n", "\n");
        string roster = Read("Assets/Scripts/Gameplay/SquadRoster.cs");
        check(roster.Contains("member.SoulSpectra = w.Soul.CopySpectra();") &&
              roster.Contains("member.SoulLevel = w.Soul.Level;"),
            "roster captures the already-bound live soul (source)");
        string save = Read("Assets/Scripts/Core/SaveSystem.cs");
        check(save.Contains("HasSoulSpectra = m.SoulSpectra != null,") &&
              save.Contains("SoulSpectra = m.HasSoulSpectra ? (float[])m.SoulSpectra.Clone() : null,") &&
              save.Contains("m.HasSoulSpectra && !SoulData.IsValidRosterSpectra(m.SoulSpectra)"),
            "save has explicit captured-vector presence and validates before restore (source)");
        string spawn = Read("Assets/Scripts/Gameplay/PrologueCampSpawner.cs");
        check(spawn.Contains("bool alreadyBound = member.SoulSpectra != null;") &&
              spawn.Contains("SoulData.FromRoster(member.Name, member.Sin, member.Moral,") &&
              spawn.Contains("if (!alreadyBound) Trades.Apply(soul, member.Trade);") &&
              spawn.Contains("if (alreadyBound)\n                warrior.RestoreBoundBody(") &&
              spawn.Contains("else\n                warrior.Initialize(soul, member.Shell,"),
            "captured and initial spawn routes use different bias paths (source)");
        check(roster.Contains("member.SoulId = w.Soul.Id;") &&
              roster.Contains("member.SoulKin = w.Soul.Kin;") &&
              save.Split(new[] { "SoulId = m.SoulId," }, StringSplitOptions.None).Length == 3 &&
              save.Split(new[] { "SoulKin = m.SoulKin," }, StringSplitOptions.None).Length == 3 &&
              spawn.Contains("member.SoulId, member.SoulKin, member.SoulPersonId);"),
            "identity and kin flow through roster capture, save, load and spawn (source)");
        string expedition = Read("Assets/Scripts/Gameplay/Expedition.cs");
        check(expedition.Contains("SoulData.FromRoster(m.Name, m.Sin, m.Moral, m.Intensity,") &&
              expedition.Contains("m.SoulLevel, m.SoulSpectra, MemorySeed.Copy(m.Seed), m.Gender,") &&
              expedition.Contains("m.SoulId, m.SoulKin, m.SoulPersonId);") &&
              expedition.Contains("soul.SetTrade(m.Trade);") &&
              expedition.Contains("if (m.SoulSpectra != null)\n                w.RestoreBoundBody("),
            "actual expedition uses tested full-soul reconstruction and skips repeated binding (source)");
        string warrior = Read("Assets/Scripts/Gameplay/Warrior.cs");
        int begin = warrior.IndexOf("internal void RestoreBoundBody(", StringComparison.Ordinal);
        int end = begin >= 0 ? warrior.IndexOf("/// <summary>", begin, StringComparison.Ordinal) : -1;
        string body = begin >= 0 && end > begin ? warrior.Substring(begin, end - begin) : "";
        check(body.Contains("InitializeCore(soul, shell, relSystem, isCommander, team, warriorId);") &&
              body.Contains("ApplyShellBody(data);") && !body.Contains("ShellBinder.Bind("),
            "reconstruction applies physical body properties without binding drift (source)");
        check(warrior.Contains("Core.ShellBinder.Bind(soul, data);"),
            "normal binding still applies real shell drift (source)");
    }
}
