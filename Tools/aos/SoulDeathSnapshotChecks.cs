using System;
using System.IO;
using Sinbinder.Core;
using Sinbinder.AOS;

internal static class SoulDeathSnapshotChecks
{
    internal static void Run(Action<bool, string> check, string root)
    {
        var seed = new MemorySeed { Story = "life before death", Trigger = "event", Atoned = 2 };
        seed.ActionModifiers.Add(new ActionModifier { Action = ActionType.Idle, ScoreBonus = -7f });
        seed.NarrativePerks.Add(new NarrativePerk { PerkName = "test", IsFound = true, RelatedCharacterID = "soul-b" });
        var source = new SoulData("Warrior", MoralType.Neutral, 4,
            new float[] { 80f, -40f, 20f, -20f, 0f, 60f, -80f }, seed);
        source.SetTrade(Trade.OrderHunter);
        source.SetKin("pair-a");
        source.Remember("old title");
        var snapshot = SoulData.CaptureDeath(source, "title at death");
        check(snapshot.Id == source.Id && !ReferenceEquals(snapshot, source) &&
              snapshot.Level == 4 && snapshot.Trade == Trade.OrderHunter && snapshot.Kin == "pair-a" &&
              snapshot.EarnedTitle == "title at death" && source.EarnedTitle == "old title",
            "death snapshot retains identity, trade, kin, level and actual death title without owning another soul");
        float[] before = snapshot.CopySpectra();
        source.Set(SinType.Greed, -100f);
        source.SetKin("different pair");
        source.SetTrade(Trade.None);
        source.Remember("changed corpse");
        seed.Story = "changed corpse";
        seed.ActionModifiers[0].ScoreBonus = 99f;
        seed.NarrativePerks[0].RelatedCharacterID = "wrong";
        check(snapshot.CopySpectra().AsSpan().SequenceEqual(before) &&
              snapshot.Kin == "pair-a" && snapshot.Trade == Trade.OrderHunter &&
              snapshot.EarnedTitle == "title at death" && snapshot.Memory.Story == "life before death" &&
              snapshot.Memory.ActionModifiers[0].ScoreBonus == -7f &&
              snapshot.Memory.NarrativePerks[0].RelatedCharacterID == "soul-b",
            "mutation of corpse after death cannot rewrite historical snapshot");

        var shock = SoulDecay.Harvest(snapshot, SoulQuality.Shock);
        var accepted = SoulDecay.Harvest(snapshot, SoulQuality.Acceptance);
        var fading = SoulDecay.Harvest(snapshot, SoulQuality.Fading);
        var dissolved = SoulDecay.Harvest(snapshot, SoulQuality.Dissolved);
        check(shock.CopySpectra().AsSpan().SequenceEqual(before) && shock.Memory.Story == "life before death" &&
              shock.EarnedTitle == "title at death", "fresh harvest uses frozen death state");
        check(accepted.CopySpectra().AsSpan().SequenceEqual(new float[] { 68f, -34f, 17f, -17f, 0f, 51f, -68f }) &&
              accepted.Memory.Story == "life before death" && accepted.EarnedTitle == "title at death",
            "Acceptance retains biography with the existing 0.85 decay factor");
        check(fading.CopySpectra().AsSpan().SequenceEqual(new float[] { 44f, -22f, 11f, -11f, 0f, 33f, -44f }) &&
              fading.Memory != null && fading.Memory.Story == "" && fading.Memory.NarrativePerks.Count == 1 &&
              fading.EarnedTitle == "", "Fading retains only existing perk subset and 0.55 spectra");
        check(dissolved.CopySpectra().AsSpan().SequenceEqual(new float[] { 16f, -8f, 4f, -4f, 0f, 12f, -16f }) &&
              dissolved.Memory == null && dissolved.EarnedTitle == "",
            "Dissolved keeps the existing 0.20 spectrum factor and loses biography");
        shock.Memory.Story = "changed collected soul";
        shock.Memory.ActionModifiers[0].ScoreBonus = 100f;
        fading.Memory.NarrativePerks[0].RelatedCharacterID = "wrong after harvest";
        check(snapshot.Memory.Story == "life before death" &&
              snapshot.Memory.ActionModifiers[0].ScoreBonus == -7f &&
              snapshot.Memory.NarrativePerks[0].RelatedCharacterID == "soul-b" &&
              accepted.Memory.Story == "life before death" &&
              accepted.Memory.NarrativePerks[0].RelatedCharacterID == "soul-b",
            "harvested memory never aliases death snapshot or another harvest");
        check(snapshot.CopySpectra().AsSpan().SequenceEqual(before), "decay is never applied to the snapshot itself");
        check(SoulData.CaptureDeath(null, null) == null && SoulDecay.Harvest(null, SoulQuality.Shock) == null,
            "absent soul is not fabricated");
        var empty = SoulData.CaptureDeath(new SoulData("empty", SinType.Wrath, MoralType.Neutral, 1), "");
        check(empty.Sin == SinType.Wrath && empty.Memory == null,
            "neutral spectrum retains label and absence of memory");

        // Actual trait keys live in NarrativePerks even after biography fades.
        var traitOnly = new MemorySeed { Story = "" };
        traitOnly.NarrativePerks.Add(new NarrativePerk { PerkName = "Глуховат", IsFound = true });
        traitOnly.NarrativePerks.Add(new NarrativePerk { PerkName = "Не бросит своих", IsFound = true });
        var personality = new SoulData("Traits without story", SinType.Wrath, MoralType.Neutral, 3, 80f, traitOnly);
        bool SameTraits(MemorySeed m) => m != null && m.NarrativePerks != null &&
            m.NarrativePerks.Count == 2 &&
            m.NarrativePerks[0].PerkName == "Глуховат" && m.NarrativePerks[0].IsFound &&
            m.NarrativePerks[1].PerkName == "Не бросит своих" && m.NarrativePerks[1].IsFound;
        check(!personality.HasMemory && SameTraits(personality.Memory),
            "absence of story is not absence of assigned traits");
        var traitDeath = SoulData.CaptureDeath(personality, "");
        check(SameTraits(traitDeath.Memory) && !ReferenceEquals(traitDeath.Memory, traitOnly),
            "death snapshot preserves assigned traits without biography");
        var traitFading = SoulDecay.Harvest(traitDeath, SoulQuality.Fading);
        check(!traitFading.HasMemory && SameTraits(traitFading.Memory) &&
              traitFading.PersonId == personality.PersonId,
            "fading retains the same traits and person, even with no story");
        check(SameTraits(SoulDecay.Harvest(traitDeath, SoulQuality.Shock).Memory) &&
              SameTraits(SoulDecay.Harvest(traitDeath, SoulQuality.Acceptance).Memory),
            "early harvest preserves both named traits");
        check(SoulDecay.Harvest(traitDeath, SoulQuality.Dissolved).Memory == null,
            "dissolved soul drops trait memory under existing decay policy");
        int legacyCalls = 0;
        var rosterSeed = MemorySeed.ForRosterSpawn(traitFading.Memory, true, () =>
        {
            legacyCalls++;
            return new MemorySeed { Story = "wrong biography" };
        });
        var projected = SoulData.FromRoster(traitFading.Name, traitFading.Sin, traitFading.Moral,
            traitFading.SinIntensity, traitFading.Level, traitFading.CopySpectra(), rosterSeed,
            traitFading.Gender, traitFading.Id, traitFading.Kin, traitFading.PersonId);
        check(!projected.HasMemory && SameTraits(projected.Memory) && legacyCalls == 0 &&
              projected.PersonId == traitFading.PersonId,
            "roster reconstruction of trait-only memory does not substitute a legacy biography");
        projected.Memory.NarrativePerks[0].IsFound = false;
        projected.Memory.NarrativePerks.RemoveAt(1);
        check(SameTraits(traitFading.Memory) && SameTraits(traitDeath.Memory),
            "projected trait changes do not rewrite harvested or historical personality");
        check(projected.RestoreBoundPersonality(traitFading, true) &&
              SameTraits(projected.Memory) && !projected.HasMemory,
            "same-body rollback restores exact named traits without inventing a story");

        string manager = File.ReadAllText(Path.Combine(root, "Assets/Scripts/Gameplay/SoulManager.cs"));
        check(manager.Contains("DeathSnapshot = Core.SoulData.CaptureDeath(warrior.Soul, warrior.Soul.EarnedTitle)") &&
              manager.Contains("Core.SoulDecay.Harvest(closest.DeathSnapshot, closest.SoulQuality)") &&
              !manager.Contains("Core.SoulDecay.Harvest(closest.Warrior.Soul"),
            "production fading entry snapshots death and harvest no longer reads the corpse (source wiring)");
    }
}
