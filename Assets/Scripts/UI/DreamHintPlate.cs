// Assets/Scripts/UI/DreamHintPlate.cs
// Перевод: текст через Loc
using UnityEngine;
using UnityEngine.UI;

namespace Sinbinder.UI
{
    /// <summary>
    /// Строка сна — табличкой игры, а не серой коробкой отладки.
    ///
    /// До 16.11 сон писал свою подсказку через OnGUI: GUI.skin.box, шрифт Unity по умолчанию,
    /// внизу слева поверх полосы выделенного — при выбранном воине коробка лежала на его
    /// голосах. И на кадрах прогона её не было вовсе: снимок берёт холст, а OnGUI мимо него.
    /// Теперь — табличка как у окон (UiStyle, 46-UI §2.1) вверху по центру: там в сне ничего
    /// нет, а взгляд при новой строке идёт туда же, куда смотрит камера. Строка урока
    /// крупно, над ней мелко «Воспоминание»; упёрся в черту урока — строка вспыхивает.
    /// </summary>
    public class DreamHintPlate : MonoBehaviour
    {
        private static DreamHintPlate _one;

        private CanvasGroup _group;
        private RectTransform _back;
        private Text _title, _words;
        private string _shown;
        private bool _want;
        private float _nudge;

        private static readonly Color Ink = new(0.94f, 0.92f, 0.86f);
        private static readonly Color Warm = new(1f, 0.82f, 0.45f);
        private const float Width = 980f, Top = 34f, Pad = 30f;

        /// <summary>Показать строку; nudge 0..1 — насколько она сейчас вспыхивает.</summary>
        public static void Show(string line, float nudge)
        {
            if (string.IsNullOrEmpty(line)) { Hide(); return; }
            if (_one == null) Build();
            _one._want = true;
            _one._nudge = nudge;
            if (line != _one._shown) _one.Say(line);
        }

        public static void Hide()
        {
            if (_one != null) _one._want = false;
        }

        private static void Build()
        {
            var go = new GameObject("Строка сна", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 44;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UiStyle.Reference;
            scaler.matchWidthOrHeight = 0.5f;

            _one = go.AddComponent<DreamHintPlate>();
            _one._group = go.GetComponent<CanvasGroup>();
            _one._group.alpha = 0f;
            _one._group.blocksRaycasts = false;
            _one._group.interactable = false;

            var back = new GameObject("Табличка", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)back.transform;
            rt.SetParent(go.transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -Top);
            rt.sizeDelta = new Vector2(Width, 120f);
            var shade = back.GetComponent<Image>();
            UiStyle.Dress(shade, UiStyle.PlateWeight.Screen);
            shade.raycastTarget = false;
            _one._back = rt;

            _one._title = Label(rt, "Над строкой", UiStyle.Title, 21, TextAnchor.UpperCenter, new Color(0.80f, 0.70f, 0.48f));
            var tr = _one._title.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
            tr.anchoredPosition = new Vector2(0f, -12f);
            tr.sizeDelta = new Vector2(-2f * Pad, 28f);
            _one._title.text = Core.Loc.T("Воспоминание");

            _one._words = Label(rt, "Строка", UiStyle.Body, 28, TextAnchor.UpperCenter, Ink);
            var wr = _one._words.rectTransform;
            wr.anchorMin = new Vector2(0f, 1f); wr.anchorMax = new Vector2(1f, 1f); wr.pivot = new Vector2(0.5f, 1f);
            wr.anchoredPosition = new Vector2(0f, -42f);
            wr.sizeDelta = new Vector2(-2f * Pad, 80f);
        }

        private static Text Label(RectTransform parent, string name, Font font, int size, TextAnchor align, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.color = colour;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Новая строка: табличка по высоте текста (одна строка — низкая, три — выше).</summary>
        private void Say(string line)
        {
            _shown = line;
            _words.text = line;
            var settings = _words.GetGenerationSettings(new Vector2(Width - 2f * Pad, 0f));
            float h = _words.cachedTextGeneratorForLayout.GetPreferredHeight(line, settings) / _words.pixelsPerUnit;
            _words.rectTransform.sizeDelta = new Vector2(-2f * Pad, h + 4f);
            _back.sizeDelta = new Vector2(Width, 42f + h + 22f);
        }

        void Update()
        {
            float target = _want ? 1f : 0f;
            _group.alpha = Mathf.MoveTowards(_group.alpha, target, Time.unscaledDeltaTime * 5f);
            _words.color = Color.Lerp(Ink, Warm, _nudge);
        }

        void OnDestroy()
        {
            if (_one == this) _one = null;
        }
    }
}
