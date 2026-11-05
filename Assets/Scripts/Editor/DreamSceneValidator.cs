#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using Sinbinder.Gameplay;

namespace Sinbinder.Utilets
{
    /// <summary>Compile-gated, non-destructive checks for the optional dream scene.</summary>
    public static class DreamSceneValidator
    {
        private const string ScenePath = "Assets/Scenes/Prologue_Dream.unity";

        [MenuItem("Sinbinder/Сон Бога Смерти/Проверить сцену")]
        public static void ValidateFromMenu() => Validate(false);

        // Run with Unity -batchmode -quit -executeMethod Sinbinder.Utilets.DreamSceneValidator.BatchValidate
        public static void BatchValidate() => Validate(true);

        private static void Validate(bool batch)
        {
            var errors = new System.Collections.Generic.List<string>();
            Scene scene = default;
            bool opened = false;
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                if (!File.Exists(Path.Combine(projectRoot, ScenePath)))
                {
                    errors.Add("Dream scene absent: run DemoSceneBuilder.BuildDreamTutorial first.");
                }
                else
                {
                    bool inBuild = EditorBuildSettings.scenes.Any(s => s.enabled && s.path == ScenePath);
                    if (!inBuild) errors.Add("Dream scene not enabled in Build Settings.");
                    scene = SceneManager.GetSceneByPath(ScenePath);
                    if (!scene.IsValid() || !scene.isLoaded)
                    {
                        scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                        opened = true;
                    }
                    var roots = scene.GetRootGameObjects();
                    int Count<T>() where T : Component => roots.Sum(r => r.GetComponentsInChildren<T>(true).Length);
                    if (Count<DreamTutorialDirector>() != 1) errors.Add("Expected exactly one DreamTutorialDirector.");
                    if (Count<SelectionManager>() != 1) errors.Add("Expected one SelectionManager.");
                    if (Count<CombatManager>() != 1) errors.Add("Expected one CombatManager.");
                    if (Count<SoulManager>() != 1) errors.Add("Expected one SoulManager.");
                    if (Count<Camera>() < 1) errors.Add("No camera.");
                    if (Count<NavMeshSurface>() < 1) errors.Add("No NavMeshSurface.");
                    if (Count<GroundNavMesh>() < 1) errors.Add("No GroundNavMesh runtime baker.");
                    if (Count<Collider>() < 1) errors.Add("No physical collision surfaces.");
                }
                if (!EditorBuildSettings.scenes.Any(s => s.enabled && s.path == "Assets/Scenes/Prologue_Camp.unity"))
                    errors.Add("Camp destination not enabled in Build Settings.");
            }
            catch (Exception ex) { errors.Add(ex.ToString()); }
            finally
            {
                if (opened && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
            string result = errors.Count == 0 ? "PASS (static scene checks; no Play Mode performed)"
                : "FAIL\n" + string.Join("\n", errors);
            Debug.Log("[Dream QA] " + result);
            string reportFolder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library");
            Directory.CreateDirectory(reportFolder);
            File.WriteAllText(Path.Combine(reportFolder, "Sinbinder_Dream_Validation.txt"), result);
            if (batch) EditorApplication.Exit(errors.Count == 0 ? 0 : 1);
        }
    }
}
#endif
