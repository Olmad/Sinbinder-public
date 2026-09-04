// Assets/Scripts/Tests/SelfCheckRunner.cs
using UnityEngine;

namespace Sinbinder.Tests
{
    /// <summary>
    /// Запуск самопроверки из игры.
    ///
    /// Повесь на любой объект сцены. При старте в консоли появится одна
    /// строка: пройдено столько-то из стольких. Если что-то не сошлось —
    /// список того, что именно, человеческим языком.
    ///
    /// Проверка ничего не портит: души создаются свои, объекты
    /// уничтожаются за собой.
    /// </summary>
    public class SelfCheckRunner : MonoBehaviour
    {
        [Tooltip("Прогонять проверку при запуске сцены.")]
        [SerializeField] private bool _onStart = true;

        [Tooltip("Останавливать игру, если проверка провалена. "
               + "Удобно, пока движок правится каждый вечер.")]
        [SerializeField] private bool _pauseOnFailure = true;

        void Start()
        {
            if (_onStart) Run();
        }

        [ContextMenu("Проверить движок")]
        public void Run()
        {
            var report = SelfCheck.RunAll();

            if (report.Ok)
            {
                Debug.Log("[SINBINDER] " + report);
                return;
            }

            Debug.LogError("[SINBINDER] " + report);
            if (_pauseOnFailure) Debug.Break();
        }
    }
}
