// Assets/_Project/Scripts/AOS/CrisisManager.cs
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Gameplay;
using Sinbinder.Core;

namespace Sinbinder.AOS
{
    public enum CrisisType
    {
        GreedPayment,
        PrideDuel,
        WrathRage,
        EnvyDemand,
        LustCharm,
        GluttonyHunger,
        SlothRefuse,
        Betrayal
    }

    public class CrisisManager : MonoBehaviour
    {
        public static CrisisManager Instance { get; private set; }

        private List<Warrior> _crisisQueue = new();

        void Awake()
        {
            if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
            else Destroy(gameObject);
        }

        public void TriggerCrisis(CrisisType type, Warrior warrior)
        {
            if (_crisisQueue.Contains(warrior)) return;
            _crisisQueue.Add(warrior);
            Debug.Log($"[CRISIS] Кризис {type} для {warrior.DisplayName}");
            // Здесь будет вызов UI для выбора игрока
            switch (type)
            {
                case CrisisType.GreedPayment:
                    ResolveGreedPayment(warrior);
                    break;
                case CrisisType.Betrayal:
                    ResolveBetrayal(warrior);
                    break;
            }
        }

        private void ResolveGreedPayment(Warrior warrior)
        {
            GamePauseController.Instance?.Pause();
            // Симуляция выбора игрока: заплатить
            if (warrior.UnpaidMissions >= 3)
            {
                int cost = warrior.Salary;
                Debug.Log($"[CRISIS] Жадный {warrior.DisplayName} требует {cost} золота!");
                // Здесь должен быть UI, пока просто логируем
                // Если игрок платит:
                warrior.UnpaidMissions = 0;
                warrior.ChangeLoyalty(10f);
                MemoryProcessor.Instance?.CreateMemory(warrior, "SalaryPaid", "Commander", "благодарность", 0.3f);
            }
            GamePauseController.Instance?.Resume();
        }

        private void ResolveBetrayal(Warrior warrior)
        {
            Debug.Log($"[CRISIS] {warrior.DisplayName} предаёт!");
            warrior.Team = Team.Enemy;
            if (CombatManager.Instance != null)
            {
                var dmg = warrior.GetComponent<Damageable>();
                if (dmg != null)
                {
                    CombatManager.Instance.UnregisterUnit(dmg);
                    CombatManager.Instance.RegisterEnemyUnit(dmg);
                }
            }
        }
    }
}