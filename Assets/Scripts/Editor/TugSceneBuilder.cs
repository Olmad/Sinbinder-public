#if UNITY_EDITOR
// Assets/Scripts/Editor/TugSceneBuilder.cs
// Нагрузочные сцены «Перетягивания каната» — отдельным файлом (автор, 27.10:
// «её сделать отдельным файлом»). Общий низ сцен — тот же, что у демо (DemoSceneBuilder).
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Sinbinder.Core;
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
            "Rock", "Stump", "Tent", "Campfire", "Palisade", "Barrel", "Crate", "Torch", "TorchStand", "LogBench", "GrassTuft",
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

            // Алтарь (автор, 28.10: «в склепе нужен алтарь… поднимать новых, но более послушных воинов»;
            // «собирать как в демо и на полигоне — расставляя по местам»): то же устройство связывания —
            // гнёзда души и тела, рычаг, полка банок, стол тел. Тела на столе — из принесённых (Remains).
            var altar = BindingZone(new Vector3(0f, 0.12f, 2.6f), seed: false);
            altar.name = "Алтарь";
            altar.transform.SetParent(root.transform, true);

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
    /// Проход обучения пакетно (TugTutorial): девять уроков подряд, каждый — как сделал бы игрок, и снимок
    /// плашки урока в учебник (Docs/Учебник/кадры/обучение). Строки «[ОБУЧЕНИЕ]» — сделан ли урок и пошёл ли следующий.
    /// </summary>
    [InitializeOnLoad]
    public static class TutorialRun
    {
        private const string Active = "Sinbinder.TutorialRun.Active";
        private static float _start = -1f, _at = -1f;
        private static int _seen = -1;

        static TutorialRun()
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

        private static void Note(string s) => Debug.Log("[ОБУЧЕНИЕ] " + s);

        private static void Tick()
        {
            if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying) return;
            Sinbinder.UI.StartPanel.ChooseFresh();
            var tut = TugTutorial.Instance;
            if (!SinbinderPlayer.Exists || tut == null || TugMatch.Instance == null) return;
            if (_start < 0f) { _start = Time.realtimeSinceStartup; return; }
            if (Time.realtimeSinceStartup - _start < 3f) return;

            // Новый урок — снимок плашки и действие игрока; дальше ждём, пока урок сам скажет «готово».
            if (tut.Step != _seen)
            {
                _seen = tut.Step;
                _at = Time.realtimeSinceStartup;
                if (_seen >= 9) { Finish(true); return; }
                Sinbinder.Utilets.Snapshot.Now(Sinbinder.Utilets.Reel.Root + "/обучение", $"урок {_seen + 1}");
                Act(_seen, SinbinderPlayer.Instance);
                return;
            }
            if (Time.realtimeSinceStartup - _at > 25f) { Note($"урок {_seen + 1} не сделан за 25 с — НЕ ТАК"); Finish(false); }
        }

        /// <summary>Что делает игрок на уроке.</summary>
        private static void Act(int step, SinbinderPlayer hero)
        {
            var m = TugMatch.Instance;
            int me = m.PlayerSide;
            var folk = new System.Collections.Generic.List<Warrior>();
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
                if (w.Team == Team.Neutral && !w.IsDead && RivalLord.LordOf(w) == null) folk.Add(w);

            switch (step)
            {
                case 0: { var (door, _) = m.PartsOf(me); if (door != null && !door.Open) door.Toggle(); break; }
                case 1:
                    foreach (var r in Object.FindObjectsByType<Residents>(FindObjectsSortMode.None))
                    {
                        var at = r.transform.position + Vector3.back * 4f;
                        hero.transform.position = at;
                        var legs = hero.GetComponent<UnityEngine.AI.NavMeshAgent>();
                        if (legs != null && legs.isOnNavMesh) legs.Warp(at);
                        break;
                    }
                    break;
                case 2: if (folk.Count > 0) folk[0].IssueCommand(CommandKind.Move, folk[0].transform.position + Vector3.forward * 3f); break;
                case 3: if (folk.Count > 1) Favor.Give(folk[1]); break;
                case 4:
                    for (int i = 0; i < 3 && i < folk.Count; i++)
                        Allegiance.Change(folk[i].Id, folk[i].Loyalty, Lords.Player, 70f - Allegiance.Of(folk[i].Id, folk[i].Loyalty, Lords.Player));
                    break;
                case 5:
                    // Отбить: охотники вышли с началом урока — пусть падут (как от наших клинков).
                    foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
                        if (w.Team == Team.Enemy && !w.IsDead) w.GetComponent<Damageable>()?.TakeDamage(99999f, folk.Count > 0 ? folk[0].gameObject : null);
                    break;
                case 6: FieldRun.Altar(hero); break;
                case 7:
                    foreach (var s in BuildSite.All)
                        if (s != null && s.Side == me && s.What == BuildSite.Kind.Fire)
                        { Favor.AddCoins(Lords.Player, s.Cost); s.Lay(); s.Finish(); break; }
                    break;
                case 8:
                    foreach (var r in RivalLord.All)
                    {
                        if (r == null) continue;
                        var (_, throne) = m.PartsOf(r.Side);
                        throne?.TakeDamage(throne.Max * 2f);
                    }
                    break;
            }
            Note($"урок {step + 1}: сделано, что сделал бы игрок");
        }

        private static void Finish(bool all)
        {
            Note(all ? "все девять уроков пройдены — как надо" : "обучение не пройдено");
            EditorApplication.update -= Tick;
            SessionState.SetBool(Active, false);
            Time.timeScale = 1f;
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(0);
        }
    }

    /// <summary>
    /// Длинная партия пакетно: поле, против — ИИ силой из <c>-tugAi Weak|Medium|Strong</c>, игрок стоит
    /// в склепе за закрытой дверью; время ×4, четыре минуты игрового. Каждые 30 с — сводка «[ПАРТИЯ]»:
    /// сколько приказов отдал ИИ, сколько верных собрал, нападает ли, что с дверью и троном игрока.
    /// Так видно то, чего короткая проверка поля не успевает: сбор отряда, поход, осаду.
    /// </summary>
    [InitializeOnLoad]
    public static class MatchRun
    {
        private const string Active = "Sinbinder.MatchRun.Active";
        private const float Length = 420f;   // первая волна охотников — на пятой минуте
        private static float _nextNote = -1f;

        static MatchRun()
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
            if (!SinbinderPlayer.Exists || TugMatch.Instance == null) return;
            if (_nextNote < 0f) { Time.timeScale = 4f; _nextNote = Time.time + 2f; return; }
            if (!TugMatch.Over) Time.timeScale = 4f;
            if (Time.time < _nextNote && Time.time < Length && !TugMatch.Over) return;
            _nextNote = Time.time + 30f;

            var match = TugMatch.Instance;
            var (door, throne) = match.PartsOf(match.PlayerSide);
            var leaf = door != null ? door.Leaf.GetComponent<Sturdy>() : null;
            int hunters = 0;
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None)) if (w.Team == Team.Enemy && !w.IsDead) hunters++;
            int built = 0; foreach (var s in BuildSite.All) if (s != null && s.Done && s.Side != match.PlayerSide) built++;
            Debug.Log($"[ПАРТИЯ] {Time.time:F0} с: охотников на поле {hunters}, волна {HunterWaves.Instance?.Wave}, кошель игрока {Favor.Purse}, построек у ИИ {built}");
            foreach (var ai in RivalLord.All)
            {
                if (ai == null) continue;
                int loyal = 0, aimed = 0;
                foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
                {
                    if (w.IsDead || w.Team != Team.Neutral) continue;
                    if (Allegiance.Dearest(w.Id) == ai.Lord && Allegiance.Of(w.Id, w.Loyalty, ai.Lord) >= Hostility.Follows) loyal++;
                    if (w.HasCommand && w.Command.Lord == ai.Lord && w.Command.Kind == CommandKind.Attack) aimed++;
                }
                Debug.Log($"[ПАРТИЯ] {Time.time:F0} с: {ai.Lord} ({ai.Level}, стиль {ai.Way}/{ai.Second}, грех {ai.Sin}) — приказов {ai.Orders}, верных {loyal}, с приказом бить {aimed}, нападает: {(ai.Attacking ? "да" : "нет")}; " +
                          $"дверь игрока {(leaf != null ? leaf.HP : 0):F0}/{(leaf != null ? leaf.Max : 0):F0}, трон {(throne != null ? throne.HP : 0):F0}/{(throne != null ? throne.Max : 0):F0}; кошель ИИ {Favor.PurseOf(ai.Lord)}");
            }

            if (Time.time < Length && !TugMatch.Over) return;
            Debug.Log($"[ПАРТИЯ] ИТОГ за {Time.time:F0} с: {(TugMatch.Over ? (TugMatch.Won ? "победа игрока" : "поражение игрока") : "партия идёт")}");
            EditorApplication.update -= Tick;
            SessionState.SetBool(Active, false);
            Time.timeScale = 1f;
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(0);
        }
    }

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
        private static Warrior _order;

        private static int People()
        {
            int n = 0;
            if (MapLoader.Current != null) foreach (var l in MapLoader.Current.Locations) n += l.People;
            return n;
        }

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

                // Ничьи (54-TUG.md, шаг 2): жители есть, зря не враждуют, приказ игрока несёт его голос.
                var folk = new System.Collections.Generic.List<Warrior>();
                foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
                    if (w.Team == Team.Neutral && !w.IsDead) folk.Add(w);
                int wars = 0;
                foreach (var a in folk) foreach (var b in folk) if (Hostility.Wants(a, b)) wars++;
                Line($"ничьих жителей {folk.Count} (по карте {People()}), враждуют меж собой без причины: {wars} — {(folk.Count > 0 && wars == 0 ? "как надо" : "НЕ ТАК")}");
                if (folk.Count > 0)
                {
                    _order = folk[0];
                    _order.IssueCommand(CommandKind.Move, _order.transform.position + Vector3.forward * 4f);
                    Line($"приказ ничьему: голос «{_order.Command.Lord}», игрок — «{Lords.Player}» — {(_order.Command.Lord == Lords.Player && Lords.Active ? "как надо" : "НЕ ТАК")}; верность к игроку {Allegiance.Of(_order.Id, _order.Loyalty, Lords.Player):F0}");
                }

                // Шаг 3: Греховод-ИИ встал в своём склепе; осада его двери — по приказу.
                var ai = RivalLord.All.Count > 0 ? RivalLord.All[0] : null;
                Line($"Греховод-ИИ: {(ai != null ? $"{ai.Lord} ({ai.Level}), сторона {ai.Side + 1}" : "НЕТ")}");
                if (ai != null && TugMatch.Instance != null && folk.Count > 1)
                {
                    var (aiDoor, _) = TugMatch.Instance.PartsOf(ai.Side);
                    if (aiDoor != null)
                    {
                        _siege = folk[1];
                        _siegeDoor = aiDoor.Leaf.GetComponent<Sturdy>();
                        _siege.IssueCommand(CommandKind.Attack, aiDoor.Leaf.position, aiDoor.Leaf.gameObject);
                    }
                }
                if (mine != null)
                {
                    mine.Toggle();
                    // Учебник: дверь распахивается — сверху, изнутри склепа.
                    Sinbinder.Utilets.Reel.Aim(mine.Leaf.position, 9f);
                    Sinbinder.Utilets.Reel.Start("дверь", 5f);
                }
                _opened = Time.time;   // игровое время: съёмка тормозит кадры, а дверь и навмеш живут им
                return;
            }
            if (Time.time - _opened < 1.5f) return;
            if (_phase > 0) { Reels(hero); return; }
            _phase = 1;

            bool open = PathOut(hero, mine, out var wide);
            Line($"наружу при открытой двери: {wide} — {(open ? "выход есть" : "НЕ ТАК")}");
            if (_order != null)
            {
                Line($"ничей после приказа: верность к игроку {Allegiance.Of(_order.Id, _order.Loyalty, Lords.Player):F0}, к кому верней всех — «{Allegiance.Dearest(_order.Id) ?? "ни к кому"}» (послушал — растёт, нет — та же)");

                // Подарок (Favor): жадному дороже всех, второй — вдвое дешевле, кошель убывает.
                float before = Allegiance.Of(_order.Id, _order.Loyalty, Lords.Player);
                int purse = Favor.Purse;
                Favor.Give(_order);
                float first = Allegiance.Of(_order.Id, _order.Loyalty, Lords.Player) - before;
                Favor.Give(_order);
                float second = Allegiance.Of(_order.Id, _order.Loyalty, Lords.Player) - before - first;
                Line($"подарок ({_order.Soul.Sin}): первый +{first:F1}, второй +{second:F1}, кошель {purse}→{Favor.Purse}; «{Favor.Attitude(_order)}» — {(first > 0f && second < first && Favor.Purse == purse - 2 ? "как надо" : "НЕ ТАК")}");
            }
        }

        private static int _phase;

        /// <summary>
        /// Учебник (автор, 28.10: «учебник с гифками»): серии кадров одна за другой — подарок
        /// жадному (и обида завистливого рядом), приказ гордому одному; потом — конец проверки.
        /// </summary>
        private static Warrior _proud;
        private static Warrior _siege;
        private static Sturdy _siegeDoor;
        private static float _selectedAt;

        private static void Reels(SinbinderPlayer hero)
        {
            // Приказ — через секунду после выделения: на гифке сперва кольцо, потом шаг (автор, 28.10:
            // «выделение у воинов плохо видно в учебнике»).
            if (_proud != null && Time.time - _selectedAt > 1.2f)
            {
                _proud.IssueCommand(CommandKind.Move, _proud.transform.position + _proud.transform.forward * 6f);
                _proud = null;
            }
            if (Sinbinder.Utilets.Reel.Rolling) return;

            if (_phase == 1)
            {
                _phase = 2;
                var taker = Pick(SinType.Greed, null);
                if (taker == null) return;
                var front = taker.transform.position + taker.transform.forward * 1.6f;
                hero.transform.position = front;
                var agent = hero.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.isOnNavMesh) agent.Warp(front);
                Sinbinder.Utilets.Reel.Aim(taker.transform.position, 6f);
                Sinbinder.Utilets.Reel.Start("подарок", 4.5f);
                Favor.Give(taker);
                Line($"учебник: подарок — {taker.Soul.Sin}, «{Favor.Attitude(taker)}»");
                return;
            }

            if (_phase == 2)
            {
                _phase = 3;
                var proud = Pick(SinType.Pride, _order);
                if (proud == null) return;
                // Рядом — иначе туман войны: видит только Греховод, и голос слышен вблизи.
                var near = proud.transform.position - proud.transform.forward * 2.5f;
                hero.transform.position = near;
                var legs = hero.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (legs != null && legs.isOnNavMesh) legs.Warp(near);
                // Выделить, как игрок щелчком: кольцо цвета кости — ничей.
                var ring = proud.GetComponentInChildren<SelectionComponent>();
                if (ring != null) ring.Select();
                _proud = proud;
                _selectedAt = Time.time;
                Sinbinder.Utilets.Reel.Aim(proud.transform.position + proud.transform.forward * 3f, 7f);
                Sinbinder.Utilets.Reel.Start("приказ", 6.5f);
                Line("учебник: приказ гордому — одному, первым");
                return;
            }

            // Гибель Греховода (автор, 28.10): кошель — на землю, души нет, сторона в игре; встаёт у трона.
            if (_phase == 3)
            {
                _phase = 4;
                int had = Favor.Purse;
                hero.GetComponent<Damageable>().TakeDamage(99999f, null);
                int lying = 0;
                foreach (var p in LordPurse.All) if (p != null) lying += p.Coins;
                bool down = TugMatch.Instance != null && TugMatch.Instance.Down(TugMatch.Instance.PlayerSide);
                bool soul = CombatManager.Instance != null && CombatManager.Instance.BodiesOnField.Exists(b => b != null && b.name.Contains(hero.DisplayName));
                Line($"Греховод пал: ждёт у трона — {(down ? "да" : "НЕТ")}, кошель на земле {lying} мон. (было {had}), в кошеле {Favor.Purse}, тело для жатвы — {(soul ? "ЕСТЬ" : "нет")}, партия идёт — {(!TugMatch.Over ? "да" : "НЕТ")} — {(down && lying == had && Favor.Purse == 0 && !soul && !TugMatch.Over ? "как надо" : "НЕ ТАК")}");
                var (_, ownThrone) = TugMatch.Instance.PartsOf(TugMatch.Instance.PlayerSide);
                bool crown = ownThrone != null && ownThrone.transform.Find("Корона на троне") != null;
                bool bar = ownThrone != null && ownThrone.transform.Find("Полоса до восстания") != null;
                Line($"на троне корона — {(crown ? "да" : "НЕТ")}, полоса до восстания своей стороне — {(bar ? "да" : "НЕТ")}");
                // Учебник: корона и полоса над пустым троном.
                if (ownThrone != null) Sinbinder.Utilets.Snapshot.Now(Sinbinder.Utilets.Reel.Root + "/корона", "000");
                TugMatch.Instance?.Rise(TugMatch.Instance.PlayerSide);
                return;
            }
            if (_phase == 4)
            {
                _phase = 5;
                var (_, mineThrone) = TugMatch.Instance.PartsOf(TugMatch.Instance.PlayerSide);
                float off = mineThrone != null ? CampFocus.GroundDistance(hero.transform.position, mineThrone.transform.position) : -1f;
                Line($"Греховод встал: жив — {(!hero.IsDead ? "да" : "НЕТ")}, от трона {off:F1} м — {(!hero.IsDead && off >= 0f && off < 4f ? "у трона, как надо" : "НЕ ТАК")}; корона убрана — {(mineThrone != null && mineThrone.transform.Find("Корона на троне") == null ? "да" : "НЕТ")}");
            }

            // Алтарь (автор, 28.10): убил — тело на плечи — на стол — душу в гнездо, тело на ложе — рычаг.
            if (_phase == 5)
            {
                _phase = 6;
                Altar(hero);

                // Охотники (54-TUG.md): волна — к каждому живому склепу, с ближней тропы, на склеп.
                int sent = HunterWaves.Instance != null ? HunterWaves.Instance.Release() : 0;
                int hunters = 0, aimed = 0;
                foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
                {
                    if (w.Team != Team.Enemy || w.IsDead || w.GetComponent<EventGuard>() != null) continue;   // стражи событий — не охотники
                    hunters++;
                    if (w.GetComponent<HunterSiege>() != null && w.GetComponent<HunterGoal>() != null) aimed++;
                }
                int crypts = 0;
                for (int i = 0; i < TugMatch.Instance.Crypts.Count; i++) if (!TugMatch.Instance.Out(i)) crypts++;
                int want = 0;
                for (int i = 0; i < TugMatch.Instance.Crypts.Count; i++) if (!TugMatch.Instance.Out(i)) want += 3 + HunterWaves.Loyal(i) / 4;
                Line($"охотники, волна {HunterWaves.Instance?.Wave}: вышло {sent} (по 3 на {crypts} склепа и по одному на каждые 4 верных), на поле {hunters}, идут на склеп {aimed} — {(sent == want && aimed == hunters ? "как надо" : "НЕ ТАК")}");

                // Постройки (54-TUG.md): места перед каждым склепом; заложить — монеты; достроить; дозорный — наверх.
                int side0 = TugMatch.Instance.PlayerSide, sites = 0;
                BuildSite tower = null, gateSite = null, fireSite = null;
                foreach (var s in BuildSite.All)
                {
                    if (s == null) continue;
                    sites++;
                    if (s.Side != side0) continue;
                    if (s.What == BuildSite.Kind.Tower && tower == null) tower = s;
                    if (s.What == BuildSite.Kind.Gate) gateSite = s;
                    if (s.What == BuildSite.Kind.Fire) fireSite = s;
                }
                Favor.AddCoins(Lords.Player, 20);
                int purse = Favor.Purse;
                tower?.Lay(); gateSite?.Lay(); fireSite?.Lay();
                bool paid = Favor.Purse == purse - 17;
                tower?.Finish(); gateSite?.Finish(); fireSite?.Finish();
                var post = tower != null ? tower.GetComponent<TowerPost>() : null;
                Warrior lookout = null;
                foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
                    if (w.Team == Team.Neutral && !w.IsDead && RivalLord.LordOf(w) == null && w.Soul != null && w.Soul.Sin == SinType.Sloth) { lookout = w; break; }
                if (lookout != null && post != null)
                {
                    Allegiance.Change(lookout.Id, lookout.Loyalty, Lords.Player, 70f - Allegiance.Of(lookout.Id, lookout.Loyalty, Lords.Player));
                    post.Take(lookout);
                }
                bool up = lookout != null && post != null && post.Guard == lookout && Mathf.Abs(lookout.transform.position.y - post.Top.y) < 0.2f;
                Line($"постройки: мест {sites} (по 4 на склеп), заложены вышка, ворота, костёр — плачено {(paid ? "17 мон., как надо" : "НЕ ТАК")}; " +
                     $"вышка — {(post != null ? "стоит" : "НЕТ")}, дозорный наверху — {(up ? "да" : "НЕТ")}; ворота — {(BuildSite.GateOf(side0) != null ? "стоят" : "НЕТ")}; " +
                     $"костёр — {(fireSite != null && BuildSite.NearFire(side0, fireSite.transform.position, 1f) ? "горит" : "НЕТ")} — {(sites == 4 * TugMatch.Instance.Crypts.Count && paid && up && BuildSite.GateOf(side0) != null ? "как надо" : "НЕ ТАК")}");
                if (gateSite != null) Sinbinder.Utilets.Reel.Aim(gateSite.transform.position, 16f);
                if (gateSite != null) Sinbinder.Utilets.Snapshot.Now(Sinbinder.Utilets.Reel.Root + "/постройки", "000");

                // Экспедиции (MapEvent по ExpeditionBook): роды по перестановке номера партии; боевое —
                // перебили стражу, развилку решили пошедшие; мирное — выбор Греховода; события живут разом,
                // кончилось — новое ждёт паузы (EventDirector).
                var kinds = new System.Collections.Generic.HashSet<int>();
                for (int j = 0; j < ExpeditionBook.All.Length; j++) kinds.Add(MapEvent.Pick(j));
                var director = EventDirector.Instance;
                int alive = EventDirector.Alive(), target = director != null ? director.Target : -1, places = director != null ? director.Places.Count : 0;
                MapEvent Fresh(bool parley)
                {
                    foreach (var e in MapEvent.All) if (e != null && !e.Done && e.Parley == parley) return e;
                    for (int j = 0; director != null && j < ExpeditionBook.All.Length * 2; j++)
                        if (ExpeditionBook.All[MapEvent.Pick(j)].Parley == parley) { var e = director.Spawn(j); e?.Ensure(); return e; }
                    return null;
                }
                MapEvent caravan = Fresh(false);
                int guarded = 0, fights = 0;
                foreach (var e in MapEvent.All) if (e != null && !e.Parley && !e.Done) { fights++; if (e.Guards.Count == e.Entry.Guards) guarded++; }
                string fightKind = caravan != null ? caravan.Kind : "—";
                if (caravan != null)
                {
                    Warrior fighter = null;
                    foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
                        if (w.Team == Team.Neutral && !w.IsDead && RivalLord.LordOf(w) == null && w != lookout) { fighter = w; break; }
                    if (fighter != null)
                    {
                        Allegiance.Change(fighter.Id, fighter.Loyalty, Lords.Player, 70f - Allegiance.Of(fighter.Id, fighter.Loyalty, Lords.Player));
                        var near = caravan.transform.position + Vector3.right * 2f;
                        fighter.transform.position = near;
                        var legs = fighter.GetComponent<UnityEngine.AI.NavMeshAgent>();
                        if (legs != null && legs.isOnNavMesh) legs.Warp(near);
                        foreach (var g in caravan.Guards) if (g != null && !g.IsDead) g.GetComponent<Damageable>().TakeDamage(99999f, fighter.gameObject);
                    }
                    if (fighter != null) { caravan.Send(Lords.Player, new System.Collections.Generic.List<Warrior> { fighter }); caravan.Suggest(1); }
                    caravan.Resolve();
                }
                bool fought = caravan != null && caravan.Done;
                string leader = caravan != null && caravan.LastLeader != null ? caravan.LastLeader.DisplayName + " (" + caravan.LastLeader.Soul?.Sin + ")" : "—";
                // Мирное: Греховод выбирает бесплатную развилку.
                var talk = Fresh(true);
                string talkKind = talk != null ? talk.Kind : "—";
                bool chose = talk != null && talk.Choose(talk.Entry.A.Cost == 0 ? 0 : talk.Entry.B.Cost == 0 ? 1 : 0, Lords.Player);
                int waiting = director != null ? director.Waiting : 0;
                Line($"экспедиции: родов в книге {ExpeditionBook.All.Length}, перестановка даёт разных {kinds.Count}; мест {places}, живут разом {alive} из {target} (ждут паузы {(director != null ? director.Waiting : 0)} — ИИ мог решить своё); боевых {fights}, стража на месте у {guarded}; боевое «{fightKind}» взято — {(fought ? "да" : "НЕТ")}, решал лидер {leader}; мирное «{talkKind}» решено — {(chose ? "да" : "НЕТ")}; новых ждут паузы {waiting} — {(kinds.Count == ExpeditionBook.All.Length && target >= 2 && alive + (director != null ? director.Waiting : 0) >= target && guarded == fights && fought && leader != "—" && chose && waiting >= 2 ? "как надо" : "НЕ ТАК")}");

                // Жители — каждый одет по-своему (автор, 28.10: «все воины очень похожи»): три портрета — смотреть глазами.
                int dressed = 0;
                foreach (var r in Object.FindObjectsByType<Residents>(FindObjectsSortMode.None))
                    foreach (var w in r.Folk)
                        if (w != null && !w.IsDead && dressed < 3 && RivalLord.LordOf(w) == null)
                        {
                            // Ночь и туман: без света кадр выходил чёрным (29.10) — открыть место и подсветить.
                            FogOfWar.Reveal(w.transform.position, 8f);
                            var lamp = new GameObject("свет кадра").AddComponent<Light>();
                            lamp.type = LightType.Point; lamp.range = 6f; lamp.intensity = 3f;
                            lamp.transform.position = w.transform.position + w.transform.forward * 1.5f + Vector3.up * 2f;
                            Sinbinder.Utilets.Snapshot.Portrait("Docs/Образцы/облик в игре", "житель " + (dressed + 1), w.transform);
                            Object.DestroyImmediate(lamp.gameObject);
                            dressed++;
                        }

                // Великая охота: в лобби по умолчанию включена; конец по ней — у кого больше верных.
                Line($"Великая охота: {(MapSession.GreatHunt ? "включена" : "выключена")}, на {HunterWaves.GreatAt / 60f:F0}-й минуте, решает через {HunterWaves.GreatHold / 60f:F0} мин.");
            }

            // Шаг 3: что делал ИИ, чего добилась осада, и конец партии — трон ИИ рушим: победа.
            var rival = RivalLord.All.Count > 0 ? RivalLord.All[0] : null;
            if (rival != null)
            {
                Line($"Греховод-ИИ за {Time.time:F0} с: приказов {rival.Orders}, нападает: {(rival.Attacking ? "да" : "нет")}, жив: {(!rival.GetComponent<Warrior>().IsDead ? "да" : "нет")}");
                if (_siegeDoor != null)
                    Line($"осада двери ИИ: прочность {_siegeDoor.HP:F0} из {_siegeDoor.Max:F0}, осаждающий {(_siege != null ? (_siege.HasCommand ? "при приказе" : "приказ снял/отказал") : "—")} (отказ возможен: голосует сам)");
                var (_, throne) = TugMatch.Instance.PartsOf(rival.Side);
                if (throne != null) throne.TakeDamage(throne.Max * 2f);
                Line($"трон ИИ разрушен — {(TugMatch.Over && TugMatch.Won ? "победа, как надо" : "НЕ ТАК")}");
            }

            Line("ГОТОВО");
            EditorApplication.update -= Tick;
            SessionState.SetBool(Active, false);
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(0);
        }

        /// <summary>Алтарь целиком, как игрок: убить, взвалить, положить на стол, собрать на устройстве.</summary>
        internal static void Altar(SinbinderPlayer hero)
        {
            var device = TugMatch.Instance.Crypts[TugMatch.Instance.PlayerSide].GetComponentInChildren<Sinbinder.Crypt.BindingDevice>();
            if (device == null) { Line("алтарь: устройства в склепе НЕТ"); return; }

            Warrior victim = null;
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
                if (w.Team == Team.Neutral && !w.IsDead && (w.Shell == ShellType.Zombie || (victim == null && w.Shell == ShellType.Skeleton)) && RivalLord.LordOf(w) == null) { victim = w; if (w.Shell == ShellType.Zombie) break; }
            if (victim == null) { Line("алтарь: ничьего с телом для проверки нет"); return; }
            var raise = victim.Shell == ShellType.Zombie ? ShellType.Zombie : ShellType.Skeleton;
            int fleshWant = raise == ShellType.Zombie ? 1 : 0;

            var soul = victim.Soul;
            victim.GetComponent<Damageable>().TakeDamage(99999f, hero.gameObject);
            HarvestableBody body = null;
            foreach (var b in CombatManager.Instance.BodiesOnField) if (b != null && b.name == "Body_" + victim.DisplayName) body = b;
            if (body == null) { Line("алтарь: тела убитого НЕТ"); return; }

            Remains.Take(body);
            bool slow = Remains.Carrying;
            Remains.Lay();
            int bones = Remains.Count(Remains.Stuff.Bones), flesh = Remains.Count(Remains.Stuff.Flesh);

            Sinbinder.Crypt.CryptHands.TakeSoul(soul, SoulQuality.Acceptance);
            device.PutSoul();
            bool enough = Remains.Enough(raise);
            Remains.Spend(raise);
            Sinbinder.Crypt.CryptHands.TakeShell(raise);
            device.PutShell();
            int before = device.Raised;
            device.Bind();

            Warrior risen = null;
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
                if (!w.IsDead && w.DisplayName == soul.Name) risen = w;
            float trust = risen != null ? Allegiance.Of(risen.Id, risen.Loyalty, Lords.Player) : -1f;
            bool ok = slow && flesh == fleshWant && bones == 1 && enough && device.Raised == before + 1 && risen != null
                      && risen.Team == Team.Neutral && trust > 30f && trust < 50f;
            Line($"алтарь: ноша — {(slow ? "на плечах" : "НЕТ")}; на столе стало плоть {flesh}, кости {bones}; " +
                 $"поднят — {(risen != null ? $"{risen.DisplayName} ({risen.Shell}, {risen.Team})" : "НЕТ")}; верность к поднявшему {trust:F0} " +
                 $"(убил его сам — меньше «готов слушать»); материала осталось: кости {Remains.Count(Remains.Stuff.Bones)} — {(ok ? "как надо" : "НЕ ТАК")}");
        }

        /// <summary>Ничей с этим грехом; такого нет — любой, кроме <paramref name="not"/>.</summary>
        private static Warrior Pick(SinType sin, Warrior not)
        {
            Warrior any = null;
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
            {
                if (w.Team != Team.Neutral || w.IsDead || w == not || w.Soul == null) continue;
                if (w.Soul.Sin == sin) return w;
                any ??= w;
            }
            return any;
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

        internal static void Line(string s) => Debug.Log("[ПОЛЕ] " + s);
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
