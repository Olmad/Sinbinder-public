// Assets/Scripts/Editor/SteamCover.cs
// Обложка для страницы Steam (автор, 23.11: «собрать полноценную красивую картину для обложки на
// странице Steam» и «сохрани отдельную сцену с этой собранной картиной»).
//
// Сцена Assets/Scenes/SteamCover.unity — ночь после разгрома: Греховод в багровом за столом
// военного совета, перед ним шар с рельефной картой и огнями отрядов (тот же, что в игре,
// UI.ReliefMap), по бокам банки с душами и факелы, за ним в тумане его отряд — скелеты, зомби,
// голем, призрак; вдали горит лагерь, над лесом луна. Всё из моделей игры; созданные кодом
// материалы, текстуры и сетки лежат рядом в Assets/Art/SteamCover/SteamCover.asset, чтобы
// сцена открывалась такой же.
//
// Верстак (вне игры):
//   call Sinbinder.EditorTools.SteamCover.Build  — собрать сцену заново, сохранить, снять кадры;
//   call Sinbinder.EditorTools.SteamCover.Shoot  — снять кадры с сохранённой сцены (после правок
//                                                   руками в редакторе).
// Кадры без логотипа — Logs/workbench/shots/steam-*.png; логотип кладёт Tools/steam/compose.py.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Sinbinder.Core;
using Sinbinder.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using static Sinbinder.EditorTools.LocationShots;
using Object = UnityEngine.Object;

namespace Sinbinder.EditorTools
{
    public static class SteamCover
    {
        public const string ScenePath = "Assets/Scenes/SteamCover.unity";
        private const string AssetFolder = "Assets/Art/SteamCover";
        private const string AssetPath = AssetFolder + "/SteamCover.asset";
        private const string WideCamera = "Камера обложки";
        private const string TallCamera = "Камера обложки — вертикаль";

        /// <summary>Размеры Steam: главная капсула, шапка, малая, фон библиотеки; вертикальная — своей камерой.</summary>
        private static readonly (string name, int w, int h, bool tall)[] Sizes =
        {
            ("main_1232x706", 1232, 706, false), ("header_920x430", 920, 430, false),
            ("small_462x174", 462, 174, false), ("hero_3840x1240", 3840, 1240, false),
            ("library_600x900", 600, 900, true),
        };

        private static readonly List<string> Notes = new();

