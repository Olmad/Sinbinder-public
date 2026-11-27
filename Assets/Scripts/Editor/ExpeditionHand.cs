// Assets/Scripts/Editor/ExpeditionHand.cs
// «Рука игрока» для личной вылазки (автор, 26.11: «не хватает более ручно-глазной проверки от тебя»).
// Зонд часовни (PersonalExpeditionProbe) ведёт свой сценарий; здесь — только то, что может игрок:
// Греховод идёт щелчком по земле (UnitMover.CommandMove — как ПКМ), а если приказ встал — шагом,
// как с клавиш (agent.Move, как PlayerWalk). F — только то, что игра предлагает рядом (OfferInteractions).
// Без переносов. Глаза — туда, куда идёт (игрок ведёт мышью); кадр каждые 5 с в D:\Claude\обоз-рука.
//
// Верстак (скрытая Unity без фокуса — камера стоит, пока не дать ей фокус, см. Begin):
//   call Sinbinder.EditorTools.ExpeditionHand.To старший ;; wait 40 ;; get Sinbinder.EditorTools.ExpeditionHand.Report
//   call Sinbinder.EditorTools.ExpeditionHand.Press говорить ;; buttons ;; click <ответ>
//   куда: «старший» (охрана обоза), «цель», «знак» или «x,z».
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Sinbinder.Gameplay;

namespace Sinbinder.EditorTools
{
    public static class ExpeditionHand
    {
        private const string Shots = "D:/Claude/обоз-рука";
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        private static string _where = "";
        private static Vector3 _goal;
        private static float _reach, _started, _nextShot, _lastTick, _stillSince, _lastLeft;
        private static int _shots, _clicks;
        private static bool _running, _stepping;
        private static readonly StringBuilder Trace = new StringBuilder();

        /// <summary>Итог: идёт ли, где Греховод, сколько осталось, что предлагает F.</summary>
        public static string Report
        {
            get
            {
                if (!SinbinderPlayer.Exists) return "Греховода нет";
                var at = SinbinderPlayer.Where;
                return $"{(_running ? "идёт" : "стоит")} к «{_where}»: осталось {Flat(at, _goal):0.0} м, щелчков {_clicks}"
                     + $"{(_stepping ? ", дальше шагом" : "")}; кадров {_shots}; F: {string.Join(" / ", Offers().ConvertAll(o => o.label))}. {Trace}";
            }
        }

        /// <summary>Идти к точке, как игрок: щелчок по земле, при остановке — шагом.</summary>
        public static string To(string where)
        {
            if (!Application.isPlaying || !PersonalExpedition.Active || !SinbinderPlayer.Exists) return "нужна идущая личная вылазка";
            if (!Resolve(where, out _goal, out _reach)) return $"не знаю, где «{where}»: старший, цель, знак или x,z";
            _where = where;
            Begin();
            Click();
            return $"пошёл к «{where}»: {Flat(SinbinderPlayer.Where, _goal):0} м";
        }

        /// <summary>Нажать F там, где игра предлагает надпись с этими словами.</summary>
        public static string Press(string part)
        {
            var offers = Offers();
            var hit = offers.Find(o => o.label.Contains(part));
            if (hit.act == null) return $"F «{part}» рядом не предлагают; есть: {string.Join(" / ", offers.ConvertAll(o => o.label))}";
            hit.act();
            return "нажато: " + hit.label;
        }

        /// <summary>Кадр глазами Греховода сейчас — в ту же папку.</summary>
        public static string Shot(string name)
        {
            Directory.CreateDirectory(Shots);
            Utilets.Snapshot.Now(Shots, name);
            return $"{Shots}/{name}";
        }

        private static void Begin()
        {
            // Скрытая Unity без фокуса: RTS_Camera.Update выходит на первой строке, и глаза
            // остаются в склепе (27.11). Игрок с окном в фокусе этого не видит.
            var cam = Camera.main != null ? Camera.main.GetComponent<RTS_Camera>() : null;
            typeof(RTS_Camera).GetField("_hasFocus", Hidden)?.SetValue(cam, true);
            Directory.CreateDirectory(Shots);
            Trace.Clear();
            _clicks = 0;
            _stepping = false;
            _started = _lastTick = _stillSince = Time.realtimeSinceStartup;
            _nextShot = 0f;
            _lastLeft = Flat(SinbinderPlayer.Where, _goal);
            _running = true;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!Application.isPlaying || !PersonalExpedition.Active || !SinbinderPlayer.Exists) { Stop("вылазка кончилась"); return; }
            if (EditorApplication.isPaused) return;
            float now = Time.realtimeSinceStartup;
            var hero = SinbinderPlayer.Instance;
            var at = hero.transform.position;
            float left = Flat(at, _goal);

