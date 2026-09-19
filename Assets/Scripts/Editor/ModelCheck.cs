// Assets/Scripts/Editor/ModelCheck.cs
using System.Linq;
using UnityEditor;
using UnityEngine;

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
            "Assets/Scenes/Prologue_Raid.unity",
            "Assets/Scenes/Crypt_Entrance.unity",
            "Assets/Scenes/Сакура.unity",
        };

        [MenuItem("Sinbinder/Проверить модели")]
        public static void All()
        {
            Folder("Assets/Resources/Bodies", 0.6f, 2.6f);
            Folder("Assets/Resources/Props", 0.05f, 20f);
            Folder("Assets/Resources/Wear", 0.01f, 2f);

            foreach (var path in Scenes) Scene(path);
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
