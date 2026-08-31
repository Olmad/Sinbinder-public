using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.AOS.Modules
{
    public class LoyaltyModule : IPersonalityModule
    {
        public string ModuleID => "Loyalty";
        public float Weight => 1.0f;

        private AOSConfig _config;

        public LoyaltyModule()
        {
            _config = AOSConfig.Load();
        }

        public float Evaluate(Soul soul, DecisionContext context, ActionType action)
        {
            float score = 0f;
            if (context.HasCommand && action == ActionType.ObeyCommand)
            {
                score += soul.Loyalty * _config.LoyaltyObeySinMultiplier;

                // У приказа появилась цена. Уйти из ближнего боя — значит
                // подставиться под удар вслед, и «отойди» перестаёт быть
                // бесплатным. Именно это делает отказ иногда правильным,
                // а голосование — спором по существу, а не капризом.
                if (context.IsEngaged && context.CommandLeavesFight)
                    score += _config.LoyaltyObeyEngagedPenalty
                             * (context.Surrounded ? 1.5f : 1f);
            }
            return score * Weight;
        }
    }
}