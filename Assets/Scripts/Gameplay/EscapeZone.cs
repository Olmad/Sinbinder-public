// Assets/Scripts/Gameplay/EscapeZone.cs
// Перевод: текст через Loc
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Sinbinder.Core;
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

        /// <summary>
        /// Кто остался прикрывать отход — пусто, если никто (решение автора,
        /// 30 сентября: «сделать задумкой»; `docs/09-PROLOGUE.md` сцена 5:
        /// «Семеро уходят. Карган остаётся»).
        ///
        /// Телохранитель, отказавший приказу уходить, пока круг открыт, говорит,
        /// что остаётся и выиграет время. Не постановка: реплика идёт только
        /// за настоящим отказом движка (правило пролога, §2), и какой отказ —
        /// такая реплика: позвали издали — «криком не уведёте», вблизи —
        /// «я не бегу». Дошёл до него игрок и он послушался — уходит со всеми.
        /// </summary>
        public static string Rearguard { get; private set; }

        /// <summary>
        /// Оставшийся прикрывать пал до ухода. Тогда и журнал, и чёрный экран
        /// говорят «пал, прикрывая отход», а не молчат: до 1 октября смерть
        /// стирала <see cref="Rearguard"/>, и «выиграю вам время» пропадало
        /// бесследно (docs/41-SHOWCASE.md, п. 11).
        /// </summary>
        public static bool RearguardFell { get; private set; }

        private Warrior _rearguard;

        /// <summary>Сигнал по павшему уже прозвучал — второй раз не звучит.</summary>
        private bool _mourned;
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
            Rearguard = null;
            RearguardFell = false;
            _mourned = false;

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

            // Круг выхода загорается, и над ним встаёт метка: «нужно бежать»
            // без «куда» оставляло игрока в лагере (автор, 26 сентября).
            Lamps(true);
            UI.ExitMarker.Show(this);
        }

        void Start()
        {
            // Запертый круг тлеет без света: свет — знак, что уходить пора.
            Lamps(Open);
            if (Open) UI.ExitMarker.Show(this);

            if (AOS.AOSEventHub.Instance != null) AOS.AOSEventHub.Instance.OnRefusal += Refused;
        }

        /// <summary>
        /// Отказ приказу уходить, пока круг открыт. Телохранитель (в составе —
        /// «не отходит от вас», по нему находят, а не по имени) остаётся
        /// и говорит об этом — один раз за побег.
        /// </summary>
        private void Refused(Warrior w, AOS.Decision decision, AOS.DecisionContext context)
        {
            if (!Open || Departing || !string.IsNullOrEmpty(Rearguard)) return;
            if (w == null || w.IsDead || w.Team != Team.Player || w is SinbinderPlayer) return;
            if (context == null || !(context.CommandIsFallBack || context.CommandType == "Move")) return;
            if (!SquadRoster.TryGet(w.DisplayName, out var m) || string.IsNullOrEmpty(m.Unavailable)) return;
            if (Within(w.transform.position)) return;

            Rearguard = w.DisplayName;
            _rearguard = w;
            Debug.Log($"[ПОБЕГ] Прикрывать отход остался {w.DisplayName} (приказ {(context.CommandVolume < 1f ? "издали" : "вблизи")}).");

            string name = Loc.Name(w.DisplayName);
            Herald.Line(context.CommandVolume < 1f
                ? Loc.F("{0}: «Криком меня не уведёте, владыка. Идите — я останусь здесь и выиграю вам время».", name)
                : Loc.F("{0}: «Я не бегу от них, владыка. Уходите — я выиграю вам время».", name));

            // «Выиграю время» — делом, а не словом: он зовёт охотников на себя
            // (Provocation, решение автора 30 сентября). Со щитом — закрывается.
            Provocation.Begin(w);
            Log(Provocation.HasShield(w)
                ? Loc.F("{0} поднимает щит и зовёт их на себя.", name)
                : Loc.F("{0} зовёт их на себя.", name));
        }

        /// <summary>Огни ворот, если они у края есть.</summary>
        private void Lamps(bool on)
        {
            foreach (var l in GetComponentsInChildren<Light>(true)) l.enabled = on;
        }

        void OnDestroy()
        {
            if (AOS.AOSEventHub.Instance != null) AOS.AOSEventHub.Instance.OnRefusal -= Refused;

            // Сцена кончилась — зов кончился с ней.
            Provocation.Clear();

            if (Active == this) Active = null;
        }

        void Update()
        {
            // Пал, пока прикрывал: миг, а не строка при уходе. Журнал скажет
            // «пал, прикрывая отход» в конце, а музыка — сейчас: «Whisper
            // of the Fallen» автора (docs/43-SOUND.md §6). Один раз.
            if (_rearguard != null && !_mourned && _rearguard.IsDead)
            {
                _mourned = true;
                Audio.Music.Play(Audio.Cue.Fallen);
            }

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
                    Log(Loc.T("Уходить рано: лагерь ещё держится."));
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
                Log(Loc.T("Отряд уходит. Кто не успеет — останется."), warning: true);
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

            // Оставшийся прикрывать — не «не дождались»: он сам остался,
            // и журнал называет его, а не считает.
            bool covering = false;
            if (!string.IsNullOrEmpty(Rearguard))
                foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
                    if (w != null && !w.IsDead && w.DisplayName == Rearguard && !_inside.Contains(w)) { covering = true; break; }
            // Пал, пока прикрывал: это тоже ответ на «выиграю вам время», и он
            // звучит. Оставшийся прикрывать, но вошедший в круг, — ушёл со всеми.
            RearguardFell = !covering && !string.IsNullOrEmpty(Rearguard)
                         && (_rearguard == null || _rearguard.IsDead);
            if (!covering && !RearguardFell) Rearguard = null;
            if (covering)
            {
                left--;
                Log(Loc.F("{0} остался прикрывать отход.", Loc.Name(Rearguard)));
            }
            else if (RearguardFell)
                Log(Loc.F("{0} пал, прикрывая отход.", Loc.Name(Rearguard)));

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

            StartCoroutine(Farewell(Object.FindFirstObjectByType<PrologueDirector>()));
        }

        /// <summary>Сколько камера отъезжает над кругом.</summary>
        private const float ShotSeconds = 3.5f;

        /// <summary>Строка на чёрном между лагерем и склепом.</summary>
        private static readonly string Dawn = Loc.N("К рассвету они вышли к старому склепу.");

        /// <summary>
        /// Уход с поля — роликом, как в Warcraft 3. Слово автора, 27 сентября:
        /// «сильно не хватает какого-то перехода от побега к склепу». До того
        /// отсчёт кончался, и склеп открывался следующим же кадром.
        ///
        /// Мир встаёт стоп-кадром, поднимаются полосы, камера отъезжает над
        /// кругом в сторону ночи; затем кадр гаснет строкой — и склеп, который
        /// открывается своей: «Кто-то уже занял этот склеп». Стоп-кадр —
        /// не для красоты: список ушедших уже снят, и смерть под роликом
        /// разошлась бы с ним.
        /// </summary>
        private IEnumerator Farewell(PrologueDirector director)
        {
            // Греховод пал в тот же миг — конец игры главнее ухода. Иначе
            // экран «Греховод пал» остановил бы мир насовсем, а склеп
            // открылся бы под этой остановкой и стоял бы паузой вечно
            // (прогон 27 сентября: «пауза True, насовсем True»).
            if (SinbinderPlayer.Instance != null && SinbinderPlayer.Instance.IsDead) yield break;

            Core.GamePauseController.Instance?.Pause();

            var cam = Camera.main;
            var rig = cam != null ? cam.GetComponent<RTS_Camera>() : null;
            if (rig != null) rig.enabled = false;

            var bars = UI.Letterbox.Instance;
            if (bars != null)
            {
                bars.Show();
                bars.Say(null, Loc.T("Лагерь остаётся за спиной."));
            }

            if (cam != null)
            {
                var centre = transform.position;
                var away = new Vector3(centre.x, 0f, centre.z);
                away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.right;

                var from = cam.transform.position;
                var turn = cam.transform.rotation;
                var to = centre - away * 9f + Vector3.up * 7f;
                var look = Quaternion.LookRotation(centre + away * 10f - to);

                for (float t = 0f; t < ShotSeconds; t += Time.unscaledDeltaTime)
                {
                    float k = Mathf.SmoothStep(0f, 1f, t / ShotSeconds);
                    cam.transform.SetPositionAndRotation(Vector3.Lerp(from, to, k),
                                                         Quaternion.Slerp(turn, look, k));
                    yield return null;
                }
            }

            var title = Object.FindFirstObjectByType<UI.PrologueTitleUI>();
            string dawn = Loc.T(Dawn);
            if (!string.IsNullOrEmpty(Rearguard))
                dawn += "\n" + (RearguardFell
                    ? Loc.F("{0} пал, прикрывая отход.", Loc.Name(Rearguard))
                    : Loc.F("{0} остался у лагеря.", Loc.Name(Rearguard)));
            if (title != null) yield return title.Darken(dawn, 0.9f);

            yield return new WaitForSecondsRealtime(2.2f);

            if (director != null) director.LeaveNow(Loc.T("Отряд ушёл с поля."));
        }

        private static string Waited(int left)
        {
            switch (left)
            {
                case 1:  return Loc.T("Одного не дождались.");
                case 2:  return Loc.T("Двоих не дождались.");
                case 3:  return Loc.T("Троих не дождались.");
                case 4:  return Loc.T("Четверых не дождались.");
                default: return Loc.T("Многих не дождались.");
            }
        }

        /// <summary>
        /// Строка в журнал. Предупреждение «отряд уходит» — один раз: его
        /// узнаём по флагу, а не по началу строки — переведённая строка
        /// начиналась бы иначе (docs/38-LANG.md).
        /// </summary>
        private void Log(string line, bool warning = false)
        {
            if (_warned && warning) return;
            if (warning) _warned = true;

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
