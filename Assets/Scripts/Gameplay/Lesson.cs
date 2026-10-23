// Assets/Scripts/Gameplay/Lesson.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>Глагол, которому учит урок.</summary>
    public enum LessonKind
    {
        /// <summary>Жатва: E у угасающей души. Первая душа первой волны.</summary>
        Harvest,

        /// <summary>
        /// Разговор: F у своего воина — меню «как ты / долг / снаряжение».
        /// Первый раз после сундука; когда Карган сказал о долге — у должника
        /// (решение автора, 30 сентября). Имя прежнее: так урок записан в сохранениях.
        /// </summary>
        Exchange,

        /// <summary>
        /// Вселение: душа из сумы — в гнездо, тело со стола — в ложе, рычаг.
        /// Мастерская склепа демо (<see cref="Crypt.CryptWorkshop"/>), первый раз,
        /// когда поднять есть из кого. Отдельный урок — ответ автора 28.09.
        /// </summary>
        Raising,

        /// <summary>
        /// Спина: один свой держит врага лицом к себе, второй свободен — его
        /// посылают за спину врага, на отметку. Решение автора, 15 октября:
        /// «Спину показать в обучении» (docs/08-FLOOR.md §8). Удар в спину
        /// с 28 августа бил в полтора раза сильнее, а игрок не знал, что так
        /// можно: это первый тактический глагол — «одним сковать, вторым обойти».
        /// </summary>
        Backstab,
    }

    /// <summary>
    /// Урок стоп-кадром (docs/36-LESSONS.md). Предложение автора, 24 сентября:
    /// «весь мир ставится на паузу, вокруг души поле, в котором может
    /// двигаться игрок, и сбоку плашка-подсказка».
    ///
    /// Миг, когда игроку впервые нужен новый глагол: мир замирает, вокруг
    /// цели на земле — кольцо, сбоку — плашка с клавишей. Урок кончается
    /// делом, а не кнопкой «Понятно»: собрал душу — мир пошёл дальше.
    ///
    /// <b>Мир держит время, а не каждый по отдельности.</b> В замысле
    /// (§4) было иначе: спросить остановку у воинов, ног, ударов, угасания,
    /// аниматоров. Это три десятка мест, и забытое место — это душа, которая
    /// гаснет на уроке, или удар в замершем бою. Здесь наоборот: время стоит
    /// для всех (<see cref="GamePauseController.Hold"/>), а по реальному
    /// времени идёт только Греховод и то, что нужно игроку, — ходьба,
    /// камера, панели. Забытое так остаётся стоять, а не бежит.
    ///
    /// <b>Кольцо стягивается</b> (решение автора, 28 сентября). Греховод
    /// может быть далеко, когда урок начнётся. Не переносим его — подход
    /// к душе и есть урок, — и не рисуем кольцо сразу по нему — на пол-поля
    /// оно ничего не показывает. Кольцо встаёт по Греховоду и только
    /// сжимается за ним: шаг к цели — край подтягивается следом, шаг
    /// назад — упёрся. У цели оно встаёт на <see cref="Inner"/>.
    ///
    /// Один раз за игру (отметка в сохранении) и выключается ступенью
    /// ясности (O, галочка «Уроки»). Пока не проверен в Unity — за
    /// выключателем консоли «уроки».
    ///
    /// Ставит себя сам и живёт между сценами.
    /// </summary>
    public class Lesson : MonoBehaviour
    {
        /// <summary>Радиус кольца у цели, м. Автор: «4–5 метров».</summary>
        public const float Inner = 4.5f;

        /// <summary>
        /// Насколько можно отступить от ближайшего, куда уже дошёл, м.
        /// Путь к душе бывает кривым — вокруг палатки, камня, — и кольцо,
        /// идущее вплотную за спиной, зажало бы Греховода между собой
        /// и препятствием.
        /// </summary>
        public const float Slack = 1.5f;

        /// <summary>Урок обмена: свой воин ближе этого, м.</summary>
        private const float Invite = 6f;

        /// <summary>
        /// Кольцо урока вселения у цели, м. Шире обычного: мастерская —
        /// устройство, полка и стол тел вдоль стены, — и тело со стола
        /// должно быть досягаемо изнутри кольца.
        /// </summary>
        public const float WorkshopInner = 5.5f;

        private static readonly Color SoulColor = new(0.66f, 0.82f, 1f, 0.75f);
        private static readonly Color WarriorColor = new(0.96f, 0.86f, 0.56f, 0.7f);
        private static readonly Color FoeColor = new(0.96f, 0.58f, 0.22f, 0.75f);
        private static readonly Color MarkColor = new(0.96f, 0.94f, 0.88f, 0.95f);

        /// <summary>Урок спины: насколько за спиной врага отметка, м.</summary>
        private const float Behind = 1.6f;

        /// <summary>Урок спины: свободный свой не дальше этого от врага, м.</summary>
        private const float FlankReach = 12f;

        /// <summary>Урок спины: приказ идти ближе этого к отметке — «за спину», м.</summary>
        private const float MarkReach = 2.5f;

        /// <summary>
        /// Урок спины не встаёт раньше этого после конца прошлого урока, секунд
        /// реального времени: два стоп-кадра подряд — та самая «ядерная физика
        /// за пять секунд» (автор, 10 октября).
        /// </summary>
        private const float Gap = 10f;

        // ──────────────────────────────────
        // Кому и когда
        // ──────────────────────────────────

        private static Lesson _instance;
        private static readonly HashSet<LessonKind> _given = new();

        /// <summary>
        /// Выключатель консоли «уроки». Урок держит мир, и держащий мир
        /// код, не проверенный в игре, — это риск остановить демо насовсем.
        /// Прогон со «все» его проходит; с 22 октября включён по умолчанию:
        /// автор, п. 9 отзыва 21.10 — «не увидел обучения и плашек с подсказками».
        /// Выключается галочкой на O и в консоли.
        /// </summary>
        public static bool Switch { get; set; } = true;

        /// <summary>Идёт ли урок сейчас и какой.</summary>
        public static LessonKind? Now => _instance != null ? _instance._now : null;

        /// <summary>Идёт ли урок: мир стоит, Греховод ходит.</summary>
        public static bool Holding => Now.HasValue;

        /// <summary>Разрешены ли уроки: выключатель и ступень ясности.</summary>
        public static bool Allowed => Switch && Transparency.Shows(Detail.Lessons);

        /// <summary>Был ли уже этот урок в этой игре.</summary>
        public static bool Given(LessonKind kind) => _given.Contains(kind);

        /// <summary>
        /// Урок ещё впереди. Спрашивает подсказка о жатве: пока урок жатвы
        /// впереди, первую душу объясняет он, а подсказка ждёт второй.
        /// </summary>
        public static bool Owes(LessonKind kind)
            => Allowed && !_given.Contains(kind) && SinbinderPlayer.Exists
            && GamePauseController.Instance != null;   // без него мир не остановить — урока не будет

        /// <summary>
        /// Шаг времени для того, что на уроке идёт: реальный, пока мир стоит.
        /// Панель поверх урока (меню, вещи, ясность) — ноль, как на любой
        /// паузе: иначе Греховод ходил бы под открытым экраном.
        /// </summary>
        public static float Delta => Holding && !Paused ? Time.unscaledDeltaTime : Time.deltaTime;

        private static bool Paused
            => GamePauseController.Instance != null && GamePauseController.Instance.IsPaused;

        /// <summary>Идёт ли Греховод по дороге урока — для ног его модели.</summary>
        public static bool HeroWalking => _instance != null && _instance._walking;

        /// <summary>Середина кольца. Автопрогону: куда вести Греховода.</summary>
        public static Vector3 Target => _instance != null && _instance._leash != null
            ? _instance._leash.Center : Vector3.zero;

        /// <summary>Воин урока обмена. Автопрогону: с кем говорить.</summary>
        public static Warrior Partner => _instance != null ? _instance._partner : null;

        /// <summary>Урок спины: кого послать за спину. Автопрогону.</summary>
        public static Warrior Flanker => _instance != null ? _instance._flanker : null;

        /// <summary>Урок спины: отметка за спиной врага. Автопрогону: куда послать.</summary>
        public static Vector3 Mark => _instance != null ? _instance._mark : Vector3.zero;

        /// <summary>Радиус кольца сейчас. Самопроверке и автопрогону.</summary>
        public static float Radius => _instance != null && _instance._leash != null
            ? _instance._leash.Radius : 0f;

        /// <summary>Новая игра: уроки впереди снова.</summary>
        public static void Forget()
        {
            _given.Clear();
            _backTold = false;
            if (_instance != null) _instance.End();
        }

        /// <summary>Какие уроки были — в запись (<see cref="SaveGame.Lessons"/>).</summary>
        public static List<string> Saved()
        {
            var list = new List<string>();
            foreach (var k in _given) list.Add(k.ToString());
            list.Sort(System.StringComparer.Ordinal);
            return list;
        }

        /// <summary>Уроки из записи. Незнакомое имя пропускаем: запись могла быть новее.</summary>
        public static void Restore(List<string> saved)
        {
            _given.Clear();
            if (saved == null) return;
            foreach (var s in saved)
                if (System.Enum.TryParse(s, out LessonKind k)) _given.Add(k);
        }

        // ──────────────────────────────────
        // Кольцо
        // ──────────────────────────────────

        /// <summary>
        /// Поводок: кольцо вокруг цели, которое только стягивается. Чистая
        /// геометрия, без сцены — самопроверка гоняет его отдельно.
        /// Высоту не считает: лагерь на холме, и кольцо — по земле.
        /// </summary>
        public sealed class Leash
        {
            public Vector3 Center { get; }
            public float Radius { get; private set; }

            /// <summary>Радиус у цели: ниже него кольцо не сжимается.</summary>
            public float Floor { get; }

            public Leash(Vector3 center, Vector3 hero, float floor = Inner)
            {
                Center = center;
                Floor = floor;
                Radius = Mathf.Max(Floor, Flat(hero) + Slack);
            }

            /// <summary>Расстояние до середины по земле.</summary>
            public float Flat(Vector3 p)
            {
                var d = p - Center;
                d.y = 0f;
                return d.magnitude;
            }

            /// <summary>
            /// Шаг, который кольцо пропустит. Наружу — срезается по окружности:
            /// Греховод скользит вдоль края, а не встаёт как вкопанный.
            /// </summary>
            public Vector3 Rein(Vector3 from, Vector3 step)
            {
                // Оказался снаружи не своим шагом (перенос прогоном, толчок) —
                // кольцо встаёт по нему, а не тянет его рывком к себе.
                Radius = Mathf.Max(Radius, Flat(from));

                var to = from + step;
                var off = to - Center;
                off.y = 0f;
                if (off.magnitude > Radius)
                {
                    off = off.normalized * Radius;
                    to = new Vector3(Center.x + off.x, to.y, Center.z + off.z);
                }

                Tighten(to);
                return to - from;
            }

            /// <summary>Подтянуть край за Греховодом. Назад кольцо не расходится.</summary>
            public void Tighten(Vector3 at)
                => Radius = Mathf.Max(Floor, Mathf.Min(Radius, Flat(at) + Slack));
        }

        /// <summary>
        /// Шаг Греховода на уроке — через кольцо. Нет урока — шаг как есть.
        /// Зовут ходьба с клавиш (<see cref="PlayerWalk"/>) и дорога урока.
        /// </summary>
        public static Vector3 Rein(Vector3 from, Vector3 step)
        {
            if (!Holding || _instance._leash == null) return step;
            return _instance._leash.Rein(from, step);
        }

        /// <summary>
        /// Приказ Греховоду идти, отданный на уроке (<see cref="UnitMover.CommandMove"/>).
        /// Агент стоит вместе с миром, поэтому дорогу считаем сами и ведём
        /// по ней по реальному времени. false — урока нет, пусть ведёт агент.
        /// </summary>
        public static bool WalkTo(Vector3 point)
        {
            if (!Holding) return false;
            _instance.Road(point);
            return true;
        }

        // ──────────────────────────────────
        // Урок
        // ──────────────────────────────────

        private LessonKind? _now;
        private Leash _leash;
        private FadingSoul _soul;
        private Warrior _partner;
        private Crypt.BindingDevice _device;
        private bool _talked;

        // Урок спины: кто держит врага, кто его обходит, куда идти.
        private Warrior _foe, _pinner, _flanker;
        private Vector3 _mark;
        private PendingCommand _flankerWas;
        private bool _frontal;
        private float _endedAt = -999f;

        private readonly List<Vector3> _road = new();
        private int _roadAt;
        private bool _walking;

        private Animator _heroBody;
        private AnimatorUpdateMode _heroWas;

        private float _nextLook;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm()
        {
            _instance = null;
            _given.Clear();
            _backTold = false;
            Switch = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;

            var go = new GameObject("Урок");
            _instance = go.AddComponent<Lesson>();
            DontDestroyOnLoad(go);
        }

        void OnEnable() => SceneManager.sceneLoaded += OnScene;

        void OnDisable() => SceneManager.sceneLoaded -= OnScene;

        void OnDestroy()
        {
            End();
            if (_instance == this) _instance = null;
        }

        /// <summary>Сцена сменилась под уроком — цель осталась в старой.</summary>
        private void OnScene(Scene scene, LoadSceneMode mode) => End();

        void Update()
        {
            if (_now.HasValue) { Hold(); return; }

            // Искать повод раз в пятую долю секунды: поиск душ и воинов
            // каждый кадр ни к чему.
            if (Time.unscaledTime < _nextLook) return;
            _nextLook = Time.unscaledTime + 0.2f;

            if (!Allowed || !Calm()) return;

            if (!_given.Contains(LessonKind.Harvest)) TryHarvest();
            if (!_now.HasValue && !_given.Contains(LessonKind.Backstab)) TryBackstab();
            if (!_now.HasValue && !_given.Contains(LessonKind.Exchange)) TryExchange();
            if (!_now.HasValue && !_given.Contains(LessonKind.Raising)) TryRaising();
        }

        /// <summary>
        /// Можно ли сейчас остановить мир. Не поверх разговора, панели,
        /// заставки или конца: там руки игрока уже заняты другим.
        /// </summary>
        private static bool Calm()
        {
            var pause = GamePauseController.Instance;
            if (pause == null || pause.IsPaused || pause.Halted) return false;

            var hero = SinbinderPlayer.Instance;
            if (hero == null || hero.IsDead) return false;

            var talk = Dialogue.DialogueCameraController.Instance;
            if (talk != null && talk.InDialogue) return false;

            return !UI.PrologueTitleUI.Showing && !UI.GearPanel.Open;
        }

        /// <summary>Жатва: первая угасающая душа — ближайшая к Греховоду.</summary>
        private void TryHarvest()
        {
            var souls = SoulManager.Instance;
            if (souls == null || souls.FadingCount == 0) return;

            var hero = SinbinderPlayer.Where;
            FadingSoul best = null;
            float bestDist = float.MaxValue;
            foreach (var s in souls.GetAllFadingSouls())
            {
                if (s == null) continue;
                float d = Vector3.Distance(hero, s.Position);
                if (d < bestDist && Reachable(s.Position)) { bestDist = d; best = s; }
            }

            // Урок, которого нельзя пройти, запер бы игру: мир стоит, пока
            // душа не собрана. Нет банки или дороги — миг урока прошёл,
            // первую душу объяснит подсказка, как без уроков.
            if (best == null || Satchel.FreeJar() < 0)
            {
                Debug.LogWarning("[УРОК] Жатва: " + (best == null
                    ? "до угасающей души нет дороги" : "свободной банки нет")
                    + " — урока не будет, объяснит подсказка.");
                _given.Add(LessonKind.Harvest);
                return;
            }

            _soul = best;
            souls.OnSoulHarvested += Harvested;
            Begin(LessonKind.Harvest, best.Position);
        }

        /// <summary>
        /// Обмен вещью: сундук разобран, в мешке есть что отдать, свой воин
        /// рядом и боя нет. Обмен — первый глагол «искушай», и его стоит
        /// поставить на свет, а не оставить строке «F — поговорить».
        /// </summary>
        private void TryExchange()
        {
            if (!TrophyChest.Looted || RaidEvent.Running) return;

            var souls = SoulManager.Instance;
            if (souls != null && souls.FadingCount > 0) return;

            var hero = SinbinderPlayer.Where;

            // Карган сказал о долге — урок у должника: «игрок подходит, и тут
            // обучение диалогу» (автор, 30 сентября). Подошёл к другому — урок
            // подождёт; побежал к шару — не будет вовсе, пока не подойдёт.
            if (CampOpening.DebtWarned)
            {
                var debtor = CampOpening.Debtor();
                if (debtor != null)
                {
                    if (Vector3.Distance(hero, debtor.transform.position) > Invite
                        || !Reachable(debtor.transform.position)) return;
                    _partner = debtor;
                    _talked = false;
                    Begin(LessonKind.Exchange, debtor.transform.position);
                    return;
                }
            }

            var bag = Inventory.PlayerInventory.Instance;
            if (bag == null || bag.Count == 0) return;

            Warrior best = null;
            float bestDist = Invite;
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
            {
                if (w == null || w is SinbinderPlayer || w.IsDead || w.Team != Team.Player) continue;
                float d = Vector3.Distance(hero, w.transform.position);
                if (d < bestDist && Reachable(w.transform.position)) { bestDist = d; best = w; }
            }
            if (best == null) return;

            _partner = best;
            _talked = false;
            Begin(LessonKind.Exchange, best.transform.position);
        }

        /// <summary>
        /// Вселение: мастерская склепа открыта и ждёт — в суме есть душа,
        /// которую примет хоть одно тело (<see cref="Crypt.CryptWorkshop.Waiting"/>).
        /// Цепочка длинная — пять шагов, — и без урока игрок видит стол
        /// с телами и не знает, с какого конца браться.
        /// </summary>
        private void TryRaising()
        {
            var device = Crypt.CryptWorkshop.Device;
            if (!Crypt.CryptWorkshop.Waiting || device == null) return;
            if (!Reachable(device.transform.position)) return;

            _device = device;
            Begin(LessonKind.Raising, device.transform.position, WorkshopInner);
        }

        /// <summary>
        /// Спина: враг дерётся с одним своим лицом к лицу, а второй свой свободен,
        /// стоит недалеко и спереди или сбоку — показать, что его можно послать
        /// за спину. Враг — первый по порядку сцены, обходящий — самый негордый
        /// из свободных (гордый в спину не бьёт: <c>PrideModule</c>, и урок
        /// кончился бы отказом), при равных — ближний. Ничего случайного.
        /// </summary>
        private void TryBackstab()
        {
            if (Time.unscaledTime - _endedAt < Gap) return;

            foreach (var foe in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
            {
                if (foe == null || foe.IsDead || foe.Team != Team.Enemy || FogOfWar.Hides(foe)) continue;

                var pinner = Pinner(foe);
                if (pinner == null) continue;

                var behind = foe.transform.position - foe.transform.forward * Behind;
                if (!NavMesh.SamplePosition(behind, out var spot, 1.5f, NavMesh.AllAreas)) continue;
                if (!Facing.IsFromBehind(foe.transform, spot.position)) continue;

                var flanker = Free(foe, pinner, spot.position);
                if (flanker == null) continue;

                _foe = foe;
                _pinner = pinner;
                _flanker = flanker;
                _mark = spot.position;
                _flankerWas = flanker.Command;
                _frontal = false;

                Begin(LessonKind.Backstab, foe.transform.position);
                ShowMark();
                return;
            }
        }

        /// <summary>Свой, который дерётся с врагом спереди или сбоку — держит его.</summary>
        private static Warrior Pinner(Warrior foe)
        {
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
            {
                if (!Ours(w)) continue;
                var engagement = w.GetComponent<Engagement>();
                if (engagement == null || !engagement.IsEngaged) continue;
                if (Vector3.Distance(w.transform.position, foe.transform.position) > 2.5f) continue;
                if (Facing.IsFromBehind(foe.transform, w.transform.position)) continue;
                return w;
            }
            return null;
        }

        /// <summary>Свободный свой, который дойдёт до отметки: не в бою, не за спиной уже.</summary>
        private static Warrior Free(Warrior foe, Warrior pinner, Vector3 mark)
        {
            Warrior best = null;
            float bestPride = float.MaxValue, bestDist = float.MaxValue;

            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
            {
                if (!Ours(w) || w == pinner || w.Soul == null) continue;

                var engagement = w.GetComponent<Engagement>();
                if (engagement != null && engagement.IsEngaged) continue;

                float d = Vector3.Distance(w.transform.position, foe.transform.position);
                if (d > FlankReach) continue;
                if (Facing.IsFromBehind(foe.transform, w.transform.position)) continue;

                float pride = w.Soul.Get(SinType.Pride);
                if (pride > bestPride || (Mathf.Approximately(pride, bestPride) && d >= bestDist)) continue;
                if (!Path(w.transform.position, mark)) continue;

                best = w;
                bestPride = pride;
                bestDist = d;
            }
            return best;
        }

        private static bool Ours(Warrior w)
            => w != null && !w.IsDead && w.Team == Team.Player && !(w is SinbinderPlayer);

        /// <summary>Есть ли дорога ногами от одной точки до другой.</summary>
        private static bool Path(Vector3 from, Vector3 to)
        {
            var path = new NavMeshPath();
            return NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path)
                && path.status == NavMeshPathStatus.PathComplete;
        }

        /// <summary>
        /// Урок спины кончается приказом: обходящему велели идти к отметке.
        /// Послушается ли он — решит его душа, как всегда: урок учит глаголу,
        /// а не обещает исполнение. Велели бить врага в лоб — урок не кончен,
        /// плашка говорит, чем это отличается.
        /// </summary>
        private bool Flanked()
        {
            var c = _flanker.Command;
            if (c.Kind == _flankerWas.Kind && c.Point == _flankerWas.Point && c.Target == _flankerWas.Target)
                return false;
            _flankerWas = c;

            bool toMark = (c.Kind == CommandKind.Move || c.Kind == CommandKind.AttackMove)
                       && Flat(c.Point, _mark) <= MarkReach;
            if (toMark) return true;

            _frontal = c.Kind == CommandKind.Attack && c.Target == _foe.gameObject;
            return false;
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        /// <summary>Первый настоящий удар в спину своим — названа ли уже.</summary>
        private static bool _backTold;

        /// <summary>
        /// Удар сзади попал (<see cref="Damageable"/>). Первый раз за игру, когда
        /// бьёт свой, журнал называет, что случилось: урок показал глагол,
        /// а эта строка — что глагол сработал. Работает и без уроков.
        /// </summary>
        public static void BackStruck(GameObject attacker)
        {
            if (_backTold || attacker == null) return;
            var w = attacker.GetComponent<Warrior>();
            if (!Ours(w)) return;

            _backTold = true;
            Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(Loc.F(
                "{0} заходит сзади и бьёт в спину — такой удар сильнее обычного.",
                Loc.Name(w.DisplayName)));
        }

        /// <summary>Дойдёт ли Греховод ногами. Урок без дороги — ловушка.</summary>
        private static bool Reachable(Vector3 point)
        {
            if (!NavMesh.SamplePosition(point, out var hit, 2f, NavMesh.AllAreas)) return false;
            var path = new NavMeshPath();
            return NavMesh.CalculatePath(SinbinderPlayer.Where, hit.position, NavMesh.AllAreas, path)
                && path.status == NavMeshPathStatus.PathComplete;
        }

        private void Begin(LessonKind kind, Vector3 centre, float floor = Inner)
        {
            var hero = SinbinderPlayer.Instance;

            // Один раз за игру — отметка сразу, а не по концу: урок,
            // прерванный сменой сцены, не должен начинаться снова и снова.
            _given.Add(kind);
            _now = kind;
            _leash = new Leash(centre, hero.transform.position, floor);
            _road.Clear();
            _walking = false;

            // Шёл по приказу мышью — дойдёт и на уроке: агент встанет
            // вместе с миром, и дорогу поведём сами.
            var agent = hero.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isOnNavMesh && agent.hasPath)
            {
                Road(agent.destination);
                agent.ResetPath();
            }

            // Тело Греховода живёт по реальному времени, остальные — нет.
            _heroBody = hero.GetComponentInChildren<Animator>();
            if (_heroBody != null)
            {
                _heroWas = _heroBody.updateMode;
                _heroBody.updateMode = AnimatorUpdateMode.UnscaledTime;
            }

            GamePauseController.Instance.Hold(true);

            ShowRing(kind == LessonKind.Exchange ? WarriorColor
                   : kind == LessonKind.Backstab ? FoeColor : SoulColor);
            ShowPlate(true);
            Debug.Log("[УРОК] " + kind + ": мир стоит, кольцо по Греховоду.");
        }

        /// <summary>Урок идёт: кольцо, плашка, дорога, конец.</summary>
        private void Hold()
        {
            var hero = SinbinderPlayer.Instance;

            // Уроки выключили на ходу (O, консоль) или пропало то, ради
            // чего урок, — отпустить мир. Ждать дела, которого не будет,
            // значит остановить игру насовсем.
            if (!Allowed || hero == null || hero.IsDead
                || (_now == LessonKind.Exchange && (_partner == null || _partner.IsDead || RaidEvent.Running))
                || (_now == LessonKind.Harvest && !SoulStillThere())
                || (_now == LessonKind.Raising && _device == null)
                || (_now == LessonKind.Backstab && (_foe == null || _foe.IsDead
                                                    || _flanker == null || _flanker.IsDead)))
            {
                End();
                return;
            }

            if (_now == LessonKind.Exchange && Exchanged()) { End(); return; }
            if (_now == LessonKind.Backstab && Flanked()) { End(); return; }

            // Поднял — выучил. И страховка: поднимать стало не из кого
            // (выключили мастерскую, душу вернули на полку, где её не примет
            // ни одно тело) — урок, ждущий невозможного, остановил бы игру.
            if (_now == LessonKind.Raising && (_device.Raised > 0 || !Crypt.CryptWorkshop.Waiting))
            {
                End();
                return;
            }

            // Панель поверх урока — руки у неё: ни шага, ни плашки поверх экрана.
            if (Paused) { ShowPlate(false); return; }

            Walk(hero);
            _leash.Tighten(hero.transform.position);
            DrawRing();
            ShowPlate(true);
        }

        private bool SoulStillThere()
        {
            var souls = SoulManager.Instance;
            return souls != null && _soul != null && souls.GetAllFadingSouls().Contains(_soul);
        }

        /// <summary>Любая собранная душа кончает урок: глагол выучен.</summary>
        private void Harvested(FadingSoul any)
        {
            if (_now == LessonKind.Harvest) End();
        }

        /// <summary>Поговорил вблизи и закрыл экран — вещь отдана или нет, глагол выучен.</summary>
        private bool Exchanged()
        {
            if (UI.GearPanel.Talking) _talked = true;
            return _talked && !UI.GearPanel.Open;
        }

        private void End()
        {
            if (!_now.HasValue) return;

            _now = null;
            _leash = null;
            _partner = null;
            _device = null;
            _foe = _pinner = _flanker = null;
            _frontal = false;
            _endedAt = Time.unscaledTime;
            _road.Clear();
            _walking = false;

            if (SoulManager.Instance != null) SoulManager.Instance.OnSoulHarvested -= Harvested;
            _soul = null;

            if (_heroBody != null) _heroBody.updateMode = _heroWas;
            _heroBody = null;

            GamePauseController.Instance?.Hold(false);

            if (_ring != null) _ring.enabled = false;
            if (_markRing != null) _markRing.enabled = false;
            ShowPlate(false);
            Debug.Log("[УРОК] конец: мир идёт.");
        }

        // ──────────────────────────────────
        // Дорога по приказу мышью
        // ──────────────────────────────────

        private void Road(Vector3 point)
        {
            _road.Clear();
            _roadAt = 0;

            var from = SinbinderPlayer.Where;
            if (!NavMesh.SamplePosition(point, out var hit, 2f, NavMesh.AllAreas)) return;

            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(from, hit.position, NavMesh.AllAreas, path)
                || path.status == NavMeshPathStatus.PathInvalid) return;

            _road.AddRange(path.corners);
        }

        private void Walk(SinbinderPlayer hero)
        {
            _walking = false;
            if (_roadAt >= _road.Count) return;

            var at = hero.transform.position;
            var to = _road[_roadAt] - at;
            to.y = 0f;
            if (to.magnitude < 0.15f) { _roadAt++; return; }

            var agent = hero.GetComponent<NavMeshAgent>();
            float speed = agent != null && agent.speed > 0f ? agent.speed : 3.5f;
            var step = to.normalized * Mathf.Min(to.magnitude, speed * Time.unscaledDeltaTime);
            var reined = _leash.Rein(at, step);

            // Дорога вывела на кольцо — он скользил бы вдоль края без конца.
            // Встать: дальше путь только к цели, а не мимо неё.
            if (reined.magnitude < step.magnitude * 0.3f) { _road.Clear(); return; }

            if (agent != null && agent.isOnNavMesh) agent.Move(reined);
            else hero.transform.position += reined;

            // Сверху видно, куда он повёрнут. От первого лица курс
            // всё равно задаёт голова (RTS_Camera) — каждый кадр заново.
            if (step.sqrMagnitude > 1e-6f) hero.transform.rotation = Quaternion.LookRotation(step);
            _walking = true;
        }

        // ──────────────────────────────────
        // Кольцо на земле
        // ──────────────────────────────────

        private const int Segments = 96;
        private const float Lift = 0.08f;

        private static Material _material;
        private LineRenderer _ring;
        private float _drawnRadius = -1f;

        private void ShowRing(Color color)
        {
            if (_ring == null)
            {
                // Тот же шейдер, что у круга голоса и кругов выделения:
                // он в списке всегда включаемых в сборку.
                if (_material == null)
                {
                    var shader = Shader.Find("Sprites/Default");
                    if (shader == null)
                    {
                        Debug.LogWarning("[УРОК] Шейдера Sprites/Default нет: кольцо урока "
                                       + "рисовать нечем. Урок идёт и без него, но граница невидима.");
                        return;
                    }
                    _material = new Material(shader);
                }

                var go = new GameObject("Кольцо урока");
                go.transform.SetParent(transform, false);
                _ring = go.AddComponent<LineRenderer>();
                _ring.sharedMaterial = _material;
                _ring.useWorldSpace = true;
                _ring.loop = true;
                _ring.widthMultiplier = 0.1f;
                _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _ring.receiveShadows = false;
                _ring.positionCount = Segments;
            }

            _ring.startColor = _ring.endColor = color;
            _ring.enabled = true;
            _drawnRadius = -1f;
            DrawRing();
        }

        /// <summary>Перерисовать, когда край подтянулся заметно: сотня лучей в землю на каждый кадр ни к чему.</summary>
        private void DrawRing()
        {
            if (_ring == null || _leash == null) return;
            if (Mathf.Abs(_leash.Radius - _drawnRadius) < 0.05f) return;
            _drawnRadius = _leash.Radius;

            var centre = _leash.Center;
            for (int i = 0; i < Segments; i++)
            {
                float a = i / (float)Segments * Mathf.PI * 2f;
                var p = centre + new Vector3(Mathf.Cos(a) * _drawnRadius, 0f, Mathf.Sin(a) * _drawnRadius);
                _ring.SetPosition(i, OnGround(p));
            }
        }

        private LineRenderer _markRing;

        /// <summary>Отметка за спиной врага: светлое кольцо, куда посылать.</summary>
        private void ShowMark()
        {
            if (_material == null) return;   // ShowRing сказал, почему

            if (_markRing == null)
            {
                var go = new GameObject("Отметка урока");
                go.transform.SetParent(transform, false);
                _markRing = go.AddComponent<LineRenderer>();
                _markRing.sharedMaterial = _material;
                _markRing.useWorldSpace = true;
                _markRing.loop = true;
                _markRing.widthMultiplier = 0.08f;
                _markRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _markRing.receiveShadows = false;
                _markRing.positionCount = 32;
            }

            _markRing.startColor = _markRing.endColor = MarkColor;
            for (int i = 0; i < 32; i++)
            {
                float a = i / 32f * Mathf.PI * 2f;
                _markRing.SetPosition(i, OnGround(_mark + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.7f));
            }
            _markRing.enabled = true;
        }

        /// <summary>Точка на том, что под ней: земля, холм, реквизит. Воинов пропускаем.</summary>
        private static Vector3 OnGround(Vector3 p)
        {
            var hits = Physics.RaycastAll(p + Vector3.up * 20f, Vector3.down, 60f);
            System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<Warrior>() != null) continue;
                return hit.point + Vector3.up * Lift;
            }
            return p + Vector3.up * Lift;
        }

        // ──────────────────────────────────
        // Плашка
        // ──────────────────────────────────

        private GameObject _plate;
        private Text _key;
        private Text _words;

        private void ShowPlate(bool on)
        {
            if (!on)
            {
                if (_plate != null) _plate.SetActive(false);
                return;
            }

            if (_plate == null) BuildPlate();
            _plate.SetActive(true);

            var (key, words) = Words();
            _key.text = key;
            _words.text = words;
        }

        /// <summary>
        /// Что сказать. Строки собираются каждый кадр: вид меняется клавишей,
        /// а язык — в меню, и плашка обязана говорить про нынешние.
        /// </summary>
        private (string key, string words) Words()
        {
            if (_now == LessonKind.Harvest)
                return ("E", Loc.T("Подойдите к душе и нажмите E — она ляжет в пустую банку.")
                           + "\n" + Loc.T("Эта душа подождёт. Другие гаснут, пока вы идёте: чем позже, тем меньше от них останется."));

            if (_now == LessonKind.Raising) return RaisingStep();
            if (_now == LessonKind.Backstab) return BackstabStep();

            // Разговор — только от первого лица, вблизи и глядя на воина.
            // Сверху плашка сперва говорит, как посмотреть его глазами;
            // в разговоре — что можно сказать.
            // Ответы теперь стоят списком на экране (TalkView, 22.10), с номерами:
            // перечислять их здесь — второй раз и с ошибкой (без долга
            // снаряжение — второй пункт, а плашка звала его третьим).
            if (UI.GearPanel.Talking)
                return ("1 2 3", Loc.T("Выберите ответ — цифрой или мышью.")
                               + "\n" + Loc.T("F или Esc — уйти."));

            var view = Object.FindFirstObjectByType<RTS_Camera>();
            string name = _partner != null ? Grammar.Dative(_partner.DisplayName, _partner.Gender) : "";
            string approach = Loc.F("Подойдите к {0}, посмотрите на воина и нажмите F — поговорить.", name);
            string why = _partner != null && _partner.UnpaidMissions > 0
                ? Loc.T("Воину задолжали. В разговоре долг отдают из рук в руки.")
                : Loc.T("В разговоре узнают, как воин, и передают вещи из рук в руки.");

            if (view != null && !view.FirstPersonNow)
                return (view.SwitchKey.ToString(),
                        Loc.F("{0} — смотреть глазами Греховода.", view.SwitchKey) + "\n" + approach);

            return ("F", approach + "\n" + why);
        }

        /// <summary>
        /// Спина — два шага: выделить того, кто свободен, и послать его на
        /// отметку. Сказано, зачем: держащий стоит к врагу лицом, значит враг
        /// стоит спиной ко всем остальным.
        /// </summary>
        private (string key, string words) BackstabStep()
        {
            string pinner = _pinner != null ? Loc.Name(_pinner.DisplayName) : "";
            var selection = _flanker != null ? _flanker.GetComponent<SelectionComponent>() : null;

            if (selection == null || !selection.IsSelected)
            {
                string flanker = _flanker != null ? Grammar.Dative(_flanker.DisplayName, _flanker.Gender) : "";
                return (Loc.T("ЛКМ"),
                        Loc.F("{0} держит врага, и враг стоит к остальным спиной.", pinner)
                      + "\n" + Loc.F("Щёлкните по {0} — выделить.", flanker));
            }

            return (Loc.T("ПКМ"),
                    Loc.T("Правой кнопкой — по светлой отметке за спиной врага: воин зайдёт сзади.")
                  + "\n" + (_frontal
                        ? Loc.T("Это приказ бить в лоб. Чтобы ударить в спину, пошлите его на отметку.")
                        : Loc.T("Удар в спину сильнее обычного.")));
        }

        /// <summary>
        /// Вселение — пять шагов, и плашка говорит только нынешний: пять
        /// строк разом — это инструкция, а не урок. Шаг читается по тому,
        /// что лежит в руках и в устройстве, — сделал шаг, плашка сменилась.
        /// </summary>
        private (string key, string words) RaisingStep()
        {
            var d = _device;

            if (d.HasSoul && d.HasShell)
            {
                // Всё на месте, но устройство отказывает — сказать почему
                // (истлевшая в тяжёлом теле, голем без оков) и что делать.
                string no = d.NotReady;
                if (!string.IsNullOrEmpty(no))
                    return ("F", no + "\n" + Loc.T("F у ложа тела — забрать тело и взять со стола другое."));

                return ("F", Loc.T("Подойдите к рычагу и нажмите F — воин встанет.")
                           + "\n" + d.Foretell());
            }

            if (Crypt.CryptHands.HasSoul)
                return ("F", Loc.T("Подойдите к гнезду души на устройстве и нажмите F — вложить."));

            if (Crypt.CryptHands.HasShell)
                return ("F", Loc.T("Подойдите к ложу тела на устройстве и нажмите F — положить."));

            if (!d.HasSoul)
                return ("R", Loc.T("R — душа из сумы в руки. Tab — выбрать другую банку.")
                           + "\n" + Loc.T("Душа встанет в теле, которое вы ей дадите."));

            return ("F", Loc.T("Тела — на столе у стены. Подойдите и нажмите F — взять.")
                       + "\n" + Loc.T("Тело тянет душу к своему греху — прочтите табличку."));
        }

        private void BuildPlate()
        {
            var font = UIFont();

            _plate = new GameObject("Плашка урока", typeof(Canvas), typeof(CanvasScaler));
            _plate.transform.SetParent(transform, false);
            var canvas = _plate.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 45;
            var scaler = _plate.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Sinbinder.UI.UiStyle.Reference;

            // Справа по центру: не на панели приказов (справа внизу)
            // и не на журнале (слева внизу).
            var back = Rect("Подложка", _plate.transform, new Vector2(1f, 0.5f),
                            new Vector2(-40f, 60f), new Vector2(560f, 150f));
            // Табличка, как у окон (46-UI §2.1): до 15 октября — полупрозрачная
            // полоса без рамки, сквозь неё читался журнал (опись, кадр 10б).
            var shade = back.gameObject.AddComponent<Image>();
            UI.UiStyle.Dress(shade, UI.UiStyle.PlateWeight.Screen);
            shade.raycastTarget = false;

            // Клавиша крупно, в рамке.
            var frame = Rect("Рамка клавиши", back, new Vector2(0f, 0.5f),
                             new Vector2(22f, 0f), new Vector2(92f, 92f));
            var border = frame.gameObject.AddComponent<Image>();
            border.color = new Color(0.94f, 0.92f, 0.86f, 0.9f);
            border.raycastTarget = false;
            var inside = Rect("Клавиша", frame, new Vector2(0.5f, 0.5f),
                              Vector2.zero, new Vector2(84f, 84f));
            var fill = inside.gameObject.AddComponent<Image>();
            fill.color = new Color(0.10f, 0.09f, 0.08f, 1f);
            fill.raycastTarget = false;
            _key = Label(inside, font, 46, TextAnchor.MiddleCenter);
            _key.fontStyle = FontStyle.Bold;

            var text = Rect("Слова", back, new Vector2(0f, 0.5f),
                            new Vector2(136f, 0f), new Vector2(404f, 130f));
            _words = Label(text, font, 21, TextAnchor.MiddleLeft);
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 at, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = at;
            rt.sizeDelta = size;
            return rt;
        }

        private static Text Label(RectTransform parent, Font font, int size, TextAnchor align)
        {
            var go = new GameObject("Строка", typeof(RectTransform), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.color = new Color(0.94f, 0.92f, 0.86f);
            t.raycastTarget = false;
            return t;
        }

        /// <summary>
        /// Книжная антиква, как у всех панелей (<see cref="UI.UiStyle"/>). Раньше —
        /// шрифт «у первого текста сцены»: какой попадётся (опись экранов, 46-UI §1.1, п. 9).
        /// </summary>
        private static Font UIFont() => UI.UiStyle.Body;
    }
}
