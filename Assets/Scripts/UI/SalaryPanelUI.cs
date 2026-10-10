// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.Gameplay;

using Sinbinder.Core;
namespace Sinbinder.UI
{
    /// <summary>
    /// Плата после вылазки — второй соблазн пролога.
    ///
    /// Механика была написана и не подключена: <c>Warrior.PaySalary</c>
    /// существовал, но его не звал никто, а <c>UnpaidMissions</c> читали
    /// три места и не писало ни одно. Голос Жадности против приказа
    /// (<c>GreedObeyUnpaidPenalty</c>) поэтому не мог сработать никогда —
    /// один из четырёх рычагов игрока был муляжом.
    ///
    /// Соблазн должен быть настоящим, иначе он не соблазн: заплатить —
    /// значит расстаться с золотом, придержать — ничего не потерять
    /// сейчас и потерять верность потом. Игрок сам выбирает, чем
    /// заплатит, и узнаёт цену через четыре минуты.
    ///
    /// Чисел на панели нет: ни суммы, ни остатка. Только слова —
    /// правило проекта одно для всех экранов.
    ///
    /// <b>Плата лично</b> (решение автора, 30 сентября; docs/40-SWITCHES.md
    /// §8.1). Прежде «Заплатить» брало плату за всех разом — пять–восемь
    /// плат при кошеле меньше трёх, и «платить было нечем» выходило всегда:
    /// выбора не было. Теперь платят из рук в руки, в разговоре (F, пункт 2,
    /// <see cref="GearPanel"/>), кому хватит монет, — и вопрос становится
    /// «кому». Кончается плата у алтаря: кому не заплачено до него, тот
    /// запомнит. Заплатили всем — кончается сразу.
    /// </summary>
    public class SalaryPanelUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Text _title;
        [SerializeField] private Button _payButton;
        [SerializeField] private Text _payLabel;
        [SerializeField] private Button _withholdButton;
        [SerializeField] private Text _withholdLabel;

        // Плата одному — SquadGear.Wage: та же, что в разговоре в лагере.

        [Tooltip("Спросить о плате, как только отряд пришёл в сцену, а не "
               + "по концу боя. Ставится склепу: вылазка кончается побегом, "
               + "и платят за неё там, куда пришли, а не посреди набега.")]
        [SerializeField] private bool _askOnArrival;

        private bool _sawEnemies;
        private bool _asked;

        /// <summary>
        /// Ответил ли игрок о плате. Конец доли в склепе ведётся этим,
        /// а не секундами (<see cref="Gameplay.PrologueDirector"/>):
        /// эпилог поверх неотвеченной панели запер бы выбор под собой.
        /// </summary>
        public static bool Answered { get; private set; }

        /// <summary>Идёт плата лично: от выбора «платить лично» до алтаря.</summary>
        public static bool Payday { get; private set; }

        /// <summary>Кому ещё не заплачено за эту вылазку.</summary>
        private static readonly HashSet<Warrior> Owed = new();

        /// <summary>Скольким заплачено лично за эту вылазку.</summary>
        private static int _paidInPerson;

        /// <summary>Ближе этого к алтарю — плата кончена (как у эпилога, PrologueDirector).</summary>
        private const float AltarReach = 3.5f;

        private Transform _altar;

        /// <summary>Ждёт ли этот воин платы за вылазку.</summary>
        public static bool Owes(Warrior w) => Payday && w != null && Owed.Contains(w);

        /// <summary>Воину заплачено лично (<see cref="SquadGear.PayDebt"/>).</summary>
        public static void Settle(Warrior w)
        {
            if (w != null && Owed.Remove(w)) _paidInPerson++;
        }

        void Awake()
        {
            Answered = false;
            Payday = false;
            Owed.Clear();
            _paidInPerson = 0;
        }

