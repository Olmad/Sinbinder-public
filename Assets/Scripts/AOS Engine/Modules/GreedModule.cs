// Assets/Scripts/AOS Engine/Modules/GreedModule.cs
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.AOS.Modules
{
    public class GreedModule : IPersonalityModule, IMissionModule
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
                    // Проверять здесь, есть ли добыча, не нужно: без неё
                    // Добыча не попадает в бюллетень (BehaviourResolver.BuildCandidates).
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

        /// <summary>
        /// Мирная миссия. По таблице квеста жадный при любой морали
        /// облагает деревню данью — деньги есть деньги.
        /// </summary>
        public float EvaluateMission(Soul soul, MissionContext context, MissionAction action)
        {
            float sin = soul.Get(SinType.Greed) * _config.MissionSinScale;

            switch (action)
            {
                case MissionAction.TaxVillage:     return sin;
                case MissionAction.EnslaveVillage: return sin * 0.35f;  // доход, но хлопотный
                case MissionAction.HelpVillage:    return -sin * 0.5f;  // даром не работает
                case MissionAction.IgnoreVillage:  return -sin * 0.3f;  // мимо денег не проходит
                default:                           return 0f;
            }
        }

    }
}