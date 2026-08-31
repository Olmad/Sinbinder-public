using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sinbinder.AOS.Modules;
using Sinbinder.Core;
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

        /// <summary>
        /// База нарративных перков. Сто штук написано, и до сих пор
        /// PerkResolver не вызывался ниоткуда: «Мать-волчица», «Бывший
        /// Охотник», «Певец смерти» существовали как данные и не влияли
        /// на решения ни одним битом.
        /// </summary>
        private static PerkDatabase _perks;
        private static bool _perksLookedUp;

        private static PerkDatabase Perks
        {
            get
            {
                if (_perksLookedUp) return _perks;
                _perksLookedUp = true;
                _perks = Resources.Load<PerkDatabase>("PerkDatabase");
                if (_perks == null)
                    Debug.LogWarning("[AOS] Resources/PerkDatabase не найден — "
                        + "сюжетные перки не влияют на решения. Создай ассет через "
                        + "Assets → Create → Sinbinder → Perk Database и положи в Resources.");
                return _perks;
            }
        }

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
            var scores = BuildCandidates(warrior, context);

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

            // Сюжетные перки: врождённая история воина правит очки.
            PerkResolver.ApplyPerks(scores, warrior, context, Perks);

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
        /// Что воин вообще может выбрать в этот момент.
        ///
        /// Пять базовых действий доступны всегда. Подчинение — только если
        /// приказ есть: без него «подчиниться» было пустым кандидатом,
        /// который мог выиграть голосование и выродиться в бездействие.
        /// Умения добавляются с самого воина: какие компоненты на нём висят
        /// и что из них не на откате.
        /// </summary>
        private Dictionary<ActionType, float> BuildCandidates(Warrior warrior, DecisionContext context)
        {
            var scores = new Dictionary<ActionType, float>
            {
                { ActionType.Attack, 0f }, { ActionType.SaveAlly, 0f },
                { ActionType.Loot, 0f }, { ActionType.Flee, 0f },
                { ActionType.Idle, 0f }
            };

            if (context.HasCommand)
                scores[ActionType.ObeyCommand] = 0f;

            if (warrior == null) return scores;

            foreach (var set in warrior.GetComponents<ISkillSet>())
            {
                if (set?.SkillActions == null) continue;
                foreach (var action in set.SkillActions)
                {
                    if (scores.ContainsKey(action)) continue;
                    if (!set.CanUseSkill(action)) continue;   // на откате — не предлагаем
                    scores[action] = 0f;
                }
            }

            return scores;
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