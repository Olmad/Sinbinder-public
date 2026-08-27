using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.AOS.Modules
{
    public class FearModule : IPersonalityModule
    {
        public string ModuleID => "Fear";
        public float Weight => 2.0f;

        private AOSConfig _config;

        public FearModule()
        {
            _config = Resources.Load<AOSConfig>("AOSConfig");
            if (_config == null)
                Debug.LogWarning("[FearModule] AOSConfig не найден в Resources!");
        }

        public float Evaluate(Soul soul, DecisionContext context, ActionType action)
        {
            if (_config == null) return 0f;

            float score = 0f;

            if (context.CurrentHP < context.MaxHP * 0.4f)
            {
                if (action == ActionType.Flee) score += _config.FearFleeLowHpBonus;
                if (action == ActionType.Attack) score += _config.FearAttackLowHpPenalty;
            }

            if (context.DangerLevel > 0.6f)
            {
                if (action == ActionType.Flee) score += context.DangerLevel * _config.FearFleeDangerMultiplier;
                if (action == ActionType.Idle) score += _config.FearIdleDangerPenalty;
            }

            if (action == ActionType.Attack)
                score += _config.FearAttackGlobalPenalty * 10f; // небольшая общая нерешительность

            return score * Weight;
        }
    }
}