using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.AOS.Modules
{
    public class MoralityModule : IPersonalityModule
    {
        public string ModuleID => "Morality";
        public float Weight => 1.0f;

        private AOSConfig _config;

        public MoralityModule()
        {
            _config = Resources.Load<AOSConfig>("AOSConfig");
            if (_config == null)
                Debug.LogWarning("[MoralityModule] AOSConfig не найден в Resources!");
        }

        public float Evaluate(Soul soul, DecisionContext context, ActionType action)
        {
            if (_config == null) return 0f;

            float score = 0f;

            if (soul.Morality == MoralityType.Pious)
            {
                if (action == ActionType.SaveAlly) score += _config.MoralityPiousSaveAlly;
                if (action == ActionType.Loot) score += _config.MoralityPiousLootPenalty;
                if (action == ActionType.Flee) score += _config.MoralityPiousFleePenalty;
            }
            else if (soul.Morality == MoralityType.Vicious)
            {
                if (action == ActionType.Attack) score += _config.MoralityViciousAttack;
                if (action == ActionType.Loot) score += _config.MoralityViciousLoot;
                if (action == ActionType.SaveAlly) score += _config.MoralityViciousSaveAllyPenalty;
            }
            return score * Weight;
        }
    }
}