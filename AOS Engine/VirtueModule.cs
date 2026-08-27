using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.AOS.Modules
{
    public class VirtueModule : IPersonalityModule
    {
        public string ModuleID => "Virtue";
        public float Weight => 1.4f;

        private AOSConfig _config;

        public VirtueModule()
        {
            _config = Resources.Load<AOSConfig>("AOSConfig");
            if (_config == null)
                Debug.LogWarning("[VirtueModule] AOSConfig не найден в Resources!");
        }

        public float Evaluate(Soul soul, DecisionContext context, ActionType action)
        {
            if (_config == null) return 0f;

            float score = 0f;
            float virtue = -soul.SinIntensity;

            switch (action)
            {
                case ActionType.SaveAlly:
                    score += virtue * _config.VirtueSaveAllySinMultiplier;
                    if (context.AllyInDanger) score += _config.VirtueSaveAllyDangerBonus;
                    break;
                case ActionType.Loot:
                    if (virtue > 50f) score += _config.VirtueLootHighVirtuePenalty;
                    break;
                case ActionType.ObeyCommand:
                    score += virtue * _config.VirtueObeySinMultiplier;
                    break;
            }
            return score * Weight;
        }
    }
}