            // Глаза — по ходу, как игрок ведёт мышью.
            var cam = Camera.main != null ? Camera.main.GetComponent<RTS_Camera>() : null;
            var ahead = _goal - at; ahead.y = 0f;
            if (cam != null && cam.FirstPersonNow && ahead.sqrMagnitude > 1f)
                cam.Yaw = Mathf.MoveTowardsAngle(cam.Yaw, Quaternion.LookRotation(ahead).eulerAngles.y, 180f * (now - _lastTick));

            if (now >= _nextShot)
            {
                _nextShot = now + 5f;
                try { Utilets.Snapshot.Now(Shots, $"{_where} {_shots:00} — {left:0} м"); _shots++; } catch { }
            }

            if (left <= _reach) { Stop($"дошёл: {left:0.0} м"); return; }
            if (now - _started > 120f) { Stop($"120 с вышли, осталось {left:0.0} м"); return; }
            if (Core.GamePauseController.Stopped || UI.Modal.Any) { _lastTick = now; return; }

            // Не сдвинулся 3 с — приказ мышью встал. Сначала ещё один щелчок, потом шагом.
            if (_lastLeft - left > 0.3f) { _lastLeft = left; _stillSince = now; }
            else if (now - _stillSince > 3f)
            {
                if (!_stepping && _clicks < 3) { Trace.Append($"[встал в {left:0} м — щелчок] "); Click(); }
                else if (!_stepping) { _stepping = true; Trace.Append($"[встал в {left:0} м — шагом] "); }
                else { Trace.Append($"[шагом не идёт, {left:0} м] "); Stop($"застрял в {left:0.0} м у {at.x:0},{at.z:0}"); return; }
                _stillSince = now;
            }

            if (_stepping)
            {
                var agent = hero.GetComponent<NavMeshAgent>();
                if (agent != null && agent.isOnNavMesh)
                {
                    var path = new NavMeshPath();
                    NavMesh.CalculatePath(at, _goal, new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask }, path);
                    var corner = path.corners.Length > 1 ? path.corners[1] : _goal;
                    var dir = corner - at; dir.y = 0f;
                    if (dir.sqrMagnitude < 0.01f) dir = ahead;
                    agent.Move(dir.normalized * 3.5f * Mathf.Min(0.1f, now - _lastTick));
                }
            }
            _lastTick = now;
        }

        private static void Click()
        {
            _clicks++;
            var legs = SinbinderPlayer.Instance.GetComponent<UnitMover>();
            // Как ПКМ: точка на земле у цели, не в самой вещи (у алтаря и повозки точка внутри).
            var spot = Vector3.MoveTowards(_goal, SinbinderPlayer.Where, Mathf.Min(1.5f, _reach * 0.5f));
            legs?.CommandMove(spot);
        }

        private static void Stop(string why)
        {
            if (!_running) return;
            _running = false;
            EditorApplication.update -= Tick;
            Trace.Append($"[{why}] ");
            try { Utilets.Snapshot.Now(Shots, $"{_where} конец — {why}"); _shots++; } catch { }
        }

        private static bool Resolve(string where, out Vector3 goal, out float reach)
        {
            goal = Vector3.zero; reach = 3.5f;
            var live = typeof(PersonalExpedition).GetField("_active", Hidden)?.GetValue(null);
            if (live == null) return false;
            switch (where)
            {
                case "цель": goal = (Vector3)typeof(PersonalExpedition).GetProperty("Objective", Hidden).GetValue(live); return true;
                case "знак": goal = (Vector3)typeof(PersonalExpedition).GetProperty("Gate", Hidden).GetValue(live); return true;
                case "старший":
                    var captain = typeof(PersonalExpedition).GetMethod("Captain", Hidden)?.Invoke(live, null) as Warrior;
                    if (captain == null) return false;
                    goal = captain.transform.position; reach = 4.5f;   // F — говорить: ближе 6 м (TalkReach)
                    return true;
            }
            var xz = where.Split(',');
            if (xz.Length == 2 && float.TryParse(xz[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x)
                && float.TryParse(xz[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z))
            { goal = new Vector3(x, 0f, z); return true; }
            return false;
        }

        private static List<(string label, System.Action act)> Offers()
        {
            var list = new List<(string label, System.Action act)>();
            PersonalExpedition.OfferInteractions((gaze, label, act) => list.Add((label, act)));
            return list;
        }

        private static float Flat(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }
    }
}