        public static string Build()
        {
            if (EditorApplication.isPlaying) return "нужно вне игры (stop): сцена заменяется";
            Notes.Clear();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _lit = null; _sky = null; Flames.Clear();

            // Ночь после разгрома: небо тёмно-лиловое, у земли — зарево горящего лагеря.
            Night(new Color(0.035f, 0.03f, 0.075f), new Color(0.30f, 0.12f, 0.09f), new Color(0.11f, 0.09f, 0.13f),
                  0.040f, new Vector3(-0.42f, 0.30f, 1f), new Color(0.62f, 0.70f, 0.98f), 0.55f, 34f);
            Ground("Ground077", new Color(0.42f, 0.36f, 0.34f), 60f);

            // Стол совета, шар, банки душ.
            var table = Prop("CouncilTable", new Vector3(0f, 0f, 0f), 0f, 1f);
            float top = table != null ? Top(table) : 0.9f;
            const float ball = 0.46f;
            Box("Подставка шара", new Vector3(0f, top + 0.03f, 0f), new Vector3(0.2f, 0.03f, 0.2f),
                Plain(new Color(0.08f, 0.07f, 0.07f)), 0f, 0f, PrimitiveType.Cylinder);
            Ball(new Vector3(0f, top + 0.06f + ball * 0.5f, 0f), ball);
            Jar(new Vector3(-0.62f, top, 0.05f));
            Jar(new Vector3(0.66f, top, -0.08f));
            Prop("Torch", new Vector3(0.38f, top, 0.28f), 30f, 0.8f, 82f);

            // Греховод — за столом, лицом к зрителю.
            var sin = new GameObject("Греховод");
            sin.transform.SetPositionAndRotation(new Vector3(0f, 0f, 0.82f), Quaternion.Euler(0f, 180f, 0f));
            var body = typeof(SinbinderPlayer).GetMethod("BuildBody", BindingFlags.NonPublic | BindingFlags.Static);
            if (body != null) body.Invoke(null, new object[] { sin.transform });
            else Notes.Add("нет SinbinderPlayer.BuildBody");
            Pose(sin, 0.35f, "Idle", "Stand");

            // Отряд за спиной — в тумане, глаза горят грехом.
            Warrior(ShellType.Skeleton, new Vector3(-1.55f, 0f, 2.5f), 196f, 1.78f, 0.5f, new Color(0.9f, 0.25f, 0.2f), 0.1f, "Sword", "Shield", "PauldronLeft");
            Warrior(ShellType.Zombie, new Vector3(1.75f, 0f, 2.8f), 165f, 1.72f, 0.58f, new Color(0.35f, 0.9f, 0.35f), 0.55f, "Axe", "Tabard");
            Warrior(ShellType.Golem, new Vector3(-3.3f, 0f, 4.6f), 200f, 2.35f, 0.75f, new Color(1f, 0.6f, 0.15f), 0.8f, "Club");
            Warrior(ShellType.Ghost, new Vector3(3.2f, 0f, 4.9f), 160f, 1.8f, 0.5f, new Color(0.45f, 0.75f, 1f), 0.25f, "Cloak");
            Warrior(ShellType.Skeleton, new Vector3(0.9f, 0f, 6.2f), 175f, 1.7f, 0.48f, new Color(0.8f, 0.4f, 1f), 0.7f, "Bow", "Quiver", "Hood");
            Warrior(ShellType.Zombie, new Vector3(-0.9f, 0f, 7.4f), 188f, 1.75f, 0.55f, new Color(0.9f, 0.25f, 0.2f), 0.4f, "Club");

            // Факелы по бокам стола — тёплый контровой свет на багрец.
            foreach (var x in new[] { -1.45f, 1.55f })
            {
                Prop("TorchStand", new Vector3(x, 0f, 0.55f));
                Fire(new Vector3(x, 2.1f, 0.55f), 0.9f);
            }

            // Вдали горит лагерь: палатки, огонь, частокол; лес по сторонам; кости у стола.
            for (int i = 0; i < 6; i++)
            {
                var p = new Vector3(-14f + i * 5.6f + (i % 2) * 1.5f, 0f, 20f + (i % 3) * 5f);
                Prop("Tent", p, i * 47f, 1.3f, i % 2 == 0 ? 8f : -6f);
                Fire(p + Vector3.up * 1.2f, 2.2f + (i % 2), new Color(1f, 0.42f, 0.12f));
            }
            for (int i = 0; i < 12; i++)
                Prop("Palisade", new Vector3(-20f + i * 3.4f, 0f, 15f + Mathf.Sin(i * 1.7f)), 0f, 1f, i % 3 == 1 ? 18f : 0f);
            Prop("Banner", new Vector3(-6f, 0f, 13f), 10f, 1.4f, 12f);
            Trees(Dead, new Vector3(-9f, 0f, 6f), new Vector3(-16f, 0f, 30f), 9, 7f, 1.3f, 51);
            Trees(Dead, new Vector3(9f, 0f, 6f), new Vector3(16f, 0f, 30f), 9, 7f, 1.3f, 52);
            Trees(Pines, new Vector3(-40f, 0f, 45f), new Vector3(40f, 0f, 55f), 26, 14f, 1.6f, 53);
            Hill(new Vector3(0f, -16f, 200f), new Vector3(520f, 60f, 80f), new Color(0.07f, 0.05f, 0.07f));
            for (int i = 0; i < 10; i++)
                Prop(i % 3 == 0 ? "Rock" : "GrassTuft", new Vector3(-3.5f + i * 0.8f, 0f, -0.6f + (i % 4) * 0.9f), i * 61f, 0.6f + (i % 3) * 0.3f);
            var bone = Plain(new Color(0.78f, 0.75f, 0.68f));
            var rnd = new System.Random(5);
            for (int i = 0; i < 14; i++)
            {
                var p = new Vector3((float)rnd.NextDouble() * 4f - 2f, 0.04f, (float)rnd.NextDouble() * 1.6f - 1.2f);
                if (i % 6 == 0) Box("Череп", p + Vector3.up * 0.08f, Vector3.one * 0.2f, bone, 0f, 0f, PrimitiveType.Sphere);
                else Box("Кость", p, new Vector3(0.05f, 0.25f, 0.05f), bone, (float)rnd.NextDouble() * 180f, 90f, PrimitiveType.Cylinder);
            }

            // Камеры: широкая (Греховод левее, справа место логотипу) и вертикальная (он по центру).
            var wideFrom = new Vector3(0.35f, 1.0f, -2.75f);
            var wideTo = new Vector3(1.0f, 1.32f, 1.4f);
            Stage(wideFrom, wideTo);
            var wide = Cam(wideFrom, wideTo, 40f);
            wide.name = WideCamera;
            var tall = Cam(new Vector3(0f, 1.05f, -3.3f), new Vector3(0f, 1.5f, 1f), 44f);
            tall.name = TallCamera;
            tall.enabled = false;

            Persist();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            Notes.Add("сцена: " + ScenePath);
            Notes.Add(Shoot());
            return string.Join("\n", Notes);
        }

