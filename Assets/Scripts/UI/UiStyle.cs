// Assets/Scripts/UI/UiStyle.cs
// Перевод: текст через Loc
using UnityEngine;
using UnityEngine.UI;

namespace Sinbinder.UI
{
    /// <summary>
    /// Облик макета лагеря (docs/42-INTERFACE.md §1, пп. 8–9) — одно место
    /// на все панели: шрифты, табличка окон и подсказок, капля огня души,
    /// овальная плашка.
    ///
    /// <b>Шрифты.</b> Книжная антиква: заглавные с засечками для названий
    /// и имён (Cormorant SC), простая читаемая для остального (PT Serif).
    /// Обе с кириллицей, обе под открытой лицензией OFL — файлы и лицензии
    /// в <c>Resources/Fonts</c>. До 1 октября весь интерфейс шёл встроенным
    /// Arial, и полоса по макету в игре читалась «как веб-приложение».
    /// Нет файла — встроенный шрифт: облик — украшение, игра обязана
    /// остаться игрой.
    ///
    /// <b>Фигуры</b> рисуются кодом — табличка, капля и овал, без картинок:
    /// их нечем испортить при импорте.
    /// </summary>
    public static class UiStyle
    {
        private static Font _body, _bodyBold, _title;
        private static Sprite _drop, _pill, _plate, _plateThin;

        /// <summary>
        /// Вес таблички. Экран, который читают (совет, плата, эпилог, пауза),
        /// — плотный, с метками в углах; мебель, всегда в кадре (журнал,
        /// полоса), — чуть прозрачнее; летучая подсказка — тонкий край
        /// без меток: рама ей была бы тяжела.
        /// </summary>
        public enum PlateWeight { Screen, Board, Strip }

        /// <summary>
        /// Табличка — одна на все панели (46-UI §2.1; макет, 42-INTERFACE §1,
        /// п. 8: «тёмное железо и кожа, текст цвета кости, тонкая окантовка
        /// старым золотом»). Снаружи — край тёмного железа, за ним линия
        /// старого золота, тень внутрь, кожа светлее сверху, метки в углах.
        /// Тянется по девяти частям на любой размер.
        ///
        /// До 15 октября панели были двух семей: тёмная заливка с волосяной
        /// рамкой цвета кости (сборщик сцен) и сливовая без рамки (разговор,
        /// сундук), а подсказки — вовсе без таблички (опись, 46-UI §1.1).
        /// </summary>
        public static Sprite Plate => _plate ??= MakePlate(96, 24, gold: 1.5f, corners: true);

        /// <summary>Тонкая табличка подсказок: край уже, меток нет.</summary>
        public static Sprite PlateThin => _plateThin ??= MakePlate(48, 12, gold: 1f, corners: false);

        /// <summary>
        /// Одеть картинку в табличку. Картинка, нарисованная кодом, в сцену
        /// не сохраняется — панели сборщика одевает <see cref="PlateLook"/>
        /// при запуске, панели кода — сами, этим же вызовом.
        /// </summary>
        public static void Dress(Image image, PlateWeight weight)
        {
            if (image == null) return;
            image.sprite = weight == PlateWeight.Strip ? PlateThin : Plate;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            float alpha = weight == PlateWeight.Screen ? 1f : weight == PlateWeight.Board ? 0.94f : 0.90f;
            image.color = new Color(1f, 1f, 1f, alpha);
        }

        private static readonly Color Iron = new Color(0.063f, 0.055f, 0.055f);
        private static readonly Color OldGold = new Color(0.62f, 0.49f, 0.27f);
        private static readonly Color LeatherTop = new Color(0.135f, 0.102f, 0.082f);
        private static readonly Color LeatherLow = new Color(0.082f, 0.062f, 0.050f);

