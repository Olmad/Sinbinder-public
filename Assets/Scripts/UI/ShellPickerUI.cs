// Assets/Scripts/UI/ShellPickerUI.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.UI
{
    /// <summary>
    /// Во что связать собранную душу.
    ///
    /// Четыре оболочки собраны как ассеты, смещение спектров у каждой
    /// настоящее, необратимый дрейф работает, панель предсказания
    /// написана, — и всем этим пользовались на одну четверть:
    /// <c>SoulBinding</c> держал зашитый <c>ShellType.Zombie</c>.
    /// Экран открывает кран на уже проложенной трубе.
    ///
    /// Здесь же выбор оболочки становится следствием того, насколько
    /// игрок торопился: правило <see cref="ShellChoice"/> не пускает
    /// истлевшую душу в тяжёлое тело. Урок сцены 4 пролога — «спеши» —
    /// перестаёт быть словом рассказчика.
    ///
    /// <b>Ни одной цифры.</b> Тело описывает себя словами
    /// (<c>ShellData.DescribeBias</c>), отказ объясняется словами
    /// (<c>ShellChoice.Refusal</c>), а чем воин встанет — доминирующим
    /// грехом после связывания, посчитанным настоящим
    /// <c>ShellBinder</c> на копии души.
    ///
    /// Компонент вешается <b>на Canvas</b>, а не на панель, которую сам
    /// выключает: выключенный объект не крутит Update. На этом уже
    /// спотыкался совет командиров.
    /// </summary>
    public class ShellPickerUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Text _title;
        [SerializeField] private RectTransform _rows;
        [SerializeField] private Font _font;

        private readonly List<GameObject> _spawned = new();
        private System.Action<ShellType> _then;

        /// <summary>Открыт ли сейчас — <c>SoulBinding</c> не должен звать дважды.</summary>
        public bool IsOpen => _panel != null && _panel.activeSelf;

        void Start()
        {
            if (_panel == gameObject)
                Debug.LogError("[ОБОЛОЧКИ] Компонент висит на панели, которую сам "
                             + "выключает. Перевесить его на Canvas.");

            if (_panel != null) _panel.SetActive(false);
        }

        /// <summary>
        /// Показать выбор. <paramref name="then"/> зовётся с выбранным типом;
        /// если открыть не удалось, возвращает false — и связывание идёт
        /// своим прежним путём, а не пропадает.
        /// </summary>
        public bool Open(SoulData soul, SoulQuality quality, System.Action<ShellType> then)
        {
            if (_panel == null || _rows == null || soul == null) return false;
            if (_panel.activeSelf) return false;

            var shells = new List<ShellData>();
            foreach (ShellType type in System.Enum.GetValues(typeof(ShellType)))
            {
                var data = ShellLibrary.Get(type);
                if (data != null) shells.Add(data);
            }

            // Ассетов нет — экран не открываем. Пустая панель на паузе
            // заперла бы игру насмерть, а связывание умеет работать
            // и без выбора.
            if (shells.Count == 0)
            {
                Debug.LogWarning("[ОБОЛОЧКИ] Ни одного ассета тела: экран выбора "
                               + "пропущен, связываем как раньше.");
                return false;
            }

            _then = then;

            if (_title != null)
                _title.text = $"{soul.Name}. {Left(quality)}";

            Build(soul, quality, shells);

            _panel.SetActive(true);
            GamePauseController.Instance?.Pause();
            return true;
        }

        /// <summary>Сколько от души осталось — словами, без шкалы.</summary>
        private static string Left(SoulQuality quality)
        {
            switch (quality)
            {
                case SoulQuality.Shock:      return "Она ещё вся здесь.";
                case SoulQuality.Acceptance: return "Крайности уже сгладились.";
                case SoulQuality.Fading:     return "Характер тускнеет.";
                default:                     return "От неё осталась одна воля.";
            }
        }

        private void Build(SoulData soul, SoulQuality quality, List<ShellData> shells)
        {
            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear();

            float y = 0f;
            foreach (var shell in shells)
            {
                _spawned.Add(Row(soul, quality, shell, y));
                y -= 158f;
            }
        }

        private GameObject Row(SoulData soul, SoulQuality quality, ShellData shell, float y)
        {
            bool allowed = ShellChoice.Allows(shell, quality);

            var go = new GameObject(shell.shellName, typeof(RectTransform));
            go.transform.SetParent(_rows, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 148f);
            rt.anchoredPosition = new Vector2(0f, y);

            var plate = go.AddComponent<Image>();
            plate.color = allowed
                ? new Color(0.10f, 0.09f, 0.08f, 0.92f)
                : new Color(0.07f, 0.06f, 0.06f, 0.80f);

            // Кнопка только на то, что примет. Строка, которая не сработает,
            // не должна и нажиматься.
            if (allowed)
            {
                var type = shell.type;
                var button = go.AddComponent<Button>();
                button.targetGraphic = plate;
                button.onClick.AddListener(() => Choose(type));
            }

            Label(rt, shell.shellName, 30, new Vector2(18f, -10f), 40f, allowed);

            Label(rt, allowed ? shell.DescribeBias() : ShellChoice.Refusal(shell, quality),
                20, new Vector2(18f, -48f), 52f, allowed);

            if (allowed)
                Label(rt, Becomes(soul, shell), 21, new Vector2(18f, -104f), 34f, true);

            return go;
        }

        /// <summary>
        /// Кем он встанет. Считает не эта панель, а настоящий
        /// <see cref="ShellBinder"/> на копии души: экран, у которого своя
        /// формула, разойдётся с игрой при первой же правке смещений.
        /// </summary>
        private static string Becomes(SoulData soul, ShellData shell)
        {
            var after = ShellChoice.Preview(soul, shell);
            if (after == null) return "";

            string now = SoulData.GetSinName(after.Sin);

            return after.Sin == soul.Sin
                ? $"Встанет прежним: {now}."
                : $"Встанет другим: {now}. Тело перетянуло.";
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

        private void Choose(ShellType type)
        {
            if (_panel != null) _panel.SetActive(false);
            GamePauseController.Instance?.Resume();

            var then = _then;
            _then = null;
            then?.Invoke(type);
        }
    }
}
