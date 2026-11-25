// Assets/Scripts/Crypt/ShootingRange.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.Crypt
{
    /// <summary>
    /// Стрельбище полигона (автор, 25.11: «как мы можем тестировать фрагмент с лучниками — без лучников?»,
    /// «почему бы не сделать полигон?»). Ставит перед Греховодом мишени — врагов, которые стоят и не
    /// решают ничего: в схватке со своим (свой стоит между лучником и врагом — проверка, бьёт ли лучник
    /// навесом над головой), на ходу (упреждение, самонаведение) и дальнюю (22 м — только небесный лук).
    /// Лучники полигона — обычные воины из создателя (N): стреляют, когда так решит их душа.
    /// </summary>
    public static class ShootingRange
    {
        public static List<GameObject> PlaceTargets()
        {
            var made = new List<GameObject>();
            var hero = Object.FindFirstObjectByType<SinbinderPlayer>();
            Vector3 f = hero != null ? hero.transform.forward : Vector3.forward;
            f.y = 0f;
            f = f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
            Vector3 r = Vector3.Cross(Vector3.up, f);
            Vector3 c = hero != null ? hero.transform.position : Vector3.zero;
            var rel = new RelationshipSystem(AOS.MemoryProcessor.Instance);

            made.Add(Stand(Loc.T("Мишень"), Team.Enemy, Ground(c + f * 12f), -f, rel));
            made.Add(Stand(Loc.T("Свой в схватке"), Team.Player, Ground(c + f * 10.6f), f, rel));
            var walker = Stand(Loc.T("Мишень на ходу"), Team.Enemy, Ground(c + f * 14f - r * 4f), r, rel);
            walker.AddComponent<RangeWalker>().Between(Ground(c + f * 14f - r * 4f), Ground(c + f * 14f + r * 4f));
            made.Add(walker);
            made.Add(Stand(Loc.T("Дальняя мишень"), Team.Enemy, Ground(c + f * 22f + r * 2f), -f, rel));
            return made;
        }

        /// <summary>Воин, который стоит и не решает: тело, ноги, руки — без движка решений.</summary>
        public static GameObject Stand(string name, Team team, Vector3 at, Vector3 face, RelationshipSystem rel, Trade trade = Trade.Peasant)
        {
            var soul = new SoulData(name, SinType.Pride, MoralType.Neutral, 3, 30f);
            soul.SetTrade(trade);
            var go = new GameObject(name);
            go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(face));
            var w = go.AddComponent<Warrior>();
            w.Initialize(soul, ShellType.Skeleton, rel, false, team);
            WarriorRig.Attach(go);
            WarriorLook.Build(go, ShellType.Skeleton, 1.05f, 0.5f, 0.65f);
            var setup = Object.FindFirstObjectByType<AOS.AOSSceneSetup>();
            if (setup != null) setup.SetupWarrior(go);
            var dmg = go.GetComponent<Damageable>();
            if (CombatManager.Instance != null && dmg != null) CombatManager.Instance.UpdateTeam(dmg, team);
            var mind = go.GetComponent<AOS.AutoAttackAOS>();
            if (mind != null) mind.enabled = false;
            return go;
        }

        public static Vector3 Ground(Vector3 p)
            => NavMesh.SamplePosition(p, out var hit, 6f, NavMesh.AllAreas) ? hit.position : p;
    }

    /// <summary>Мишень ходит туда-обратно между двумя точками.</summary>
    public class RangeWalker : MonoBehaviour
    {
        private Vector3 _a, _b;
        private float _next;
        private NavMeshAgent _legs;
        private Damageable _self;

        public void Between(Vector3 a, Vector3 b) { _a = a; _b = b; }

        void Start()
        {
            _legs = GetComponent<NavMeshAgent>();
            _self = GetComponent<Damageable>();
        }

        void Update()
        {
            if (_legs == null || !_legs.isOnNavMesh || (_self != null && _self.IsDead) || Time.time < _next) return;
            _next = Time.time + 3f;
            _legs.SetDestination(Vector3.Distance(transform.position, _a) < 1f ? _b : _a);
        }
    }
}
