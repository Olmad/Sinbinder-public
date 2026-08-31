// Assets/_Project/Scripts/AOS/DebugAOS.cs
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.AOS
{
    public class DebugAOS : MonoBehaviour
    {
        void Update()
        {
            // Клавиша 6: принудительно проверить SaveAlly для первого выделенного союзника
            if (Input.GetKeyDown(KeyCode.Alpha6))
            {
                var warrior = GetFirstSelectedAlly();
                if (warrior != null)
                {
                    var context = CombatDecisionContext.Create(warrior);
                    // Вручную выставляем, что союзник в опасности
                    context.AllyInDanger = true;
                    // Убираем врагов, чтобы атака не мешала
                    context.NearbyEnemies = 0;
                    context.DangerLevel = 0.2f;
                    var resolver = new BehaviourResolver();
                    var result = resolver.Decide(warrior, context);
                    Debug.Log($"[DEBUG AOS] SaveAlly test for {warrior.DisplayName}: chose {result}");
                }
                else
                {
                    Debug.LogWarning("[DEBUG AOS] Нет выделенного союзника для теста SaveAlly.");
                }
            }

            // Клавиша 7: принудительно проверить Flee для первого выделенного союзника
            if (Input.GetKeyDown(KeyCode.Alpha7))
            {
                var warrior = GetFirstSelectedAlly();
                if (warrior != null)
                {
                    var context = CombatDecisionContext.Create(warrior);
                    // Высокая опасность и низкое здоровье
                    context.DangerLevel = 0.9f;
                    context.CurrentHP = context.MaxHP * 0.2f;
                    context.NearbyEnemies = 5;
                    context.AllyInDanger = false;
                    var resolver = new BehaviourResolver();
                    var result = resolver.Decide(warrior, context);
                    Debug.Log($"[DEBUG AOS] Flee test for {warrior.DisplayName}: chose {result}");
                }
                else
                {
                    Debug.LogWarning("[DEBUG AOS] Нет выделенного союзника для теста Flee.");
                }
            }
        }

        private Warrior GetFirstSelectedAlly()
        {
            var selected = SelectionManager.Instance?.GetSelectedUnits();
            if (selected == null || selected.Count == 0) return null;
            var warrior = selected[0].Warrior;
            if (warrior == null || warrior.IsDead || warrior.Team != Team.Player) return null;
            return warrior;
        }
    }
}