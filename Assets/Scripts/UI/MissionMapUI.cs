// Assets/Scripts/UI/MissionMapUI.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.AOS;
using Sinbinder.Crypt;
using Sinbinder.Gameplay;

using Sinbinder.Core;
namespace Sinbinder.UI
{
    /// <summary>
    /// Карта в хрустальном шаре: куда пойти и кого послать.
    ///
    /// Два шага в одной панели. Сперва точка — читается слух и то,
    /// насколько там опасно. Потом старший — и вот здесь у каждого
    /// стоит <b>пророчество</b>, посчитанное настоящим резолвером
    /// (<see cref="TemperamentPredictor"/>), а не написанное руками.
    ///
    /// Пророчество и есть то, ради чего этот экран вообще существует.
    /// Без него выбор старшего — выбор имени; с ним игрок читает,
    /// чем этот человек рискует всё сорвать, и решает, стоит ли оно
    /// того. Через полчаса ему вернут счёт.
    ///
    /// Панель ничего не считает: и допуск, и исход, и объяснение
    /// спрашиваются у <see cref="MissionBoard"/>. Своя проверка здесь
    /// разошлась бы с движком при первой же правке.
    ///
    /// Компонент висит <b>на Canvas</b>, а не на панели, которую сам
    /// выключает: у выключенного объекта не крутится Update, и подход
    /// игрока к шару остался бы незамеченным.
    /// </summary>
    public class MissionMapUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Text _title;
        [SerializeField] private RectTransform _rows;
        [SerializeField] private Font _font;

        private readonly List<GameObject> _spawned = new();
        private ScrollRect _missionScroll;

        private static MissionMapUI _shown;

        /// <summary>Открыта ли карта. Спрашивает <see cref="Modal.Any"/>.</summary>
        public static bool IsOpen => _shown != null && _shown._panel != null && _shown._panel.activeInHierarchy;

        private CrystalBall _ball;
        private MissionBoard _board;

        private bool _pickingCommander;
        private Mission _chosen;

        /// <summary>Открыта карточка экспедиции (между картой и выбором старшего).</summary>
        private bool _card;

        /// <summary>
        /// Шаг сбора отряда: старший выбран, игрок решает, кого он возьмёт.
        /// До 16 октября шага не было — спутников подбирал список по порядку
        /// (автор: «войны, отправляемые на них, фиксированные»).
        /// </summary>
        private bool _gathering;
        private bool _personal;
        private int _replayPage;
        private string _leader;
        private readonly HashSet<string> _party = new();

        /// <summary>Отходил ли игрок от шара с прошлого раза.</summary>
        private bool _leftTheBall = true;

        void Start()
        {
            EnsureScrolling();
            if (_panel == gameObject)
                Debug.LogError("[КАРТА] Компонент висит на панели, которую сам "
                             + "выключает: подход к шару замечен не будет.");

            if (_panel != null) _panel.SetActive(false);

            _ball = Object.FindFirstObjectByType<CrystalBall>();
            _board = Object.FindFirstObjectByType<MissionBoard>();

            // Стол в склепе демо спрятан без выключателя «вылазки» (CryptMap):
            // тогда его нет не по ошибке, и найдём, когда появится.
            if (Object.FindFirstObjectByType<CryptMap>() != null) return;

            if (_ball == null)
                Debug.LogWarning("[КАРТА] Шара в сцене нет: открывать нечем.");
            if (_board == null)
                Debug.LogError("[КАРТА] Доски вылазок нет: отправлять некому.");
        }

        private float _lookAgain;

        void Update()
        {
            if (_panel == null) return;

            // Карту включили на ходу (консоль «вылазки») — стол появился
            // после старта. Искать раз в секунду, а не каждый кадр.
            if ((_ball == null || _board == null) && Time.unscaledTime >= _lookAgain)
            {
                _lookAgain = Time.unscaledTime + 1f;
                if (_ball == null) _ball = Object.FindFirstObjectByType<CrystalBall>();
                if (_board == null) _board = Object.FindFirstObjectByType<MissionBoard>();
            }

            if (_panel.activeSelf)
            {
                // Выход обязан быть. Панель ставит игру на паузу, и карта,
                // которую нельзя закрыть, запирает склеп насмерть — ровно
                // тот род поломки, из-за которого пришлось вырезать сцену
                // побега.
                if (ExpeditionReplayViewer.OwnsEscape) return;
                if (Input.GetKeyDown(KeyCode.Escape)) Back();
                return;
            }

            if (_ball == null) return;

            bool near = _ball.PlayerIsClose();

            // Отойти и вернуться — вот способ открыть карту заново.
            // Без этого закрытая панель распахивалась бы в следующем же
            // кадре: игрок всё ещё стоит у шара.
            if (!near) { _leftTheBall = true; return; }
            if (!_leftTheBall) return;

            _leftTheBall = false;
            Open();
        }

        /// <summary>
        /// Шаг назад: со списка старших — к точкам, с точек — из карты.
        /// Один клавишный выход на оба шага: два разных были бы двумя
        /// правилами там, где хватает одного.
        /// </summary>
        private void Back()
        {
            if (_gathering) { PickCommander(_chosen); return; }
            if (_pickingCommander) { ShowCard(_chosen); return; }
            if (_card) { ShowMissions(); return; }

            Close();
        }

