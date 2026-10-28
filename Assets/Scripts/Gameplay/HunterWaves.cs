// Assets/Scripts/Gameplay/HunterWaves.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Охотники «Перетягивания» (54-TUG.md; автор, 27.10: «каждые 5 минут на каждый склеп выходит
    /// отряд, каждый следующий сильнее; выходят с троп на краю карты, ближе к середине края — максимально
    /// далеко от всех склепов»). Волна — к каждому склепу, чей трон цел: отряд с ближней к нему тропы, людей
    /// на одного больше прежнего, и крепче. Дошли — ломают дверь, за ней трон (<see cref="HunterSiege"/>);
    /// рядом есть кого бить — решает их голос, как у всех.
    ///
    /// Охотники — враги всем (Team.Enemy): и Греховодам, и ничьим. Убитый охотник — монеты тому Греховоду,
    /// чей человек его убил, и тело с душой — для алтаря: волны кормят тех, кто их выдержал.
    /// </summary>
    public class HunterWaves : MonoBehaviour
    {
        public static HunterWaves Instance { get; private set; }

        public const float Period = 300f;
        private const int Bounty = 2;

        public int Wave { get; private set; }
        public float Next { get; private set; }

        private readonly List<Vector3> _trails = new();
        private RelationshipSystem _relations;
        private AOS.AOSSceneSetup _setup;
        private CombatManager _subscribed;
        private int _born;

        void Awake() => Instance = this;
        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_subscribed != null) _subscribed.OnAnyDeath -= OnDeath;
        }

        public void Begin(IEnumerable<Vector3> trails)
        {
            _trails.Clear();
            _trails.AddRange(trails);
            Wave = 0;
            Next = Time.time + Period;
        }

        void Update()
        {
            if (_subscribed == null && CombatManager.Instance != null)
            {
                _subscribed = CombatManager.Instance;
                _subscribed.OnAnyDeath += OnDeath;
            }
            if (TugMatch.Over || Time.time < Next) return;
            Release();
        }

        /// <summary>Выпустить волну сейчас (по часам; прогон зовёт сразу).</summary>
        public int Release()
        {
            var match = TugMatch.Instance;
            Next = Time.time + Period;
            if (match == null || _trails.Count == 0) return 0;
            Wave++;
            _relations ??= new RelationshipSystem(AOS.MemoryProcessor.Instance);
            if (_setup == null) _setup = FindFirstObjectByType<AOS.AOSSceneSetup>();

            int sent = 0;
            for (int side = 0; side < match.Crypts.Count; side++)
            {
                if (match.Out(side) || match.Crypts[side] == null) continue;
                var crypt = match.Crypts[side];
                var trail = _trails[0];
                foreach (var t in _trails)
                    if (CampFocus.GroundDistance(t, crypt.position) < CampFocus.GroundDistance(trail, crypt.position)) trail = t;

                var gate = crypt.position + crypt.forward * 10f;
                var across = Vector3.Cross(Vector3.up, (gate - trail).normalized);
                int count = 2 + Wave;
                for (int i = 0; i < count; i++)
                    if (Hunter(trail + across * ((i - (count - 1) * 0.5f) * 1.6f), gate, side) != null) sent++;
            }
            Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(Loc.F("С троп вышли охотники — волна {0}.", Wave));
            Debug.Log($"[ПАРТИЯ] охотники, волна {Wave}: {sent}");
            return sent;
        }

        private Warrior Hunter(Vector3 at, Vector3 gate, int side)
        {
            if (UnityEngine.AI.NavMesh.SamplePosition(at, out var hit, 6f, UnityEngine.AI.NavMesh.AllAreas)) at = hit.position;
            int n = _born++;
            string name = HunterSquadSpawner.HunterName(n);
            var go = new GameObject(name);
            go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(gate - at));

            var warrior = go.AddComponent<Warrior>();
            var soul = new SoulData(name, (SinType)(n % 7), MoralType.Pious, 1 + Wave / 2, 40f + Wave * 8f);
            warrior.Initialize(soul, ShellType.Living, _relations, n % 6 == 0, Team.Enemy);
            var data = ShellLibrary.Get(ShellType.Living);
            WarriorRig.Attach(go, data != null ? data.movementSpeed : 3.5f);
            WarriorLook.Build(go, ShellType.Living, 1.7f + n % 5 * 0.03f, 1f, 1.05f);

            // Каждая волна крепче прежней: запас тела — на четверть больше за волну.
            var body = go.GetComponent<Damageable>();
            if (body != null) body.Initialize(body.MaxHP * (1f + 0.25f * (Wave - 1)), 0f);

            go.AddComponent<HunterGoal>().Configure(HunterGoal.Aim.CampCentre, gate);
            go.AddComponent<HunterSiege>().Configure(side);
            if (_setup != null) _setup.SetupWarrior(go);
            return warrior;
        }

        /// <summary>Убит охотник — монеты Греховоду, чей человек убил.</summary>
        private static void OnDeath(Damageable killed, GameObject killer)
        {
            if (!Lords.Active || killed == null || killer == null || killed.GetComponent<HunterSiege>() == null) return;
            var by = killer.GetComponent<Warrior>();
            if (by == null) return;
            string lord = Lords.Of(by) ?? (by.HasCommand ? by.Command.Lord : null) ?? Allegiance.Dearest(by.Id);
            if (string.IsNullOrEmpty(lord)) return;
            Favor.AddCoins(lord, Bounty);
        }
    }

    /// <summary>
    /// Охотник у склепа: дошёл (HunterGoal), рядом бить некого — ломает дверь, а за ней трон.
    /// Тем же уроном и темпом, что тела (AutoAttack): у вещи нет ни защиты, ни сдачи.
    /// </summary>
    public class HunterSiege : MonoBehaviour
    {
        private int _side;
        private float _ready;
        private const float Beat = 1.3f;
        private const float Busy = 8f;

        public int Side => _side;

        public void Configure(int side) => _side = side;

        void Update()
        {
            var me = GetComponent<Warrior>();
            if (me == null || me.IsDead || TugMatch.Instance == null) return;
            var manager = CombatManager.Instance;
            if (manager != null && manager.GetClosestEnemy(transform.position, Busy, gameObject) != null) return;

            var (door, throne) = TugMatch.Instance.PartsOf(_side);
            var goal = GetComponent<HunterGoal>();
            if (goal != null && goal.TryGet(transform.position, out _)) return;   // ещё идёт к склепу

            Sturdy target = null;
            if (door != null && !door.Open) target = door.Leaf.GetComponent<Sturdy>();
            if (target == null || target.Broken) target = throne;
            if (target == null || target.Broken) return;

            var hit = target.GetComponentInChildren<Collider>();
            var near = hit != null ? hit.ClosestPoint(transform.position) : target.transform.position;
            var auto = GetComponent<AutoAttack>();
            float range = (auto != null ? auto.AttackRange : 2f) + 0.4f;
            var legs = GetComponent<UnitMover>();
            if (Vector3.Distance(new Vector3(transform.position.x, near.y, transform.position.z), near) > range)
            {
                legs?.CommandMove(near);
                return;
            }
            legs?.Stop();
            if (Time.time < _ready) return;
            _ready = Time.time + Beat;
            target.TakeDamage(auto != null ? auto.AttackDamage : 6f, me);
            GetComponent<WarriorAnimation>()?.Struck();
        }
    }
}