        private static Sprite MakePlate(int size, int border, float gold, bool corners)
        {
            const float radius = 3f;
            const float rim = 2f;
            float g0 = rim, g1 = rim + gold;

            var t = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var px = new Color32[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;

                // Сколько точка внутри скруглённого прямоугольника (знак — снаружи).
                float qx = Mathf.Abs(fx - half) - (half - radius);
                float qy = Mathf.Abs(fy - half) - (half - radius);
                float sd = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude
                         + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                float e = -sd;
                if (e <= -0.5f) { px[y * size + x] = new Color32(0, 0, 0, 0); continue; }

                // Кожа светлее сверху; тень от края внутрь.
                Color c = Color.Lerp(LeatherLow, LeatherTop, fy / size);
                if (e > g1) c *= 1f - 0.45f * Mathf.Exp(-(e - g1) / 4f);

                // Железный край снаружи, за ним золото — оба со сглаженной кромкой.
                c = Color.Lerp(c, Iron, Mathf.Clamp01(g0 + 0.5f - e));
                float band = Mathf.Clamp01(Mathf.Min(e - g0 + 0.5f, g1 + 0.5f - e));
                c = Color.Lerp(c, OldGold, band);

                // Метки в углах — ромбики золота внутри рамы.
                if (corners)
                {
                    const float m = 9f, r = 3.2f;
                    foreach (var k in new[] { new Vector2(m, m), new Vector2(size - m, m),
                                              new Vector2(m, size - m), new Vector2(size - m, size - m) })
                    {
                        float dd = Mathf.Abs(fx - k.x) + Mathf.Abs(fy - k.y);
                        c = Color.Lerp(c, OldGold, Mathf.Clamp01(r - dd));
                    }
                }

                c.a = Mathf.Clamp01(e + 0.5f);
                px[y * size + x] = c;
            }

            t.SetPixels32(px);
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f,
                                 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        /// <summary>Основной текст: PT Serif.</summary>
        public static Font Body => _body ??= Load("Fonts/PTSerif-Regular");

        /// <summary>Жирный основной: PT Serif Bold — свой файл, а не дорисованный жирный.</summary>
        public static Font BodyBold => _bodyBold ??= Load("Fonts/PTSerif-Bold");

        /// <summary>Названия и имена: Cormorant SC.</summary>
        public static Font Title => _title ??= Load("Fonts/CormorantSC-Bold");

        private static Font Load(string path)
        {
            var font = Resources.Load<Font>(path);
            if (font != null) return font;

            Debug.LogWarning($"[ОБЛИК] Шрифта {path} нет в Resources — текст встроенным шрифтом.");
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        /// <summary>
        /// Капля пламени, остриём вверх — огонь души в портрете (§3): у всех
        /// скелетов один череп, а душа у каждого своя. Белая — красит Image.
        /// </summary>
        public static Sprite Drop => _drop ??= MakeDrop();

        /// <summary>
        /// Овал для кнопок и плашек (руки Греховода, установка): края резаны
        /// по девяти частям, так что он тянется на любую ширину.
        /// </summary>
        public static Sprite Pill => _pill ??= MakePill();

        private static Sprite MakeDrop()
        {
            const int w = 64, h = 80;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];

            // Круг снизу и остриё сверху, мягкий край в полтора пикселя.
            float cx = w * 0.5f, cy = h * 0.36f, r = w * 0.40f, tip = h * 0.98f;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float px0 = x + 0.5f, py0 = y + 0.5f;
                float dc = Mathf.Sqrt((px0 - cx) * (px0 - cx) + (py0 - cy) * (py0 - cy)) - r;

                // Выше центра — сужение к острию: допустимая полуширина падает до нуля.
                float d = dc;
                if (py0 > cy)
                {
                    float k = Mathf.InverseLerp(cy, tip, py0);
                    float half = r * (1f - k) * (1f - 0.35f * k);
                    d = Mathf.Min(dc, Mathf.Abs(px0 - cx) - half);
                    if (py0 > tip) d = 1f;
                }

                float a = Mathf.Clamp01(0.5f - d / 1.5f);
                px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }

            t.SetPixels32(px);
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakePill()
        {
            const int size = 64, radius = 31;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float qx = Mathf.Max(0f, Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - radius));
                float qy = Mathf.Max(0f, Mathf.Abs(y + 0.5f - size * 0.5f) - (size * 0.5f - radius));
                float d = Mathf.Sqrt(qx * qx + qy * qy) - radius;
                float a = Mathf.Clamp01(0.5f - d);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            t.SetPixels32(px);
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f,
                                 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }
    }
}