        public void Open()
        {
            if (_panel == null || _board == null) return;

            // Склеп-песочница: состав — с тех, кто стоит в склепе. Поднятые
            // в мастерской иначе в нём не значились бы до смены сцены,
            // а плата лично меняет долг на живых, а не в составе.
            if (CryptMap.Opened) SquadRoster.RefreshFromScene(MissionBoard.Squad());

            _pickingCommander = false;
            ShowMissions();

            _shown = this;
            _panel.SetActive(true);
            Core.GamePauseController.Instance?.Pause();
        }

        private void Close()
        {
            _leftTheBall = false;
            if (_panel != null) _panel.SetActive(false);
            Core.GamePauseController.Instance?.Resume();
        }

        private void ShowMissions()
        {
            _personal = false;
            _pickingCommander = false;
            _gathering = false;
            _card = false;

            if (_title != null)
                _title.text = string.IsNullOrEmpty(_board.LastReport)
                    ? Loc.T("Куда послать отряд?")
                    : _board.LastReport;

            Rows(out float y);
            _spawned.Add(Row(y, true, Loc.T("Продолжить игру"),
                MissionBoard.Journeys.Count > 0
                    ? Loc.T("Время вылазок остановлено, пока открыта карта.") : "",
                "", Close, 68f));
            y -= 76f;

            // Карта окрестностей с точками экспедиций (автор, 23.11: «открывается небольшая карта
            // с точками интереса… нажимает на точку — описание, выйти и продолжить, как в This Is
            // the Police»). До 23.11 здесь был список строк в конце прокрутки.
            y -= MapBlock(y) + 8f;

            // Ongoing operations take precedence over archived reports.
            // A commander remains away until the real resolver finishes on return.
            foreach (var journey in MissionBoard.Journeys)
            {
                string commanderId=journey.Commander;
                var phase=journey.CombatBlocked
                    ? Loc.T("AOS-бой остановлен: снимок требует проверки")
                    : journey.ReturnBlocked
                    ? Loc.T("Возврат заблокирован: необходимо восстановить состав")
                    : journey.Phase==0 ? Loc.T("В пути к цели")
                    : journey.Phase==1 ? Loc.T("Ожидает совета (решит сам, если совета нет)")
                    : journey.Phase==3 ? Loc.T("Идёт реальный AOS-бой")
                    : Loc.T("Возвращается в склеп");
                int completed=journey.BattleCheckpoint!=null
                    ? journey.BattleCheckpoint.CompletedRounds : 0;
                var eta=journey.CombatBlocked
                    ? Loc.T("Бой приостановлен. Добыча не выдана; перезапускать расчёт нельзя.")
                    : journey.ReturnBlocked
                    ? Loc.T("Награды не выданы. Исправьте данные состава или загрузите исправное сохранение.")
                    : journey.Phase==3
                    ? Loc.F("Завершено ходов AOS: {0}. До следующего хода около {1} сек.",
                        completed,Mathf.CeilToInt(journey.SecondsRemaining))
                    : journey.Phase==1
                    ? Loc.F("Старший решит сам через {0} сек. Нажмите, чтобы дать совет.",
                        Mathf.CeilToInt(journey.SecondsRemaining))
                    : journey.Phase==0 ? Loc.F("До прибытия около {0} сек. Нажмите, чтобы отозвать.",
                        Mathf.CeilToInt(journey.SecondsRemaining))
                    : Loc.F("До возвращения около {0} сек.",Mathf.CeilToInt(journey.SecondsRemaining));
                _spawned.Add(Row(y,true,
                    Loc.F("Отряд: {0}",Loc.Name(journey.MissionName)),
                    Loc.F("Старший: {0} · {1}",Loc.Name(journey.Commander),phase),
                    eta,journey.CombatBlocked ? null
                        : journey.Phase==1 ? (System.Action)(() => PickJourneyDecision(commanderId))
                        : journey.Phase==0 ? (System.Action)(() =>
                            { _board.RecallJourney(commanderId); ShowMissions(); })
                        : null,125f));
                y-=133f;
            }

            // Wounds are no longer an eternal penalty with no way to
            // recover. Returning soldiers can have their bodies repaired
            // for ordinary silver collected on expeditions. Never treat an
            // away soldier, heal an old save default, or revive the dead.
            var purse=Inventory.PlayerInventory.Instance;
            foreach(var member in SquadRoster.Members)
            {
                if(member.IsAway) continue;
                int cost=ExpeditionHealthRules.TreatmentCost(member.HealthFraction);
                if(cost<=0) continue;
                string name=member.Name;
                bool affordable=purse!=null && purse.Gold>=cost;
                _spawned.Add(Row(y,affordable,
                    Loc.F("Восстановить бойца: {0}",Loc.Name(member.ShownName)),
                    Loc.F("Здоровье: {0}% · восстановление: {1} монет",
                        Mathf.RoundToInt(
                            ExpeditionHealthRules.RestoreFraction(member.HealthFraction)*100f),
                        cost),
                    affordable
                        ? Loc.T("Нажмите: вернуть полное здоровье перед новой экспедицией.")
                        : Loc.T("Не хватает монет. Вернувшийся воин остаётся раненым."),
                    affordable ? (System.Action)(() => RepairWarrior(name))
                               : null,118f));
                y-=126f;
            }

            // The old summary still fits the title, but the complete
            // auto-battle log is now a separate user-controlled viewer.
            // A new mission does not erase old complete battle records.
            // These are actual events from the resolver, not invented shots.
            var history = ExpeditionReplayArchive.History;
            // Only two replays per page; the mission buttons must remain
            // reachable even after eight automatic expeditions.
            _replayPage = Mathf.Clamp(_replayPage, 0,
                Mathf.Max(0,(history.Count-1)/2));
            int last = history.Count - 1 - _replayPage*2;
            int first = Mathf.Max(0,last-1);
            for(int i=last; i>=first; i--)
            {
                var replay=history[i];
                if(replay==null || replay.Frames==null || replay.Frames.Count==0) continue;
                _spawned.Add(Row(y,true,
                    Loc.F("▶ Реплей: {0}",replay.MissionName),
                    Loc.F("Командир: {0} · событий: {1}",replay.Leader,replay.Frames.Count),
                    Loc.T("Полная хронология решений, без вымышленных координат."),
                    () => ExpeditionReplayViewer.Open(replay),105f));
                y-=113f;
            }
            if(history.Count>2)
            {
                bool older=first>0;
                bool newer=_replayPage>0;
                _spawned.Add(Row(y,true,
                    Loc.F("Архив: страница {0} из {1}",_replayPage+1,
                        (history.Count+1)/2),
                    older ? Loc.T("Ещё более ранние реплеи") :
                            Loc.T("Это самые ранние записи"),
                    newer ? Loc.T("Новые записи доступны выше в архиве") : "",
                    () => { _replayPage=older ? _replayPage+1 : 0; ShowMissions(); },
                    78f));
                y-=86f;
            }

        }

