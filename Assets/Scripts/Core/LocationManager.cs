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
        /// Проверяет, соответствует ли текущая локация указанному тегу.
        /// </summary>
        public static bool HasTag(string tag)
        {
            return CurrentLocation != null && CurrentLocation.Tag == tag;
        }
    }
}