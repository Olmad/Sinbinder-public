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

        [Tooltip("Открыт ли край сразу. Снять для доли 4: бежать полагается "
               + "от второй волны, а не вместо первой. Круг откроет тот, "
               + "кто её выпустит.")]
        [SerializeField] private bool _openAtStart = true;

        [Tooltip("Через сколько секунд ПОСЛЕ НАЧАЛА СЦЕНЫ круг откроется сам, "
               + "если его никто не открыл. Запирать демо навсегда нельзя "
               + "ни при какой ошибке сборки. "
               + "Было 180. С 13 сентября подкрепление ждёт, пока соберут "
               + "души (до полутора минут страховки), и три минуты от начала "
               + "сцены могли истечь раньше, чем выйдет вторая волна: край "
               + "открылся бы до того, как бежать стало от кого.")]
        [SerializeField] private float _opensAnyway = 360f;

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

        /// <summary>Открыт ли край. Закрытый круг никого не считает.</summary>
        public bool Open { get; private set; }

        private readonly HashSet<Warrior> _inside = new();
        private float _leftAt = -1f;
        private float _nextPoll;
        private bool _warned;
        private float _sceneStarted;

        /// <summary>
        /// Греховод в круге. В отряд он не входит (<see cref="Recount"/>),
        /// но уход начинает и он: встал у ворот — значит уходим.
        /// </summary>
        private bool _heroAtGate;

        /// <summary>Сказано ли уже, почему ворота заперты. Один раз за сцену.</summary>
        private bool _toldClosed;

        void Awake()
        {
            if (Active == null) Active = this;

            // Новая сцена — новый отбор. Иначе прошлый результат утёк бы
            // в следующую долю и увёл не тех.
            SelectionMade = false;
            _escapedNames.Clear();

            Open = _openAtStart;

            // Именно от начала сцены, а не от запуска игры: Time.time
            // между сценами не сбрасывается, и предохранитель сработал бы
            // сразу — стоило игроку провести в лагере три минуты, что
            // с советом, сундуком и тревогой более чем возможно.
            _sceneStarted = Time.time;
        }

        /// <summary>
        /// Настроить край, поставленный по ходу сцены (<see cref="RaidEvent"/>).
        /// Звать сразу после AddComponent: Awake уже прошёл, а он открыл край
        /// по умолчанию — здесь это решение отменяется.
        /// </summary>
        public void Configure(float radius, bool openAtStart)
        {
            _radius = radius;
            _openAtStart = openAtStart;
            Open = openAtStart;
        }

        /// <summary>
        /// Открыть край. Зовёт тот, после кого бежать уже пора, — вторая
        /// волна Охотников. До неё уйти нельзя: отказ Каргана случается
        /// на отходе, и игрок, ушедший раньше, не увидит продукта демо.
        /// </summary>
        public void Arm()
        {
            if (Open) return;

            Open = true;
            Debug.Log("[ПОБЕГ] Край карты открыт.");

            // Бежать надо туда, где игрок, может, ещё не бывал: в тумане
            // войны край открывается серым — дорога видна, врагов на ней нет.
            FogOfWar.Reveal(transform.position, _radius + 3f);

            // Ворота зажигаются, и над ними встаёт метка: «нужно бежать»
            // без «куда» оставляло игрока в лагере (автор, 26 сентября).
            Lamps(true);
            UI.ExitMarker.Show(this);
        }

        void Start()
        {
            // Запертые ворота стоят тёмными: свет — знак, что уходить пора.
            Lamps(Open);
            if (Open) UI.ExitMarker.Show(this);
        }

        /// <summary>Огни ворот, если они у края есть.</summary>
        private void Lamps(bool on)
        {
            foreach (var l in GetComponentsInChildren<Light>(true)) l.enabled = on;
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

            if (!Open)
            {
                // Открыть должен был кто-то другой. Не открыл — открываем
                // сами и говорим об этом: запертое навсегда демо хуже
                // сцены, сыгранной не по порядку.
                if (_opensAnyway > 0f && Time.time - _sceneStarted >= _opensAnyway)
                {
                    Debug.LogWarning("[ПОБЕГ] Край никто не открыл — открываем сами.");
                    Arm();
                    return;
                }

                // Пришёл к запертым воротам — сказать почему, а не молчать:
                // тишина читается как «здесь не выход», и выход ищут дальше.
                if (!_toldClosed && SinbinderPlayer.Exists && Within(SinbinderPlayer.Where))
                {
                    _toldClosed = true;
                    Log("Уходить рано: лагерь ещё держится.");
                }
                return;
            }

            if (Time.time < _nextPoll) return;
            _nextPoll = Time.time + _pollSeconds;

            Recount();

            if (_inside.Count == 0 && !_heroAtGate) { _leftAt = -1f; return; }

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
            _heroAtGate = false;

            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
            {
                if (w == null || w.IsDead || w.Team != Team.Player) continue;

                // Греховод — не отряд. Он тоже Warrior и тоже Team.Player,
                // но «довести отряд до края карты» считается по тем, кого
                // ведут, а не по тому, кто ведёт: иначе он попал бы
                // и в список ушедших, и в счёт недождавшихся. Уход он
                // при этом начинает — до 26 сентября Греховод, пришедший
                // к краю первым, стоял там в тишине.
                if (w is SinbinderPlayer)
                {
                    _heroAtGate = Within(w.transform.position);
                    continue;
                }

                if (Within(w.transform.position)) _inside.Add(w);
            }
        }

        /// <summary>В круге ли точка. По плоскости: высота к побегу отношения не имеет.</summary>
        private bool Within(Vector3 there)
        {
            var here = transform.position;
            float dx = here.x - there.x;
            float dz = here.z - there.z;
            return dx * dx + dz * dz <= _radius * _radius;
        }

        private void Depart()
        {
            Departing = true;

            int left = 0;
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
                if (w != null && !w.IsDead && w.Team == Team.Player
                    && !(w is SinbinderPlayer) && !_inside.Contains(w)) left++;

            // Словами, а не числом: игрок чисел не видит. До 24 сентября
            // здесь было «Не дождались: 3.» — единственная цифра в журнале.
            if (left > 0) Log(Waited(left));

            // Лагерь брошен: что осталось в сундуке, досталось охотникам.
            TrophyChest.Abandon();

            _escapedNames.Clear();
            foreach (var w in _inside)
            {
                if (w == null || w.IsDead) continue;

                _escapedNames.Add(w.DisplayName);

                // «Беглец» стоит на Escape, и писать его было некому.
                // Уйти живым с поля — деяние: не доблесть, но и не ничто,
                // и титул на нём в игре заведён.
                w.Reputation.Deeds.Add(new AOS.DeedRecord
                    { Type = AOS.DeedType.Escape, Importance = 0.3f });
                AOS.TitleManager.UpdateTitle(w);
            }

            SelectionMade = true;

            Debug.Log($"[ПОБЕГ] Ушли {_escapedNames.Count}, остались {left}.");

            var director = Object.FindFirstObjectByType<PrologueDirector>();
            if (director != null) director.LeaveNow("Отряд ушёл с поля.");
        }

        private static string Waited(int left)
        {
            switch (left)
            {
                case 1:  return "Одного не дождались.";
                case 2:  return "Двоих не дождались.";
                case 3:  return "Троих не дождались.";
                case 4:  return "Четверых не дождались.";
                default: return "Многих не дождались.";
            }
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
