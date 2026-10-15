// Assets/Scripts/UI/SurroundedMarkUI.cs
// Перевод: текст через Loc
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.UI
{
    /// <summary>
    /// Знак «окружён» слева от полосы здоровья (решение автора, 15 октября:
    /// «Окружён — да»; docs/08-FLOOR.md §8).
    ///
    /// Окружение (<see cref="Engagement"/>) с 28 августа делало удары по
    /// окружённому больнее за каждого лишнего врага рядом, а игрок узнавал
    /// о нём только из причины отказа: «его обступили». Свой окружён —
    /// выводить или выручать; враг окружён — бить, пока стоит.
    ///
    /// Знак — кольцо со стрелками внутрь. Новый знак без объяснения — шум
    /// (docs/37-DEMO.md §0: «слово над головой — только с причиной»), и первый
    /// раз за игру журнал говорит, что он значит.
    ///
    /// Окружение мерится каждый кадр по расстоянию и на краю дистанции
    /// удара мигало бы: знак встаёт, когда окружение продержалось миг,
    /// и уходит не сразу.
    /// </summary>
    public class SurroundedMarkUI : MonoBehaviour
    {
        /// <summary>Сколько окружение должно продержаться, чтобы знак встал, секунд.</summary>
        private const float Settle = 0.3f;

        /// <summary>Сколько знак держится после того, как окружение кончилось.</summary>
        private const float Linger = 0.6f;

        /// <summary>Цвет углей — тревога, но не сторона: кольца сторон — зелёное и красное.</summary>
        private static readonly Color Ember = new(0.96f, 0.58f, 0.22f);

        private static Sprite _sprite;
        private static bool _told;

        private Image _image;
        private Engagement _engagement;
        private Warrior _warrior;
        private float _on, _off;
        private bool _shown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => _told = false;

        /// <summary>Новая игра: знак объясняется снова.</summary>
        public static void Forget() => _told = false;

        /// <summary>Чей знак. Зовётся при сборке и при выдаче из пула.</summary>
        public void Init(GameObject warrior)
        {
            _engagement = warrior != null ? warrior.GetComponent<Engagement>() : null;
            _warrior = warrior != null ? warrior.GetComponent<Warrior>() : null;
            _on = _off = 0f;
            Show(false);
        }

        void Update()
        {
            if (_image == null) return;

            bool surrounded = _engagement != null && _warrior != null && !_warrior.IsDead
                           && _engagement.IsSurrounded;

            // Игровое время: на паузе окружение не меняется, и знак тоже.
            if (surrounded) { _on += Time.deltaTime; _off = 0f; }
            else { _off += Time.deltaTime; _on = 0f; }

            if (!_shown && surrounded && _on >= Settle) { Show(true); Explain(); }
            else if (_shown && !surrounded && _off >= Linger) Show(false);

            if (_shown)
            {
                // Дышит: замечается краем глаза среди десятка полос.
                float k = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 6f);
                _image.rectTransform.localScale = new Vector3(k, k, 1f);
            }
        }

        private void Show(bool on)
        {
            _shown = on;
            if (_image != null) _image.enabled = on;
        }

        /// <summary>
        /// Первый раз за игру — что значит знак и почему это важно. Только если
        /// воина видно: знак над врагом в тумане объяснять незачем — его не видят.
        /// </summary>
        private void Explain()
        {
            if (_told || _warrior == null || FogOfWar.Hides(_warrior)) return;
            _told = true;

            string name = Loc.Name(_warrior.DisplayName);
            Object.FindFirstObjectByType<BattleLogUI>()?.Write(Grammar.Pick(_warrior.Gender,
                Loc.F("{0} окружён — у полосы здоровья встал знак. Когда рядом двое врагов и больше, "
                    + "каждый лишний делает удары по нему больнее.", name),
                Loc.F("{0} окружена — у полосы здоровья встал знак. Когда рядом двое врагов и больше, "
                    + "каждый лишний делает удары по ней больнее.", name)));
        }

        /// <summary>
        /// Собрать знак слева от полосы здоровья. Зовёт <see cref="OverheadBuilder"/>.
        /// </summary>
        public static SurroundedMarkUI Attach(Transform overhead)
        {
            var go = new GameObject("Окружён", typeof(RectTransform));
            go.transform.SetParent(overhead, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(30f, 30f);
            rect.anchoredPosition = new Vector2(-4f, 3f);

            var image = go.AddComponent<Image>();
            image.sprite = Glyph();
            image.color = Ember;
            image.raycastTarget = false;
            image.enabled = false;

            var mark = go.AddComponent<SurroundedMarkUI>();
            mark._image = image;
            return mark;
        }

        /// <summary>
        /// Кольцо и три стрелки к середине — «со всех сторон». Рисуется кодом
        /// один раз: файла под один значок не нужно, и выглядит он одинаково
        /// при каждом запуске.
        /// </summary>
        private static Sprite Glyph()
        {
            if (_sprite != null) return _sprite;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "Знак окружения",   // ключ: имя текстуры, игрок его не видит
            };

            var pixels = new Color32[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);

                // Кольцо: полоса между 0,70 и 0,90 радиуса, края мягкие.
                float ring = Band(r, 0.70f, 0.90f);

                // Стрелки: клинья от кольца к середине, сужаются внутрь.
                float wedge = 0f;
                for (int i = 0; i < 3; i++)
                {
                    float dir = Mathf.PI * 0.5f + i * Mathf.PI * 2f / 3f;
                    float d = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, dir * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                    float width = Mathf.InverseLerp(0.30f, 0.72f, r) * 0.42f;
                    if (r >= 0.30f && r <= 0.72f)
                        wedge = Mathf.Max(wedge, Mathf.Clamp01((width - d) * 18f));
                }

                byte a = (byte)(Mathf.Clamp01(Mathf.Max(ring, wedge)) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            _sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _sprite;
        }

        /// <summary>Мягкая полоса: единица внутри [from, to], сходит на нет на краях.</summary>
        private static float Band(float r, float from, float to)
        {
            const float soft = 0.04f;
            return Mathf.Clamp01((r - from) / soft + 1f) * Mathf.Clamp01((to - r) / soft + 1f)
                 * (r >= from - soft && r <= to + soft ? 1f : 0f);
        }
    }
}
