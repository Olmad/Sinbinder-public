using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sinbinder.Core
{
    public class LocationManager : MonoBehaviour
    {
        public static LocationManager Instance { get; private set; }
        public static LocationData CurrentLocation { get; private set; }

        [SerializeField] private LocationDatabase _database;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_database == null) return;
            CurrentLocation = _database.Locations.Find(l => l.SceneName == scene.name);
        }

        /// <summary>
        /// Соответствует ли текущая локация указанному тегу.
        ///
        /// Пока базы локаций нет, CurrentLocation пуст, и метод честно
        /// отвечает «нет» на любой вопрос. Беда в том, что от этого ответа
        /// зависят четырнадцать эффектов перков — «в руинах», «в темноте»,
        /// «у воды», «на месте своей смерти», — и все они молча не
        /// срабатывают. Поэтому об отсутствии базы говорим вслух,
        /// один раз за запуск.
        /// </summary>
        public static bool HasTag(string tag)
        {
            if (CurrentLocation == null)
            {
                if (!_warnedNoLocation)
                {
                    _warnedNoLocation = true;
                    Debug.LogWarning("[МЕСТО] Текущая локация не определена: "
                        + "нет LocationDatabase, либо в ней нет записи под имя этой сцены. "
                        + "Все перки с условием по месту не сработают.");
                }
                return false;
            }
            return CurrentLocation.Tag == tag;
        }

        private static bool _warnedNoLocation;

        /// <summary>
        /// Задать место вручную — для сцен без базы и для проверок.
        /// </summary>
        public static void SetLocation(LocationData location) => CurrentLocation = location;
    }
}