        void Start()
        {
            if (_panel != null) _panel.SetActive(false);

            if (_payButton != null) _payButton.onClick.AddListener(PayInPerson);
            if (_withholdButton != null) _withholdButton.onClick.AddListener(Withhold);

            // До 14 сентября панель стояла в набеге и открывалась, когда
            // падала первая волна: «Вылазка окончена» и пауза — посреди
            // боя, до подкрепления, которое вот-вот выйдет. В порядке
            // пролога, записанном автором, после сбора душ идут
            // подкрепление и сразу побег. Нашёл прогон DemoWalkthrough.
            if (_askOnArrival)
            {
                StartCoroutine(AskWhenArrived());
                return;
            }

            if (CombatManager.Instance != null)
                CombatManager.Instance.OnUnitsChanged += OnUnitsChanged;
        }

        /// <summary>
        /// Спросить, когда уйдёт заставка сцены: панель поверх чёрного
        /// полотна никто не прочтёт.
        ///
        /// <b>И не раньше, чем склеп встретил отряд, а мастерская подняла
        /// воина</b> (docs/37-DEMO.md §0, 10 октября): сперва потеря Каргана,
        /// потом «нас мало» и ответ на это — связывание, и только потом
        /// плата. До того панель вставала сразу после заставки — первым,
        /// что игрок видел в склепе после бегства, было «Отряд ждёт платы».
        /// </summary>
        private System.Collections.IEnumerator AskWhenArrived()
        {
            yield return Gameplay.Beat.Until(() => Core.GamePauseController.Instance == null
                                                 || !Core.GamePauseController.Instance.IsPaused,
                30f, Loc.T("Заставка склепа не ушла — о плате спрашиваем поверх неё."));

            // Отряд должен успеть появиться: платят живым, а их ставит
            // спавнер в своём Start.
            yield return null;

            while (Gameplay.CryptArrival.Running) yield return null;

            // Связывание — шаг игрока, ждём сколько нужно: напоминает
            // о нём ведущий (PrologueDirector). Поднял — миг поднятому:
            // панель поверх «Spark of Eternity» отняла бы его.
            if (Crypt.CryptWorkshop.Waiting)
            {
                while (Crypt.CryptWorkshop.Waiting) yield return null;
                yield return Moment(RaisedMoment);
            }

            Open();
        }

        /// <summary>Сколько дать поднятому постоять, прежде чем спросить о плате, секунд.</summary>
        private const float RaisedMoment = 5f;