        /// <summary>Можно ли сюда идти: есть кому вести, или Греховод пойдёт сам (личная карта).</summary>
        private bool CanGo(Mission m)
        {
            // The Sinbinder himself is a valid one-person party.
            // No roster soldiers is NOT a reason to disable a personally
            // visitable expedition. Automatic dispatch still requires
            // an available commander.
            bool personalPossible = ExpeditionMapPlan.ForName(m.Name) != null &&
                                    SinbinderPlayer.Exists && !SinbinderPlayer.Instance.IsDead;
            return _board.Ready() > 0 || personalPossible;
        }

        /// <summary>
        /// Карточка экспедиции: слух, опасность и добыча словами, что известно о месте, —
        /// и два выхода: «Выйти» (к карте) и «Продолжить» (выбрать, кто поведёт). О противниках —
        /// только встреченное (автор, 23.11: «знать о противниках экспедиции только после того,
        /// как узнали их»): что сделали мы и кого видели (PersonalExpedition.KnownAbout).
        /// </summary>
        private void ShowCard(Mission m)
        {
            _personal = false;
            _pickingCommander = false;
            _gathering = false;
            _card = true;
            _chosen = m;

            if (_title != null) _title.text = Loc.Name(m.Name) + Loc.T("  Esc — назад");
            Rows(out float y);

            // Людей меньше, чем советуют, — не запрет (с 16 октября):
            // послать можно и одного. Строка говорит, сколько советуют
            // и сколько есть, а решает игрок.
            int ready = _board.Ready();
            string facts = $"{MissionCatalog.Danger(m)} {MissionCatalog.Riches(m.Prize)} "
                         + MissionCatalog.Promise(m.Spoils);
            // Сколько советуют — уже в строке опасности (MissionCatalog.Danger).
            if (ready < m.Squad)
                facts += " " + Loc.F("У вас сейчас только {0}.", Leadership.Collective(ready));
            y -= TextBlock(y, Loc.T("Слух"), m.Rumour + "\n" + facts) + 8f;

            var plan = ExpeditionMapPlan.ForName(m.Name);
            var known = PersonalExpedition.KnownAbout(plan);
            y -= TextBlock(y, Loc.T("Что известно"), known.Count > 0 ? string.Join("\n", known)
                : Loc.T("Здесь ещё никто из ваших не был. Кто там — узнаете на месте.")) + 8f;

            bool can = CanGo(m);
            Buttons(y, (Loc.T("Выйти"), true, ShowMissions),
                       (Loc.T("Продолжить"), can, () => PickCommander(m)));
            if (!can)
                _spawned.Add(Row(y - 84f, false, Loc.T("Вести некому"),
                    Loc.T("Все бойцы в пути или не могут идти."), "", null, 68f));
        }

        private void PickCommander(Mission mission)
        {
            _personal = false;
            _card = false;
            _chosen = mission;
            _pickingCommander = true;
            _gathering = false;

            if (_title != null)
                _title.text = Loc.F("{0}. Кто поведёт отряд?  Esc — назад", Loc.Name(mission.Name));

            Rows(out float y);

            // Personal visit is available on the first two physical maps.
            // It uses the same party picker, but the Lord commands directly.
            if (ExpeditionMapPlan.ForName(mission.Name) != null && SinbinderPlayer.Exists)
            {
                _spawned.Add(Row(y, true,
                    Loc.T("Греховод — вести лично"),
                    Loc.T("Выйти на трёхмерную карту, командовать и собрать добычу самому."),
                    Loc.T("Души может собирать только Греховод."),
                    () => LeadPersonal()));
                y -= 150f;
            }

            foreach (var member in SquadRoster.Members)
            {
                string why = _board.WhyNot(member, mission);
                bool can = string.IsNullOrEmpty(why);
                var name = member.Name;

                // Пророчество спрашиваем у настоящей души со сцены,
                // а не у записи в составе: у души есть смещения оболочки,
                // а у записи их нет.
                string prophecy = Foretell(name);

                // Имя — по старшинству, как в совете у стола (Core/Naming);
                // до 16 октября здесь стояло голое имя, и без Loc.Name.
                _spawned.Add(Row(y, can,
                    Loc.Name(member.ShownName),
                    !can ? why
                        : (Leadership.CanLead(member.Leadership, mission.Squad) ? Leadership.Describe(member.Leadership)
                            : Leadership.Shortfall(member.Leadership, mission.Squad))
                          + (HealthNote(member).Length>0 ? " · " + HealthNote(member) : ""),
                    prophecy,
                    can ? () => Lead(name) : (System.Action)null));

                y -= 150f;
            }
        }

