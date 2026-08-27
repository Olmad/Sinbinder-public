using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sinbinder.AOS.Modules;
using Sinbinder.Gameplay;

namespace Sinbinder.AOS
{
    public class BehaviourResolver
    {
        private List<IPersonalityModule> _modules;

        public BehaviourResolver()
        {
            _modules = new List<IPersonalityModule>
            {
                new GreedModule(),
                new WrathModule(),
                new VirtueModule(),
                new FearModule(),
                new LoyaltyModule(),
                new MoralityModule(),
                new MemoryModule(),
                new SlothModule()
            };
        }

        public ActionType Decide(Warrior warrior, DecisionContext context)
        {
            Dictionary<ActionType, float> scores = new()
            {
                { ActionType.Attack, 0 }, { ActionType.SaveAlly, 0 },
                { ActionType.Loot, 0 }, { ActionType.Flee, 0 },
                { ActionType.Idle, 0 }, { ActionType.ObeyCommand, 0 }
            };

            if (MemoryProcessor.Instance != null)
                context.RecentMemories = MemoryProcessor.Instance.GetMemories(warrior);

            var soul = new Soul
            {
                Name = warrior.DisplayName,
                SinIntensity = warrior.Soul.SinIntensity,
                Morality = (MoralityType)(int)warrior.Soul.Moral,
                Loyalty = warrior.Loyalty
            };

            foreach (var module in _modules)
            {
                float weight = EmotionSystem.Instance != null
                    ? EmotionSystem.Instance.GetEmotionWeight(warrior, module.ModuleID) : 1.0f;
                foreach (var action in scores.Keys.ToList())
                    scores[action] += module.Evaluate(soul, context, action) * weight;
            }

            var best = scores.OrderByDescending(kv => kv.Value).First();
            var sorted = scores.OrderByDescending(kv => kv.Value).ToList();
            float gap = sorted[0].Value - sorted[1].Value;

            if (gap < 10f)
            {
                Debug.Log($"[AOS] {warrior.DisplayName} колеблется (gap={gap:F1})");
                return ActionType.Idle;
            }

            Debug.Log($"[AOS] {warrior.DisplayName} выбрал {best.Key} (очки: {best.Value:F1}, gap: {gap:F1})");
            return best.Key;
        }

        /// <summary>
        /// Принимает решение по мирной миссии на основе доступных действий.
        /// </summary>
        public MissionAction DecideMission(Warrior warrior, MissionContext context, List<MissionAction> availableActions)
        {
            var soul = new Soul
            {
                Name = warrior.DisplayName,
                SinIntensity = warrior.Soul.SinIntensity,
                Morality = (MoralityType)(int)warrior.Soul.Moral,
                Loyalty = warrior.Loyalty
            };

            // Создаём словарь только из доступных действий
            Dictionary<MissionAction, float> scores = new();
            foreach (var action in availableActions)
                scores[action] = 0f;

            foreach (var module in _modules)
            {
                float weight = EmotionSystem.Instance != null
                    ? EmotionSystem.Instance.GetEmotionWeight(warrior, module.ModuleID) : 1.0f;
                foreach (var action in availableActions)
                    scores[action] += module.EvaluateMission(soul, context, action) * weight;
            }

            var best = scores.OrderByDescending(kv => kv.Value).First();
            Debug.Log($"[AOS MISSION] {warrior.DisplayName} выбрал {best.Key} (очки: {best.Value:F1})");
            return best.Key;
        }
    }
}