// Перевод: текст через Loc
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

using Sinbinder.Core;
namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Что ведёт пролог из доли в долю.
    ///
    /// Четыре собранные сцены были четырьмя тестами: каждая запускалась
    /// отдельно, ничто никуда не вело, и сценарий 00-GDD.md §8
    /// («пробуждение → совет → разгром → побег → склеп → возвращение»)
    /// существовал только на бумаге. Демо — это последовательность,
    /// а не набор комнат.
    ///
    /// Условие ухода зависит от сцены:
    /// — лагерь уходит дальше, когда игрок выбрал командира (доля 3);
    /// — боевые сцены — когда на поле не осталось врагов.
    ///
    /// Состав отряда снимает <see cref="PrologueCampSpawner"/> при выгрузке
    /// сцены, поэтому в следующую долю приходят ровно выжившие.
    /// </summary>
    public class PrologueDirector : MonoBehaviour
    {
        [Tooltip("Куда идти дальше. Пусто — это последняя доля демо.")]
        [SerializeField] private string _nextScene = "";

        [Tooltip("Ждать ли конца боя. Снять для лагеря: врагов там нет, "
               + "и уходить надо после военного совета.")]
        [SerializeField] private bool _waitForBattle = true;

        [Tooltip("Сколько подержать игрока после конца боя, прежде чем "
               + "уводить. Строке журнала надо успеть прочитаться.")]
        [SerializeField] private float _delaySeconds = 4f;

        [Tooltip("Ждать ли, пока игрок уведёт отряд за край карты. Доля 5: "
               + "побег — не катсцена, а отбор. Условие ставит EscapeZone, "
               + "директор только уходит по её слову.")]
        [SerializeField] private bool _waitForEscape;

        [Tooltip("Начало пролога: забыть прошлый отряд. Ставить только "
               + "на первой доле — остальные обязаны получить выживших.")]
        [SerializeField] private bool _startsPrologue;

        [Tooltip("Последняя доля: через сколько секунд показать эпилог. "
               + "Ноль — доля кончается не по времени. Нужен склепу: боя "
               + "там больше нет (сцены 6 и 7 вырезаны), а конец доли "
               + "прежде вёл именно конец боя.")]
        [SerializeField] private float _endsAfterSeconds;

        [Tooltip("Последняя доля: насколько близко к алтарю подойти, чтобы "
               + "отряд вошёл следом. Мерится по земле, в метрах.")]
        [SerializeField] private float _altarReach = 3.5f;

        [Tooltip("Последняя доля: как часто напоминать о шаге, которого склеп "
               + "ждёт от игрока: поднять воина, дойти до алтаря.")]
        [SerializeField] private float _altarNudge = 60f;

        [Tooltip("Что сказать, входя в последнюю долю. Пусто — молча.")]
        [TextArea(1, 3)]
        [SerializeField] private string _arrivalLine = "";

        [Tooltip("Сколько лагерь ждёт чужого слова после назначения старшего. "
               + "Сцену 3 — тревогу шара — ведёт CrystalBall, и уводит лагерь "
               + "тоже он. Этот срок нужен на случай, когда вести некому: "
               + "шара в сцене нет. Тогда директор уходит сам и говорит "
               + "об этом в консоль — пропавшая сцена должна быть слышна.")]
        [SerializeField] private float _campGrace = 30f;

        private bool _battleJoined;
        private bool _leaving;
        private float _sinceCommander;

        /// <summary>
        /// Куда войти в последней доле: алтарь зала. Нет алтаря — зал целиком;
        /// нет и зала — доля идёт по времени, как до 24 сентября, и говорит
        /// об этом в консоль.
        /// </summary>
        private Transform _altar;
        private bool _entered;
        private float _sinceNudge;

        /// <summary>
        /// Состав отряда статичен и переживает не только смену сцены,
        /// но и повторный запуск демо из редактора. Без этой уборки
        /// второй прогон начинался бы с уже выбранным командиром:
        /// совет доли 3 не собрался бы, а он — главная панель демо.
        ///
        /// Awake, а не Start: спавнер читает состав в своём Start,
        /// и забывать надо до того, как он посмотрит.
        /// </summary>
        void Awake()
        {
            if (!_startsPrologue) return;

            // Пришли загрузкой, а не новой игрой — отряд не забывать.
            // Без этой строки загрузка записи в лагерь молча выбрасывала
            // бы её: состав уже восстановлен, а здесь его стирали, и
            // спавнер собирал канонический отряд поверх (SaveSystem.ReturnTo).
            if (Core.SaveSystem.Arriving) return;

            SquadRoster.Clear();

            // Отъезд камеры один на весь пролог, а не на сцену: его
            // счётчик переживает смену сцен и потому обязан забываться
            // здесь же, где забывается отряд. Иначе второй прогон демо
            // из редактора прошёл бы вообще без единственной постановки.
            CameraPullback.Forget();
            TrophyChest.Forget();
            UI.MovementHintUI.Forget();
            UI.HarvestHintUI.Forget();
            UI.CommandHintUI.Forget();
            Lesson.Forget();

            // Сума своя, не чужая: статика переживает смену сцен,
            // и новая игра начиналась бы с добром прошлой.
            Core.Satchel.Forget();

            // Установка отряда — тоже статика, живущая между сценами.
            // Второй прогон демо начинался бы с той, что игрок выбрал
            // в первом, и «по умолчанию» означало бы разное.
            SquadOrders.Reset();
        }

        void Start()
        {
            if (CombatManager.Instance != null)
                CombatManager.Instance.OnUnitsChanged += OnUnitsChanged;

            if (!string.IsNullOrEmpty(_arrivalLine))
                Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(Loc.T(_arrivalLine));

            if (_endsAfterSeconds > 0f)
            {
                var altar = GameObject.Find("Altar");
                if (altar == null) altar = GameObject.Find("Зал");

                if (altar != null) _altar = altar.transform;
                else Debug.LogWarning("[ПРОЛОГ] В последней доле нет ни алтаря, "
                                    + "ни зала: эпилог придёт по времени, а не "
                                    + "по шагу игрока. Пересоберите сцены.");
            }
        }

        /// <summary>
        /// Вошёл ли Греховод в зал. «Игрок входит в склеп. Пустой трон,
        /// алтарь, замурованный гроб в нише. И тогда входит отряд»
        /// (09-PROLOGUE §4, сцена 8). До 24 сентября эпилог приходил
        /// через четырнадцать секунд после ответа о плате, где бы игрок
        /// ни стоял, — автор: «можно просто стоять, а сюжет будет
        /// двигаться».
        /// </summary>
        private bool EnteredHall()
        {
            if (_entered || _altar == null || !SinbinderPlayer.Exists) return true;

            _entered = CampFocus.GroundDistance(SinbinderPlayer.Where, _altar.position)
                    <= _altarReach;
            return _entered;
        }

        void OnDestroy()
        {
            if (CombatManager.Instance != null)
                CombatManager.Instance.OnUnitsChanged -= OnUnitsChanged;
        }

        /// <summary>
        /// Напомнить о шаге игрока раз в <see cref="_altarNudge"/> секунд
        /// игры не на паузе. Строка — что сделать, целиком, а не намёк:
        /// напоминание читает тот, кто уже не понял.
        /// </summary>
        private void Nudge(string line)
        {
            _sinceNudge += Time.unscaledDeltaTime;
            if (_sinceNudge < _altarNudge) return;

            _sinceNudge = 0f;
            Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(line);
        }

        /// <summary>
        /// Уйти немедленно. Зовёт <see cref="EscapeZone"/>, когда отсчёт
        /// вышел: кто в круге — тот идёт дальше.
        /// </summary>
        public void LeaveNow(string line) => Leave(line);

        void Update()
        {
            if (_leaving || _waitForBattle || _waitForEscape) return;

            // Последняя доля: игрок входит в зал, осматривается, и входит
            // отряд. Ждём шага — дойти до алтаря, — а время отсчитываем
            // только после него: на прочтение и на то, чтобы отряд вошёл.
            if (_endsAfterSeconds > 0f)
            {
                // Склеп встречает отряд: потеря Каргана, «нас мало»
                // (CryptArrival). Пока идёт — ничего не считаем.
                if (CryptArrival.Running) { _sinceCommander = 0f; return; }

                // На паузе не считаем и не напоминаем: игрок читает панель,
                // а не медлит.
                if (Core.GamePauseController.Stopped) return;

                // Шаг игрока: поднять воина (docs/37-DEMO.md §4, шаг 2). Пока
                // в суме есть душа, а мастерская не подняла никого, эпилог
                // ждёт — создание воина и есть эта часть демо. Душ нет —
                // ждать нечего. Мастерская — за выключателем «связывание».
                // Раньше платы: связывание отвечает на «нас мало», сказанное
                // только что, а плата — уже следующая беда (§0, 10 октября).
                if (Crypt.CryptWorkshop.Waiting)
                {
                    _sinceCommander = 0f;
                    Nudge(Loc.T("Чтобы поднять воина, возьмите душу из сумы, положите её "
                              + "в устройство у левой стены, добавьте тело со стола "
                              + "и потяните рычаг."));
                    return;
                }

                // Спросили о плате — ждём ответа, сколько нужно. Эпилог
                // поверх неотвеченной панели запер бы выбор под собой:
                // до 14 сентября счётчик шёл и на паузе, и через
                // четырнадцать секунд конец демо ложился поверх вопроса.
                var salary = Object.FindFirstObjectByType<UI.SalaryPanelUI>();
                if (salary != null && !UI.SalaryPanelUI.Answered && !UI.SalaryPanelUI.Payday)
                { _sinceCommander = 0f; return; }

                // Шаг игрока: войти в зал. Пока он стоит у входа, отряд
                // не входит — только склеп напоминает о себе. Плата лично
                // кончается там же, у алтаря.
                if (!EnteredHall())
                {
                    _sinceCommander = 0f;
                    Nudge(UI.SalaryPanelUI.Payday
                        ? Loc.T("Когда заплатите, кому решили, подойдите к алтарю в глубине зала.")
                        : Loc.T("Подойдите к алтарю в глубине зала."));
                    return;
                }

                if (salary != null && !UI.SalaryPanelUI.Answered) { _sinceCommander = 0f; return; }

                // Вылазки — не шаг истории, а песочница после неё
                // (docs/37-DEMO.md §0): стол встаёт, когда игрок решит
                // остаться в склепе на экране эпилога (Crypt.CryptMap.Open).

                _sinceCommander += Time.unscaledDeltaTime;
                if (_sinceCommander < _endsAfterSeconds) return;

                Leave("");
                return;
            }

            // Лагерь: старший назначен — начинается сцена 3, и ведёт её шар.
            if (string.IsNullOrEmpty(SquadRoster.CommanderName)) return;

            // Пока шар ведёт сцену 3, ждём его сколько нужно и запаса
            // не тратим. Сверять два срока руками нельзя: они разъезжаются
            // при первой же правке любого из них, и лагерь уходил бы
            // посреди сцены, не дав игроку разобрать трофеи.
            if (CrystalBall.Leading) { _sinceCommander = 0f; return; }

            // Реальное время: совет только что снял паузу, и растягивать
            // страховку на чужие остановки незачем. Кроме урока: пока
            // игрок учится отдавать вещь, лагерь не уходит у него из-под ног.
            if (Core.GamePauseController.Instance != null
                && Core.GamePauseController.Instance.Held) return;
            _sinceCommander += Time.unscaledDeltaTime;
            if (_sinceCommander < _campGrace) return;

            Debug.LogWarning("[ПРОЛОГ] Тревоги не случилось: лагерь уходит "
                           + "без сцены 3.");
            Leave(Loc.T("Отряд выступает."));
        }

        private void OnUnitsChanged()
        {
            if (_leaving) return;

            var combat = CombatManager.Instance;
            if (combat == null) return;

            // Отряд, которого не стало, дальше не идёт ни по какому условию.
            // Проверяем это в любом режиме: на побеге ждать края было бы
            // некому, и демо заперлось бы на мёртвом поле.
            if (_battleJoined && combat.GetAlivePlayerCount() == 0)
            {
                Leave(Loc.T("Отряд не вернулся."), wiped: true);
                return;
            }

            // До первой встречи с врагом ноль на поле ничего не значит:
            // отряд ещё только собирается.
            //
            // Встречей считается миг, когда на поле видны <b>обе</b> стороны.
            // До 14 сентября хватало одних врагов — и в начале набега
            // охотники, случалось, регистрировались раньше отряда: воинов
            // ставят спавнеры в своих Start, и порядок этих Start Unity
            // не обещает. Директор видел врагов, видел ноль своих, решал
            // «отряд не вернулся» — и демо кончалось, не начав боя.
            // Зависело от порядка загрузки, то есть ломалось через раз.
            // Нашёл прогон DemoWalkthrough.
            if (combat.GetAliveEnemyCount() > 0)
            {
                if (combat.GetAlivePlayerCount() > 0) _battleJoined = true;
                return;
            }

            if (!_waitForBattle) return;   // уходим не по концу боя
            if (!_battleJoined) return;

            Leave(Loc.T("Поле осталось за отрядом."));
        }

        private void Leave(string line, bool wiped = false)
        {
            if (_leaving) return;
            _leaving = true;

            var log = Object.FindFirstObjectByType<UI.BattleLogUI>();
            if (log != null && !string.IsNullOrEmpty(line)) log.Write(line);

            // Музыка доли гаснет, пока длится задержка: следующая доля
            // начнёт свою, а не оборвёт эту на полуфразе.
            Audio.Music.Stop(_delaySeconds);

            StartCoroutine(LeaveRoutine(wiped));
        }

        private IEnumerator LeaveRoutine(bool wiped)
        {
            // Реальное время: панель платы могла поставить игру на паузу.
            yield return new WaitForSecondsRealtime(_delaySeconds);

            // Отряд, которого не стало, дальше не идёт — пролог начинается
            // заново, иначе следующая доля соберёт мертвецов.
            if (wiped) SquadRoster.Clear();

            if (wiped || string.IsNullOrEmpty(_nextScene))
            {
                var end = Object.FindFirstObjectByType<UI.DemoEndUI>();
                if (end != null) { end.Show(wiped); yield break; }

                // Экран конца есть только в склепе. Отряд, легший в набеге,
                // до 24 сентября кончался строкой в консоли: игрок оставался
                // на пустом поле без единой кнопки.
                if (wiped)
                {
                    UI.GameOverUI.Show(Loc.T("Отряд не вернулся."),
                        Loc.T("Никто не дошёл до склепа. Души разойдутся Некроэфиром, "
                      + "и помнить о них будет некому.\n\nИгра окончена."));
                    yield break;
                }

                Debug.Log("[ПРОЛОГ] Демо окончено.");
                yield break;
            }

            // Набег — событие лагеря, а не отдельная сцена (слово автора,
            // 24 сентября): Греховода не отбрасывает к палатке, отряд стоит
            // там, где стоял. Сцены набега нет вовсе — грузить её нечего.
            if (_nextScene == RaidEvent.SceneName)
            {
                if (!RaidEvent.Stage(this))
                    Debug.LogError("[ПРОЛОГ] Разгром разворачивается только в лагере "
                                 + "(Prologue_Camp), а ведущий стоит в другой сцене. "
                                 + "Пересоберите сцены демо.");
                yield break;
            }

            Core.GamePauseController.Instance?.Resume();
            SceneManager.LoadScene(_nextScene);
        }

        /// <summary>
        /// Стать ведущим набега в той же сцене: дальше доля кончается
        /// краем карты, а следующая — <paramref name="next"/>. Зовёт
        /// <see cref="RaidEvent"/>, развернув набег на месте.
        /// </summary>
        public void BecomeRaid(string next)
        {
            _nextScene = next;
            _waitForEscape = true;
            _waitForBattle = false;
            _battleJoined = false;
            _leaving = false;
        }
    }
}