        /// <summary>
        /// Старший выбран — собрать ему отряд. Открывается с подсказкой
        /// (<see cref="SquadRoster.Suggest"/>): не тронувший ничего уводит
        /// тех же, кого уводил до 16 октября. Опытных в склепе подсказка
        /// не бережёт — кем рискнуть, решает игрок.
        /// </summary>
        private void LeadPersonal()
        {
            _personal = true;
            _leader = Loc.N("Греховод");
            _gathering = true;
            _party.Clear();
            int count = 0;
            foreach(var member in SquadRoster.Members)
                if (count < Room && string.IsNullOrEmpty(_board.WhyNot(member, _chosen)))
                {
                    _party.Add(member.Name);
                    count++;
                }
            ShowParty(null);
        }

        private void Lead(string commanderName)
        {
            _personal = false;
            _leader = commanderName;
            _gathering = true;
            _party.Clear();
            foreach (var name in SquadRoster.Suggest(commanderName, MissionBoard.Fits(commanderName, _chosen),
                                                     keepExperienced: false))
                _party.Add(name);
            ShowParty(null);
        }

        /// <summary>Скольких старший возьмёт сверх себя.</summary>
        private int Room => Mathf.Max(0,
            (_personal ? _chosen.Squad : MissionBoard.Fits(_leader, _chosen)) - 1);

        /// <summary>Отправить отряд или начать личную вылазку.</summary>
        private void Gathered()
        {
            _gathering = false;
            if (_personal)
            {
                if (PersonalExpedition.Enter(_chosen, new List<string>(_party), out var error))
                {
                    Close();
                    return;
                }
                _gathering = true;
                ShowParty(error);
                return;
            }
            // Decisions occur when the squad reaches the location, not before departure.
            Send(_leader, null);
        }

        private void Toggle(string name)
        {
            if (_party.Contains(name)) _party.Remove(name);
            else if (_party.Count < Room) _party.Add(name);
            else { ShowParty(Loc.T("Свободных мест в отряде нет. Сначала снимите выбор с другого бойца.")); return; }
            ShowParty(null);
        }

        /// <summary>
        /// Кого старший возьмёт: строка на каждого, щелчок — идёт или
        /// остаётся. Последняя строка — отправить: с любым числом, хоть одного
        /// старшего (16 октября); меньше совета — заголовок говорит, чем грозит.
        /// </summary>
        private void ShowParty(string warning)
        {
            int total = _party.Count + 1;
            string leader = _personal ? Loc.T("Греховод") : Loc.Name(Shown(_leader));

            // Меньше, чем советуют, — можно; сказано, чем это грозит.
            string risk = total >= _chosen.Squad ? ""
                : total == 1
                    ? Loc.F("Советуют послать {0}, а пойдёт один — охраны от этого меньше не станет.",
                            Leadership.Count(_chosen.Squad))
                    : Loc.F("Советуют послать {0}, а пойдут {1} — охраны от этого меньше не станет.",
                            Leadership.Count(_chosen.Squad), Leadership.Collective(total));

            if (_title != null)
                _title.text = Loc.F("{0}. Кого {1} возьмёт.", Loc.Name(_chosen.Name), leader)
                    + Loc.T("  Esc — назад")
                    + (risk == "" ? "" : "\n" + risk)
                    + (string.IsNullOrEmpty(warning) ? "" : "\n" + warning);

            Rows(out float y);

            var members = new List<SquadRoster.Member>();
            foreach (var m in SquadRoster.Members)
                if (m.Name != _leader && !m.IsAway) members.Add(m);

            // Строк до десятка: шаг — по месту, как в совете у стола, иначе
            // последние уходят за край панели.
            float room = _rows != null ? _rows.rect.height : 0f;
            float step = room > 1f ? Mathf.Min(150f, room / (members.Count + 1)) : 150f;
            float height = Mathf.Max(48f, step - 8f);

            foreach (var m in members)
            {
                string why = _board.WhyNot(m, _chosen);
                bool can = string.IsNullOrEmpty(why);
                bool goes = can && _party.Contains(m.Name);
                string name = m.Name;

                _spawned.Add(Row(y, goes, Loc.Name(m.ShownName), can ? PartyLine(m, goes) : why, "",
                    can ? () => Toggle(name) : (System.Action)null, height));
                y -= step;
            }

            _spawned.Add(Row(y, true,
                _personal ? Loc.T("Войти на карту лично")
                : _party.Count == 0 ? Loc.F("Отправить одного: {0}", leader)
                                  : Loc.F("Отправить отряд — ведёт {0}", leader),
                "", "", Gathered, height));
        }

