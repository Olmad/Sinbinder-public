#if UNITY_EDITOR
// A controlled Unity authoring workflow, never invoked while loading user JSON.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Sinbinder.EditorTools
{
    public sealed class MapAssetImporter : EditorWindow
    {
        private string assetId = "MyAsset";
        private GameObject sourceModel;
        private Material sourceMaterial;
        private Texture2D sourceTexture;
        private bool addBoxCollider = true;

        [MenuItem("Sinbinder/Редактор карт/Импорт моделей и материалов")]
        private static void ShowWindow()
        {
            var window = GetWindow<MapAssetImporter>("Sinbinder: ассеты карты");
            window.minSize = new Vector2(480, 340);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Ассеты пользовательских карт", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Модели FBX/OBJ импортируются в проект Unity и становятся prefab. " +
                "Материалы копируются в Resources для карт. JSON карты не запускает код и " +
                "не читает произвольные файлы.", MessageType.Info);
            assetId = EditorGUILayout.TextField("Имя (A-Z, 0-9, _ и -)", assetId);
            if (!ValidName(assetId))
            {
                EditorGUILayout.HelpBox("Имя 1–48 символов, латиница, цифры, _ или -.", MessageType.Error);
                return;
            }

            GUILayout.Space(10);
            EditorGUILayout.LabelField("3D-модель", EditorStyles.boldLabel);
            sourceModel = (GameObject)EditorGUILayout.ObjectField("Модель в проекте", sourceModel, typeof(GameObject), false);
            addBoxCollider = EditorGUILayout.Toggle("Коллайдер, если отсутствует", addBoxCollider);
            if (GUILayout.Button("Выбрать FBX / OBJ с диска")) ImportModelFile();
            if (GUILayout.Button("Создать игровой prefab из модели") && sourceModel != null)
                CreatePrefab(sourceModel);

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Материал", EditorStyles.boldLabel);
            sourceMaterial = (Material)EditorGUILayout.ObjectField("Материал в Unity", sourceMaterial, typeof(Material), false);
            sourceTexture = (Texture2D)EditorGUILayout.ObjectField("Текстура для нового URP-материала", sourceTexture, typeof(Texture2D), false);
            if (GUILayout.Button("Выбрать PNG / JPG с диска")) ImportTextureFile();
            if (GUILayout.Button("Создать материал для объектов")) CreateMaterial(false);
            if (GUILayout.Button("Создать материал-кисть для земли")) CreateMaterial(true);
            EditorGUILayout.HelpBox(
                "После создания запустите редактор карты заново для обновления палитры. " +
                "Шейдер, цвет, прозрачность и масштаб текстуры можно настроить в Inspector. " +
                "Готовые материалы земли: Ground048, Ground077, Gravel043, Rock022.",
                MessageType.None);
        }

        private static bool ValidName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 48) return false;
            foreach (char c in name)
                if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') &&
                    !(c >= '0' && c <= '9') && c != '_' && c != '-') return false;
            return true;
        }

        private static void EnsureDirectory(string path)
        {
            Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }

        private bool CheckNewAsset(string path)
        {
            if (!ValidName(assetId))
            {
                EditorUtility.DisplayDialog("Sinbinder", "Недопустимое имя ассета.", "OK");
                return false;
            }
            if (File.Exists(path) || AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                EditorUtility.DisplayDialog("Sinbinder",
                    "Ассет уже существует. Выберите другое имя, чтобы не перезаписать карту.", "OK");
                return false;
            }
            return true;
        }

        private void ImportModelFile()
        {
            string file = EditorUtility.OpenFilePanel("Импорт FBX или OBJ", "", "fbx,obj");
            if (string.IsNullOrEmpty(file)) return;
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext != ".fbx" && ext != ".obj") return;
            const string dir = "Assets/MapAuthoringImports/Models";
            EnsureDirectory(dir);
            string target = dir + "/" + assetId + ext;
            if (!CheckNewAsset(target)) return;
            File.Copy(file, target);
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
            sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(target);
            if (sourceModel == null)
                Debug.LogWarning("[Sinbinder] Модель импортирована, но Unity не создал GameObject: " + target);
        }

        private void ImportTextureFile()
        {
            string file = EditorUtility.OpenFilePanel("Импорт PNG / JPG", "", "png,jpg,jpeg");
            if (string.IsNullOrEmpty(file)) return;
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg") return;
            const string dir = "Assets/MapAuthoringImports/Textures";
            EnsureDirectory(dir);
            string target = dir + "/" + assetId + ext;
            if (!CheckNewAsset(target)) return;
            File.Copy(file, target);
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
            sourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(target);
        }

        private void CreatePrefab(GameObject source)
        {
            const string dir = "Assets/Resources/MapUserProps";
            EnsureDirectory(dir);
            string target = dir + "/" + assetId + ".prefab";
            if (!CheckNewAsset(target)) return;
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(source);
            if (obj == null) { Debug.LogError("[Sinbinder] Невозможно создать объект модели."); return; }
            try
            {
                obj.name = assetId;
                if (addBoxCollider && obj.GetComponentInChildren<Collider>() == null)
                {
                    var renderers = obj.GetComponentsInChildren<Renderer>();
                    if (renderers.Length > 0)
                    {
                        Bounds bounds = renderers[0].bounds;
                        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                        var box = obj.AddComponent<BoxCollider>();
                        box.center = obj.transform.InverseTransformPoint(bounds.center);
                        var size = obj.transform.InverseTransformVector(bounds.size);
                        box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(obj, target);
                AssetDatabase.SaveAssets();
                Debug.Log("[Sinbinder] Imported map prefab: user:" + assetId);
            }
            finally { DestroyImmediate(obj); }
        }

        private void CreateMaterial(bool terrain)
        {
            string dir = terrain ? "Assets/Resources/MapTerrainMaterials"
                                 : "Assets/Resources/MapUserMaterials";
            EnsureDirectory(dir);
            string target = dir + "/" + assetId + ".mat";
            if (!CheckNewAsset(target)) return;
            Material material;
            if (sourceMaterial != null) material = new Material(sourceMaterial);
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    EditorUtility.DisplayDialog("Sinbinder", "Не найден URP/Lit shader.", "OK");
                    return;
                }
                material = new Material(shader);
                if (sourceTexture != null)
                {
                    material.SetTexture("_BaseMap", sourceTexture);
                    material.SetTextureScale("_BaseMap", terrain ? new Vector2(14,14) : Vector2.one);
                }
            }
            material.name = assetId;
            AssetDatabase.CreateAsset(material, target);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Sinbinder] Imported map material: " + target);
        }
    }
}
#endif
