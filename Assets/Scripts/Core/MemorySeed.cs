// Assets/Scripts/Core/MemorySeed.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.AOS;

namespace Sinbinder.Core
{
    [System.Serializable]
    public class MemorySeed
    {
        public string Object;
        public string Emotion;
        public string Story;
        public string Trigger;

        [Tooltip("Сколько ночей наказания душа отбыла за это деяние (Core/Punishment). "
               + "Отбыла предел — наказание за него только копит обиду.")]
        public int Atoned;

        [Tooltip("Врождённые модификаторы действий, которые не меняются со временем.")]
        public List<ActionModifier> ActionModifiers = new();

        [Tooltip("Нарративные перки души: личная история, которую разбирает PerkResolver.")]
        public List<NarrativePerk> NarrativePerks = new();

        // Roster and newly created souls must not share mutable perk state.
        public static MemorySeed Copy(MemorySeed source)
        {
            if (source == null) return null;
            var copy = new MemorySeed
            {
                Object = source.Object,
                Emotion = source.Emotion,
                Story = source.Story,
                Trigger = source.Trigger,
                Atoned = source.Atoned,
                ActionModifiers = source.ActionModifiers == null ? null : new List<ActionModifier>(),
                NarrativePerks = source.NarrativePerks == null ? null : new List<NarrativePerk>(),
            };
            if (source.ActionModifiers != null)
                foreach (var modifier in source.ActionModifiers)
                    copy.ActionModifiers.Add(modifier == null ? null : new ActionModifier
                    {
                        Action = modifier.Action,
                        ScoreBonus = modifier.ScoreBonus,
                        Blocked = modifier.Blocked,
                    });
            if (source.NarrativePerks != null)
                foreach (var perk in source.NarrativePerks)
                    copy.NarrativePerks.Add(perk == null ? null : new NarrativePerk
                    {
                        PerkName = perk.PerkName,
                        IsFound = perk.IsFound,
                        RelatedCharacterID = perk.RelatedCharacterID,
                    });
            return copy;
        }

        // Only genuinely legacy entries may reconstruct a biography from
        // the original camp description. Captured null is deliberate absence.
        public static MemorySeed ForRosterSpawn(MemorySeed seed, bool captured,
            System.Func<MemorySeed> legacy)
            => captured || seed != null ? Copy(seed) : legacy?.Invoke();
    }

    [System.Serializable]
    public class ActionModifier
    {
        public ActionType Action;
        public float ScoreBonus;
        public bool Blocked;
    }
}