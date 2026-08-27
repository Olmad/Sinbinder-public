// Assets/_Project/Scripts/AOS/AOSEventHub.cs
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
                MemoryProcessor.Instance?.CreateMemory(killerWarrior, "KilledEnemy", victim.Id, "удовлетворение", 0.5f);

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
                    MemoryProcessor.Instance?.CreateMemory(w, "AllyDied", victim.Id, "печаль", 0.7f);

                    if (killerWarrior != null)
                        MemoryProcessor.Instance?.CreateMemory(w, "EnemyKilledAlly", killerWarrior.Id, "гнев", 0.8f);
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

            MemoryProcessor.Instance?.CreateMemory(saved, "AllySavedMe", savior.Id, "благодарность", 0.9f);
            MemoryProcessor.Instance?.CreateMemory(savior, "SavedAlly", saved.Id, "гордость", 0.7f);
        }

        public void OnBetrayal(Warrior traitor, Warrior betrayedCommander)
        {
            EmotionSystem.Instance?.TriggerEmotion(betrayedCommander, EmotionType.Anger, 0.8f);
            EmotionSystem.Instance?.TriggerEmotion(betrayedCommander, EmotionType.Sadness, 0.5f);
            MemoryProcessor.Instance?.CreateMemory(betrayedCommander, "AllyBetrayedMe", traitor.Id, "гнев", 1.0f);

            var allWarriors = Object.FindObjectsOfType<Warrior>();
            foreach (var w in allWarriors)
            {
                if (w.IsDead || w.Team != betrayedCommander.Team) continue;
                float dist = Vector3.Distance(w.transform.position, traitor.transform.position);
                if (dist < 20f)
                {
                    EmotionSystem.Instance?.TriggerEmotion(w, EmotionType.Anger, 0.4f);
                    MemoryProcessor.Instance?.CreateMemory(w, "WitnessedBetrayal", traitor.Id, "гнев", 0.6f);
                }
            }
        }

        public void OnBattleEnd(bool playerWon, int enemiesKilled, int alliesLost)
        {
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