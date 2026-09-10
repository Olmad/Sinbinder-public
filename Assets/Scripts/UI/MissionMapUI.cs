// Assets/Scripts/UI/MissionMapUI.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.AOS;
using Sinbinder.Crypt;
using Sinbinder.Gameplay;

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

        private CrystalBall _ball;
        private MissionBoard _board;

        private bool _pickingCommander;
        private Mission _chosen;

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

            if (_ball == null)
                Debug.LogWarning("[КАРТА] Шара в сцене нет: открывать нечем.");
            if (_board == null)
                Debug.LogError("[КАРТА] Доски вылазок нет: отправлять некому.");
        }

        void Update()
        {
            if (_panel == null) return;

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
            if (_pickingCommander) { ShowMissions(); return; }

            Close();
        }

        public void Open()
        {
            if (_panel == null || _board == null) return;

            _pickingCommander = false;
            ShowMissions();

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

            if (_title != null)
                _title.text = string.IsNullOrEmpty(_board.LastReport)
                    ? "Куда пойти.  Esc — отойти от шара"
                    : _board.LastReport;

            Rows(out float y);

            foreach (var mission in _board.Missions)
            {
                bool enough = _board.EnoughPeople(mission);
                var m = mission;

                _spawned.Add(Row(y, enough,
                    m.Name,
                    m.Rumour,
                    enough
                        ? $"{MissionCatalog.Danger(m)} {MissionCatalog.Riches(m.Prize)} "
                          + MissionCatalog.Promise(m.Spoils)
                        : "Столько людей не наберётся.",
                    enough ? () => PickCommander(m) : (System.Action)null));

                y -= 150f;
            }
        }

        private void PickCommander(Mission mission)
        {
            _chosen = mission;
            _pickingCommander = true;

            if (_title != null)
                _title.text = $"{mission.Name}. Кого поставить старшим.  Esc — назад";

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

                _spawned.Add(Row(y, can,
                    name,
                    can ? Leadership.Describe(member.Leadership) : why,
                    prophecy,
                    can ? () => Lead(name) : (System.Action)null));

                y -= 150f;
            }
        }

        /// <summary>
        /// Старший выбран. Если на точке есть развилка — спрашиваем,
        /// что предложить; если нет — уходят молча.
        /// </summary>
        private void Lead(string commanderName)
        {
            if (_chosen.Junction == Junction.None) { Send(commanderName, null); return; }
            PickOffer(commanderName);
        }

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
                            + "  Что вы им скажете?  Esc — к карте";

            Rows(out float y);

            foreach (var option in JunctionCatalog.Options(_chosen.Junction))
            {
                var offer = option;
                _spawned.Add(Row(y, true,
                    JunctionCatalog.Offer(offer),
                    "Это не приказ. Решать будет старший.",
                    "",
                    () => Send(commanderName, offer)));

                y -= 150f;
            }

            // Промолчать — тоже ход: тогда за отряд не говорит никто,
            // и видно, чего он хочет сам.
            _spawned.Add(Row(y, true,
                "Промолчать.",
                "Пусть решают сами.",
                "",
                () => Send(commanderName, null)));
        }

        private void Send(string commanderName, MissionAction? offer)
        {
            _board.Send(_chosen, commanderName, offer);

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
            string third, System.Action onClick)
        {
            var go = new GameObject(title, typeof(RectTransform));
            go.transform.SetParent(_rows, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 140f);
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
