// Assets/Scripts/AOS Engine/AOSEventHub.cs
// Перевод: текст через Loc
using UnityEngine;
using Sinbinder.Gameplay;
using Sinbinder.Core;

namespace Sinbinder.AOS
{
    public class AOSEventHub : MonoBehaviour
    {
        public static AOSEventHub Instance { get; private set; }

        void Awake()
        {
            if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
            else Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        internal void OnUnitDied(Damageable killed, GameObject killer, bool lordDown = false)
        {
            if (killed == null || killed.Warrior == null) return;

            var victim = killed.Warrior;
            var killerWarrior = killer != null ? killer.GetComponent<Warrior>() : null;

            if (killerWarrior != null)
            {
                if (!lordDown)
                {
                    using (Core.Cost.Of("death.emotion"))
                        EmotionSystem.Instance?.TriggerEmotion(killerWarrior, EmotionType.Joy, 0.3f);
                    MemoryProcessor.Instance?.CreateMemory(killerWarrior, "KilledEnemy", victim.Id, EmotionType.Joy, 0.5f);
                }
                RecordKill(killerWarrior, victim);
            }

            // В «Перетягивании» Греховод вернётся к трону.
            if (lordDown) return;

            // Реакция союзников на смерть
            using var witnesses = Core.Cost.Of("death.witnesses");
            var allWarriors = Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID);
            foreach (var w in allWarriors)
            {
                if (w.IsDead || w.Team != victim.Team || w == victim) continue;

                float dist = Vector3.Distance(w.transform.position, victim.transform.position);
                if (dist < 15f)
                {
                    EmotionSystem.Instance?.TriggerEmotion(w, EmotionType.Sadness, 0.4f);
                    EmotionSystem.Instance?.TriggerEmotion(w, EmotionType.Anger, 0.3f);
                    MemoryProcessor.Instance?.CreateMemory(w, "AllyDied", victim.Id, EmotionType.Sadness, 0.7f);

                    if (killerWarrior != null)
                        MemoryProcessor.Instance?.CreateMemory(w, "EnemyKilledAlly", killerWarrior.Id, EmotionType.Anger, 0.8f);
                }
            }
        }

        internal static void RecordKill(Warrior killer, Warrior victim)
        {
            // Титулы проверяют и число убийств, и их суммарную важность.
            // Бонусы за черты личности входят в важность единственного деяния.
            float importance = 0.5f;
            var perks = killer.Soul?.Memory?.NarrativePerks;
            if (perks != null)
            {
                foreach (var perk in perks)
                {
                    if (perk?.PerkName == "Мститель" && victim.DisplayName.Contains("Бандит"))
                        importance += 1.5f;
                    if (perk?.PerkName == "Бывший Охотник (ненавидит)" && victim.Soul != null && Trades.OfOrder(victim.Soul.Trade))
                        importance += 2.0f;
                }
            }
            killer.Reputation.Deeds.Add(new DeedRecord { Type = DeedType.Kill, Importance = importance });

            // Смерть инквизитора даёт отдельное деяние для титула «Каратель».
            if (victim.Soul != null && victim.Soul.Trade == Trade.Inquisitor)
                killer.Reputation.Deeds.Add(new DeedRecord { Type = DeedType.KillCommander, Importance = 0.5f });

            using (Core.Cost.Of("death.title")) TitleManager.UpdateTitle(killer);
        }

        public void OnAllySaved(Warrior savior, Warrior saved)
        {
            EmotionSystem.Instance?.TriggerEmotion(savior, EmotionType.Joy, 0.4f);
            EmotionSystem.Instance?.TriggerEmotion(saved, EmotionType.Joy, 0.5f);

            // Деяние "Спасение союзника"
            savior.Reputation.Deeds.Add(new DeedRecord { Type = DeedType.SaveAlly, Importance = 0.7f });

            // Спасли старшего — это другое деяние, и титулы на нём свои
            // («Щит Отряда», «Телохранитель»). До 17 сентября
            // ProtectCommander не писал никто, и оба титула были
            // недостижимы по данным, а не по цене (11-MISSING.md §5).
            if (saved != null && !string.IsNullOrEmpty(SquadRoster.CommanderName)
                && saved.DisplayName == SquadRoster.CommanderName)
                savior.Reputation.Deeds.Add(new DeedRecord
                    { Type = DeedType.ProtectCommander, Importance = 0.7f });

            TitleManager.UpdateTitle(savior);

            MemoryProcessor.Instance?.CreateMemory(saved, "AllySavedMe", savior.Id, EmotionType.Joy, 0.9f);
            MemoryProcessor.Instance?.CreateMemory(savior, "SavedAlly", saved.Id, EmotionType.Joy, 0.7f);
        }