        public static string Shoot()
        {
            if (EditorApplication.isPlaying) return "нужно вне игры (stop)";
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Camera wide = null, tall = null;
            foreach (var c in cams)
            {
                if (c.name == WideCamera) wide = c;
                if (c.name == TallCamera) tall = c;
            }
            if (wide == null) return "в сцене нет камеры «" + WideCamera + "»";
            Directory.CreateDirectory(CleanShot.Folder);
            var done = new List<string>();
            foreach (var (name, w, h, isTall) in Sizes)
            {
                var cam = isTall && tall != null ? tall : wide;
                var tex = Render(cam, w, h);
                var file = Path.GetFullPath($"{CleanShot.Folder}/steam-{name}.png");
                File.WriteAllBytes(file, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                done.Add(file);
            }
            return string.Join("\n", done);
        }

        // ── части ──

        private static float Top(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return 0.9f;
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b.max.y;
        }

        /// <summary>Шар совета: стекло, рельефная карта внутри, огни отрядов над местами, свет изнутри.</summary>
        private static void Ball(Vector3 centre, float diameter)
        {
            var root = new GameObject("Шар").transform;
            root.position = centre;
            root.localScale = Vector3.one * diameter;

            var relief = new GameObject("Рельеф в шаре");
            relief.transform.SetParent(root, false);
            relief.transform.localPosition = new Vector3(0f, -0.19f, 0f);
            relief.AddComponent<MeshFilter>().sharedMesh = UI.ReliefMap.ReliefMesh(64, 0.36f, 0.20f);
            var rr = relief.AddComponent<MeshRenderer>();
            rr.sharedMaterial = new Material(Shader.Find("Sprites/Default"))
            {
                name = "Рельеф", mainTexture = Readable(UI.ReliefMap.MapTexture()),
                renderQueue = 2999, color = new Color(1.3f, 1.25f, 1.15f, 1f),
            };
            rr.shadowCastingMode = ShadowCastingMode.Off;

            var glass = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glass.name = "Стекло";
            Object.DestroyImmediate(glass.GetComponent<Collider>());
            glass.transform.SetParent(root, false);
            var gr = glass.GetComponent<Renderer>();
            gr.sharedMaterial = Glass();
            gr.shadowCastingMode = ShadowCastingMode.Off;

            var light = new GameObject("Свет шара").AddComponent<Light>();
            light.transform.position = centre + Vector3.up * diameter * 0.1f;
            light.type = LightType.Point;
            light.color = new Color(0.62f, 0.82f, 1f);
            light.range = 4.5f;
            light.intensity = 6f;
            light.shadows = LightShadows.Soft;

            var halo = Quad("Сияние шара", centre, Vector3.one * diameter * 3.4f, Soft(), new Color(0.55f, 0.78f, 1f, 0.32f));
            Flames.Add(halo.transform);

            // Огни отрядов — над местами, как в игре (CrystalBall.Above).
            foreach (var place in new[] { "Придорожная часовня", "Затопленная каменоломня", "Сожжённая застава", "Старый гарнизон" })
            {
                var uv = UI.ReliefMap.MapPos(place);
                var d = uv - new Vector2(0.5f, 0.5f);
                float h = (UI.ReliefMap.HeightAt(uv) + 0.05f) * 0.20f;
                var at = relief.transform.TransformPoint(new Vector3(d.x * 2f * 0.36f, h + 0.07f, d.y * 2f * 0.36f));
                var mote = Quad("Огонь отряда", at, Vector3.one * diameter * 0.09f, Soft(), new Color(3.2f, 2.6f, 1.7f, 1f));
                Flames.Add(mote.transform);
            }
        }

        private static Material Glass()
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Стекло шара" };
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            m.SetColor("_BaseColor", new Color(0.55f, 0.72f, 0.9f, 0.12f));
            m.SetFloat("_Smoothness", 0.97f);
            m.SetFloat("_Metallic", 0f);
            return m;
        }

        /// <summary>Банка с душой на столе — голубой огонёк, как на логотипе.</summary>
        private static void Jar(Vector3 at)
        {
            Prop("SoulJarFull", at, UnityEngine.Random.Range(0f, 360f), 1f);
            var light = new GameObject("Свет души").AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.45f, 0.8f, 1f);
            light.range = 1.8f;
            light.intensity = 2.5f;
            light.transform.position = at + Vector3.up * 0.2f;
            var glow = Quad("Огонёк души", at + Vector3.up * 0.16f, Vector3.one * 0.32f, Soft(), new Color(0.9f, 1.8f, 2.6f, 0.9f));
            Flames.Add(glow.transform);
        }