        /// <summary>Подождать столько секунд игры не на паузе.</summary>
        private static System.Collections.IEnumerator Moment(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                if (!Core.GamePauseController.Stopped) t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        void OnDestroy()
        {
            if (CombatManager.Instance != null)
                CombatManager.Instance.OnUnitsChanged -= OnUnitsChanged;
        }

        private void OnUnitsChanged()
        {
            if (_asked || CombatManager.Instance == null) return;

            if (CombatManager.Instance.GetAliveEnemyCount() > 0) { _sawEnemies = true; return; }

            // Спрашиваем только после настоящего боя: сцена без врагов
            // не повод требовать плату.
            if (!_sawEnemies) return;
            if (CombatManager.Instance.GetAlivePlayerCount() == 0) return;

            Open();
        }

        public void Open()
        {
            if (_asked || _panel == null) return;
            _asked = true;

            if (_title != null) _title.text = Loc.T("Отряд пережил эту ночь и ждёт платы.");
            if (_payLabel != null) _payLabel.text = Loc.T("Платить лично\nкому хватит монет");
            if (_withholdLabel != null) _withholdLabel.text = Loc.T("Придержать всем\nони запомнят");

            Modal.Open(_panel);
            Core.GamePauseController.Instance?.Pause();
        }

        /// <summary>
        /// Платить лично: панель уходит, мир идёт, игрок подходит к воинам
        /// и платит в разговоре. Кому — решает он: монет на всех не хватит.
        /// </summary>
        private void PayInPerson()
        {
            BeginPayday(Squad());
            Modal.Close(_panel);
            Core.GamePauseController.Instance?.Resume();

            if (!Payday) { Answered = true; return; }

            var altar = GameObject.Find("Altar");
            if (altar == null) altar = GameObject.Find("Зал");
            _altar = altar != null ? altar.transform : null;

            Log(Loc.T("Платите каждому из рук в руки: подойдите к воину, нажмите F "
                    + "и выберите второй пункт. Кому не заплатите, пока не дойдёте "
                    + "до алтаря, тот это запомнит."));
        }

        /// <summary>
        /// Начать плату лично: должны все живые свои, кроме Греховода.
        /// Отдельно от кнопки — самопроверке (Tests/SelfCheck, PaydayInPerson).
        /// Платить некому — плата сразу кончена.
        /// </summary>
        public static void BeginPayday(IEnumerable<Warrior> squad)
        {
            Owed.Clear();
            _paidInPerson = 0;
            if (squad != null)
                foreach (var w in squad)
                    if (Due(w)) Owed.Add(w);

            Payday = Owed.Count > 0;
            if (!Payday)
            {
                Answered = true;
                Log(Loc.T("Платить некому."));
            }
        }

        /// <summary>
        /// Плата кончена: кому не заплачено — не заплачено, и он это
        /// запомнит (<see cref="Warrior.PaySalary"/> с нулём — тот же,
        /// что «придержать»). Честность та же: намерение не в счёт.
        /// </summary>
        public static void FinishPayday()
        {
            if (!Payday) return;
            Payday = false;

            int left = 0;
            foreach (var w in Owed)
            {
                if (w == null || w.IsDead) continue;
                w.PaySalary(0f);
                left++;
            }
            Owed.Clear();

            if (left == 0) Log(Loc.T("Отряду заплачено — каждому из рук в руки."));
            else if (_paidInPerson == 0) Log(Loc.T("Никому не заплачено. Отряд это запомнил."));
            else Log(Loc.T("Остальным не заплачено. Они это запомнили."));

            Answered = true;
        }

        /// <summary>
        /// Кому платят за эту ночь: кто её пережил. Поднятый в мастерской
        /// склепа её не переживал — этой ночью он охотился на вас, — и плата
        /// ему, как и обида за неё, была бы ложью. Его нет в составе отряда:
        /// состав пишется при уходе со сцены.
        /// </summary>
        private static IEnumerable<Warrior> Squad()
        {
            var all = CombatManager.Instance?.GetAllWarriors();
            if (all == null) yield break;

            foreach (var w in all)
                if (w != null && (!SquadRoster.HasSquad || SquadRoster.TryGet(w.DisplayName, out _)))
                    yield return w;
        }

        /// <summary>Платят своим живым воинам. Греховод себе не платит.</summary>
        private static bool Due(Warrior w)
            => w != null && !w.IsDead && w.Team == Team.Player && !(w is SinbinderPlayer);

        void Update()
        {
            if (!Payday) return;

            // Заплатили всем — кончено: ждать алтаря незачем.
            Owed.RemoveWhere(w => w == null || w.IsDead);
            if (Owed.Count == 0) { FinishPayday(); return; }

            // Алтарь — конец платы: дальше эпилог, и кто вернулся, тот вернулся.
            if (_altar != null && SinbinderPlayer.Exists
                && CampFocus.GroundDistance(SinbinderPlayer.Where, _altar.position) <= AltarReach)
                FinishPayday();
        }

        private void Withhold()
        {
            foreach (var w in Squad())
                if (Due(w)) w.PaySalary(0f);

            Log(Loc.T("Золото осталось в мешке. Отряд это запомнил."));
            Close();
        }

        private void Close()
        {
            Answered = true;
            Modal.Close(_panel);
            Core.GamePauseController.Instance?.Resume();
        }

        private static void Log(string text)
        {
            var log = Object.FindFirstObjectByType<BattleLogUI>();
            if (log != null) log.Write(text);
            else Debug.Log("[ПЛАТА] " + text);
        }
    }
}
