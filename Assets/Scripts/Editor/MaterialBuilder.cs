// Assets/Scripts/Editor/MaterialBuilder.cs
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Sinbinder.Utilets
{
    /// <summary>
    /// Материалы из скачанных текстур — по списку, а не руками.
    ///
    /// Текстуры лежат в <c>Assets/Textures</c> с 13 сентября
    /// (<c>Tools/textures/ambientcg.py</c>, CC0), а материалов в проекте
    /// не было ни одного: тридцать девять файлов лежали мёртвым грузом,
    /// и земля с палатками оставались однотонными. Замер готовности
    /// 18 сентября это и показал — «представление» держалось на моделях
    /// без единой поверхности.
    ///
    /// <b>Тайлинг крупный нарочно.</b> Камера стоит под 84° на высоте
    /// двадцати двух метров; скан с мелкой крошкой читается оттуда
    /// фотографией и спорит с плоскими цветами персонажей
    /// (<c>14-HANDOFF.md</c> §17.3). Крупный тайлинг уводит деталь ниже
    /// порога различимости, и она работает шумом поверхности, а не
    /// фотографией.
    ///
    /// <b>Блеска нет.</b> Гладкость почти в нуле: мир ночной и матовый,
    /// а блик на земле мгновенно выдаёт пластик.
    /// </summary>
    public static class MaterialBuilder
    {
        private const string TexturesDir = "Assets/Textures";
        private const string MaterialsDir = "Assets/Materials";

        /// <summary>
        /// Что во что превращается. Тайлинг — на метр поверхности;
        /// у земли он крупнее всего, потому что земли больше всего.
        /// </summary>
        private static readonly (string Id, float Tiling, float Smoothness)[] Wanted =
        {
            ("Ground048", 14f, 0.04f),
            ("Ground110", 14f, 0.05f),
            ("Gravel043", 18f, 0.05f),
            ("Rock050", 3f, 0.08f),
            ("Rock022", 3f, 0.08f),
            ("PavingStones127", 8f, 0.10f),
            ("Bricks076A", 5f, 0.06f),
            ("Planks037A", 4f, 0.10f),
            ("Fabric061", 3f, 0.05f),
            ("Leather033A", 2f, 0.12f),
        };

        [MenuItem("Sinbinder/Собрать материалы")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(MaterialsDir);

            var shader = Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("Standard");

            if (shader == null)
            {
                Debug.LogError("[МАТЕРИАЛЫ] Шейдера не нашлось ни URP, ни Standard: "
                             + "собирать материалы не из чего.");
                return;
            }

            int made = 0;

            foreach (var (id, tiling, smoothness) in Wanted)
            {
                var colour = Load(id, "Color");
                if (colour == null)
                {
                    Debug.LogWarning($"[МАТЕРИАЛЫ] {id}: нет карты цвета. "
                                   + "Скачайте: python Tools/textures/ambientcg.py");
                    continue;
                }

                string path = $"{MaterialsDir}/{id}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);

                // Пересобираем поверх существующего, а не создаём заново:
                // иначе все ссылки из сцен оборвались бы при каждой правке
                // списка, и сцены пришлось бы пересобирать следом.
                if (material == null)
                {
                    material = new Material(shader);
                    AssetDatabase.CreateAsset(material, path);
                }

                material.shader = shader;
                Set(material, "_BaseMap", colour);
                Set(material, "_MainTex", colour);

                var normal = Load(id, "NormalGL");
                if (normal != null)
                {
                    Set(material, "_BumpMap", normal);
                    material.EnableKeyword("_NORMALMAP");
                }

                var occlusion = Load(id, "AmbientOcclusion");
                if (occlusion != null)
                {
                    Set(material, "_OcclusionMap", occlusion);
                    material.EnableKeyword("_OCCLUSIONMAP");
                }

                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
                if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);

                var scale = new Vector2(tiling, tiling);
                if (material.HasProperty("_BaseMap")) material.SetTextureScale("_BaseMap", scale);
                if (material.HasProperty("_MainTex")) material.SetTextureScale("_MainTex", scale);

                EditorUtility.SetDirty(material);
                made++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[МАТЕРИАЛЫ] Готово: {made} из {Wanted.Length}.");
        }

        /// <summary>Точка входа для пакетного режима.</summary>
        public static void BuildAllBatch() => BuildAll();

        private static Texture2D Load(string id, string map)
            => AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/{id}/{id}_{map}.jpg");

        private static void Set(Material material, string property, Texture texture)
        {
            if (material.HasProperty(property)) material.SetTexture(property, texture);
        }

        /// <summary>
        /// Материал по имени текстуры. Спрашивает сборщик сцен: земля,
        /// скалы и палатки берут поверхность отсюда, а не заводят свою.
        /// </summary>
        public static Material Get(string id)
            => AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/{id}.mat");
    }
}
