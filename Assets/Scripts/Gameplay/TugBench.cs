// Assets/Scripts/Gameplay/TugBench.cs
// Перевод: текст через Loc
using System.IO;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Нагрузочный стенд «Перетягивания каната» (автор, 27.10: «для теста нужно сделать
    /// максимально большую карту… чтобы проверить мощности», «по 100 на сторону.
    /// Я думаю о картах на 2 и на 4»). Ставит стороны по краям поля, по сотне в каждой,
    /// и ведёт их к середине — и пишет, сколько стоит кадр, пока они сходятся и бьются.
    ///
    /// Воины — настоящие, тем же путём, что охотники (<see cref="HunterSquadSpawner"/>):
    /// душа, тело, агент, движок решений. Иначе замер мерил бы не игру.
    ///
    /// Команд в движке две (<see cref="Team"/>), поэтому четыре стороны — двое на двое:
    /// север и юг за одних, запад и восток за других. Для замера мощности этого хватает;
    /// четыре враждующие стороны — дело самого режима.
    ///
    /// Ничего случайного: душа, тело и место — по номеру воина.
    /// </summary>
    public class TugBench : MonoBehaviour
    {
        [SerializeField] private int _sides = 2;
        [SerializeField] private int _perSide = 100;
        [Tooltip("Расстояние от середины до строя стороны, м.")]
        [SerializeField] private float _reach = 60f;
        [Tooltip("Сколько секунд мерить после появления.")]
        [SerializeField] private float _seconds = 85f;

        public const string Report = "Logs/stress-tug.txt";

        /// <summary>Замер окончен — прогон может выходить.</summary>
        public static bool Done { get; private set; }

        private RelationshipSystem _relations;
        private AOS.AOSSceneSetup _setup;
        private float _spawnedAt = -1f;
        private float _windowStart;
        private int _frames;
        private float _sum, _worst;
        private float _allSum, _allWorst;
        private int _allFrames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Done = false;

        void Start()
        {
            Done = false;
            Cost.On = true;
            Directory.CreateDirectory("Logs");
            File.WriteAllText(Report, Loc.F("=== НАГРУЗКА: сторон {0}, по {1} ===", _sides, _perSide) + "\n");
            // Память отношений готова после всех Awake (как в BattleTest).
            Invoke(nameof(Spawn), 0.5f);
        }

        private static readonly ShellType[] Shells =
            { ShellType.Skeleton, ShellType.Zombie, ShellType.Living, ShellType.Skeleton, ShellType.Golem, ShellType.Ghost };

        private void Spawn()
        {
            _relations = new RelationshipSystem(AOS.MemoryProcessor.Instance);
            // Мерить, как играет игрок: в редакторе открыта ступень разработчика, и каждое
            // решение каждого воина писалось бы в журнал — в собранной игре этого нет.
            Transparency.SetDeveloper(false);
            Transparency.Set(Transparency.Default);
            var centre = transform.position;

            for (int s = 0; s < _sides; s++)
            {
                // Стороны по кругу: на двоих — юг и север, на четверых — ещё запад и восток.
                float yaw = s * 360f / _sides;
                var toEdge = Quaternion.Euler(0f, yaw, 0f) * Vector3.back;
                var across = Vector3.Cross(Vector3.up, toEdge);
                var team = s % 2 == 0 ? Team.Player : Team.Enemy;
                var side = new GameObject(Loc.F("Сторона {0}", s + 1)).transform;
                side.SetParent(transform, false);

                for (int i = 0; i < _perSide; i++)
                {
                    int row = i / 20, col = i % 20;
                    var at = centre + toEdge * ((Probe ? 300f : _reach) + row * 1.8f) + across * ((col - 9.5f) * 1.6f);
                    Make(side, s, i, at, team, centre);
                }
            }

            _spawnedAt = _windowStart = Time.realtimeSinceStartup;
            Line(Loc.F("появились: {0} воинов", _sides * _perSide));
        }

        private void Make(Transform parent, int side, int i, Vector3 at, Team team, Vector3 centre)
        {
            int n = side * 1000 + i;
            string name = Loc.F("Сторона {0} — {1}", side + 1, i + 1);
            var shell = Shells[n % Shells.Length];

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            go.transform.rotation = Quaternion.LookRotation(centre - at);

            var warrior = go.AddComponent<Warrior>();
            if (team == Team.Enemy) go.AddComponent<HunterGoal>().Configure(HunterGoal.Aim.CampCentre, centre);

            var soul = new SoulData(name, (SinType)(n % 7), (MoralType)(n / 7 % 3), 1 + n % 3, 40f + n * 13 % 50);
            warrior.Initialize(soul, shell, _relations, i == 0, team);

            var data = ShellLibrary.Get(shell);
            WarriorRig.Attach(go, data != null ? data.movementSpeed : 3.5f);
            float height = 1.55f + n * 7 % 30 / 100f;
            WarriorLook.Build(go, shell, height, 1f, height * 0.62f);

            // Поздно рождённых движок решений не подхватит сам (AOSSceneSetup — в своём Start).
            if (_setup == null) _setup = Object.FindFirstObjectByType<AOS.AOSSceneSetup>();
            if (_setup != null) _setup.SetupWarrior(go);

            if (team == Team.Player) warrior.IssueCommand(CommandKind.AttackMove, centre);
        }

        void Update()
        {
            if (_spawnedAt < 0f || Done) return;

            float dt = Time.unscaledDeltaTime * 1000f;
            _frames++; _sum += dt; _worst = Mathf.Max(_worst, dt);
            _allFrames++; _allSum += dt; _allWorst = Mathf.Max(_allWorst, dt);

            float now = Time.realtimeSinceStartup;
            if (now - _windowStart >= 5f)
            {
                Line(Loc.F("{0,5:F0} с [{4}]: живых {1}, кадр {2:F1} мс в среднем, худший {3:F1} мс", now - _spawnedAt, Alive(), _sum / _frames, _worst, Stage(now - _spawnedAt)));
                Line(Minds());
                Line(Loc.F("      цена: {0}", Cost.Drain()));
                Line(Loc.F("      движок: {0}", Engine()));
                _windowStart = now; _frames = 0; _sum = 0f; _worst = 0f;
            }

            if (Probe) { ProbeStep(now - _spawnedAt); return; }
            Strip(now - _spawnedAt);

            if (now - _spawnedAt >= _seconds)
            {
                Line(Loc.F("ИТОГ: кадр {0:F1} мс в среднем ({1:F0} кадров/с), худший {2:F1} мс, живых в конце {3}", _allSum / Mathf.Max(1, _allFrames), 1000f * _allFrames / Mathf.Max(1f, _allSum), _allWorst, Alive()));
                Done = true;
                Cost.On = false;
            }
        }

        // ── Раскладка кадра: первые 45 с — бой целиком, дальше по 10 с снимаем по слою.
        // Разница соседних окон — цена слоя. Слои снимаются насовсем: итог ниже — уже не игра.

        private int _stripped;

        private static string Stage(float t) => t < 45f ? Loc.T("всё") : t < 55f ? Loc.T("без теней") : t < 65f ? Loc.T("без тел")
                                             : t < 75f ? Loc.T("без аниматоров") : Loc.T("без решений");

        private void Strip(float t)
        {
            int want = t < 45f ? 0 : t < 55f ? 1 : t < 65f ? 2 : t < 75f ? 3 : 4;
            while (_stripped < want)
            {
                _stripped++;
                switch (_stripped)
                {
                    case 1:
                        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) l.shadows = LightShadows.None;
                        break;
                    case 2:
                        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
                        break;
                    case 3:
                        foreach (var a in GetComponentsInChildren<Animator>()) a.enabled = false;
                        break;
                    case 4:
                        foreach (var m in GetComponentsInChildren<AOS.AutoAttackAOS>()) m.enabled = false;
                        break;
                }
                Line(Loc.F("— снято: {0}", Stage(t)));
            }
        }
        /// <summary>Что решили и где стоят: решения по числу и среднее расстояние до середины.</summary>
        private string Minds()
        {
            var counts = new System.Collections.Generic.SortedDictionary<string, int>();
            float far = 0f; int n = 0, ordered = 0;
            foreach (var w in GetComponentsInChildren<Warrior>())
            {
                if (w.IsDead) continue;
                var mind = w.GetComponent<AOS.AOSWarriorWrapper>();
                string k = mind != null ? mind.LastDecision.ToString() : "—";
                counts[k] = counts.TryGetValue(k, out var c) ? c + 1 : 1;
                far += Vector3.Distance(w.transform.position, transform.position); n++;
                if (w.HasCommand) ordered++;
            }
            var parts = new System.Collections.Generic.List<string>();
            foreach (var kv in counts) parts.Add(kv.Key + " " + kv.Value);
            return Loc.F("      решения: {0}; с приказом {1}; до середины в среднем {2:F0} м",
                         string.Join(", ", parts), ordered, n > 0 ? far / n : 0f);
        }

        // Счётчики самого Unity — то, чего не видят наши (Cost): навигация, физика,
        // анимация, отрисовка. Нет такого счётчика в этой версии — показывает ноль.
        private static readonly string[] Markers =
        {
            "BehaviourUpdate", "LateBehaviourUpdate", "NavMeshManager", "Physics.Simulate",
            "PreLateUpdate.DirectorUpdateAnimationBegin", "PreLateUpdate.DirectorUpdateAnimationEnd",
            "MeshSkinning.Update", "RenderPipelineManager.DoRenderLoop_Internal()",
            "Canvas.SendWillRenderCanvases", "Gfx.WaitForPresentOnGfxThread",
        };

        private readonly System.Collections.Generic.List<Unity.Profiling.ProfilerRecorder> _recorders = new();

        private void OnEnable()
        {
            foreach (var m in Markers)
                _recorders.Add(Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Internal, m, 400));
        }

        private void OnDisable()
        {
            foreach (var r in _recorders) r.Dispose();
            _recorders.Clear();
        }

        /// <summary>Сколько за последние кадры окна ушло на каждый раздел движка, мс на кадр.</summary>
        private string Engine()
        {
            var parts = new System.Collections.Generic.List<string>();
            for (int i = 0; i < _recorders.Count; i++)
            {
                var r = _recorders[i];
                if (!r.Valid || r.Count == 0) continue;
                double sum = 0;
                int n = r.Count;
                for (int k = 0; k < n; k++) sum += r.GetSample(k).Value;
                double ms = sum / n / 1e6;
                if (ms >= 0.5) parts.Add(Loc.F("{0} {1:F1}", Markers[i], ms));
            }
            return string.Join("; ", parts);
        }
        // ── Проба (-tugProbe): по очереди выключаем каждый вид скрипта на всех воинах
        // и смотрим, на сколько легче стал BehaviourUpdate. Армии стоят далеко — не сходятся.

        private static bool Probe => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-tugProbe") >= 0;

        private System.Collections.Generic.List<System.Type> _kinds;
        private int _kind = -1;
        private float _probeFrom;
        private double _probeSum;
        private int _probeFrames;
        private double _was = -1;

        private void ProbeStep(float t)
        {
            var update = _recorders.Count > 0 ? _recorders[0] : default;
            if (update.Valid && update.Count > 0) { _probeSum += update.LastValue / 1e6; _probeFrames++; }

            if (t < 6f) return;
            if (_kinds == null)
            {
                _kinds = new System.Collections.Generic.List<System.Type>();
                foreach (var b in GetComponentsInChildren<MonoBehaviour>())
                    if (b != null && !(b is TugBench) && !_kinds.Contains(b.GetType())) _kinds.Add(b.GetType());
                _probeFrom = t; _probeSum = 0; _probeFrames = 0;
                return;
            }
            if (t - _probeFrom < 4f) return;

            double ms = _probeFrames > 0 ? _probeSum / _probeFrames : 0;
            string what = _kind < 0 ? Loc.T("всё включено") : Loc.F("без {0}", _kinds[_kind].FullName);
            Line(Loc.F("  проба: {0} — скрипты {1:F1} мс/кадр{2}", what, ms,
                       _was >= 0 ? Loc.F(" (−{0:F1})", _was - ms) : ""));
            _was = ms;

            _kind++;
            if (_kind >= _kinds.Count) { Done = true; Cost.On = false; return; }
            foreach (var b in GetComponentsInChildren<MonoBehaviour>())
                if (b != null && b.GetType() == _kinds[_kind]) b.enabled = false;
            _probeFrom = t; _probeSum = 0; _probeFrames = 0;
        }
        private int Alive()
        {
            int n = 0;
            foreach (var w in GetComponentsInChildren<Warrior>()) if (!w.IsDead) n++;
            return n;
        }

        private static void Line(string text)
        {
            Debug.Log("[НАГРУЗКА] " + text);
            File.AppendAllText(Report, text + "\n");
        }
    }
}
