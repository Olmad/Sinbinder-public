// Assets/AOS Engine/Modules/GreedModule.cs
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.AOS.Modules
{
    public class GreedModule : IPersonalityModule
    {
        public string ModuleID => "Greed";
        public float Weight => 1.0f;

        private AOSConfig _config;

        public GreedModule()
        {
            _config = AOSConfig.Load();
        }

        public float Evaluate(Soul soul, DecisionContext context, ActionType action)
        {
            float score = 0f;
            float sin = soul.Get(SinType.Greed);

            switch (action)
            {
                case ActionType.Loot:
                    score += context.NearbyLoot * _config.GreedLootPerItem;
                    score += sin * _config.GreedSinMultiplier;
                    // Предметы-искусители считает TemptationResolver, один раз на решение.
                    break;

                case ActionType.SaveAlly:
                    score -= sin * _config.GreedSaveAllySinMultiplier;
                    if (sin > 50f) score += _config.GreedSaveAllyHighSinPenalty;
                    break;

                case ActionType.Attack:
                    if (context.NearbyLoot > 0) score += _config.GreedAttackPenaltyWhenLoot;
                    if (sin < -50f) score += _config.GreedAttackGoodVirtueBonus;
                    break;

                case ActionType.ObeyCommand:
                    if (context.UnpaidMissions > 2) score += _config.GreedObeyUnpaidPenalty;
                    break;
            }
            return score * Weight;
        }
    }
}