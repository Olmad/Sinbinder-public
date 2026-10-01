// Assets/Scripts/UI/UiStyle.cs
// Перевод: текст через Loc
using UnityEngine;

namespace Sinbinder.UI
{
    /// <summary>
    /// Облик макета лагеря (docs/42-INTERFACE.md §1, пп. 8–9) — одно место
    /// на все панели: шрифты, капля огня души, овальная плашка.
    ///
    /// <b>Шрифты.</b> Книжная антиква: заглавные с засечками для названий
    /// и имён (Cormorant SC), простая читаемая для остального (PT Serif).
    /// Обе с кириллицей, обе под открытой лицензией OFL — файлы и лицензии
    /// в <c>Resources/Fonts</c>. До 1 октября весь интерфейс шёл встроенным
    /// Arial, и полоса по макету в игре читалась «как веб-приложение».
    /// Нет файла — встроенный шрифт: облик — украшение, игра обязана
    /// остаться игрой.
    ///
    /// <b>Фигуры</b> рисуются кодом — капля и овал, без картинок: их
    /// нечем испортить при импорте.
    /// </summary>
    public static class UiStyle
    {
        private static Font _body, _bodyBold, _title;
        private static Sprite _drop, _pill;

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
