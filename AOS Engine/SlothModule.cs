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
            _config = AOSConfig.Load();
        }

        public float Evaluate(Soul soul, DecisionContext context, ActionType action)
        {
            float score = 0f;
            float sin = soul.Get(SinType.Sloth);

            if (action == ActionType.Idle)
            {
                score += sin * _config.SlothIdleSinMultiplier;
                if (context.DangerLevel < 0.3f) score += _config.SlothIdleLowDangerBonus;

                // Усталость — главный союзник уныния. Чем меньше сил,
                // тем громче голос «постоять».
                score += context.Fatigue * _config.SlothIdleFatigueMultiplier;
            }

            if (action == ActionType.Attack && context.IsExhausted)
                score += _config.SlothAttackFatiguePenalty;

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