// Перевод: текст через Loc
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
        // Offscreen battles must not read the location of a player's
        // *currently open* scene. The current expedition catalog has no
        // authored location tags, so an offscreen encounter conservatively
        // has none. This never changes CurrentLocation for actual scene actors.
        private static System.Collections.Generic.HashSet<string> _offscreenTags;

        private sealed class OffscreenLocationScope : System.IDisposable
        {
            private readonly System.Collections.Generic.HashSet<string> _previous;
            private bool _disposed;

            public OffscreenLocationScope(string[] knownTags)
            {
                _previous=_offscreenTags;
                _offscreenTags=new System.Collections.Generic.HashSet<string>(
                    knownTags ?? System.Array.Empty<string>(),
                    System.StringComparer.Ordinal);
            }

            public void Dispose()
            {
                if(_disposed) return;
                _disposed=true;
                _offscreenTags=_previous;
            }
        }

        /// <summary>
        /// During an auto-expedition use ONLY explicitly authored site tags.
        /// If none exist, perks requiring a location remain inactive rather
        /// than borrowing a Crypt/Ruins tag from the player's open scene.
        /// </summary>
        public static System.IDisposable ForOffscreenSite(params string[] knownTags)
            => new OffscreenLocationScope(knownTags);

        public static bool HasTag(string tag)
        {
            if(_offscreenTags!=null)
                return !string.IsNullOrEmpty(tag) && _offscreenTags.Contains(tag);
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