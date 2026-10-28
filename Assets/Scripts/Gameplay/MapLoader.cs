// Assets/Scripts/Gameplay/MapLoader.cs
// Перевод: текст через Loc
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Собирает поле «Перетягивания каната» из файла карты (<see cref="MapData"/>; 54-TUG.md) —
    /// в игре, а не в редакторе: карты лежат в папке и правятся без пересборки сцен.
    ///
    /// В <c>Awake</c> — до выпечки навмеша (<see cref="GroundNavMesh"/> печёт в <c>Start</c>):
    /// деревья, палатки и склепы должны попасть в навмеш препятствиями.
    ///
    /// Пока ставит: землю по размеру карты, вещи, склепы с тронами, метки подлокаций, троп
    /// и событий, Греховода игрока в его склепе. Жители подлокаций, Греховоды-ИИ, охотники
    /// волнами и события — шаги 1–4 (54-TUG.md).
    /// </summary>
    public class MapLoader : MonoBehaviour
    {
        public static MapData Current { get; private set; }

        /// <summary>Номер партии «Перетягивания» — из него и имени карты складываются её люди.</summary>
        public static int Match { get; private set; }

        private const string MatchKey = "Sinbinder.TugMatch";

        void Awake()
        {
            var map = MapSession.Map;
            if (map == null)
            {
                var all = MapData.List();
                if (all.Count == 0) { Debug.LogError("[КАРТЫ] В папке карт нет ни одной карты."); return; }
                MapSession.Choose(all[0].path, all[0].map);
                map = all[0].map;
                // Пакетная партия (TugSceneBuilder.MatchRun) просит силу ИИ из командной строки: «-tugAi Strong».
                var args = System.Environment.GetCommandLineArgs();
                for (int a = 0; a < args.Length - 1; a++)
                    if (args[a] == "-tugAi" && System.Enum.TryParse<MapSession.Seat>("Ai" + args[a + 1], out var level))
                        for (int s = 1; s < MapSession.Seats.Count; s++) MapSession.Seats[s] = level;
            }
            Current = map;

            var ground = GameObject.Find("Поле");
            if (ground != null) ground.transform.localScale = new Vector3(map.Size / 10f, 1f, map.Size / 10f);

            var root = new GameObject(Loc.F("Карта — {0}", map.Name)).transform;
            foreach (var t in map.Things) Place(t.Prop, root, new Vector3(t.X, 0f, t.Z), t.Yaw, t.Scale);

            // Склеп — пол, стены, дверь на петле и трон (автор, 27.10: «у склепа не хватает стен и
            // возможности открывать дверь и пола»), префабом сборщика (TugSceneBuilder.BuildCrypt).
            var built = Resources.Load<GameObject>("MapProps/Crypt");
            _match = gameObject.AddComponent<TugMatch>();
            for (int i = 0; i < map.Crypts.Count; i++)
            {
                var c = map.Crypts[i];
                var at = new Vector3(c.X, 0f, c.Z);
                var rot = Quaternion.Euler(0f, c.Yaw, 0f);
                if (built == null)
                {
                    var bare = new GameObject(Loc.F("Склеп {0}", i + 1)).transform;
                    bare.SetParent(root, false);
                    bare.SetPositionAndRotation(at, rot);
                    Place("CryptGate", bare, at + rot * new Vector3(0f, 0f, 6f), c.Yaw, 1f);
                    Place("Throne", bare, at + rot * new Vector3(0f, 0f, -2f), c.Yaw, 1f);
                    _match.Crypts.Add(bare);
                    continue;
                }
                var crypt = Instantiate(built, at, rot, root);
                crypt.name = Loc.F("Склеп {0}", i + 1);
                _match.Crypts.Add(crypt.transform);
                foreach (var part in crypt.GetComponentsInChildren<Sturdy>())
                {
                    bool door = part.GetComponentInParent<CryptDoor>() != null;
                    part.Configure(door ? Loc.F("Дверь склепа {0}", i + 1) : Loc.F("Трон склепа {0}", i + 1),
                                   door ? DoorStrength : ThroneStrength, i);
                }
            }

            // Новая партия: верности и обиды — с нуля; голос игрока — его сторона (54-TUG.md).
            Allegiance.Forget();
            Hostility.Forget();
            Favor.Begin(Favor.StartPurse);
            Remains.Forget();
            Lords.Player = Loc.F("Греховод стороны {0}", Mathf.Max(0, MapSession.Seats.IndexOf(MapSession.Seat.Player)) + 1);

            // Подлокации с жителями — ничьими (Residents). Каждая партия — новые люди (автор, 28.10:
            // «каждый воин должен генерироваться по новой каждую игру»): номер партии растёт с каждой
            // игрой и входит в формулу — без жребия, и в сетевой партии хозяин передаст его всем.
            Match = PlayerPrefs.GetInt(MatchKey, 0) + 1;
            PlayerPrefs.SetInt(MatchKey, Match);
            int stamp = Wardrobe.Stamp(map.Name) ^ (Match * 7919);
            for (int i = 0; i < map.Locations.Count; i++)
            {
                var l = map.Locations[i];
                var home = new GameObject(Loc.F("Подлокация — {0}", Loc.T(l.Name)));
                home.transform.SetParent(root, false);
                home.transform.position = new Vector3(l.X, 0f, l.Z);
                home.AddComponent<Residents>().Configure(l.Kind, l.Radius, l.People, Mathf.Abs(stamp % 10000) * 10 + i * 100);
            }
            foreach (var s in map.Trails) Mark(root, Loc.T("Тропа охотников"), new Vector3(s.X, 0f, s.Z));
            foreach (var s in map.Events) Mark(root, Loc.F("Событие — {0}", s.Kind), new Vector3(s.X, 0f, s.Z));

            // Греховод игрока — в склепе первой стороны с местом «Игрок»: перед троном, лицом к
            // двери (54-TUG.md: «Греховоды появляются в склепах с тронами»). Наружу — открыть дверь.
            int seat = Mathf.Max(0, MapSession.Seats.IndexOf(MapSession.Seat.Player));
            if (seat < map.Crypts.Count)
            {
                var c = map.Crypts[seat];
                var entry = new GameObject("Появление Греховода");
                var inside = built != null ? new Vector3(0f, 0.12f, 1.5f) : new Vector3(0f, 0f, 9f);
                entry.transform.position = new Vector3(c.X, 0f, c.Z) + Quaternion.Euler(0f, c.Yaw, 0f) * inside;
                entry.AddComponent<Crypt.TestChamberEntry>();
            }
            _match.Begin(seat);

            // Охотники — с троп карты, раз в пять минут к каждому склепу (HunterWaves).
            var trails = new System.Collections.Generic.List<Vector3>();
            foreach (var s in map.Trails) trails.Add(new Vector3(s.X, 0f, s.Z));
            gameObject.AddComponent<HunterWaves>().Begin(trails);
            // Армия без дела остывает — копить её вечно нельзя (Idleness).
            gameObject.AddComponent<Idleness>();
        }

        private TugMatch _match;

        /// <summary>
        /// Греховоды-ИИ — в склепах мест «ИИ» лобби (шаг 3, 54-TUG.md). В Start: навмеш уже испечён
        /// (GroundNavMesh, порядок −900), и ноги встанут на него.
        /// </summary>
        void Start()
        {
            var map = Current;
            if (map == null || _match == null) return;
            for (int i = 0; i < map.Crypts.Count && i < MapSession.Seats.Count; i++)
            {
                var level = MapSession.Seats[i];
                if (level != MapSession.Seat.AiWeak && level != MapSession.Seat.AiMedium && level != MapSession.Seat.AiStrong) continue;
                var c = map.Crypts[i];
                var at = new Vector3(c.X, 0f, c.Z) + Quaternion.Euler(0f, c.Yaw, 0f) * new Vector3(0f, 0.12f, 1.5f);
                Rival(i, level, at, Quaternion.Euler(0f, c.Yaw, 0f));
            }
        }

        /// <summary>Тело Греховода-ИИ: скелет, как Греховод игрока; решает не душа, а его ум (RivalLord).</summary>
        public static void Rival(int side, MapSession.Seat level, Vector3 at, Quaternion facing)
        {
            string lord = Loc.F("Греховод стороны {0}", side + 1);
            var go = new GameObject(lord);
            if (UnityEngine.AI.NavMesh.SamplePosition(at, out var hit, 3f, UnityEngine.AI.NavMesh.AllAreas)) at = hit.position;
            go.transform.SetPositionAndRotation(at, facing);
            var warrior = go.AddComponent<Warrior>();
            var soul = new SoulData(lord, SinType.Pride, MoralType.Neutral, 3, 60f);
            warrior.Initialize(soul, ShellType.Skeleton, new RelationshipSystem(AOS.MemoryProcessor.Instance), true, Team.Neutral);
            var data = ShellLibrary.Get(ShellType.Skeleton);
            WarriorRig.Attach(go, data != null ? data.movementSpeed : 3.5f);
            WarriorLook.Build(go, ShellType.Skeleton, 1.9f, 1f, 1.2f);
            go.GetComponent<Damageable>()?.Initialize(RivalStrength, 0f);
            go.AddComponent<RivalLord>().Configure(lord, side, level);
            Debug.Log($"[ПАРТИЯ] {lord}: Греховод-ИИ ({level})");
        }

        /// <summary>Запас тела Греховода-ИИ — как у Греховода игрока на поле: убить можно, но не с одного удара.</summary>
        private const float RivalStrength = 300f;

        /// <summary>Ушли с поля — «Перетягивания» больше нет: приказы снова без имени, как в демо.</summary>
        void OnDestroy() => Lords.Player = null;

        /// <summary>Запас прочности трона и двери склепа. Дверь держит натиск, трон — партию.</summary>
        private const float ThroneStrength = 1500f;
        private const float DoorStrength = 400f;

        /// <summary>
        /// Одетая модель: из Resources/MapProps (тот же материал, что в лагере, — TugSceneBuilder.BuildMapProps),
        /// нет такой — голая из Props.
        /// </summary>
        public static GameObject Prefab(string prop)
        {
            var dressed = Resources.Load<GameObject>("MapProps/" + prop);
            return dressed != null ? dressed : Resources.Load<GameObject>("Props/" + prop);
        }

        /// <summary>Модель — как ставит сборщик сцен: поворот импорта сохраняется.</summary>
        private static void Place(string prop, Transform parent, Vector3 at, float yaw, float scale)
        {
            var prefab = Prefab(prop);
            if (prefab == null) { Debug.LogWarning($"[КАРТЫ] Нет модели «{prop}» в Resources/Props."); return; }
            var go = Instantiate(prefab, parent);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f) * prefab.transform.rotation);
            go.transform.localScale = prefab.transform.localScale * scale;
        }

        /// <summary>Невидимая метка места: по ней ставят жителей, охотников и события (шаги 1–4).</summary>
        private static void Mark(Transform parent, string name, Vector3 at)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
        }
    }
}
