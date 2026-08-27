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
            _config = Resources.Load<AOSConfig>("AOSConfig");
            if (_config == null)
                Debug.LogWarning("[WrathModule] AOSConfig не найден в Resources!");
        }

        public float Evaluate(Soul soul, DecisionContext context, ActionType action)
        {
            if (_config == null) return 0f;

            float score = 0f;
            float sin = soul.SinIntensity;

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
            return score * Weight;
        }
    }
}