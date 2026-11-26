using System;
using System.IO;
using Sinbinder.Core;
using Sinbinder.AOS;

internal static class RosterBiographyChecks
{
    internal static void Run(Action<bool, string> check, string root)
    {
        var source = new MemorySeed
        {
            Object = "sword", Emotion = "grief", Story = "A particular life",
            Trigger = "incident-17", Atoned = 3,
        };
        source.ActionModifiers.Add(new ActionModifier { Action = ActionType.Idle, ScoreBonus = -7f, Blocked = true });
        source.NarrativePerks.Add(new NarrativePerk { PerkName = "Fixture", IsFound = true, RelatedCharacterID = "soul-B" });

        var captured = MemorySeed.Copy(source);
        check(captured != null && !ReferenceEquals(captured, source) &&
              captured.Object == "sword" && captured.Emotion == "grief" &&
              captured.Story == "A particular life" && captured.Trigger == "incident-17" &&
              captured.Atoned == 3, "roster captures the actual biography and atonement");
        check(captured.ActionModifiers.Count == 1 && captured.ActionModifiers[0].Action == ActionType.Idle &&
              captured.ActionModifiers[0].ScoreBonus == -7f && captured.ActionModifiers[0].Blocked &&
              captured.NarrativePerks.Count == 1 && captured.NarrativePerks[0].PerkName == "Fixture" &&
              captured.NarrativePerks[0].IsFound && captured.NarrativePerks[0].RelatedCharacterID == "soul-B",
            "roster captures all modifier and personal perk fields");
        source.Story = "Changed live";
        source.Atoned = 4;
        source.ActionModifiers[0].ScoreBonus = 123f;
        source.NarrativePerks[0].RelatedCharacterID = "wrong";
        check(captured.Story == "A particular life" && captured.Atoned == 3 &&
              captured.ActionModifiers[0].ScoreBonus == -7f &&
              captured.NarrativePerks[0].RelatedCharacterID == "soul-B",
            "live mutation cannot rewrite the captured roster biography");

        int fallbackCalls = 0;
        MemorySeed Legacy()
        {
            fallbackCalls++;
            return new MemorySeed { Story = "Legacy camp description" };
        }
        var spawn = MemorySeed.ForRosterSpawn(captured, true, Legacy);
        var secondSpawn = MemorySeed.ForRosterSpawn(captured, true, Legacy);
        spawn.NarrativePerks[0].IsFound = false;
        spawn.ActionModifiers.Clear();
        check(secondSpawn.NarrativePerks[0].IsFound && captured.NarrativePerks[0].IsFound &&
              secondSpawn.ActionModifiers.Count == 1 && captured.ActionModifiers.Count == 1,
            "two restored souls and the stored roster do not share mutable perk lists");
        check(MemorySeed.ForRosterSpawn(null, true, Legacy) == null && fallbackCalls == 0,
            "explicitly absent captured biography never inherits a same-name camp story");
        check(MemorySeed.ForRosterSpawn(captured, false, Legacy).Story == "A particular life" && fallbackCalls == 0,
            "legacy record with its own seed uses that seed");
        check(MemorySeed.ForRosterSpawn(null, false, Legacy).Story == "Legacy camp description" && fallbackCalls == 1,
            "truly legacy absent seed retains existing fallback");
        var sparse = MemorySeed.Copy(new MemorySeed { ActionModifiers = null, NarrativePerks = null });
        check(sparse.ActionModifiers == null && sparse.NarrativePerks == null &&
              MemorySeed.Copy(null) == null, "null memory and null lists are not fabricated");

        // Source wiring only: actual roster, Unity SaveSystem and scene spawning are not
        // compiled in this shim. Verify that the production callers use the tested helper.
        string Read(string path) => File.ReadAllText(Path.Combine(root, path));
        string roster = Read("Assets/Scripts/Gameplay/SquadRoster.cs");
        int capture = roster.IndexOf("private static Member Capture(Warrior w, Member member)", StringComparison.Ordinal);
        int end = roster.IndexOf("public static void CaptureDeeds", StringComparison.Ordinal);
        string captureBody = capture >= 0 && end > capture ? roster.Substring(capture, end - capture) : "";
        check(captureBody.Contains("member.Seed = MemorySeed.Copy(w.Soul.Memory);") &&
              captureBody.Contains("member.SeedCaptured = true;") &&
              roster.Contains("Capture(warrior, NewMember(warrior))") &&
              roster.Contains("survivors.Add(Capture(w, previous))"),
            "recruitment and scene roster capture use the actual soul (source wiring)");
        string save = Read("Assets/Scripts/Core/SaveSystem.cs");
        check(save.Contains("HasSeed = m.Seed != null,") &&
              save.Contains("Seed = MemorySeed.Copy(m.Seed),") &&
              save.Contains("Seed = m.HasSeed ? MemorySeed.Copy(m.Seed) : null,") &&
              save.Split(new[] { "SeedCaptured = m.SeedCaptured," }, StringSplitOptions.None).Length == 3 &&
              Read("Assets/Scripts/Core/SaveGame.cs").Contains("public bool SeedCaptured;"),
            "roster serialization carries the presence marker and copied seed (source wiring)");
        check(Read("Assets/Scripts/Gameplay/PrologueCampSpawner.cs").Contains(
            "MemorySeed.ForRosterSpawn(member.Seed, member.SeedCaptured, () => LegacyMemory(member))"),
            "camp/crypt scene spawning resolves captured versus legacy memory (source wiring)");
    }
}
