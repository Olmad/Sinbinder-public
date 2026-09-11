using UnityEngine;
using UnityEngine.UI;

namespace Sinbinder.UI
{
    /// <summary>
    /// Строка меню: плашка, заголовок, пояснение под ним, нажатие.
    ///
    /// Заведено потому, что этот код был написан четыре раза подряд —
    /// прозрачность, сохранения, начало, пауза, — и все четыре раза
    /// почти одинаково. «Почти» и есть беда: панели уже начали расходиться
    /// отступами и цветом, и через месяц они выглядели бы как четыре
    /// разных игры.
    ///
    /// Нерабочая строка не получает кнопки вовсе. Строка, которая
    /// нажимается и ничего не делает, читается как поломка.
    /// </summary>
    public static class MenuRows
    {
        public const float Height = 54f;

        public static GameObject Row(RectTransform parent, Font font, float y,
            string title, string second, System.Action onClick)
        {
            var go = new GameObject(string.IsNullOrEmpty(title) ? "Строка" : title,
                                    typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, (string.IsNullOrEmpty(second)
                                            ? Height : Height * 1.4f) - 6f);
            rt.anchoredPosition = new Vector2(0f, y);

            var plate = go.AddComponent<Image>();
            plate.color = new Color(0.10f, 0.09f, 0.08f, onClick == null ? 0.35f : 0.88f);

            if (onClick != null)
            {
                var button = go.AddComponent<Button>();
                button.targetGraphic = plate;
                button.onClick.AddListener(() => onClick());
            }

            Label(rt, font, title, 26, -8f, onClick != null);
            if (!string.IsNullOrEmpty(second)) Label(rt, font, second, 20, -40f, false);

            return go;
        }

        private static void Label(RectTransform parent, Font font, string text,
            int size, float y, bool bright)
        {
            if (string.IsNullOrEmpty(text)) return;

            var go = new GameObject("Текст", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(18f, 0f);
            rt.offsetMax = new Vector2(-18f, 0f);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, 34f);
            rt.anchoredPosition = new Vector2(18f, y);

            var label = go.AddComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.alignment = TextAnchor.UpperLeft;
            label.color = bright
                ? new Color(0.91f, 0.89f, 0.85f)
                : new Color(0.62f, 0.59f, 0.55f);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.text = text;
        }
    }
}
