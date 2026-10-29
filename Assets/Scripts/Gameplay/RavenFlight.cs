// Assets/Scripts/Gameplay/RavenFlight.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Полёт ворона Каргана (автор, 27.10: «ворон не улетает и не прилетает»). Раз в сто секунд —
    /// час по имени хозяина, без жребия (<see cref="Wardrobe.Stamp"/>), — слетает с плеча с криком:
    /// круг над хозяином, конёк ближней палатки — посидеть, и назад на плечо, где бы тот ни стоял.
    /// Палатки рядом нет — второй круг вместо привала.
    ///
    /// Крылья — части модели (raven.py): сидя видны сложенные (Folded), в полёте — раскрытые
    /// (WingL, WingR), они машут поворотом вокруг оси тела. Машет на взлёте и на посадке, на круге —
    /// то машет, то парит. В полёте птицу клонят носом вниз (<see cref="FlightPitch"/>): крылья в модели
    /// наклонены вместе с телом сидящего и ложатся тогда горизонтально.
    ///
    /// Карган пал — ворон слетает к огню и остаётся там (класс <see cref="RavenPerch"/>: «ворон
    /// прибился к нему ещё при жизни и не ушёл после смерти»).
    /// </summary>
    public class RavenFlight : MonoBehaviour
    {
        private const float Period = 100f;
        private const float Lift = 2.0f;     // взлёт с плеча к кругу
        private const float Lap = 9.0f;      // круг над хозяином
        private const float Glide = 3.5f;    // к коньку палатки
        private const float Rest = 20f;      // сидит на коньке
        private const float Back = 4.5f;     // назад на плечо

        private const float Radius = 7f;
        private const float Height = 6f;

        /// <summary>Наклон носом вниз в полёте, градусы — FLIGHT_PITCH в raven.py.</summary>
        private const float FlightPitch = 25f;

        private static readonly List<RavenFlight> Live = new();

        private Warrior _master;
        private Transform _shoulder;
        private Vector3 _seatPos;
        private Quaternion _seatRot;
        private Vector3 _seatScale;
        private float _size;
        private float _offset;
        private int _turn;

        private Transform _folded, _wingL, _wingR;
        private Quaternion _restL, _restR;

        /// <summary>С какой стороны тела крыло: +1 — справа (вверх — плюсом), −1 — слева.</summary>
        private float _sideL = 1f, _sideR = -1f;
        private float _orphanAt;

        private bool _aloft;
        private Vector3 _legFrom;
        private int _leg = -1;
        private Vector3 _ridge;
        private bool _hasRidge;
        private bool _orphan;
        private Vector3 _fire;
        private Vector3 _last;
        private float _restingUntil = -1f;

        /// <summary>Сидит ли ворон этого хозяина на привале (ветка или конёк) — и где (прогон снимает кадр).</summary>
        public static bool Perched(Warrior master, out Transform bird)
        {
            bird = null;
            foreach (var r in Live)
                if (r != null && r._master == master && r._aloft && Time.time < r._restingUntil) { bird = r.transform; return true; }
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Live.Clear();

        void OnEnable() => Live.Add(this);
        void OnDisable() => Live.Remove(this);

        /// <summary>Имена частей модели — их даёт raven.py (объекты Blender), а не код.</summary>
        public const string FoldedPart = "Folded", LeftWingPart = "WingL", RightWingPart = "WingR";

        /// <summary>Где ворон этого хозяина: на плече, в полёте, у огня — для прогона и подписи.</summary>
        public static string State(Warrior master)
        {
            foreach (var r in Live)
                if (r != null && r._master == master)
                    return r._orphan ? Loc.T("у огня") : r._aloft ? Loc.T("в полёте") : Loc.T("сидит на плече");
            return Loc.T("нет");
        }

        /// <summary>Летит ли ворон этого хозяина сейчас.</summary>
        public static bool Flying(Warrior master)
        {
            foreach (var r in Live)
                if (r != null && r._master == master) return r._aloft && !r._orphan;
            return false;
        }

        /// <summary>Есть ли у этого хозяина ворон — сидит, летит или у огня.</summary>
        public static bool Has(Warrior master)
        {
            foreach (var r in Live)
                if (r != null && r._master == master) return true;
            return false;
        }

        /// <summary>Поднять ворона сейчас — его час начнётся с этого мгновения (прогон, кадр полёта).</summary>
        public static RavenFlight Launch(Warrior master)
        {
            foreach (var r in Live)
                if (r != null && r._master == master && !r._orphan)
                {
                    r._offset = Period - Time.time % Period;
                    return r;
                }
            return null;
        }

        public void Configure(Warrior master, Transform shoulder)
        {
            _master = master;
            _shoulder = shoulder;
            _seatPos = transform.localPosition;
            _seatRot = transform.localRotation;
            _seatScale = transform.localScale;
            _size = transform.lossyScale.x;
            _offset = Wardrobe.Stamp(master.DisplayName) % Period;
            _turn = Wardrobe.Stamp(master.DisplayName) % 2 == 0 ? 1 : -1;

            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == FoldedPart) _folded = t;
                else if (t.name == LeftWingPart) _wingL = t;
                else if (t.name == RightWingPart) _wingR = t;
            }
            if (_wingL != null) { _restL = Quaternion.Inverse(transform.rotation) * _wingL.rotation; _sideL = Side(_wingL); }
            if (_wingR != null) { _restR = Quaternion.Inverse(transform.rotation) * _wingR.rotation; _sideR = Side(_wingR); }
            Wings(false);
        }

        /// <summary>Сторона крыла — по середине его рамки относительно тела.</summary>
        private float Side(Transform wing)
        {
            var r = wing.GetComponent<Renderer>();
            if (r == null) return 1f;
            return Vector3.Dot(r.bounds.center - transform.position, transform.right) >= 0f ? 1f : -1f;
        }

        void Update()
        {
            if (_orphan) { Orphaned(); return; }

            if (_master == null || _master.IsDead)
            {
                // Хозяин пал: с плеча — к огню, и там остаться.
                Depart();
                _orphan = true;
                _fire = FireSpot();
                _legFrom = transform.position;
                _orphanAt = Time.time;
                return;
            }

            float t = (Time.time + _offset) % Period;
            float lapEnd = Lift + Lap, glideEnd = lapEnd + Glide, restEnd = glideEnd + Rest, backEnd = restEnd + Back;

            // Не в свой час, или хозяин в бою, — на плече.
            if (t >= backEnd || (!_aloft && (t > 0.5f || RaidEvent.Running)))
            {
                if (_aloft) Settle();
                return;
            }
            if (!_aloft) { Depart(); _hasRidge = FindRidge(out _ridge); }

            var centre = _master.transform.position + Vector3.up * Height;
            float a0 = Wardrobe.Stamp(_master.DisplayName) % 360 * Mathf.Deg2Rad;
            Vector3 Circle(float k) => centre + new Vector3(Mathf.Cos(a0 + _turn * k), 0f, Mathf.Sin(a0 + _turn * k)) * Radius
                                       + Vector3.up * (0.6f * Mathf.Sin(2f * k));

            if (t < Lift)
            {
                Leg(0);
                float k = Smooth(t / Lift);
                var to = Circle(0f);
                Fly(Bezier(_legFrom, _legFrom + Vector3.up * 2.5f + _master.transform.forward * 1.5f, to, k), Flap(t, 6f, 55f), 0f);
            }
            else if (t < lapEnd || (!_hasRidge && t < restEnd))
            {
                // Палатки нет — круг длится и в часы привала: ещё два с лишним оборота.
                float k = (t - Lift) / Lap * 2f * Mathf.PI;
                bool beat = ((int)((t - Lift) / 2.2f)) % 2 == 0;
                Fly(Circle(k), beat ? Flap(t, 4.5f, 45f) : 8f, -_turn * 25f);
            }
            else if (t < glideEnd)
            {
                Leg(2);
                float k = Smooth((t - lapEnd) / Glide);
                bool braking = k > 0.85f;
                Fly(Bezier(_legFrom, Vector3.Lerp(_legFrom, _ridge, 0.5f) + Vector3.up * 1.5f, _ridge, k),
                    braking ? Flap(t, 8f, 50f) : 6f, 0f);
            }
            else if (t < restEnd)
            {
                // На коньке или на ветке: крылья сложены, клювом к хозяину; изредка поворачивает голову — всем телом.
                Wings(false);
                _restingUntil = Time.time + 0.2f;
                transform.position = _ridge;
                var look = _master.transform.position - _ridge; look.y = 0f;
                if (look.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(look) * Quaternion.Euler(0f, 25f * Mathf.Sin((t - glideEnd) * 0.7f), 0f);
                _last = transform.position;
            }
            else
            {
                Leg(4);
                float k = Smooth((t - restEnd) / Back);
                var seat = _shoulder != null ? _shoulder.TransformPoint(_seatPos) : _master.transform.position + Vector3.up * 1.5f;
                Fly(Bezier(_legFrom, Vector3.Lerp(_legFrom, seat, 0.5f) + Vector3.up * 3f, seat, k),
                    Flap(t, k > 0.8f ? 8f : 5f, 50f), 0f);
            }
        }

        // ---------------------------------------------------------- взлёт и посадка

        /// <summary>С плеча — в мир: дальше летит сам, а не за костью.</summary>
        private void Depart()
        {
            if (_aloft) return;
            _aloft = true;
            _leg = -1;
            transform.SetParent(null, true);
            transform.localScale = Vector3.one * _size;
            _legFrom = transform.position;
            _last = transform.position;
            Wings(true);
            Audio.Sfx.Caw(transform.position);
        }

        /// <summary>Сесть на плечо — туда же, где сидел, клювом по ходу хозяина.</summary>
        private void Settle()
        {
            _aloft = false;
            if (_shoulder == null) { Destroy(gameObject); return; }
            transform.SetParent(_shoulder, false);
            transform.localPosition = _seatPos;
            transform.localRotation = _seatRot;
            transform.localScale = _seatScale;
            Wings(false);
        }

        /// <summary>Новый отрезок пути — запомнить, откуда он начался.</summary>
        private void Leg(int leg)
        {
            if (_leg == leg) return;
            _leg = leg;
            _legFrom = transform.position;
        }

        /// <summary>Хозяина нет — к огню, сесть на землю у него и остаться.</summary>
        private void Orphaned()
        {
            float k = Smooth(Mathf.Clamp01((Time.time - _orphanAt) / 5f));
            if (k < 1f)
            {
                Fly(Bezier(_legFrom, Vector3.Lerp(_legFrom, _fire, 0.5f) + Vector3.up * 4f, _fire, k), Flap(Time.time, 5f, 50f), 0f);
                return;
            }
            Wings(false);
            transform.position = _fire;
            var level = transform.forward; level.y = 0f;
            if (level.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(level);
        }

        // ---------------------------------------------------------- полёт

        private void Fly(Vector3 at, float wing, float roll)
        {
            var step = at - _last;
            transform.position = at;
            if (step.sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.LookRotation(step.normalized) * Quaternion.Euler(FlightPitch, 0f, roll);
            _last = at;
            Wings(true);
            Beat(wing);
        }

        /// <summary>Взмах: угол крыла вверх от горизонта, градусы; машет с частотой и размахом.</summary>
        private static float Flap(float t, float hz, float span) => span * Mathf.Sin(2f * Mathf.PI * hz * t) - 5f;

        /// <summary>Повернуть оба крыла на угол вокруг оси тела: правое — плюсом, левое — минусом.</summary>
        private void Beat(float angle)
        {
            var axis = transform.rotation * Quaternion.Euler(-FlightPitch, 0f, 0f) * Vector3.forward;
            Turn(_wingL, _restL, _sideL * angle, axis);
            Turn(_wingR, _restR, _sideR * angle, axis);
        }

        private void Turn(Transform wing, Quaternion rest, float angle, Vector3 axis)
        {
            if (wing == null) return;
            wing.rotation = Quaternion.AngleAxis(angle, axis) * (transform.rotation * rest);
        }

        private void Wings(bool open)
        {
            if (_folded != null) _folded.gameObject.SetActive(!open);
            if (_wingL != null) _wingL.gameObject.SetActive(open);
            if (_wingR != null) _wingR.gameObject.SetActive(open);
        }

        // ---------------------------------------------------------- куда

        /// <summary>Где посидеть: через раз — ветка сухого дерева, иначе конёк палатки; чего нет — другое.</summary>
        private bool FindRidge(out Vector3 ridge)
        {
            // Автор, 28.10: «было бы ещё лучше, если бы ворон садился на ветки».
            bool branchTurn = ((int)((Time.time + _offset) / Period)) % 2 == 0;
            if (branchTurn && FindBranch(out ridge)) return true;
            return FindTent(out ridge) || FindBranch(out ridge);
        }

        private static readonly Dictionary<Mesh, List<Vector3>> Perches = new();

        /// <summary>
        /// Ветка ближнего сухого дерева (props.py: DeadTree, AxeTree), не дальше 30 м от хозяина: из вершин модели —
        /// точки на средней части ветвей (45–85 % высоты, 0,7–1,7 м от ствола, верх ветви), та, что смотрит на хозяина.
        /// Модели читаемы (isReadable); считается раз на модель.
        /// </summary>
        private bool FindBranch(out Vector3 perch)
        {
            perch = default;
            float best = 30f;
            bool found = false;
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                var n = mf.gameObject.name;
                if (!(n.StartsWith("DeadTree") || n.StartsWith("AxeTree")) || mf.sharedMesh == null) continue;
                float d = CampFocus.GroundDistance(mf.transform.position, _master.transform.position);
                if (d >= best) continue;
                var points = PerchesOf(mf.sharedMesh);
                if (points.Count == 0) continue;
                var toward = _master.transform.position - mf.transform.position; toward.y = 0f;
                Vector3 pick = mf.transform.TransformPoint(points[0]);
                float face = float.MinValue;
                foreach (var p in points)
                {
                    var w = mf.transform.TransformPoint(p);
                    var side = w - mf.transform.position; side.y = 0f;
                    float f = Vector3.Dot(side.normalized, toward.normalized);
                    if (f > face) { face = f; pick = w; }
                }
                best = d;
                perch = pick + Vector3.up * 0.04f;
                found = true;
            }
            return found;
        }

        private static List<Vector3> PerchesOf(Mesh mesh)
        {
            if (Perches.TryGetValue(mesh, out var got)) return got;
            got = new List<Vector3>();
            if (mesh.isReadable)
            {
                var b = mesh.bounds;
                float h = b.size.y, y0 = b.min.y;
                // Верх ветви: из вершин в одной ячейке (треть метра) — самая высокая: сидят на ветке, а не под ней.
                var cells = new Dictionary<long, Vector3>();
                foreach (var p in mesh.vertices)
                {
                    float up = (p.y - y0) / Mathf.Max(0.01f, h);
                    float r = new Vector2(p.x - b.center.x, p.z - b.center.z).magnitude;
                    if (up < 0.45f || up > 0.85f || r < 0.7f || r > 1.7f) continue;
                    long cell = Mathf.RoundToInt(p.x * 3f) * 100000L + Mathf.RoundToInt(p.z * 3f);
                    if (!cells.TryGetValue(cell, out var top) || p.y > top.y) cells[cell] = p;
                }
                got.AddRange(cells.Values);
            }
            Perches[mesh] = got;
            return got;
        }

        /// <summary>Конёк ближней палатки (не дальше 30 м от хозяина): верх её рамки, над серединой.</summary>
        private bool FindTent(out Vector3 ridge)
        {
            ridge = default;
            float best = 30f;
            bool found = false;
            foreach (var tent in Object.FindObjectsByType<TentInterior>(FindObjectsSortMode.None))
            {
                float d = CampFocus.GroundDistance(tent.transform.position, _master.transform.position);
                if (d >= best) continue;
                var box = new Bounds(tent.transform.position, Vector3.zero);
                foreach (var r in tent.GetComponentsInChildren<Renderer>()) box.Encapsulate(r.bounds);
                if (box.size.y < 1f) continue;
                best = d;
                ridge = new Vector3(box.center.x, box.max.y + 0.02f, box.center.z);
                found = true;
            }
            return found;
        }

        /// <summary>Место на земле у главного огня; огня нет — там, где был хозяин.</summary>
        private Vector3 FireSpot()
        {
            var stoke = Object.FindFirstObjectByType<CampfireStoke>();
            if (stoke == null) return transform.position - Vector3.up * transform.position.y;
            var c = stoke.Centre;
            return new Vector3(c.x + 1.3f, c.y - 0.4f, c.z + 0.6f);
        }

        private static float Smooth(float k) => k * k * (3f - 2f * k);

        private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float k)
            => Vector3.Lerp(Vector3.Lerp(a, b, k), Vector3.Lerp(b, c, k), k);
    }
}
