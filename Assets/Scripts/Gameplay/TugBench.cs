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
        [SerializeField] private float _seconds = 90f;

        public const string Report = "Logs/stress-tug.txt";

        /// <summary>Замер окончен — прогон может выходить.</summary>
        public static bool Done { get; private set; }

        private RelationshipSystem _relations;
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
                    var at = centre + toEdge * (_reach + row * 1.8f) + across * ((col - 9.5f) * 1.6f);
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
                Line(Loc.F("{0,5:F0} с: живых {1}, кадр {2:F1} мс в среднем, худший {3:F1} мс", now - _spawnedAt, Alive(), _sum / _frames, _worst));
                _windowStart = now; _frames = 0; _sum = 0f; _worst = 0f;
            }

            if (now - _spawnedAt >= _seconds)
            {
                Line(Loc.F("ИТОГ: кадр {0:F1} мс в среднем ({1:F0} кадров/с), худший {2:F1} мс, живых в конце {3}", _allSum / Mathf.Max(1, _allFrames), 1000f * _allFrames / Mathf.Max(1f, _allSum), _allWorst, Alive()));
                Done = true;
            }
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
