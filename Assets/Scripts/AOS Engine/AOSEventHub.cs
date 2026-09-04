// Assets/Scripts/AOS Engine/AOSEventHub.cs
using UnityEngine;
using Sinbinder.Gameplay;

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

        void Start()
        {
            if (CombatManager.Instance != null)
                CombatManager.Instance.OnAnyDeath += OnUnitDied;
        }

        void OnDestroy()
        {
            if (CombatManager.Instance != null)
                CombatManager.Instance.OnAnyDeath -= OnUnitDied;
        }

        private void OnUnitDied(Damageable killed, GameObject killer)
        {
            if (killed?.Warrior == null) return;

            var victim = killed.Warrior;
            var killerWarrior = killer?.GetComponent<Warrior>();

            if (killerWarrior != null)
            {
                // Эмоции
                EmotionSystem.Instance?.TriggerEmotion(killerWarrior, EmotionType.Joy, 0.3f);
                // Запись деяния "Убийство" для убийцы
                killerWarrior.Reputation.Deeds.Add(new DeedRecord
                {
                    Type = DeedType.Kill,
                    Importance = 0.5f,
                    Time = System.DateTime.Now
                });
                MemoryProcessor.Instance?.CreateMemory(killerWarrior, "KilledEnemy", victim.Id, EmotionType.Joy, 0.5f);

                // Проверка специальных перков убийцы (Мститель, Бывший Охотник и т.д.)
                if (killerWarrior.Soul.Memory?.NarrativePerks != null)
                {
                    foreach (var perk in killerWarrior.Soul.Memory.NarrativePerks)
                    {
                        if (perk.PerkName == "Мститель" && victim.DisplayName.Contains("Бандит"))
                            killerWarrior.Reputation.Deeds.Add(new DeedRecord { Type = DeedType.Kill, Importance = 1.5f, Time = System.DateTime.Now });
                        if (perk.PerkName == "Бывший Охотник (ненавидит)" && victim.DisplayName.Contains("Охотник"))
                            killerWarrior.Reputation.Deeds.Add(new DeedRecord { Type = DeedType.Kill, Importance = 2.0f, Time = System.DateTime.Now });
                    }
                }
                // Обновляем титул убийцы
                TitleManager.UpdateTitle(killerWarrior);
            }

            // Реакция союзников на смерть
            var allWarriors = Object.FindObjectsOfType<Warrior>();
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

        public void OnAllySaved(Warrior savior, Warrior saved)
        {
            EmotionSystem.Instance?.TriggerEmotion(savior, EmotionType.Joy, 0.4f);
            EmotionSystem.Instance?.TriggerEmotion(saved, EmotionType.Joy, 0.5f);

            // Деяние "Спасение союзника"
            savior.Reputation.Deeds.Add(new DeedRecord { Type = DeedType.SaveAlly, Importance = 0.7f, Time = System.DateTime.Now });
            TitleManager.UpdateTitle(savior);

            MemoryProcessor.Instance?.CreateMemory(saved, "AllySavedMe", savior.Id, EmotionType.Joy, 0.9f);
            MemoryProcessor.Instance?.CreateMemory(savior, "SavedAlly", saved.Id, EmotionType.Joy, 0.7f);
        }

        /// <summary>Отказ подчиниться. Раз в бою на воина — событие, а не шум.</summary>
        public System.Action<Warrior, Decision, DecisionContext> OnRefusal;

        private readonly System.Collections.Generic.Dictionary<string, float> _lastRefusal = new();

        /// <summary>
        /// Воин не выполнил приказ.
        ///
        /// Это главный момент игры, поэтому он проходит здесь, а не тонет
        /// в Debug.Log: отказ запоминается обеими сторонами, вызывает
        /// эмоцию и поднимает подписчиков — микропаузу, значок, журнал.
        ///
        /// Голосование идёт раз в секунду, а приказ живёт дольше, поэтому
        /// один и тот же отказ повторялся бы каждый такт. Гасим повтор:
        /// не чаще раза в несколько секунд на воина.
        /// </summary>
        public void OnCommandRefused(Warrior warrior, Decision decision, DecisionContext context)
        {
            if (warrior == null) return;

            if (_lastRefusal.TryGetValue(warrior.Id, out float last)
                && Time.time - last < RefusalCooldown) return;
            _lastRefusal[warrior.Id] = Time.time;

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

            Debug.Log($"[AOS] ОТКАЗ: {PhraseGenerator.LogLine(warrior, context, decision)}");
            OnRefusal?.Invoke(warrior, decision, context);
        }

        /// <summary>Пауза между двумя отказами одного воина, секунды.</summary>
        public const float RefusalCooldown = 4f;

        public void OnBetrayal(Warrior traitor, Warrior betrayedCommander)
        {
            EmotionSystem.Instance?.TriggerEmotion(betrayedCommander, EmotionType.Anger, 0.8f);
            EmotionSystem.Instance?.TriggerEmotion(betrayedCommander, EmotionType.Sadness, 0.5f);
            MemoryProcessor.Instance?.CreateMemory(betrayedCommander, "AllyBetrayedMe", traitor.Id, EmotionType.Anger, 1.0f);

            var allWarriors = Object.FindObjectsOfType<Warrior>();
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

        public void OnBattleEnd(bool playerWon, int enemiesKilled, int alliesLost)
        {
            // Сводка по решениям — единственное место, где видно долю
            // отказов при отданном приказе. Разработчику, не игроку:
            // это четвёртая ступень прозрачности.
            Debug.Log(AOSStats.Report());
            AOSStats.Reset();

            var allWarriors = Object.FindObjectsOfType<Warrior>();
            foreach (var w in allWarriors)
            {
                if (w.IsDead) continue;
                if (w.Team == Team.Player)
                {
                    MemoryProcessor.Instance?.RecordBattle(w, playerWon, enemiesKilled, alliesLost);

                    // Деяние "Выживание в миссии"
                    w.Reputation.Deeds.Add(new DeedRecord { Type = DeedType.SurviveMission, Importance = 0.3f, Time = System.DateTime.Now });

                    // Если союзников не осталось – "Последний рубеж"
                    if (alliesLost >= CombatManager.Instance.GetAlivePlayerCount())
                        w.Reputation.Deeds.Add(new DeedRecord { Type = DeedType.LastStand, Importance = 1.0f, Time = System.DateTime.Now });

                    TitleManager.UpdateTitle(w);
                }
            }
        }
    }
}