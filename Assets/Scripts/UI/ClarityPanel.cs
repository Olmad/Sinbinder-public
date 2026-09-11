using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.Core;

namespace Sinbinder.UI
{
    /// <summary>
    /// Что игре позволено объяснять. Открывается на O.
    ///
    /// Сверху — готовые наборы, одним нажатием. Снизу — галочки:
    /// кому-то довольно слова над головой, кому-то нужна причина,
    /// а кому-то мешает и журнал. Лестница остаётся лестницей, но
    /// перестаёт быть единственным способом.
    ///
    /// <b>Трассировки здесь нет</b>, пока замок закрыт, — и это не
    /// «галочка выключена по умолчанию», а строка, которой не существует.
    /// Выключенное однажды включают по ошибке; отсутствующее — нет.
    /// Правило живёт в <see cref="Transparency"/>, панель его только
    /// показывает: настройка, знающая правило отдельно, разойдётся
    /// с ним на первой правке.
    ///
    /// Ни одной цифры, включая настройки: «уровень 3» — это цифра.
    /// </summary>
    public class ClarityPanel : MonoBehaviour
    {
        [SerializeField] private KeyCode _key = KeyCode.O;
        [SerializeField] private GameObject _panel;
        [SerializeField] private RectTransform _rows;
        [SerializeField] private Text _title;
        [SerializeField] private Font _font;

        private readonly List<GameObject> _spawned = new();
        private bool _open;

        void Start()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        void Update()
        {
            if (Input.GetKeyDown(_key)) Toggle();
            else if (_open && Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        private void Toggle()
        {
            if (_open) Close(); else Open();
        }

        private void Open()
        {
            _open = true;
            if (_panel != null) _panel.SetActive(true);
            GamePauseController.Instance?.Pause();
            Draw();
        }

        private void Close()
        {
            _open = false;
            if (_panel != null) _panel.SetActive(false);
            GamePauseController.Instance?.Resume();
        }

        private void Draw()
        {
            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear();

            if (_title != null)
                _title.text = "Что игра объясняет.  O или Esc — закрыть";

            float y = 0f;

            // Готовые наборы. Выше потолка не показываем вовсе:
            // строка, которая при нажатии зажмётся обратно, — это
            // интерфейс, который спорит с собой.
            foreach (Clarity step in new[] { Clarity.Silent, Clarity.Icons,
                                             Clarity.Tooltips, Clarity.Log,
                                             Clarity.Trace })
            {
                if (step > Transparency.Ceiling) continue;

                var chosen = step;
                bool now = !Transparency.IsCustom && Transparency.Level == step;

                _spawned.Add(Row(y, now ? "▸" : " ", Transparency.Describe(step),
                    () => { Choose(chosen); Draw(); }));

                y -= RowHeight;
            }

            y -= RowHeight * 0.6f;

            // Свой набор — такая же строка, как ступени, а не состояние,
            // в которое сваливаешься. Пока он не собран, строка есть,
            // но не нажимается: место под него видно заранее.
            _spawned.Add(Row(y,
                Transparency.IsCustom ? "▸" : " ",
                Transparency.HasCustom ? "Свой набор" : "Свой набор — соберите галочками ниже",
                Transparency.HasCustom && !Transparency.IsCustom
                    ? () => { Back(); Draw(); }
                    : (System.Action)null));

            y -= RowHeight * 1.2f;

            foreach (var piece in Transparency.Pieces())
            {
                // Трассировки для игрока не существует. Clamp всё равно
                // срежет её, но показывать галочку, которая не сработает,
                // нельзя: игрок решит, что игра сломана.
                if (piece == Detail.Trace && !Transparency.DeveloperUnlocked) continue;

                var one = piece;
                bool on = Transparency.Shows(piece);

                _spawned.Add(Row(y, on ? "×" : " ", Transparency.Describe(piece),
                    () => { Tick(one, !on); Draw(); }));

                y -= RowHeight;
            }
        }

        /// <summary>
        /// Выбор идёт через настройку, а не через правило напрямую:
        /// иначе он проживёт до конца сцены и умрёт. Запоминает
        /// его TransparencySettings — он же и загружает при запуске.
        /// </summary>
        private static void Choose(Clarity level)
        {
            var settings = Object.FindFirstObjectByType<TransparencySettings>();

            if (settings != null) settings.Choose(level);
            else Transparency.Set(level);
        }

        private static void Back()
        {
            var settings = Object.FindFirstObjectByType<TransparencySettings>();

            if (settings != null) settings.UseCustom();
            else Transparency.UseCustom();
        }

        private static void Tick(Detail one, bool on)
        {
            var wanted = on ? Transparency.Shown | one : Transparency.Shown & ~one;
            var settings = Object.FindFirstObjectByType<TransparencySettings>();

            if (settings != null) settings.ChooseCustom(wanted);
            else Transparency.SetCustom(wanted);
        }

        private const float RowHeight = 54f;

        private GameObject Row(float y, string mark, string text, System.Action onClick)
        {
            var go = new GameObject(text, typeof(RectTransform));
            go.transform.SetParent(_rows, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, RowHeight - 6f);
            rt.anchoredPosition = new Vector2(0f, y);

            var plate = go.AddComponent<Image>();
            plate.color = new Color(0.10f, 0.09f, 0.08f, onClick == null ? 0f : 0.85f);

            if (onClick != null)
            {
                var button = go.AddComponent<Button>();
                button.targetGraphic = plate;
                button.onClick.AddListener(() => onClick());
            }

            var labelGo = new GameObject("Строка", typeof(RectTransform));
            labelGo.transform.SetParent(rt, false);

            var lrt = (RectTransform)labelGo.transform;
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(1f, 1f);
            lrt.offsetMin = new Vector2(18f, 0f);
            lrt.offsetMax = new Vector2(-18f, 0f);

            var label = labelGo.AddComponent<Text>();
            label.font = _font;
            label.fontSize = 26;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = onClick == null
                ? new Color(0.55f, 0.52f, 0.48f)
                : new Color(0.91f, 0.89f, 0.85f);
            label.raycastTarget = false;
            label.text = $"{mark}  {text}";

            return go;
        }
    }
}
