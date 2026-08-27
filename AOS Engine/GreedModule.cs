// 5. Scripts/AOS/Modules/GreedModule.cs
using UnityEngine;

namespace Sinbinder.AOS.Modules
{
    public class GreedModule : IPersonalityModule
    {
        public string ModuleID => "Greed";
        public float Weight => 1.0f;

        public float Evaluate(Soul soul, DecisionContext context, ActionType action)
        {
            float score = 0f;
            float sin = soul.SinIntensity;

            switch (action)
            {
                case ActionType.Loot:
                    score += context.NearbyLoot * 15f;
                    score += sin * 0.5f;
                    break;
                case ActionType.SaveAlly:
                    score -= sin * 0.3f;
                    if (sin > 50f) score -= 20f;
                    break;
                case ActionType.Attack:
                    if (context.NearbyLoot > 0) score -= 10f;
                    if (sin < -50f) score += 15f;
                    break;
                case ActionType.ObeyCommand:
                    if (context.UnpaidMissions > 2) score -= 40f;
                    break;
            }
            return score * Weight;
        }
    }
}