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
            _config = AOSConfig.Load();
        }

        public float Evaluate(Soul soul, DecisionContext context, ActionType action)
        {
            float score = 0f;
            // Общая добродетельность: среднее по всем семи спектрам со знаком минус.
            // Это голос «в целом хорошего человека», а не одной конкретной шкалы.
            float virtue = -soul.Average;

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