        private static void Warrior(ShellType shell, Vector3 at, float yaw, float height, float girth, Color eyes,
                                    float phase, params string[] wear)
        {
            var go = new GameObject(shell.ToString());
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            var model = WarriorLook.Build(go, shell, height, girth, 0.9f);
            if (model == null) { Notes.Add("нет тела " + shell); return; }
            foreach (var item in wear) Wardrobe.Wear(model.gameObject, item);
            Wardrobe.Tint(model.gameObject, "Eye", eyes, glow: 3f);
            Pose(go, phase, "Idle", "Stand");
        }

        /// <summary>Поза из клипа тела: без неё модель стоит в позе привязки, руки в стороны.</summary>
        private static void Pose(GameObject root, float phase, params string[] wanted)
        {
            var animator = root.GetComponentInChildren<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null) { Notes.Add(root.name + ": нет аниматора"); return; }
            var clips = animator.runtimeAnimatorController.animationClips;
            AnimationClip pick = null;
            foreach (var w in wanted)
                foreach (var c in clips)
                    if (pick == null && c != null && c.name.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) pick = c;
            if (pick == null && clips.Length > 0) pick = clips[0];
            if (pick == null) return;
            var names = new List<string>();
            foreach (var c in clips) if (c != null) names.Add(c.name);
            Notes.Add($"{root.name}: поза {pick.name} (есть: {string.Join(", ", names)})");

            var graph = PlayableGraph.Create("Поза обложки");
            var output = AnimationPlayableOutput.Create(graph, "поза", animator);
            var play = AnimationClipPlayable.Create(graph, pick);
            output.SetSourcePlayable(play);
            play.SetTime(phase * pick.length);
            graph.Evaluate(0f);
            graph.Destroy();
        }

        /// <summary>Текстура, которую можно сохранить: нечитаемую (карта шара) переснимаем через GPU.</summary>
        private static Texture2D Readable(Texture src)
        {
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var t = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false) { name = src.name, wrapMode = TextureWrapMode.Clamp };
            t.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            t.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return t;
        }

        // ── сохранить созданное кодом рядом со сценой ──

        private static readonly Dictionary<Object, Object> Kept = new();

        /// <summary>
        /// Материалы, текстуры, сетки и профиль постобработки, созданные кодом, сцена сама не хранит —
        /// открылась бы розовой. Копии ложатся в один файл SteamCover.asset; кэши игры (карта шара,
        /// материалы гардероба) не трогаем — сцена ссылается на копии.
        /// </summary>
        private static void Persist()
        {
            Kept.Clear();
            if (!AssetDatabase.IsValidFolder("Assets/Art")) AssetDatabase.CreateFolder("Assets", "Art");
            if (!AssetDatabase.IsValidFolder(AssetFolder)) AssetDatabase.CreateFolder("Assets/Art", "SteamCover");
            AssetDatabase.DeleteAsset(AssetPath);
            var volume = Object.FindFirstObjectByType<Volume>();
            var profile = volume != null ? volume.sharedProfile : null;
            if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); }
            profile.name = "Постобработка обложки";
            AssetDatabase.CreateAsset(profile, AssetPath);
            foreach (var c in profile.components) { c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy; AssetDatabase.AddObjectToAsset(c, AssetPath); }

            if (RenderSettings.customReflectionTexture != null)
                RenderSettings.customReflectionTexture = (Texture)Keep(RenderSettings.customReflectionTexture);

            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = (Material)Keep(mats[i]);
                r.sharedMaterials = mats;
                if (r is SkinnedMeshRenderer skin) skin.sharedMesh = (Mesh)Keep(skin.sharedMesh);
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null) mf.sharedMesh = (Mesh)Keep(mf.sharedMesh);
            }
            AssetDatabase.SaveAssets();
            Notes.Add($"сохранено рядом: {Kept.Count} шт. в {AssetPath}");
        }

        private static Object Keep(Object o)
        {
            if (o == null || EditorUtility.IsPersistent(o)) return o;
            if (Kept.TryGetValue(o, out var copy)) return copy;
            copy = o is Texture2D t2 && !t2.isReadable ? Readable(t2) : Object.Instantiate(o);
            copy.name = string.IsNullOrEmpty(o.name) ? o.GetType().Name : o.name.Replace("(Clone)", "").Trim();
            Kept[o] = copy;
            if (copy is Material m)
                foreach (var id in m.GetTexturePropertyNameIDs())
                {
                    var tex = m.GetTexture(id);
                    if (tex != null && !EditorUtility.IsPersistent(tex)) m.SetTexture(id, (Texture)Keep(tex));
                }
            AssetDatabase.AddObjectToAsset(copy, AssetPath);
            return copy;
        }
    }
}
