// Assets/Scripts/Editor/SakuraSceneBuilder.cs
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sinbinder.Utilets
{
    /// <summary>
    /// Отдельная сцена: два скелета с катанами, река, островок с сакурой.
    ///
    /// Просьба автора от 14 сентября, дословно. С прологом не связана
    /// ничем: ни ведущего, ни спавнеров, ни интерфейса — поэтому её
    /// нельзя сломать, сломав демо, и наоборот.
    ///
    /// Зачем она есть, кроме просьбы: это <b>витрина</b>. Всё, что
    /// собрано за неделю, стоит в одном кадре — тела, гардероб, предметы,
    /// материалы, взгляд, движения. Одна такая картинка объясняет проект
    /// быстрее, чем двенадцать минут прохождения.
    ///
    /// Собирается пунктом меню, а не руками: сцена, собранная руками,
    /// живёт до первой пересборки и не воспроизводится.
    /// </summary>
    public static class SakuraSceneBuilder
    {
        private const string Path = "Assets/Scenes/Сакура.unity";

        [MenuItem("Sinbinder/Собрать сцену с сакурой")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                                                    NewSceneMode.Single);

            Sky();
            Water();
            Island();
            Duel();
            Frame();

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, Path);
            AssetDatabase.SaveAssets();

            Debug.Log("[САКУРА] Сцена собрана: " + Path);
        }

        /// <summary>Точка входа для пакетного режима.</summary>
        public static void BuildBatch() => Build();

        // ───────────────────────────── мир ─────────────────────────────

        private static void Sky()
        {
            // Вечер, а не ночь: лепестки и вода должны читаться цветом,
            // а в темноте пролога от них осталась бы одна серая рябь.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.30f, 0.33f, 0.42f);
            RenderSettings.ambientEquatorColor = new Color(0.22f, 0.21f, 0.24f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.11f, 0.11f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.022f;
            RenderSettings.fogColor = new Color(0.20f, 0.22f, 0.28f);

            var sun = new GameObject("Закат");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.74f, 0.55f);
            light.intensity = 1.35f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(24f, 38f, 0f);

            var look = EffectsBuilder.Look();
            if (look == null) return;

            var go = new GameObject("Взгляд");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = look;
        }

        /// <summary>
        /// Река: берег, на котором стоят, и вода за ним. Вода — плоскость
        /// с гладким тёмным материалом: отражать ей нечего, а блик
        /// от заката делает её водой убедительнее любой ряби.
        /// </summary>
        private static void Water()
        {
            var bank = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bank.name = "Берег";
            bank.transform.position = new Vector3(0f, -0.25f, -6f);
            bank.transform.localScale = new Vector3(60f, 0.5f, 14f);
            Wear(bank, MaterialBuilder.Get("Ground048"));

            var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "Река";
            water.transform.position = new Vector3(0f, -0.18f, 14f);
            water.transform.localScale = new Vector3(8f, 1f, 4f);
            Wear(water, Paint("Вода", new Color(0.075f, 0.115f, 0.145f), 0.92f));

            // Дальний берег: без него река уходит в туман обрывом.
            var far = GameObject.CreatePrimitive(PrimitiveType.Cube);
            far.name = "Дальний берег";
            far.transform.position = new Vector3(0f, -0.25f, 34f);
            far.transform.localScale = new Vector3(60f, 0.5f, 16f);
            Wear(far, MaterialBuilder.Get("Ground110"));

            for (int i = 0; i < 6; i++)
            {
                float x = -18f + i * 7.5f;
                Prop("Rock", new Vector3(x, 0f, -1.2f + (i % 2) * 0.6f), i * 47f, 0.7f);
            }
        }

        /// <summary>Островок с сакурой — то, ради чего кадр и строится.</summary>
        private static void Island()
        {
            var isle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            isle.name = "Островок";
            isle.transform.position = new Vector3(1.6f, -0.12f, 12f);
            isle.transform.localScale = new Vector3(6.4f, 0.22f, 6.4f);
            Wear(isle, MaterialBuilder.Get("Ground110"));

            Prop("Sakura", new Vector3(1.2f, 0.05f, 12.4f), 18f, 1.35f);
            Prop("Rock", new Vector3(3.6f, 0f, 10.4f), 120f, 0.5f);
            Prop("Rock", new Vector3(-0.9f, 0f, 13.6f), 260f, 0.4f);

            Petals(new Vector3(1.2f, 3.6f, 12.4f));
        }

        /// <summary>
        /// Лепестки: те же частицы, что искры у костра, только медленные,
        /// падающие и розовые. Материал один — значит и вид один.
        /// </summary>
        private static void Petals(Vector3 at)
        {
            var material = EffectsBuilder.SparkOf();
            if (material == null) return;

            var go = new GameObject("Лепестки");
            go.transform.position = at;

            var particles = go.AddComponent<ParticleSystem>();

            var main = particles.main;
            main.loop = true;
            main.startLifetime = 9f;
            main.startSpeed = 0.35f;
            main.startSize = 0.09f;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.86f, 0.62f, 0.68f), new Color(0.72f, 0.45f, 0.55f));
            main.gravityModifier = 0.035f;
            main.maxParticles = 220;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = particles.emission;
            emission.rateOverTime = 16f;

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1.9f;

            // Сносит ветром к воде: лепесток, падающий отвесно, читается
            // снегом, а не лепестком.
            var wind = particles.velocityOverLifetime;
            wind.enabled = true;
            wind.space = ParticleSystemSimulationSpace.World;
            wind.x = new ParticleSystem.MinMaxCurve(-0.35f, -0.08f);
            wind.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.05f);

            var spin = particles.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-90f, 90f);

            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f),
                        new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // ──────────────────────────── поединок ────────────────────────────

        /// <summary>
        /// Двое друг напротив друга, катаны в правых руках. Стоят
        /// в покое: это не бой, а миг перед ним, и он читается лучше
        /// любого замаха.
        /// </summary>
        private static void Duel()
        {
            Fighter("Скелет слева", new Vector3(-2.4f, 0f, 0.4f), 78f, 1.25f);
            Fighter("Скелет справа", new Vector3(2.4f, 0f, -0.2f), -104f, 1.32f);
        }

        private static void Fighter(string name, Vector3 at, float yaw, float height)
        {
            var body = Resources.Load<GameObject>("Bodies/Skeleton");
            if (body == null)
            {
                Debug.LogWarning("[САКУРА] Модели скелета нет: соберите "
                               + "Tools/blender/bodies.py.");
                return;
            }

            var go = (GameObject)Object.Instantiate(body);
            go.name = name;
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = go.transform.localScale * height;

            var animator = go.GetComponentInChildren<Animator>();
            var controller = Resources.Load<RuntimeAnimatorController>("Bodies/SkeletonMotion");
            if (animator != null && controller != null) animator.runtimeAnimatorController = controller;

            Katana(go);
        }

        /// <summary>
        /// Катана в правой руке.
        ///
        /// Гардероб сажает вещи матрицей привязки кости, и для него это
        /// верно: его части собраны <b>в координатах тела</b> — капюшон
        /// уже стоит там, где голова. Катана собрана отдельно, у начала
        /// координат, вдоль своей оси; та же матрица уносила её на
        /// четыре с половиной метра от ладони. Проверка кадра это
        /// и показала — иначе выяснилось бы на показе.
        ///
        /// Поэтому поворот задаём <b>в осях тела</b>, а не кости: оси
        /// костей заданы ригом и меняются вместе с ним, а «остриём
        /// вперёд-вверх» — это про бойца, а не про кость.
        /// </summary>
        private static void Katana(GameObject body)
        {
            var blade = Resources.Load<GameObject>("Props/Katana");
            var skin = body.GetComponentInChildren<SkinnedMeshRenderer>();
            if (blade == null || skin == null) return;

            Transform hand = null;
            if (skin.bones != null)
                foreach (var bone in skin.bones)
                    if (bone != null && bone.name == "RightHand") { hand = bone; break; }

            if (hand == null) return;

            var go = (GameObject)Object.Instantiate(blade, hand);
            go.name = "Катана";

            // Клинок остаётся клинком, как бы ни был растянут скелет:
            // рост меняют бойцу, а не оружию.
            var scale = hand.lossyScale;
            go.transform.localScale = Vector3.Scale(
                blade.transform.localScale,
                new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z));

            // Остриём вверх и вперёд: клинок опущенный читается усталостью,
            // а поднятый — мгновением до удара, ради которого кадр и собран.
            var aim = body.transform.rotation * Quaternion.Euler(-34f, 0f, 12f);
            go.transform.rotation = aim * blade.transform.localRotation;

            // Держат за середину рукояти, а не за цубу: рукоять уходит
            // от начала координат вниз на 0,26 м (props.py, katana).
            //
            // Смещение считаем по прицелу, а не по итоговому повороту:
            // в итоговом сидит ещё и переворот осей модели, и «вверх»
            // в нём смотрит совсем не туда, куда смотрит клинок.
            go.transform.position = hand.position + aim * new Vector3(0f, 0.13f, 0f);
        }

        // ───────────────────────────── кадр ─────────────────────────────

        private static void Frame()
        {
            var go = new GameObject("Камера");
            var camera = go.AddComponent<Camera>();
            camera.fieldOfView = 34f;
            camera.backgroundColor = new Color(0.16f, 0.17f, 0.21f);
            go.tag = "MainCamera";

            // Сбоку и чуть снизу: так за спинами видно и реку, и сакуру,
            // а двое стоят силуэтами против воды.
            go.transform.position = new Vector3(-6.6f, 1.55f, -5.4f);
            go.transform.rotation = Quaternion.LookRotation(
                (new Vector3(0.4f, 1.0f, 3.0f) - go.transform.position).normalized);

            go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        }

        // ──────────────────────────── проверка ────────────────────────────

        /// <summary>
        /// Что попало в кадр. Собрать сцену вслепую можно, а увидеть её —
        /// нет: пакетный режим не рисует. Поэтому спрашиваем камеру
        /// числами — где на экране оказался каждый предмет.
        ///
        /// Доля экрана 0…1 по обеим осям и глубина больше нуля означают
        /// «в кадре». Скелет за краем или сакура за спиной у камеры —
        /// это то, что иначе выяснилось бы только на показе.
        /// </summary>
        [MenuItem("Sinbinder/Проверить кадр сакуры")]
        public static void Check()
        {
            if (!UnityEngine.SceneManagement.SceneManager.GetActiveScene().path.Equals(Path))
                EditorSceneManager.OpenScene(Path);

            var camera = Object.FindFirstObjectByType<Camera>();
            if (camera == null) { Debug.LogError("[САКУРА] Камеры в сцене нет."); return; }

            // Без экрана Unity считает соотношение сторон по пикселям
            // окна, которого в пакетном режиме нет. Задаём кадр сами.
            camera.aspect = 16f / 9f;

            foreach (var name in new[] { "Скелет слева", "Скелет справа", "Sakura", "Островок", "Река" })
            {
                var go = GameObject.Find(name);
                if (go == null) { Debug.LogWarning("[КАДР] " + name + ": нет в сцене."); continue; }

                var middle = Middle(go);
                int parts = go.GetComponentsInChildren<Renderer>(true).Length;
                var v = camera.WorldToViewportPoint(middle);
                bool inside = v.z > 0f && v.x > 0.02f && v.x < 0.98f && v.y > 0.02f && v.y < 0.98f;

                Debug.Log($"[КАДР] {name}: x {v.x:0.00} y {v.y:0.00} даль {v.z:0.0} м — "
                        + (inside ? "в кадре" : "ЗА КРАЕМ")
                        + $" (в мире {middle.x:0.00} {middle.y:0.00} {middle.z:0.00}, "
                        + $"размер {Size.x:0.00}×{Size.y:0.00}×{Size.z:0.00} м, частей {parts})");
            }

            foreach (var skin in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))
            {
                var hand = Bone(skin, "RightHand");
                var blade = hand == null ? null : hand.Find("Катана");
                if (hand == null || blade == null) { Debug.LogWarning("[КАДР] катана не села в руку."); continue; }

                float away = Vector3.Distance(blade.position, hand.position);
                Debug.Log($"[КАДР] катана у {skin.transform.root.name}: {away:0.00} м от ладони");
            }
        }

        /// <summary>Точка входа для пакетного режима.</summary>
        public static void CheckBatch() => Check();

        /// <summary>Размер последнего измеренного предмета — для отчёта.</summary>
        private static Vector3 Size;

        /// <summary>Середина предмета со всей его роднёй, а не точка опоры.</summary>
        private static Vector3 Middle(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return go.transform.position;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            Size = bounds.size;
            return bounds.center;
        }

        private static Transform Bone(SkinnedMeshRenderer skin, string name)
        {
            if (skin.bones == null) return null;
            foreach (var bone in skin.bones)
                if (bone != null && bone.name == name) return bone;
            return null;
        }

        // ──────────────────────────── помощники ────────────────────────────

        private static void Prop(string name, Vector3 at, float yaw, float scale)
        {
            var prefab = Resources.Load<GameObject>("Props/" + name);
            if (prefab == null) return;

            var go = (GameObject)Object.Instantiate(prefab);
            go.name = name;
            go.transform.position = at;

            // К повороту осей модели, а не вместо него (см. DemoSceneBuilder.Axis).
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * prefab.transform.localRotation;

            // Умножаем на масштаб корня, а не затираем его: см. ModelCheck.
            go.transform.localScale = go.transform.localScale * scale;
        }

        private static void Wear(GameObject go, Material material)
        {
            if (material == null) return;

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
        }

        /// <summary>
        /// Одноцветный материал: вода. Не скан — его не из чего брать,
        /// и рябь на такой камере всё равно не читается.
        /// </summary>
        private static Material Paint(string name, Color colour, float smoothness)
        {
            string path = "Assets/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return null;

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_Color")) material.SetColor("_Color", colour);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.1f);

            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
