// Assets/Scripts/Editor/ModelCheck.cs
using System.Linq;
using UnityEditor;
using UnityEngine;
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.Utilets
{
    /// <summary>
    /// Что на самом деле лежит в <c>Resources</c>: размер, вершины, части.
    ///
    /// Заведено 18 сентября, когда проверка кадра показала сакуру
    /// высотой пять сантиметров, хотя в Blender она три метра. Пока
    /// модель не поставлена в сцену, такое не видно вообще никак:
    /// файл на месте, импорт без ошибок, в проекте тишина.
    ///
    /// Поэтому меряем не в Blender и не на глаз, а ровно то, что получит
    /// игра, — меш из ассета. Тело обязано быть ростом около метра,
    /// предмет — от пяти сантиметров до двадцати метров; всё, что вне
    /// этих границ, почти наверняка сломанный экспорт, а не замысел.
    /// </summary>
    public static class ModelCheck
    {
        /// <summary>Сцены, которые обязаны стоять собранными.</summary>
        private static readonly string[] Scenes =
        {
            "Assets/Scenes/Prologue_Camp.unity",
            "Assets/Scenes/Crypt_Entrance.unity",
            "Assets/Scenes/Сакура.unity",
        };

        [MenuItem("Sinbinder/Проверить модели")]
        public static void All()
        {
            Folder("Assets/Resources/Bodies", 0.6f, 2.6f);
            Folder("Assets/Resources/Props", 0.05f, 20f);
            Folder("Assets/Resources/Wear", 0.01f, 2f);
            ShellWear();

            foreach (var path in Scenes) Scene(path);
        }

        /// <summary>
        /// Свои вещи оболочки (<c>Wear/Skeleton/…</c>) надеваются, только если
        /// <see cref="Wardrobe"/> узнаёт оболочку тела по имени меша, а имя
        /// вещи — из тех, что он умеет надевать. Иначе вещь молча берётся
        /// общая, и на скелете повисает наплечник, сшитый по чужому телу:
        /// сцена собирается, ошибок нет — ровно тот случай, ради которого
        /// эта проверка заведена (заголовок класса).
        /// </summary>
        private static void ShellWear()
        {
            foreach (var dir in AssetDatabase.GetSubFolders("Assets/Resources/Wear"))
            {
                string shell = System.IO.Path.GetFileName(dir);
                var body = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Resources/Bodies/{shell}.fbx");
                var skin = body != null ? body.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
                string seen = Wardrobe.ShellOf(skin);

                if (seen != shell)
                {
                    Debug.LogError($"[ГАРДЕРОБ] Wear/{shell}: тело узнаётся как «{seen}» — "
                                 + "свои вещи этой оболочке не наденутся никогда.");
                    continue;
                }

                int known = 0;
                foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { dir }))
                {
                    string item = System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
                    if (Wardrobe.Knows(item)) known++;
                    else Debug.LogError($"[ГАРДЕРОБ] Wear/{shell}/{item}: такой вещи Wardrobe не знает — "
                                      + "не наденется никогда.");
                }

                Debug.Log($"[ГАРДЕРОБ] Wear/{shell}: своих вещей {known}, тело узнаётся "
                        + $"по мешу «{skin.sharedMesh.name}».");
            }
        }

        /// <summary>
        /// Не уменьшился ли предмет <b>в самой сцене</b>.
        ///
        /// Модель может быть правильной, а в сцене стоять сантиметровой:
        /// так и случилось 18 сентября — сборщик задавал масштаб напрямую
        /// и стирал множитель единиц файла. Предметы исправно стояли
        /// по местам, невидимые, и ни одна проверка этого не ловила:
        /// сцена собирается, объекты на месте, ошибок нет.
        ///
        /// Мерка простая: всё, что мельче трёх сантиметров, — не замысел.
        /// Самая мелкая наша вещь, трещина на черепе, вчетверо крупнее.
        /// </summary>
        private static void Scene(string path)
        {
            if (!System.IO.File.Exists(path))
            {
                Debug.LogWarning($"[СЦЕНА] {path}: нет. Соберите сцены.");
                return;
            }

            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path);
            int tiny = 0, all = 0;

            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer is ParticleSystemRenderer) continue;
                if (renderer.GetComponentInParent<Canvas>() != null) continue;

                all++;
                var size = renderer.bounds.size;
                float tall = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                if (tall >= 0.03f) continue;

                tiny++;
                if (tiny <= 5)
                    Debug.LogError($"[СЦЕНА] {scene.name}: {renderer.name} — "
                                 + $"{tall:0.000} м. Предмет уменьшился при сборке.");
            }

            // Три самых крупных предмета сцены. Мелочь ловится порогом,
            // а великаны — нет: случайно раздутый предмет не ломает
            // ничего, просто занимает четверть кадра, и замечает это
            // только глаз. Облачная сессия спросила 19 сентября про
            // «большую тёмную сферу» — вот способ ответить числом.
            var big = new System.Collections.Generic.List<(float Tall, string Name)>();

            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer is ParticleSystemRenderer) continue;
                if (renderer.GetComponentInParent<Canvas>() != null) continue;

                var size = renderer.bounds.size;
                big.Add((Mathf.Max(size.x, Mathf.Max(size.y, size.z)), renderer.name));
            }

            big.Sort((a, b) => b.Tall.CompareTo(a.Tall));

            for (int i = 0; i < Mathf.Min(3, big.Count); i++)
                Debug.Log($"[СЦЕНА] {scene.name}: крупнее всех — {big[i].Name} "
                        + $"{big[i].Tall:0.0} м");

            if (tiny == 0) Debug.Log($"[СЦЕНА] {scene.name}: {all} поверхностей, мелочи нет.");
            else Debug.LogError($"[СЦЕНА] {scene.name}: уменьшенных {tiny} из {all}.");
        }

        /// <summary>Точка входа для пакетного режима.</summary>
        public static void AllBatch() => All();

        /// <summary>
        /// Банки души — кадром из самой Unity, а не из Блендера: стекло
        /// прозрачным его делает импорт (<c>PropImport</c>), душу красит
        /// игра (<see cref="SoulJarGlow"/>), и ни того ни другого превью
        /// Блендера не покажет. Кадр — <c>Docs/Образцы/предметы/банки.png</c>.
        ///
        /// Пустая сцена: доска, ночной свет, пустая банка и три полные —
        /// жадность с гордыней, гнев с унынием, добродетель гордыни.
        /// Пакетный запуск — без <c>-nographics</c>: иначе рисовать нечем.
        /// </summary>
        public static void JarsBatch()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.10f, 0.10f, 0.13f);

            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.transform.position = new Vector3(0f, -0.02f, 0f);
            board.transform.localScale = new Vector3(1.6f, 0.04f, 0.5f);
            var wood = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            wood.color = new Color(0.20f, 0.14f, 0.09f);
            board.GetComponent<Renderer>().sharedMaterial = wood;

            var back = GameObject.CreatePrimitive(PrimitiveType.Cube);
            back.transform.position = new Vector3(0f, 0.4f, 0.26f);
            back.transform.localScale = new Vector3(1.6f, 0.9f, 0.02f);
            back.GetComponent<Renderer>().sharedMaterial = wood;

            var moon = new GameObject("Луна").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.55f, 0.62f, 0.80f);
            moon.intensity = 0.55f;
            moon.transform.rotation = Quaternion.Euler(38f, -30f, 0f);

            var empty = Resources.Load<GameObject>("Props/SoulJar");
            var full = Resources.Load<GameObject>("Props/SoulJarFull");
            if (empty == null || full == null)
            {
                Debug.LogError("[БАНКИ] Банок нет в Resources/Props — соберите Tools/blender/props.py.");
                return;
            }

            Place(empty, -0.54f);
            Place(full, -0.18f).AddComponent<SoulJarGlow>().Set(SinType.Greed, SinType.Pride, 0.75f);
            Place(full, 0.18f).AddComponent<SoulJarGlow>().Set(SinType.Wrath, SinType.Sloth, 0.55f);
            Place(full, 0.54f).AddComponent<SoulJarGlow>().Set(SinType.Pride, SinType.Envy, 0.60f, virtue: true);

            var camera = new GameObject("Камера").AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0.34f, -1.25f);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.12f, 0f) - camera.transform.position);
            camera.fieldOfView = 34f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.03f, 0.04f);

            const int w = 1600, h = 700;
            var texture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            camera.targetTexture = texture;
            camera.Render();

            var read = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            read.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            read.Apply();
            RenderTexture.active = previous;
            camera.targetTexture = null;

            const string folder = "Docs/Образцы/предметы";
            System.IO.Directory.CreateDirectory(folder);
            string file = folder + "/банки.png";
            System.IO.File.WriteAllBytes(file, read.EncodeToPNG());

            // Прозрачно ли стекло — числом: материал модели после импорта.
            var glass = empty.GetComponentInChildren<Renderer>().sharedMaterials
                             .FirstOrDefault(m => m != null && m.name.StartsWith("Glass"));
            string said = glass == null
                ? "материала «Glass» нет"
                : $"стекло: очередь {glass.renderQueue}, альфа {glass.color.a:0.00}, "
                + $"тип {glass.GetTag("RenderType", false)}";
            Debug.Log($"[БАНКИ] кадр {file}; {said}");
        }

        private static GameObject Place(GameObject prefab, float x)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.transform.position = new Vector3(x, 0f, 0f);
            return go;
        }

        private static void Folder(string path, float least, float most)
        {
            var guids = AssetDatabase.FindAssets("t:Model", new[] { path });
            if (guids.Length == 0)
            {
                Debug.LogWarning($"[МОДЕЛИ] {path}: пусто.");
                return;
            }

            int bad = 0;

            foreach (var guid in guids.OrderBy(g => AssetDatabase.GUIDToAssetPath(g)))
            {
                string file = AssetDatabase.GUIDToAssetPath(guid);
                string name = System.IO.Path.GetFileNameWithoutExtension(file);

                // Меряем экземпляр, а не сам меш. Меш в ассете лежит
                // в единицах файла, а единицы Blender выравниваются
                // масштабом корня при импорте: сырой меш показывает
                // сантиметры там, где игра получает метры.
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(file);
                if (prefab == null)
                {
                    Debug.LogError($"[МОДЕЛИ] {name}: не читается как объект.");
                    bad++;
                    continue;
                }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                var renderers = go.GetComponentsInChildren<Renderer>(true);

                if (renderers.Length == 0)
                {
                    Debug.LogError($"[МОДЕЛИ] {name}: ни одной поверхности.");
                    Object.DestroyImmediate(go);
                    bad++;
                    continue;
                }

                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

                int verts = 0, parts = 0;
                foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(file).OfType<Mesh>())
                {
                    verts += mesh.vertexCount;
                    parts += mesh.subMeshCount;
                }

                Object.DestroyImmediate(go);

                var size = bounds.size;
                float tall = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                bool ok = tall >= least && tall <= most;
                if (!ok) bad++;

                // Масштаб корня — не украшение: им импортёр выравнивает
                // единицы файла. Сборщик сцен обязан на него умножать,
                // а не затирать его, иначе предмет уменьшится в сто раз.
                var root = prefab.transform.localScale;
                var turn = prefab.transform.localRotation.eulerAngles;

                string line = $"[МОДЕЛИ] {name}: "
                            + $"{size.x:0.00}×{size.y:0.00}×{size.z:0.00} м, "
                            + $"вершин {verts}, частей {parts}, корень ×{root.x:0.##}, "
                            + $"середина {bounds.center.x:0.00} {bounds.center.y:0.00} "
                            + $"{bounds.center.z:0.00}"
                            + (turn.sqrMagnitude > 0.01f
                                ? $", поворот {turn.x:0.#} {turn.y:0.#} {turn.z:0.#}" : "");

                if (ok) Debug.Log(line);
                else Debug.LogError(line + $" — вне пределов {least:0.00}…{most:0.00} м");
            }

            Debug.Log($"[МОДЕЛИ] {path}: {guids.Length} шт., негодных {bad}.");
        }
    }
}
