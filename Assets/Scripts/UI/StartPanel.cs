using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.Core;

namespace Sinbinder.UI
{
    /// <summary>
    /// Дверь в игру: каким режимом играть и продолжать ли прошлое.
    ///
    /// <b>Зачем.</b> Режим обязательств — половина замысла: если после
    /// отказа воина можно перезагрузиться, отказ не стоит ничего.
    /// Но выбрать его было нечем — он стоял галкой в инспекторе, то есть
    /// существовал для автора и не существовал для игрока. Механика,
    /// которую нельзя включить, всё равно что ненаписанная.
    ///
    /// <b>Спрашиваем один раз за запуск.</b> Демо идёт через три сцены,
    /// и вопрос, заданный в каждой, превратил бы уговор в формальность.
    /// Признак статический: он и обязан пережить смену сцен.
    ///
    /// Дверь открывается до слогана доли 1, а не после: выбор режима —
    /// это ещё не игра, а условие, на котором в неё садятся. Порядок
    /// меняется переносом компонента, если автор решит иначе.
    /// </summary>
    public class StartPanel : MonoBehaviour
    {
        /// <summary>Спрашивали ли уже в этом запуске.</summary>
        private static bool _asked;

        [SerializeField] private GameObject _panel;
        [SerializeField] private RectTransform _rows;
        [SerializeField] private Text _title;
        [SerializeField] private Font _font;

        private readonly List<GameObject> _spawned = new();

        /// <summary>Спросить заново. Для меню и проверок.</summary>
        public static void Forget() => _asked = false;

        void Start()
        {
            if (_asked) { Hide(); return; }

            _asked = true;

            if (_panel != null) _panel.SetActive(true);
            GamePauseController.Instance?.Pause();
            Draw();
        }

        private void Hide()
        {
            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear();

            if (_panel != null) _panel.SetActive(false);
        }

        private void Begin(bool commitment)
        {
            var mode = Object.FindFirstObjectByType<GameModeSettings>();

            // Через настройку, а не через правило напрямую: выбор обязан
            // пережить запуск, а помнит его она.
            if (mode != null) mode.Choose(commitment);
            else Commitment.Set(commitment);

            Hide();
            GamePauseController.Instance?.Resume();
        }

        private void Continue(string slot)
        {
            var save = SaveSystem.Read(SaveSystem.PathOf(slot));

            // Режим лежит в самом файле, поэтому его здесь не спрашивают:
            // снимок ответственной игры не имеет права открыться свободной.
            if (!SaveSystem.Restore(save))
            {
                Say("Эта запись не от нынешней игры.");
                return;
            }

            Hide();
            GamePauseController.Instance?.Resume();
        }

        private void Draw()
        {
            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear();

            if (_title != null)
                _title.text = "Греху всё равно, чьё это тело.";

            float y = 0f;

            // Сначала продолжить: игрок, у которого есть незаконченная
            // партия, пришёл за ней, а не выбирать режим заново.
            foreach (var slot in new[] { SaveSystem.Bound, SaveSystem.Quick })
            {
                var save = SaveSystem.Read(SaveSystem.PathOf(slot));
                if (save == null) continue;

                var where = slot;
                string what = string.IsNullOrEmpty(save.Label) ? "Запись" : save.Label;
                string how = save.Commitment
                    ? "с обязательством — переиграть нельзя"
                    : "свободно";

                _spawned.Add(Row(y, "Продолжить", $"{what} · {how}",
                                 () => Continue(where)));
                y -= RowHeight;
            }

            if (y < 0f) y -= RowHeight * 0.4f;

            _spawned.Add(Row(y, "Начать заново",
                Commitment.Describe(false), () => Begin(false)));
            y -= RowHeight;

            _spawned.Add(Row(y, "Начать заново, взяв обязательство",
                Commitment.Describe(true), () => Begin(true)));
            y -= RowHeight * 1.3f;

            // Объяснение, а не предупреждение. Режим с обязательством —
            // не «сложнее», а тот, в котором игра работает как задумана,
            // и сказать это надо до выбора, а не после.
            _spawned.Add(Row(y, "",
                "Воины здесь спорят и отказываются. Если отказ можно "
              + "переиграть, он ничего не стоит — и весь спор становится "
              + "помехой, которую обходят клавишей.", null));
        }

        private static void Say(string line)
            => Object.FindFirstObjectByType<BattleLogUI>()?.Write(line);

        private const float RowHeight = 92f;

        private GameObject Row(float y, string title, string second, System.Action onClick)
        {
            var go = new GameObject(string.IsNullOrEmpty(title) ? "Пояснение" : title,
                                    typeof(RectTransform));
            go.transform.SetParent(_rows, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, RowHeight - 8f);
            rt.anchoredPosition = new Vector2(0f, y);

            var plate = go.AddComponent<Image>();
            plate.color = new Color(0.10f, 0.09f, 0.08f, onClick == null ? 0f : 0.88f);

            if (onClick != null)
            {
                var button = go.AddComponent<Button>();
                button.targetGraphic = plate;
                button.onClick.AddListener(() => onClick());
            }

            if (!string.IsNullOrEmpty(title)) Label(rt, title, 30, -10f, true);
            Label(rt, second, 21, string.IsNullOrEmpty(title) ? -8f : -48f, false);

            return go;
        }

        private void Label(RectTransform parent, string text, int size, float y, bool bright)
        {
            var go = new GameObject("Строка", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(22f, 0f);
            rt.offsetMax = new Vector2(-22f, 0f);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, 72f);
            rt.anchoredPosition = new Vector2(22f, y);

            var label = go.AddComponent<Text>();
            label.font = _font;
            label.fontSize = size;
            label.alignment = TextAnchor.UpperLeft;
            label.color = bright
                ? new Color(0.93f, 0.91f, 0.87f)
                : new Color(0.62f, 0.59f, 0.55f);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.text = text;
        }
    }
}
