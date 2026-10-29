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
            Flame();

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

            // Подобрано по кадрам, а не по цифрам (26 сентября, проба в
            // редакторе: лагерь сверху и от первого лица, четыре варианта).
            // Цифры §9 под ACES давали почти чёрный кадр: тонирующая кривая
            // ACES на ночной сцене с виньеткой и контрастом сажала лагерь
            // в ноль. Нейтральная кривая держит тёмное читаемым.

            // Цвет. Насыщенность вниз, контраст вверх — «тёмные тона,
            // высокая контрастность». Экспозиция поднята под нейтральную
            // кривую: с ней кадр не темнее, чем без обработки.
            var colour = Grab<ColorAdjustments>(profile);
            colour.saturation.overrideState = true;
            colour.saturation.value = -28f;
            colour.contrast.overrideState = true;
            colour.contrast.value = 22f;
            colour.postExposure.overrideState = true;
            colour.postExposure.value = 0.4f;

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
            // Середина тона — серый 0,5: чем дальше цвет от него, тем сильнее
            // сдвиг. Первый заход (0,30/0,42/0,55 с перевесом к теням
            // и приподнятыми чёрными) залил кадр в игре ровной сине-серой
            // мглой — лагерь не читался вовсе.
            var split = Grab<SplitToning>(profile);
            split.shadows.overrideState = true;
            // 26 октября (облачная, «Красота», п. 6: «тёплое/холодное — огонь против
            // луны»): тени синее, света золотее — на кадрах набега встреча почти
            // не читалась. Середина та же, сдвиг сильнее на треть.
            split.shadows.value = new Color(0.38f, 0.46f, 0.60f);
            split.highlights.overrideState = true;
            split.highlights.value = new Color(0.68f, 0.54f, 0.36f);
            split.balance.overrideState = true;
            split.balance.value = 0f;

            // Подъём чёрных из первого захода — выброшен: он и давал мглу.
            if (profile.Has<LiftGammaGain>())
            {
                var lifted = (LiftGammaGain)profile.components.Find(c => c is LiftGammaGain);
                profile.Remove<LiftGammaGain>();
                Object.DestroyImmediate(lifted, true);
            }

            // Виньетка держит взгляд в середине кадра. Край — не чёрный,
            // а цвета старого лака: так темнеет картина, а не объектив.
            var vignette = Grab<Vignette>(profile);
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0.36f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.45f;
            vignette.color.overrideState = true;
            vignette.color.value = new Color(0.06f, 0.04f, 0.025f);

            // Зерно — то, что отличает «тёмную картинку» от «ночного кадра».
            // Было 0,2 при отклике 0,75 — в темноте его не видел никто:
            // отклик гасит зерно ровно там, где у нас весь кадр.
            var grain = Grab<FilmGrain>(profile);
            grain.type.overrideState = true;
            grain.type.value = FilmGrainLookup.Medium5;
            grain.intensity.overrideState = true;
            grain.intensity.value = 0.38f;
            grain.response.overrideState = true;
            grain.response.value = 0.55f;

            // Свечение только у огня: порог высокий, чтобы светился
            // костёр и шар, а не вся картинка разом.
            var bloom = Grab<Bloom>(profile);
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 0.85f;   // 26.10: огонь светит заметнее (облачная, «Красота», п. 6)
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 1.0f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.7f;

            // Хроматическая аберрация — последняя строка описания стиля
            // в 00-GDD.md §9. Она и отличает «тёмную картинку» от кадра,
            // снятого стеклом: по краям цвет расходится, в середине нет.
            // Сильная читается поломкой монитора — эта заметна у края.
            var glass = Grab<ChromaticAberration>(profile);
            glass.intensity.overrideState = true;
            glass.intensity.value = 0.18f;

            // Нейтральная, а не ACES: см. начало — ACES сажал ночь в ноль.
            var tone = Grab<Tonemapping>(profile);
            tone.mode.overrideState = true;
            tone.mode.value = TonemappingMode.Neutral;

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

        // ---------------------------------------------------------------- огонь

        private const string FlamePath = "Assets/Textures/Пламя.png";
        private const string FlameMaterial = "Assets/Materials/Пламя.mat";
        private const string SmokePath = "Assets/Textures/Дым.png";
        private const string SmokeMaterial = "Assets/Materials/Дым.mat";

        /// <summary>Кадров пламени по стороне листа: 4×4 — шестнадцать языков.</summary>
        private const int FlameTiles = 4;

        /// <summary>
        /// Языки пламени и дым (автор, 29.10: «поднять бы качество огня»). До того огонь был светом, искрами
        /// и тлеющим комком модели — издали оранжевая точка, вблизи — шар. Язык — лист 4×4 кадров, нарисованных
        /// формулой (как искра): капля, сужается кверху, кончик гнётся и рвётся шумом Перлина по номеру кадра.
        /// Белый с яркой серединой — цвет даёт частица, жёлтый у основания, багровый на излёте.
        /// </summary>
        private static void Flame()
        {
            if (!File.Exists(FlamePath))
            {
                const int cell = 64;
                int size = cell * FlameTiles;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int f = 0; f < FlameTiles * FlameTiles; f++)
                {
                    int ox = (f % FlameTiles) * cell;
                    int oy = (FlameTiles - 1 - f / FlameTiles) * cell;   // кадр 0 — левый верхний, как читает листовая анимация
                    float phase = f / (float)(FlameTiles * FlameTiles) * Mathf.PI * 2f;
                    for (int y = 0; y < cell; y++)
                    for (int x = 0; x < cell; x++)
                    {
                        float u = (x + 0.5f) / cell * 2f - 1f;           // −1…1 поперёк
                        float v = (y + 0.5f) / cell;                    // 0 — низ, 1 — верх
                        float sway = 0.22f * Mathf.Pow(v, 1.6f) * Mathf.Sin(phase + v * 3.5f)
                                   + 0.20f * v * (Mathf.PerlinNoise(f * 1.37f, v * 2.6f) - 0.5f);
                        float width = 0.62f * Mathf.Pow(Mathf.Clamp01(1f - v), 0.75f) * Mathf.Sqrt(Mathf.Clamp01(v / 0.18f));
                        float d = width > 1e-3f ? Mathf.Abs(u - sway) / width : 9f;
                        float n = Mathf.PerlinNoise(u * 2.8f + f * 0.61f, v * 4.2f - f * 0.93f);
                        float a = Mathf.Clamp01((1f - d) * 2.4f + (n - 0.5f) * 1.6f * v);
                        a *= Mathf.Clamp01((1f - v) * 3.2f);
                        float core = Mathf.Clamp01(1f - d * 1.6f) * (1f - v * 0.5f);
                        float c = Mathf.Lerp(0.55f, 1f, core);
                        texture.SetPixel(ox + x, oy + y, new Color(c, c, c, a * a));
                    }
                }
                texture.Apply();
                File.WriteAllBytes(FlamePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(FlamePath);
            }
            if (!File.Exists(SmokePath))
            {
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                    float n = Mathf.PerlinNoise(x * 0.09f, y * 0.09f) * 0.6f + Mathf.PerlinNoise(x * 0.23f + 7f, y * 0.23f) * 0.4f;
                    float a = Mathf.Clamp01(1f - r) * Mathf.Lerp(0.45f, 1f, n);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
                texture.Apply();
                File.WriteAllBytes(SmokePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(SmokePath);
            }
            foreach (var path in new[] { FlamePath, SmokePath })
            {
                if (AssetImporter.GetAtPath(path) is TextureImporter importer)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.SaveAndReimport();
                }
            }
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            if (shader == null) return;
            var flame = ParticleMaterial(FlameMaterial, shader, FlamePath);
            Blend(flame, 2f, UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.One);
            EditorUtility.SetDirty(flame);
            var smoke = ParticleMaterial(SmokeMaterial, shader, SmokePath);
            Blend(smoke, 0f, UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            EditorUtility.SetDirty(smoke);
        }

        private static Material ParticleMaterial(string path, Shader shader, string texture)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            var sprite = AssetDatabase.LoadAssetAtPath<Texture2D>(texture);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", sprite);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", sprite);
            return material;
        }

        /// <summary>
        /// Огонь в точке: языки пламени (лицом к камере с любой стороны — у огня нет стороны) и дым над ними.
        /// scale — 1 у костра, ~0,35 у факела. Свет и искры ставит зовущий: у костра и факела они разные.
        /// bright — насколько светит (1 — костёр); угли под котлом — тусклее (автор, 29.10: «ярко-жёлтый огонь под котлом»).
        /// </summary>
        public static void Fire(Transform parent, Vector3 at, float scale, float rate, bool smoke = true, float bright = 1f)
        {
            // Сборщик сцен зовёт без «Собрать эффекты» — листы пламени и дыма рисуются при первой нужде.
            if (AssetDatabase.LoadAssetAtPath<Material>(FlameMaterial) == null) Flame();
            var flameMat = AssetDatabase.LoadAssetAtPath<Material>(FlameMaterial);
            if (flameMat != null)
            {
                var go = new GameObject("Пламя");
                go.transform.SetParent(parent);
                go.transform.position = at;
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 2f;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f * Mathf.Sqrt(scale) + 0.15f, 0.75f * Mathf.Sqrt(scale) + 0.2f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f * scale, 0.7f * scale);
                main.startSize = new ParticleSystem.MinMaxCurve(0.55f * scale, 0.85f * scale);
                main.startRotation = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
                main.startColor = Color.white;
                main.gravityModifier = -0.12f;
                main.maxParticles = 80;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;   // под моделью (корень ×100) — мировой масштаб, а не свой

                var emission = ps.emission;
                emission.rateOverTime = rate;

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 6f;
                shape.radius = 0.16f * scale;
                shape.rotation = new Vector3(-90f, 0f, 0f);

                // Жёлто-белое у основания → оранжевое → багровое и прозрачное на излёте.
                var colour = ps.colorOverLifetime;
                colour.enabled = true;
                var g = new Gradient();
                g.SetKeys(
                    new[] { new GradientColorKey(new Color(1f, 0.86f, 0.55f), 0f),
                            new GradientColorKey(new Color(1f, 0.52f, 0.16f), 0.45f),
                            new GradientColorKey(new Color(0.55f, 0.10f, 0.03f), 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f * bright, 0.12f),
                            new GradientAlphaKey(0.55f * bright, 0.6f), new GradientAlphaKey(0f, 1f) });
                colour.color = new ParticleSystem.MinMaxGradient(g);

                var size = ps.sizeOverLifetime;
                size.enabled = true;
                var curve = new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.35f));
                size.size = new ParticleSystem.MinMaxCurve(1f, curve);

                // Языки сходятся к середине — пламя конусом, а не столбом.
                var pull = ps.velocityOverLifetime;
                pull.enabled = true;
                pull.space = ParticleSystemSimulationSpace.Local;
                pull.radial = new ParticleSystem.MinMaxCurve(-0.25f * scale);

                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = 0.25f * scale;
                noise.frequency = 1.2f;
                noise.scrollSpeed = 0.8f;

                var sheet = ps.textureSheetAnimation;
                sheet.enabled = true;
                sheet.numTilesX = FlameTiles;
                sheet.numTilesY = FlameTiles;
                sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, FlameTiles * FlameTiles - 1);
                sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
                sheet.cycleCount = 1;

                var r = go.GetComponent<ParticleSystemRenderer>();
                r.sharedMaterial = flameMat;
                r.renderMode = ParticleSystemRenderMode.Billboard;
                r.sortingFudge = -1f;    // поверх дыма
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            var smokeMat = smoke ? AssetDatabase.LoadAssetAtPath<Material>(SmokeMaterial) : null;
            if (smokeMat != null)
            {
                var go = new GameObject("Дым");
                go.transform.SetParent(parent);
                go.transform.position = at + Vector3.up * 0.7f * scale;
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 5f;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(3.0f, 4.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.5f * scale, 0.8f * scale);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.gravityModifier = -0.02f;
                main.maxParticles = 40;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                var emission = ps.emission;
                emission.rateOverTime = Mathf.Max(1.5f, rate * 0.18f);
                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 10f;
                shape.radius = 0.1f * scale;
                shape.rotation = new Vector3(-90f, 0f, 0f);
                var colour = ps.colorOverLifetime;
                colour.enabled = true;
                var g = new Gradient();
                g.SetKeys(
                    new[] { new GradientColorKey(new Color(0.30f, 0.27f, 0.25f), 0f), new GradientColorKey(new Color(0.20f, 0.20f, 0.22f), 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.28f, 0.2f), new GradientAlphaKey(0f, 1f) });
                colour.color = new ParticleSystem.MinMaxGradient(g);
                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 3.2f));
                var spin = ps.rotationOverLifetime;
                spin.enabled = true;
                spin.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = 0.3f;
                noise.frequency = 0.4f;
                var r = go.GetComponent<ParticleSystemRenderer>();
                r.sharedMaterial = smokeMat;
                r.renderMode = ParticleSystemRenderMode.Billboard;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        /// <summary>Материал искры для костра и факелов.</summary>
        public static Material SparkOf()
            => AssetDatabase.LoadAssetAtPath<Material>(SparkMaterial);
    }
}
