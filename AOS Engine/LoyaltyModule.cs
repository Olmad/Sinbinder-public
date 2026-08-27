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
            _config = Resources.Load<AOSConfig>("AOSConfig");
            if (_config == null)
                Debug.LogWarning("[LoyaltyModule] AOSConfig не найден в Resources!");
        }

        public float Evaluate(Soul soul, DecisionContext context, ActionType action)
        {
            if (_config == null) return 0f;

            float score = 0f;
            if (context.HasCommand && action == ActionType.ObeyCommand)
            {
                score += soul.Loyalty * _config.LoyaltyObeySinMultiplier;
            }
            return score * Weight;
        }
    }
}