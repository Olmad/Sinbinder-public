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
        string Read(string path) => File.ReadAllText(Path.Combine(root, path));
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
