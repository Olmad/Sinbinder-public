using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.AOS.Modules
{
    public class WrathModule : IPersonalityModule
    {
        public string ModuleID => "Wrath";
        public float Weight => 1.0f;

        private AOSConfig _config;

        public WrathModule()
        {
            _config = AOSConfig.Load();
        }

        public float Evaluate(Soul soul, DecisionContext context, ActionType action)
        {
            float score = 0f;
            float sin = soul.Get(SinType.Wrath);

            switch (action)
            {
                case ActionType.Attack:
                    score += context.NearbyEnemies * _config.WrathAttackPerEnemy;
                    score += sin * _config.WrathSinMultiplier;
                    if (context.AllyInDanger && sin > 30f) score += _config.WrathAllyInDangerBonus;
                    break;
                case ActionType.Flee:
                    score -= sin * _config.WrathFleeSinMultiplier;
                    if (sin > 60f) score += _config.WrathFleeHighSinPenalty;
                    break;
                case ActionType.Idle:
                    if (sin > 40f) score += _config.WrathIdleHighSinPenalty;
                    break;
            }

            // Гнев не считает силы: усталость его не останавливает,
            // а раззадоривает. Единственный голос, который на неё
            // отвечает прибавкой, а не убавкой.
            if (action == ActionType.Attack && sin > 40f && context.IsExhausted)
                score += _config.WrathAttackFatigueIgnore;

            return score * Weight;
        }
    }
}