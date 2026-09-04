// Assets/Scripts/Gameplay/EscapeZone.cs
using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Край карты, куда игрок уводит отряд на доле 5.
    ///
    /// Побег — не катсцена, а механика отбора: дальше идут только те, кого
    /// успели довести (docs/09-PROLOGUE.md §4, сцена 5). Кто замешкался,
    /// тот остался, и в склеп войдёт отряд поменьше.
    ///
    /// Считаем по расстоянию, а не по триггеру. Коллайдеров и Rigidbody
    /// у воинов нет, а событий OnTrigger между двумя статичными телами
    /// Unity не шлёт вовсе: побег молча не срабатывал бы, и это был бы
    /// худший вид отказа — тот, о котором никто не узнает.
    ///
    /// Отсчёт начинается, когда в круг входит первый. Он и есть цена:
    /// уводить надо не одного, а всех, и времени на это меньше, чем
    /// хочется.
    /// </summary>
    public class EscapeZone : MonoBehaviour
    {
        [Tooltip("Радиус круга, в котором отряд считается выведенным.")]
        [SerializeField] private float _radius = 7f;

        [Tooltip("Сколько секунд ждут остальных после того, как первый дошёл.")]
        [SerializeField] private float _countdown = 9f;

        [Tooltip("Как часто пересчитывать, кто в круге. Раз в кадр не нужно.")]
        [SerializeField] private float _pollSeconds = 0.25f;

        /// <summary>Зона в сцене одна.</summary>
        public static EscapeZone Active { get; private set; }

        /// <summary>
        /// Итог отбора переживает уничтожение самой зоны.
        ///
        /// Спавнер снимает состав отряда в OnDestroy, и порядок уничтожения
        /// объектов Unity не гарантирует: умри зона первой — спавнер увидел
        /// бы пустоту и унёс дальше всех живых, молча отменив отбор. Ровно
        /// тот отказ, которого в этом проекте боятся больше прочих: ничего
        /// не падает, просто механика перестаёт существовать.
        /// </summary>
        public static bool SelectionMade { get; private set; }

        public static IReadOnlyList<string> EscapedNames => _escapedNames;
        private static readonly List<string> _escapedNames = new();

        public bool Departing { get; private set; }

        private readonly HashSet<Warrior> _inside = new();
        private float _leftAt = -1f;
        private float _nextPoll;
        private bool _warned;

        void Awake()
        {
            if (Active == null) Active = this;

            // Новая сцена — новый отбор. Иначе прошлый результат утёк бы
            // в следующую долю и увёл не тех.
            SelectionMade = false;
            _escapedNames.Clear();
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        /// <summary>Успел ли этот воин к краю.</summary>
        public bool Holds(Warrior warrior)
            => warrior != null && _inside.Contains(warrior);

        /// <summary>Те, кто пойдёт дальше.</summary>
        public IEnumerable<Warrior> Escaped()
        {
            foreach (var w in _inside)
                if (w != null && !w.IsDead) yield return w;
        }

        void Update()
        {
            if (Departing) return;
            if (Time.time < _nextPoll) return;
            _nextPoll = Time.time + _pollSeconds;

            Recount();

            if (_inside.Count == 0) { _leftAt = -1f; return; }

            // Первый дошёл — пошёл отсчёт. Об этом говорим вслух: молчаливый
            // таймер игрок не поймёт и решит, что отряд бросили просто так.
            if (_leftAt < 0f)
            {
                _leftAt = Time.time;
                Log("Отряд уходит. Кто не успеет — останется.");
                return;
            }

            if (Time.time - _leftAt >= _countdown) Depart();
        }

        private void Recount()
        {
            _inside.Clear();

            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
            {
                if (w == null || w.IsDead || w.Team != Team.Player) continue;

                var here = transform.position;
                var there = w.transform.position;

                // По плоскости: высота к побегу отношения не имеет.
                float dx = here.x - there.x;
                float dz = here.z - there.z;

                if (dx * dx + dz * dz <= _radius * _radius) _inside.Add(w);
            }
        }

        private void Depart()
        {
            Departing = true;

            int left = 0;
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
                if (w != null && !w.IsDead && w.Team == Team.Player && !_inside.Contains(w)) left++;

            if (left > 0) Log(left == 1 ? "Одного не дождались." : $"Не дождались: {left}.");

            _escapedNames.Clear();
            foreach (var w in _inside)
                if (w != null && !w.IsDead) _escapedNames.Add(w.DisplayName);

            SelectionMade = true;

            Debug.Log($"[ПОБЕГ] Ушли {_escapedNames.Count}, остались {left}.");

            var director = Object.FindFirstObjectByType<PrologueDirector>();
            if (director != null) director.LeaveNow("Отряд ушёл с поля.");
        }

        private void Log(string line)
        {
            if (_warned && line.StartsWith("Отряд уходит")) return;
            if (line.StartsWith("Отряд уходит")) _warned = true;

            var log = Object.FindFirstObjectByType<UI.BattleLogUI>();
            if (log != null) log.Write(line);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.9f, 0.5f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, _radius);
        }
    }
}
