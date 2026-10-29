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
        private readonly Dictionary<Warrior, float> _ordered = new();
        private const float StayAfterOrder = 90f;

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
            if (_people > 0) Build();
            Debug.Log($"[КАРТЫ] {name}: жителей {_folk.Count}, построек {_built}");
        }

        private int _built;

        /// <summary>
        /// Строения очага (автор, 28.10: «не хватает строений и окружения для деревень»; модели — village.py):
        /// деревня — избы кругом за краем, где ходят жители, входом к очагу, у каждой поленница сбоку и угол
        /// плетня; колодец у середины. Поле — изб меньше, вместо колодца — поленницы. Бор — без изб: костёр и
        /// поленница. Всё по формуле от номера очага, без жребия. Ставится после выпечки навмеша, поэтому каждая
        /// постройка — препятствием, что вырезает себя из навмеша (сквозь избу не ходят).
        /// </summary>
        private void Build()
        {
            var centre = transform.position;
            var root = new GameObject("Строения");
            root.transform.SetParent(transform, false);

            if (_kind != "forest")
            {
                int houses = Mathf.Clamp(_people / (_kind == "field" ? 5 : 3), 2, 7);
                float ring = _radius + 4.5f;
                float a0 = _seed % 360;
                for (int i = 0; i < houses; i++)
                {
                    float a = (a0 + i * 360f / houses + 9f * ((i * 7) % 3 - 1)) * Mathf.Deg2Rad;
                    var at = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * ring;
                    var face = Quaternion.LookRotation(centre - at);      // входом — к очагу
                    Izba(root, at, face, _seed + i * 31);
                    // Поленница — у правого бока, плетень — уголком от левого.
                    var right = face * Vector3.right;
                    var back = face * Vector3.back;
                    Place(root, "Woodpile", at + right * 3.6f + back * 0.6f, face * Quaternion.Euler(0f, 90f, 0f), new Vector3(2.2f, 1.4f, 1f));
                    Place(root, "Wattle", at - right * 3.8f + back * 1.2f, face * Quaternion.Euler(0f, 90f, 0f), new Vector3(2f, 1.1f, 0.2f));
                    Place(root, "Wattle", at - right * 2.8f + back * 2.3f, face, new Vector3(2f, 1.1f, 0.2f));
                }
                if (_kind == "village")
                    Place(root, "Well", centre + new Vector3(Mathf.Cos(a0 * Mathf.Deg2Rad), 0f, Mathf.Sin(a0 * Mathf.Deg2Rad)) * 2.5f,
                          Quaternion.Euler(0f, a0, 0f), new Vector3(1.4f, 1.4f, 1.4f));
            }
            else
            {
                Place(root, "Campfire", centre, Quaternion.identity, Vector3.zero);
                Place(root, "Woodpile", centre + new Vector3(3f, 0f, 1.5f), Quaternion.Euler(0f, 30f, 0f), new Vector3(2.2f, 1.4f, 1f));
            }
        }

        /// <summary>
        /// Изба, в которую входят (IzbaInterior): стены с полом и крыша — отдельно, убранство своё; навмеш
        /// перепекается после стройки (GroundNavMesh.Rebake) — стены не пройти, в дверь — войти.
        /// </summary>
        private void Izba(GameObject root, Vector3 at, Quaternion face, int stamp)
        {
            var walls = MapLoader.Prefab("IzbaWalls");
            var roofP = MapLoader.Prefab("IzbaRoof");
            if (walls == null || roofP == null) { Place(root, "Izba", at, face, new Vector3(5.6f, 4f, 4.6f)); return; }
            if (NavMesh.SamplePosition(at, out var hit, 3f, NavMesh.AllAreas)) at.y = hit.position.y;
            var house = new GameObject("Изба");
            house.transform.SetParent(root.transform, false);
            house.transform.SetPositionAndRotation(at, face);
            var w = Instantiate(walls, house.transform);
            w.transform.localPosition = Vector3.zero; w.transform.localRotation = walls.transform.rotation; w.transform.localScale = walls.transform.localScale;
            var r = Instantiate(roofP, house.transform);
            r.transform.localPosition = Vector3.zero; r.transform.localRotation = roofP.transform.rotation; r.transform.localScale = roofP.transform.localScale;
            house.AddComponent<IzbaInterior>().Configure(stamp, r);
            GroundNavMesh.Rebake();
            _built++;
        }

        /// <summary>Поставить вещь из MapProps; size — коробка препятствия навмеша (ноль — без него).</summary>
        private void Place(GameObject root, string prop, Vector3 at, Quaternion facing, Vector3 size)
        {
            var p = MapLoader.Prefab(prop);
            if (p == null) return;
            if (NavMesh.SamplePosition(at, out var hit, 3f, NavMesh.AllAreas)) at.y = hit.position.y;
            var go = Instantiate(p, root.transform);
            go.transform.SetPositionAndRotation(at, facing * p.transform.rotation);
            go.transform.localScale = p.transform.localScale;
            if (size != Vector3.zero)
            {
                var block = new GameObject("Препятствие");
                block.transform.SetParent(go.transform.parent, false);
                block.transform.SetPositionAndRotation(at + Vector3.up * size.y * 0.5f, facing);
                var obstacle = block.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.size = size;
                obstacle.carving = true;
                obstacle.carveOnlyStationary = true;
            }
            _built++;
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

        /// <summary>
        /// Титулованный или легенда (Champions): тот же генератор, но душа старше и сильнее, и титул — с рождения.
        /// </summary>
        public Warrior Champion(int seed, Vector3 at, float strength, int level, string title)
        {
            _relations ??= new RelationshipSystem(AOS.MemoryProcessor.Instance);
            var present = new List<string>();
            foreach (var r in FindObjectsByType<Residents>(FindObjectsSortMode.None))
                foreach (var w in r._folk) if (w != null) present.Add(w.DisplayName);
            var k = WarriorGenerator.Make(seed, SoulOrigin.Sortie, present);
            k.Strength = strength;
            if (NavMesh.SamplePosition(at, out var hit, 4f, NavMesh.AllAreas)) at = hit.position;
            var made = Make(k, at, FindFirstObjectByType<AOS.AOSSceneSetup>(), level);
            if (made == null) return null;
            made.Soul.Remember(title);
            _folk.Add(made);
            return made;
        }

        private Warrior Make(Sketch k, Vector3 at, AOS.AOSSceneSetup setup, int level = 0)
        {
            var go = new GameObject(k.Name);
            go.transform.SetParent(transform, true);
            go.transform.position = at;
            go.transform.rotation = Quaternion.LookRotation((transform.position - at).sqrMagnitude > 0.01f ? transform.position - at : Vector3.forward);

            var warrior = go.AddComponent<Warrior>();
            var soul = new SoulData(k.Name, k.Sin, k.Moral, level > 0 ? level : 1 + k.Number % 3, k.Strength, k.Seed);
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
                if (w == null || w.IsDead || w.HasCommand) { if (w != null && w.HasCommand) _ordered[w] = Time.time; continue; }
                // Чей-то верный или недавно с приказом — стоит, где его поставили, а не бежит к очагу
                // (автор, 28.10: «воины быстро возвращаются на места — секунд через десять»).
                if (_ordered.TryGetValue(w, out var told) && Time.time - told < StayAfterOrder) continue;
                string lord = Allegiance.Dearest(w.Id);
                if (lord != null && Allegiance.Of(w.Id, w.Loyalty, lord) >= Hostility.Follows) continue;
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
