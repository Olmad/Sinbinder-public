// Сон Греховода — обучение перед лагерем. С 07.11 с него начинается новая игра (UI.StartPanel);
// пропустить — «Проснуться» в меню паузы.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Сон Бога Смерти: обучение до лагеря. Его ядро (автор, 05.11: «нужно один раз
    /// КОНКРЕТНО, на сто процентов понятно показать AOS») — один приказ, которого
    /// воин может не исполнить, и суд души (<see cref="SoulCourt"/>), который
    /// показывает изнутри, кто в нём решал.
    ///
    /// Порядок (автор, 17.11: суд — «ближе к концу внимания», и судить миг вовремя, а не
    /// задним числом): умирающий, жатва, поднятие, первый приказ — исполнит; поднятый идёт
    /// за Греховодом в форт; уроки глазами у чучела; на дворе форта возвращаются убийцы —
    /// приказ, решение и суд сразу над этим мигом; бой, тот же приказ без убийц, банка с камня
    /// в руку (автор, 12.11) — и к воротам склепа.
    ///
    /// Ничего не подстроено (автор: «игра должна показать, что она не скриптовая»):
    /// душа каждый раз новая, решение принимает движок, а после боя тот же приказ
    /// звучит ещё раз — уже без убийц на глазах, — и игрок видит, что решение
    /// держалось на причине. Подстроено только положение: эти двое его убили,
    /// и они стоят перед ним.
    /// </summary>
    public class DreamTutorialDirector : MonoBehaviour
    {
        public static DreamTutorialDirector Instance { get; private set; }
        public static bool InputLocked => Instance != null && Instance._handBusy;
        public static bool ProtectingHero => Instance != null && Instance._stage == Stage.Battle;

        [SerializeField] private Vector3 _start = new Vector3(0, 0, -27);
        [SerializeField] private Vector3 _woundedAt = new Vector3(0, 0, -2);
        [SerializeField] private Vector3 _allowed = new Vector3(0, 0, 1);
        [SerializeField] private float _backBoundary = -30f;
        [SerializeField] private float _allowedCorridorHalfWidth = 8f;
        [SerializeField] private string _campScene = "Prologue_Camp";

        // Порядок шагов — порядок сна (17.11: уроки глазами — до убийц).
        private enum Stage { Approach, Dying, Harvest, Rebind, Command, FirstPerson, WarriorMode, DrawWeapon, Practice, Standoff, Chase, Bridge, Deciding, Battle, Fallen, Retest, Whisper, Jars, Return, Finished }

        /// <summary>Шаг сна — для прогона (DreamWalkthrough) и отчёта.</summary>
        public static string StageName => Instance != null ? Instance._stage.ToString() : "";

        // [AOS-5M] — вехи первых пяти минут для вечернего теста (ветка ботов, 16.11): от кнопки
        // «Новая игра», а не от первого кадра сна. На игру не влияют — только строки журнала.
        private static float _firstFiveStartedAt = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetFirstFiveTrace() => _firstFiveStartedAt = -1f;

        public static void BeginFirstFiveTrace()
        {
            _firstFiveStartedAt = Time.realtimeSinceStartup;
            Debug.Log("[AOS-5M] +0.0s new_game_clicked");
        }

        private static void TraceFirstFive(string milestone)
        {
            if (_firstFiveStartedAt < 0f)
            {
                BeginFirstFiveTrace();
                Debug.Log("[AOS-5M] direct_dream_scene_without_menu");
            }
            Debug.Log($"[AOS-5M] +{Mathf.Max(0f, Time.realtimeSinceStartup - _firstFiveStartedAt):F1}s {milestone}");
        }

        /// <summary>
        /// Послушался — как решил сам движок (<see cref="AOS.DecisionContext.SatisfiedBy"/>): голос «исполнить»
        /// или действие, которое и есть приказ — нападение по «догнать», бегство по «отойти». Без приказа
        /// в контексте послушаться нечему — тогда только ObeyCommand.
        /// До 20.11 сон считал послушанием лишь ObeyCommand (ветка ботов, 16.11). Бросился на убийцу
        /// по «догнать» — движок и суд: «исполнил», а сон: «не погнался и по приказу» (аудит 84, п. 3).
        /// </summary>
        private static bool ActuallyObeyed(AOS.Decision d, AOS.DecisionContext c)
            => !d.RefusedCommand && !d.Hesitated &&
               (d.Action == AOS.ActionType.ObeyCommand || (c != null && c.SatisfiedBy(d.Action)));

        /// <summary>Голоса, записанные в миг решения: суд показывает их, а не пересчитывает (ветка ботов, 17.11).</summary>
        private static List<AOS.Vote> Ballot(AOS.AOSWarriorWrapper mind)
            => mind != null && mind.LastDecisionBallot != null ? new List<AOS.Vote>(mind.LastDecisionBallot) : null;

        /// <summary>
        /// Весы до приказа во сне — только на проверке тем же приказом (63-TEACHING, п. 7):
        /// суд их уже объяснил, плашка спрашивает «что решит душа теперь?» — и, пока игрок
        /// целится приказом, весы над воином отвечают до щелчка. До суда — нет: там весы
        /// ещё никто не объяснил.
        /// </summary>
        public static bool Foreseeing => Instance != null && Instance._stage == Stage.Retest;

        /// <summary>Поднятый — для прогона.</summary>
        public Warrior Zombie => _zombie;
        public Warrior Wounded => _wounded;
        public IReadOnlyList<Warrior> Killers => _killers;
        public Vector3 WoundedAt => _woundedAt;
        public Vector3 Gate => _allowed;
        public SinbinderPlayer Hero => _hero;

        /// <summary>Как кончился миг приказа при убийцах — для прогона: «отказал», «колебался», «послушался».</summary>
        public static string RefusalResult;

        /// <summary>Как кончилась проверка тем же приказом без убийц: «послушался», «снова нет».</summary>
        public static string RetestResult;

        /// <summary>Шёпот после «снова нет»: «поддался», «не поддался»; null — не шептали.</summary>
        public static string WhisperResult;

        /// <summary>Засада (docs/68, этап 5): пал ли, поднят ли снова, пришёл ли владыка; null — не было.</summary>
        public static string LateResult;

        /// <summary>Как открылись печати у ворот — для прогона: оба встали, один, поднятого нет.</summary>
        public static string SealsResult;

        /// <summary>Печати горят — ворота открываются на F.</summary>
        public bool SealsOpen => _sealsOpen;

        /// <summary>Поднятый не погнался сам — сон ждёт приказа «догнать» (для прогона).</summary>
        public bool ChaseAsked => _chaseAsked && !_chaseDecided;

        /// <summary>Где пал поднятый в засаде (для прогона).</summary>
        public Vector3 LateAt => _lateAt;

        /// <summary>Сон прячет задачи: руки заняты (наезд, ворота) или сон кончился.</summary>
        public static bool HidesTasks => Instance != null && (Instance._stage == Stage.Finished || Instance._handBusy);

        /// <summary>Клавиша, нажатая прогоном (без людей Input молчит). Живёт один кадр.</summary>
        private KeyCode _testKey;
        private int _testKeyFrame = -1;
        public void TestPress(KeyCode key) { _testKey = key; _testKeyFrame = Time.frameCount; }
        private bool Pressed(KeyCode key)
            => Input.GetKeyDown(key) || (_testKey == key && Time.frameCount - _testKeyFrame <= 1);

        /// <summary>Прогон: управление (Tab, V, J) проверено в лагере — уроки глазами сразу к убийцам, банки — к воротам.</summary>
        public void TestSkipControls()
        {
            if (_stage >= Stage.FirstPerson && _stage <= Stage.Practice) { if (!Speaking()) BeginStandoff(); }
            else if (_stage == Stage.Whisper) Set(Stage.Jars, JarsLine);
            else if (_stage == Stage.Jars) Set(Stage.Return, GateLine);
        }

        private const string DreamsKey = "Sinbinder.DreamTutorial.Count";

        // Дорога сна (docs/64-DREAM-ROAD.md): уроки — остановками, в конце склеп.
        // Ворота открываются не сразу: сначала печати перед ними (Seals, 17.11).
        private static readonly string GateLine = Loc.N("Дорога ведёт к склепу. Идите к воротам.");
        private static readonly string JarsLine = Loc.N("За мостом и расщелиной, у колонн, — камень с банками. Идите к нему.");
        private const string EyesLine = "Tab: перейди от тактического вида к первому лицу.";
        private bool _atDummy, _atShrine, _cryptRunning;
        private int _swingsSeen;
        private float _heldBackAt = -10f, _nudgeAt = -10f;

        /// <summary>
        /// Докуда пускает дорога на этом шаге — в метрах главного пути карты (DreamRoad.Progress; карта
        /// обучения, 19.11: дорога больше не прямая по z). Урок не сделан — дальше не пройти. Первый приказ —
        /// до стоянки убийц, уроки глазами — до чучела на стоянке, убийцы, суд и бой — стоянка (до развилки
        /// к мосту), банки — до камня у колонн за расщелиной, потом до ворот.
        /// </summary>
        private float AheadLimit()
        {
            switch (_stage)
            {
                case Stage.FirstPerson: case Stage.WarriorMode: case Stage.DrawWeapon: case Stage.Practice:
                    return DreamRoad.Progress(DreamRoad.DummyAt) + 3f;
                case Stage.Standoff: case Stage.Chase: case Stage.Bridge: case Stage.Deciding:
                    // До моста: на настил Греховод не ступает — мост поднимают у него перед носом.
                    return DreamRoad.Progress(DreamRoad.BridgeWestEnd) - 0.8f;
                case Stage.Battle: case Stage.Fallen: case Stage.Retest: case Stage.Whisper:
                    // Бой — у засады за мостом; мост поднят — в обход лесом, до бревна.
                    return DreamRoad.Progress(DreamRoad.LogAt) - 3f;
                case Stage.Jars: return DreamRoad.Progress(DreamRoad.ShrineAt) + 3f;
                case Stage.Return: case Stage.Finished: return DreamRoad.RouteLength;
                default: return DreamRoad.Progress(DreamRoad.DummyAt) - 7f;
            }
        }

        /// <summary>Ушёл дальше урока — вернуть к черте и подсветить строку урока.</summary>
        private void HoldBack()
        {
            _nudgeAt = Time.unscaledTime;
            if (Time.unscaledTime - _heldBackAt < 1f) return;
            _heldBackAt = Time.unscaledTime;
            var mover = _hero.GetComponent<UnitMover>();
            if (mover != null) mover.CommandMove(DreamRoad.RoutePoint(AheadLimit() - 1.2f));
        }

        /// <summary>Куда тянет рука, если Греховод упрямо идёт назад: вперёд по дороге, не дальше урока.</summary>
        private Vector3 Ahead()
        {
            float s = (_hero != null ? DreamRoad.Progress(_hero.transform.position) : 0f) + 4f;
            return DreamRoad.RoutePoint(Mathf.Min(s, AheadLimit() - 1.5f));
        }

        /// <summary>Позади опушки — туда дороги нет.</summary>
        private static bool Behind(Vector3 p) => p.x < DreamRoad.StarAt.x - 3f;

        private Stage _stage;
        private bool _ready;   // Start поставил сон на дорогу — до того Update молчит
        private readonly HashSet<Stage> _deathSpoken = new();
        private string _deathCaption;

        // Only a few important steps belong to Death's voice; controls
        // remain in Lesson. No praise, prophecy or promise of obedience.
        private string DeathCue()
        {
            switch (_stage)
            {
                case Stage.Harvest: return Loc.T("Забери душу.");
                case Stage.Command:
                    return _zombie != null && !_zombie.IsDead && _orderedAt < 0f && !OrderedSinceMark()
                        ? Loc.T("Отдай приказ.") : null;
                case Stage.Bridge:
                    return _zombie != null && !_zombie.IsDead && DreamRoad.BridgeRaised && !LeverOrderedSinceMark()
                        ? Loc.T("Опусти мост.") : null;
                default: return null;
            }
        }

        private string WithDeath(string instruction)
            => string.IsNullOrEmpty(_deathCaption) ? instruction
                : Loc.T("Бог Смерти") + ": " + _deathCaption + "\n" + instruction;

        private void SilenceDeathLesson()
        {
            if (_deathCaption == null) return;
            _deathCaption = null;
            Audio.DeathVoice.QuietLesson();
        }

        private void UpdateDeathLesson(bool quiet)
        {
            string cue = quiet || !_ready ? null : DeathCue();
            if (cue == null || (_deathCaption != null && _deathCaption != cue))
                SilenceDeathLesson();
            if (cue == null || !_deathSpoken.Add(_stage)) return;
            _deathCaption = cue;
            Audio.DeathVoice.UtterLesson(cue);
        }

        private SinbinderPlayer _hero;
        private Warrior _wounded, _zombie;
        private readonly List<Warrior> _killers = new List<Warrior>();
        private readonly List<Sketch> _killerSketches = new List<Sketch>();
        private bool _handBusy, _battleStarted;
        private int _backAttempts;
        private int _swingsBefore, _blockFramesBefore, _blocksBefore, _ripostesBefore;
        private int _hitsBefore, _missesSaid;
        private int _jarIndex = -1;
        private bool _soulHeld;
        private float _commandWait, _orderedAt = -1f;
        private float _standoffSince, _decideSince;
        // Суд срабатывал «раз из десяти» (автор, 06.11): цепочка до него рвалась —
        // душа угасала несобранной, игрок не отдавал приказ или сам рубил убийц,
        // решение не приходило за 6 с. Каждое звено теперь доходит само.
        private float _standoffReal, _harvestSince;
        private bool _autoOrdered;
        private SoulData _fallbackSoul;
        // Мост (этап 2–3 карты, 19.11): убийцы бегут, мост поднимают, приказ — к рычагу.
        private bool _fleeing;
        private float _fleeSince;
        // «Не погнался» (docs/68): сам не гонится — сон просит приказ «догнать», и суд идёт на нём.
        private bool _chaseAsked, _chaseCourt, _chaseDecided, _chaseRefused, _autoChase;
        private float _chaseFrom, _chaseAskedAt, _chaseDecidedAt;
        private int _courts;
        private readonly HashSet<Warrior> _armed = new HashSet<Warrior>();

        // Круг обучения на каждом шаге к суду (автор, 06.11: «у нас есть круг
        // обучения. Возможно, он может решить почти все проблемы»).
        private Stage _guidedFor = Stage.Finished;
        private float _nextGuide;

        // Under a lesson ring, identical orders may share the same game time.
        private uint _cmdMarkRevision;
        private CommandKind? _courtOrder;

        private void MarkCommand()
        {
            if (_zombie != null) _cmdMarkRevision = _zombie.CommandRevision;
        }

        private bool OrderedSinceMark()
        {
            if (_zombie == null || _zombie.IsDead || !_zombie.HasCommand) return false;
            return _zombie.CommandRevision != _cmdMarkRevision;
        }

        private bool LeverOrderedSinceMark()
            => OrderedSinceMark() && _zombie.Command.Kind == CommandKind.Move
                && DreamRoad.AtLever(_zombie.Command.Point, 3f);

        private bool ChaseOrderedSinceMark()
        {
            if (!OrderedSinceMark() || _zombie.Command.Kind != CommandKind.Attack) return false;
            var target = _zombie.Command.Target;
            return target != null && _killers.Exists(k => k != null && !k.IsDead && k.gameObject == target);
        }

        private bool Picked()
        {
            var pick = _zombie != null ? _zombie.GetComponent<SelectionComponent>() : null;
            return pick != null && pick.IsSelected;
        }

        private (string key, string words) FirstOrderHint()
        {
            if (!Picked())
                return (Loc.T("ЛКМ"), WithDeath(Loc.F("Щёлкните по {0} — выделить.", Whom())
                    + "\n" + Loc.T("Это ваш первый воин.")));

            return (Loc.T("ПКМ"), WithDeath(Loc.T("Правой кнопкой — по земле, чтобы приказать идти.")
                + "\n" + Loc.T("Приказ отдаёте вы. Исполнит ли — решает душа.")));
        }

        private string Whom() => _zombie != null ? Grammar.Dative(_zombie.DisplayName, _zombie.Gender) : "";

        /// <summary>
        /// Кольцо на шаг, который игрок должен сделать сам: мир стоит, плашка
        /// говорит клавишу, шаг сделан — мир пошёл. Уроки выключены — шаг ведут
        /// строки журнала, как раньше.
        /// </summary>
        private void Guide()
        {
            if (_guidedFor == _stage || Time.unscaledTime < _nextGuide || SoulCourt.Sitting) return;
            _nextGuide = Time.unscaledTime + 0.25f;
            var g = _zombie != null ? _zombie.Gender : Gender.Male;
            bool shown = false;
            switch (_stage)
            {
                case Stage.Rebind:
                    shown = Lesson.Guide(_woundedAt,
                        () => ("R", Loc.T("Подойдите к телу и нажмите R — душа вернётся в оболочку.")
                                  + "\n" + Loc.T("Поднятый встанет вашим воином.")),
                        () => _stage != Stage.Rebind);
                    break;
                case Stage.Command:
                    if (_zombie == null || _zombie.IsDead || _orderedAt >= 0f) return;
                    shown = Lesson.Guide(_zombie.transform.position, FirstOrderHint,
                        () => _stage != Stage.Command || _orderedAt >= 0f || OrderedSinceMark());
                    break;
                case Stage.Bridge:
                    if (_zombie == null || _zombie.IsDead) return;
                    shown = Lesson.Guide(_zombie.transform.position,
                        () => !Picked()
                            ? (Loc.T("ЛКМ"), WithDeath(Grammar.Pick(g, Loc.T("Он за рекой, у рычага моста."), Loc.T("Она за рекой, у рычага моста."))
                                             + "\n" + Loc.F("Щёлкните по {0} — выделить.", Whom())))
                            : (Loc.T("ПКМ"), WithDeath(Loc.T("Правой кнопкой — по рычагу: приказ опустить мост.")
                                  + "\n" + Loc.T("Послушается ли — решит душа. Смотрите."))),
                        () => _stage != Stage.Bridge || LeverOrderedSinceMark());
                    break;
                case Stage.Retest:
                    if (_zombie == null) return;
                    shown = Lesson.Guide(_zombie.transform.position,
                        () => !Picked()
                            ? (Loc.T("ЛКМ"), Loc.T("Убийц больше нет.") + "\n" + Loc.F("Щёлкните по {0} — выделить.", Whom()))
                            : (Loc.T("ПКМ"), Loc.T("Тот же приказ: правой кнопкой — по рычагу.")
                                  + "\n" + Loc.T("Убийц рядом больше нет. Что решит душа теперь?")),
                        () => _stage != Stage.Retest || LeverOrderedSinceMark());
                    break;
                case Stage.Fallen:
                    if (_lateBody == null) return;
                    shown = Lesson.Guide(_lateAt,
                        () => FindJar() < 0
                            ? ("E", Loc.T("Душа угасает у тела. Подойдите и нажмите E — заберите её в банку."))
                            : ("R", Loc.T("Теперь R у тела — душа вернётся в оболочку.")),
                        () => _stage != Stage.Fallen);
                    break;
                default:
                    _guidedFor = _stage;   // этому шагу кольцо не нужно
                    return;
            }
            if (shown) _guidedFor = _stage;
        }
        private bool _courtRunning, _retestOrdered, _contrastRunning, _retestManualDecideTried, _fiveMarked;
        private float _whisperSince;
        private uint _decisionOrderRevision;
        private bool _decisionManualDecideTried;

        // A newer context alone is not proof that it belongs to this lesson's order.
        private bool HasLessonDecision(AOS.AOSWarriorWrapper mind)
            => mind != null && DreamTutorialChecks.MatchesOrderedDecision(
                mind.LastContext != null && mind.LastContext.HasCommand,
                _decisionOrderRevision, mind.LastDecisionCommandRevision);
        private float _nextPrompt;
        private string _line;
        private DreamHandCursor _hand;
        private int _tutorialSeed, _dreamNo;
        private bool _counterfactualWas;
        private float _retestPrompted;

        // Only intercept commands inside this optional dream. Other scenes are untouched.
        public static bool PermitDestination(Vector3 worldPoint)
        {
            var dream = Instance;
            if (dream == null || dream._stage == Stage.Finished) return true;
            if (dream._handBusy) return false;
            if (!Behind(worldPoint) &&
                (dream._backAttempts < 3 || DreamRoad.OffRoute(worldPoint) <= dream._allowedCorridorHalfWidth)) return true;
            dream.BackAttempt();
            return false;
        }

        // Used by PlayerWalk when in first-person; never moves the OS cursor.
        public static bool PermitWalking(Vector3 futurePoint)
        {
            var dream = Instance;
            if (dream == null || dream._stage == Stage.Finished) return true;
            if (dream._handBusy) return false;
            if (dream._hero != null)
            {
                float ahead = DreamRoad.Progress(futurePoint);
                if (ahead > dream.AheadLimit() && ahead > DreamRoad.Progress(dream._hero.transform.position))
                {
                    dream._nudgeAt = Time.unscaledTime;
                    return false;
                }
            }
            if (!Behind(futurePoint) &&
                (dream._backAttempts < 3 || DreamRoad.OffRoute(futurePoint) <= dream._allowedCorridorHalfWidth)) return true;
            dream.BackAttempt();
            return false;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _t0 = Time.realtimeSinceStartup;
            _timeline.Clear();
            // Душа — новая в каждой новой игре (автор, 05.11: «одна и та же душа
            // может выглядеть скриптово»; «разные души повышают заметность AOS
            // при переигровке»). Без жребия: по счёту снов на этом ПК.
            // Неизменен только факт сцены — эти двое его убили.
            int dreams = PlayerPrefs.GetInt(DreamsKey, 0);
            PlayerPrefs.SetInt(DreamsKey, dreams + 1);
            _tutorialSeed = 4242 + dreams * 7919;
            _dreamNo = dreams + 1;
            RefusalResult = null;
            RetestResult = null;
            WhisperResult = null;
            LateResult = null;
            SealsResult = null;

            // Причина отказа «от противного» — на весь сон: суд называет то,
            // без чего воин поступил бы иначе, и это должно быть посчитано.
            _counterfactualWas = AOS.Counterfactual.Enabled;
            AOS.Counterfactual.Enabled = true;

            // Дорога — до выпечки навмеша (GroundNavMesh печёт в Start): обочины держат на ней.
            DreamRoad.Build();
            _allowed = DreamRoad.GateFront;
            // Карта обучения (19.11): начало — на опушке (звезда автора), умирающий — на дороге к стоянке убийц.
            _start = DreamRoad.StarAt;
            _woundedAt = DreamRoad.DyingAt;
        }

        private void OnDisable()
        {
            SilenceDeathLesson();
            Lesson.Unhint(this);
            UI.TaskList.Place(null, null);
        }
        private void OnDestroy()
        {
            if (Instance != this) return;
            if (_stage != Stage.Finished) SaveTimeline();
            Instance = null;
            AOS.Counterfactual.Enabled = _counterfactualWas;
        }

        private string _courtVoice;

        /// <summary>
        /// Чей голос назвать: тот же, что малые весы над головой (SoulCourt.Loudest) — игрок видит
        /// его там; без приказа — решающий «от противного», нет — самый громкий.
        /// </summary>
        private string Voice(AOS.Decision d, AOS.DecisionContext c)
        {
            string seen = SoulCourt.Loudest(_zombie, d, c);
            if (!string.IsNullOrEmpty(seen)) return seen;
            return d.Weighed && !string.IsNullOrEmpty(d.DecisiveVoice) ? d.DecisiveVoice : d.TopModule;
        }

        private IEnumerator Start()
        {
            // Перебранки перед боем во сне нет: сон ведёт свои мгновения сам, а
            // перебранка, начавшись при убийцах, забирала камеру и, уходя,
            // выбрасывала её из суда души в мир (прогон 05.11).
            foreach (var talk in FindObjectsByType<Dialogue.DialogueTrigger>(FindObjectsSortMode.None))
                talk.enabled = false;

            // The existing camp is still the real first scene for the public demo.
            // Память душ живёт в MemoryProcessor этой сцены и уходит вместе с ней.
            SquadRoster.Clear();
            Core.Satchel.Forget();
            _hero = SinbinderPlayer.Spawn(_start);
            if (_hero == null) { enabled = false; yield break; }
            // Начало сна — ближе (16.11): с 22 м при угле 55° Греховод был точкой, и первый кадр
            // игры читался чёрным полем (кадр «сон — начало»). 32° — дорога во всю ширину и он сам
            // на ней; колесо отдаляет до прежних 55 (RTS_Camera держит предел по кадру сцены).
            var eye = Camera.main != null ? Camera.main.GetComponent<RTS_Camera>() : null;
            // Сначала — над ним: Start камеры мог пройти раньше, чем он появился.
            if (eye != null) { eye.SetView(RTS_Camera.CameraView.Tactical); Camera.main.fieldOfView = 32f; eye.Resync(); }
            Mark("T0 сон начался");
            if (eye != null) StartCoroutine(Opening(eye));
            _hand = gameObject.AddComponent<DreamHandCursor>();
            _hand.enabled = false;

            var names = new List<string>();
            for (int i = 0; i < 2; i++)
            {
                var k = WarriorGenerator.Make(_tutorialSeed + i + 1, SoulOrigin.Harvest, names);
                _killerSketches.Add(k);
                names.Add(k.Name);
            }
            var dying = WarriorGenerator.Make(_tutorialSeed, SoulOrigin.Harvest, names);
            // Personal memory belongs to this soul, never to a scripted 'revenge zombie'.
            var seed = new MemorySeed {
                Object = _killerSketches[0].Name, Trigger = _killerSketches[0].Name,
                Emotion = "Ненависть", Story = "Эти люди оставили его умирать на дороге."
            };
            // Грех — его собственный, но не добродетель: искусить можно только
            // то, что в душе звучит (спектр выше нуля), — иначе урок «Z» упёрся бы
            // в «Не купишь». Память об убийцах — у каждого, это факт сцены.
            var soul = new SoulData(dying.Name, dying.Sin, dying.Moral, 1,
                                    Mathf.Max(45f, Mathf.Abs(dying.Strength)), seed);
            soul.SetTrade(dying.Trade);
            _wounded = CreateLiving(soul, _woundedAt, Team.Neutral);
            if (_wounded == null) { enabled = false; yield break; }
            Debug.Log($"[СОН] Душа сна №{_dreamNo}: {soul.Name}, грех {soul.Sin}, сила {soul.SinIntensity:F0}");
            yield return null;
            var hp = _wounded.GetComponent<Damageable>();
            if (hp != null) hp.Wound(0.12f);
            // Убитый бандитами лежит на дороге, а не стоит (автор, 08.11: «должен лежать на земле
            // и говорить из последних сил»): поза падения с первого кадра — к приходу Греховода он уже лежит.
            var body = _wounded.GetComponent<WarriorAnimation>();
            if (body != null) body.Pose(BodyMotion.Die);
            var voice = _wounded.GetComponent<Sinbinder.Audio.VoiceGenerator>();
            if (voice != null) { voice.Depth = 1.12f; voice.Tempo = 1.7f; }
            Set(Stage.Approach, Loc.N("Укажи Греховоду путь правой кнопкой мыши."));
            _ready = true;
        }

        /// <summary>
        /// Первый кадр сна — не чёрное поле сверху с точкой-Греховодом (автор, 16.11: «Греховода
        /// в начале обучения не видно»). Камера стоит за его плечом: впереди дорога со свечами
        /// и далёкий склеп — куда идти, видно с первой секунды; потом за три секунды поднимается
        /// в вид сверху, над ним. Любая клавиша или щелчок — сразу сверху.
        /// </summary>
        private IEnumerator Opening(RTS_Camera eye)
        {
            var cam = Camera.main;
            if (cam == null || _hero == null) { Mark("T1 управление у игрока"); yield break; }
            _handBusy = true;
            var topPos = cam.transform.position;
            var topRot = cam.transform.rotation;
            float topFov = cam.fieldOfView;
            eye.enabled = false;
            var h = _hero.transform.position;
            var fwd = DreamRoad.RouteForward(0f);
            var from = h - fwd * 3.4f + new Vector3(fwd.z, 0f, -fwd.x) * 0.8f + Vector3.up * 1.8f;
            var fromRot = Quaternion.LookRotation(h + fwd * 40f + Vector3.up * 3.5f - from);
            const float hold = 1.8f, rise = 2.8f;
            for (float t = 0f; t < hold + rise; t += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f))
            {
                if ((t > 0.4f && Input.anyKeyDown) || _stage != Stage.Approach) break;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - hold) / rise));
                cam.transform.SetPositionAndRotation(Vector3.Lerp(from, topPos, k), Quaternion.Slerp(fromRot, topRot, k));
                cam.fieldOfView = Mathf.Lerp(48f, topFov, k);
                yield return null;
            }
            cam.transform.SetPositionAndRotation(topPos, topRot);
            cam.fieldOfView = topFov;
            eye.enabled = true;
            eye.Resync();
            _handBusy = false;
            Mark("T1 управление у игрока");
        }

        private Warrior CreateLiving(SoulData soul, Vector3 at, Team team)
        {
            if (soul == null) return null;
            var go = new GameObject(soul.Name);
            go.transform.position = at;
            var w = go.AddComponent<Warrior>();
            w.Initialize(soul, ShellType.Living, new RelationshipSystem(AOS.MemoryProcessor.Instance), false, team);
            WarriorRig.Attach(go);
            WarriorLook.Build(go, ShellType.Living, 1.25f, 0.52f, 0.7f);
            var setup = FindFirstObjectByType<AOS.AOSSceneSetup>();
            if (setup != null) setup.SetupWarrior(go);
            return w;
        }

        /// <summary>Идёт ли чья-то реплика нижней полосой или наезд камеры — новый миг подождёт.</summary>
        private static bool Speaking()
        {
            var box = UI.Letterbox.Instance;
            var talk = Dialogue.DialogueCameraController.Instance;
            return (box != null && box.Covering) || (talk != null && talk.InDialogue);
        }

        // Поднятый идёт за Греховодом от первого приказа до убийц (17.11): миг у чучела — его миг,
        // он должен быть рядом. Это не приказ и не решение души — ум его на это время спит.
        private bool _escorting, _jarsWalk;
        private float _escortAt;

        private void StartEscort()
        {
            if (_zombie == null || _zombie.IsDead) return;
            Hold(_zombie, true);
            MarkCommand();
            _escorting = true;
        }

        private void Escort()
        {
            if (_zombie == null || _zombie.IsDead) { _escorting = false; return; }
            // Приказ игрока сильнее провожания: ум проснулся и решает (к рычагу — тоже приказ).
            if (OrderedSinceMark())
            {
                // Путь провожания — прочь: решит стоять — стоит, а не идёт по старому пути за Греховодом.
                var stale = _zombie.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (stale != null && stale.enabled && stale.isOnNavMesh) stale.ResetPath();
                _escorting = false;
                Hold(_zombie, false);
                return;
            }
            if (Time.unscaledTime < _escortAt) return;
            _escortAt = Time.unscaledTime + 0.4f;
            var legs = _zombie.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (legs == null || !legs.enabled || !legs.isOnNavMesh) return;
            var h = _hero.transform;
            if (GroundDistance(_zombie.transform.position, h.position) < 3.2f) { legs.isStopped = true; return; }
            legs.isStopped = false;
            legs.SetDestination(h.position - Flat(h.forward) * 2.2f + Flat(h.right) * 1.2f);
        }

        private void Set(Stage stage, string line)
        {
            if (stage != _stage) { SilenceDeathLesson(); Mark(stage.ToString()); }
            _stage = stage;
            _line = line;
            _nextPrompt = Time.unscaledTime + 15f;
            Log(line);
        }

        // Хронометраж сна (FIRST-FIVE-MINUTES, P0.1): метки по настоящим часам от начала сцены —
        // когда игрок получил управление, отдал приказ, увидел решение, суд, повтор. В журнал
        // Unity и в файл dream-timeline.txt рядом с сохранениями: по нему меряем первые пять минут.
        private float _t0 = -1f;
        private static readonly List<string> _timeline = new List<string>();

        /// <summary>Метки последнего сна — для отчёта прогона (DreamWalkthrough).</summary>
        public static IReadOnlyList<string> Timeline => _timeline;

        private void Mark(string what)
        {
            if (_t0 < 0f) _t0 = Time.realtimeSinceStartup;
            float t = Time.realtimeSinceStartup - _t0;
            string line = $"{(int)(t / 60f)}:{(int)(t % 60f):00} {what}";
            _timeline.Add(line);
            Debug.Log("[СОН T] " + line);
        }

        private void SaveTimeline()
        {
            if (_timeline.Count == 0) return;
            try
            {
                var lines = new List<string> { $"Сон №{_dreamNo}, {System.DateTime.Now:dd.MM.yyyy HH:mm}" };
                lines.AddRange(_timeline);
                System.IO.File.WriteAllLines(System.IO.Path.Combine(Application.persistentDataPath, "dream-timeline.txt"), lines);
            }
            catch (System.Exception e) { Debug.LogWarning("[СОН T] хронометраж не записан: " + e.Message); }
        }

        private void Log(string message)
        {
            // Строки сна помечены Loc.N — переводятся здесь, при показе; в журнал Unity — по-русски.
            FindFirstObjectByType<UI.BattleLogUI>()?.Write(Loc.T(message));
            Debug.Log("[СОН] " + message);
        }

        private void BackAttempt()
        {
            if (_handBusy) return;
            _backAttempts++;
            if (_backAttempts >= 3)
            {
                StartCoroutine(HandIntervenes());
            }
        }

        private IEnumerator HandIntervenes()
        {
            if (_handBusy || _hero == null) yield break;
            _handBusy = true;
            if (_hand != null)
            {
                _hand.enabled = true;
                yield return _hand.DragTo(Ahead(), 1.05f);
                _hand.enabled = false;
            }
            if (_hero != null)
            {
                var mover = _hero.GetComponent<UnitMover>();
                if (mover != null) mover.CommandMove(Ahead());
            }
            _handBusy = false;
        }

        private void Update()
        {
            if (!_ready || _hero == null || _stage == Stage.Finished) return;
            // [AOS-5M]: где игрок через пять минут после «Новой игры» — по часам без паузы.
            if (!_fiveMarked && _firstFiveStartedAt >= 0f && Time.realtimeSinceStartup - _firstFiveStartedAt >= 300f)
            {
                _fiveMarked = true;
                TraceFirstFive("five_minute_mark stage=" + _stage);
            }
            if (Time.unscaledTime >= _nextPrompt && !SoulCourt.Sitting && !Lesson.Holding)
            {
                Log(_stage == Stage.Return ? SealPrompt().words : _line);
                _nextPrompt = Time.unscaledTime + 25f;
            }
            Guide();
            // От стоянки к колоннам — вместе (карта обучения, 19.11): камень с банками за мостом и бревном,
            // и поднятый, оставшись на стоянке, отстал бы на полторы сотни метров.
            if (_stage == Stage.Jars && !_jarsWalk) { _jarsWalk = true; if (_zombie != null && !_zombie.IsDead) StartEscort(); }
            if (_escorting) Escort();
            if (!_handBusy && DreamRoad.Progress(_hero.transform.position) > AheadLimit() + 0.4f) HoldBack();
            switch (_stage)
            {
                case Stage.Approach:
                    if (_wounded != null && GroundDistance(_hero.transform.position, _woundedAt) <= 4.8f)
                    {
                        if (_wounded.IsDead) BeginHarvest();
                        else
                        {
                            Set(Stage.Dying, Loc.N("Незнакомец пытается что-то сказать."));
                            StartCoroutine(DyingEncounter());
                        }
                    }
                    break;
                case Stage.Dying:
                    if (_wounded != null && _wounded.IsDead) BeginHarvest();
                    break;
                case Stage.Harvest:
                    // The actual SoulHarvester puts the soul in a jar after E.
                    if (FindJar() >= 0)
                    {
                        Set(Stage.Rebind, Loc.N("Подойди к телу. Нажми R, чтобы вернуть душу в оболочку."));
                    }
                    else if (Time.time - _harvestSince > 50f && _wounded != null && _wounded.Soul != null)
                    {
                        // Душа угасает за минуту; несобранная — сон не должен на этом встать.
                        _fallbackSoul = _wounded.Soul;
                        Set(Stage.Rebind, Loc.N("Душа ещё держится за тело. Подойди к телу и нажми R, чтобы вернуть её."));
                    }
                    break;
                case Stage.Rebind:
                    if (Pressed(KeyCode.R) && GroundDistance(_hero.transform.position, _woundedAt) < 4f)
                        TryRebind();
                    break;
                case Stage.Command:
                    if (GamePauseController.Instance?.IsPaused == true || UI.Modal.Any || Speaking()) break;
                    if (_zombie == null || _zombie.IsDead)
                    {
                        Set(Stage.FirstPerson, Loc.T(EyesLine));
                        break;
                    }
                    if (_orderedAt < 0f)
                    {
                        // Движение самого Греховода в составе отряда не считается приказом поднятому.
                        if (OrderedSinceMark())
                        {
                            _orderedAt = Time.time;
                            Mark("T2 первый приказ");
                            TraceFirstFive("first_command_detected");
                            Set(Stage.Command, Loc.T("Воин услышал приказ. Посмотрите, как он ответит."));
                        }
                        else _commandWait += Time.unscaledDeltaTime;
                    }
                    bool ordered = _orderedAt >= 0f;
                    if (ordered ? Time.time - _orderedAt > 3.5f : _commandWait > (Lesson.Holding ? 60f : 20f))
                    {
                        StartEscort();
                        Set(Stage.FirstPerson, (ordered ? "" : Loc.T("Приказ можно попробовать позже.") + " ")
                            + Loc.T(EyesLine));
                    }
                    break;
                case Stage.Standoff:
                    if (_zombie == null || _zombie.IsDead) { StartBattle(Loc.N("Первый воин пал.")); break; }
                    // Узнали — и бегут: от мёртвого, которого сами оставили на дороге.
                    if (_killers.Count > 0 && !_fleeing && Time.time - _standoffSince > 3.2f) Flee();
                    break;
                case Stage.Chase:
                    Chase();
                    break;
                case Stage.Bridge:
                    if (_zombie == null || _zombie.IsDead) { StartBattle(Loc.N("Первый воин пал.")); break; }
                    // Игрок медлит — приказ отдаёт сам Греховод: суд важнее урока клавиш.
                    if (!_autoOrdered && !LeverOrderedSinceMark()
                        && Time.unscaledTime - _standoffReal > (Lesson.Holding ? 45f : 14f) && !SoulCourt.Sitting)
                    {
                        _autoOrdered = true;
                        Log(Loc.N("Греховод сам велит опустить мост."));
                        _zombie.IssueCommand(CommandKind.Move, DreamRoad.LeverStand);
                    }
                    if (LeverOrderedSinceMark())
                    {
                        _courtOrder = _zombie.Command.Kind;
                        Mark("приказ при убийцах: " + _courtOrder);
                        _decisionOrderRevision = _zombie.CommandRevision;
                        _decisionManualDecideTried = false;
                        _decideSince = Time.unscaledTime;
                        Hold(_zombie, false);   // теперь решает он
                        _stage = Stage.Deciding;
                    }
                    break;
                case Stage.Deciding:
                {
                    var mind = _zombie != null ? _zombie.GetComponent<AOS.AOSWarriorWrapper>() : null;
                    if (mind == null || _zombie.IsDead) { StartBattle(Loc.N("Решение не дошло до суда.")); break; }
                    if (_courtRunning) break;
                    // Голоса не заговорили сами за секунду с лишним — спросить душу напрямую:
                    // тот же Decide, что зовёт её мозг, просто сейчас.
                    if (!HasLessonDecision(mind) && !_decisionManualDecideTried
                        && _zombie.HasCommand && _zombie.CommandRevision == _decisionOrderRevision
                        && Time.unscaledTime - _decideSince > 1.2f)
                    {
                        _decisionManualDecideTried = true;
                        mind.Decide();
                    }
                    if (HasLessonDecision(mind))
                    {
                        _courtRunning = true;
                        StartCoroutine(Judge(mind.LastDecisionDetail, mind.LastContext, Ballot(mind)));
                    }
                    else if (Time.unscaledTime - _decideSince > 6f)
                    {
                        Debug.LogWarning("[СОН QA] Воин не принял решения за 6 с после приказа — суда не будет.");
                        StartBattle(Loc.N("Убийцы идут."));
                    }
                    break;
                }
                case Stage.Battle:
                    KeepHeroAlive();
                    ArmArrived();
                    WatchLord();
                    // Пал в засаде впервые — душа угасает у тела, как у любого павшего: успеть E и R.
                    if (_battleStarted && _zombie != null && _zombie.IsDead && _lateBody == null) { Fall(); break; }
                    if (_battleStarted && _killers.Count > 0 && _killers.TrueForAll(k => k == null || k.IsDead))
                    {
                        bool refused = Refused;
                        if (_zombie != null && !_zombie.IsDead && refused)
                        {
                            if (_lordCame) Remember(false);
                            _retestPrompted = Time.unscaledTime;
                            MarkCommand();
                            _retestOrdered = false;
                            _retestManualDecideTried = false;
                            Set(Stage.Retest, Loc.N("Убийц больше нет. Отдайте ему тот же приказ — ПКМ по рычагу."));
                        }
                        else
                        {
                            // Пал в бою — сказать, а не молча перейти к управлению: проверка тем же
                            // приказом пропала не сама, её съело его решение (прогон 06.11: кинулся мстить).
                            if (_zombie != null && _zombie.IsDead && refused)
                                Log(Grammar.Pick(_zombie.Gender, Loc.T("Он пал. Своё решение стоило ему второй жизни."),
                                                                 Loc.T("Она пала. Своё решение стоило ей второй жизни.")));
                            Set(Stage.Jars, JarsLine);
                        }
                    }
                    break;
                case Stage.Fallen:
                    KeepHeroAlive();
                    ArmArrived();
                    if (Pressed(KeyCode.R) && GroundDistance(_hero.transform.position, _lateAt) < 4f) Reraise();
                    else if (Time.time - _lateSince > 2f && FindJar() < 0 && !Fading(_lateBody))
                    {
                        // Не успели: душа угасла. Дальше без него — как в игре.
                        LateResult += "; душа угасла";
                        Mark("T5 засада: душа угасла");
                        Set(Stage.Battle, Grammar.Pick(_lateBody != null ? _lateBody.Gender : Gender.Male,
                            Loc.N("Душа угасла — он больше не встанет."), Loc.N("Душа угасла — она больше не встанет.")));
                    }
                    break;
                case Stage.Retest:
                {
                    if (_zombie == null || _zombie.IsDead) { Set(Stage.Jars, JarsLine); break; }
                    var mind = _zombie.GetComponent<AOS.AOSWarriorWrapper>();
                    if (!_retestOrdered && LeverOrderedSinceMark())
                    {
                        _retestOrdered = true;
                        _decisionOrderRevision = _zombie.CommandRevision;
                        _decideSince = Time.unscaledTime;
                    }
                    bool fresh = HasLessonDecision(mind);
                    if (_retestOrdered && !_contrastRunning && fresh)
                    {
                        _contrastRunning = true;
                        StartCoroutine(Contrast(mind.LastDecisionDetail, mind.LastContext));
                    }
                    else if (_retestOrdered && !_contrastRunning)
                    {
                        // Голоса молчат: раз спросить душу напрямую (как в Deciding), а не дождалась
                        // и после — честно «решение не поступило», без выдуманного послушания.
                        // До 20.11 повтор без решения висел вечно (тест ботов, Tools/tutorial).
                        if (mind != null && !_retestManualDecideTried && _zombie.HasCommand
                            && _zombie.CommandRevision == _decisionOrderRevision
                            && Time.unscaledTime - _decideSince > 1.2f)
                        {
                            _retestManualDecideTried = true;
                            mind.Decide();
                        }
                        else if (Time.unscaledTime - _decideSince > 6f)
                        {
                            RetestResult = "решение не поступило";
                            Debug.LogWarning("[СОН QA] Воин не принял решения за 6 с после повтора приказа.");
                            Mark("T6 тот же приказ без убийц: решение не поступило");
                            Set(Stage.Jars, JarsLine);
                        }
                    }
                    else if (!_retestOrdered && !Lesson.Holding && Time.unscaledTime - _retestPrompted > 40f)
                        Set(Stage.Jars, JarsLine);
                    break;
                }
                case Stage.Whisper:
                    WhisperStep();
                    break;
                case Stage.FirstPerson:
                {
                    var cam = Camera.main != null ? Camera.main.GetComponent<RTS_Camera>() : null;
                    if (cam != null && cam.FirstPersonNow)
                        Set(Stage.WarriorMode, Loc.N("V: переключись в режим Воина. ЛКМ удар, ПКМ блок."));
                    break;
                }
                case Stage.WarriorMode:
                {
                    var cam = Camera.main != null ? Camera.main.GetComponent<RTS_Camera>() : null;
                    if (cam == null || !cam.WarriorModeNow) break;
                    // Оружия со стойки во сне нет — Греховод бьёт посохом, и урока B не нужно.
                    if (_hero.GetComponentInChildren<Armament>(true) != null)
                        Set(Stage.DrawWeapon, Loc.N("B — достань оружие. Только обнажённым оружием можно атаковать."));
                    else
                        BeginPractice("Посох всегда в руке. Впереди у дороги — чучело: подойдите к нему.");
                    break;
                }
                case Stage.DrawWeapon:
                {
                    var cam = Camera.main != null ? Camera.main.GetComponent<RTS_Camera>() : null;
                    if (cam == null || !cam.WarriorModeNow) break;
                    var arms = _hero.GetComponentInChildren<Armament>(true);
                    if (arms != null && !arms.Drawn) break;
                    BeginPractice("Впереди у дороги — чучело: подойдите к нему.");
                    break;
                }
                case Stage.Practice:
                {
                    // Урок принимает чучело у дороги: удар и блок — рядом с ним, оно качается.
                    var combat = _hero.GetComponent<FirstPersonCombat>();
                    if (combat == null) break;
                    // Считаются попадания (DreamRoad.DummyHits), а не взмахи: удар идёт туда,
                    // куда смотрит Греховод, и чучело отшатывается от него.
                    if (!DreamRoad.NearDummy(_hero.transform.position))
                    {
                        _atDummy = false;
                        _swingsSeen = combat.AcceptedSwings;
                        break;
                    }
                    if (!_atDummy)
                    {
                        _atDummy = true;
                        _swingsBefore = _swingsSeen = combat.AcceptedSwings;
                        _hitsBefore = DreamRoad.DummyHits;
                        _missesSaid = 0;
                        _blockFramesBefore = combat.AcceptedBlockFrames;
                        _blocksBefore = DreamRoad.DummyBlocks;
                        _ripostesBefore = DreamRoad.DummyRipostes;
                        _line = "Ударьте чучело ЛКМ. Оно ответит — встретьте удар блоком ПКМ.";
                    }
                    if (DreamRoad.DummyHits == _hitsBefore && combat.AcceptedSwings - _swingsBefore >= 2 && _missesSaid == 0)
                    {
                        _missesSaid = 1;
                        _line = "Мимо. Удар идёт туда, куда смотрите: подойдите вплотную и смотрите на чучело.";
                    }
                    var cam = Camera.main != null ? Camera.main.GetComponent<RTS_Camera>() : null;
                    if (cam == null || !cam.WarriorModeNow) break;
                    // Блок считается только встреченный: чучело замахнулось в ответ, ПКМ был зажат.
                    if (DreamRoad.DummyRipostes > _ripostesBefore)
                    {
                        _ripostesBefore = DreamRoad.DummyRipostes;
                        _line = "Чучело ударило в ответ. Держите ПКМ, пока оно замахивается.";
                    }
                    if (DreamRoad.DummyBlocks > _blocksBefore && !Speaking())
                        BeginStandoff();
                    break;
                }
                case Stage.Jars:
                {
                    // Камень — не декорация (автор, 08.11: «зачем банки по пути, если мы просто нажимаем
                    // кнопку на этом месте»). На полке банка с чужой душой: её берут в суму (E), выбирают
                    // в суме (J) и берут душу в руку (R) — с ней в руке идут к воротам.
                    // J и R — настоящие SatchelHands; урок только смотрит, что стало с сумой и руками.
                    if (_jarIndex < 0)
                    {
                        if (!DreamRoad.NearShrine(_hero.transform.position)) break;
                        if (!_atShrine) { _atShrine = true; _line = "На камне — банка с душой. E — забрать её в суму."; }
                        if (!Pressed(KeyCode.E)) break;
                        if (!Satchel.AddJar()) { Set(Stage.Return, GateLine); break; }   // сума полна — к воротам
                        _jarIndex = Satchel.Jars - 1;
                        Satchel.Fill(_jarIndex, new SoulData(Loc.N("Путник у дороги"), SinType.Sloth, MoralType.Neutral, 1, 35f),
                                     SoulQuality.Acceptance);
                        DreamRoad.Instance?.TakeSoulJar();
                        _line = "Банка в суме. J — выберите её: под рукой та, что в рамке.";
                        break;
                    }
                    // Автор, 12.11: «обязательно взять в руку душу с камня. А после просто открыть
                    // ворота» — обратно в банку не просим. 16.11: «непонятно зачем брать душу в руку —
                    // почему бы не брать банку с душой?» — в руке банка (SoulInHandView), и сказано зачем.
                    if (Satchel.Selected != _jarIndex) break;
                    if (Sinbinder.Crypt.CryptHands.Empty) { _line = "Под рукой — банка с душой. R — возьмите её в руку: в склепе душу несут к телу в руках."; break; }
                    _soulHeld = true;
                    Set(Stage.Return, Loc.T("Банка в руке.") + " " + Loc.T(GateLine));
                    break;
                }
                case Stage.Return:
                    Seals();
                    if (!_sealsOpen) break;
                    if (!DreamRoad.NearGate(_hero.transform.position) && !OnSeal(_hero.transform.position, DreamRoad.LordSealAt)) break;
                    if (Pressed(KeyCode.F)) StartCoroutine(Crypt());
                    break;
            }
        }

        private void BeginPractice(string line)
        {
            var combat = _hero.GetComponent<FirstPersonCombat>();
            if (combat == null)
            {
                Debug.LogError("[СОН QA] У Греховода нет FirstPersonCombat (SinbinderPlayer.Spawn).");
                BeginStandoff();
                return;
            }
            _swingsBefore = combat.AcceptedSwings;
            _blockFramesBefore = combat.AcceptedBlockFrames;
            Set(Stage.Practice, line);
        }

        private IEnumerator DyingEncounter()
        {
            if (_wounded == null || _wounded.IsDead) yield break;
            var lines = new[] { "Они вернулись...", "Я ничего у них не брал...", "Не пускай их к воротам..." };
            string line = lines[_tutorialSeed % lines.Length];
            Log(_wounded.DisplayName + ": «" + line + "»");
            // Из последних сил: по два слова, с остановкой на вдох, всё тише — и обрывается.
            var words = line.Replace("...", "").Split(' ');
            for (int i = 0; i < words.Length; i += 2)
            {
                if (_wounded == null || _wounded.IsDead) yield break;
                string piece = string.Join(" ", words, i, Mathf.Min(2, words.Length - i)) + "...";
                UI.SpeechBubbles.Say(_wounded, Loc.T(piece), 2.2f);
                yield return new WaitForSeconds(1.5f);
            }
            yield return new WaitForSeconds(0.6f);
            if (_wounded == null || _wounded.IsDead) yield break;
            var hp = _wounded.GetComponent<Damageable>();
            if (hp == null) { Log(Loc.N("Нет Damageable у раненого.")); yield break; }
            hp.TakeDamage(hp.HP + hp.MaxHP + 1f, null);
        }

        private void BeginHarvest()
        {
            // Отсчёт запасного пути стоит вместе с миром, пока игрок читает урок.
            _harvestSince = Time.time;
            Set(Stage.Harvest, Loc.N("Подойди к душе и нажми E. Свежая душа сохраняет память."));
        }

        private int FindJar()
        {
            for (int i = 0; i < Satchel.Jars; i++)
                if (Satchel.At(i).FullJar) return i;
            return -1;
        }

        private void TryRebind()
        {
            int jar = FindJar();
            var soul = jar >= 0 ? Satchel.At(jar).Soul : _fallbackSoul;
            if (soul == null) { Log(Loc.N("Сначала забери душу в банку.")); return; }
            // A tutorial reanimation is a local, explicit shortcut to Raising.Rise,
            // not a second copy of the binding/shell maths.
            var relations = new RelationshipSystem(AOS.MemoryProcessor.Instance);
            var risen = Raising.Rise(soul, ShellType.Zombie,
                _woundedAt + new Vector3(2f, 0f, 0f), Quaternion.identity, relations, corpse: _wounded);
            if (risen == null) { Log(Loc.N("Не удалось поднять тело. Душа осталась в банке.")); return; }
            if (jar >= 0) Satchel.TakeSoul(jar); // Only remove after successful creation.
            else if (_wounded != null) SoulManager.Instance?.RemoveFadingSoul(_wounded);
            _fallbackSoul = null;
            if (_wounded != null) _wounded.gameObject.SetActive(false);
            _zombie = risen;
            // This is actual AOS memory keyed to the actors, not Attack(killer).
            if (AOS.MemoryProcessor.Instance != null)
                foreach (var killer in _killerSketches)
                    AOS.MemoryProcessor.Instance.CreateMemory(_zombie, "MurderedMe", killer.Name,
                        AOS.EmotionType.Anger, 0.95f);
            _commandWait = 0f;
            _orderedAt = -1f;
            MarkCommand();
            Set(Stage.Command, Loc.N("Выделите воина и отдайте приказ ПКМ по земле. Исполнит ли — решает душа."));
        }

        /// <summary>
        /// Убийцы возвращаются и встают в задних воротах форта, на дороге к склепу. И они, и он
        /// замирают: миг ждёт приказа игрока. Решать воин начнёт, когда приказ прозвучит.
        /// С 17.11 — после уроков у чучела: суд ближе к концу сна и судит этот же миг.
        /// </summary>
        private void BeginStandoff()
        {
            if (_stage == Stage.Standoff) return;
            _escorting = false;
            // Глазами приказ не отдать — камера краном поднимается в вид сверху.
            var eye = Camera.main != null ? Camera.main.GetComponent<RTS_Camera>() : null;
            if (eye != null && eye.FirstPersonNow) StartCoroutine(eye.Crane(1.2f));
            // Отстал (Греховод ушёл вперёд, прогон переставил) — догнал.
            if (_zombie != null && !_zombie.IsDead && GroundDistance(_zombie.transform.position, _hero.transform.position) > 7f)
            {
                var h = _hero.transform;
                var beside = h.position - Flat(h.forward) * 2f + Flat(h.right) * 1.2f;
                var legs = _zombie.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (legs != null && legs.enabled && legs.isOnNavMesh) legs.Warp(beside);
                else _zombie.transform.position = beside;
            }
            _standoffSince = Time.time;
            _standoffReal = Time.unscaledTime;
            _autoOrdered = false;
            MarkCommand();
            Hold(_zombie, true);
            _fleeing = false;
            Set(Stage.Standoff, Grammar.Pick(_zombie != null ? _zombie.Gender : Gender.Male,
                Loc.N("Его убийцы вернулись."), Loc.N("Её убийцы вернулись.")));
            StartCoroutine(ReleaseKillers());
        }

        /// <summary>
        /// Убийцы узнали того, кого оставили на дороге, — и бегут (замысел автора, docs/68): один к рычагу
        /// моста, другой к засаде за рекой. Поднятого отпускают: гнаться или нет — решает его душа.
        /// </summary>
        private void Flee()
        {
            _fleeing = true;
            _fleeSince = Time.time;
            _chaseFrom = _zombie != null ? Vector3.Distance(_zombie.transform.position, DreamRoad.BridgeWestEnd) : 0f;
            for (int i = 0; i < _killers.Count; i++)
            {
                var k = _killers[i];
                if (k == null || k.IsDead) continue;
                Hold(k, true);   // ум молчит — ведут ноги
                Run(k, i == 0 ? DreamRoad.LeverStand : DreamRoad.AmbushAt + new Vector3(1.4f, 0f, 1.0f));
            }
            // Прежний приказ (первый урок) — снят: иначе душа «отказывает» давно исполненному приказу.
            if (_zombie != null) _zombie.ClearCommand();
            // Прогон ветки «не погнался» (верстак: dream nochase): ум молчит до приказа «догнать».
            Hold(_zombie, Application.isBatchMode && System.Environment.GetEnvironmentVariable("SINBINDER_NOCHASE") == "1");
            Mark("убийцы бегут");
            Set(Stage.Chase, Grammar.Pick(_zombie != null ? _zombie.Gender : Gender.Male,
                Loc.N("Убийцы бегут к мосту. Гнаться ли за ними — решает его душа."),
                Loc.N("Убийцы бегут к мосту. Гнаться ли за ними — решает её душа.")));
        }

        /// <summary>Где и как идёт — для отчёта прогона.</summary>
        public static string Legs(Warrior w)
        {
            if (w == null) return "нет";
            var p = w.transform.position;
            var legs = w.GetComponent<UnityEngine.AI.NavMeshAgent>();
            string s = $"{w.DisplayName} ({p.x:F1}; {p.z:F1})" + (w.IsDead ? " мёртв" : "");
            if (legs == null) return s + ", ног нет";
            if (!legs.enabled || !legs.isOnNavMesh) return s + $", ноги {(legs.enabled ? "вне навмеша" : "выключены")}";
            return s + $", стоп {legs.isStopped}, путь {(legs.hasPath ? legs.pathStatus.ToString() : "нет")}, "
                     + $"до цели {legs.remainingDistance:F1}, цель ({legs.destination.x:F1}; {legs.destination.z:F1}), скорость {legs.velocity.magnitude:F1}";
        }

        private static void Run(Warrior w, Vector3 to)
        {
            var legs = w != null ? w.GetComponent<UnityEngine.AI.NavMeshAgent>() : null;
            if (legs == null || !legs.enabled || !legs.isOnNavMesh) return;
            legs.isStopped = false;
            legs.SetDestination(to);
        }

        /// <summary>
        /// Погоня к мосту. Тот, кто у рычага, поднимает мост, когда поднятый проскочил (или не погнался)
        /// и над рекой никого: хозяин остаётся на этом берегу, воин — на том.
        /// </summary>
        private void Chase()
        {
            var road = DreamRoad.Instance;
            var g = _zombie != null ? _zombie.Gender : Gender.Male;
            bool alive = _zombie != null && !_zombie.IsDead;
            if (road == null) { StartBattle(Loc.N("Убийцы идут.")); return; }
            var puller = _killers.Count > 0 ? _killers[0] : null;
            if (AskChase(puller, alive, g)) return;
            if (!DreamRoad.BridgeRaised)
            {
                bool there = puller != null && !puller.IsDead && DreamRoad.AtLever(puller.transform.position);
                if (!there && Time.time - _fleeSince < 25f) return;
                if (!there) Debug.LogWarning("[СОН QA] Убийца не дошёл до рычага за 25 с: " + Legs(puller));
                bool crossed = alive && DreamRoad.PastRiver(_zombie.transform.position);
                // После суда над приказом «догнать» — свои 16 с на бег; отказал — ждать нечего.
                float since = Time.time - (_chaseDecided ? _chaseDecidedAt : _fleeSince);
                if (alive && !crossed && !_chaseRefused && since < 16f) return;
                if ((alive && DreamRoad.OnRiverSpan(_zombie.transform.position)) || DreamRoad.OnRiverSpan(_hero.transform.position)) return;
                road.PullLever(puller != null ? puller.DisplayName : "убийца");
                if (puller != null) Run(puller, DreamRoad.AmbushAt + new Vector3(-1.4f, 0f, -1.0f));
                Mark("мост подняли");
                return;
            }
            if (!DreamRoad.BridgeFullyUp) return;
            if (alive && DreamRoad.PastRiver(_zombie.transform.position))
            {
                _standoffReal = Time.unscaledTime;
                _autoOrdered = false;
                RefusalResult = null; // Do not reuse the chase verdict as a bridge verdict.
                MarkCommand();
                // В кадре — оба берега: Греховод у настила, воин у рычага (кадр 19.11: вид остался у стоянки,
                // и поднятого, которого урок просит выделить, на экране не было).
                var eye = Camera.main != null ? Camera.main.GetComponent<RTS_Camera>() : null;
                if (eye != null) eye.Overlook(Vector3.Lerp(_zombie.transform.position, _hero.transform.position, 0.5f));
                Set(Stage.Bridge, Grammar.Pick(g,
                    Loc.N("Мост подняли у него за спиной. Он за рекой, у рычага. Прикажите ему опустить мост: выделите его и ПКМ по рычагу."),
                    Loc.N("Мост подняли у неё за спиной. Она за рекой, у рычага. Прикажите ей опустить мост: выделите её и ПКМ по рычагу.")));
                return;
            }
            // Не погнался — тоже его решение: суда нет, мост поднят, к засаде — лесом.
            RefusalResult = alive ? "не погнался" : null;
            Mark("не погнался");
            _battleStarted = true;
            Set(Stage.Battle, alive
                ? Grammar.Pick(g, Loc.N("Он не погнался — остался с вами. Мост поднят: к засаде — в обход, лесом."),
                                  Loc.N("Она не погналась — осталась с вами. Мост поднят: к засаде — в обход, лесом."))
                : Loc.N("Мост поднят. К засаде — в обход, лесом."));
        }

        /// <summary>
        /// Не гонится сам — это тоже решение его души, но суд взвешивает приказ, а приказа не было:
        /// в этой ветке игрок не видел бы ни суда, ни проверки. Через 6 с, если он не идёт к мосту,
        /// сон просит приказ «догнать» (ПКМ по убийце) и держит мост опущенным, пока душа решает;
        /// решение идёт в суд, как у рычага. Медлит игрок — через 14 с (под плашкой 45) приказ
        /// отдаёт сам Греховод. Это не сценарий под один грех: просьба та же при любой душе.
        /// </summary>
        private bool AskChase(Warrior puller, bool alive, Gender g)
        {
            if (!alive || _chaseDecided || DreamRoad.BridgeRaised) return false;
            if (!_chaseAsked)
            {
                if (Time.time - _fleeSince < 6f || DreamRoad.PastRiver(_zombie.transform.position)
                    || DreamRoad.OnRiverSpan(_zombie.transform.position)
                    || Vector3.Distance(_zombie.transform.position, DreamRoad.BridgeWestEnd) < _chaseFrom - 4f)
                    return false;
                _chaseAsked = true;
                _chaseAskedAt = Time.unscaledTime;
                MarkCommand();
                Mark("не гонится сам — нужен приказ");
                Set(Stage.Chase, Grammar.Pick(g,
                    Loc.N("Он не гонится сам. Прикажите ему догнать убийц: выделите его и ПКМ по убийце."),
                    Loc.N("Она не гонится сама. Прикажите ей догнать убийц: выделите её и ПКМ по убийце.")));
                return true;
            }
            if (!_autoChase && !ChaseOrderedSinceMark() && !SoulCourt.Sitting && puller != null && !puller.IsDead
                && Time.unscaledTime - _chaseAskedAt > (Lesson.Holding ? 45f : 14f))
            {
                _autoChase = true;
                Log(Loc.N("Греховод сам велит догнать убийц."));
                _zombie.IssueCommand(CommandKind.Attack, puller.transform.position, puller.gameObject);
            }
            if (ChaseOrderedSinceMark())
            {
                _courtOrder = _zombie.Command.Kind;
                Mark("приказ догнать: " + _courtOrder);
                _decisionOrderRevision = _zombie.CommandRevision;
                _decisionManualDecideTried = false;
                _decideSince = Time.unscaledTime;
                _chaseCourt = true;
                Hold(_zombie, false);      // теперь решает он
                _stage = Stage.Deciding;   // дальше — как у рычага: решение, весы над головой, суд
            }
            return true;   // мост не поднимают, пока он решает
        }

        /// <summary>Добежавшие до засады (или стоящие) убийцы — снова обычные противники.</summary>
        private void ArmArrived()
        {
            foreach (var k in _killers)
            {
                if (k == null || k.IsDead || _armed.Contains(k)) continue;
                var legs = k.GetComponent<UnityEngine.AI.NavMeshAgent>();
                bool running = legs != null && legs.enabled && legs.isOnNavMesh && !legs.isStopped
                    && (legs.pathPending || legs.remainingDistance > 0.6f);
                if (running) continue;
                _armed.Add(k);
                Hold(k, false);
                var hp = k.GetComponent<Damageable>();
                if (hp != null) hp.Initialize(9f, 0f);   // теперь — обычные противники
            }
        }

        private IEnumerator ReleaseKillers()
        {
            yield return new WaitForSeconds(0.6f);
            Vector3 near = _zombie != null ? _zombie.transform.position : _woundedAt;
            for (int i = 0; i < _killerSketches.Count; i++)
            {
                var k = _killerSketches[i];
                var soul = new SoulData(k.Name, k.Sin, k.Moral, 1, k.Strength, k.Seed);
                soul.SetTrade(k.Trade);
                // На дороге перед ним, со стороны моста: видно, но не вплотную.
                float sNear = DreamRoad.Progress(near);
                float s = Mathf.Max(Mathf.Min(sNear + 9f, AheadLimit() - 1.5f), sNear + 4f);
                var fwd = DreamRoad.RouteForward(s);
                var at = DreamRoad.RoutePoint(s) + new Vector3(fwd.z, 0f, -fwd.x) * (-1.3f + i * 2.6f);
                var enemy = CreateLiving(soul, at, Team.Enemy);
                if (enemy == null) continue;
                enemy.Attack = Mathf.Min(enemy.Attack, 2.5f); // Weaker than the hero.
                var hp = enemy.GetComponent<Damageable>();
                // До боя их не срубить: срубленные раньше приказа уводили мимо суда.
                if (hp != null) hp.Initialize(999f, 0f);
                enemy.transform.rotation = Quaternion.LookRotation(Flat(near - at));
                Hold(enemy, true);
                _killers.Add(enemy);
                // Link memory to concrete actor identity as well as the old mortal name.
                if (_zombie != null && AOS.MemoryProcessor.Instance != null)
                    AOS.MemoryProcessor.Instance.CreateMemory(_zombie, "MurderedMe", enemy.Id,
                        AOS.EmotionType.Anger, 1f);
            }
            if (_killers.Count > 0 && _zombie != null)
            {
                // В кадре — и он, и они: вид сверху встаёт посередине (над Греховодом убийцы
                // стояли у верхнего края кадра, 17.11).
                var centre = Vector3.zero;
                foreach (var k in _killers) centre += k.transform.position;
                centre /= _killers.Count;
                var eye = Camera.main != null ? Camera.main.GetComponent<RTS_Camera>() : null;
                if (eye != null) eye.Overlook(Vector3.Lerp(_zombie.transform.position, centre, 0.5f));
                StartCoroutine(Recognized(_killers[0], _zombie));
            }
            if (_killers.Count == 0)
            {
                Debug.LogError("[СОН QA] No killers spawned. Check dependencies and Unity Console.");
                Hold(_zombie, false);
                Set(Stage.Return, Loc.T("Убийцы не появились. Воспоминание прервано.") + " " + Loc.T(GateLine));
            }
        }

        /// <summary>
        /// Знакомый голос среди убийц (FIRST-FIVE-MINUTES-10-DRAMAS, 01; из черновика 16.11 оставлена
        /// только строка узнавания): убийца узнаёт того, кого оставил на дороге, по имени. Одна
        /// строка — и это уже не «враги», а те самые. Какая из двух — по номеру сна.
        /// </summary>
        private IEnumerator Recognized(Warrior killer, Warrior victim)
        {
            yield return new WaitForSecondsRealtime(0.9f);
            if (killer == null || victim == null || victim.IsDead) yield break;
            var g = victim.Gender;
            string line = _dreamNo % 2 == 1
                ? Loc.F("Глянь… {0}? Я ж тебя на дороге оставил.", victim.DisplayName)
                : Loc.F("{0}?! {1} {2}", victim.DisplayName,
                        Grammar.Pick(g, Loc.T("Ты ж подох."), Loc.T("Ты ж подохла.")),
                        Grammar.Pick(killer.Gender, Loc.T("Сам проверял."), Loc.T("Сама проверяла.")));
            UI.SpeechBubbles.Say(killer, line, 4f);
            Log(killer.DisplayName + ": «" + line + "»");
            Mark("убийца узнал его");
        }

        /// <summary>
        /// Суд над решением — каким бы оно ни было. Отказ, колебание, послушание —
        /// суд показывает, чьи голоса это решили.
        /// </summary>
        private IEnumerator Judge(AOS.Decision d, AOS.DecisionContext c, List<AOS.Vote> originalVotes)
        {
            RefusalResult = d.RefusedCommand ? "отказал" : d.Hesitated ? "колебался"
                : ActuallyObeyed(d, c) ? "послушался" : "выбрал своё";
            TraceFirstFive("first_verdict action=" + d.Action + " outcome=" + RefusalResult);
            _courtVoice = Voice(d, c);
            Mark($"T3 решение при убийцах: {RefusalResult} ({d.Action}, голос {_courtVoice})");
            int murders = 0;
            if (c != null && c.RecentMemories != null)
                foreach (var m in c.RecentMemories) if (m != null && m.EventType == "MurderedMe") murders++;
            Debug.Log($"[СОН] Решение при убийцах: {RefusalResult} ({d.Action}, причина {d.Decisive}, голос {d.DecisiveVoice}); "
                + $"убийцы на глазах {(c != null && c.KnownMurdererInSight ? "да" : "нет")}, памяти об убийстве {murders}");

            // Миг на его слово и малые весы над головой — потом суд, который их объяснит.
            // В лагере игрок узнает эти весы над отказавшим (63-TEACHING, п. 3).
            // Во сне — при любом решении: суд разбирает любое (в лагере — только отказ).
            if (_zombie != null) UI.ScalesOverhead.Show(_zombie, d, c, force: true,
                originalVotes: originalVotes);
            yield return new WaitForSecondsRealtime(1.8f);
            // Первый суд во сне — всегда полный: это урок. Второй (у рычага после суда над
            // «догнать») — коротко, как суды по делу потом.
            if (_zombie != null && !_zombie.IsDead)
            {
                Mark("T8 суд запрошен");
                TraceFirstFive("court_requested");
                // The Court coroutine can yield break without ever opening
                // (missing camera/Letterbox). An enumerator ending is NOT a
                // completed lesson. Keep the first Court full until it succeeds.
                int completedBefore = SoulCourt.Completed;
                bool briefCourt = _courts > 0;
                // Суд во сне — урок: идёт и при выключенных автоматических судах (force).
                yield return SoulCourt.Convene(_zombie, d, c, _courtOrder, brief: briefCourt,
                    originalVotes: originalVotes, force: true);
                if (SoulCourt.Completed > completedBefore)
                {
                    _courts++;
                    TraceFirstFive("court_completed duration_s=" + SoulCourt.LastDurationSeconds.ToString("F1")
                        + " brief=" + briefCourt);
                    Mark("T8 суд показан за " + SoulCourt.LastDurationSeconds.ToString("F1") + " с");
                }
                else
                {
                    // Do not pretend the lesson happened and do not turn the
                    // next attempted first Court into the abbreviated version.
                    TraceFirstFive("court_not_shown");
                    Mark("T8 суд не состоялся");
                    Debug.LogWarning("[СОН QA] Суд не показан: отсутствует завершённый проход " +
                        "(проверить камеру, Letterbox и ошибки Console).");
                }
            }
            _courtRunning = false;

            var g = _zombie != null ? _zombie.Gender : Gender.Male;
            if (_chaseCourt)
            {
                // Суд над приказом «догнать»: дальше снова погоня — с ним или без него.
                _chaseCourt = false;
                _chaseDecided = true;
                _chaseDecidedAt = Time.time;
                _chaseRefused = !ActuallyObeyed(d, c);
                Set(Stage.Chase, _chaseRefused
                    ? Grammar.Pick(g, Loc.N("Не погнался и по приказу. Убийцы уходят за мост."), Loc.N("Не погналась и по приказу. Убийцы уходят за мост."))
                    : Grammar.Pick(g, Loc.N("Гонится — по вашему приказу. Идите за ним к мосту."), Loc.N("Гонится — по вашему приказу. Идите за ней к мосту.")));
                yield break;
            }
            if (!ActuallyObeyed(d, c))
                StartBattle(Grammar.Pick(g,
                    Loc.N("Он решил сам — гонится за убийцами к засаде. Мост поднят: идите в обход, лесом."),
                    Loc.N("Она решила сама — гонится за убийцами к засаде. Мост поднят: идите в обход, лесом.")));
            else
                StartBattle(Grammar.Pick(g, Loc.N("Он идёт к рычагу. Мост опустится — убийцы ждут за рекой, у засады."),
                                            Loc.N("Она идёт к рычагу. Мост опустится — убийцы ждут за рекой, у засады.")));
        }

        /// <summary>Бой: убийцы становятся обычными противниками, добежав до засады (ArmArrived).</summary>
        private void StartBattle(string line)
        {
            ArmArrived();
            if (_zombie != null) Hold(_zombie, false);
            _battleStarted = true;
            Set(Stage.Battle, line);
        }

        // ── Засада (docs/68, этап 5): пришёл ли владыка ──────────────────────────────
        // Отказал и убежал мстить один — бьётся у засады, пока Греховод идёт в обход. Успел —
        // поднятый запомнит, что владыка пришёл; пал до прихода — душа угасает у тела, как у
        // любого павшего (E, R), и, встав снова, он помнит, что владыка не пришёл. Мера общая
        // для любой души и любого греха: память и верность, а не реплика под одну душу.
        private const float LordCameGain = 10f, LordAbsentLoss = 15f, LordNear = 12f;
        private Warrior _lateBody;
        private Vector3 _lateAt;
        private float _lateSince;
        private bool _lordCame, _remembered;

        private static bool Refused => RefusalResult == "отказал" || RefusalResult == "колебался" || RefusalResult == "выбрал своё";

        /// <summary>Греховод рядом с поднятым, пока тот бьётся у засады, — пришёл.</summary>
        private void WatchLord()
        {
            if (_lordCame || _zombie == null || _zombie.IsDead || _hero == null) return;
            if (!_killers.Exists(k => k != null && !k.IsDead)) return;
            if (GroundDistance(_zombie.transform.position, DreamRoad.AmbushAt) > 15f) return;
            if (GroundDistance(_hero.transform.position, _zombie.transform.position) > LordNear) return;
            _lordCame = true;
            Mark("T5 засада: владыка пришёл");
        }

        private void Fall()
        {
            _lateBody = _zombie;
            _lateAt = _zombie.transform.position;
            _lateSince = Time.time;
            bool alone = Refused && !_lordCame;
            LateResult = alone ? "пал до вашего прихода" : "пал";
            Mark("T5 засада: " + LateResult);
            var g = _zombie.Gender;
            Set(Stage.Fallen, alone
                ? Grammar.Pick(g, Loc.N("Он пал до вашего прихода. Душа ещё у тела и угасает: подойдите — E, потом R, и он встанет снова."),
                                  Loc.N("Она пала до вашего прихода. Душа ещё у тела и угасает: подойдите — E, потом R, и она встанет снова."))
                : Grammar.Pick(g, Loc.N("Он пал. Душа ещё у тела и угасает: подойдите — E, потом R, и он встанет снова."),
                                  Loc.N("Она пала. Душа ещё у тела и угасает: подойдите — E, потом R, и она встанет снова.")));
        }

        /// <summary>Душа этого тела ещё гаснет рядом с ним (не собрана и не угасла).</summary>
        private static bool Fading(Warrior body)
        {
            var souls = SoulManager.Instance != null ? SoulManager.Instance.GetAllFadingSouls() : null;
            return body != null && souls != null && souls.Exists(s => s.Warrior == body);
        }

        /// <summary>R у тела павшего: та же жатва и то же поднятие, что у дороги, — в его же тело.</summary>
        private void Reraise()
        {
            int jar = FindJar();
            if (jar < 0) { Log(Loc.N("Сначала заберите душу в банку — E у тела.")); return; }
            var face = Flat(_hero.transform.position - _lateAt);
            if (face.sqrMagnitude < 0.01f) face = Vector3.forward;
            var relations = new RelationshipSystem(AOS.MemoryProcessor.Instance);
            var risen = Raising.Rise(Satchel.At(jar).Soul, ShellType.Zombie, _lateAt + face * 0.8f,
                                     Quaternion.LookRotation(face), relations, corpse: _lateBody);
            if (risen == null) { Log(Loc.N("Не удалось поднять тело. Душа осталась в банке.")); return; }
            Satchel.TakeSoul(jar);
            if (_lateBody != null) _lateBody.gameObject.SetActive(false);
            _zombie = risen;
            // Убийцы — всё те же его убийцы: свежая душа помнит (SoulDecay).
            if (AOS.MemoryProcessor.Instance != null)
                foreach (var k in _killers)
                    if (k != null && !k.IsDead)
                        AOS.MemoryProcessor.Instance.CreateMemory(_zombie, "MurderedMe", k.Id, AOS.EmotionType.Anger, 1f);
            LateResult += "; поднят снова";
            Mark("T5 засада: поднят снова — " + _zombie.DisplayName);
            var g = _zombie.Gender;
            bool abandoned = Refused && !_lordCame;
            if (Refused) Remember(abandoned);
            Set(Stage.Battle, abandoned
                ? Grammar.Pick(g, Loc.N("Он снова на ногах — и помнит, что вы не пришли. Верность к вам упала."),
                                  Loc.N("Она снова на ногах — и помнит, что вы не пришли. Верность к вам упала."))
                : Grammar.Pick(g, Loc.N("Он снова на ногах."), Loc.N("Она снова на ногах.")));
        }

        /// <summary>Пришёл ли владыка — в память и в верность. Один раз за сон.</summary>
        private void Remember(bool abandoned)
        {
            if (_remembered || _zombie == null || _hero == null) return;
            _remembered = true;
            float before = _zombie.Loyalty;
            _zombie.ChangeLoyalty(abandoned ? -LordAbsentLoss : LordCameGain);
            if (AOS.MemoryProcessor.Instance != null)
                AOS.MemoryProcessor.Instance.CreateMemory(_zombie, abandoned ? "LordAbsent" : "LordCame", _hero.Id,
                    abandoned ? AOS.EmotionType.Sadness : AOS.EmotionType.Joy, 0.9f);
            string what = abandoned ? "владыка не пришёл" : "владыка пришёл";
            LateResult = (string.IsNullOrEmpty(LateResult) ? "" : LateResult + "; ") + $"помнит: {what}, верность {before:F0} → {_zombie.Loyalty:F0}";
            Mark($"T5 память: {what}, верность {before:F0} → {_zombie.Loyalty:F0}");
            UI.SpeechBubbles.Say(_zombie, abandoned ? Loc.T("Вы не пришли.") : Loc.T("Вы пришли."), 3.5f);
            if (!abandoned)
                Log(Grammar.Pick(_zombie.Gender, Loc.N("Вы пришли к нему в бою — он это запомнит. Верность к вам выросла."),
                                                 Loc.N("Вы пришли к ней в бою — она это запомнит. Верность к вам выросла.")));
        }

        /// <summary>
        /// Тот же приказ в изменившихся обстоятельствах. Смена решения не
        /// доказывает, какой голос или событие его изменили: слова не должны
        /// выдавать корреляцию за причину. Суд хранит прежнее голосование.
        /// </summary>
        private IEnumerator Contrast(AOS.Decision d, AOS.DecisionContext c)
        {
            bool obeyed = ActuallyObeyed(d, c);
            RetestResult = obeyed ? "послушался" : "снова нет";
            TraceFirstFive("second_verdict action=" + d.Action + " outcome=" + RetestResult);
            string voice = Voice(d, c);
            Debug.Log($"[СОН] Тот же приказ без убийц: {RetestResult} ({d.Action}, голос {voice})");
            Mark($"T6 тот же приказ без убийц: {RetestResult} ({d.Action}, голос {voice})");

            // Названный голос — наблюдаемый результат голосования, не доказанная
            // единственная причина. Не утверждать, будто прежняя причина исчезла:
            // новый выбор может совпасть с приказом по совсем другому мотиву.
            var g = _zombie != null ? _zombie.Gender : Gender.Male;
            string named = _zombie != null && !string.IsNullOrEmpty(voice)
                ? UI.SelectedUnitPanelUI.VoiceName(voice, _zombie.Soul) : null;
            string line = obeyed
                ? Grammar.Pick(g,
                    Loc.T("Тот же приказ — и теперь его решение совпало с вашим. Убийц рядом больше нет."),
                    Loc.T("Тот же приказ — и теперь её решение совпало с вашим. Убийц рядом больше нет."))
                : string.IsNullOrEmpty(named)
                    ? Loc.T("И снова нет. Убийц рядом больше нет; какой голос перевесил, неизвестно.")
                    : voice == _courtVoice
                        ? Loc.F("И снова нет. Среди голосов вновь выделяется {0}.", named)
                        : Loc.F("И снова нет. Теперь среди голосов выделяется {0}.", named);

            var camera = Dialogue.DialogueCameraController.Instance;
            if (camera != null && _zombie != null && UI.Letterbox.Instance != null)
            {
                yield return camera.WaitUntilFree(4f);
                camera.SaveCameraPosition();
                yield return camera.FocusOn(_zombie.transform);
                UI.Letterbox.Instance?.Say(null, line);
                yield return UI.Letterbox.Hold(Herald.Hold(line) + 0.5f);
                yield return camera.RestoreCamera();
            }
            Log(line);
            _contrastRunning = false;
            // «Снова нет» — приказ можно не повторять, а искусить (вариант «Шёпот», docs/68): Z рядом с отказавшим.
            // Шёпот — общая механика игры, на любой грех; у моста через реку он не достаёт (слышен вблизи), здесь — рядом.
            if (!obeyed && _zombie != null && !_zombie.IsDead && (Whisper.Can(_zombie) || Whisper.CanIfCloser(_zombie)))
            {
                Whisper.LastResult = null;
                _whisperSince = Time.unscaledTime;
                Set(Stage.Whisper, Grammar.Pick(g,
                    Loc.N("Приказ можно и не повторять. Подойдите к нему и нажмите Z — шепните его греху. Что шепнуть, решит его душа."),
                    Loc.N("Приказ можно и не повторять. Подойдите к ней и нажмите Z — шепните её греху. Что шепнуть, решит её душа.")));
            }
            else Set(Stage.Jars, JarsLine);
        }

        /// <summary>
        /// Шёпот после «снова нет». Поддался — исполняет тот же приказ: идёт к рычагу, мост опускается (рычаг
        /// принимает и уговорённого — DreamRoad.LeverCalls). Не поддался или не шептали — дорога к колоннам и так открыта.
        /// </summary>
        private void WhisperStep()
        {
            if (_zombie == null || _zombie.IsDead) { Set(Stage.Jars, JarsLine); return; }
            if (Whisper.LastResult == null || Whisper.Busy)
            {
                if (Whisper.LastResult == null && !Whisper.Busy && Time.unscaledTime - _whisperSince > (Lesson.Holding ? 60f : 30f))
                {
                    Log(Loc.T("Шептать не обязательно: дорога к колоннам открыта."));
                    Set(Stage.Jars, JarsLine);
                }
                return;
            }
            bool yielded = Whisper.Persuaded(_zombie);
            WhisperResult = yielded ? "поддался" : "не поддался";
            Mark("шёпот: " + WhisperResult);
            var g = _zombie.Gender;
            if (yielded)
            {
                // Исполнять — сразу, не ждать такта решения: уговор держится недолго.
                _zombie.GetComponent<AOS.AOSWarriorWrapper>()?.Decide();
                Log(Grammar.Pick(g, Loc.T("Поддался. Приказ тот же — другим стало то, что вы пообещали его греху. Он идёт к рычагу."),
                                    Loc.T("Поддалась. Приказ тот же — другим стало то, что вы пообещали её греху. Она идёт к рычагу.")));
            }
            else Log(Grammar.Pick(g, Loc.T("Не поддался: не всякую душу купишь шёпотом."),
                                     Loc.T("Не поддалась: не всякую душу купишь шёпотом.")));
            Set(Stage.Jars, JarsLine);
        }

        /// <summary>
        /// Замереть или ожить: ум (такт решения) и ноги. Замерший не решает
        /// ничего — его решение начнётся, когда отпустят.
        /// </summary>
        private static void Hold(Warrior w, bool hold)
        {
            if (w == null) return;
            var mind = w.GetComponent<AOS.AutoAttackAOS>();
            if (mind != null) mind.enabled = !hold;
            var legs = w.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (legs != null && legs.enabled && legs.isOnNavMesh)
            {
                legs.isStopped = hold;
                if (hold) legs.ResetPath();
            }
        }

        private void KeepHeroAlive()
        {
            // Death restores only the Death God's instrument, never the zombie.
            var body = _hero.GetComponent<Damageable>();
            if (body != null && !body.IsDead && body.HP < body.MaxHP * 0.28f)
                body.Heal(body.MaxHP * 0.55f);
        }

        /// <summary>
        /// Ворота склепа (автор, 07.11): створки расходятся, экран гаснет раньше, чем можно
        /// рассмотреть склеп, — только силуэт Бога Смерти. На чёрном — лагерь, свет за спиной.
        /// </summary>
        private IEnumerator Crypt()
        {
            if (_cryptRunning) yield break;
            _cryptRunning = true;
            _handBusy = true;
            var agent = _hero.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.isOnNavMesh) agent.ResetPath();
            var walk = _hero.GetComponent<PlayerWalk>();
            if (walk != null) walk.enabled = false;
            Debug.Log("[СОН] Ворота склепа открываются.");
            Mark("ворота склепа");
            if (DreamRoad.Instance != null) yield return DreamRoad.Instance.OpenCrypt(_hero);
            Finish();
        }

        private void Finish()
        {
            if (!Application.CanStreamedLevelBeLoaded(_campScene))
            {
                Log(Loc.F("Сцена лагеря отсутствует в Build Settings: {0}", _campScene));
                return;
            }
            Mark("сон кончился");
            SaveTimeline();
            _stage = Stage.Finished;
            // Душа с камня — сна, а не лагеря: руки статичны и пережили бы смену сцены.
            Sinbinder.Crypt.CryptHands.Forget();
            SceneManager.LoadScene(_campScene);
        }

        private static float GroundDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 1e-4f ? Vector3.forward : v.normalized;
        }

        /// <summary>
        /// Шаг сна — как в лагере (автор, 17.11: «в лагере и в сне обучение и плашки с заданиями
        /// отличаются»): что сделать — задачей в списке «Задачи» справа сверху (UI.TaskList), какой
        /// клавишей — на плашке урока, клавиша в рамке (Lesson.Hint). До того у сна была своя
        /// табличка вверху по центру («Воспоминание», 16.11), а в лагере — список и плашка.
        /// Полная строка шага, как и раньше, — в журнале.
        /// </summary>
        private void LateUpdate()
        {
            // Под полосами — кадр, а не урок: плашка спорила бы с репликой.
            bool quiet = _stage == Stage.Finished || _handBusy || SoulCourt.Sitting || UI.TalkView.Shown
                      || Speaking() || UI.Modal.Any || GamePauseController.Instance?.IsPaused == true
                      || (UI.Letterbox.Instance != null && UI.Letterbox.Instance.Shown);
            UpdateDeathLesson(quiet);
            // Упёрся в черту шага — задача вспыхивает: сначала это.
            if (_nudgeAt != _nudgeSent) { _nudgeSent = _nudgeAt; UI.TaskList.Nudge(); }
            if (quiet) UI.TaskList.Place(null, null); else PlaceNow();
            if (!quiet && _stage == Stage.Return)
            {
                var prompt = SealPrompt();
                UI.TaskList.Step(prompt.task);
                if (prompt.key != null) Lesson.Hint(this, prompt.key, prompt.words);
                else Lesson.Unhint(this);
                return;
            }
            if (!quiet && RoleRecoveryHint() is { } recovery)
            {
                UI.TaskList.Step(recovery.words);
                Lesson.Hint(this, recovery.key, recovery.words);
                return;
            }
            if (!quiet) UI.TaskList.Step(TaskTitle());
            if (!quiet && _stage == Stage.Command)
            {
                var hint = _orderedAt < 0f ? FirstOrderHint() : ("", _line);
                Lesson.Hint(this, hint.Item1, hint.Item2);
                return;
            }
            string key = quiet || string.IsNullOrEmpty(_line) ? null : KeyNow();
            if (key != null) Lesson.Hint(this, key, WithDeath(Loc.T(_line)));
            else Lesson.Unhint(this);
        }

        private (string key, string words)? RoleRecoveryHint()
        {
            if (_stage != Stage.WarriorMode && _stage != Stage.DrawWeapon && _stage != Stage.Practice) return null;

            var view = Camera.main != null ? Camera.main.GetComponent<RTS_Camera>() : null;
            if (view == null) return null;
            if (!view.FirstPersonNow) return ("Tab", Loc.T("Tab — посмотрите глазами Греховода."));
            if (!view.WarriorModeNow) return ("V", Loc.T("V — переключитесь с Командира на Воина."));
            return null;
        }

        private float _nudgeSent = -10f;

        /// <summary>
        /// Стрелка к месту шага (автор, 19.11: «не забывай про подсказывающую стрелку»): метка задач, как
        /// в лагере, — над местом его имя, вне кадра — у края со стрелкой. Мост поднят, а цель за рекой —
        /// стрелка ведёт лесной тропой, от поворота к повороту.
        /// </summary>
        private void PlaceNow()
        {
            switch (_stage)
            {
                case Stage.Approach: Place(Loc.T("Умирающий"), _woundedAt, 1.2f, 3f); break;
                case Stage.Harvest: case Stage.Rebind:
                    Place(Loc.T("Тело"), _woundedAt, 1.2f, 0f); break;
                case Stage.Command:
                    if (_zombie != null && !_zombie.IsDead && _orderedAt < 0f)
                        UI.TaskList.Place(Loc.T("Воин"), _zombie.transform.position + Vector3.up * 2.4f, 0f);
                    else UI.TaskList.Place(null, null);
                    break;
                case Stage.Practice: Place(Loc.T("Чучело"), DreamRoad.DummyAt, 2.0f, 2.4f); break;
                case Stage.Chase: Place(Loc.T("Мост"), DreamRoad.BridgeWestEnd, 1.4f, 3f); break;
                case Stage.Bridge: case Stage.Retest:
                    // Приказ — через реку: стрелка на рычаг, в обход не ведёт.
                    UI.TaskList.Place(Loc.T("Рычаг"), DreamRoad.LeverAt + Vector3.up * 2.1f, 0f);
                    break;
                case Stage.Battle: Place(Loc.T("Засада"), DreamRoad.AmbushAt, 1.6f, 6f); break;
                case Stage.Fallen:
                    if (_lateBody != null)
                        Place(Grammar.Pick(_lateBody.Gender, Loc.T("Павший"), Loc.T("Павшая")), _lateAt, 1.2f, 2.5f);
                    break;
                case Stage.Whisper:
                    // К отказавшему — пока шёпот его не достаёт; подошли ближе — метка гаснет.
                    if (_zombie != null)
                        Place(Grammar.Pick(_zombie.Gender, Loc.T("Поднятый"), Loc.T("Поднятая")), _zombie.transform.position, 2.4f, 10f);
                    break;
                case Stage.Jars: Place(Loc.T("Камень с банками"), DreamRoad.ShrineAt, 1.5f, 2.5f); break;
                case Stage.Return:
                    if (_hero != null && DreamRoad.OnSquare(_hero.transform.position)) UI.TaskList.Place(null, null);
                    else Place(Loc.T("Ворота склепа"), new Vector3(0f, 0f, DreamRoad.GateZ), 3.5f, 6f);
                    break;
                default: UI.TaskList.Place(null, null); break;
            }
        }

        private void Place(string word, Vector3 goal, float up, float near)
        {
            var bypass = DreamRoad.BridgeRaised && _hero != null && goal.x > DreamRoad.BridgeAt.x
                ? DreamRoad.Detour(_hero.transform.position) : null;
            if (bypass.HasValue) UI.TaskList.Place(Loc.T("Лесная тропа"), bypass.Value + Vector3.up * 1.5f, 0f);
            else UI.TaskList.Place(word, goal + Vector3.up * up, near);
        }

        /// <summary>Задача шага — коротко, клавиша в скобках, как задачи лагеря.</summary>
        private string TaskTitle()
        {
            switch (_stage)
            {
                case Stage.Approach: return Loc.T("Ведите Греховода по дороге (ПКМ по земле)");
                case Stage.Dying: return Loc.T("Выслушайте умирающего");
                case Stage.Harvest: return Loc.T("Заберите его душу (E)");
                case Stage.Rebind: return Loc.T("Верните душу в тело (R)");
                case Stage.Command: return _orderedAt < 0f
                    ? Loc.T("Отдайте поднятому первый приказ (ЛКМ, затем ПКМ)")
                    : Loc.T("Посмотрите, как воин ответит на приказ");
                case Stage.FirstPerson: return Loc.T("Взгляните глазами Греховода (Tab)");
                case Stage.WarriorMode: return Loc.T("Станьте Воином (V)");
                case Stage.DrawWeapon: return Loc.T("Достаньте оружие (B)");
                case Stage.Practice: return Loc.T("Ударьте чучело и закройтесь от ответа (ЛКМ, ПКМ)");
                case Stage.Standoff: return Loc.T("Убийцы вернулись");
                case Stage.Chase: return _chaseAsked && !_chaseDecided ? Loc.T("Прикажите догнать убийц (ПКМ по убийце)") : Loc.T("Идите к мосту");
                case Stage.Bridge: case Stage.Deciding:
                    return _chaseCourt ? Loc.T("Прикажите догнать убийц (ПКМ по убийце)") : Loc.T("Прикажите опустить мост (ПКМ по рычагу)");
                case Stage.Battle: return DreamRoad.BridgeRaised ? Loc.T("Обойдите реку лесом — к засаде") : Loc.T("Отбейтесь от убийц");
                case Stage.Fallen: return FindJar() < 0 ? Loc.T("Заберите душу павшего (E)") : Loc.T("Верните душу в тело (R)");
                case Stage.Retest: return Loc.T("Тот же приказ — без убийц (ПКМ по рычагу)");
                case Stage.Whisper: return Loc.T("Шепните греху поднятого (Z)");
                case Stage.Jars: return Loc.T("Возьмите банку с камня у колонн (E, J, R)");
                default: return null;
            }
        }

        /// <summary>Клавиша шага без кольца — для плашки урока; null — плашка не нужна.</summary>
        private string KeyNow()
        {
            switch (_stage)
            {
                case Stage.Approach: return Loc.T("ПКМ");
                case Stage.Harvest: return "E";
                case Stage.Rebind: return "R";
                case Stage.Fallen: return FindJar() < 0 ? "E" : "R";
                case Stage.FirstPerson: return "Tab";
                case Stage.WarriorMode: return "V";
                case Stage.DrawWeapon: return "B";
                case Stage.Practice: return Loc.T("ЛКМ");
                case Stage.Bridge: return Loc.T("ПКМ");
                case Stage.Whisper: return "Z";
                case Stage.Jars:
                    if (_jarIndex < 0) return _atShrine ? "E" : null;
                    return Satchel.Selected != _jarIndex ? "J" : "R";
                default: return null;
            }
        }

        private (string task, string key, string words) SealPrompt()
        {
            if (_sealsOpen) return (Loc.T("Откройте ворота склепа (F)"), "F", Loc.T(_line));
            if (!_sealsAsked || _hero == null || !DreamRoad.OnSquare(_hero.transform.position))
            {
                string approach = Loc.T("Идите к воротам склепа");
                return (approach, null, approach);
            }

            int lit = 0;
            foreach (var on in _sealLit) if (on) lit++;
            string task = lit == 0
                ? Loc.T("Зажгите пять печатей: венец — ваша, четыре — для воинов")
                : Loc.F("Горят печати: {0} из 5", lit);

            if (_raiseOffered && !AnyRaisedAlive() && _fallen.Count > 0)
                return (Loc.T("Поднимите павшего стража (E)"), "E", Loc.T(_line));
            if (NearFallen(_hero.transform.position) != null)
                return (task, "E", Loc.T("Рядом павший страж. Нажмите E, чтобы поднять его."));

            // У тёмной печати — кого она ждёт (§5: «при подходе можно узнать требуемый тип, но не
            // правильного воина»). Имён здесь нет нарочно: кто гневный и кто чей брат — узнаёт игрок.
            var near = NearSeal(_hero.transform.position, 2.6f);
            if (near.HasValue && !_sealLit[(int)near.Value]) return (task, Loc.T("ПКМ"), SealNeedLine(near.Value));
            return (task, Loc.T("ПКМ"), Loc.T(_line));
        }

        private static string SealNeedLine(DreamRoad.SealKind kind)
        {
            switch (kind)
            {
                case DreamRoad.SealKind.Lord: return Loc.T("Печать с венцом — ваша: встаньте на неё.");
                case DreamRoad.SealKind.Wrath: return Loc.T("Печать с мечом ждёт гневного. Выделите воина и ПКМ по печати.");
                case DreamRoad.SealKind.Order: return Loc.T("Печать с крестом ждёт того, кто служил Ордену.");
                default: return Loc.T("Сцепленные печати ждут двоих братьев — каждого на свою.");
            }
        }

        // ── Две печати у ворот (план 66, сцена 10) ──
        // Ворота ждут двоих: Греховод — на печать с венцом, воин — на печать с мечом. Половины нет
        // (автор, 17.11: «загадка, которую можно решить наполовину и пройти дальше? серьёзно?») —
        // до того через 30 с ворота пускали и одного. Если воин не пришёл, причина может быть разной;
        // у печатей лежат павшие стражи, поднимите другого (автор: «поднимать других воинов —
        // какой-нибудь не откажется»). Души у стражей свои: один какой выпадет, у второго приказ
        // «встань на печать» ничего в душе не задевает — кто-нибудь да встанет.
        //
        // С 25.11 печатей пять (автор 24.11, №69 §17: «игроку, чтобы продолжить, НЕОБХОДИМО понимать
        // свой отряд… ну или перебирать»): венец — Греховод; меч — гневный; две сцепленные — братья;
        // крест — служивший Ордену. Ворота — когда все пять горят разом (короткое удержание).
        // Стражей складывает генератор с ролями §17.1; встанет ли страж — решает его душа.
        private bool _sealsOpen, _sealsAsked, _escortedToSeals, _raiseOffered, _fallenSpawned;
        private readonly bool[] _sealLit = new bool[5];
        private readonly Warrior[] _sealOn = new Warrior[5];
        private float _sealHeld;
        private float _sealsAskedAt = -1f, _sealOrderAt = -1f;
        private Warrior _sealOrderedWarrior;
        private uint _sealOrderRevision, _sealRefusalSaid;
        private const float SealPatience = 45f, SealWait = 10f;
        private readonly List<Warrior> _fallen = new();
        private readonly List<Warrior> _raised = new();

        /// <summary>Павшие стражи у печатей, ещё не поднятые — для прогона.</summary>
        public IReadOnlyList<Warrior> Fallen => _fallen;
        /// <summary>Последний поднятый у печатей — для прогона.</summary>
        public Warrior LastRaised => _raised.Count > 0 ? _raised[_raised.Count - 1] : null;

        /// <summary>
        /// Где лежат стражи: гневный, два брата рядом, тихий охотник Ордена, охотник какой выпал.
        /// Не на печатях и не в проходе арки (пилоны x = ±4,6 на z = 46).
        /// </summary>
        private static readonly Vector3[] FallenAt =
        {
            new Vector3(-8.0f, 0f, 47.6f),
            new Vector3(-6.6f, 0f, 42.4f), new Vector3(-5.2f, 0f, 43.2f),
            new Vector3(7.8f, 0f, 48.0f),
            new Vector3(4.2f, 0f, 37.6f),
        };

        private static bool OnSeal(Vector3 at, Vector3 seal) => GroundDistance(at, seal) < 1.25f;

        private static DreamRoad.SealKind? NearSeal(Vector3 at, float reach)
        {
            DreamRoad.SealKind? best = null;
            float bestD = reach;
            for (int i = 0; i < DreamRoad.SealAt.Length; i++)
            {
                float d = GroundDistance(at, DreamRoad.SealAt[i]);
                if (d < bestD) { bestD = d; best = (DreamRoad.SealKind)i; }
            }
            return best;
        }

        /// <summary>Подходит ли душа воина печати — условие мира, не решение души (§5, таблица).</summary>
        public static bool Fits(DreamRoad.SealKind kind, Warrior w)
        {
            var soul = w != null ? w.Soul : null;
            if (soul == null) return false;
            switch (kind)
            {
                case DreamRoad.SealKind.Wrath: return DreamTutorialChecks.FitsWrath(soul.Sin == SinType.Wrath, soul.Get(SinType.Wrath));
                case DreamRoad.SealKind.Order: return Trades.OfOrder(soul.Trade);
                case DreamRoad.SealKind.BrotherLeft:
                case DreamRoad.SealKind.BrotherRight: return soul.Kin.Length > 0;
                default: return false;
            }
        }

        /// <summary>Горит ли печать сейчас — для прогона.</summary>
        public bool SealLit(DreamRoad.SealKind kind) => _sealLit[(int)kind];

        private static bool EligibleSealWarrior(Warrior w)
            => w != null && !w.IsDead && w.gameObject.activeInHierarchy && w.Team == Team.Player;

        private bool AnyRaisedAlive()
        {
            if (EligibleSealWarrior(_zombie)) return true;
            foreach (var r in _raised) if (EligibleSealWarrior(r)) return true;
            return false;
        }

        private void Seals()
        {
            if (_sealsOpen) return;
            if (!_fallenSpawned) { _fallenSpawned = true; SpawnFallen(); }
            var me = _hero.transform.position;
            bool alone = !EligibleSealWarrior(_zombie);

            // От банок поднятый идёт за Греховодом; на площади — отпускаем: дальше его решение.
            if (!alone && !_escortedToSeals && !DreamRoad.OnSquare(me)) { _escortedToSeals = true; StartEscort(); }
            if (_escorting && DreamRoad.OnSquare(me, 2f)) { _escorting = false; if (!alone) Hold(_zombie, false); }
            if (!DreamRoad.OnSquare(me)) return;

            if (!_sealsAsked)
            {
                _sealsAsked = true;
                _sealsAskedAt = Time.unscaledTime;
                Mark("печати");
                _line = Loc.N("Ворота ждут пятерых: венец — ваша печать, четыре — для воинов. Подойдите к печати — узнаете, кого она ждёт. Павших стражей можно поднять: E у тела.");
                Log(_line);
            }

            // Поднять павшего можно в любой миг на площади: E у тела.
            var near = NearFallen(me);
            if (near != null && Pressed(KeyCode.E)) RaiseFallen(near);

            // Кто где стоит: поднятый у дороги или любой поднятый страж; один человек — одна печать.
            for (int i = 0; i < _sealOn.Length; i++) _sealOn[i] = null;
            void Place(Warrior w)
            {
                if (!EligibleSealWarrior(w)) return;
                for (int i = 1; i < DreamRoad.SealAt.Length; i++)
                    if (_sealOn[i] == null && OnSeal(w.transform.position, DreamRoad.SealAt[i])) { _sealOn[i] = w; return; }
            }
            Place(_zombie);
            foreach (var r in _raised) Place(r);

            var road = DreamRoad.Instance;
            _sealLit[0] = OnSeal(me, DreamRoad.LordSealAt);
            foreach (var kind in new[] { DreamRoad.SealKind.Wrath, DreamRoad.SealKind.Order })
                _sealLit[(int)kind] = Fits(kind, _sealOn[(int)kind]);
            var left = _sealOn[(int)DreamRoad.SealKind.BrotherLeft];
            var right = _sealOn[(int)DreamRoad.SealKind.BrotherRight];
            bool pair = left != null && right != null
                && DreamTutorialChecks.KinPair(left.Soul?.Kin, left.Soul?.Id, right.Soul?.Kin, right.Soul?.Id);
            int glowL = DreamTutorialChecks.KinSeal(left != null, Fits(DreamRoad.SealKind.BrotherLeft, left), pair);
            int glowR = DreamTutorialChecks.KinSeal(right != null, Fits(DreamRoad.SealKind.BrotherRight, right), pair);
            _sealLit[(int)DreamRoad.SealKind.BrotherLeft] = _sealLit[(int)DreamRoad.SealKind.BrotherRight] = pair;
            if (road != null)
            {
                for (int i = 0; i < _sealLit.Length; i++)
                {
                    int glow = i == (int)DreamRoad.SealKind.BrotherLeft ? glowL : i == (int)DreamRoad.SealKind.BrotherRight ? glowR : _sealLit[i] ? 2 : 0;
                    road.LightSeal((DreamRoad.SealKind)i, (DreamRoad.SealGlow)glow);
                }
                road.LightKin(pair);
            }

            // Все пять разом — и держатся миг (§5: «короткое одновременное удержание»).
            bool warriors = true;
            for (int i = 1; i < _sealLit.Length; i++) warriors &= _sealLit[i];
            if (DreamTutorialChecks.AdvanceSeals(ref _sealHeld, true, warriors, _sealLit[0], Time.deltaTime)) { OpenSeals(); return; }

            // Waiting is not evidence of refusal: the warrior may still be walking,
            // blocked, hesitating, or awaiting a decision. Offer a neutral alternative.
            Warrior ordered = null;
            if (OrderedToSeal(_zombie)) ordered = _zombie;
            foreach (var r in _raised)
                if (ordered == null && OrderedToSeal(r)) ordered = r;

            // Душа отказала именно этому приказу — тогда это и правда её воля, и строка автора верна.
            // До 25.11 строка звучала через 10 с без шага на печать — и про того, кто ещё шёл (бот прав).
            if (_sealOrderedWarrior != null && _sealRefusalSaid != _sealOrderRevision
                && RefusedSealOrder(_sealOrderedWarrior, _sealOrderRevision))
            {
                _sealRefusalSaid = _sealOrderRevision;
                OfferRaise(_sealOrderedWarrior);
                return;
            }

            if (ordered == null)
            {
                _sealOrderedWarrior = null;
                _sealOrderAt = -1f;
            }
            else if (_sealOrderedWarrior != ordered || _sealOrderRevision != ordered.CommandRevision)
            {
                _sealOrderedWarrior = ordered;
                _sealOrderRevision = ordered.CommandRevision;
                _sealOrderAt = Time.unscaledTime;
            }

            // Напоминание «воинов на печатях нет» — только пока и правда никто не стоит.
            bool anyStands = false;
            for (int i = 1; i < _sealOn.Length; i++) anyStands |= _sealOn[i] != null;
            if (!anyStands && !_raiseOffered && ((_sealOrderAt >= 0f && Time.unscaledTime - _sealOrderAt > SealWait)
                || Time.unscaledTime - _sealsAskedAt > SealPatience)) OfferRaise();
        }

        /// <summary>Приказ «на воинскую печать», которую он ещё не занял. Дошёл — ждать больше нечего.</summary>
        private static bool OrderedToSeal(Warrior w)
        {
            if (!(EligibleSealWarrior(w) && w.HasCommand && w.Command.Kind == CommandKind.Move)) return false;
            for (int i = 1; i < DreamRoad.SealAt.Length; i++)
                if (GroundDistance(w.Command.Point, DreamRoad.SealAt[i]) < 1.5f)
                    return !OnSeal(w.transform.position, DreamRoad.SealAt[i]);
            return false;
        }

        /// <summary>Решение души об этом самом приказе — отказ (шёпот не уговорил).</summary>
        private static bool RefusedSealOrder(Warrior w, uint revision)
        {
            var mind = w != null ? w.GetComponent<AOS.AOSWarriorWrapper>() : null;
            return mind != null && mind.LastContext != null && mind.LastContext.HasCommand
                && mind.LastDecisionCommandRevision == revision
                && mind.LastDecisionDetail.RefusedCommand && !Whisper.Persuaded(w);
        }

        /// <summary>Подсказка поднять другого. Причину называет, только если душа правда отказала.</summary>
        private void OfferRaise(Warrior refused = null)
        {
            _raiseOffered = true;
            if (_fallen.Count == 0) return;
            bool dead = !AnyRaisedAlive();
            if (refused != null)
                _line = Grammar.Pick(refused.Gender,
                    Loc.N("Он не встаёт на печать — это его воля, уговаривать не нужно. У печатей лежат павшие стражи: подойдите к одному и нажмите E — поднимите другого."),
                    Loc.N("Она не встаёт на печать — это её воля, уговаривать не нужно. У печатей лежат павшие стражи: подойдите к одному и нажмите E — поднимите другого."));
            else if (dead)
                _line = Loc.N("Четыре печати ждут воинов, а ваших нет. У печатей лежат павшие стражи: подойдите к одному и нажмите E — поднимите его.");
            else
                _line = Loc.N("Воинов на печатях всё нет. Их можно поставить приказом — или поднять павших стражей: подойдите и нажмите E.");
            Log(_line);
            Mark(refused != null ? "печати: " + refused.DisplayName + " отказал(а) — поднять другого" : "печати: подсказка поднять стража");
        }

        /// <summary>
        /// Павшие стражи склепа — лежат на площади у печатей. Живые тела с душой внутри, как
        /// раненый у дороги до смерти: ум спит, поза падения. E у тела — душа встаёт в нём.
        /// </summary>
        private void SpawnFallen()
        {
            var names = new List<string>();
            foreach (var k in _killerSketches) names.Add(k.Name);
            if (_zombie != null) names.Add(_zombie.DisplayName);
            foreach (var soul in DreamGuards.Make(_tutorialSeed, names))
            {
                int i = _fallen.Count;
                var body = CreateLiving(soul, FallenAt[i], Team.Neutral);
                if (body == null) continue;
                foreach (var talk in body.GetComponents<Dialogue.DialogueTrigger>()) talk.enabled = false;
                _fallen.Add(body);
                StartCoroutine(LieDown(body));
            }
            var log = new System.Text.StringBuilder("[СОН] Стражи у печатей:");
            foreach (var f in _fallen)
                log.Append($" {f.DisplayName} ({f.Soul.Sin} {f.Soul.Get(f.Soul.Sin):0}, {f.Soul.Trade}{(f.Soul.Kin.Length > 0 ? ", родня " + f.Soul.Kin : "")});");
            Debug.Log(log.ToString());
        }

        private static IEnumerator LieDown(Warrior body)
        {
            yield return null;
            if (body == null) yield break;
            Hold(body, true);
            var pose = body.GetComponent<WarriorAnimation>();
            if (pose != null) pose.Pose(BodyMotion.Die);
        }

        private Warrior NearFallen(Vector3 at)
        {
            Warrior best = null;
            float bestD = 2.4f;
            foreach (var f in _fallen)
            {
                if (f == null || !f.gameObject.activeSelf) continue;
                float d = GroundDistance(at, f.transform.position);
                if (d < bestD) { bestD = d; best = f; }
            }
            return best;
        }

        private void RaiseFallen(Warrior body)
        {
            var at = body.transform.position;
            var face = Flat(_hero.transform.position - at);
            var relations = new RelationshipSystem(AOS.MemoryProcessor.Instance);
            // Первые слова — о том, что спрашивают печати (автор, 25.11): брат — о брате, Орден — руки, грех — грех.
            string kinName = null;
            foreach (var f in _fallen) if (f != body && SoulData.Kindred(body.Soul, f.Soul)) kinName = Loc.Name(f.DisplayName);
            foreach (var r in _raised) if (SoulData.Kindred(body.Soul, r.Soul)) kinName = Loc.Name(r.DisplayName);
            var risen = Raising.Rise(body.Soul, ShellType.Zombie, at + face * 1.1f, Quaternion.LookRotation(face), relations, corpse: body,
                words: RisingWords.SealWords(body.Soul, ShellType.Zombie, kinName));
            if (risen == null) { Log(Loc.N("Не удалось поднять тело.")); return; }
            body.gameObject.SetActive(false);
            _fallen.Remove(body);
            _raised.Add(risen);
            Mark("печати: поднят страж " + risen.DisplayName);
            Debug.Log($"[СОН] Поднят страж у печатей: {risen.DisplayName} ({risen.Soul?.Sin} {risen.Soul?.Get(risen.Soul.Sin):0}, {risen.Soul?.Trade})");
            // Брат — по душе, не по имени (§17.1): кто ему родня, говорит знак родни. Слова — черновик.
            Warrior kin = null;
            foreach (var f in _fallen) if (SoulData.Kindred(risen.Soul, f.Soul)) kin = f;
            foreach (var r in _raised) if (r != risen && SoulData.Kindred(risen.Soul, r.Soul)) kin = r;
            if (kin != null && _fallen.Contains(kin))
                _line = Loc.F("Страж поднят. Рядом лежит его брат — {0}. Поднимите и его.", Loc.Name(kin.DisplayName));
            else if (kin != null)
                _line = Loc.F("Страж поднят — брат {0}. Братьям — сцепленные печати.", Loc.Name(kin.DisplayName));
            else
                _line = Grammar.Pick(risen.Gender,
                    Loc.N("Страж поднят. Выделите его и ПКМ по печати."),
                    Loc.N("Стража поднята. Выделите её и ПКМ по печати."));
            Log(_line);
        }

        private void OpenSeals()
        {
            _sealsOpen = true;
            string Who(DreamRoad.SealKind k)
            {
                var w = _sealOn[(int)k];
                return w == null ? "—" : (w == _zombie ? "поднятый у дороги " : "") + w.DisplayName;
            }
            SealsResult = $"меч — {Who(DreamRoad.SealKind.Wrath)}; братья — {Who(DreamRoad.SealKind.BrotherLeft)} и {Who(DreamRoad.SealKind.BrotherRight)}; крест — {Who(DreamRoad.SealKind.Order)}";
            Mark("печати открыты: " + SealsResult);
            Debug.Log("[СОН] Печати: " + SealsResult);
            _line = Loc.N("Горят все пять печатей. F — открыть ворота.");
            Log(_line);
            var road = DreamRoad.Instance;
            if (road != null)
            {
                for (int i = 0; i < _sealLit.Length; i++) road.LightSeal((DreamRoad.SealKind)i, DreamRoad.SealGlow.Lit);
                road.LightKin(true);
            }
        }
    }
}
