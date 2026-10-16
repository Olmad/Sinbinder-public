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

        private static MissionMapUI _shown;

        /// <summary>Открыта ли карта. Спрашивает <see cref="Modal.Any"/>.</summary>
        public static bool IsOpen => _shown != null && _shown._panel != null && _shown._panel.activeInHierarchy;

        private CrystalBall _ball;
        private MissionBoard _board;

        private bool _pickingCommander;
        private Mission _chosen;

        /// <summary>
        /// Шаг сбора отряда: старший выбран, игрок решает, кого он возьмёт.
        /// До 16 октября шага не было — спутников подбирал список по порядку
        /// (автор: «войны, отправляемые на них, фиксированные»).
        /// </summary>
        private bool _gathering;
        private string _leader;
        private readonly HashSet<string> _party = new();

        /// <summary>Отходил ли игрок от шара с прошлого раза.</summary>
        private bool _leftTheBall = true;

        void Start()
        {
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
            if (_pickingCommander) { ShowMissions(); return; }

            Close();
        }

        public void Open()
        {
            if (_panel == null || _board == null) return;

            // Склеп-песочница: состав — с тех, кто стоит в склепе. Поднятые
            // в мастерской иначе в нём не значились бы до смены сцены,
            // а плата лично меняет долг на живых, а не в составе.
            if (CryptMap.Opened) SquadRoster.Remember(MissionBoard.Squad());

            _pickingCommander = false;
            ShowMissions();

            _shown = this;
            _panel.SetActive(true);
            Core.GamePauseController.Instance?.Pause();
        }

        private void Close()
        {
            if (_panel != null) _panel.SetActive(false);
            Core.GamePauseController.Instance?.Resume();
        }

        private void ShowMissions()
        {
            _pickingCommander = false;
            _gathering = false;

            if (_title != null)
                _title.text = string.IsNullOrEmpty(_board.LastReport)
                    ? Loc.T("Куда пойти.  Esc — отойти от шара")
                    : _board.LastReport;

            Rows(out float y);

            foreach (var mission in _board.Missions)
            {
                bool enough = _board.EnoughPeople(mission);
                var m = mission;

                _spawned.Add(Row(y, enough,
                    Loc.Name(m.Name),
                    m.Rumour,
                    enough
                        ? $"{MissionCatalog.Danger(m)} {MissionCatalog.Riches(m.Prize)} "
                          + MissionCatalog.Promise(m.Spoils)
                        : Loc.T("Столько людей не наберётся."),
                    enough ? () => PickCommander(m) : (System.Action)null));

                y -= 150f;
            }
        }

        private void PickCommander(Mission mission)
        {
            _chosen = mission;
            _pickingCommander = true;
            _gathering = false;

            if (_title != null)
                _title.text = Loc.F("{0}. Кого поставить старшим.  Esc — назад", Loc.Name(mission.Name));

            Rows(out float y);

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
                    can ? Leadership.Describe(member.Leadership) : why,
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
        private void Lead(string commanderName)
        {
            _leader = commanderName;
            _gathering = true;
            _party.Clear();
            foreach (var name in SquadRoster.Suggest(commanderName, _chosen.Squad, keepExperienced: false))
                _party.Add(name);
            ShowParty(null);
        }

        /// <summary>Отряд набран. Есть развилка — спросить, что предложить; нет — уходят.</summary>
        private void Gathered()
        {
            if (_party.Count < _chosen.Squad - 1) return;
            _gathering = false;
            if (_chosen.Junction == Junction.None) { Send(_leader, null); return; }
            PickOffer(_leader);
        }

        private void Toggle(string name)
        {
            if (_party.Contains(name)) _party.Remove(name);
            else if (_party.Count < _chosen.Squad - 1) _party.Add(name);
            else { ShowParty(Loc.T("Отряд набран. Чтобы взять этого, сперва оставьте кого-нибудь в склепе.")); return; }
            ShowParty(null);
        }

        /// <summary>
        /// Кого старший возьмёт: строка на каждого, щелчок — идёт или
        /// остаётся. Последняя строка — отправить, когда набрано сколько нужно.
        /// </summary>
        private void ShowParty(string warning)
        {
            int missing = _chosen.Squad - 1 - _party.Count;
            string leader = Loc.Name(Shown(_leader));

            if (_title != null)
                _title.text = (missing > 0
                        ? Loc.F("{0}. Кого {1} возьмёт — нужно ещё: {2}.", Loc.Name(_chosen.Name), leader,
                                Leadership.Collective(missing))
                        : Loc.F("{0}. Кого {1} возьмёт.", Loc.Name(_chosen.Name), leader))
                    + Loc.T("  Esc — назад")
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
                bool can = SquadRoster.CanGo(m);
                bool goes = can && _party.Contains(m.Name);
                string name = m.Name;

                _spawned.Add(Row(y, goes, Loc.Name(m.ShownName), PartyLine(m, can, goes), "",
                    can ? () => Toggle(name) : (System.Action)null, height));
                y -= step;
            }

            _spawned.Add(Row(y, missing <= 0,
                missing <= 0 ? Loc.F("Отправить отряд — ведёт {0}", leader)
                             : Loc.F("Нужно ещё: {0}", Leadership.Collective(missing)),
                "", "", missing <= 0 ? Gathered : (System.Action)null, height));
        }

        /// <summary>Идёт ли и что о нём стоит знать, решая: грех словом, долг, братство.</summary>
        private static string PartyLine(SquadRoster.Member m, bool can, bool goes)
        {
            if (!can) return m.IsAway ? Loc.T("В пути с другим отрядом.") : Loc.T(m.Unavailable);

            var parts = new List<string> { goes ? Loc.T("идёт") : Loc.T("остаётся в склепе") };
            parts.Add(SoulData.GetSinName(m.Sin).ToLowerInvariant());
            if (m.UnpaidMissions > 0) parts.Add(Grammar.Pick(m.Gender, Loc.T("ему должны"), Loc.T("ей должны")));
            if (m.Brother) parts.Add(Loc.T("брат по оружию"));
            return string.Join(" · ", parts);
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

        private void Send(string commanderName, MissionAction? offer)
        {
            _board.Send(_chosen, commanderName, new List<string>(_party), offer);

            // Возвращаемся к карте: отчёт стоит в заголовке, и следующий
            // выбор игрок делает, уже зная цену прошлого.
            ShowMissions();
        }

        private static string Foretell(string name)
        {
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
                if (w != null && !w.IsDead && w.DisplayName == name)
                    return TemperamentPredictor.Describe(w);

            return "";
        }

        private void Rows(out float y)
        {
            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear();
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
