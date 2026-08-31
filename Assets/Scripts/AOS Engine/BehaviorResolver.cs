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
        /// Ниже этой уверенности воин колеблется. Порог живёт здесь один раз:
        /// его же читает панель предсказания темперамента, иначе движок
        /// и подсказка будут говорить игроку разное.
        ///
        /// Значение переехало в конфиг: 11-MISSING §2.4 называет его первым,
        /// что придётся крутить, а константу нельзя покрутить, не пересобрав
        /// проект.
        ///
        /// Заодно поменялась мера. Порог был абсолютным — «меньше десяти
        /// очков», — и врал в обе стороны: в лагере, где голоса лежат около
        /// нуля, уверенная победа считалась колебанием, а в бою при счёте
        /// под сотню настоящая мука выбора колебанием не считалась. Тот же
        /// §2.4 советовал поднять порог, но это делает колебание чаще,
        /// а не реже: чем больше кандидатов, тем тоньше разрывы.
        /// </summary>
        public static float HesitationShare => AOSConfig.Load().HesitationShare;

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

            float maxVoice = AOSConfig.Load().MaxVoice;

            foreach (var module in _modules)
            {
                float weight = EmotionSystem.Instance != null
                    ? EmotionSystem.Instance.GetEmotionWeight(warrior, module.ModuleID) : 1.0f;

                foreach (var action in scores.Keys.ToList())
                {
                    float voice = module.Evaluate(soul, context, action) * weight;

                    // Потолок вклада. Без него Страх выдавал до пятисот очков
                    // там, где остальные голоса дают по сорок: его тройной
                    // бонус (мало здоровья, высокая опасность, окружение)
                    // умножался на вес 2.0. Совет, в котором один участник
                    // может перекричать всех разом, — не совет.
                    //
                    // Ограничение симметрично: запретить голосу в одиночку
                    // проталкивать действие и в одиночку его хоронить —
                    // одно и то же требование.
                    if (maxVoice > 0f) voice = Mathf.Clamp(voice, -maxVoice, maxVoice);

                    scores[action] += voice;

                    if (!loudest.TryGetValue(action, out var current) || voice > current.value)
                        loudest[action] = (module.ModuleID, voice);
                }
            }

            // Снаряжение: восьмой рычаг игрока. Считается один раз, после характера.
            TemptationResolver.Apply(scores, context, AOSConfig.Load().TemptationScale);

            // Сюжетные перки: врождённая история воина правит очки.
            PerkResolver.ApplyPerks(scores, warrior, context, Perks);

            // Установка отряда: седьмой рычаг игрока. Меняет склонность
            // всех сразу, но не отменяет характер — поправка складывается
            // с голосами и может им проиграть.
            if (warrior != null && warrior.Team == Team.Player)
            {
                float scale = AOSConfig.Load().StrategyScale;
                if (scale > 0f)
                {
                    foreach (var mod in Gameplay.SquadOrders.CurrentModifiers())
                        if (scores.ContainsKey(mod.Action))
                            scores[mod.Action] += mod.Bonus * scale;
                }
            }

            var sorted = scores.OrderByDescending(kv => kv.Value).ToList();
            var best = sorted[0];

            // Бюллетень теперь бывает и коротким: в пустом лагере без приказа
            // выбирать не из чего. Спорить не с кем — значит, сомнений нет.
            bool alone = sorted.Count < 2;
            float gap = alone ? 0f : best.Value - sorted[1].Value;

            // Мера шкалы — громкость самого громкого из двух первых, а не
            // одного победителя. По победителю нормировать нельзя: когда он
            // сам около нуля, а второй глубоко в минусе (гордец, которому
            // одинаково нельзя и бежать, и стоять), деление на его почти-ноль
            // выдавало уверенность в двадцать пять единиц.
            //
            // Единица снизу — на вырожденный случай, когда молчат все.
            float loudness = Mathf.Max(
                Mathf.Max(Mathf.Abs(best.Value), alone ? 0f : Mathf.Abs(sorted[1].Value)), 1f);

            float confidence = alone ? 1f : gap / loudness;

            var decision = new Decision
            {
                Action = best.Key,
                TopContender = best.Key,
                RunnerUp = alone ? best.Key : sorted[1].Key,
                Gap = gap,
                Confidence = confidence,
                TopModule = loudest.TryGetValue(best.Key, out var top) ? top.module : "",
                Hesitated = confidence < HesitationShare
            };

            if (decision.Hesitated)
            {
                decision.Action = ActionType.Idle;
                decision.TopModule = "";
                Debug.Log($"[AOS] {warrior.DisplayName} колеблется "
                    + $"(gap={gap:F1}, уверенность={confidence:F2})");
            }
            else
            {
                // Без пометки о приказе строка лога не даёт отличить
                // послушание от его отсутствия: главное число игры
                // оставалось неизмеримым.
                Debug.Log($"[AOS] {warrior.DisplayName} выбрал {best.Key} "
                    + $"(очки: {best.Value:F1}, gap: {gap:F1}, уверенность: {confidence:F2}, "
                    + $"громче всех: {decision.TopModule}, "
                    + $"приказ: {(context.HasCommand ? context.CommandType : "нет")})");
            }

            // Приказ отойти исполняется и бегством. Воин, побежавший от
            // врага, когда велено отходить, сделал ровно то, о чём просили,
            // — по своей причине и не в ту точку, но сделал. Считать это
            // ослушанием значило бы записать человеку отказ за то, что он
            // послушался, и выдать ему с командиром обоюдную память
            // о непослушании (AOSEventHub.OnCommandRefused).
            bool obeyed = decision.Action == ActionType.ObeyCommand
                || (decision.Action == ActionType.Flee && context.CommandIsFallBack);

            // Замереть и ослушаться — разные вещи. Колеблющийся не выбрал
            // ничего вместо приказа: два голоса тянули его поровну, и он
            // остался стоять. Отказ — это когда против приказа нашлось
            // что-то, что оказалось важнее.
            //
            // Разница не косметическая: отказ поднимает AOSEventHub, а тот
            // выдаёт воину и командиру обоюдную память о непослушании.
            // За оцепенение эта память ложна — она делает врагами двоих,
            // из которых никто ничего не решил. Игрок при этом всё равно
            // всё видит: журнал пишет «не сдвинулся с места — не смог выбрать».
            decision.RefusedCommand = context.HasCommand && !obeyed && !decision.Hesitated;

            AOSStats.Record(decision, context);
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
            // Стоять на месте можно всегда — это пол голосования.
            var scores = new Dictionary<ActionType, float> { { ActionType.Idle, 0f } };

            // Остальное надо ещё иметь возможность сделать.
            //
            // Раньше все пять базовых действий стояли в бюллетене при любом
            // положении, и модули исправно голосовали за недоступное: Гордыня
            // шла в драку в пустом лагере, где драться не с кем, Зависть — за
            // добычей, которой нет. Такой голос не просто пропадал: он выигрывал
            // у приказа, и первый же приказ пролога оказывался невыполним.
            //
            // Умения фильтровались так с самого начала — «на откате
            // не предлагаем». Базовым действиям того же не досталось.
            if (context.NearbyEnemies > 0)
            {
                scores[ActionType.Attack] = 0f;

                // Бежать не от кого, когда врагов нет. Отход по приказу —
                // это ObeyCommand, а не Flee.
                scores[ActionType.Flee] = 0f;
            }

            if (context.NearbyLoot > 0) scores[ActionType.Loot] = 0f;
            if (context.AllyInDanger) scores[ActionType.SaveAlly] = 0f;
            if (context.HasCommand) scores[ActionType.ObeyCommand] = 0f;

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