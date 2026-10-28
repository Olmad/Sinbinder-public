#if UNITY_EDITOR
// Assets/Scripts/Editor/TugSceneBuilder.cs
// Нагрузочные сцены «Перетягивания каната» — отдельным файлом (автор, 27.10:
// «её сделать отдельным файлом»). Общий низ сцен — тот же, что у демо (DemoSceneBuilder).
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.Utilets
{
    public static partial class DemoSceneBuilder
    {
        /// <summary>Сцены полей — в списке сборки с 27.10: по ним гуляют из паузы.</summary>
        private const string TugDir = "Assets/Scenes/Tug";

        [MenuItem("Sinbinder/Нагрузка/Собрать поля «Перетягивания» (2 и 4 стороны)")]
        public static void BuildTugScenes()
        {
            BuildTug(2);
            BuildTug(4);
        }

        public static void BuildTugScenesBatch() { BuildMapProps(); BuildTugScenes(); BuildField(); BuildMapEditor(); }

        /// <summary>Вещи карт — те, что ставят палитра редактора и карты по умолчанию.</summary>
        private static readonly string[] MapPropNames =
        {
            "Pine", "TallPineA", "TallPineB", "TallPineC", "DeadTreeA", "DeadTreeB", "DeadTreeC", "Bush",
            "Rock", "Stump", "Tent", "Campfire", "Palisade", "Barrel", "Crate", "Torch", "LogBench", "GrassTuft",
        };

        private const string MapPropsDir = "Assets/Resources/MapProps";
        private const string MapMaterialsDir = "Assets/Materials/Карты";

        /// <summary>
        /// Одетые вещи карт (автор, 27.10: «у палаток в Перетягивании видно только внутренние стены»):
        /// модель из Resources/Props, накрытая тем же материалом, что в лагере (<see cref="Surface"/>), —
        /// префабом в Resources/MapProps. Карту собирают в игре (MapLoader, редактор карт), а
        /// MaterialBuilder живёт только в редакторе: голая модель из Props шла без фактуры, и ткань
        /// палатки, видная лишь с одной стороны, показывала только нутро. И склеп (<see cref="BuildCrypt"/>).
        /// </summary>
        [MenuItem("Sinbinder/Нагрузка/Собрать вещи карт")]
        public static void BuildMapProps()
        {
            if (!AssetDatabase.IsValidFolder(MapPropsDir)) AssetDatabase.CreateFolder("Assets/Resources", "MapProps");
            if (!AssetDatabase.IsValidFolder(MapMaterialsDir)) AssetDatabase.CreateFolder("Assets/Materials", "Карты");

            foreach (var name in MapPropNames)
            {
                var go = Prop(name, null, Vector3.zero);
                if (go == null) { Debug.LogWarning($"[КАРТЫ] Нет модели {name} в Resources/Props"); continue; }
                PrefabUtility.SaveAsPrefabAsset(go, $"{MapPropsDir}/{name}.prefab");
                Object.DestroyImmediate(go);
            }
            BuildCrypt();
            AssetDatabase.SaveAssets();
            Debug.Log($"[НАГРУЗКА] Собраны вещи карт: {MapPropNames.Length} и склеп");
        }

        /// <summary>
        /// Склеп стороны на карте (автор, 27.10: «у склепа не хватает стен и возможности открывать
        /// дверь и пола»): каменный пол, стены в рост с проёмом спереди (+Z), дверь на петле
        /// (<see cref="CryptDoor"/>, прочность — <see cref="Sturdy"/>) и трон у задней стены (тоже
        /// Sturdy). Крыши нет: камера смотрит сверху — внутрь. 12 на 14 м; свет — один, тёплый, без тени.
        /// Имена и запас прочности даёт MapLoader: сторона известна только на поле.
        /// </summary>
        private static void BuildCrypt()
        {
            var root = new GameObject("Crypt");
            var floor = Tiled("PavingStones127", new Vector2(6f, 7f), "Склеп — пол");
            var back = Tiled("Bricks076A", new Vector2(6.3f, 1.8f), "Склеп — стена");
            var side = Tiled("Bricks076A", new Vector2(7f, 1.8f), "Склеп — бок");
            var part = Tiled("Bricks076A", new Vector2(2.45f, 1.8f), "Склеп — простенок");
            var lintel = Tiled("Bricks076A", new Vector2(1.4f, 0.4f), "Склеп — перемычка");
            var wood = Tiled("Planks037A", new Vector2(1.4f, 1.35f), "Склеп — дверь");

            Slab(root.transform, "Пол", new Vector3(0f, 0.06f, 0f), new Vector3(12f, 0.12f, 14f), floor);
            Slab(root.transform, "Стена сзади", new Vector3(0f, 1.8f, -7f), new Vector3(12.6f, 3.6f, 0.6f), back);
            Slab(root.transform, "Стена слева", new Vector3(-6f, 1.8f, 0f), new Vector3(0.6f, 3.6f, 14f), side);
            Slab(root.transform, "Стена справа", new Vector3(6f, 1.8f, 0f), new Vector3(0.6f, 3.6f, 14f), side);
            Slab(root.transform, "Простенок слева", new Vector3(-3.85f, 1.8f, 7f), new Vector3(4.9f, 3.6f, 0.6f), part);
            Slab(root.transform, "Простенок справа", new Vector3(3.85f, 1.8f, 7f), new Vector3(4.9f, 3.6f, 0.6f), part);
            Slab(root.transform, "Перемычка", new Vector3(0f, 3.2f, 7f), new Vector3(2.8f, 0.8f, 0.6f), lintel);

            // Петля — у левого косяка; створка тянется от неё вправо и распахивается внутрь.
            var hinge = new GameObject("Дверь склепа");
            hinge.transform.SetParent(root.transform, false);
            hinge.transform.localPosition = new Vector3(-1.4f, 0.12f, 7f);
            var leaf = Slab(hinge.transform, "Створка", new Vector3(1.4f, 1.34f, 0f), new Vector3(2.8f, 2.68f, 0.16f), wood);
            leaf.AddComponent<Sturdy>();
            // Закрытая держит путь вырезом в навмеше; в саму выпечку не идёт — иначе проём
            // остался бы стеной и после того, как дверь открыли.
            var gap = leaf.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            gap.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            gap.size = Vector3.one;
            gap.carving = true;
            gap.carveOnlyStationary = false;
            leaf.AddComponent<Unity.AI.Navigation.NavMeshModifier>().ignoreFromBuild = true;
            hinge.AddComponent<CryptDoor>();

            var throne = Prop("Throne", root.transform, new Vector3(0f, 0.12f, -5f));
            if (throne != null)
            {
                throne.name = "Трон";
                throne.AddComponent<BoxCollider>();   // по рамке модели: на него наводят мышь
                throne.AddComponent<Sturdy>();
            }

            var lamp = new GameObject("Свет склепа");
            lamp.transform.SetParent(root.transform, false);
            lamp.transform.localPosition = new Vector3(0f, 2.8f, -2f);
            var light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 10f;
            light.intensity = 1.6f;
            light.color = new Color(1f, 0.72f, 0.45f);
            light.shadows = LightShadows.None;

            PrefabUtility.SaveAsPrefabAsset(root, $"{MapPropsDir}/Crypt.prefab");
            Object.DestroyImmediate(root);
        }

        /// <summary>Кирпич склепа — куб нужного размера в нужном материале.</summary>
        private static GameObject Slab(Transform parent, string name, Vector3 at, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>
        /// Материал лагеря с шагом фактуры под размер грани: куб растягивает её на всю стену, и
        /// кирпич выходил бы метровым. Шаг — примерно два метра на повтор.
        /// </summary>
        private static Material Tiled(string id, Vector2 tiling, string name)
        {
            var source = MaterialBuilder.Get(id);
            if (source == null) { Debug.LogWarning($"[КАРТЫ] Нет материала {id}"); return null; }
            string path = $"{MapMaterialsDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(source) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            else m.CopyPropertiesFromMaterial(source);
            m.mainTextureScale = tiling;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// Сцена редактора карт (<see cref="MapEditor"/>; автор, 27.10): земля, свет, камера сверху,
        /// ни менеджеров боя, ни Греховода — только правка описания карты.
        /// </summary>
        [MenuItem("Sinbinder/Нагрузка/Собрать редактор карт")]
        public static void BuildMapEditor()
        {
            var scene = NewScene();
            Atmosphere(warm: false);
            Ground("Поле", 40f, AssetDatabase.IsValidFolder("Assets/Textures/Ground077") ? "Ground077" : "Ground048");
            Look();
            CameraRig(new Vector3(0f, 70f, -25f), new Vector3(72f, 0f, 0f), movable: false);
            new GameObject("Редактор карт").AddComponent<MapEditor>();

            if (!AssetDatabase.IsValidFolder(TugDir)) AssetDatabase.CreateFolder("Assets/Scenes", "Tug");
            string path = $"{TugDir}/MapEditor.unity";
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
            RegisterInBuildSettings(path);
            Debug.Log($"[НАГРУЗКА] Собрана: {path}");
        }

        /// <summary>
        /// Сцена «Поле» — пустая основа, в которой карта из файла собирается в игре
        /// (<see cref="MapLoader"/>; 54-TUG.md): земля, свет, менеджеры, интерфейс, камера.
        /// Всё остальное — из описания карты.
        /// </summary>
        [MenuItem("Sinbinder/Нагрузка/Собрать сцену «Поле» (карты из файлов)")]
        public static void BuildField()
        {
            var scene = NewScene();
            Atmosphere(warm: false);
            Ground("Поле", 40f, AssetDatabase.IsValidFolder("Assets/Textures/Ground077") ? "Ground077" : "Ground048");
            Look();
            Managers();
            Interface();
            CameraRig(new Vector3(0f, 40f, -40f), new Vector3(55f, 0f, 0f), movable: true);
            new GameObject("Карта").AddComponent<MapLoader>();

            if (!AssetDatabase.IsValidFolder(TugDir)) AssetDatabase.CreateFolder("Assets/Scenes", "Tug");
            string path = $"{TugDir}/Field.unity";
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
            RegisterInBuildSettings(path);
            Debug.Log($"[НАГРУЗКА] Собрана: {path}");
        }

        /// <summary>
        /// Поле на 2 или 4 стороны по сотне: 800 на 800 м плит (самая большая карта,
        /// какую стоит печь навмешем при запуске), холодная ночь, камера сверху и стенд
        /// (<see cref="TugBench"/>), который ставит воинов и мерит кадр.
        /// </summary>
        private static void BuildTug(int sides)
        {
            var scene = NewScene();
            Atmosphere(warm: false);
            Ground("Поле", 80f, "PavingStones127");
            Look();
            Managers();
            Interface();

            CameraRig(new Vector3(0f, 60f, -55f), new Vector3(55f, 0f, 0f), movable: true);

            var bench = new GameObject("Стенд нагрузки").AddComponent<TugBench>();
            var so = new SerializedObject(bench);
            so.FindProperty("_sides").intValue = sides;
            so.FindProperty("_perSide").intValue = 100;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Прогулка по полю (автор, 27.10): Греховод — на диагонали, между сторонами,
            // чтобы выпущенные по B армии шли не сквозь него. Камера следует за ним сама.
            var entry = new GameObject("Появление Греховода");
            entry.transform.position = new Vector3(40f, 0f, -40f);
            entry.AddComponent<Sinbinder.Crypt.TestChamberEntry>();

            if (!AssetDatabase.IsValidFolder(TugDir)) AssetDatabase.CreateFolder("Assets/Scenes", "Tug");
            string path = $"{TugDir}/Tug_{sides}.unity";
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
            // В сборку — чтобы гулять по полю из паузы (PauseMenu, «Поля „Перетягивания“»).
            RegisterInBuildSettings(path);
            Debug.Log($"[НАГРУЗКА] Собрана: {path}");
        }
    }
}

namespace Sinbinder.EditorTools
{
    /// <summary>
    /// Поле из файла — пакетно (автор, 27.10: склеп без стен, пола и двери; прочность трона и двери):
    /// открыть сцену Field, войти в игру и через три секунды проверить — склепы и троны на месте,
    /// Греховод внутри своего, наружу при закрытой двери не пройти, а при открытой — можно; палатки
    /// одеты. Строки «[ПОЛЕ]» — в журнал Unity (D:\Claude\runs\s-field.ps1 их и выводит).
    /// </summary>
    [InitializeOnLoad]
    public static class FieldRun
    {
        private const string Active = "Sinbinder.FieldRun.Active";
        private static float _start = -1f, _opened = -1f;

        static FieldRun()
        {
            if (SessionState.GetBool(Active, false)) EditorApplication.update += Tick;
        }

        public static void RunBatch()
        {
            EditorUtility.audioMasterMute = true;
            SessionState.SetBool(Active, true);
            EditorSceneManager.OpenScene("Assets/Scenes/Tug/Field.unity");
            EditorApplication.isPlaying = true;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying) return;
            Sinbinder.UI.StartPanel.ChooseFresh();
            if (!SinbinderPlayer.Exists) return;
            if (_start < 0f) { _start = Time.realtimeSinceStartup; return; }
            if (Time.realtimeSinceStartup - _start < 3f) return;

            var hero = SinbinderPlayer.Instance;
            CryptDoor mine = null;
            float best = float.MaxValue;
            var doors = Object.FindObjectsByType<CryptDoor>(FindObjectsSortMode.None);
            foreach (var d in doors)
            {
                float k = Vector3.Distance(d.transform.position, hero.transform.position);
                if (k < best) { best = k; mine = d; }
            }

            if (_opened < 0f)
            {
                int thrones = 0;
                foreach (var s in Object.FindObjectsByType<Sturdy>(FindObjectsSortMode.None))
                {
                    if (s.GetComponentInParent<CryptDoor>() == null) thrones++;
                    if (s.Mine) Line("наведение: " + s.Describe().Replace("\n", " · "));
                }
                Line($"склепов на карте {MapLoader.Current?.Crypts.Count}, дверей {doors.Length}, тронов {thrones}");
                float inside = mine != null ? CampFocus.GroundDistance(hero.transform.position, mine.transform.parent.position) : -1f;
                Line($"Греховод от середины своего склепа {inside:F1} м — {(inside >= 0f && inside < 6.5f ? "внутри" : "НЕ ТАК")}");
                bool through = PathOut(hero, mine, out var shut);
                Line($"наружу при закрытой двери: {shut} — {(!through ? "заперто, как надо" : "НЕ ТАК")}");
                Line($"палатки одеты: {Dressed("Tent", "Fabric")}");
                if (mine != null) mine.Toggle();
                _opened = Time.realtimeSinceStartup;
                return;
            }
            if (Time.realtimeSinceStartup - _opened < 1.5f) return;

            bool open = PathOut(hero, mine, out var wide);
            Line($"наружу при открытой двери: {wide} — {(open ? "выход есть" : "НЕ ТАК")}");
            Line("ГОТОВО");
            EditorApplication.update -= Tick;
            SessionState.SetBool(Active, false);
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(0);
        }

        /// <summary>Есть ли путь от Греховода за дверь его склепа — на 11 м перед ней.</summary>
        private static bool PathOut(SinbinderPlayer hero, CryptDoor door, out string status)
        {
            status = "двери нет";
            if (door == null || door.transform.parent == null) return false;
            var crypt = door.transform.parent;
            var outside = crypt.position + crypt.forward * 11f;
            if (!UnityEngine.AI.NavMesh.SamplePosition(hero.transform.position, out var from, 2f, UnityEngine.AI.NavMesh.AllAreas)
                || !UnityEngine.AI.NavMesh.SamplePosition(outside, out var to, 3f, UnityEngine.AI.NavMesh.AllAreas))
            {
                status = "нет навмеша";
                return false;
            }
            var path = new UnityEngine.AI.NavMeshPath();
            UnityEngine.AI.NavMesh.CalculatePath(from.position, to.position, UnityEngine.AI.NavMesh.AllAreas, path);
            status = path.status.ToString();
            return path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete;
        }

        private static string Dressed(string prop, string material)
        {
            int all = 0, dressed = 0;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!r.gameObject.name.StartsWith(prop)) continue;
                all++;
                if (r.sharedMaterial != null && r.sharedMaterial.name.StartsWith(material)) dressed++;
            }
            return $"{dressed} из {all}";
        }

        private static void Line(string s) => Debug.Log("[ПОЛЕ] " + s);
    }

    /// <summary>
    /// Замер поля пакетно: открыть сцену, войти в игру, дождаться итога стенда, выйти.
    /// Сцена — аргумент <c>-tugSides 2|4</c>. Отчёт — <see cref="TugBench.Report"/>.
    /// </summary>
    [InitializeOnLoad]
    public static class TugRun
    {
        private const string Active = "Sinbinder.TugRun.Active";

        static TugRun()
        {
            if (SessionState.GetBool(Active, false)) EditorApplication.update += Tick;
        }

        public static void RunBatch()
        {
            int sides = 2;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-tugSides" && int.TryParse(args[i + 1], out var s)) sides = s;

            EditorUtility.audioMasterMute = true;
            SessionState.SetBool(Active, true);
            EditorSceneManager.OpenScene($"Assets/Scenes/Tug/Tug_{sides}.unity");
            EditorApplication.isPlaying = true;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying) return;

            // Вопрос о сохранении ставит игру на паузу — отвечаем, как игрок.
            Sinbinder.UI.StartPanel.ChooseFresh();
            if (!TugBench.Done) return;

            EditorApplication.update -= Tick;
            SessionState.SetBool(Active, false);
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(0);
        }
    }
}
#endif
