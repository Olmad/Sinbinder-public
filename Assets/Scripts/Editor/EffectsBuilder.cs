// Assets/Scripts/Editor/EffectsBuilder.cs
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sinbinder.Utilets
{
    /// <summary>
    /// Взгляд игры: цвет, виньетка, зерно и искра для огня.
    ///
    /// Собирается ассетом, а не настраивается руками в инспекторе:
    /// настройка, живущая только в сцене, теряется при первой пересборке,
    /// а пересобираем мы сцены каждый день.
    ///
    /// <b>Зачем это вообще.</b> Модели, текстуры и свет уже есть, но
    /// смотрятся они как набор объектов, а не как кадр. Общий профиль —
    /// то единственное, что сводит разные источники в один вид: сканы
    /// ambientCG, плоские цвета персонажей и огонь костра проходят через
    /// одну кривую (14-HANDOFF §16.3, решение вдвоём).
    ///
    /// Палитра из <c>23-PROMPTS.md</c> §2: приглушённое, холодное, без
    /// золота и блеска. Поэтому насыщенность вниз, температура в синеву,
    /// контраст чуть вверх — и виньетка, которая держит взгляд в центре
    /// кадра, где и происходит игра.
    /// </summary>
    public static class EffectsBuilder
    {
        private const string Dir = "Assets/Settings";
        private const string ProfilePath = Dir + "/Взгляд.asset";
        private const string SparkPath = "Assets/Textures/Искра.png";
        private const string SparkMaterial = "Assets/Materials/Искра.mat";

        [MenuItem("Sinbinder/Собрать эффекты")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(Dir);
            Directory.CreateDirectory("Assets/Materials");

            Profile();
            Spark();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ЭФФЕКТЫ] Готово: профиль взгляда и материал искры.");
        }

        /// <summary>Точка входа для пакетного режима.</summary>
        public static void BuildAllBatch() => BuildAll();

        private static void Profile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);

            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            // Цвет. Насыщенность вниз, в синеву, контраст чуть вверх:
            // ночь, холод, никакого золота (23-PROMPTS.md §2).
            var colour = Grab<ColorAdjustments>(profile);
            colour.saturation.overrideState = true;
            colour.saturation.value = -14f;
            colour.contrast.overrideState = true;
            colour.contrast.value = 10f;
            colour.postExposure.overrideState = true;
            colour.postExposure.value = -0.15f;

            var balance = Grab<WhiteBalance>(profile);
            balance.temperature.overrideState = true;
            balance.temperature.value = -12f;
            balance.tint.overrideState = true;
            balance.tint.value = 4f;

            // Виньетка держит взгляд в середине кадра. Мягкая: жёсткая
            // читается как дырка в маске, а не как свет.
            var vignette = Grab<Vignette>(profile);
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0.34f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.45f;

            // Зерно — то, что отличает «тёмную картинку» от «ночного
            // кадра». Слабое: сильное съедает и без того тёмные детали.
            var grain = Grab<FilmGrain>(profile);
            grain.type.overrideState = true;
            grain.type.value = FilmGrainLookup.Medium1;
            grain.intensity.overrideState = true;
            grain.intensity.value = 0.22f;
            grain.response.overrideState = true;
            grain.response.value = 0.75f;

            // Свечение только у огня: порог высокий, чтобы светился
            // костёр и шар, а не вся картинка разом.
            var bloom = Grab<Bloom>(profile);
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.05f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.55f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.68f;

            var tone = Grab<Tonemapping>(profile);
            tone.mode.overrideState = true;
            tone.mode.value = TonemappingMode.ACES;

            EditorUtility.SetDirty(profile);
        }

        private static T Grab<T>(VolumeProfile profile) where T : VolumeComponent
            => profile.Has<T>() ? (T)profile.components.Find(c => c is T) : profile.Add<T>(true);

        /// <summary>
        /// Искра: мягкое пятно, из которого сделаны угли костра и пламя
        /// факела. Рисуется формулой, а не кистью, — как и значки
        /// действий в проекте: одна зависимость меньше.
        /// </summary>
        private static void Spark()
        {
            if (!File.Exists(SparkPath))
            {
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f;
                    float dy = (y + 0.5f) / size - 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;

                    // Мягкий спад до нуля на краю: квадратная искра
                    // видна квадратом, и весь огонь рассыпается на пиксели.
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a;

                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }

                texture.Apply();
                File.WriteAllBytes(SparkPath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);

                AssetDatabase.ImportAsset(SparkPath);
            }

            var importer = AssetImporter.GetAtPath(SparkPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(SparkMaterial);
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                      ?? Shader.Find("Particles/Standard Unlit")
                      ?? Shader.Find("Sprites/Default");

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, SparkMaterial);
            }

            material.shader = shader;

            var sprite = AssetDatabase.LoadAssetAtPath<Texture2D>(SparkPath);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", sprite);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", sprite);

            // Аддитивное смешивание: искры складываются со светом костра,
            // а не закрывают его серым квадратом.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 1f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.renderQueue = (int)RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");

            EditorUtility.SetDirty(material);
        }

        /// <summary>Профиль взгляда. Спрашивает сборщик сцен.</summary>
        public static VolumeProfile Look()
            => AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);

        /// <summary>Материал искры для костра и факелов.</summary>
        public static Material SparkOf()
            => AssetDatabase.LoadAssetAtPath<Material>(SparkMaterial);
    }
}
