using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            if (_startsPrologue) SquadRoster.Clear();
        }

        void Start()
        {
            if (CombatManager.Instance != null)
                CombatManager.Instance.OnUnitsChanged += OnUnitsChanged;
        }

        void OnDestroy()
        {
            if (CombatManager.Instance != null)
                CombatManager.Instance.OnUnitsChanged -= OnUnitsChanged;
        }

        /// <summary>
        /// Уйти немедленно. Зовёт <see cref="EscapeZone"/>, когда отсчёт
        /// вышел: кто в круге — тот идёт дальше.
        /// </summary>
        public void LeaveNow(string line) => Leave(line);

        void Update()
        {
            if (_leaving || _waitForBattle || _waitForEscape) return;

            // Лагерь: старший назначен — начинается сцена 3, и ведёт её шар.
            if (string.IsNullOrEmpty(SquadRoster.CommanderName)) return;

            // Реальное время: совет только что снял паузу, и растягивать
            // страховку на чужие остановки незачем.
            _sinceCommander += Time.unscaledDeltaTime;
            if (_sinceCommander < _campGrace) return;

            Debug.LogWarning("[ПРОЛОГ] Тревоги не случилось: лагерь уходит "
                           + "без сцены 3.");
            Leave("Отряд выступает.");
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
                Leave("Отряд не вернулся.", wiped: true);
                return;
            }

            // До первой встречи с врагом ноль на поле ничего не значит:
            // отряд ещё только собирается.
            if (combat.GetAliveEnemyCount() > 0) { _battleJoined = true; return; }

            if (!_waitForBattle) return;   // уходим не по концу боя
            if (!_battleJoined) return;

            Leave("Поле осталось за отрядом.");
        }

        private void Leave(string line, bool wiped = false)
        {
            if (_leaving) return;
            _leaving = true;

            var log = Object.FindFirstObjectByType<UI.BattleLogUI>();
            if (log != null && !string.IsNullOrEmpty(line)) log.Write(line);

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

                Debug.Log("[ПРОЛОГ] Демо окончено.");
                yield break;
            }

            Core.GamePauseController.Instance?.Resume();
            SceneManager.LoadScene(_nextScene);
        }
    }
}
