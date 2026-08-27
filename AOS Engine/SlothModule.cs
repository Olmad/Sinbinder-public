using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.AOS.Modules
{
    public class SlothModule : IPersonalityModule
    {
        public string ModuleID => "Sloth";
        public float Weight => 1.2f;

        private AOSConfig _config;

        public SlothModule()
        {
            _config = Resources.Load<AOSConfig>("AOSConfig");
            if (_config == null)
                Debug.LogWarning("[SlothModule] AOSConfig не найден в Resources!");
        }

        public float Evaluate(Soul soul, DecisionContext context, ActionType action)
        {
            if (_config == null) return 0f;

            float score = 0f;
            float sin = soul.SinIntensity;

            if (action == ActionType.Idle)
            {
                score += sin * _config.SlothIdleSinMultiplier;
                if (context.DangerLevel < 0.3f) score += _config.SlothIdleLowDangerBonus;
            }

            if (context.DangerLevel > 0.6f || context.CurrentHP < context.MaxHP * 0.4f)
            {
                if (action == ActionType.Flee) score += _config.SlothFleeDangerThreshold;
                if (action == ActionType.Attack) score += _config.SlothAttackDangerPenalty;
            }

            if (action == ActionType.Attack)
                score -= sin * _config.SlothAttackSinMultiplier;

            if (action == ActionType.SaveAlly)
                score -= sin * _config.SlothSaveAllySinMultiplier;

            return score * Weight;
        }
    }
}