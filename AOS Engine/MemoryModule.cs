using System.Linq;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.AOS.Modules
{
    public class MemoryModule : IPersonalityModule
    {
        public string ModuleID => "Memory";
        public float Weight => 1.5f;

        private AOSConfig _config;

        public MemoryModule()
        {
            _config = Resources.Load<AOSConfig>("AOSConfig");
            if (_config == null)
                Debug.LogWarning("[MemoryModule] AOSConfig не найден в Resources!");
        }

        public float Evaluate(Soul soul, DecisionContext context, ActionType action)
        {
            if (_config == null || context.RecentMemories == null) return 0f;

            float score = 0f;
            foreach (var memory in context.RecentMemories)
            {
                float strength = memory.Strength;
                switch (memory.EventType)
                {
                    case "AllySavedMe" when action == ActionType.SaveAlly:
                        score += strength * _config.MemorySaveAllyStrengthMultiplier;
                        break;
                    case "AllyBetrayedMe" when action == ActionType.SaveAlly:
                        score += strength * _config.MemoryBetrayalStrengthMultiplier;
                        break;
                    case "EnemyKilledAlly" when action == ActionType.Attack:
                        score += strength * _config.MemoryKillStrengthMultiplier;
                        break;
                    case "FoundLoot" when action == ActionType.Loot:
                        score += strength * _config.MemoryLootStrengthMultiplier;
                        break;
                }
            }
            return score * Weight;
        }
    }
}