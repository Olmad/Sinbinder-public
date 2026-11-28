using System;
using System.Collections.Generic;
using Sinbinder.AOS;
using Sinbinder.Core;
using Sinbinder.Gameplay;
using UnityEngine;

internal static class PerkNullChecks
{
    static void Check(bool ok, string name)
    {
        if (!ok) throw new InvalidOperationException("F02: " + name);
    }

    public static void Run()
    {
        const string trait = "Кровник Ордена";
        const string story = "F02 test story";
        var memory = MemorySeed.Copy(new MemorySeed { NarrativePerks = new List<NarrativePerk>
        {
            null, new NarrativePerk { PerkName = trait }, new NarrativePerk { PerkName = story }
        }});
        Check(Traits.Of(memory).Count == 1 && Traits.Of(memory)[0] == trait, "mixed read");
        Traits.Write(memory, new[] { trait, story, "Боится Ордена" });
        Check(memory.NarrativePerks.Count == 4 && memory.NarrativePerks[0] == null, "write preserves entries without duplicates");
        var context = new DecisionContext { EnemyIsHunter = true };
        var scores = new Dictionary<ActionType, float> { [ActionType.Attack] = 0f };
        var database = ScriptableObject.CreateInstance<PerkDatabase>();
        var perk = ScriptableObject.CreateInstance<SoulPerk>();
        perk.PerkName = story;
        perk.Effects.Add(new PerkEffect { UnlockedAction = ActionType.Attack, ScoreModifier = 7f });
        perk.Effects.Add(new PerkEffect { UnlockedAction = ActionType.Attack, ScoreModifier = 7f });
        database.AllPerks.Add(perk);
        var warrior = new Warrior { Soul = new SoulData("F02", MoralType.Pious, 1, new float[7], memory) };
        PerkResolver.ApplyPerks(scores, warrior, context, database);
        Check(scores[ActionType.Attack] == 94f, "mixed trait and duplicate legacy effects preserve existing score");
        scores[ActionType.Attack] = 0f;
        PerkResolver.ApplyPerks(scores, warrior, context, database, trait);
        Check(scores[ActionType.Attack] == 14f, "muted trait leaves legacy effects unchanged");
        memory.NarrativePerks = null;
        Check(Traits.Of(memory).Count == 0, "null list read");
        scores[ActionType.Attack] = 0f;
        PerkResolver.ApplyPerks(scores, warrior, context, database);
        Check(scores[ActionType.Attack] == 0f, "null list no vote");
        Traits.Write(memory, new[] { trait, trait });
        Check(memory.NarrativePerks.Count == 1, "null list write");
        Console.WriteLine("PASS F02 null narrative perks");
    }
}