        /// <summary>Отказ подчиниться. Раз в бою на воина — событие, а не шум.</summary>
        public System.Action<Warrior, Decision, DecisionContext> OnRefusal;

        /// <summary>
        /// Воин сделал что-то заметное сам, без приказа.
        ///
        /// Отдельно от OnRefusal: отказ — это спор с игроком, а это —
        /// поступок в его отсутствие. До сих пор второе не объявлялось
        /// ничем, и уникальные моменты движка проходили мимо игрока
        /// молча. См. Moment.
        /// </summary>
        public System.Action<Warrior, Decision, DecisionContext> OnSelfWill;

        private readonly RefusalEventGate _refusalEvents = new RefusalEventGate();

        /// <summary>
        /// Воин не выполнил приказ.
        ///
        /// Это главный момент игры, поэтому он проходит здесь, а не тонет
        /// в Debug.Log: отказ запоминается обеими сторонами, вызывает
        /// эмоцию и поднимает подписчиков — микропаузу, значок, журнал.
        ///
        /// Голосование идёт раз в секунду, а приказ живёт дольше. Один и
        /// тот же приказ может получить много повторных вердиктов, но не
        /// должен десятками раз ухудшать память и отношения. Запоминаем
        /// один отказ на выдачу приказа, сохраняя защиту от спама новыми.
        /// </summary>
        public void OnCommandRefused(Warrior warrior, Decision decision, DecisionContext context)
        {
            if (warrior == null) return;

            // Use real command identity, not soul ID or elapsed seconds
            // alone. Same-command refusal at t+4, t+8 must not repeatedly
            // create a memory; two new orders in one frame have different
            // Warrior.CommandRevision values.
            if (!_refusalEvents.TryRecord(
                warrior.GetInstanceID(), warrior.CommandRevision, Time.time, RefusalCooldown)) return;

            var commander = context?.Commander;

            EmotionSystem.Instance?.TriggerEmotion(warrior, EmotionType.Anger, 0.25f);

            // Воин помнит, что ослушался; командир помнит, что его ослушались.
            MemoryProcessor.Instance?.CreateMemory(
                warrior, "RefusedCommand", commander != null ? commander.Id : "",
                EmotionType.Anger, 0.4f);

            if (commander != null && commander != warrior)
            {
                EmotionSystem.Instance?.TriggerEmotion(commander, EmotionType.Anger, 0.3f);
                MemoryProcessor.Instance?.CreateMemory(
                    commander, "AllyDisobeyed", warrior.Id, EmotionType.Anger, 0.5f);
            }

            // Строка лога — разработчику (четвёртая ступень): стек вызовов на каждую
            // запись дорог, а на поле в сотни воинов отказов и своеволий — десятки в секунду.
            if (Core.Transparency.Shows(Core.Clarity.Trace))
                Debug.Log($"[AOS] ОТКАЗ: {PhraseGenerator.LogLine(warrior, context, decision)}");
            OnRefusal?.Invoke(warrior, decision, context);
        }

        /// <summary>Защита от спама разными приказами, секунды.</summary>
        public const float RefusalCooldown = 4f;

        /// <summary>
        /// Объявить поступок, сделанный без приказа.
        ///
        /// Отбор — у <see cref="Moment"/>; здесь только оглашение.
        /// Держать правило рядом с событием нельзя: его меряет стенд,
        /// а стенд не знает ни сцены, ни воинов.
        /// </summary>
        public void OnSelfWilled(Warrior warrior, Decision decision, DecisionContext context)
        {
            if (warrior == null) return;
            Notice worth;
            using (Core.Cost.Of("self.worth")) worth = Moment.Worth(decision, context);
            if (worth == Notice.None) return;

            if (Core.Transparency.Shows(Core.Clarity.Trace))
                Debug.Log($"[AOS] САМ: {PhraseGenerator.LogLine(warrior, context, decision)}");
            if (OnSelfWill == null) return;
            foreach (var d in OnSelfWill.GetInvocationList())
                using (Core.Cost.Of(Core.Cost.On ? "self>" + d.Method.DeclaringType?.Name : null))
                    ((System.Action<Warrior, Decision, DecisionContext>)d)(warrior, decision, context);
        }

