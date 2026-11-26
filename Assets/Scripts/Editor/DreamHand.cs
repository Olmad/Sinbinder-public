// Assets/Scripts/Editor/DreamHand.cs
// «Рука игрока» для сна (автор, 26.11: «не хватает более ручно-глазной проверки от тебя»; «в обучении я не могу
// пройти дальше моста — меня возвращает»). Прогон сна (DreamWalkthrough) переносит Греховода и сам тянет рычаг —
// игрок так не может. Здесь Греховод идёт ТОЛЬКО щелчками по земле: та же проверка, что у щелчка игрока
// (DreamTutorialDirector.PermitDestination → UnitMover.CommandMove), цель — куда показывает стрелка «Лесная тропа»
// (DreamRoad.Detour). Без переносов. Каждые 4 с — кадр в D:\Claude\сон-рука.
//
// Верстак: dream stop=шёпот ;; set Sinbinder.EditorTools.DreamWalkthrough._done true ;; resume ;;
//          call Sinbinder.EditorTools.DreamHand.Forest true ;; wait 90 ;; get Sinbinder.EditorTools.DreamHand.Report
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.EditorTools
{
    public static class DreamHand
    {
        private const string Shots = "D:/Claude/сон-рука";
        private static int _clicks, _refused, _oldRefused, _shots, _pulledBack;
        private static float _bestProgress, _lastProgress, _nextClick, _nextShot, _nextPan, _started;
        private static Vector3 _target;
        private static bool _running;
        private static readonly StringBuilder Trace = new StringBuilder();

        /// <summary>Итог: щелчки, отказы, «рука вернула», докуда дошёл.</summary>
        public static string Report
        {
            get
            {
                var dream = DreamTutorialDirector.Instance;
                var hero = dream != null ? dream.Hero : null;
                string at = hero != null ? $"{hero.transform.position.x:0},{hero.transform.position.z:0}" : "—";
                return $"{(_running ? "идёт" : "стоит")}: стадия {DreamTutorialDirector.StageName}, мост {(DreamRoad.BridgeRaised ? "поднят" : "опущен")}; " +
                       $"щелчков {_clicks}, отвергнуто {_refused} (по старому правилу было бы {_oldRefused}), рука тянула назад {_pulledBack}; " +
                       $"Греховод у {at}, путь {_lastProgress:0} м (лучший {_bestProgress:0}); кадров {_shots}. {Trace}";
            }
        }

        /// <summary>
        /// Лесом в обход поднятого моста. tried — игрок уже трижды «шёл назад» (у завала на опушке): с этого
        /// мига сон пускал щелчок только в 8 м от главного пути.
        /// </summary>
        public static string Forest(bool tried)
        {
            var dream = DreamTutorialDirector.Instance;
            if (!Application.isPlaying || dream == null || dream.Hero == null) return "нужен идущий сон";
            if (tried)
            {
                var f = typeof(DreamTutorialDirector).GetField("_backAttempts", BindingFlags.Instance | BindingFlags.NonPublic);
                if (f != null) f.SetValue(dream, 3);
            }
            Directory.CreateDirectory(Shots);
            _clicks = _refused = _oldRefused = _shots = _pulledBack = 0;
            _bestProgress = _lastProgress = DreamRoad.Progress(dream.Hero.transform.position);
            _nextClick = _nextShot = 0f;
            _started = Time.realtimeSinceStartup;
            _target = dream.Hero.transform.position;
            Trace.Clear();
            _running = true;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            return $"пошёл: стадия {DreamTutorialDirector.StageName}, мост {(DreamRoad.BridgeRaised ? "поднят" : "опущен")}, путь {_lastProgress:0} м";
        }

        private static void Tick()
        {
            var dream = DreamTutorialDirector.Instance;
            if (!Application.isPlaying || dream == null || dream.Hero == null) { Stop("сон кончился"); return; }
            if (EditorApplication.isPaused) return;
            float now = Time.realtimeSinceStartup;
            var hero = dream.Hero;
            float s = DreamRoad.Progress(hero.transform.position);
            // Рука сна тянет назад: путь резко убыл, хотя игрок щёлкал вперёд по стрелке.
            if (s < _lastProgress - 6f) { _pulledBack++; Trace.Append($"[назад {_lastProgress:0}→{s:0}] "); }
            _lastProgress = s;
            _bestProgress = Mathf.Max(_bestProgress, s);

            // Как игрок: обзор за Греховодом (кадры 26.11 без этого теряли его в тёмном лесу).
            if (now >= _nextPan)
            {
                _nextPan = now + 1f;
                var cam = Camera.main != null ? Camera.main.GetComponent<RTS_Camera>() : null;
                if (cam != null) cam.Overlook(hero.transform.position);
            }
            if (now >= _nextShot)
            {
                _nextShot = now + 4f;
                try { Utilets.Snapshot.Now(Shots, $"рука {_shots:00} — {DreamTutorialDirector.StageName}"); _shots++; } catch { }
            }
            if (now - _started > 150f) { Stop("150 с вышли"); return; }
            if (now < _nextClick) return;
            _nextClick = now + 1.5f;

            // Куда показывает стрелка: по лесной тропе, пока мост поднят; дошёл до конца тропы — к засаде.
            var next = DreamRoad.BridgeRaised ? DreamRoad.Detour(hero.transform.position) : null;
            Vector3 goal = next ?? DreamRoad.AmbushAt;
            if ((goal - _target).sqrMagnitude < 0.25f && Flat(hero.transform.position - goal) > 2.5f) return;   // ещё идёт к прежней
            if (Flat(hero.transform.position - DreamRoad.AmbushAt) < 4f) { Stop("дошёл до засады"); return; }
            Click(dream, hero, goal);
        }

        private static void Click(DreamTutorialDirector dream, Component hero, Vector3 goal)
        {
            _clicks++;
            // Старое правило (до 26.11): коридор мерился от главного пути, «позади» — всё западнее опушки.
            if ((DreamRoad.OffRoute(goal) > 8f) || goal.x < DreamRoad.StarAt.x - 3f) _oldRefused++;
            if (!DreamTutorialDirector.PermitDestination(goal)) { _refused++; Trace.Append($"[отказ {goal.x:0},{goal.z:0}] "); return; }
            var mover = hero.GetComponent<UnitMover>();
            if (mover != null) mover.CommandMove(goal);
            _target = goal;
        }

        private static void Stop(string why)
        {
            if (!_running) return;
            _running = false;
            EditorApplication.update -= Tick;
            Trace.Append("— " + why);
        }

        private static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }
    }
}
