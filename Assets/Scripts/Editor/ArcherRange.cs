// Assets/Scripts/Editor/ArcherRange.cs
// Стрельбище для верстака (автор, 25.11: «как мы можем тестировать фрагмент с лучниками — без лучников?
// нам нужны лучники!» и «лук с повышенной дальностью и самонаводящимися стрелами»).
// call Sinbinder.EditorTools.ArcherRange.Stage — в идущей игре ставит перед Греховодом:
//   лучника с обычным луком (дуга, 11 м) и «небесного» (взлёт — на высоте к цели — отвесно вниз, 22 м);
//   мишень у свалки (рядом с ней свой — проверка, задевает ли стрела своих), мишень на ходу, дальнюю мишень.
// Решения у всех выключены: проверяется стрела, а не душа. call ...ArcherRange.Report — счёт.
using System.Collections.Generic;
using System.Text;
using Sinbinder.Core;
using Sinbinder.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Sinbinder.EditorTools
{
    public static class ArcherRange
    {
        private static AutoAttack _bow, _sky;
        private static readonly List<Damageable> Targets = new();
        private static readonly List<Damageable> Mates = new();
        private static NavMeshAgent _walker;
        private static Vector3 _walkA, _walkB;
        private static double _nextTurn;
        private static Vector3 _centre;

        public static string Stage()
        {
            if (!Application.isPlaying) return "нужна идущая игра (play)";
            var hero = Object.FindFirstObjectByType<SinbinderPlayer>();
            Vector3 f = hero != null ? hero.transform.forward : Vector3.forward;
            f.y = 0f;
            f = f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
            Vector3 r = Vector3.Cross(Vector3.up, f);
            Vector3 c = Ground((hero != null ? hero.transform.position : Vector3.zero) + f * 3f);
            _centre = c;
            var rel = new RelationshipSystem(AOS.MemoryProcessor.Instance);
            Targets.Clear();
            Mates.Clear();

            _bow = Make("Лучник стрельбища", Trade.Archer, Team.Player, c, f, rel).GetComponent<AutoAttack>();
            _sky = Make("Небесный лучник", Trade.Archer, Team.Player, Ground(c + r * 3f), f, rel).GetComponent<AutoAttack>();
            if (_sky != null) _sky.SkyBow = true;
            Targets.Add(Make("Мишень у свалки", Trade.Peasant, Team.Enemy, Ground(c + f * 9f), -f, rel).GetComponent<Damageable>());
            Mates.Add(Make("Свой перед лучником", Trade.Peasant, Team.Player, Ground(c + f * 7.4f), f, rel).GetComponent<Damageable>());
            Mates.Add(Make("Свой у мишени", Trade.Peasant, Team.Player, Ground(c + f * 9f + r * 0.6f), -f, rel).GetComponent<Damageable>());
            var walker = Make("Мишень на ходу", Trade.Peasant, Team.Enemy, Ground(c + f * 10f - r * 4f), r, rel);
            Targets.Add(walker.GetComponent<Damageable>());
            Targets.Add(Make("Дальняя мишень", Trade.Peasant, Team.Enemy, Ground(c + f * 19f + r * 3f), -f, rel).GetComponent<Damageable>());
            _walker = walker.GetComponent<NavMeshAgent>();
            _walkA = Ground(c + f * 10f - r * 4f);
            _walkB = Ground(c + f * 10f + r * 4f);
            _nextTurn = 0;

            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            return $"стрельбище у {c.x:0.0},{c.y:0.0},{c.z:0.0}; вперёд {f.x:0.00},{f.z:0.00}; лучников {(_bow != null ? 1 : 0) + (_sky != null ? 1 : 0)}, мишеней {Targets.Count}";
        }

        /// <summary>Приказ лучнику полигона «бить» ближайшую живую мишень — как ПКМ игрока по врагу.</summary>
        public static string Order()
        {
            if (_bow == null) return "лучника нет";
            var aim = Targets.Find(d => d != null && !d.IsDead);
            if (aim == null) return "мишеней нет";
            _bow.GetComponent<Warrior>().IssueCommand(CommandKind.Attack, aim.transform.position, aim.gameObject);
            return "приказ: бить " + aim.name;
        }

        public static string Report()
        {
            var sb = new StringBuilder();
            foreach (var a in new[] { _bow, _sky })
                if (a != null)
                    sb.Append($"{a.name}: выпущено {a.Shots}, во врага {a.Hits}, в своих {a.FriendlyHits}, мимо {a.Misses}; ");
            foreach (var d in Targets) if (d != null) sb.Append($"{d.name} {(d.IsDead ? "пал" : "жив")}; ");
            foreach (var d in Mates) if (d != null) sb.Append($"{d.name} {(d.IsDead ? "пал" : "жив")}; ");
            sb.Append($"центр {_centre.x:0.0},{_centre.y:0.0},{_centre.z:0.0}");
            return sb.ToString();
        }

        private static void Tick()
        {
            if (!Application.isPlaying) { EditorApplication.update -= Tick; return; }
            Shoot(_bow);
            Shoot(_sky);
            if (_walker != null && _walker.isOnNavMesh && EditorApplication.timeSinceStartup >= _nextTurn)
            {
                _nextTurn = EditorApplication.timeSinceStartup + 3.0;
                _walker.SetDestination(Vector3.Distance(_walker.transform.position, _walkA) < 1f ? _walkB : _walkA);
            }
        }

        /// <summary>Ближайшая живая мишень в досягаемости — по ней (темп держит сам AutoAttack).</summary>
        private static void Shoot(AutoAttack bow)
        {
            if (bow == null) return;
            Damageable best = null;
            float bestD = bow.Reach;
            foreach (var d in Targets)
            {
                if (d == null || d.IsDead) continue;
                float dist = Vector3.Distance(bow.transform.position, d.transform.position);
                // Небесный бьёт дальнюю первой — затем ту, что на ходу: самонаведение видно на ней.
                if (bow.SkyBow) dist = d.name == "Дальняя мишень" ? 0f : d.name == "Мишень на ходу" ? 1f : dist;
                if (dist <= bestD) { bestD = dist; best = d; }
            }
            if (best != null) bow.ForceAttack(best);
        }

        private static Warrior Make(string name, Trade trade, Team team, Vector3 at, Vector3 face, RelationshipSystem rel)
            => Sinbinder.Crypt.ShootingRange.Stand(name, team, at, face, rel, trade).GetComponent<Warrior>();

        /// <summary>
        /// Полигон (Crypt_Test): мишени из строки создателя и лучник, который решает сам — как в игре.
        /// sky — небесный лук. Счёт — Report.
        /// </summary>
        public static string Polygon(bool sky)
        {
            if (!Application.isPlaying) return "нужна идущая игра (play)";
            var spawner = Object.FindFirstObjectByType<PrologueCampSpawner>();
            var hero = Object.FindFirstObjectByType<SinbinderPlayer>();
            if (spawner == null || hero == null) return "нет отряда или Греховода — это не полигон";
            EditorApplication.update -= Tick;
            Targets.Clear();
            Mates.Clear();
            foreach (var go in Sinbinder.Crypt.ShootingRange.PlaceTargets())
            {
                var d = go.GetComponent<Damageable>();
                if (d == null) continue;
                if (go.GetComponent<Warrior>().Team == Team.Enemy) Targets.Add(d); else Mates.Add(d);
            }
            Vector3 f = hero.transform.forward; f.y = 0f; f.Normalize();
            var member = new SquadRoster.Member
            {
                Name = sky ? "Небесный лучник" : "Лучник полигона", Sin = SinType.Pride, Moral = MoralType.Neutral,
                Gender = Gender.Male, Intensity = 55f, Loyalty = 90f, Trade = Trade.Archer, Shell = ShellType.Skeleton,
            };
            var w = spawner.Forge(member, Ground(hero.transform.position + f * 1.5f + Vector3.Cross(Vector3.up, f) * 1.5f));
            if (w == null) return "лучник не встал";
            _bow = w.GetComponent<AutoAttack>();
            if (_bow != null) _bow.SkyBow = sky;
            _sky = null;
            _centre = hero.transform.position;
            return $"полигон: лучник {w.transform.position}, мишеней {Targets.Count}, своих {Mates.Count}, вперёд {f}";
        }
        private static Vector3 Ground(Vector3 p)
            => NavMesh.SamplePosition(p, out var hit, 6f, NavMesh.AllAreas) ? hit.position : p;
    }
}
