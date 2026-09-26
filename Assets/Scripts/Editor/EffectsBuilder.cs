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
    /// <b>Сверено со спецификацией стиля</b> (<c>00-GDD.md</c> §9,
    /// «Knightcore») 20 сентября: насыщенность −25, контраст +15,
    /// виньетка 0,40, зерно 0,20 крупностью около 1,8, слабое свечение,
    /// лёгкая хроматическая аберрация. До сверки профиль был вдвое
    /// слабее заявленного, а аберрации не было ни одной.
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
        private const string PetalMaterial = "Assets/Materials/Лепесток.mat";

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

            // Пустые ссылки — компоненты, которые прежний сборщик добавлял
            // в память и не клал в файл (Grab). Их девять, и все пустые.
            profile.components.RemoveAll(c => c == null);

            // Автор, 26 сентября, пройдя демо: «Фильтр Knight-core вообще
            // не чувствуется». Профиль был сверен с цифрами §9 — и цифры эти
            // на и без того тёмном кадре не отличались от просто ночи:
            // серо-лиловая каша, где огонь не спорит с холодом. Стиль
            // держится не на ослаблении цвета, а на подписи: холодные тени,
            // старое золото в светах, зерно, которое видно, и чёрные,
            // приподнятые, как у старой картины, а не провал в ноль.

            // Цвет. Насыщенность вниз, контраст вверх — «тёмные тона,
            // высокая контрастность». Контраст сажает середину, поэтому
            // экспозиция уже не в минусе.
            var colour = Grab<ColorAdjustments>(profile);
            colour.saturation.overrideState = true;
            colour.saturation.value = -30f;
            colour.contrast.overrideState = true;
            colour.contrast.value = 30f;
            colour.postExposure.overrideState = true;
            colour.postExposure.value = 0.05f;

            var balance = Grab<WhiteBalance>(profile);
            balance.temperature.overrideState = true;
            balance.temperature.value = -10f;
            balance.tint.overrideState = true;
            balance.tint.value = 4f;

            // Раздельное тонирование — подпись стиля: тени в холодную синеву,
            // света в старое золото (00-GDD.md §9: «палитра сырой земли
            // и старого золота»). Огонь, шар и лица встают золотом на холоде,
            // и встреча тёплого с холодным, на которой держится свет лагеря,
            // становится видна. Перевес — к теням: холодного в кадре больше.
            var split = Grab<SplitToning>(profile);
            split.shadows.overrideState = true;
            split.shadows.value = new Color(0.30f, 0.42f, 0.55f);
            split.highlights.overrideState = true;
            split.highlights.value = new Color(0.80f, 0.63f, 0.38f);
            split.balance.overrideState = true;
            split.balance.value = -20f;

            // Чёрные приподняты и чуть в синеву — потёртая картина, а не
            // провал в ноль: высокий контраст без этого съел бы ночной лагерь.
            var levels = Grab<LiftGammaGain>(profile);
            levels.lift.overrideState = true;
            levels.lift.value = new Vector4(0.97f, 1.0f, 1.06f, 0.035f);

            // Виньетка держит взгляд в середине кадра. Край — не чёрный,
            // а цвета старого лака: так темнеет картина, а не объектив.
            var vignette = Grab<Vignette>(profile);
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0.48f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.42f;
            vignette.color.overrideState = true;
            vignette.color.value = new Color(0.06f, 0.04f, 0.025f);

            // Зерно — то, что отличает «тёмную картинку» от «ночного кадра».
            // Было 0,2 при отклике 0,75 — в темноте его не видел никто:
            // отклик гасит зерно ровно там, где у нас весь кадр.
            var grain = Grab<FilmGrain>(profile);
            grain.type.overrideState = true;
            grain.type.value = FilmGrainLookup.Medium5;
            grain.intensity.overrideState = true;
            grain.intensity.value = 0.42f;
            grain.response.overrideState = true;
            grain.response.value = 0.45f;

            // Свечение только у огня: порог высокий, чтобы светился
            // костёр и шар, а не вся картинка разом.
            var bloom = Grab<Bloom>(profile);
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.0f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.75f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.7f;

            // Хроматическая аберрация — последняя строка описания стиля
            // в 00-GDD.md §9. Она и отличает «тёмную картинку» от кадра,
            // снятого стеклом: по краям цвет расходится, в середине нет.
            // Сильная читается поломкой монитора — эта заметна у края.
            var glass = Grab<ChromaticAberration>(profile);
            glass.intensity.overrideState = true;
            glass.intensity.value = 0.22f;

            var tone = Grab<Tonemapping>(profile);
            tone.mode.overrideState = true;
            tone.mode.value = TonemappingMode.ACES;

            // Значения живут в самих компонентах, а не в профиле: помечаем
            // каждый, иначе в файл ушёл бы только список.
            foreach (var component in profile.components) EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(profile);
        }

        /// <summary>
        /// Компонент профиля: найти или добавить — и добавленный положить
        /// в файл профиля.
        ///
        /// Компонент — отдельный объект внутри файла профиля. Прежде он
        /// только добавлялся в список: в памяти редактора профиль работал,
        /// а в файл ложилась пустая ссылка (<c>fileID: 0</c>) — со следующего
        /// запуска профиль был пуст. Так с 20 сентября: «Knightcore» сверили
        /// кадром в той же сессии, где собрали, а в игре его не было ни дня.
        /// Автор, 26 сентября: «Фильтр Knight-core вообще не чувствуется».
        /// </summary>
        private static T Grab<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.Has<T>()) return (T)profile.components.Find(c => c is T);

            var added = profile.Add<T>(true);
            added.name = typeof(T).Name;
            added.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(added, profile);
            return added;
        }

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
            //
            // <b>_Blend здесь — не «включить смешивание», а его род</b>
            // (0 — обычная прозрачность, 1 — предумноженная, 2 —
            // аддитивная). Единица, стоявшая тут до 19 сентября, означала
            // предумноженную: белый квадрат текстуры складывался с фоном
            // целиком, и лепестки сакуры сыпались белыми квадратиками
            // с чёткими краями. Видно это стало только на снимке.
            Blend(material, 2f, UnityEngine.Rendering.BlendMode.SrcAlpha,
                  UnityEngine.Rendering.BlendMode.One);

            EditorUtility.SetDirty(material);

            Petal();
        }

        /// <summary>
        /// Род смешивания разом: и числами, и словом. URP смотрит и туда
        /// и туда, а при пересборке материала сверяет одно с другим.
        /// </summary>
        private static void Blend(Material material, float kind,
            UnityEngine.Rendering.BlendMode src, UnityEngine.Rendering.BlendMode dst)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", kind);
            material.SetFloat("_SrcBlend", (float)src);
            material.SetFloat("_DstBlend", (float)dst);
            material.SetFloat("_SrcBlendAlpha", (float)src);
            material.SetFloat("_DstBlendAlpha", (float)dst);
            material.SetFloat("_ZWrite", 0f);
            material.renderQueue = (int)RenderQueue.Transparent;

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHAMODULATE_ON");
        }

        /// <summary>
        /// Лепесток: та же текстура, но обычная прозрачность.
        ///
        /// Аддитивный лепесток на закатном небе не виден вовсе — он
        /// складывается с тем, что и так светлее его. Огню аддитивность
        /// нужна, цветку — нет, и это два материала, а не один.
        /// </summary>
        private static void Petal()
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                      ?? Shader.Find("Sprites/Default");
            if (shader == null) return;

            var material = AssetDatabase.LoadAssetAtPath<Material>(PetalMaterial);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, PetalMaterial);
            }

            material.shader = shader;

            var sprite = AssetDatabase.LoadAssetAtPath<Texture2D>(SparkPath);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", sprite);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", sprite);

            Blend(material, 0f, UnityEngine.Rendering.BlendMode.SrcAlpha,
                  UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

            EditorUtility.SetDirty(material);
        }

        /// <summary>Материал лепестка. Спрашивает сцена с сакурой.</summary>
        public static Material PetalOf()
            => AssetDatabase.LoadAssetAtPath<Material>(PetalMaterial);

        /// <summary>Профиль взгляда. Спрашивает сборщик сцен.</summary>
        public static VolumeProfile Look()
            => AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);

        /// <summary>Материал искры для костра и факелов.</summary>
        public static Material SparkOf()
            => AssetDatabase.LoadAssetAtPath<Material>(SparkMaterial);
    }
}
