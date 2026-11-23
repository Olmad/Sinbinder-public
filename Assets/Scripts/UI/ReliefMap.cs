// Assets/Scripts/UI/ReliefMap.cs
// Перевод: подписи мест — Loc.Name у вызывающего; здесь только строки через Loc.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sinbinder.UI
{
    /// <summary>
    /// Рельефная карта окрестностей в хрустальном шаре — одна на лагерь и склеп
    /// (автор, 23.11: «нужно, чтобы в шаре находилась рельефная общая карта с точками
    /// интереса»; «по задумке мы отправляем первую экспедицию ещё в лагере»).
    ///
    /// Места стоят на одной земле: лагерь пролога, склеп, часовня, каменоломня, застава —
    /// те же, что в совете у шара, и те же, что потом на карте склепа (отряды у часовни
    /// и каменоломни гибнут в набеге, и места освобождаются). Положение постоянное:
    /// карта, которая тасуется, отняла бы у игрока сравнение (MissionCatalog).
    ///
    /// Рельеф рисуется кодом один раз: высоты (шум и холмы по замыслу — «за холмами
    /// стоит брошенная застава»), отмывка светом с северо-запада, горизонтали, река через
    /// затопленную каменоломню в топь, леса у лагеря и часовни, дороги. Потом — шар:
    /// линза (середина крупнее), холодный свет стекла, тёмный край, блик.
    /// </summary>
    public static class ReliefMap
    {
        public const string Camp = "Лагерь";
        public const string Crypt = "Склеп";

        // Доли карты, y — вверх (север).
        private static readonly Dictionary<string, Vector2> Places = new()
        {
            [Camp] = new Vector2(0.47f, 0.22f),
            [Crypt] = new Vector2(0.16f, 0.52f),
            ["Придорожная часовня"] = new Vector2(0.30f, 0.40f),
            ["Затопленная каменоломня"] = new Vector2(0.68f, 0.35f),
            ["Сожжённая застава"] = new Vector2(0.50f, 0.61f),
            ["Соляной обоз"] = new Vector2(0.28f, 0.76f),
            ["Старый гарнизон"] = new Vector2(0.74f, 0.71f),
            ["Костяная топь"] = new Vector2(0.85f, 0.47f),
        };

        private static readonly string[][] RoadPairs =
        {
            new[] { Camp, "Придорожная часовня" }, new[] { Camp, "Затопленная каменоломня" },
            new[] { Camp, "Сожжённая застава" }, new[] { Crypt, "Придорожная часовня" },
            new[] { "Придорожная часовня", "Соляной обоз" }, new[] { "Сожжённая застава", "Старый гарнизон" },
            new[] { "Затопленная каменоломня", "Костяная топь" }, new[] { "Соляной обоз", "Сожжённая застава" },
        };

        private static readonly Vector2[] River =
        {
            new(0.60f, 1.02f), new(0.62f, 0.82f), new(0.66f, 0.62f), new(0.69f, 0.44f),
            new(0.70f, 0.35f), new(0.78f, 0.40f), new(0.86f, 0.46f), new(0.95f, 0.40f), new(1.04f, 0.36f),
        };

        /// <summary>Линза шара: середина крупнее, край на краю.</summary>
        private const float Lens = 0.55f;

        public static bool Has(string place) => place != null && Places.ContainsKey(place);

        /// <summary>Где место в шаре: доли квадрата шара (0..1), с линзой.</summary>
        public static Vector2 InBall(string place)
        {
            if (!Places.TryGetValue(place ?? "", out var m)) return new Vector2(0.5f, 0.5f);
            var d = m - new Vector2(0.5f, 0.5f);
            float rm = d.magnitude;
            if (rm < 1e-5f) return m;
            // Обратная линза: rm = rd(1+K rd²)/(1+K R²) — Ньютоном.
            const float R = 0.5f;
            float norm = 1f + Lens * R * R, rd = rm;
            for (int i = 0; i < 8; i++)
            {
                float f = rd * (1f + Lens * rd * rd) / norm - rm;
                float df = (1f + 3f * Lens * rd * rd) / norm;
                rd -= f / df;
            }
            return new Vector2(0.5f, 0.5f) + d * (rd / rm);
        }

        // ── шар ──

        private static Texture2D _ball;

        /// <summary>Шар с картой: квадратная картинка, вне круга — прозрачно.</summary>
        public static RectTransform Ball(RectTransform parent, float diameter, Vector2 topLeft)
        {
            var go = new GameObject("Шар — карта окрестностей", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(diameter, diameter);
            rt.anchoredPosition = topLeft;
            var img = go.AddComponent<RawImage>();
            img.texture = Texture();
            img.raycastTarget = false;
            return rt;
        }

        private const int M = 512;
        private static Color[] _colors;
        private static float[] _heights;
        private static Texture2D _flat;

        private static Color[] Colors()
        {
            if (_colors == null) _colors = Paint(M);
            return _colors;
        }

        /// <summary>Место на земле карты (доли, без линзы) — для рельефа в стеклянном шаре.</summary>
        public static Vector2 MapPos(string place)
            => Places.TryGetValue(place ?? "", out var m) ? m : new Vector2(0.5f, 0.5f);

        /// <summary>Высота земли (как на карте) в долях карты.</summary>
        public static float HeightAt(Vector2 uv)
        {
            Colors();
            float fx = Mathf.Clamp(uv.x * M - 0.5f, 0f, M - 1.001f), fy = Mathf.Clamp(uv.y * M - 0.5f, 0f, M - 1.001f);
            int x0 = (int)fx, y0 = (int)fy;
            float tx = fx - x0, ty = fy - y0;
            float a = Mathf.Lerp(_heights[y0 * M + x0], _heights[y0 * M + x0 + 1], tx);
            float b = Mathf.Lerp(_heights[(y0 + 1) * M + x0], _heights[(y0 + 1) * M + x0 + 1], tx);
            return Mathf.Lerp(a, b, ty);
        }

        /// <summary>Карта без шара: квадрат земли с отмывкой — текстура рельефа в стекле.</summary>
        public static Texture2D MapTexture()
        {
            if (_flat != null) return _flat;
            _flat = new Texture2D(M, M, TextureFormat.RGBA32, false)
            { name = "Рельеф окрестностей", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            _flat.SetPixels(Colors());
            _flat.Apply(false, true);
            return _flat;
        }

        /// <summary>
        /// Рельеф для стеклянного шара: круглая сетка в плоскости XZ радиуса <paramref name="radius"/>,
        /// высоты карты × <paramref name="lift"/>; к краю земля опускается — край круглый, без зубцов.
        /// UV — доли карты (текстура <see cref="MapTexture"/>).
        /// </summary>
        public static Mesh ReliefMesh(int n, float radius, float lift)
        {
            Colors();
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int y = 0; y <= n; y++)
            for (int x = 0; x <= n; x++)
            {
                var d = new Vector2(x / (float)n - 0.5f, y / (float)n - 0.5f);
                if (d.magnitude > 0.5f) d = d.normalized * 0.5f;
                var uv = d + new Vector2(0.5f, 0.5f);
                float fall = Mathf.InverseLerp(0.5f, 0.42f, d.magnitude);
                float h = (HeightAt(uv) + 0.05f) * lift * fall;
                verts.Add(new Vector3(d.x * 2f * radius, h, d.y * 2f * radius));
                uvs.Add(uv);
            }
            // С севера на юг: без записи глубины дальние склоны рисуются раньше ближних.
            for (int y = n - 1; y >= 0; y--)
            for (int x = 0; x < n; x++)
            {
                int i = y * (n + 1) + x;
                tris.Add(i); tris.Add(i + n + 1); tris.Add(i + 1);
                tris.Add(i + 1); tris.Add(i + n + 1); tris.Add(i + n + 2);
            }
            var mesh = new Mesh { name = "Рельеф в шаре" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            var white = new Color32[verts.Count];
            for (int i = 0; i < white.Length; i++) white[i] = new Color32(255, 255, 255, 255);
            mesh.SetColors(white);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Texture2D Texture()
        {
            if (_ball != null) return _ball;
            const int N = 800;
            var map = Colors();
            var px = new Color32[N * N];
            const float R = 0.5f;
            float norm = 1f + Lens * R * R;
            var tint = new Color(0.86f, 0.93f, 1.08f);
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N - 0.5f, dy = (y + 0.5f) / N - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01((R - r) * N * 0.9f);
                if (alpha <= 0f) { px[y * N + x] = new Color32(0, 0, 0, 0); continue; }
                float k = (1f + Lens * r * r) / norm;
                var c = Sample(map, M, 0.5f + dx * k, 0.5f + dy * k);
                // Стекло: холодный свет, тёмный край (кривизна), сине-лиловое свечение у кромки, блик.
                c = Color.Lerp(c, c * tint, 0.35f);
                float edge = Mathf.InverseLerp(0.30f, R, r);
                c *= 1f - 0.55f * edge * edge;
                float rim = Mathf.InverseLerp(0.43f, R, r);
                c += new Color(0.20f, 0.26f, 0.55f) * (0.35f * rim * rim);
                float ring = Mathf.Exp(-Mathf.Pow((r - 0.488f) / 0.006f, 2f));
                c += new Color(0.55f, 0.65f, 0.85f) * (0.45f * ring);
                float hx = (dx + 0.17f) / 0.15f, hy = (dy - 0.21f) / 0.09f;
                float glint = Mathf.Exp(-(hx * hx + hy * hy));
                c += Color.white * (0.16f * glint);
                px[y * N + x] = new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), alpha);
            }
            _ball = new Texture2D(N, N, TextureFormat.RGBA32, false)
            { name = "Шар — рельеф", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            _ball.SetPixels32(px);
            _ball.Apply(false, true);
            return _ball;
        }

        private static Color Sample(Color[] map, int m, float u, float v)
        {
            float fx = Mathf.Clamp(u * m - 0.5f, 0f, m - 1.001f), fy = Mathf.Clamp(v * m - 0.5f, 0f, m - 1.001f);
            int x0 = (int)fx, y0 = (int)fy;
            float tx = fx - x0, ty = fy - y0;
            var a = Color.Lerp(map[y0 * m + x0], map[y0 * m + x0 + 1], tx);
            var b = Color.Lerp(map[(y0 + 1) * m + x0], map[(y0 + 1) * m + x0 + 1], tx);
            return Color.Lerp(a, b, ty);
        }

        // ── рельеф ──

        private static float Bump(float u, float v, float x, float y, float r)
        {
            float du = u - x, dv = v - y;
            return Mathf.Exp(-(du * du + dv * dv) / (r * r));
        }

        private static float Fbm(float x, float y, int octaves)
        {
            float sum = 0f, amp = 0.5f, f = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * Mathf.PerlinNoise(x * f + i * 17.3f, y * f + i * 9.1f);
                f *= 2.03f; amp *= 0.5f;
            }
            return sum;
        }

        private static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return (p - (a + ab * t)).magnitude;
        }

        private static float Polyline(Vector2 p, IReadOnlyList<Vector2> line)
        {
            float best = 9f;
            for (int i = 0; i + 1 < line.Count; i++) best = Mathf.Min(best, Segment(p, line[i], line[i + 1]));
            return best;
        }

        /// <summary>Река петляет: точку сдвигает шум, прежде чем мерить до русла.</summary>
        private static float RiverDistance(float u, float v)
        {
            float wu = (Mathf.PerlinNoise(u * 6f + 31f, v * 6f) - 0.5f) * 0.07f
                     + (Mathf.PerlinNoise(u * 17f, v * 17f + 5f) - 0.5f) * 0.02f;
            float wv = (Mathf.PerlinNoise(u * 6f, v * 6f + 47f) - 0.5f) * 0.05f;
            return Polyline(new Vector2(u + wu, v + wv), River);
        }

        private static float Height(float u, float v, float river)
        {
            float h = 0.40f * Fbm(u * 2.2f + 7.3f, v * 2.2f + 1.9f, 4);
            h += 0.30f * Bump(u, v, 0.50f, 0.47f, 0.10f);   // холмы: «за холмами стоит застава»
            h += 0.18f * Bump(u, v, 0.40f, 0.52f, 0.07f);
            float ridge = 1f - Mathf.Abs(Mathf.PerlinNoise(u * 7f + 3f, v * 7f + 5f) * 2f - 1f);
            h += 0.50f * Bump(u, v, 0.84f, 0.88f, 0.22f) * (0.5f + 0.9f * ridge);   // горы северо-востока
            h += 0.22f * Bump(u, v, 0.10f, 0.86f, 0.18f);
            h -= 0.20f * Mathf.Exp(-river * river / 0.0016f);   // долина реки
            h = Mathf.Lerp(h, 0.10f, 0.85f * Bump(u, v, 0.85f, 0.47f, 0.09f));   // топь
            h -= 0.16f * Bump(u, v, 0.68f, 0.35f, 0.028f);   // яма каменоломни
            return h;
        }

        private static Color[] Paint(int m)
        {
            var hgt = new float[m * m];
            _heights = hgt;
            var riv = new float[m * m];
            for (int y = 0; y < m; y++)
            for (int x = 0; x < m; x++)
            {
                float u = (x + 0.5f) / m, v = (y + 0.5f) / m;
                float d = RiverDistance(u, v);
                riv[y * m + x] = d;
                hgt[y * m + x] = Height(u, v, d);
            }

            // Дороги — чуть изогнутые: середина сдвинута поперёк.
            var roads = new List<Vector2[]>();
            foreach (var pair in RoadPairs)
            {
                var a = Places[pair[0]]; var b = Places[pair[1]];
                var mid = (a + b) * 0.5f + new Vector2(-(b - a).y, (b - a).x) * 0.12f * (roads.Count % 2 == 0 ? 1f : -1f);
                roads.Add(new[] { a, Vector2.Lerp(a, mid, 0.5f) + (mid - (a + b) * 0.5f) * 0.3f, mid,
                                  Vector2.Lerp(mid, b, 0.5f) + (mid - (a + b) * 0.5f) * 0.3f, b });
            }

            var low = new Color(0.31f, 0.33f, 0.23f);
            var mid2 = new Color(0.42f, 0.39f, 0.27f);
            var high = new Color(0.50f, 0.45f, 0.35f);
            var peak = new Color(0.64f, 0.62f, 0.58f);
            var forest = new Color(0.17f, 0.23f, 0.16f);
            var water = new Color(0.16f, 0.24f, 0.29f);
            var road = new Color(0.70f, 0.61f, 0.45f);
            var light = new Vector3(-0.62f, 0.58f, 0.55f).normalized;

            var col = new Color[m * m];
            for (int y = 0; y < m; y++)
            for (int x = 0; x < m; x++)
            {
                int i = y * m + x;
                float u = (x + 0.5f) / m, v = (y + 0.5f) / m;
                float h = hgt[i];
                float t = Mathf.Clamp01(h / 0.85f);
                var c = t < 0.35f ? Color.Lerp(low, mid2, t / 0.35f)
                      : t < 0.65f ? Color.Lerp(mid2, high, (t - 0.35f) / 0.30f)
                      : Color.Lerp(high, peak, (t - 0.65f) / 0.35f);

                // Соляная равнина у обоза светлее; пепелище заставы темнее.
                c = Color.Lerp(c, new Color(0.60f, 0.52f, 0.36f), 0.45f * Bump(u, v, 0.27f, 0.79f, 0.09f));
                c = Color.Lerp(c, new Color(0.20f, 0.17f, 0.15f), 0.70f * Bump(u, v, 0.50f, 0.61f, 0.025f));

                // Леса — у лагеря, часовни, склепа и где шум густ и земля невысока.
                float woods = Mathf.Max(Bump(u, v, 0.31f, 0.43f, 0.12f), Mathf.Max(Bump(u, v, 0.44f, 0.18f, 0.12f),
                              Bump(u, v, 0.15f, 0.55f, 0.11f)));
                woods = Mathf.Max(woods, Mathf.InverseLerp(0.58f, 0.70f, Mathf.PerlinNoise(u * 8f + 11f, v * 8f + 4f)) * (t < 0.5f ? 1f : 0f));
                if (woods > 0.35f)
                {
                    float trees = Mathf.PerlinNoise(u * 110f, v * 110f);
                    var w = Color.Lerp(forest, forest * 0.6f, trees > 0.55f ? 1f : 0f);
                    c = Color.Lerp(c, w, Mathf.InverseLerp(0.35f, 0.7f, woods) * 0.85f);
                }

                // Отмывка: нормаль по соседям.
                float hl = hgt[y * m + Mathf.Max(0, x - 1)], hr = hgt[y * m + Mathf.Min(m - 1, x + 1)];
                float hd = hgt[Mathf.Max(0, y - 1) * m + x], hu = hgt[Mathf.Min(m - 1, y + 1) * m + x];
                var n = new Vector3((hl - hr) * m * 0.6f, (hd - hu) * m * 0.6f, 1f).normalized;
                float shade = 0.42f + 0.80f * Mathf.Max(0f, Vector3.Dot(n, light));
                c *= shade;

                // Горизонтали.
                float ct = h * 15f - Mathf.Floor(h * 15f);
                if (ct < 0.07f && t > 0.12f) c *= 0.86f;

                // Вода: река, топь (пятнами), залитая яма каменоломни.
                float rd = riv[i];
                bool wet = rd < 0.0035f + 0.0035f * Mathf.InverseLerp(1f, 0.3f, v)
                        || Bump(u, v, 0.85f, 0.47f, 0.08f) > 0.45f && Mathf.PerlinNoise(u * 34f + 2f, v * 34f) > 0.52f
                        || Bump(u, v, 0.68f, 0.35f, 0.02f) > 0.55f;
                if (wet) c = Color.Lerp(water, water * 1.5f, Mathf.PerlinNoise(u * 60f, v * 60f) * 0.5f);

                // Дороги.
                var p = new Vector2(u, v);
                float dr = 9f;
                foreach (var line in roads) dr = Mathf.Min(dr, Polyline(p, line));
                float ra = Mathf.InverseLerp(0.0040f, 0.0018f, dr);
                if (ra > 0f) c = Color.Lerp(c, road * shade, ra * 0.85f);

                c.a = 1f;
                col[i] = c;
            }
            return col;
        }

        // ── точки ──

        private static Sprite _dot;

        /// <summary>
        /// Место на карте: ромб, имя, строка под ним. Нажатие — <paramref name="click"/>, наведение —
        /// <paramref name="hover"/>. Подписи с обводкой: рельеф пёстрый, без неё не прочесть.
        /// </summary>
        public static void Point(RectTransform ball, string place, string title, string note, Color colour,
                                 bool bright, Font font, Action click, Action hover = null, bool home = false)
        {
            var at = InBall(place);
            var go = new GameObject(title, typeof(RectTransform));
            go.transform.SetParent(ball, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = at;
            rt.pivot = new Vector2(0.5f, 0.78f);
            rt.sizeDelta = new Vector2(220f, 84f);
            rt.anchoredPosition = Vector2.zero;
            var hit = go.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0.001f);
            hit.raycastTarget = click != null || hover != null;
            if (click != null && bright)
            {
                var button = go.AddComponent<Button>();
                button.targetGraphic = hit;
                button.onClick.AddListener(() => click());
            }
            if (hover != null)
            {
                var trigger = go.AddComponent<EventTrigger>();
                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => hover());
                trigger.triggers.Add(enter);
            }

            var mark = new GameObject("Ромб", typeof(RectTransform));
            mark.transform.SetParent(rt, false);
            var mt = (RectTransform)mark.transform;
            mt.anchorMin = mt.anchorMax = new Vector2(0.5f, 0.78f);
            mt.sizeDelta = home ? new Vector2(16f, 16f) : new Vector2(20f, 20f);
            mt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var img = mark.AddComponent<Image>();
            img.color = bright ? colour : new Color(colour.r * 0.6f, colour.g * 0.6f, colour.b * 0.6f, 0.8f);
            img.raycastTarget = false;
            mark.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);

            Caption(rt, title, home ? 17 : 20, -30f, bright ? new Color(0.96f, 0.92f, 0.84f) : new Color(0.70f, 0.66f, 0.60f), font);
            if (!string.IsNullOrEmpty(note))
                Caption(rt, note, 15, -54f, colour, font);
        }

        /// <summary>Огонь отряда в пути — тёплая мягкая точка, дышит (MapPulse).</summary>
        public static void Glow(RectTransform ball, string place, Color colour, float size = 34f)
        {
            var go = new GameObject("Огонь отряда", typeof(RectTransform));
            go.transform.SetParent(ball, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = InBall(place);
            rt.sizeDelta = new Vector2(size, size);
            var img = go.AddComponent<Image>();
            img.sprite = Dot();
            img.color = colour;
            img.raycastTarget = false;
            go.AddComponent<MapPulse>();
        }

        private static Text Caption(RectTransform parent, string text, int size, float top, Color colour, Font font)
        {
            var go = new GameObject("Подпись", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, size + 8f);
            rt.anchoredPosition = new Vector2(0f, top);
            var label = go.AddComponent<Text>();
            label.font = font; label.fontSize = size; label.text = text; label.raycastTarget = false;
            label.alignment = TextAnchor.UpperCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.color = colour;
            var o = go.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.9f);
            o.effectDistance = new Vector2(1.5f, -1.5f);
            return label;
        }

        public static Sprite Dot()
        {
            if (_dot != null) return _dot;
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Огонь на карте" };
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                float k = Mathf.InverseLerp(0.2f, 1f, Mathf.Sqrt(dx * dx + dy * dy));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(1f - k * k * (3f - 2f * k)) * 255f));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            _dot = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _dot;
        }

        // ── кадр места (карточка) ──

        private static readonly Dictionary<string, string> Art = new()
        {
            ["Соляной обоз"] = "salt-road", ["Придорожная часовня"] = "roadside-chapel",
            ["Затопленная каменоломня"] = "flooded-quarry", ["Сожжённая застава"] = "burned-outpost",
            ["Старый гарнизон"] = "old-garrison", ["Костяная топь"] = "bone-marsh",
        };

        /// <summary>Кадр места для карточки (Resources/Expeditions/&lt;место&gt;), или null.</summary>
        public static Texture2D Shot(string place)
            => Art.TryGetValue(place ?? "", out var key) ? Resources.Load<Texture2D>("Expeditions/" + key) : null;

        /// <summary>Имя файла кадра места — для съёмки (Editor/LocationShots).</summary>
        public static string ShotKey(string place) => Art.TryGetValue(place ?? "", out var key) ? key : null;

        /// <summary>
        /// Шапка карточки: кадр места во всю ширину, имя крупно поверх, внизу — затемнение.
        /// Кадра нет — тёмная плашка с именем. Вернёт высоту.
        /// </summary>
        public static float Header(RectTransform parent, float y, float height, string place, string title, Font font)
        {
            var go = new GameObject("Кадр места", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = new Vector2(0f, y);
            var shot = Shot(place);
            if (shot != null)
            {
                var img = go.AddComponent<RawImage>();
                img.texture = shot;
                img.raycastTarget = false;
                // Кадр не растягиваем: шапка шире кадра — режем по высоте, уже — по ширине.
                float w = parent.rect.width > 10f ? parent.rect.width : 1350f;
                float shotAspect = (float)shot.width / Mathf.Max(1, shot.height), headAspect = w / height;
                img.uvRect = headAspect >= shotAspect
                    ? new Rect(0f, (1f - shotAspect / headAspect) * 0.5f, 1f, shotAspect / headAspect)
                    : new Rect((1f - headAspect / shotAspect) * 0.5f, 0f, headAspect / shotAspect, 1f);
            }
            else
                go.AddComponent<Image>().color = new Color(0.08f, 0.07f, 0.07f, 0.95f);

            var shade = new GameObject("Затемнение", typeof(RectTransform));
            shade.transform.SetParent(rt, false);
            var st = (RectTransform)shade.transform;
            st.anchorMin = Vector2.zero; st.anchorMax = new Vector2(1f, 0f); st.pivot = new Vector2(0.5f, 0f);
            st.sizeDelta = new Vector2(0f, 70f);
            var si = shade.AddComponent<Image>();
            si.color = new Color(0f, 0f, 0f, 0.55f);
            si.raycastTarget = false;

            var name = new GameObject("Имя места", typeof(RectTransform));
            name.transform.SetParent(rt, false);
            var nt = (RectTransform)name.transform;
            nt.anchorMin = Vector2.zero; nt.anchorMax = new Vector2(1f, 0f); nt.pivot = new Vector2(0f, 0f);
            nt.offsetMin = new Vector2(22f, 10f); nt.offsetMax = new Vector2(-22f, 60f);
            var label = name.AddComponent<Text>();
            label.font = font; label.fontSize = 38; label.text = title; label.raycastTarget = false;
            label.alignment = TextAnchor.LowerLeft;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.color = new Color(0.97f, 0.92f, 0.82f);
            var o = name.AddComponent<Shadow>();
            o.effectColor = new Color(0f, 0f, 0f, 0.9f);
            o.effectDistance = new Vector2(2f, -2f);
            return height;
        }
    }
}
