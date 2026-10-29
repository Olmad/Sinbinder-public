// Assets/Scripts/Gameplay/Remains.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Тела и материал для алтаря «Перетягивания» (54-TUG.md; автор, 28.10: «трупы таскать»;
    /// «с живого можно взять и плоть и собрать зомби, то же и с самими зомби»).
    ///
    /// Павший оставляет тело (<see cref="HarvestableBody"/>). F — взвалить его на плечи: Греховод идёт
    /// медленнее (<see cref="Burden"/>), руки заняты. У стола тел в склепе F — положить: тело становится
    /// материалом. С живого и с зомби — плоть и кости, со скелета — кости, с голема — камень, с
    /// призрака — ничего (тела нет). Что поднимешь, решает материал (<see cref="Needs"/>): скелет — кости,
    /// зомби — плоть и кости, призрак — одна душа, голем — камень (и рецепт — CryptUpgrades).
    ///
    /// Счёт — числом: этим Греховод владеет. В демо (не «Перетягивание») тела на столе не кончаются, как прежде.
    /// </summary>
    public static class Remains
    {
        public enum Stuff { Flesh, Bones, Stone }

        private static readonly Dictionary<Stuff, int> Stock = new();

        public static bool Counted => Lords.Active;

        /// <summary>Что несёт Греховод на плечах; нет — null.</summary>
        public static HarvestableBody Carried { get; private set; }
        private static ShellType _carriedShell;
        private static string _carriedName;
        private static GameObject _load;
        private static float _speed = -1f;

        /// <summary>С ношей — во столько раз медленнее.</summary>
        public const float Burden = 0.6f;
        private const float Reach = 2.2f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() { Stock.Clear(); Carried = null; _load = null; _speed = -1f; }

        public static void Forget() => Rearm();

        public static int Count(Stuff s) => Stock.TryGetValue(s, out var n) ? n : 0;

        /// <summary>Материал на стол — добыча экспедиции (MapEvent): камень кургана, кости могилы.</summary>
        public static void Add(Stuff s, int n) { if (n > 0) Stock[s] = Count(s) + n; }

        public static bool Carrying => _load != null;

        /// <summary>Что даёт тело этой оболочки.</summary>
        public static IEnumerable<Stuff> From(ShellType body)
        {
            switch (body)
            {
                case ShellType.Living:
                case ShellType.Zombie: yield return Stuff.Flesh; yield return Stuff.Bones; break;
                case ShellType.Skeleton: yield return Stuff.Bones; break;
                case ShellType.Golem: yield return Stuff.Stone; break;
                case ShellType.Ghost: break;
                default: yield return Stuff.Bones; break;
            }
        }

        /// <summary>Что нужно, чтобы поднять в этой оболочке.</summary>
        public static IEnumerable<Stuff> Needs(ShellType shell)
        {
            switch (shell)
            {
                case ShellType.Zombie: yield return Stuff.Flesh; yield return Stuff.Bones; break;
                case ShellType.Golem: yield return Stuff.Stone; break;
                case ShellType.Ghost: break;
                default: yield return Stuff.Bones; break;
            }
        }

        public static bool Enough(ShellType shell)
        {
            var need = new Dictionary<Stuff, int>();
            foreach (var s in Needs(shell)) { need.TryGetValue(s, out var n); need[s] = n + 1; }
            foreach (var kv in need) if (Count(kv.Key) < kv.Value) return false;
            return true;
        }

        public static void Spend(ShellType shell)
        {
            foreach (var s in Needs(shell)) Stock[s] = Mathf.Max(0, Count(s) - 1);
        }

        public static string Word(Stuff s) => s switch
        {
            Stuff.Flesh => Loc.T("плоть"),
            Stuff.Stone => Loc.T("камень"),
            _ => Loc.T("кости"),
        };

        /// <summary>Сколько чего на столе: «плоть 1, кости 2».</summary>
        public static string Ledger()
            => Loc.F("на столе: плоть {0}, кости {1}, камень {2}", Count(Stuff.Flesh), Count(Stuff.Bones), Count(Stuff.Stone));

        // ---------------------------------------------------------- ноша

        /// <summary>Тела рядом с Греховодом, которые можно взвалить (руки пусты, тело есть).</summary>
        public static IEnumerable<HarvestableBody> Bodies()
        {
            if (!Counted || Carrying || !SinbinderPlayer.Exists || SinbinderPlayer.Instance.IsDead) yield break;
            var manager = CombatManager.Instance;
            if (manager == null) yield break;
            foreach (var b in manager.BodiesOnField)
                if (b != null && !b.IsCollected && b.Shell != ShellType.Ghost
                    && CampFocus.GroundDistance(SinbinderPlayer.Where, b.transform.position) <= Reach)
                    yield return b;
        }

        public static string TakePrompt(HarvestableBody b) => Loc.F("F — взвалить тело: {0}", Loc.Name(Who(b)));

        /// <summary>Взвалить тело: павший уходит с земли, на плечах — ноша, шаг медленнее.</summary>
        public static void Take(HarvestableBody b)
        {
            if (b == null || Carrying || !SinbinderPlayer.Exists) return;
            var hero = SinbinderPlayer.Instance;
            _carriedShell = b.Shell;
            _carriedName = Who(b);
            b.MarkCollected();
            Carried = b;

            // Тело павшего — с земли: оно теперь на плечах.
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
                if (w != null && w.IsDead && w.DisplayName == _carriedName
                    && CampFocus.GroundDistance(w.transform.position, b.transform.position) < 3f)
                { Object.Destroy(w.gameObject); break; }

            _load = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _load.name = Loc.F("Ноша — {0}", _carriedName);
            Object.Destroy(_load.GetComponent<Collider>());
            _load.transform.SetParent(hero.transform, false);
            _load.transform.localPosition = new Vector3(0f, 1.55f, -0.05f);
            _load.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            _load.transform.localScale = new Vector3(0.32f, 0.75f, 0.32f);
            _load.GetComponent<Renderer>().material.color = new Color(0.22f, 0.19f, 0.17f);

            var agent = hero.GetComponent<NavMeshAgent>();
            if (agent != null) { _speed = agent.speed; agent.speed *= Burden; }
            Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(Loc.F("На плечах — тело: {0}. Отнесите его к столу в склепе.", Loc.Name(_carriedName)));
            Object.Destroy(b.gameObject);
        }

        /// <summary>Положить ношу на стол: тело — в материал.</summary>
        public static void Lay()
        {
            if (!Carrying) return;
            var got = new List<string>();
            foreach (var s in From(_carriedShell)) { Stock[s] = Count(s) + 1; got.Add(Word(s)); }
            Drop();
            Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(got.Count > 0
                ? Loc.F("Тело на столе: {0}. {1}.", string.Join(", ", got), Ledger())
                : Loc.T("С этого тела взять нечего."));
        }

        /// <summary>
        /// Положить тело с плеч на землю (автор, 29.10: «отпускание предмета в руках на G») — лежит перед
        /// Греховодом, поднять снова — F, как любое тело. Не пропадает, как при <see cref="Drop"/>.
        /// </summary>
        public static void PutDown()
        {
            if (!Carrying || !SinbinderPlayer.Exists) return;
            var hero = SinbinderPlayer.Instance.transform;
            var flat = hero.forward; flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f) flat = Vector3.forward;
            var at = hero.position + flat.normalized * 0.9f;
            if (NavMesh.SamplePosition(at, out var hit, 1.5f, NavMesh.AllAreas)) at = hit.position;

            var body = new GameObject("Body_" + _carriedName);   // ключ: так тела зовёт Who
            body.transform.position = at;
            var look = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            look.name = "Тело";
            Object.Destroy(look.GetComponent<Collider>());
            look.transform.SetParent(body.transform, false);
            look.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            look.transform.localRotation = Quaternion.Euler(90f, hero.eulerAngles.y, 0f);
            look.transform.localScale = new Vector3(0.32f, 0.75f, 0.32f);
            look.GetComponent<Renderer>().material.color = new Color(0.22f, 0.19f, 0.17f);
            body.AddComponent<HarvestableBody>().Initialize(_carriedShell, 0, false, "");
            string who = _carriedName;
            Drop();
            Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(Loc.F("Тело положено на землю: {0}.", Loc.Name(who)));
        }

        /// <summary>Сбросить ношу (пал, ушёл со сцены) — без материала.</summary>
        public static void Drop()
        {
            if (_load != null) Object.Destroy(_load);
            _load = null;
            Carried = null;
            var hero = SinbinderPlayer.Instance;
            var agent = hero != null ? hero.GetComponent<NavMeshAgent>() : null;
            if (agent != null && _speed > 0f) agent.speed = _speed;
            _speed = -1f;
        }

        private static string Who(HarvestableBody b)
        {
            var n = b != null ? b.name : "";
            return n.StartsWith("Body_") ? n.Substring(5) : n;
        }
    }
}
