// Assets/Scripts/AOS Engine/AOSSceneSetup.cs
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.AOS
{
    public class AOSSceneSetup : MonoBehaviour
    {
        [SerializeField] private bool _runOnStart = true;

        void Start()
        {
            if (_runOnStart) SetupScene();
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

        private void SetupWarrior(GameObject go)
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