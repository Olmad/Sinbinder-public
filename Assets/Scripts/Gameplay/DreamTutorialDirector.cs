// Sinbinder: optional, isolated dream tutorial before Prologue_Camp.
// Does not alter the campaign until a level designer explicitly opts in.
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

        private enum Stage { Approach, Dying, Harvest, Rebind, Command, Standoff, Deciding, Battle, Retest, FirstPerson, WarriorMode, DrawWeapon, Practice, Jars, Return, Finished }

        /// <summary>Шаг сна — для прогона (DreamWalkthrough) и отчёта.</summary>
        public static string StageName => Instance != null ? Instance._stage.ToString() : "";

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

        /// <summary>Клавиша, нажатая прогоном (без людей Input молчит). Живёт один кадр.</summary>
        private KeyCode _testKey;
        private int _testKeyFrame = -1;
        public void TestPress(KeyCode key) { _testKey = key; _testKeyFrame = Time.frameCount; }
        private bool Pressed(KeyCode key)
            => Input.GetKeyDown(key) || (_testKey == key && Time.frameCount - _testKeyFrame <= 1);

        /// <summary>Прогон: управление (Tab, V, J) проверено в лагере — сразу к воротам.</summary>
        public void TestSkipControls()
        {
            if (_stage >= Stage.FirstPerson && _stage < Stage.Return)
                Set(Stage.Return, "Вернись к воротам и нажми F, чтобы проснуться.");
        }

        private const string DreamsKey = "Sinbinder.DreamTutorial.Count";

        private Stage _stage;
        private SinbinderPlayer _hero;
        private Warrior _wounded, _zombie;
        private readonly List<Warrior> _killers = new List<Warrior>();
        private readonly List<Sketch> _killerSketches = new List<Sketch>();
        private bool _handBusy, _battleStarted;
        private int _backAttempts;
        private int _swingsBefore, _blockFramesBefore;
        private bool _squadOrdered;
        private int _selectedJarBefore;
        private float _commandSince, _orderedAt = -1f;
        private float _standoffSince, _decideSince, _retestSince;
        // Суд срабатывал «раз из десяти» (автор, 06.11): цепочка до него рвалась —
        // душа угасала несобранной, игрок не отдавал приказ или сам рубил убийц,
        // решение не приходило за 6 с. Каждое звено теперь доходит само.
        private float _standoffReal, _harvestSince;
        private bool _autoOrdered;
        private SoulData _fallbackSoul;

        // Круг обучения на каждом шаге к суду (автор, 06.11: «у нас есть круг
        // обучения. Возможно, он может решить почти все проблемы»).
        private Stage _guidedFor = Stage.Finished;
        private float _nextGuide;

        // Приказ замечаем по самому приказу, а не по игровому времени: под кругом
        // мир стоит, и время приказа равно времени начала шага.
        private PendingCommand _cmdMark;
        private CommandKind? _courtOrder;

        private void MarkCommand()
        {
            if (_zombie != null) _cmdMark = _zombie.Command;
        }

        private bool OrderedSinceMark()
        {
            if (_zombie == null || _zombie.IsDead || !_zombie.HasCommand) return false;
            var c = _zombie.Command;
            return c.Kind != _cmdMark.Kind || c.Point != _cmdMark.Point || c.Target != _cmdMark.Target
                || !Mathf.Approximately(c.IssuedAt, _cmdMark.IssuedAt);
        }

        private bool Picked()
        {
            var pick = _zombie != null ? _zombie.GetComponent<SelectionComponent>() : null;
            return pick != null && pick.IsSelected;
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
                    if (_zombie == null) return;
                    shown = Lesson.Guide(_zombie.transform.position,
                        () => !Picked()
                            ? (Loc.T("ЛКМ"), Loc.F("Щёлкните по {0} — выделить.", Whom())
                                             + "\n" + Loc.T("Это ваш первый воин."))
                            : (Loc.T("ПКМ"), Grammar.Pick(g, Loc.T("Правой кнопкой — по земле: он пойдёт туда."),
                                                             Loc.T("Правой кнопкой — по земле: она пойдёт туда."))
                                             + "\n" + Loc.T("Приказ отдаёте вы. Исполнит ли — решает душа.")),
                        () => _stage != Stage.Command || _squadOrdered || OrderedSinceMark());
                    break;
                case Stage.Standoff:
                    if (_zombie == null || _killers.Count == 0) return;   // убийцы ещё не встали
                    shown = Lesson.Guide(_zombie.transform.position,
                        () => !Picked()
                            ? (Loc.T("ЛКМ"), Grammar.Pick(g, Loc.T("Его убийцы вернулись."), Loc.T("Её убийцы вернулись."))
                                             + "\n" + Loc.F("Щёлкните по {0} — выделить.", Whom()))
                            : ("X", Grammar.Pick(g, Loc.T("X, затем щелчок по земле позади него — приказ отступить."),
                                                    Loc.T("X, затем щелчок по земле позади неё — приказ отступить."))
                                  + "\n" + Loc.T("Послушается ли — решит душа. Смотрите.")),
                        () => _stage != Stage.Standoff || OrderedSinceMark());
                    break;
                case Stage.Retest:
                    if (_zombie == null) return;
                    shown = Lesson.Guide(_zombie.transform.position,
                        () => !Picked()
                            ? (Loc.T("ЛКМ"), Loc.T("Убийц больше нет.") + "\n" + Loc.F("Щёлкните по {0} — выделить.", Whom()))
                            : ("X", Loc.T("Тот же приказ: X и щелчок позади.")
                                  + "\n" + Loc.T("Причины больше нет — что решит душа теперь?")),
                        () => _stage != Stage.Retest || OrderedSinceMark());
                    break;
                default:
                    _guidedFor = _stage;   // этому шагу кольцо не нужно
                    return;
            }
            if (shown) _guidedFor = _stage;
        }
        private bool _courtRunning, _retestOrdered, _contrastRunning;
        private AOS.DecisionContext _mindBefore;
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
            if (worldPoint.z >= dream._backBoundary &&
                (dream._backAttempts < 3 || Mathf.Abs(worldPoint.x) <= dream._allowedCorridorHalfWidth)) return true;
            dream.BackAttempt();
            return false;
        }

        // Used by PlayerWalk when in first-person; never moves the OS cursor.
        public static bool PermitWalking(Vector3 futurePoint)
        {
            var dream = Instance;
            if (dream == null || dream._stage == Stage.Finished) return true;
            if (dream._handBusy) return false;
            if (futurePoint.z >= dream._backBoundary &&
                (dream._backAttempts < 3 || Mathf.Abs(futurePoint.x) <= dream._allowedCorridorHalfWidth)) return true;
            dream.BackAttempt();
            return false;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
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

            // Причина отказа «от противного» — на весь сон: суд называет то,
            // без чего воин поступил бы иначе, и это должно быть посчитано.
            _counterfactualWas = AOS.Counterfactual.Enabled;
            AOS.Counterfactual.Enabled = true;
        }

        private void OnEnable() { SelectionManager.OnPlayerOrder += TrackOrder; }
        private void OnDisable() { SelectionManager.OnPlayerOrder -= TrackOrder; }
        private void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            AOS.Counterfactual.Enabled = _counterfactualWas;
        }

        private void TrackOrder(CommandKind kind, int count)
        {
            if (count <= 0 || SelectionManager.Instance == null) return;
            if (_stage == Stage.Command && SelectionManager.Instance.Selection == SelectionManager.Picked.Squad)
                _squadOrdered = true;
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
            Set(Stage.Approach, "Укажи Греховоду путь правой кнопкой мыши.");
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

        private void Set(Stage stage, string line)
        {
            _stage = stage;
            _line = line;
            _nextPrompt = Time.unscaledTime + 15f;
            Log(line);
        }

        private void Log(string message)
        {
            FindFirstObjectByType<UI.BattleLogUI>()?.Write(message);
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
                yield return _hand.DragTo(_allowed, 1.05f);
                _hand.enabled = false;
            }
            if (_hero != null)
            {
                var mover = _hero.GetComponent<UnitMover>();
                if (mover != null) mover.CommandMove(_allowed);
            }
            _handBusy = false;
        }

        private void Update()
        {
            if (_hero == null || _stage == Stage.Finished) return;
            if (Time.unscaledTime >= _nextPrompt && !SoulCourt.Sitting && !Lesson.Holding)
            {
                Log(_line);
                _nextPrompt = Time.unscaledTime + 25f;
            }
            Guide();
            switch (_stage)
            {
                case Stage.Approach:
                    if (_wounded != null && GroundDistance(_hero.transform.position, _woundedAt) <= 4.8f)
                    {
                        Set(Stage.Dying, "Незнакомец пытается что-то сказать.");
                        StartCoroutine(DyingEncounter());
                    }
                    break;
                case Stage.Harvest:
                    // The actual SoulHarvester puts the soul in a jar after E.
                    if (FindJar() >= 0)
                    {
                        Set(Stage.Rebind, "Подойди к телу. Нажми R, чтобы вернуть душу в оболочку.");
                    }
                    else if (Time.unscaledTime - _harvestSince > 50f && _wounded != null && _wounded.Soul != null)
                    {
                        // Душа угасает за минуту; несобранная — сон не должен на этом встать.
                        _fallbackSoul = _wounded.Soul;
                        Set(Stage.Rebind, "Душа ещё держится за тело. Подойди к телу и нажми R, чтобы вернуть её.");
                    }
                    break;
                case Stage.Rebind:
                    if (Pressed(KeyCode.R) && GroundDistance(_hero.transform.position, _woundedAt) < 4f)
                        TryRebind();
                    break;
                case Stage.Command:
                    // Первый приказ — любой. Он его исполнит: послушание — фон,
                    // на котором отказ потом читается характером, а не поломкой.
                    if (_orderedAt < 0f && (_squadOrdered || Ordered(_commandSinceGame) || OrderedSinceMark()))
                        _orderedAt = Time.unscaledTime;
                    if ((_orderedAt >= 0f && Time.unscaledTime - _orderedAt > 3.5f)
                        || Time.unscaledTime - _commandSince > (Lesson.Holding ? 60f : 20f))
                        BeginStandoff();
                    break;
                case Stage.Standoff:
                    if (_zombie == null || _zombie.IsDead) { StartBattle("Первый воин пал."); break; }
                    if (_killers.Count > 0 && _killers.TrueForAll(k => k == null || k.IsDead))
                    {
                        // Игрок сразил их сам раньше приказа — тоже выбор; миг отказа ушёл.
                        Hold(_zombie, false);
                        Set(Stage.FirstPerson, "Убийцы пали. Tab: перейди от тактического вида к первому лицу.");
                        break;
                    }
                    // Игрок медлит — приказ отдаёт сам Греховод: суд важнее урока клавиш.
                    if (!_autoOrdered && _killers.Count > 0 && !OrderedSinceMark()
                        && Time.unscaledTime - _standoffReal > (Lesson.Holding ? 45f : 14f) && !SoulCourt.Sitting)
                    {
                        _autoOrdered = true;
                        Vector3 away = _zombie.transform.position - _killers[0].transform.position;
                        away.y = 0f;
                        away = away.sqrMagnitude < 0.01f ? Vector3.back : away.normalized;
                        Log("Греховод сам велит ему отступить.");
                        _zombie.IssueCommand(CommandKind.FallBack, _zombie.transform.position + away * 6f);
                    }
                    if (_killers.Count > 0 && OrderedSinceMark())
                    {
                        _courtOrder = _zombie.Command.Kind;
                        var mind = _zombie.GetComponent<AOS.AOSWarriorWrapper>();
                        _mindBefore = mind != null ? mind.LastContext : null;
                        _decideSince = Time.unscaledTime;
                        Hold(_zombie, false);   // теперь решает он
                        _stage = Stage.Deciding;
                    }
                    break;
                case Stage.Deciding:
                {
                    var mind = _zombie != null ? _zombie.GetComponent<AOS.AOSWarriorWrapper>() : null;
                    if (mind == null || _zombie.IsDead) { StartBattle("Решение не дошло до суда."); break; }
                    if (_courtRunning) break;
                    // Голоса не заговорили сами за секунду с лишним — спросить душу напрямую:
                    // тот же Decide, что зовёт её мозг, просто сейчас.
                    if ((mind.LastContext == null || mind.LastContext == _mindBefore)
                        && Time.unscaledTime - _decideSince > 1.2f)
                        mind.Decide();
                    if (mind.LastContext != null && mind.LastContext != _mindBefore)
                    {
                        _courtRunning = true;
                        StartCoroutine(Judge(mind.LastDecisionDetail, mind.LastContext));
                    }
                    else if (Time.unscaledTime - _decideSince > 6f)
                    {
                        Debug.LogWarning("[СОН QA] Воин не принял решения за 6 с после приказа — суда не будет.");
                        StartBattle("Убийцы идут.");
                    }
                    break;
                }
                case Stage.Battle:
                    KeepHeroAlive();
                    if (_battleStarted && _killers.Count > 0 && _killers.TrueForAll(k => k == null || k.IsDead))
                    {
                        if (_zombie != null && !_zombie.IsDead && RefusalResult != "послушался")
                        {
                            _retestSince = Time.time;
                            _retestPrompted = Time.unscaledTime;
                            MarkCommand();
                            _retestOrdered = false;
                            Set(Stage.Retest, "Убийц больше нет. Отдай ему тот же приказ — отступить (X).");
                        }
                        else
                        {
                            // Пал в бою — сказать, а не молча перейти к управлению: проверка тем же
                            // приказом пропала не сама, её съело его решение (прогон 06.11: кинулся мстить).
                            if (_zombie != null && _zombie.IsDead && RefusalResult != "послушался")
                                Log(Grammar.Pick(_zombie.Gender, Loc.T("Он пал. Своё решение стоило ему второй жизни."),
                                                                 Loc.T("Она пала. Своё решение стоило ей второй жизни.")));
                            Set(Stage.FirstPerson, "Tab: перейди от тактического вида к первому лицу.");
                        }
                    }
                    break;
                case Stage.Retest:
                {
                    if (_zombie == null || _zombie.IsDead) { Set(Stage.FirstPerson, "Tab: перейди от тактического вида к первому лицу."); break; }
                    var mind = _zombie.GetComponent<AOS.AOSWarriorWrapper>();
                    if (!_retestOrdered && (Ordered(_retestSince) || OrderedSinceMark()))
                    {
                        _retestOrdered = true;
                        _mindBefore = mind != null ? mind.LastContext : null;
                        _decideSince = Time.unscaledTime;
                    }
                    if (_retestOrdered && !_contrastRunning && mind != null
                        && mind.LastContext != null && mind.LastContext != _mindBefore)
                    {
                        _contrastRunning = true;
                        StartCoroutine(Contrast(mind.LastDecisionDetail));
                    }
                    else if (!_retestOrdered && !Lesson.Holding && Time.unscaledTime - _retestPrompted > 40f)
                        Set(Stage.FirstPerson, "Tab: перейди от тактического вида к первому лицу.");
                    break;
                }
                case Stage.FirstPerson:
                {
                    var cam = Camera.main != null ? Camera.main.GetComponent<RTS_Camera>() : null;
                    if (cam != null && cam.FirstPersonNow)
                        Set(Stage.WarriorMode, "V: переключись в режим Воина. ЛКМ удар, ПКМ блок.");
                    break;
                }
                case Stage.WarriorMode:
                {
                    var cam = Camera.main != null ? Camera.main.GetComponent<RTS_Camera>() : null;
                    if (cam == null || !cam.WarriorModeNow) break;
                    // Оружия со стойки во сне нет — Греховод бьёт посохом, и урока B не нужно.
                    if (_hero.GetComponentInChildren<Armament>(true) != null)
                        Set(Stage.DrawWeapon, "B — достань оружие. Только обнажённым оружием можно атаковать.");
                    else
                        BeginPractice("Посох всегда в руке. Ударь ЛКМ и держи блок ПКМ.");
                    break;
                }
                case Stage.DrawWeapon:
                {
                    var cam = Camera.main != null ? Camera.main.GetComponent<RTS_Camera>() : null;
                    if (cam == null || !cam.WarriorModeNow) break;
                    var arms = _hero.GetComponentInChildren<Armament>(true);
                    if (arms != null && !arms.Drawn) break;
                    BeginPractice("Ударь ЛКМ и держи блок ПКМ. Урок примет только действия боевой системы.");
                    break;
                }
                case Stage.Practice:
                {
                    var cam = Camera.main != null ? Camera.main.GetComponent<RTS_Camera>() : null;
                    if (cam == null || !cam.WarriorModeNow) break;
                    var combat = _hero.GetComponent<FirstPersonCombat>();
                    if (combat != null && combat.AcceptedSwings > _swingsBefore
                        && combat.AcceptedBlockFrames > _blockFramesBefore)
                    {
                        _selectedJarBefore = Satchel.Selected;
                        Set(Stage.Jars, "J: выбери другую банку в суме. Банка остаётся после поднятия.");
                    }
                    break;
                }
                case Stage.Jars:
                    // Confirm that the real SatchelHands changed the selected jar, not merely a J key press.
                    if (Satchel.Jars < 2 || Satchel.Selected != _selectedJarBefore)
                        Set(Stage.Return, "Вернись к воротам и нажми F, чтобы проснуться.");
                    break;
                case Stage.Return:
                    if (GroundDistance(_hero.transform.position, _allowed) < 5f && Pressed(KeyCode.F))
                        Finish();
                    break;
            }
        }

        /// <summary>Отдан ли поднятому приказ после этого мига (игровое время).</summary>
        private bool Ordered(float since)
            => _zombie != null && !_zombie.IsDead && _zombie.HasCommand && _zombie.Command.IssuedAt > since;

        private float _commandSinceGame;

        private void BeginPractice(string line)
        {
            var combat = _hero.GetComponent<FirstPersonCombat>();
            if (combat == null)
            {
                Debug.LogError("[СОН QA] У Греховода нет FirstPersonCombat (SinbinderPlayer.Spawn).");
                Set(Stage.Return, "Личный бой не подключён. Вернись к воротам и нажми F.");
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
            Log(_wounded.DisplayName + ": «" + lines[_tutorialSeed % lines.Length] + "»");
            yield return new WaitForSeconds(1.8f);
            if (_wounded == null || _wounded.IsDead) yield break;
            var hp = _wounded.GetComponent<Damageable>();
            if (hp == null) { Log("Нет Damageable у раненого."); yield break; }
            hp.TakeDamage(hp.HP + hp.MaxHP + 1f, null);
            _harvestSince = Time.unscaledTime;
            Set(Stage.Harvest, "Подойди к душе и нажми E. Свежая душа сохраняет память.");
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
            if (soul == null) { Log("Сначала забери душу в банку."); return; }
            // A tutorial reanimation is a local, explicit shortcut to Raising.Rise,
            // not a second copy of the binding/shell maths.
            var relations = new RelationshipSystem(AOS.MemoryProcessor.Instance);
            var risen = Raising.Rise(soul, ShellType.Zombie,
                _woundedAt + new Vector3(2f, 0f, 0f), Quaternion.identity, relations);
            if (risen == null) { Log("Не удалось поднять тело. Душа осталась в банке."); return; }
            if (jar >= 0) Satchel.TakeSoul(jar); // Only remove after successful creation.
            _fallbackSoul = null;
            if (_wounded != null) _wounded.gameObject.SetActive(false);
            _zombie = risen;
            // This is actual AOS memory keyed to the actors, not Attack(killer).
            if (AOS.MemoryProcessor.Instance != null)
                foreach (var killer in _killerSketches)
                    AOS.MemoryProcessor.Instance.CreateMemory(_zombie, "MurderedMe", killer.Name,
                        AOS.EmotionType.Anger, 0.95f);
            _commandSince = Time.unscaledTime;
            _commandSinceGame = Time.time;
            MarkCommand();
            Set(Stage.Command, "Выдели его и отдай приказ ПКМ по земле — он пойдёт.");
        }

        /// <summary>
        /// Убийцы возвращаются и встают на дороге. И они, и он замирают:
        /// миг ждёт приказа игрока. Решать воин начнёт, когда приказ прозвучит.
        /// </summary>
        private void BeginStandoff()
        {
            _standoffSince = Time.time;
            _standoffReal = Time.unscaledTime;
            _autoOrdered = false;
            MarkCommand();
            Hold(_zombie, true);
            Set(Stage.Standoff, "Его убийцы вернулись. Прикажи ему отступить: выдели его, нажми X и щёлкни по земле позади (или Shift + ПКМ).");
            StartCoroutine(ReleaseKillers());
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
                // Перед ним, в десяти-одиннадцати шагах: видно, но не вплотную.
                var at = new Vector3(Mathf.Clamp(near.x, -4f, 4f) - 1.3f + i * 2.6f, 0f,
                                     Mathf.Min(near.z + 10.5f, 12f));
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
            if (_killers.Count == 0)
            {
                Debug.LogError("[СОН QA] No killers spawned. Check dependencies and Unity Console.");
                Hold(_zombie, false);
                Set(Stage.Return, "Убийцы не появились. Воспоминание прервано. Вернись к воротам и нажми F.");
            }
        }

        /// <summary>
        /// Суд над решением — каким бы оно ни было. Отказ, колебание, послушание —
        /// суд показывает, чьи голоса это решили.
        /// </summary>
        private IEnumerator Judge(AOS.Decision d, AOS.DecisionContext c)
        {
            RefusalResult = d.RefusedCommand ? "отказал" : d.Hesitated ? "колебался" : "послушался";
            int murders = 0;
            if (c != null && c.RecentMemories != null)
                foreach (var m in c.RecentMemories) if (m != null && m.EventType == "MurderedMe") murders++;
            Debug.Log($"[СОН] Решение при убийцах: {RefusalResult} ({d.Action}, причина {d.Decisive}, голос {d.DecisiveVoice}); "
                + $"убийцы на глазах {(c != null && c.KnownMurdererInSight ? "да" : "нет")}, памяти об убийстве {murders}");

            // Миг на его слово и малые весы над головой — потом суд, который их объяснит.
            // В лагере игрок узнает эти весы над отказавшим (63-TEACHING, п. 3).
            // Во сне — при любом решении: суд разбирает любое (в лагере — только отказ).
            if (_zombie != null) UI.ScalesOverhead.Show(_zombie, d, c, force: true);
            yield return new WaitForSecondsRealtime(1.8f);
            if (_zombie != null && !_zombie.IsDead)
                // Суд во сне — всегда полный: это урок (суды по делу потом идут коротко).
                yield return SoulCourt.Convene(_zombie, d, c, _courtOrder, brief: false);
            _courtRunning = false;

            var g = _zombie != null ? _zombie.Gender : Gender.Male;
            if (d.RefusedCommand || d.Hesitated)
                StartBattle(Grammar.Pick(g,
                    "Он решил сам. Убери причину — срази убийц. Или искуси его: подойди и нажми Z.",
                    "Она решила сама. Убери причину — срази убийц. Или искуси её: подойди и нажми Z."));
            else
                StartBattle(Grammar.Pick(g, "Он уходит, а убийцы идут. Встреть их.", "Она уходит, а убийцы идут. Встреть их."));
        }

        private void StartBattle(string line)
        {
            foreach (var k in _killers)
            {
                Hold(k, false);
                var hp = k != null && !k.IsDead ? k.GetComponent<Damageable>() : null;
                if (hp != null) hp.Initialize(9f, 0f);   // теперь — обычные противники
            }
            if (_zombie != null) Hold(_zombie, false);
            _battleStarted = true;
            Set(Stage.Battle, line);
        }

        /// <summary>
        /// Тот же приказ без убийц на глазах. Послушался — значит, отказ держался
        /// на причине, а не на сцене: это и есть довод, что сценария нет.
        /// </summary>
        private IEnumerator Contrast(AOS.Decision d)
        {
            bool obeyed = !d.RefusedCommand && !d.Hesitated;
            RetestResult = obeyed ? "послушался" : "снова нет";
            Debug.Log($"[СОН] Тот же приказ без убийц: {RetestResult} ({d.Action})");

            var g = _zombie != null ? _zombie.Gender : Gender.Male;
            string line = obeyed
                ? Grammar.Pick(g, Loc.T("Тот же приказ — и он идёт. Причины больше нет: убийцы мертвы."),
                                  Loc.T("Тот же приказ — и она идёт. Причины больше нет: убийцы мертвы."))
                : Grammar.Pick(g, Loc.T("И снова нет. Суд судил тот миг, а теперь мир другой — и у его души нашлась другая причина. Наведи на него мышь — узнаешь какая."),
                                  Loc.T("И снова нет. Суд судил тот миг, а теперь мир другой — и у её души нашлась другая причина. Наведи на неё мышь — узнаешь какая."));

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
            Set(Stage.FirstPerson, "Tab: перейди от тактического вида к первому лицу.");
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

        private void Finish()
        {
            if (!Application.CanStreamedLevelBeLoaded(_campScene))
            {
                Log("Сцена лагеря отсутствует в Build Settings: " + _campScene);
                return;
            }
            _stage = Stage.Finished;
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

        private void OnGUI()
        {
            if (_stage == Stage.Finished || _handBusy || SoulCourt.Sitting || Lesson.Holding || UI.TalkView.Shown) return;
            // Под полосами — кадр, а не урок: табличка спорила бы с репликой.
            if (UI.Letterbox.Instance != null && UI.Letterbox.Instance.Shown) return;
            var style = GUI.skin.box;
            GUI.Box(new Rect(16, Screen.height - 106, Mathf.Min(Screen.width - 32, 720), 80),
                "Воспоминание\n" + _line, style);
        }
    }
}