        /// <summary>Идёт ли и что о нём стоит знать, решая: грех словом, долг, братство.</summary>
        private static string PartyLine(SquadRoster.Member m, bool goes)
        {
            var parts = new List<string> { goes ? Loc.T("идёт") : Loc.T("остаётся в склепе") };
            parts.Add(SoulData.GetSinName(m.Sin).ToLowerInvariant());
            string condition=HealthNote(m);
            if(condition.Length>0) parts.Add(condition.Trim());
            if (m.UnpaidMissions > 0) parts.Add(Grammar.Pick(m.Gender, Loc.T("ему должны"), Loc.T("ей должны")));
            if (m.Brother) parts.Add(Loc.T("брат по оружию"));
            return string.Join(" · ", parts);
        }

        /// <summary>
        /// An actual crypt action: spend real carried silver, repair the
        /// persistent body AND the currently spawned Warrior, then autosave.
        /// Rechecks stock and availability at click time to prevent repeat
        /// purchases from stale UI rows.
        /// </summary>
        private void RepairWarrior(string name)
        {
            if(!CryptMap.Opened || PersonalExpedition.Active ||
               !SquadRoster.TryGet(name,out var member) || member.IsAway)
            { ShowMissions(); return; }

            int cost=ExpeditionHealthRules.TreatmentCost(member.HealthFraction);
            var purse=Inventory.PlayerInventory.Instance;
            if(cost<=0 || purse==null || !purse.SpendGold(cost))
            { ShowMissions(); return; }

            if(!SquadRoster.RepairReturnedWarrior(name))
            {
                purse.AddGold(cost); // reverse a cancelled purchase
                ShowMissions();
                return;
            }

            foreach(var warrior in MissionBoard.Squad())
                if(warrior.DisplayName==name && !warrior.IsDead)
                    warrior.SetHealthFraction(1f);

            Core.SaveSystem.AutoSave();
            ShowMissions();
        }

        private static string HealthNote(SquadRoster.Member member)
        {
            float health=ExpeditionHealthRules.RestoreFraction(member.HealthFraction);
            if(health<0.35f) return Loc.T("тяжело ранен");
            if(health<0.99f) return Loc.T("ранен");
            return "";
        }

        /// <summary>Имя по старшинству для голого имени из состава.</summary>
        private static string Shown(string name)
            => SquadRoster.TryGet(name, out var m) ? m.ShownName : name;

        /// <summary>
        /// Развилка. Игрок <b>предлагает</b>, и это надо было сказать
        /// словами: кнопка, после которой случается не то, что на ней
        /// написано, читается как поломка, пока не объяснено, что это
        /// не приказ.
        /// </summary>
        private void PickOffer(string commanderName)
        {
            _pickingCommander = true;

            if (_title != null)
                _title.text = JunctionCatalog.Situation(_chosen.Junction)
                            + Loc.T("  Что вы им скажете?  Esc — к карте");

            Rows(out float y);

            foreach (var option in JunctionCatalog.Options(_chosen.Junction))
            {
                var offer = option;
                _spawned.Add(Row(y, true,
                    JunctionCatalog.Offer(offer),
                    Loc.T("Это не приказ. Решать будет старший."),
                    "",
                    () => Send(commanderName, offer)));

                y -= 150f;
            }

            // Промолчать — тоже ход: тогда за отряд не говорит никто,
            // и видно, чего он хочет сам.
            _spawned.Add(Row(y, true,
                Loc.T("Промолчать."),
                Loc.T("Пусть решают сами."),
                "",
                () => Send(commanderName, null)));
        }

        private void PickJourneyDecision(string commanderId)
        {
            SavedAutoExpedition pending=null;
            foreach(var journey in MissionBoard.Journeys)
                if(journey.Commander==commanderId && journey.Phase==1)
                { pending=journey; break; }
            if(pending==null) { ShowMissions(); return; }
            Mission mission=default;
            foreach(var item in _board.Missions)
                if(item.Name==pending.MissionName) { mission=item; break; }
            if(mission.Junction==Junction.None)
            {
                _board.OfferJourneyOrder(commanderId,null);
                ShowMissions();
                return;
            }
            _pickingCommander=true;
            if(_title!=null)
                _title.text=JunctionCatalog.Situation(mission.Junction)+
                    Loc.T(" Ваш совет будет передан старшему; окончательное решение за ним.");
            Rows(out float y);
            foreach(var option in JunctionCatalog.Options(mission.Junction))
            {
                var value=option;
                _spawned.Add(Row(y,true,JunctionCatalog.Offer(value),
                    Loc.T("Предложить действие командиру"),"",
                    () => { _board.OfferJourneyOrder(commanderId,value); ShowMissions(); }));
                y-=150f;
            }
            _spawned.Add(Row(y,true,Loc.T("Пусть командир решает сам"),
                Loc.T("Не отправлять рекомендацию"),"",
                () => { _board.OfferJourneyOrder(commanderId,null); ShowMissions(); }));
        }

