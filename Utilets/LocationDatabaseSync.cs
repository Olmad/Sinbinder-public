#if UNITY_EDITOR
// Файл зависит от UnityEditor. Без этой обёртки сборка плеера падает с CS0246,
// потому что скрипт лежит не в папке Editor.
// Assets/_Project/Scripts/Editor/LocationDatabaseSync.cs
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Sinbinder.Utilets
{
    [InitializeOnLoad]
    public static class LocationDatabaseSync
    {
        private const string DATABASE_PATH = "Assets/_Project/Scripts/Core/LocationDatabase.asset";

        static LocationDatabaseSync()
        {
            // Автоматическая синхронизация при каждом обновлении ассетов
            EditorApplication.update += OnEditorUpdate;
        }

        private static bool _synced = false;

        private static void OnEditorUpdate()
        {
            if (_synced) return;
            _synced = true;
            SyncLocations();
            EditorApplication.update -= OnEditorUpdate;
        }

        [MenuItem("Sinbinder/Sync Location Database")]
        public static void SyncLocations()
        {
            // Загружаем базу данных
            LocationDatabase database = AssetDatabase.LoadAssetAtPath<LocationDatabase>(DATABASE_PATH);
            if (database == null)
            {
                Debug.LogWarning("[LocationDatabase] Ассет не найден. Создайте его через меню Sinbinder.");
                return;
            }

            // Получаем список всех сцен в Build Settings
            List<EditorBuildSettingsScene> buildScenes = EditorBuildSettings.scenes.ToList();
            // Если Build Settings пуст, ищем все .unity файлы в папке Scenes
            if (buildScenes.Count == 0)
            {
                string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Project/Scenes" });
                foreach (string guid in sceneGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    buildScenes.Add(new EditorBuildSettingsScene(path, true));
                }
            }

            // Собираем данные о существующих сценах
            HashSet<string> existingSceneNames = new();
            foreach (var scene in buildScenes)
            {
                string sceneName = Path.GetFileNameWithoutExtension(scene.path);
                existingSceneNames.Add(sceneName);
            }

            // Удаляем записи, которых больше нет в проекте (опционально)
            // database.Locations.RemoveAll(loc => !existingSceneNames.Contains(loc.SceneName));

            // Добавляем новые сцены
            foreach (var scene in buildScenes)
            {
                string sceneName = Path.GetFileNameWithoutExtension(scene.path);
                if (database.Locations.Any(loc => loc.SceneName == sceneName))
                    continue;

                // Автоматически определяем тег по имени сцены
                string tag = DetermineTag(sceneName);
                string displayName = sceneName; // Можно задать вручную позже

                database.Locations.Add(new LocationData
                {
                    SceneName = sceneName,
                    DisplayName = displayName,
                    Tag = tag
                });

                Debug.Log($"[LocationDatabase] Добавлена новая локация: {sceneName} (тег: {tag})");
            }

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
        }

        private static string DetermineTag(string sceneName)
        {
            // Простая эвристика: ищем ключевые слова в имени сцены
            string lower = sceneName.ToLower();
            if (lower.Contains("crypt") || lower.Contains("склеп")) return "Crypt";
            if (lower.Contains("forest") || lower.Contains("лес")) return "Forest";
            if (lower.Contains("village") || lower.Contains("деревня")) return "Village";
            if (lower.Contains("ruins") || lower.Contains("руины")) return "Ruins";
            if (lower.Contains("camp") || lower.Contains("лагерь")) return "Camp";
            if (lower.Contains("cave") || lower.Contains("пещера")) return "Cave";
            // По умолчанию — имя сцены как тег
            return sceneName;
        }
    }
}
#endif
