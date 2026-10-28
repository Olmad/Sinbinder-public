// Assets/Scripts/Gameplay/BuildSite.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Постройки «Перетягивания» — через характер, а не «бедная RTS» (54-TUG.md; автор, 28.10: «ворота перед
    /// склепом, сторожевые вышки, разумно отправлять унылых на защиту» — «вполне, делай»). Перед каждым склепом
    /// четыре места: две вышки, ворота с частоколом, сигнальный костёр.
    ///
    /// Заложить — F у места, монеты из кошеля партии. Строят верные этой стороны, что стоят рядом: чем
    /// больше рук, тем быстрее; гордому строить не по душе («Я не плотник»), он остывает, пока строит.
    ///
    /// * <b>Вышка</b> — пост: пришедший к ней верный поднимается наверх и смотрит далеко (туман войны).
    ///   Унылому на посту хорошо — верность растёт; гневному невмоготу — остывает и сам слезает.
    /// * <b>Ворота</b> — частокол поперёк подхода и створка (как дверь склепа, F — открыть); прочность
    ///   своя. Охотники сперва ломают их, потом дверь.
    /// * <b>Костёр</b> — склеп бьют: верные ближе 50 м спешат бить того, кто бьёт, без приказа. У огня
    ///   не скучают, а унылым и чревоугодным там и верности прибывает (Idleness).
    /// </summary>
    public class BuildSite : MonoBehaviour
    {
        public enum Kind { Tower, Gate, Fire }

        private static readonly List<BuildSite> Live = new();

        private Kind _kind;
        private int _side;
        private float _progress = -1f;   // −1 — не заложено
        private bool _done;
        private GameObject _look;
        private float _nextTick;
        private readonly HashSet<string> _grumbled = new();

        public Kind What => _kind;
        public int Side => _side;
        public bool Done => _done;
        public bool Laid => _progress >= 0f;
        public float Progress => _done ? 1f : Mathf.Clamp01(_progress / Need);

        public static IReadOnlyList<BuildSite> All => Live;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Live.Clear();

        void OnEnable() => Live.Add(this);
        void OnDisable() => Live.Remove(this);

        private float Need => _kind switch { Kind.Tower => 45f, Kind.Gate => 60f, _ => 15f };
        public int Cost => _kind switch { Kind.Tower => 6, Kind.Gate => 8, _ => 3 };
        private string Title => _kind switch { Kind.Tower => Loc.T("вышку"), Kind.Gate => Loc.T("ворота с частоколом"), _ => Loc.T("сигнальный костёр") };

        /// <summary>Места перед склепом: вышки по бокам, ворота поперёк подхода, костёр — у двери.</summary>
        public static void Plant(Transform crypt, int side)
        {
            Site(crypt, side, Kind.Tower, new Vector3(-7.5f, 0f, 11f));
            Site(crypt, side, Kind.Tower, new Vector3(7.5f, 0f, 11f));
            Site(crypt, side, Kind.Gate, new Vector3(0f, 0f, 14f));
            Site(crypt, side, Kind.Fire, new Vector3(4f, 0f, 9f));
        }

        private static void Site(Transform crypt, int side, Kind kind, Vector3 local)
        {
            var go = new GameObject(Loc.F("Место — {0}", kind switch { Kind.Tower => Loc.T("вышка"), Kind.Gate => Loc.T("ворота"), _ => Loc.T("костёр") }));
            go.transform.SetParent(crypt, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.identity;
            var site = go.AddComponent<BuildSite>();
            site._kind = kind;
            site._side = side;
            site.Stakes();
        }

        // ---------------------------------------------------------- заложить и строить

        /// <summary>Места своей стороны, до которых достаёт рука Греховода игрока.</summary>
        public static IEnumerable<BuildSite> InReach()
        {
            if (!Lords.Active || !SinbinderPlayer.Exists || SinbinderPlayer.Instance.IsDead || TugMatch.Instance == null) yield break;
            foreach (var s in Live)
                if (s != null && !s.Laid && s._side == TugMatch.Instance.PlayerSide
                    && CampFocus.GroundDistance(SinbinderPlayer.Where, s.transform.position) <= 3f)
                    yield return s;
        }

        public string Prompt => Loc.F("F — заложить {0}: {1} мон. (в кошеле {2})", Title, Cost, Favor.Purse);

        public void Lay() => Lay(Lords.Player);

        /// <summary>Заложить от Греховода: монеты — из его кошеля; не хватает — нет.</summary>
        public void Lay(string lord)
        {
            if (Laid || !Favor.Spend(lord, Cost)) return;
            _progress = 0f;
            Scaffold();
            Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(Loc.F("Заложено: {0}. Строят верные, что стоят рядом.", Title));
        }

        private static string LordOf(int side) => Loc.F("Греховод стороны {0}", side + 1);

        void Update()
        {
            if (!Laid || _done || TugMatch.Over || Time.time < _nextTick) return;
            _nextTick = Time.time + 1f;
            string lord = LordOf(_side);
            int hands = 0;
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
            {
                if (w == null || w.IsDead || w.Team != Team.Neutral || w.Soul == null) continue;
                if (CampFocus.GroundDistance(w.transform.position, transform.position) > 4.5f) continue;
                if (Allegiance.Dearest(w.Id) != lord || Allegiance.Of(w.Id, w.Loyalty, lord) < Hostility.Follows) continue;
                hands++;
                if (w.Soul.Sin == SinType.Pride)
                    Favor.Nudge(w, lord, -0.3f, _grumbled.Add(w.Id) ? Loc.T("Я не плотник, владыка.") : null);
            }
            _progress += hands;
            if (_progress >= Need) Finish();
        }

        /// <summary>Достроить сейчас (по ходу дела; прогон зовёт сразу).</summary>
        public void Finish()
        {
            _done = true;
            _progress = Need;
            if (_look != null) Destroy(_look);
            switch (_kind)
            {
                case Kind.Tower: Tower(); break;
                case Kind.Gate: Gate(); break;
                default: Fire(); break;
            }
            Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(Loc.F("Достроено: {0}.", Title));
            Debug.Log($"[ПОСТРОЙКИ] сторона {_side + 1}: {_kind}");
        }

        // ---------------------------------------------------------- вид

        private static Material _wood;
        private static Material Wood()
        {
            if (_wood != null) return _wood;
            var crate = Resources.Load<GameObject>("MapProps/Crate");
            var r = crate != null ? crate.GetComponentInChildren<Renderer>() : null;
            _wood = r != null ? r.sharedMaterial : new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.32f, 0.23f, 0.15f) };
            return _wood;
        }

        private GameObject Block(Transform parent, Vector3 at, Vector3 size, bool solid = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = Wood();
            if (!solid) Destroy(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>Пустое место — четыре колышка по углам: видно, что здесь можно строить.</summary>
        private void Stakes()
        {
            _look = new GameObject("Колышки");
            _look.transform.SetParent(transform, false);
            float h = _kind == Kind.Gate ? 6f : 1.2f;
            foreach (var (x, z) in new[] { (-h, -1.2f), (h, -1.2f), (-h, 1.2f), (h, 1.2f) })
                Block(_look.transform, new Vector3(x, 0.25f, z), new Vector3(0.08f, 0.5f, 0.08f), false);
        }

        /// <summary>Заложено — леса: стойки по углам, что дорастут.</summary>
        private void Scaffold()
        {
            if (_look != null) Destroy(_look);
            _look = new GameObject("Леса");
            _look.transform.SetParent(transform, false);
            float h = _kind == Kind.Gate ? 6f : 1.4f;
            foreach (var (x, z) in new[] { (-h, -1.4f), (h, -1.4f), (-h, 1.4f), (h, 1.4f) })
                Block(_look.transform, new Vector3(x, 0.9f, z), new Vector3(0.14f, 1.8f, 0.14f), false);
        }

        private void Tower()
        {
            var root = new GameObject("Вышка").transform;
            root.SetParent(transform, false);
            foreach (var (x, z) in new[] { (-1.1f, -1.1f), (1.1f, -1.1f), (-1.1f, 1.1f), (1.1f, 1.1f) })
                Block(root, new Vector3(x, 2.1f, z), new Vector3(0.22f, 4.2f, 0.22f));
            Block(root, new Vector3(0f, 4.25f, 0f), new Vector3(2.8f, 0.18f, 2.8f));
            foreach (var (x, z, sx, sz) in new[] { (0f, -1.35f, 2.8f, 0.1f), (0f, 1.35f, 2.8f, 0.1f), (-1.35f, 0f, 0.1f, 2.8f), (1.35f, 0f, 0.1f, 2.8f) })
                Block(root, new Vector3(x, 4.75f, z), new Vector3(sx, 0.8f, sz), false);
            gameObject.AddComponent<TowerPost>().Configure(_side, transform.position + Vector3.up * 4.35f);
        }

        private void Gate()
        {
            var root = new GameObject("Ворота").transform;
            root.SetParent(transform, false);
            // Частокол по бокам проёма, проём — 3 м посередине.
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f;
                var wall = Block(root, new Vector3(s * 4.25f, 1.3f, 0f), new Vector3(5.5f, 2.6f, 0.35f));
                wall.name = "Частокол";
            }
            var hinge = new GameObject("Створка ворот");
            hinge.transform.SetParent(root, false);
            hinge.transform.localPosition = new Vector3(-1.5f, 0f, 0f);
            var leaf = Block(hinge.transform, new Vector3(1.5f, 1.25f, 0f), new Vector3(3f, 2.5f, 0.22f));
            leaf.name = "Створка";
            leaf.AddComponent<Sturdy>().Configure(Loc.F("Ворота склепа {0}", _side + 1), 300f, _side);
            var gap = leaf.AddComponent<NavMeshObstacle>();
            gap.shape = NavMeshObstacleShape.Box;
            gap.size = Vector3.one;
            gap.carving = true;
            gap.carveOnlyStationary = false;
            hinge.AddComponent<CryptDoor>();
            // Частокол держит путь: стены — препятствия навмеша (навмеш уже испечён).
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (r.gameObject == leaf) continue;
                var o = r.gameObject.AddComponent<NavMeshObstacle>();
                o.shape = NavMeshObstacleShape.Box;
                o.size = Vector3.one;
                o.carving = true;
            }
        }

        private void Fire()
        {
            var prefab = MapLoader.Prefab("Campfire");
            if (prefab != null)
            {
                var go = Instantiate(prefab, transform);
                go.transform.SetPositionAndRotation(transform.position, prefab.transform.rotation);
                go.transform.localScale = prefab.transform.localScale;
            }
            var lamp = new GameObject("Огонь");
            lamp.transform.SetParent(transform, false);
            lamp.transform.localPosition = Vector3.up * 1.1f;
            var light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 9f;
            light.intensity = 2f;
            light.color = new Color(1f, 0.6f, 0.3f);
            gameObject.AddComponent<SignalFire>().Configure(_side);
        }

        // ---------------------------------------------------------- спрашивают другие

        /// <summary>Ворота стороны, если достроены и целы.</summary>
        public static CryptDoor GateOf(int side)
        {
            foreach (var s in Live)
                if (s != null && s._done && s._kind == Kind.Gate && s._side == side)
                {
                    var d = s.GetComponentInChildren<CryptDoor>();
                    var hp = d != null ? d.Leaf.GetComponent<Sturdy>() : null;
                    if (hp != null && !hp.Broken) return d;
                }
            return null;
        }

        /// <summary>Сигнальный костёр этой стороны рядом с точкой (у огня не скучают).</summary>
        public static bool NearFire(int side, Vector3 at, float within)
        {
            foreach (var s in Live)
                if (s != null && s._done && s._kind == Kind.Fire && s._side == side
                    && CampFocus.GroundDistance(s.transform.position, at) <= within) return true;
            return false;
        }
    }

    /// <summary>
    /// Пост на вышке: верный, пришедший к ней, встаёт наверх и смотрит далеко. Унылому там хорошо, гневному —
    /// невмоготу: остывает и через минуту слезает сам. Новый приказ — слезает и идёт.
    /// </summary>
    public class TowerPost : MonoBehaviour
    {
        public const float Sight = 26f;
        private static readonly List<TowerPost> Live = new();
        private int _side;
        private Vector3 _top;
        private Warrior _guard;
        private float _since, _tick;

        public Warrior Guard => _guard;
        public Vector3 Top => _top;
        public int Side => _side;
        public static IReadOnlyList<TowerPost> All => Live;

        /// <summary>Стоит ли воин на посту — тогда он при деле (Idleness).</summary>
        public static bool Guards(Warrior w)
        {
            foreach (var p in Live) if (p != null && p._guard == w) return true;
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Live.Clear();

        void OnEnable() => Live.Add(this);
        void OnDisable() => Live.Remove(this);

        public void Configure(int side, Vector3 top) { _side = side; _top = top; }

        void Update()
        {
            string lord = Loc.F("Греховод стороны {0}", _side + 1);
            if (_guard == null || _guard.IsDead) { _guard = null; Recruit(lord); return; }

            // Новый приказ — слезть и идти.
            if (_guard.HasCommand && Time.time - _guard.Command.IssuedAt < 1f && Time.time - _since > 1f) { Release(); return; }
            _guard.transform.position = _top;

            if (Time.time < _tick) return;
            _tick = Time.time + 10f;
            if (_guard.Soul == null) return;
            if (_guard.Soul.Sin == SinType.Sloth) Favor.Nudge(_guard, lord, 0.6f, null);
            else if (_guard.Soul.Sin == SinType.Wrath)
            {
                Favor.Nudge(_guard, lord, -1f, Loc.T("Стоять и смотреть? Я не сова, владыка."));
                if (Time.time - _since > 60f) Release();
            }
        }

        /// <summary>Подошёл верный этой стороны (дошёл по приказу к вышке) — наверх.</summary>
        private void Recruit(string lord)
        {
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
            {
                if (w == null || w.IsDead || w.Team != Team.Neutral) continue;
                if (CampFocus.GroundDistance(w.transform.position, transform.position) > 2.2f) continue;
                if (Allegiance.Dearest(w.Id) != lord || Allegiance.Of(w.Id, w.Loyalty, lord) < Hostility.Follows) continue;
                Take(w);
                return;
            }
        }

        public void Take(Warrior w)
        {
            _guard = w;
            _since = Time.time;
            w.ClearCommand();
            var agent = w.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled) { if (agent.isOnNavMesh) agent.ResetPath(); agent.updatePosition = false; }
            w.transform.position = _top;
        }

        private void Release()
        {
            var w = _guard;
            _guard = null;
            if (w == null) return;
            var down = transform.position + transform.forward * 2f;
            if (NavMesh.SamplePosition(down, out var hit, 3f, NavMesh.AllAreas)) down = hit.position;
            w.transform.position = down;
            var agent = w.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled) { agent.Warp(down); agent.updatePosition = true; }
        }
    }

    /// <summary>Сигнальный костёр: склеп стороны бьют — верные ближе 50 м бьют того, кто бьёт, без приказа.</summary>
    public class SignalFire : MonoBehaviour
    {
        private const float Call = 50f;
        private int _side;
        private float _tick;

        public void Configure(int side) => _side = side;

        void Update()
        {
            if (TugMatch.Instance == null || Time.time < _tick) return;
            _tick = Time.time + 2f;
            var (door, throne) = TugMatch.Instance.PartsOf(_side);
            Warrior by = null;
            foreach (var s in new[] { door != null ? door.Leaf.GetComponent<Sturdy>() : null, throne,
                                      BuildSite.GateOf(_side) != null ? BuildSite.GateOf(_side).Leaf.GetComponent<Sturdy>() : null })
                if (s != null && s.LastBy != null && !s.LastBy.IsDead && Time.time - s.LastHitAt < 4f) by = s.LastBy;
            if (by == null) return;

            string lord = Loc.F("Греховод стороны {0}", _side + 1);
            Hostility.Mark(lord, by);
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
            {
                if (w == null || w.IsDead || w.Team != Team.Neutral || w.HasCommand) continue;
                if (CampFocus.GroundDistance(w.transform.position, transform.position) > Call) continue;
                if (Allegiance.Dearest(w.Id) != lord || Allegiance.Of(w.Id, w.Loyalty, lord) < Hostility.Follows) continue;
                w.IssueCommand(CommandKind.Attack, by.transform.position, by.gameObject, 0f, lord);
            }
        }
    }
}