        private void Send(string commanderName, MissionAction? offer)
        {
            int before=MissionBoard.Journeys.Count;
            string result=_board.Send(_chosen, commanderName,
                new List<string>(_party), offer);
            if (MissionBoard.Journeys.Count != before)
            {
                ShowMissions();
                return;
            }

            _party.RemoveWhere(name => !SquadRoster.TryGet(name, out var member) ||
                !string.IsNullOrEmpty(_board.WhyNot(member, _chosen)));
            _gathering = true;
            ShowParty(result);
        }

        private static string Foretell(string name)
        {
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
                if (w != null && !w.IsDead && w.DisplayName == name)
                    return TemperamentPredictor.Describe(w);

            return "";
        }

        /// <summary>
        /// Old scenes contain a fixed-height list of mission buttons with
        /// no scroll component. Runtime installation preserves those scenes
        /// while ensuring every mission and archived replay stays reachable.
        /// </summary>
        private void EnsureScrolling()
        {
            if (_rows == null || _missionScroll != null) return;
            var existing = _rows.GetComponentInParent<ScrollRect>();
            if (existing != null)
            {
                _missionScroll = existing;
                if (existing.content != null) _rows = existing.content;
                return;
            }

            var viewport = _rows;
            if (viewport.GetComponent<RectMask2D>() == null)
                viewport.gameObject.AddComponent<RectMask2D>();

            var contentObj = new GameObject("Список экспедиций — прокрутка",
                typeof(RectTransform));
            var content = (RectTransform)contentObj.transform;
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, Mathf.Max(1f, viewport.rect.height));
            content.anchoredPosition = Vector2.zero;

