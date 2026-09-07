// Assets/Scripts/AOS Engine/AOSSceneSetup.cs
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.AOS
{
    /// <summary>
    /// Собирает недостающие части сцены. Порядок здесь — не мелочь,
    /// а условие работы половины игры.
    ///
    /// Менеджеры создавались в Start, и на них подписываются в своих Start
    /// журнал боя, тишина отказа и отъезд камеры. Порядок Start между
    /// объектами Unity не определяет никак: подписка могла прийти раньше
    /// того, на что подписываются, и тогда AOSEventHub.Instance == null —
    /// проверка тихо пропускала подписку, и отказ не поднимал ни журнал,
    /// ни тишину. Продукт демо мог не работать через раз, ничего при этом
    /// не ломая.
    ///
    /// Лечится не порядком, а фазой: Awake случается раньше любого Start
    /// в сцене, чей бы он ни был. Менеджеры переехали туда.
    ///
    /// Воинов, наоборот, надо настраивать поздно: их создают спавнеры
    /// в своих Start. Для этого весь компонент отодвинут в конец очереди
    /// (<c>DefaultExecutionOrder</c>) — его Awake всё равно раньше всех
    /// Start, а его Start уже позже спавнеров.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class AOSSceneSetup : MonoBehaviour
    {
        [SerializeField] private bool _runOnStart = true;

        void Awake()
        {
            // Раньше любого Start в сцене — значит раньше любой подписки.
            if (_runOnStart) SetupManagers();
        }

        void Start()
        {
            if (_runOnStart) SetupAllWarriors();
        }

        [ContextMenu("Setup AOS on Scene")]
        public void SetupScene()
        {
            SetupManagers();
            SetupAllWarriors();
            Debug.Log("[AOS] Сцена настроена.");
        }

        private void SetupManagers()
        {
            // Ищем СУЩЕСТВУЮЩИЙ Managers, НЕ создаём новый
            var managers = GameObject.Find("Managers");
            if (managers == null)
            {
                Debug.LogError("[AOS] Объект 'Managers' не найден на сцене! Создайте его вручную и добавьте CombatManager, SelectionManager.");
                return;
            }

            AddIfMissing<AOSEventHub>(managers);
            AddIfMissing<MemoryProcessor>(managers);
            AddIfMissing<EmotionSystem>(managers);
        }

        private void SetupAllWarriors()
        {
            var warriors = Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID);
            foreach (var w in warriors)
                SetupWarrior(w.gameObject);

            Debug.Log($"[AOS] Настроено {warriors.Length} воинов.");
        }

        /// <summary>
        /// Настроить одного воина. Публично: спавнеры создают своих
        /// не только на старте сцены — вторая волна Охотников выходит
        /// посреди боя, и без этого вызова она осталась бы без AOS вовсе:
        /// шесть тел, которые не думают.
        /// </summary>
        public void SetupWarrior(GameObject go)
        {
            AddIfMissing<AOSWarriorWrapper>(go);
            AddIfMissing<AutoAttackAOS>(go);

            var oldAttack = go.GetComponent<AutoAttack>();
            if (oldAttack != null) oldAttack.enabled = false;
        }

        private void AddIfMissing<T>(GameObject go) where T : Component
        {
            if (go.GetComponent<T>() == null)
                go.AddComponent<T>();
        }
    }
}