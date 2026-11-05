#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.Utilets
{
    // Separate menu command: existing BuildAll continues to build only the proven demo.
    public static partial class DemoSceneBuilder
    {
        [MenuItem("Sinbinder/Сон Бога Смерти/Создать отдельную сцену")]
        public static void BuildDreamTutorial()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            // Never silently overwrite a level that the user has already edited.
            const string path = "Assets/Scenes/Prologue_Dream.unity";
            if (System.IO.File.Exists(path))
            {
                Debug.LogWarning("[СОН] Сцена уже существует. Сборка отменена: " + path);
                return;
            }
            var scene = NewScene();
            Atmosphere(warm: false);
            Ground("Старая дорога", 8f, "Ground048");
            Look();
            Managers();
            Interface();
            // Camera movement and Tab/V use the actual existing RTS_Camera.
            CameraRig(new Vector3(0f, 29f, -40f), new Vector3(55f, 0f, 0f), movable: true);

            // The fort is environmental geometry, not a second combat engine.
            var fort = new GameObject("Стены и ворота старого форта");
            var stone = DreamMaterial("Stone", new Color(0.24f, 0.25f, 0.27f));
            var earth = DreamMaterial("Earth", new Color(0.20f, 0.18f, 0.15f));
            var wood = DreamMaterial("Wood", new Color(0.24f, 0.16f, 0.10f));

            // Three clear gates along the northern curtain wall.
            Segment(fort.transform, "Стена запад 1", new Vector3(-28f, 2.5f, 15f), new Vector3(12f, 5f, 2f), stone);
            Segment(fort.transform, "Стена запад 2", new Vector3(-10f, 2.5f, 15f), new Vector3(10f, 5f, 2f), stone);
            Segment(fort.transform, "Стена восток 1", new Vector3(10f, 2.5f, 15f), new Vector3(10f, 5f, 2f), stone);
            Segment(fort.transform, "Стена восток 2", new Vector3(28f, 2.5f, 15f), new Vector3(12f, 5f, 2f), stone);
            Segment(fort.transform, "Левая стена", new Vector3(-35f, 2.5f, 24f), new Vector3(2f, 5f, 20f), stone);
            Segment(fort.transform, "Правая стена", new Vector3(35f, 2.5f, 24f), new Vector3(2f, 5f, 20f), stone);
            Segment(fort.transform, "Задняя стена", new Vector3(0f, 2.5f, 33f), new Vector3(72f, 5f, 2f), stone);
            Segment(fort.transform, "Настил", new Vector3(-25f, 3.5f, 12f), new Vector3(14f, .25f, 4f), wood);
            var ramp = Segment(fort.transform, "Пандус", new Vector3(-24f, 1.75f, 7f), new Vector3(3f, .3f, 10f), wood);
            ramp.transform.rotation = Quaternion.Euler(-19f, 0f, 0f);

            // Road markers are purely visual. Tutorial progresses from actual commands and deaths.
            for (int i = -2; i <= 2; i++)
            {
                var rock = Segment(fort.transform, "Камень " + i,
                    new Vector3(i * 7f, .3f, -12f), new Vector3(1.3f, .6f, 1.2f), earth);
                rock.transform.rotation = Quaternion.Euler(0f, i * 27f, 0f);
            }

            new GameObject("Сон — обучение").AddComponent<DreamTutorialDirector>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
            RegisterInBuildSettings(path);
            AssetDatabase.SaveAssets();
            Debug.Log("[СОН] Отдельная сцена создана: " + path + ". Пролог лагеря не изменён.");
        }

        private static Material DreamMaterial(string name, Color tint)
        {
            const string folder = "Assets/Materials/DreamTutorial";
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/Materials", "DreamTutorial");
            string path = folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) throw new System.InvalidOperationException("Нет подходящего шейдера для сна.");
            material = new Material(shader) { color = tint };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject Segment(Transform parent, string name,
            Vector3 pos, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }
    }
}
#endif
