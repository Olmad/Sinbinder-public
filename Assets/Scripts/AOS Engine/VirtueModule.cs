using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.AOS.Modules
{
    public class VirtueModule : IPersonalityModule, IMissionModule
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

        /// <summary>
        /// Мирная миссия. Общая добродетельность — среднее по всем
        /// семи шкалам со знаком минус — тянет к тому, за что не стыдно.
        /// </summary>
        public float EvaluateMission(Soul soul, MissionContext context, MissionAction action)
        {
            // Добродетель говорит, только когда она есть. У грешной души
            // «отрицательная добродетель» — это тот же грех, сказанный
            // второй раз: грешные модули уже подали свой голос, и удваивать
            // его нечем. Молчание здесь — не ноль, а отказ повторяться.
            float virtue = -soul.Average * _config.MissionVirtueScale;
            if (virtue <= 0f) return 0f;

            switch (action)
            {
                case MissionAction.HelpVillage:    return virtue;
                case MissionAction.SanctifyAltar:  return virtue * 0.6f;
                case MissionAction.KillEveryone:   return -virtue * 1.5f;
                case MissionAction.EnslaveVillage: return -virtue * 1.2f;
                case MissionAction.KillTraveler:   return -virtue * 0.8f;
                default:                           return 0f;
            }
        }

    }
}