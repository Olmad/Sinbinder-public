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
            }
            return score * Weight;
        }
    }
}