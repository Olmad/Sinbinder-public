// Assets/Scripts/Editor/LocationShots.cs
// Кадры мест для карточки экспедиции (автор, 23.11: «в карточке локации сверху нужен эпичный
// красивый кадр локации»). Каждое место собирается в пустой сцене из моделей игры (Resources/
// MapProps, Props) и материалов (Assets/Materials): ночь, туман, луна, огонь, силуэты холмов и
// деревьев; снимается своей камерой с постобработкой (свечение, виньетка, тона) и ложится в
// Resources/Expeditions/<место>.jpg — оттуда его берёт карточка (UI.ReliefMap.Shot).
//
// Верстак (вне игры — сцена заменяется пустой): call Sinbinder.EditorTools.LocationShots.BakeAll
// Потом снова open нужной сцены. Копии для глаза — Logs/workbench/shots/место-<ключ>.png.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Sinbinder.EditorTools
{
    public static partial class LocationShots
    {
        private const string OutDir = "Assets/Resources/Expeditions";
        private const int W = 1920, H = 480;

        internal static readonly (string key, Action build)[] Places =
        {
            ("salt-road", SaltRoad), ("roadside-chapel", Chapel), ("flooded-quarry", Quarry),
            ("burned-outpost", Outpost), ("old-garrison", Garrison), ("bone-marsh", Marsh),
        };

        public static string BakeAll()
        {
            if (EditorApplication.isPlaying) return "нужно вне игры (stop): сцена заменяется пустой";
            var done = new List<string>();
            foreach (var (key, _) in Places) done.Add(Bake(key));
            AssetDatabase.Refresh();
            return string.Join("\n", done);
        }

        public static string Bake(string key)
        {
            if (EditorApplication.isPlaying) return "нужно вне игры (stop)";
            Action build = null;
            foreach (var p in Places) if (p.key == key) build = p.build;
            if (build == null) return "нет такого места: " + key;

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _lit = null;
            _sky = null;
            Flames.Clear();
            Camera cam = null;
            _shot = (from, to, fov) => cam = Cam(from, to, fov);
            build();
            if (cam == null) return key + ": кадр не поставлен";

            var tex = Render(cam);
            Directory.CreateDirectory(OutDir);
            File.WriteAllBytes($"{OutDir}/{key}.jpg", tex.EncodeToJPG(90));
            Directory.CreateDirectory(CleanShot.Folder);
            File.WriteAllBytes($"{CleanShot.Folder}/место-{key}.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset($"{OutDir}/{key}.jpg");
            return $"{key}: {Path.GetFullPath($"{CleanShot.Folder}/место-{key}.png")}";
        }

        // ── общее ──

        internal static Action<Vector3, Vector3, float> _shot;
        internal static Material _lit;

        internal static Camera Cam(Vector3 from, Vector3 to, float fov)
        {
            var go = new GameObject("Камера кадра");
            var cam = go.AddComponent<Camera>();
            cam.transform.position = from;
            cam.transform.rotation = Quaternion.LookRotation((to - from).normalized, Vector3.up);
            cam.fieldOfView = fov;
            cam.aspect = (float)W / H;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            cam.allowHDR = true;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            return cam;
        }

        internal static Texture2D Render(Camera cam) => Render(cam, W, H);

        internal static Texture2D Render(Camera cam, int w, int h)
        {
            float aspect = cam.aspect;
            cam.aspect = (float)w / h;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            rt.Create();
            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
            else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
            cam.aspect = aspect;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            rt.Release();
            Object.DestroyImmediate(rt);
            return tex;
        }

        /// <summary>Ночь: небо градиентом, туман цвета горизонта, луна, лунный свет, постобработка.</summary>
        internal static void Night(Color zenith, Color horizon, Color ambient, float fog, Vector3 moonDir,
                                  Color moonLight, float moonIntensity, float moonSize = 26f)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = fog;
            RenderSettings.fogColor = horizon;
            // Отражения — своё ночное небо, а не дневное по умолчанию (вода выходила белой).
            var cube = new Cubemap(16, TextureFormat.RGBA32, false);
            foreach (CubemapFace face in new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY,
                                                  CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ })
            {
                var px = new Color[16 * 16];
                for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    Color c = face == CubemapFace.PositiveY ? zenith
                            : face == CubemapFace.NegativeY ? horizon * 0.15f
                            : Color.Lerp(horizon, zenith, (15 - y) / 15f * 0.8f);
                    px[y * 16 + x] = c;
                }
                cube.SetPixels(px, face);
            }
            cube.Apply();
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = cube;
            RenderSettings.reflectionIntensity = 0.4f;

            // Небо — большая плоскость с градиентом за всем (без тумана: Sprites/Default).
            // Небо ставится перед камерой в Shoot — где бы она ни стояла.
            _sky = Quad("Небо", Vector3.zero, new Vector3(4200f, 560f, 1f), Gradient(zenith, horizon)).transform;

            // Луна и ореол.
            var at = moonDir.normalized * 600f;
            var halo = Quad("Ореол луны", at, Vector3.one * moonSize * 7f, Soft(), new Color(moonLight.r, moonLight.g, moonLight.b, 0.22f));
            halo.transform.rotation = Quaternion.LookRotation(at.normalized);
            var moon = Quad("Луна", at * 0.99f, Vector3.one * moonSize, Disc(), new Color(2.4f, 2.35f, 2.2f, 1f));
            moon.transform.rotation = Quaternion.LookRotation(at.normalized);

            var lightGo = new GameObject("Лунный свет");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = moonLight;
            light.intensity = moonIntensity;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.85f;
            lightGo.transform.rotation = Quaternion.LookRotation(-moonDir.normalized + Vector3.down * 0.2f);

            var volGo = new GameObject("Постобработка");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            vol.sharedProfile = profile;
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(1.6f); bloom.threshold.Override(0.85f); bloom.scatter.Override(0.72f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.42f); vignette.smoothness.Override(0.5f);
            var grade = profile.Add<ColorAdjustments>(true);
            grade.contrast.Override(18f); grade.saturation.Override(-4f); grade.postExposure.Override(0.9f);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
        }

        internal static Material Lit()
        {
            if (_lit != null) return _lit;
            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _lit = probe.GetComponent<Renderer>().sharedMaterial;
            Object.DestroyImmediate(probe);
            return _lit;
        }

        internal static Material Mat(string name, Color tint, float tiling = 1f)
        {
            var src = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Materials/{name}.mat");
            var m = new Material(src != null ? src : Lit());
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.GetColor("_BaseColor") * tint);
            else m.color *= tint;
            m.mainTextureScale = Vector2.one * tiling;
            return m;
        }

        internal static Material Plain(Color c)
        {
            var m = new Material(Lit());
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); else m.color = c;
            return m;
        }

        /// <summary>Чёрная гладкая вода: блик луны и огней, а не белая плита (Вода.mat в кадре бела).</summary>
        internal static Material Water()
        {
            var m = Plain(new Color(0.015f, 0.03f, 0.035f));
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.93f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.2f);
            return m;
        }

        internal static GameObject Box(string name, Vector3 at, Vector3 size, Material mat, float yaw = 0f,
                                      float tilt = 0f, PrimitiveType type = PrimitiveType.Cube)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(tilt, yaw, 0f);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        internal static GameObject Prop(string name, Vector3 at, float yaw = 0f, float scale = 1f, float tilt = 0f)
        {
            var prefab = Resources.Load<GameObject>("MapProps/" + name);
            if (prefab == null) prefab = Resources.Load<GameObject>("Props/" + name);
            if (prefab == null) { Debug.LogWarning("[КАДРЫ МЕСТ] нет модели " + name); return null; }
            // Поворот импорта сохраняется — как ставит MapLoader.Place (иначе модели лежат на боку).
            var go = (GameObject)Object.Instantiate(prefab);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(tilt, yaw, 0f) * prefab.transform.rotation);
            go.transform.localScale = prefab.transform.localScale * scale;
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
            return go;
        }

        internal static void Ground(string material, Color tint, float tiling = 40f, float size = 900f)
            => Box("Земля", new Vector3(0f, -0.5f, size * 0.4f), new Vector3(size, 1f, size), Mat(material, tint, tiling));

        /// <summary>Холм вдали — в тумане становится силуэтом.</summary>
        internal static void Hill(Vector3 at, Vector3 size, Color c)
            => Box("Холм", at, size, Plain(c), 0f, 0f, PrimitiveType.Sphere);

        internal static void Fire(Vector3 at, float power = 1f, Color? colour = null)
        {
            var c = colour ?? new Color(1f, 0.55f, 0.18f);
            var go = new GameObject("Огонь");
            go.transform.position = at;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = c;
            light.range = 9f * power;
            light.intensity = 4f * power;
            light.shadows = LightShadows.Soft;
            var core = Quad("Пламя", at, Vector3.one * 0.7f * power, Soft(), new Color(c.r * 4f, c.g * 3f, c.b * 2f, 1f));
            Flames.Add(core.transform);
        }

        internal static void Trees(string[] kinds, Vector3 from, Vector3 to, int count, float spread, float scale, int seed)
        {
            var rnd = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                float t = (float)rnd.NextDouble();
                var p = Vector3.Lerp(from, to, t) + new Vector3((float)(rnd.NextDouble() - 0.5) * spread, 0f,
                                                                (float)(rnd.NextDouble() - 0.5) * spread);
                Prop(kinds[rnd.Next(kinds.Length)], p, (float)rnd.NextDouble() * 360f,
                     scale * (0.8f + 0.5f * (float)rnd.NextDouble()));
            }
        }

        internal static GameObject Quad(string name, Vector3 at, Vector3 size, Texture2D tex, Color? colour = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.position = at;
            go.transform.localScale = size;
            var m = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex, color = colour ?? Color.white };
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        internal static Texture2D Gradient(Color top, Color bottom)
        {
            var t = new Texture2D(1, 256, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 256; y++)
            {
                float k = y / 255f;
                t.SetPixel(0, y, Color.Lerp(bottom, top, Mathf.Pow(k, 0.7f)));
            }
            t.Apply();
            return t;
        }

        internal static Texture2D _soft, _disc;
        internal static Texture2D Soft() => _soft != null ? _soft : (_soft = Round(0.0f));
        internal static Texture2D Disc() => _disc != null ? _disc : (_disc = Round(0.9f));

        internal static Texture2D Round(float hard)
        {
            const int n = 128;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = hard > 0f ? Mathf.Clamp01((1f - r) / (1f - hard)) : Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            t.Apply();
            return t;
        }

        /// <summary>Пламя смотрит в камеру кадра (ставится один раз при съёмке).</summary>
        internal static readonly List<Transform> Flames = new();

        internal static void FaceFlames(Vector3 eye)
        {
            foreach (var f in Flames)
                if (f != null) f.rotation = Quaternion.LookRotation(f.position - eye);
            Flames.Clear();
        }

        internal static Transform _sky;

        internal static void Shoot(Vector3 from, Vector3 to, float fov = 38f)
        {
            Stage(from, to);
            _shot(from, to, fov);
        }

        /// <summary>Небо — перед камерой, пламя — к ней лицом, свет со спины зрителя.</summary>
        internal static void Stage(Vector3 from, Vector3 to)
        {
            FaceFlames(from);
            var flat = Vector3.ProjectOnPlane(to - from, Vector3.up).normalized;
            if (_sky != null)
                _sky.SetPositionAndRotation(from + flat * 760f + Vector3.up * 250f, Quaternion.LookRotation(flat));
            // Слабый холодный свет из-за спины зрителя — иначе всё против луны одни чёрные силуэты.
            var fill = new GameObject("Свет со спины").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.55f, 0.62f, 0.8f);
            fill.intensity = 0.22f;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.LookRotation(flat + Vector3.down * 0.45f);
        }

        internal static readonly string[] Pines = { "TallPineA", "TallPineB", "TallPineC", "Pine" };
        internal static readonly string[] Dead = { "DeadTreeA", "DeadTreeB", "DeadTreeC" };

        // ── места ──

        /// <summary>Соляной обоз: дорога в закат, две повозки с бочками, костёр охраны.</summary>
        internal static void SaltRoad()
        {
            Night(new Color(0.10f, 0.10f, 0.20f), new Color(0.52f, 0.36f, 0.30f), new Color(0.20f, 0.17f, 0.19f),
                  0.011f, new Vector3(-0.28f, 0.15f, 1f), new Color(0.75f, 0.70f, 0.80f), 0.6f, 30f);
            Ground("Ground048", new Color(0.85f, 0.75f, 0.62f));
            for (int i = 0; i < 26; i++)
            {
                float z = 4f + i * 4f, x = Mathf.Sin(i * 0.22f) * 4f;
                Box("Дорога", new Vector3(x, -0.47f, z), new Vector3(5.5f, 1f, 4.4f), Mat("Gravel043", new Color(0.95f, 0.85f, 0.7f), 2f), Mathf.Cos(i * 0.22f) * 12f);
            }
            Wagon(new Vector3(2.2f, 0f, 15f), 18f);
            Wagon(new Vector3(-1.8f, 0f, 27f), -12f);
            Prop("Tent", new Vector3(8.5f, 0f, 22f), -30f);
            Prop("Campfire", new Vector3(5.5f, 0f, 19f));
            Fire(new Vector3(5.5f, 0.6f, 19f), 1.3f);
            Prop("TorchStand", new Vector3(-4.2f, 0f, 19f));
            Fire(new Vector3(-4.2f, 2.1f, 19f), 0.6f);
            Trees(Dead, new Vector3(-12f, 0f, 10f), new Vector3(-16f, 0f, 60f), 7, 8f, 1.3f, 3);
            Trees(Dead, new Vector3(14f, 0f, 30f), new Vector3(22f, 0f, 70f), 5, 8f, 1.2f, 4);
            for (int i = 0; i < 18; i++) Prop("Rock", new Vector3((i % 2 == 0 ? -1 : 1) * (7f + i % 5 * 2.5f), 0f, 8f + i * 4.5f), i * 47f, 0.8f + i % 3 * 0.5f);
            Hill(new Vector3(-60f, -8f, 160f), new Vector3(140f, 40f, 60f), new Color(0.2f, 0.15f, 0.14f));
            Hill(new Vector3(70f, -10f, 200f), new Vector3(180f, 46f, 70f), new Color(0.2f, 0.15f, 0.14f));
            Shoot(new Vector3(-2.6f, 1.5f, 5f), new Vector3(2.5f, 2.2f, 22f), 30f);
        }

        internal static void Wagon(Vector3 at, float yaw)
        {
            var root = new GameObject("Повозка");
            var planks = Mat("Planks037A", new Color(0.7f, 0.55f, 0.45f), 1f);
            var body = Box("Кузов", Vector3.zero, new Vector3(2.2f, 0.7f, 4.2f), planks);
            body.transform.SetParent(root.transform);
            body.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            for (int i = 0; i < 4; i++)
            {
                var wheel = Box("Колесо", Vector3.zero, new Vector3(1.2f, 0.1f, 1.2f), Plain(new Color(0.12f, 0.09f, 0.07f)), 0f, 0f, PrimitiveType.Cylinder);
                wheel.transform.SetParent(root.transform);
                wheel.transform.localPosition = new Vector3(i < 2 ? -1.2f : 1.2f, 0.6f, i % 2 == 0 ? -1.4f : 1.4f);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            root.transform.position = at;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Prop("Barrel", root.transform.TransformPoint(new Vector3(-0.4f, 1.4f, -0.9f)), yaw);
            Prop("Barrel", root.transform.TransformPoint(new Vector3(0.45f, 1.4f, -0.6f)), yaw + 40f);
            Prop("Crate", root.transform.TransformPoint(new Vector3(0f, 1.4f, 0.9f)), yaw + 10f);
            // Тент над кузовом — полотно дугой.
            var canvas = Box("Тент", Vector3.zero, new Vector3(2.3f, 4.0f, 2.3f), Mat("Fabric061", new Color(0.85f, 0.78f, 0.65f), 1f), 0f, 0f, PrimitiveType.Cylinder);
            canvas.transform.SetParent(root.transform);
            canvas.transform.localPosition = new Vector3(0f, 1.5f, 0.3f);
            canvas.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            canvas.transform.localScale = new Vector3(2.3f, 1.6f, 1.6f);
        }

        /// <summary>Придорожная часовня: развалины под луной, могилы, сосны, огонёк у алтаря.</summary>
        internal static void Chapel()
        {
            Night(new Color(0.04f, 0.06f, 0.13f), new Color(0.22f, 0.27f, 0.36f), new Color(0.12f, 0.14f, 0.20f),
                  0.014f, new Vector3(0.32f, 0.2f, 1f), new Color(0.62f, 0.72f, 0.95f), 1.1f, 34f);
            Ground("Ground110", new Color(0.55f, 0.62f, 0.55f));
            var stone = Mat("Bricks076A", new Color(0.75f, 0.78f, 0.80f), 3f);
            // Задняя стена со щипцом — силуэт на луне.
            Box("Задняя стена", new Vector3(0f, 4f, 30f), new Vector3(12f, 8f, 1.2f), stone);
            var gable = Box("Щипец", new Vector3(0f, 8f, 30f), new Vector3(6f, 6f, 1.2f), stone);
            gable.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            Box("Окно", new Vector3(0f, 6.5f, 29.3f), new Vector3(1.4f, 3f, 0.2f), Plain(new Color(0.02f, 0.02f, 0.03f)));
            // Боковые стены — обломаны по-разному.
            for (int i = 0; i < 5; i++)
            {
                float h = 6.5f - i * 1.1f + (i % 2) * 0.7f;
                Box("Левая стена", new Vector3(-6f, h * 0.5f, 28f - i * 3.2f), new Vector3(1.2f, h, 3.2f), stone);
                float h2 = 5.5f - i * 1.3f;
                if (h2 > 0.5f) Box("Правая стена", new Vector3(6f, h2 * 0.5f, 28f - i * 3.2f), new Vector3(1.2f, h2, 3.2f), stone);
            }
            var pillar = Mat("Rock050", new Color(0.8f, 0.82f, 0.85f), 1f);
            Box("Колонна", new Vector3(-2.6f, 2.8f, 22f), new Vector3(0.9f, 2.8f, 0.9f), pillar, 0f, 0f, PrimitiveType.Cylinder);
            Box("Колонна", new Vector3(2.6f, 1.6f, 22f), new Vector3(0.9f, 1.6f, 0.9f), pillar, 0f, 0f, PrimitiveType.Cylinder);
            Box("Упавшая колонна", new Vector3(3.6f, 0.45f, 17f), new Vector3(0.9f, 2.6f, 0.9f), pillar, 70f, 90f, PrimitiveType.Cylinder);
            Prop("Altar", new Vector3(0f, 0f, 27f));
            Fire(new Vector3(0.9f, 1.4f, 26.6f), 0.7f);
            Fire(new Vector3(-0.9f, 1.4f, 26.6f), 0.5f);
            Prop("Coffin", new Vector3(-2.4f, 0f, 24.5f), 80f);
            // Тёплый свет внутри — стены видны изнутри, окно щипца светится.
            var inside = new GameObject("Свет в часовне").AddComponent<Light>();
            inside.type = LightType.Point; inside.color = new Color(1f, 0.6f, 0.3f);
            inside.range = 16f; inside.intensity = 5f; inside.shadows = LightShadows.Soft;
            inside.transform.position = new Vector3(0f, 2.5f, 25f);
            var grave = Mat("Rock022", new Color(0.7f, 0.72f, 0.74f), 1f);
            for (int i = 0; i < 10; i++)
            {
                float x = (i % 5 - 2f) * 3.6f + (i % 3) * 0.5f + (i % 5 == 2 ? 2.4f : 0f), z = 9f + (i / 5) * 4f + (i % 2) * 0.8f;
                float h = 0.7f + (i * 37 % 5) * 0.12f;
                Box("Надгробие", new Vector3(x, h * 0.5f, z), new Vector3(0.6f, h, 0.18f), grave, (i * 13) % 20 - 10f, (i * 7) % 18 - 9f);
            }
            Trees(Pines, new Vector3(-14f, 0f, 8f), new Vector3(-20f, 0f, 60f), 14, 10f, 1.15f, 11);
            Trees(Pines, new Vector3(14f, 0f, 8f), new Vector3(22f, 0f, 60f), 14, 10f, 1.15f, 12);
            Trees(Pines, new Vector3(-30f, 0f, 70f), new Vector3(30f, 0f, 80f), 18, 14f, 1.4f, 13);
            Hill(new Vector3(0f, -20f, 220f), new Vector3(420f, 60f, 80f), new Color(0.08f, 0.10f, 0.12f));
            Shoot(new Vector3(-1.5f, 1.8f, 3f), new Vector3(0f, 4.2f, 28f), 32f);
        }

        /// <summary>Затопленная каменоломня: отвесные стены, чёрная вода, всплывший гроб, туман.</summary>
        internal static void Quarry()
        {
            Night(new Color(0.05f, 0.08f, 0.11f), new Color(0.24f, 0.31f, 0.33f), new Color(0.12f, 0.15f, 0.17f),
                  0.028f, new Vector3(0.45f, 0.3f, 1f), new Color(0.6f, 0.75f, 0.85f), 0.8f, 28f);
            Ground("Rock050", new Color(0.45f, 0.48f, 0.5f), 30f);
            var cliff = Mat("Rock050", new Color(0.62f, 0.64f, 0.66f), 4f);
            for (int i = 0; i < 9; i++)
            {
                Box("Стена карьера", new Vector3(-16f - i % 2 * 2f, 7f + i % 3, 6f + i * 7f), new Vector3(8f, 16f + i % 3 * 3f, 7.5f), cliff, i * 9f);
                Box("Стена карьера", new Vector3(17f + i % 2 * 2f, 6f + i % 2, 6f + i * 7f), new Vector3(8f, 14f + i % 4 * 2f, 7.5f), cliff, -i * 7f);
            }
            Box("Дальняя стена", new Vector3(0f, 9f, 66f), new Vector3(46f, 22f, 6f), cliff);
            Box("Вода", new Vector3(0f, 0.2f, 34f), new Vector3(34f, 0.1f, 64f), Water());
            Prop("Coffin", new Vector3(2.5f, 0.15f, 18f), 30f, 1f, 8f);
            Prop("Coffin", new Vector3(-4f, 0.05f, 30f), -50f, 1f, -12f);
            for (int i = 0; i < 9; i++) Prop("Rock", new Vector3((i % 3 - 1) * 9f + i, 0f, 12f + i * 5f), i * 33f, 1.4f + i % 3 * 0.6f);
            Prop("DeadTreeB", new Vector3(-11f, 6f, 20f), 40f, 1.2f, -14f);
            Prop("TorchStand", new Vector3(-9f, 0.3f, 14f));
            Fire(new Vector3(-9f, 2.4f, 14f), 0.5f);
            Fire(new Vector3(3f, 0.9f, 18.5f), 0.4f, new Color(0.5f, 0.9f, 1f));
            Shoot(new Vector3(-3f, 3.4f, 1f), new Vector3(1f, 2.5f, 34f), 32f);
        }

        /// <summary>Сожжённая застава: обугленный частокол, рухнувшая вышка, угли и зарево.</summary>
        internal static void Outpost()
        {
            Night(new Color(0.07f, 0.05f, 0.08f), new Color(0.46f, 0.20f, 0.13f), new Color(0.18f, 0.12f, 0.12f),
                  0.02f, new Vector3(0.6f, 0.25f, 1f), new Color(0.85f, 0.6f, 0.5f), 0.45f, 24f);
            Ground("Ground077", new Color(0.5f, 0.42f, 0.38f));
            for (int i = 0; i < 16; i++)
            {
                if (i == 6 || i == 7) continue;   // пролом
                float x = -22f + i * 3f;
                Prop("Palisade", new Vector3(x, 0f, 24f + Mathf.Sin(i) * 0.6f), 0f, 1f, (i % 4 == 1) ? 14f : (i % 5 == 3 ? -18f : 0f));
            }
            var charred = Mat("Planks037A", new Color(0.18f, 0.14f, 0.12f), 1f);
            // Вышка — две ноги стоят, площадка рухнула набок.
            Box("Нога вышки", new Vector3(6f, 4f, 26f), new Vector3(0.5f, 8f, 0.5f), charred);
            Box("Нога вышки", new Vector3(9f, 3f, 26f), new Vector3(0.5f, 6f, 0.5f), charred, 0f, 12f);
            Box("Площадка", new Vector3(9.5f, 1.2f, 22f), new Vector3(4f, 0.4f, 4f), charred, 25f, 38f);
            Prop("Banner", new Vector3(-4f, 0f, 18f), 20f, 1f, 16f);
            for (int i = 0; i < 6; i++)
            {
                var p = new Vector3(-9f + i * 3.6f, 0f, 14f + (i % 3) * 4f);
                Prop("Campfire", p, i * 40f, 0.9f);
                Fire(p + Vector3.up * 0.4f, 0.7f + (i % 2) * 0.4f, new Color(1f, 0.38f, 0.1f));
            }
            Prop("Barrel", new Vector3(3f, 0f, 9f), 0f, 1f, 90f);
            Prop("Crate", new Vector3(-2f, 0f, 8f), 30f);
            Trees(Dead, new Vector3(-28f, 0f, 30f), new Vector3(28f, 0f, 44f), 12, 8f, 1.3f, 21);
            Hill(new Vector3(0f, -14f, 160f), new Vector3(400f, 40f, 60f), new Color(0.15f, 0.08f, 0.07f));
            Shoot(new Vector3(-2f, 1.8f, 5f), new Vector3(1f, 2.8f, 24f), 30f);
        }

        /// <summary>Старый гарнизон: каменная стена с башнями, ворота, факелы, знамёна, луна над башней.</summary>
        internal static void Garrison()
        {
            Night(new Color(0.04f, 0.05f, 0.11f), new Color(0.26f, 0.26f, 0.36f), new Color(0.12f, 0.12f, 0.18f),
                  0.016f, new Vector3(-0.32f, 0.38f, 1f), new Color(0.62f, 0.68f, 0.92f), 0.85f, 30f);
            Ground("Ground048", new Color(0.55f, 0.55f, 0.6f));
            var stone = Mat("Bricks076A", new Color(0.7f, 0.7f, 0.75f), 4f);
            Box("Стена", new Vector3(-14f, 5f, 40f), new Vector3(20f, 10f, 3f), stone);
            Box("Стена", new Vector3(14f, 5f, 40f), new Vector3(20f, 10f, 3f), stone);
            Box("Над воротами", new Vector3(0f, 8.5f, 40f), new Vector3(8f, 3f, 3f), stone);
            for (int i = -14; i <= 14; i++)
                if (i % 2 == 0) Box("Зубец", new Vector3(i * 1.6f, 10.6f, 40f), new Vector3(0.9f, 1.2f, 3f), stone);
            for (int s = -1; s <= 1; s += 2)
            {
                Box("Башня", new Vector3(s * 26f, 8f, 41f), new Vector3(7f, 16f, 7f), stone, 0f, 0f, PrimitiveType.Cylinder);
                Box("Башня у ворот", new Vector3(s * 5.5f, 7f, 39f), new Vector3(5f, 14f, 5f), stone);
                Prop("Banner", new Vector3(s * 5.5f, 14f, 36.4f), 0f, 1.3f);
                Prop("TorchStand", new Vector3(s * 3.2f, 0f, 37f));
                Fire(new Vector3(s * 3.2f, 2.1f, 37f), 1f);
            }
            Prop("CryptGate", new Vector3(0f, 0f, 39.5f), 0f, 1.3f);
            Box("Тьма за воротами", new Vector3(0f, 3f, 41.6f), new Vector3(6f, 7f, 0.2f), Plain(new Color(0.01f, 0.01f, 0.015f)));
            for (int i = 0; i < 12; i++)
                Box("Дорога", new Vector3(Mathf.Sin(i * 0.3f) * 0.8f, -0.47f, 2f + i * 3f), new Vector3(5f, 1f, 3.2f), Mat("PavingStones127", new Color(0.7f, 0.7f, 0.72f), 1.5f));
            Trees(Pines, new Vector3(-40f, 0f, 30f), new Vector3(-30f, 0f, 70f), 10, 10f, 1.3f, 31);
            Trees(Pines, new Vector3(40f, 0f, 30f), new Vector3(30f, 0f, 70f), 10, 10f, 1.3f, 32);
            Hill(new Vector3(0f, -12f, 200f), new Vector3(500f, 70f, 80f), new Color(0.08f, 0.08f, 0.12f));
            // Свет факелов на стене и в воротах.
            var gate = new GameObject("Свет у ворот").AddComponent<Light>();
            gate.type = LightType.Point; gate.color = new Color(1f, 0.55f, 0.25f);
            gate.range = 30f; gate.intensity = 12f; gate.shadows = LightShadows.Soft;
            gate.transform.position = new Vector3(0f, 4f, 33f);
            for (int s = -1; s <= 1; s += 2)
            {
                var wall = new GameObject("Свет на стене").AddComponent<Light>();
                wall.type = LightType.Point; wall.color = new Color(1f, 0.5f, 0.22f);
                wall.range = 18f; wall.intensity = 6f;
                wall.transform.position = new Vector3(s * 16f, 6f, 36f);
                Prop("TorchStand", new Vector3(s * 16f, 0f, 38f));
                Fire(new Vector3(s * 16f, 2.1f, 38f), 0.8f);
            }
            Shoot(new Vector3(-3f, 1.4f, 8f), new Vector3(0f, 7.5f, 40f), 34f);
        }

        /// <summary>Костяная топь: мёртвые деревья в воде, кости, болотные огни, густой туман.</summary>
        internal static void Marsh()
        {
            Night(new Color(0.04f, 0.07f, 0.07f), new Color(0.24f, 0.32f, 0.27f), new Color(0.12f, 0.16f, 0.14f),
                  0.045f, new Vector3(0.2f, 0.2f, 1f), new Color(0.6f, 0.8f, 0.7f), 0.55f, 26f);
            Ground("Ground110", new Color(0.35f, 0.40f, 0.30f));
            var water = Water();
            for (int i = 0; i < 8; i++)
                Box("Заводь", new Vector3((i % 3 - 1) * 8f + i, -0.46f, 6f + i * 6f), new Vector3(9f + i % 3 * 3f, 0.5f, 6f),
                    water, i * 23f, 0f, PrimitiveType.Cylinder);
            Trees(Dead, new Vector3(-16f, 0f, 6f), new Vector3(16f, 0f, 50f), 22, 18f, 1.25f, 41);
            for (int i = 0; i < 26; i++)
                Prop("GrassTuft", new Vector3((i * 37 % 24) - 12f, 0f, 3f + i * 1.8f), i * 31f, 1.6f);
            var bone = Plain(new Color(0.82f, 0.80f, 0.72f));
            var rnd = new System.Random(7);
            for (int i = 0; i < 40; i++)
            {
                var p = new Vector3((float)rnd.NextDouble() * 16f - 8f, 0.05f, 3f + (float)rnd.NextDouble() * 20f);
                if (i % 5 == 0) Box("Череп", p + Vector3.up * 0.12f, Vector3.one * 0.28f, bone, 0f, 0f, PrimitiveType.Sphere);
                else Box("Кость", p, new Vector3(0.07f, 0.35f, 0.07f), bone, (float)rnd.NextDouble() * 180f, 90f, PrimitiveType.Cylinder);
            }
            for (int i = 0; i < 6; i++)
                Fire(new Vector3(-9f + i * 3.7f, 1.2f + (i % 2) * 0.6f, 10f + (i * 7 % 5) * 4f), 0.45f, new Color(0.45f, 1f, 0.85f));
            Shoot(new Vector3(0f, 1.5f, 0f), new Vector3(0f, 2.2f, 24f), 32f);
        }
    }
}
