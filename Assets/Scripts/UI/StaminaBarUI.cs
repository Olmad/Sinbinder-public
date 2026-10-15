// Assets/Scripts/UI/StaminaBarUI.cs
// Перевод: текст через Loc
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.Gameplay;

namespace Sinbinder.UI
{
    /// <summary>
    /// Полоса сил под полосой здоровья (решение автора, 15 октября: «Полоса
    /// сил — да»; docs/08-FLOOR.md §8).
    ///
    /// Усталость (<see cref="Fatigue"/>) с 28 августа решала бой — уставший
    /// бьёт слабее, вплоть до половины удара, — а игрок узнавал о ней только
    /// из причины отказа: «силы на исходе». Механикой, которую не видно,
    /// нельзя играть: по ней можно только проиграть, не поняв почему.
    ///
    /// Без чисел — полоса, как у здоровья. Видна, пока силы не полны, и всегда
    /// у выделенного: десять полных полос над отдыхающим отрядом — шум.
    /// Черта на трёх десятых — порог измождения: ниже неё воин слабеет,
    /// и полоса тускнеет и мерцает.
    /// </summary>
    public class StaminaBarUI : MonoBehaviour
    {
        /// <summary>С какой доли сил полоса считается полной и прячется.</summary>
        private const float Full = 0.98f;

        /// <summary>За сколько полоса появляется и уходит, долей в секунду.</summary>
        private const float FadeSpeed = 2.5f;

        // Кость — свежие силы; пепел — измождение. Приглушённые, как у здоровья
        // (23-PROMPTS.md §2): полоса не должна светиться ярче воина.
        private static readonly Color Fresh = new(0.80f, 0.75f, 0.62f);
        private static readonly Color Spent = new(0.47f, 0.42f, 0.37f);

        private RectTransform _fill;
        private Image _fillImage;
        private CanvasGroup _group;
        private RectTransform _tick;

        private Fatigue _fatigue;
        private Warrior _warrior;
        private SelectionComponent _selection;
        private float _shown;

        /// <summary>Собранная полоса: заполнение по якорям, без Slider.</summary>
        public void Build(RectTransform fill, Image fillImage, CanvasGroup group, RectTransform tick)
        {
            _fill = fill;
            _fillImage = fillImage;
            _group = group;
            _tick = tick;
        }

        /// <summary>Чья полоса. Зовётся при сборке и при выдаче из пула.</summary>
        public void Init(GameObject warrior)
        {
            _fatigue = warrior != null ? warrior.GetComponent<Fatigue>() : null;
            _warrior = warrior != null ? warrior.GetComponent<Warrior>() : null;
            _selection = warrior != null ? warrior.GetComponent<SelectionComponent>() : null;
            _shown = 0f;
            if (_group != null) _group.alpha = 0f;

            // Черта — на пороге этого воина: порог у тела свой.
            if (_tick != null && _fatigue != null)
            {
                float x = Mathf.Clamp01(_fatigue.ExhaustedBelow);
                _tick.anchorMin = new Vector2(x, 0f);
                _tick.anchorMax = new Vector2(x, 1f);
            }
        }

        void Update()
        {
            if (_group == null || _fill == null) return;

            if (_fatigue == null || _warrior == null || _warrior.IsDead)
            {
                _group.alpha = 0f;
                return;
            }

            float ratio = _fatigue.Ratio;
            bool want = ratio < Full || (_selection != null && _selection.IsSelected);

            // Реальное время: на паузе полоса выделенного не застывает полупрозрачной.
            _shown = Mathf.MoveTowards(_shown, want ? 1f : 0f, FadeSpeed * Time.unscaledDeltaTime);
            _group.alpha = _shown;

            _fill.anchorMax = new Vector2(ratio, 1f);

            if (_fillImage == null) return;
            if (!_fatigue.IsExhausted) { _fillImage.color = Fresh; return; }

            // Ниже порога — пепел и медленное мерцание: «слабеет» видно краем глаза.
            float pulse = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f));
            var c = Spent;
            c.a = pulse;
            _fillImage.color = c;
        }

        /// <summary>
        /// Собрать полосу под полосой здоровья: тонкая, во всю ширину, с чертой
        /// порога измождения. Зовёт <see cref="OverheadBuilder"/>.
        /// </summary>
        public static StaminaBarUI Attach(Transform overhead)
        {
            var go = new GameObject("Полоса сил", typeof(RectTransform));
            go.transform.SetParent(overhead, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, 7f);
            rect.anchoredPosition = new Vector2(0f, -25f);

            var group = go.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            group.alpha = 0f;

            Part("Подложка", rect, Vector2.zero, Vector2.one, new Color(0.12f, 0.11f, 0.11f, 0.9f));
            var fill = Part("Силы", rect, Vector2.zero, Vector2.one, Fresh);

            // Черта порога: тонкая, тёмная, поверх заполнения. Где — скажет Init.
            var tick = Part("Порог", rect, new Vector2(0.3f, 0f), new Vector2(0.3f, 1f),
                            new Color(0.06f, 0.05f, 0.05f, 0.9f)).rectTransform;
            tick.sizeDelta = new Vector2(2f, 0f);

            var bar = go.AddComponent<StaminaBarUI>();
            bar.Build(fill.rectTransform, fill, group, tick);
            return bar;
        }

        private static Image Part(string name, RectTransform parent, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