            _missionScroll = viewport.gameObject.AddComponent<ScrollRect>();
            _missionScroll.viewport = viewport;
            _missionScroll.content = content;
            _missionScroll.horizontal = false;
            _missionScroll.vertical = true;
            _missionScroll.movementType = ScrollRect.MovementType.Clamped;
            _missionScroll.scrollSensitivity = 34f;
            _missionScroll.inertia = true;
            _rows = content;
        }

        private void Rows(out float y)
        {
            EnsureScrolling();
            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear();
            if (_rows != null)
                _rows.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                    _missionScroll != null ? _missionScroll.viewport.rect.height : 550f);
            if (_missionScroll != null)
                _missionScroll.verticalNormalizedPosition = 1f;
            y = 0f;
        }

        private GameObject Row(float y, bool bright, string title, string second,
            string third, System.Action onClick, float height = 140f)
        {
            var go = new GameObject(title, typeof(RectTransform));
            go.transform.SetParent(_rows, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = new Vector2(0f, y);
            if (_missionScroll != null && _rows != null)
            {
                // Rows are top-anchored; extend the content's actual size
                // or Unity's ScrollRect will clamp all scrolling to zero.
                float required = -y + height + 12f;
                if (required > _rows.rect.height)
                    _rows.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, required);
            }

            var plate = go.AddComponent<Image>();
            plate.color = bright
                ? new Color(0.10f, 0.09f, 0.08f, 0.92f)
                : new Color(0.07f, 0.06f, 0.06f, 0.80f);

            // Кнопка только там, где нажатие сработает. Строка, которая
            // не сработает, не должна и нажиматься.
            if (onClick != null)
            {
                var button = go.AddComponent<Button>();
                button.targetGraphic = plate;
                button.onClick.AddListener(() => onClick());
            }

            // Узкая строка (сбор отряда) — имя и одна строка под ним.
            if (height < 100f)
            {
                Label(rt, title, 24, new Vector2(18f, -6f), 30f, bright);
                Label(rt, second, 18, new Vector2(18f, -Mathf.Min(36f, height * 0.55f)), 24f, bright);
                return go;
            }

            Label(rt, title, 28, new Vector2(18f, -10f), 36f, bright);
            Label(rt, second, 20, new Vector2(18f, -46f), 28f, bright);
            Label(rt, third, 20, new Vector2(18f, -76f), 58f, bright);

            return go;
        }

        // ── Карта окрестностей (автор, 23.11) ──

        // Где точка на карте: доли ширины и высоты. Порядок и места постоянны — карта, которая
        // тасуется, отняла бы у игрока сравнение (MissionCatalog). Склеп — дом, слева.
        private static readonly Dictionary<string, Vector2> Spots = new()
        {
            ["Соляной обоз"] = new Vector2(0.24f, 0.30f),
            ["Придорожная часовня"] = new Vector2(0.40f, 0.68f),
            ["Затопленная каменоломня"] = new Vector2(0.60f, 0.30f),
            ["Сожжённая застава"] = new Vector2(0.64f, 0.80f),
            ["Старый гарнизон"] = new Vector2(0.86f, 0.62f),
            ["Костяная топь"] = new Vector2(0.87f, 0.22f),
        };
        private static readonly Vector2 Home = new Vector2(0.07f, 0.52f);
        private static readonly string[][] Roads =
        {
            new[] { "", "Соляной обоз" }, new[] { "", "Придорожная часовня" },
            new[] { "Придорожная часовня", "Сожжённая застава" }, new[] { "Соляной обоз", "Затопленная каменоломня" },
            new[] { "Затопленная каменоломня", "Костяная топь" }, new[] { "Сожжённая застава", "Старый гарнизон" },
        };

        private static Vector2 SpotOf(string name)
            => string.IsNullOrEmpty(name) ? Home : Spots.TryGetValue(name, out var p) ? p : new Vector2(0.5f, 0.5f);

        /// <summary>Карта с точками: дороги, склеп, места. Нажатие на место — его карточка. Вернёт высоту.</summary>
        private float MapBlock(float y)
        {
            const float height = 440f;
            var go = new GameObject("Карта окрестностей", typeof(RectTransform));
            go.transform.SetParent(_rows, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = new Vector2(0f, y);
            Grow(y, height);
            go.AddComponent<Image>().color = new Color(0.14f, 0.12f, 0.09f, 0.95f);
            _spawned.Add(go);
            Label(rt, Loc.T("Окрестности склепа. Нажмите на место, чтобы прочесть о нём."), 20, new Vector2(18f, -10f), 28f, false);

            var area = new GameObject("Поле карты", typeof(RectTransform));
            area.transform.SetParent(rt, false);
            var field = (RectTransform)area.transform;
            field.anchorMin = Vector2.zero; field.anchorMax = Vector2.one;
            field.offsetMin = new Vector2(40f, 40f); field.offsetMax = new Vector2(-40f, -60f);
            float w = Mathf.Max(1f, field.rect.width), h = Mathf.Max(1f, field.rect.height);

            var road = new Color(0.42f, 0.36f, 0.27f, 0.85f);
            foreach (var pair in Roads) Line(field, SpotOf(pair[0]), SpotOf(pair[1]), w, h, road);

            Point(field, Home, Loc.T("Склеп"), "", new Color(0.55f, 0.75f, 0.85f), true, null);
            foreach (var mission in _board.Missions)
            {
                var m = mission;
                bool can = CanGo(m);
                var plan = ExpeditionMapPlan.ForName(m.Name);
                bool known = plan != null && Survivors.KnownAt(plan.Id).Count > 0;
                bool been = plan != null && PersonalExpedition.KnownAbout(plan).Count > 0;
                string note = known ? Loc.T("там ждут знакомые") : been ? Loc.T("вы там были") : "";
                var colour = known ? new Color(0.85f, 0.32f, 0.25f) : new Color(0.86f, 0.70f, 0.36f);
                Point(field, SpotOf(m.Name), Loc.Name(m.Name), note, colour, can, () => ShowCard(m));
            }

            // Кто ищет Греховода сам — известно по слуху, принесённому в склеп; места у него нет —
            // он там, куда пойдёте вы. Строка внизу карты, а не под ней: под картой её не видно.
            var hunters = Survivors.Hunters();
            if (hunters.Count > 0)
            {
                var names = new List<string>();
                foreach (var hunter in hunters) names.Add(Survivors.Shown(hunter));
                var line = Caption(rt, Loc.F("Ищет Греховода: {0}. Куда пойдёте вы, туда придёт и он.",
                    string.Join(", ", names)), 19, -(height - 34f), true);
                var lt = (RectTransform)line.transform;
                lt.offsetMin = new Vector2(18f, lt.offsetMin.y);
                lt.offsetMax = new Vector2(-18f, lt.offsetMax.y);
                line.color = new Color(0.85f, 0.32f, 0.25f);
            }
            return height;
        }

        /// <summary>Дорога между двумя точками карты — тонкая полоса под углом.</summary>
        private static void Line(RectTransform field, Vector2 a, Vector2 b, float w, float h, Color colour)
        {
            var go = new GameObject("Дорога", typeof(RectTransform));
            go.transform.SetParent(field, false);
            var rt = (RectTransform)go.transform;
            Vector2 pa = new Vector2(a.x * w, a.y * h), pb = new Vector2(b.x * w, b.y * h);
            Vector2 mid = (pa + pb) * 0.5f, d = pb - pa;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(d.magnitude, 4f);
            rt.anchoredPosition = mid;
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            var img = go.AddComponent<Image>();
            img.color = colour;
            img.raycastTarget = false;
        }

        /// <summary>Точка карты: ромб, имя под ним и строка о том, что известно. Нажатие — карточка.</summary>
        private void Point(RectTransform field, Vector2 at, string title, string note, Color colour, bool bright, System.Action onClick)
        {
            var go = new GameObject(title, typeof(RectTransform));
            go.transform.SetParent(field, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = at;
            rt.pivot = new Vector2(0.5f, 0.75f);
            rt.sizeDelta = new Vector2(240f, 96f);
            rt.anchoredPosition = Vector2.zero;
            // Прозрачная плашка ловит нажатие по всей подписи, а не только по ромбу.
            var hit = go.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0.001f);
            if (onClick != null && bright)
            {
                var button = go.AddComponent<Button>();
                button.targetGraphic = hit;
                var tint = button.colors; tint.highlightedColor = new Color(1f, 1f, 1f, 0.12f); button.colors = tint;
                button.onClick.AddListener(() => onClick());
            }

            var mark = new GameObject("Ромб", typeof(RectTransform));
            mark.transform.SetParent(rt, false);
            var mt = (RectTransform)mark.transform;
            mt.anchorMin = mt.anchorMax = new Vector2(0.5f, 0.75f);
            mt.sizeDelta = new Vector2(22f, 22f);
            mt.anchoredPosition = Vector2.zero;
            mt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var img = mark.AddComponent<Image>();
            img.color = bright ? colour : colour * 0.5f;
            img.raycastTarget = false;

            var name = Caption(rt, title, 22, -42f, bright);
            name.alignment = TextAnchor.UpperCenter;
            if (!string.IsNullOrEmpty(note))
            {
                var line = Caption(rt, note, 16, -70f, bright);
                line.alignment = TextAnchor.UpperCenter;
                line.color = new Color(colour.r, colour.g, colour.b, bright ? 1f : 0.5f);
            }
        }

        private Text Caption(RectTransform parent, string text, int size, float top, bool bright)
        {
            var go = new GameObject("Подпись", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, size + 8f);
            rt.anchoredPosition = new Vector2(0f, top);
            var label = go.AddComponent<Text>();
            label.font = _font; label.fontSize = size; label.text = text; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.color = bright ? new Color(0.91f, 0.89f, 0.85f) : new Color(0.55f, 0.52f, 0.48f);
            return label;
        }

        /// <summary>Плашка с заголовком и текстом любой длины. Вернёт высоту.</summary>
        private float TextBlock(float y, string title, string body)
        {
            var go = new GameObject(title, typeof(RectTransform));
            go.transform.SetParent(_rows, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = new Vector2(0f, y);
            go.AddComponent<Image>().color = new Color(0.10f, 0.09f, 0.08f, 0.92f);
            _spawned.Add(go);
            Label(rt, title, 26, new Vector2(18f, -10f), 34f, true);

            // Высота текста — по ширине плашки: строки переносятся, плашка растёт под них.
            float width = Mathf.Max(200f, (_rows != null ? _rows.rect.width : 900f) - 36f);
            var probe = new GameObject("Текст", typeof(RectTransform));
            probe.transform.SetParent(rt, false);
            var pt = (RectTransform)probe.transform;
            pt.anchorMin = new Vector2(0f, 1f); pt.anchorMax = new Vector2(1f, 1f); pt.pivot = new Vector2(0f, 1f);
            pt.offsetMin = new Vector2(18f, 0f); pt.offsetMax = new Vector2(-18f, 0f);
            var text = probe.AddComponent<Text>();
            text.font = _font; text.fontSize = 21; text.text = body; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = new Color(0.86f, 0.84f, 0.80f);
            text.lineSpacing = 1.1f;
            float textHeight = text.cachedTextGeneratorForLayout.GetPreferredHeight(body,
                text.GetGenerationSettings(new Vector2(width, 0f))) / Mathf.Max(0.01f, text.pixelsPerUnit);
            pt.sizeDelta = new Vector2(pt.sizeDelta.x, textHeight + 4f);
            pt.anchoredPosition = new Vector2(18f, -50f);

            float height = 50f + textHeight + 20f;
            rt.sizeDelta = new Vector2(0f, height);
            Grow(y, height);
            return height;
        }

        /// <summary>Ряд кнопок поровну по ширине: «Выйти» и «Продолжить».</summary>
        private void Buttons(float y, params (string label, bool enabled, System.Action onClick)[] buttons)
        {
            const float height = 76f;
            var go = new GameObject("Кнопки", typeof(RectTransform));
            go.transform.SetParent(_rows, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = new Vector2(0f, y);
            Grow(y, height);
            _spawned.Add(go);
            for (int i = 0; i < buttons.Length; i++)
            {
                var (label, enabled, onClick) = buttons[i];
                var b = new GameObject(label, typeof(RectTransform));
                b.transform.SetParent(rt, false);
                var bt = (RectTransform)b.transform;
                bt.anchorMin = new Vector2((float)i / buttons.Length, 0f);
                bt.anchorMax = new Vector2((float)(i + 1) / buttons.Length, 1f);
                bt.offsetMin = new Vector2(i == 0 ? 0f : 6f, 0f);
                bt.offsetMax = new Vector2(i == buttons.Length - 1 ? 0f : -6f, 0f);
                var plate = b.AddComponent<Image>();
                bool primary = i == buttons.Length - 1;
                plate.color = !enabled ? new Color(0.07f, 0.06f, 0.06f, 0.80f)
                    : primary ? new Color(0.30f, 0.22f, 0.10f, 0.95f) : new Color(0.12f, 0.11f, 0.10f, 0.95f);
                if (enabled && onClick != null)
                {
                    var button = b.AddComponent<Button>();
                    button.targetGraphic = plate;
                    var act = onClick;
                    button.onClick.AddListener(() => act());
                }
                var text = Caption(bt, label, 28, -20f, enabled);
                text.alignment = TextAnchor.UpperCenter;
            }
        }

        /// <summary>Прокрутка должна знать настоящую высоту содержимого, иначе низ не достать.</summary>
        private void Grow(float y, float height)
        {
            if (_missionScroll == null || _rows == null) return;
            float required = -y + height + 12f;
            if (required > _rows.rect.height)
                _rows.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, required);
        }

        private void Label(RectTransform parent, string text, int size,
            Vector2 offset, float height, bool bright)
        {
            var go = new GameObject("Строка", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(offset.x, 0f);
            rt.offsetMax = new Vector2(-18f, 0f);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
            rt.anchoredPosition = new Vector2(offset.x, offset.y);

            var label = go.AddComponent<Text>();
            label.font = _font;
            label.fontSize = size;
            label.alignment = TextAnchor.UpperLeft;
            label.color = bright
                ? new Color(0.91f, 0.89f, 0.85f)
                : new Color(0.55f, 0.52f, 0.48f);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.text = text;
        }
    }
}
