using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sinbinder.AOS.Modules;
using Sinbinder.Gameplay;

namespace Sinbinder.AOS
{
    public class BehaviourResolver
    {
        /// <summary>
        /// Меньше этого разрыва воин колеблется. Порог живёт здесь один раз:
        /// его же читает панель предсказания темперамента, иначе движок
        /// и подсказка будут говорить игроку разное.
        /// </summary>
        public const float HesitationGap = 10f;

        private List<IPersonalityModule> _modules;

        public BehaviourResolver()
        {
            _modules = new List<IPersonalityModule>
            {
                // Семь грехов — по одному голосу на шкалу.
                new GreedModule(),
                new PrideModule(),
                new WrathModule(),
                new EnvyModule(),
                new LustModule(),
                new GluttonyModule(),
                new SlothModule(),

                // Терпение читает шкалу Гнева со знаком минус. Отдельный
                // голос нужен потому, что только он высказывается за
                // умения Терпения — больше их никто не предлагает.
                new PatienceModule(),

                // Голоса, у которых нет своей шкалы.
                new FearModule(),
                new LoyaltyModule(),
                new MoralityModule(),
                new MemoryModule(),
                new VirtueModule()
            };
        }

        /// <summary>
        /// Что делать. Обёртка над DecideDetailed для тех, кому причина не нужна.
        /// </summary>
        public ActionType Decide(Warrior warrior, DecisionContext context)
            => DecideDetailed(warrior, context).Action;

        /// <summary>
        /// Что делать и почему. Причина нужна интерфейсу: без неё отказ
        /// нечем объяснить игроку.
        /// </summary>
        public Decision DecideDetailed(Warrior warrior, DecisionContext context)
        {
            Dictionary<ActionType, float> scores = new()
            {
                { ActionType.Attack, 0 }, { ActionType.SaveAlly, 0 },
                { ActionType.Loot, 0 }, { ActionType.Flee, 0 },
                { ActionType.Idle, 0 }, { ActionType.ObeyCommand, 0 }
            };

            if (MemoryProcessor.Instance != null)
                context.RecentMemories = MemoryProcessor.Instance.GetMemories(warrior);

            var soul = Soul.FromWarrior(warrior);

            // Кто именно поднял каждое действие сильнее всех — это и есть причина.
            var loudest = new Dictionary<ActionType, (string module, float value)>();

            foreach (var module in _modules)
            {
                float weight = EmotionSystem.Instance != null
                    ? EmotionSystem.Instance.GetEmotionWeight(warrior, module.ModuleID) : 1.0f;

                foreach (var action in scores.Keys.ToList())
                {
                    float voice = module.Evaluate(soul, context, action) * weight;
                    scores[action] += voice;

                    if (!loudest.TryGetValue(action, out var current) || voice > current.value)
                        loudest[action] = (module.ModuleID, voice);
                }
            }

            // Снаряжение: восьмой рычаг игрока. Считается один раз, после характера.
            TemptationResolver.Apply(scores, context);

            var sorted = scores.OrderByDescending(kv => kv.Value).ToList();
            var best = sorted[0];
            float gap = sorted[0].Value - sorted[1].Value;

            var decision = new Decision
            {
                Action = best.Key,
                TopContender = best.Key,
                RunnerUp = sorted[1].Key,
                Gap = gap,
                TopModule = loudest.TryGetValue(best.Key, out var top) ? top.module : "",
                Hesitated = gap < HesitationGap
            };

            if (decision.Hesitated)
            {
                decision.Action = ActionType.Idle;
                decision.TopModule = "";
                Debug.Log($"[AOS] {warrior.DisplayName} колеблется (gap={gap:F1})");
            }
            else
            {
                Debug.Log($"[AOS] {warrior.DisplayName} выбрал {best.Key} "
                    + $"(очки: {best.Value:F1}, gap: {gap:F1}, громче всех: {decision.TopModule})");
            }

            decision.RefusedCommand = context.HasCommand && decision.Action != ActionType.ObeyCommand;
            return decision;
        }

        /// <summary>
        /// Принимает решение по мирной миссии на основе доступных действий.
        /// </summary>
        public MissionAction DecideMission(Warrior warrior, MissionContext context, List<MissionAction> availableActions)
        {
            var soul = Soul.FromWarrior(warrior);

            if (availableActions == null || availableActions.Count == 0)
            {
                Debug.LogWarning($"[AOS MISSION] {warrior.DisplayName}: пустой список действий.");
                return default;
            }

            // Создаём словарь только из доступных действий
            Dictionary<MissionAction, float> scores = new();
            foreach (var action in availableActions)
                scores[action] = 0f;

            int voices = 0;
            foreach (var module in _modules)
            {
                if (!(module is IMissionModule missionModule)) continue;
                voices++;

                float weight = EmotionSystem.Instance != null
                    ? EmotionSystem.Instance.GetEmotionWeight(warrior, module.ModuleID) : 1.0f;
                foreach (var action in availableActions)
                    scores[action] += missionModule.EvaluateMission(soul, context, action) * weight;
            }

            if (voices == 0)
                Debug.LogWarning("[AOS MISSION] Ни один модуль не голосует по миссиям: "
                    + "решение определяется порядком списка, а не характером.");

            var best = scores.OrderByDescending(kv => kv.Value).First();
            Debug.Log($"[AOS MISSION] {warrior.DisplayName} выбрал {best.Key} (очки: {best.Value:F1}, голосов: {voices})");
            return best.Key;
        }
    }
}