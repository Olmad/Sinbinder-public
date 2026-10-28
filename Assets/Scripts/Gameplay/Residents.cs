// Assets/Scripts/Gameplay/Residents.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Жители подлокации «Перетягивания» (54-TUG.md; автор, 27.10: «воины без стороны, живут в
    /// подлокациях своей жизнью»). Ничьи (<see cref="Team.Neutral"/>): слушаются того, кто приказал,
    /// по верности к нему, кого бить — решают сами (<see cref="Hostility"/>).
    ///
    /// Люди — из генератора облачной (<see cref="WarriorGenerator"/>): номер — от имени карты и
    /// подлокации, без жребия; одна и та же карта — одни и те же люди. Откуда душа — по месту:
    /// деревня — обозники и послушники, лес — павшие в бою, поле — отдавшие душу сами.
    ///
    /// Своя жизнь: без приказа и без врага рядом ходит у очага, от места к месту по формуле;
    /// увели — без приказа возвращается домой.
    /// </summary>
    public class Residents : MonoBehaviour
    {
        private string _kind = "village";
        private float _radius = 18f;
        private int _people = 12;
        private int _seed;

        private readonly List<Warrior> _folk = new();
        private RelationshipSystem _relations;
        private float _nextWalk;

        public IReadOnlyList<Warrior> Folk => _folk;

        public void Configure(string kind, float radius, int people, int seed)
        {
            _kind = kind;
            _radius = Mathf.Max(6f, radius);
            _people = Mathf.Clamp(people, 0, 60);
            _seed = seed;
        }

        // После выпечки навмеша: GroundNavMesh печёт в своём Start, порядок −900.
        void Start()
        {
            _relations = new RelationshipSystem(AOS.MemoryProcessor.Instance);
            var origin = _kind switch
            {
                "forest" => SoulOrigin.Harvest,
                "field" => SoulOrigin.Pact,
                _ => SoulOrigin.Sortie,
            };

            var present = new List<string>();
            foreach (var r in FindObjectsByType<Residents>(FindObjectsSortMode.None))
                foreach (var w in r._folk) if (w != null) present.Add(w.DisplayName);

            var setup = FindFirstObjectByType<AOS.AOSSceneSetup>();
            for (int i = 0; i < _people; i++)
            {
                var k = WarriorGenerator.Make(_seed + i, origin, present);
                present.Add(k.Name);
                var w = Make(k, Home(i), setup);
                if (w != null) _folk.Add(w);
            }
            Debug.Log($"[КАРТЫ] {name}: жителей {_folk.Count}");
        }

        /// <summary>
        /// Новый человек у этого места — пленник, беженец, дезертир экспедиции (MapEvent). Сгенерирован
        /// так же, как жители: по номеру, без жребия.
        /// </summary>
        public Warrior Newcomer(int seed, Vector3 at)
        {
            _relations ??= new RelationshipSystem(AOS.MemoryProcessor.Instance);
            var present = new List<string>();
            foreach (var r in FindObjectsByType<Residents>(FindObjectsSortMode.None))
                foreach (var w in r._folk) if (w != null) present.Add(w.DisplayName);
            var k = WarriorGenerator.Make(seed, SoulOrigin.Sortie, present);
            if (NavMesh.SamplePosition(at, out var hit, 4f, NavMesh.AllAreas)) at = hit.position;
            var made = Make(k, at, FindFirstObjectByType<AOS.AOSSceneSetup>());
            if (made != null) _folk.Add(made);
            return made;
        }

        /// <summary>Место у очага: кольцами вокруг середины, по номеру.</summary>
        private Vector3 Home(int i)
        {
            float a = (i * 137.5f + _seed % 360) * Mathf.Deg2Rad;
            float r = _radius * (0.25f + 0.6f * ((i * 0.618f) % 1f));
            var at = transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
            return NavMesh.SamplePosition(at, out var hit, 4f, NavMesh.AllAreas) ? hit.position : at;
        }

        private Warrior Make(Sketch k, Vector3 at, AOS.AOSSceneSetup setup)
        {
            var go = new GameObject(k.Name);
            go.transform.SetParent(transform, true);
            go.transform.position = at;
            go.transform.rotation = Quaternion.LookRotation((transform.position - at).sqrMagnitude > 0.01f ? transform.position - at : Vector3.forward);

            var warrior = go.AddComponent<Warrior>();
            var soul = new SoulData(k.Name, k.Sin, k.Moral, 1 + k.Number % 3, k.Strength, k.Seed);
            soul.Set(k.Second, k.Strength * 0.6f);
            warrior.Initialize(soul, k.Shell, _relations, false, Team.Neutral);

            var data = ShellLibrary.Get(k.Shell);
            WarriorRig.Attach(go, data != null ? data.movementSpeed : 3.5f);
            float height = 1.55f + k.Number * 7 % 30 / 100f;
            WarriorLook.Build(go, k.Shell, height, 1f, height * 0.62f);
            if (setup != null) setup.SetupWarrior(go);
            Signature.Give(k.Name, k.Why, k.First, k.Death);
            return warrior;
        }

        void Update()
        {
            if (Time.time < _nextWalk) return;
            _nextWalk = Time.time + 1.5f;

            for (int i = 0; i < _folk.Count; i++)
            {
                var w = _folk[i];
                if (w == null || w.IsDead || w.HasCommand) continue;
                var agent = w.GetComponent<NavMeshAgent>();
                if (agent == null || !agent.enabled || !agent.isOnNavMesh) continue;
                var dmg = w.GetComponent<Damageable>();
                if (dmg != null && CombatManager.Instance != null
                    && CombatManager.Instance.GetClosestEnemy(w.transform.position, 12f, w.gameObject) != null) continue;

                float away = CampFocus.GroundDistance(w.transform.position, transform.position);
                bool idle = !agent.hasPath || agent.remainingDistance < 0.5f;
                // Свой час у каждого: раз в полминуты с небольшим — к новому месту у очага.
                bool turn = ((int)(Time.time / 6f) + i) % 5 == 0;
                if (away > _radius * 1.5f || (idle && turn))
                    agent.SetDestination(Home(i + (int)(Time.time / 30f) * 7));
            }
        }
    }
}
