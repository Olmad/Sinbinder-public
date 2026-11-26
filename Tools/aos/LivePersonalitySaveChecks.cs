using System;
using System.IO;
using Sinbinder.Core;
using Sinbinder.AOS;

internal static class LivePersonalitySaveChecks
{
    internal static void Run(Action<bool, string> check, string root)
    {
        float[] before = { -45f, 81f, 0f, -20f, 100f, 13f, -3f };
        var seed = new MemorySeed { Story = "before", Atoned = 2 };
        seed.NarrativePerks.Add(new NarrativePerk { PerkName = "past", IsFound = true });
        var live = new SoulData("Warrior", MoralType.Neutral, 4, before, seed);
        live.SetKin("old-kin");
        live.SetTrade(Trade.OrderHunter);
        var captured = SoulData.CaptureDeath(live, "");
        var originalReference = live;
        live.Set(SinType.Wrath, 98f);
        live.Set(SinType.Greed, 100f);
        live.SetKin("future-kin");
        live.SetTrade(Trade.None);
        live.Memory.Story = "future";
        live.Memory.Atoned = 9;
        live.Memory.NarrativePerks[0].IsFound = false;
        check(live.RestoreBoundPersonality(captured, true) &&
              ReferenceEquals(live, originalReference) &&
              live.CopySpectra().AsSpan().SequenceEqual(before) &&
              live.Memory.Story == "before" && live.Memory.Atoned == 2 &&
              live.Memory.NarrativePerks[0].IsFound && live.Kin == "old-kin" &&
              live.Trade == Trade.OrderHunter && live.Level == 4 &&
              live.Id == captured.Id && live.PersonId == captured.PersonId,
            "restore exact bound personality in place without a second binding");
        live.Memory.Story = "edited again";
        check(captured.Memory.Story == "before", "restored memory is independent from saved snapshot");
        check(live.RestoreBoundPersonality(captured, true) &&
              live.CopySpectra().AsSpan().SequenceEqual(before) && live.Memory.Story == "before",
            "repeated restoration does not add spectrum bias");
        var unrelated = new SoulData("Warrior", SinType.Wrath, MoralType.Neutral, 1, 99f);
        var anotherInstance = new SoulData(captured);
        check(!live.RestoreBoundPersonality(unrelated, true) &&
              !live.RestoreBoundPersonality(anotherInstance, true) &&
              live.CopySpectra().AsSpan().SequenceEqual(before),
            "reject namesake and another instance of same person without mutation");
        var absent = SoulData.FromRoster(captured.Name, captured.Sin, captured.Moral,
            captured.SinIntensity, captured.Level, before, null, captured.Gender,
            captured.Id, captured.Kin, captured.PersonId);
        check(live.RestoreBoundPersonality(absent, false) && live.Memory.Story == "before",
            "legacy missing seed does not erase live biography");
        check(live.RestoreBoundPersonality(absent, true) && live.Memory == null,
            "explicitly captured absence clears biography from future");
        check(!live.MatchesBoundIdentity("", captured.PersonId) &&
              !live.MatchesBoundIdentity(captured.Id, "wrong-person") &&
              !live.RestoreBoundPersonality(null, true),
            "missing or conflicting identity cannot restore state");

        string Read(string path) => File.ReadAllText(Path.Combine(root, path));
        string roster = Read("Assets/Scripts/Gameplay/SquadRoster.cs");
        int snapshot = roster.IndexOf("internal static IEnumerable<Member> SaveSnapshot(", StringComparison.Ordinal);
        int end = roster.IndexOf("internal static void SyncEquipment(", StringComparison.Ordinal);
        string snapshotBody = snapshot >= 0 && end > snapshot ? roster.Substring(snapshot, end - snapshot) : "";
        check(snapshotBody.Contains("member = CaptureSoul(warrior, member);") &&
              roster.Contains("member = CaptureSoul(w, member);") &&
              roster.Contains("member.Seed = MemorySeed.Copy(w.Soul.Memory);"),
            "live save and scene transition use the same personality capture (source)");
        string save = Read("Assets/Scripts/Core/SaveSystem.cs");
        int restore = save.IndexOf("private static bool Restore(SaveGame save, bool keepScene)", StringComparison.Ordinal);
        int guard = save.IndexOf("if (keepScene && !CapturedBodiesMatch(save,", restore, StringComparison.Ordinal);
        int mutation = save.IndexOf("Gameplay.CardTable.BeforeSaveOrLoad();", restore, StringComparison.Ordinal);
        check(guard > restore && mutation > guard &&
              save.Contains("!CapturedBodiesMatch(save, LivingSquadIn(SceneManager.GetActiveScene()))") &&
              save.Contains("!m.HasSoulSpectra && (!restoreCryptCondition") &&
              save.Contains("!body.Soul.MatchesBoundIdentity(member.SoulId, member.SoulPersonId)"),
            "modern mismatch requires scene reconstruction, direct restore fails before mutation (source)");
        check(save.Contains("warrior.Soul.RestoreBoundPersonality(captured, member.SeedCaptured || member.Seed != null);"),
            "actual in-place restore uses tested helper and seed presence (source)");
    }
}