        public void OnBetrayal(Warrior traitor, Warrior betrayedCommander)
        {
            // Предать можно и когда старшего в отряде нет: обиду тогда
            // держать некому, а ронять игру на этом нельзя. Зовущий
            // об этом уже сказал вслух — здесь просто выходим.
            if (traitor == null || betrayedCommander == null) return;

            EmotionSystem.Instance?.TriggerEmotion(betrayedCommander, EmotionType.Anger, 0.8f);
            EmotionSystem.Instance?.TriggerEmotion(betrayedCommander, EmotionType.Sadness, 0.5f);
            MemoryProcessor.Instance?.CreateMemory(betrayedCommander, "AllyBetrayedMe", traitor.Id, EmotionType.Anger, 1.0f);
            var allWarriors = Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID);
            foreach (var w in allWarriors)
            {
                if (w.IsDead || w.Team != betrayedCommander.Team) continue;
                float dist = Vector3.Distance(w.transform.position, traitor.transform.position);
                if (dist < 20f)
                {
                    EmotionSystem.Instance?.TriggerEmotion(w, EmotionType.Anger, 0.4f);
                    MemoryProcessor.Instance?.CreateMemory(w, "WitnessedBetrayal", traitor.Id, EmotionType.Anger, 0.6f);
                }
            }
        }

        internal void OnBattleEnd(BattleResult result)
        {
            // Сводка по решениям — единственное место, где видно долю
            // отказов при отданном приказе. Разработчику, не игроку:
            // это четвёртая ступень прозрачности.
            Debug.Log(AOSStats.Report());
            AOSStats.Reset();

            // Боевые реплики держатся флагом «уже говорили», и снимает его
            // только ResetAllBattleDialogues, который не звался ниоткуда
            // (правило orphans). То есть отряд разговаривал в первом бою
            // и молчал во всех следующих. В демо с одним боем это невидимо,
            // а в бесконечных миссиях воины замолкали бы навсегда.
            // Снимаем в конце боя: к следующему они снова при голосе.
            Object.FindFirstObjectByType<Dialogue.DialogueTrigger>()?.ResetAllBattleDialogues();

            var memories = MemoryProcessor.Instance;
            // При полном разгроме записываем исход по идентификаторам павших.
            if (result.Outcome != BattleOutcome.Victory && memories != null)
                foreach (string warriorId in result.FallenAllies)
                    memories.RecordBattle(warriorId, result);

            var combat = CombatManager.Instance;
            var allies = combat.GetAliveAllies();
            int alliesAlive = combat.GetAlivePlayerCount();
            foreach (var ally in allies)
            {
                if (ally == null || ally.IsDead) continue;
                var w = ally.Warrior;
                if (w != null)
                {
                    if (memories != null && !string.IsNullOrEmpty(w.Id))
                        memories.RecordBattle(w.Id, result);

                    // Деяние "Выживание в миссии"
                    w.Reputation.Deeds.Add(new DeedRecord { Type = DeedType.SurviveMission, Importance = 0.3f });

                    // «Берсерк» стоит на NeverRetreat: за весь бой ни разу
                    // не побежал. Деяние это не писал никто, и титул был
                    // недостижим по данным.
                    var body = w.GetComponent<AOSWarriorWrapper>();
                    if (body != null && !body.Fled)
                        w.Reputation.Deeds.Add(new DeedRecord
                            { Type = DeedType.NeverRetreat, Importance = 0.5f });

                    // «Тень» стоит на StayedOut: был в бою и не ударил ни разу.
                    // Выжить мало — выживание выше пишется всем подряд,
                    // и когда «Тень» стояла на нём, ею становился весь отряд.
                    if (body != null && !body.Struck)
                        w.Reputation.Deeds.Add(new DeedRecord
                            { Type = DeedType.StayedOut, Importance = 0.3f });

                    if (body != null) body.ForgetBattle();

                    // Если союзников не осталось – "Последний рубеж"
                    if (result.AlliesLost > 0 && result.AlliesLost >= alliesAlive)
                        w.Reputation.Deeds.Add(new DeedRecord { Type = DeedType.LastStand, Importance = 1.0f });

                    TitleManager.UpdateTitle(w);
                }
            }
        }
    }
}
