// Assets/_Project/Scripts/AOS Engine/PerkResolver.cs
using System.Collections.Generic;
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.AOS
{
    public static class PerkResolver
    {
        public static void ApplyPerks(Dictionary<ActionType, float> scores, Warrior warrior, DecisionContext context, PerkDatabase database)
        {
            if (database == null || warrior.Soul.Memory == null) return;
            if (warrior.Soul.Memory.NarrativePerks == null) return;

            foreach (var activePerk in warrior.Soul.Memory.NarrativePerks)
            {
                SoulPerk perkData = database.AllPerks.Find(p => p.PerkName == activePerk.PerkName);
                if (perkData == null) continue;

                foreach (var effect in perkData.Effects)
                {
                    if (IsConditionMet(effect.Condition, warrior, context, activePerk))
                    {
                        if (effect.UnlockedAction.HasValue && scores.ContainsKey(effect.UnlockedAction.Value))
                        {
                            scores[effect.UnlockedAction.Value] += effect.ScoreModifier;
                        }
                    }
                }
            }
        }

        private static bool IsConditionMet(string condition, Warrior warrior, DecisionContext context, NarrativePerk activePerk)
        {
            switch (condition)
            {
                case "Always":
                    return true;

                case "InRuins":
                    return LocationManager.HasTag("Ruins");

                case "InForest":
                    return LocationManager.HasTag("Forest");

                case "InVillage":
                    return LocationManager.HasTag("Village");

                case "InCrypt":
                    return LocationManager.HasTag("Crypt");

                case "CommanderInDanger":
                    // Используем командира из контекста, без FindObjectsOfType
                    return context.Commander != null && context.Commander.HP < context.Commander.MaxHP * 0.3f;

                case "CommanderOrders":
                    return context.HasCommand;

                case "NearChild":
                    return context.AllyInDanger && context.NearbyAllies > 0;

                case "ChildFound":
                    return activePerk.IsFound && !string.IsNullOrEmpty(activePerk.RelatedCharacterID);

                case "EnemyStronger":
                    return context.NearbyEnemies > 0;

                case "AtDeathPlace":
                    if (warrior.Soul.Memory == null) return false;
                    string deathTag = warrior.Soul.Memory.Trigger;
                    return !string.IsNullOrEmpty(deathTag) && LocationManager.HasTag(deathTag);
                case "InCave":
    return LocationManager.HasTag("Cave");
case "InDarkness":
    // Проверить освещённость локации (пока через тег)
    return LocationManager.HasTag("Dark") || LocationManager.HasTag("Crypt");
case "NearWater":
    return LocationManager.HasTag("River") || LocationManager.HasTag("Swamp");
case "EnemyIsUndead":
    return context.EnemyIsUndead;
case "EnemyIsPlant":
    return context.EnemyIsPlant;
case "EnemyIsBeast":
    return context.EnemyIsBeast;
case "EnemyIsBandit":
    return context.EnemyIsBandit;
case "EnemyIsHunter":
    return context.EnemyIsHunter;
case "EnemyIsMagister":
    return context.EnemyIsMagister;
case "EnemyIsPrisoner":
    return context.EnemyIsPrisoner;
case "EnemyIsHerbivore":
    return context.EnemyIsBeast; // временно
case "BrotherNearby":
    return context.BrotherNearby;
case "AllyDamaged":
    return context.AllyDamagedRecently;
case "LastAlive":
    return context.LastAlive;
case "InDefense":
    return context.HasCommand && context.CommandType == "Defend";
                default:
                    return false;
            }
        }
    }
}