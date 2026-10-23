// Assets/Scripts/Editor/OverlapCheck.cs
using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.Utilets
{
    /// <summary>
    /// Не врос ли предмет в предмет. Автор, 21 октября: «нам нужна проверка,
    /// что объекты не пересекаются с объектами. Раньше сундук Марги
    /// пересекался со склоном с холма, и это не единичный случай».
    ///
    /// Предмет — корень сцены или ребёнок одной из «корзин» (палатки,
    /// обстановка, окрестности). Его форма — повёрнутые коробки по каждой
    /// сетке (<c>Renderer.localBounds</c>), а не коробка по осям мира:
    /// повёрнутый склон в осях мира «задевал» бы всё вокруг. Две коробки
    /// разных предметов, проникшие друг в друга глубже <see cref="Slack"/>, —
    /// пересечение. Касание — нет: бочка стоит на земле, ящик на ящике.
    ///
    /// Зовёт сборщик сцен после постройки; каждое пересечение — ошибка
    /// в логе с именами обоих, итог — строкой «[ПЕРЕСЕЧЕНИЯ] …».
    /// </summary>
    public static class OverlapCheck
    {
        /// <summary>На сколько коробки ужимаются с каждой стороны: касание и мелкий зазор моделей — не пересечение.</summary>
        private const float Slack = 0.08f;

        /// <summary>Корзины: их дети — отдельные предметы.</summary>
        private static readonly HashSet<string> Baskets = new()
        {
            "Палатки", "Обстановка", "Окрестности", "Камни у тропы",
            "Мастерская", "Гнёзда",
            // Склеп: зал, вход, мастерская («Связывание») и стол вылазок.
            "Зал", "Вход в склеп", "Связывание", "Карта вылазок",
        };

        /// <summary>Не предметы: земля, свет, камера, интерфейс.</summary>
        private static readonly HashSet<string> Ignored = new()
        {
            "Земля", "Камень", "Плиты", "Main Camera", "Холодный свет", "Взгляд",
        };

        /// <summary>Пары, которые срослись нарочно (порядок не важен).</summary>
        private static readonly (string, string)[] Joined =
        {
            ("Возвышенность", "Спуск"),
            ("Возвышенность", "Палатка Греховода"),
            ("Спуск", "Палатка Греховода"),
            // Трава под кроной бора. Коробка корабельной сосны — от земли
            // до кроны шириной кроны (сетка одна), и трава по колено под
            // кроной считалась вросшей в ствол (23 октября).
            ("Лес", "GrassTuft"),
            ("Лес", "Bush"),
        };

        private struct Box
        {
            public Vector3 Centre;
            public Vector3 Half;
            public Vector3 X, Y, Z;

            /// <summary>
            /// Стоячий цилиндр (холм): круг в плане, а не квадрат — у коробки
            /// вокруг цилиндра углы торчат на 0,4 радиуса, и всё у подножия
            /// «врастало» в холм.
            /// </summary>
            public bool Round;
        }

        /// <summary>Проверить открытую сцену. Возвращает число пересечений.</summary>
        public static int Run(string scene)
        {
            var items = new List<(string Name, List<Box> Boxes)>();

            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (Ignored.Contains(root.name) || root.GetComponentInChildren<Canvas>(true) != null) continue;
                Collect(items, root.transform);
            }

            int found = 0;
            for (int a = 0; a < items.Count; a++)
                for (int b = a + 1; b < items.Count; b++)
                {
                    if (Allowed(items[a].Name, items[b].Name)) continue;
                    if (!Touch(items[a].Boxes, items[b].Boxes, out float depth)) continue;

                    found++;
                    Debug.LogError($"[ПЕРЕСЕЧЕНИЕ] {scene}: «{items[a].Name}» × «{items[b].Name}» — вглубь {depth:0.00} м");
                }

            Debug.Log($"[ПЕРЕСЕЧЕНИЯ] {scene}: предметов {items.Count}, пересечений {found}"
                    + (items.Count <= 12 ? " — " + string.Join(", ", items.ConvertAll(i => i.Name)) : ""));

            found += Grounded(scene, items);
            found += InsideTents(scene);
            return found;
        }

        /// <summary>
        /// Убранство палатки — под тканью (автор, 21.10: «сундуки часто
        /// пересекают палатки»). Палатка с вещами внутри — один предмет для
        /// проверки пересечений, поэтому сундучок сквозь скат там не виден.
        /// Здесь: каждый угол каждой вещи внутри — не за боковиной, не за
        /// пологом и не выше ската (<see cref="Gameplay.TentInterior.PokesOut"/>).
        /// </summary>
        private static int InsideTents(string scene)
        {
            int found = 0;
            foreach (var tent in Object.FindObjectsByType<Gameplay.TentInterior>(FindObjectsSortMode.None))
            {
                foreach (var r in tent.GetComponentsInChildren<Renderer>())
                {
                    if (tent.IsCover(r) || r is ParticleSystemRenderer || r is LineRenderer || !r.enabled) continue;
                    if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;

                    var lb = r.localBounds;
                    var t = r.transform;
                    float worst = 0f;
                    for (int k = 0; k < 8; k++)
                    {
                        var c = lb.center + Vector3.Scale(lb.extents,
                            new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1)) * 0.92f;
                        var w = t.TransformPoint(c);
                        if (tent.PokesOut(w)) worst = Mathf.Max(worst, 1f);
                    }
                    if (worst <= 0f) continue;

                    found++;
                    Debug.LogError($"[СКВОЗЬ ТКАНЬ] {scene}: «{r.name}» в «{tent.name}» торчит наружу");
                }
            }
            Debug.Log($"[ПАЛАТКИ] {scene}: вещей сквозь ткань — {found}");
            return found;
        }

        /// <summary>
        /// Висит или утонул (21 октября, список сторожей). Земля — верх
        /// плоскости «Земля»/«Камень»/«Плиты». Низ предмета выше земли больше
        /// чем на 15 см и под ним нет другого предмета, на котором он мог бы
        /// стоять, — висит. Верх ниже земли — утонул.
        /// </summary>
        private static int Grounded(string scene, List<(string Name, List<Box> Boxes)> items)
        {
            float ground = 0f;
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if ((root.name == "Земля" || root.name == "Камень" || root.name == "Плиты")
                    && root.TryGetComponent<Renderer>(out var g))
                { ground = g.bounds.max.y; break; }

            int found = 0;
            for (int a = 0; a < items.Count; a++)
            {
                Span(items[a].Boxes, out float low, out float high, out var flat);

                if (high < ground + 0.02f)
                {
                    found++;
                    Debug.LogError($"[УТОНУЛ] {scene}: «{items[a].Name}» целиком под землёй (верх {high:0.00} м)");
                    continue;
                }

                if (low < ground + 0.15f) continue;

                bool held = false;
                for (int b = 0; b < items.Count && !held; b++)
                {
                    if (a == b) continue;
                    Span(items[b].Boxes, out float bottom, out float top, out var under);
                    held = top > low - 0.3f && top < low + 0.2f + Slack
                        && under.xMin < flat.xMax && under.xMax > flat.xMin
                        && under.yMin < flat.yMax && under.yMax > flat.yMin;

                    // Опора сбоку — факел на стене или воротах: рядом (35 см)
                    // предмет, который стоит на этой высоте.
                    const float side = 0.35f;
                    held |= bottom < low && top > low
                        && under.xMin - side < flat.xMax && under.xMax + side > flat.xMin
                        && under.yMin - side < flat.yMax && under.yMax + side > flat.yMin;
                }

                if (held) continue;
                found++;
                Debug.LogError($"[ВИСИТ] {scene}: «{items[a].Name}» — низ на {low - ground:0.00} м над землёй, под ним пусто");
            }

            Debug.Log($"[ОПОРА] {scene}: висящих и утонувших — {found}");
            return found;
        }

        /// <summary>Низ, верх и след в плане (x, z) набора коробок.</summary>
        private static void Span(List<Box> boxes, out float low, out float high, out Rect flat)
        {
            low = float.MaxValue; high = float.MinValue;
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            foreach (var b in boxes)
            {
                float v = Mathf.Abs(b.X.y) * b.Half.x + Mathf.Abs(b.Y.y) * b.Half.y + Mathf.Abs(b.Z.y) * b.Half.z;
                float hx = Mathf.Abs(b.X.x) * b.Half.x + Mathf.Abs(b.Y.x) * b.Half.y + Mathf.Abs(b.Z.x) * b.Half.z;
                float hz = Mathf.Abs(b.X.z) * b.Half.x + Mathf.Abs(b.Y.z) * b.Half.y + Mathf.Abs(b.Z.z) * b.Half.z;
                low = Mathf.Min(low, b.Centre.y - v); high = Mathf.Max(high, b.Centre.y + v);
                x0 = Mathf.Min(x0, b.Centre.x - hx); x1 = Mathf.Max(x1, b.Centre.x + hx);
                z0 = Mathf.Min(z0, b.Centre.z - hz); z1 = Mathf.Max(z1, b.Centre.z + hz);
            }
            flat = Rect.MinMaxRect(x0, z0, x1, z1);
        }

        /// <summary>
        /// Корзина на любой глубине: «Мастерская» склепа лежит не в корне,
        /// и до 21 октября весь склеп проверялся четырьмя «предметами».
        /// Корзина — её дети по отдельности; объект с корзиной где-то внутри —
        /// разбираем дальше; иначе — один предмет.
        /// </summary>
        private static void Collect(List<(string, List<Box>)> items, Transform t)
        {
            if (Baskets.Contains(t.name))
            {
                foreach (Transform child in t) Collect(items, child);
                return;
            }

            if (HasBasket(t))
            {
                foreach (Transform child in t) Collect(items, child);
                return;
            }

            Add(items, t.gameObject);
        }

        private static bool HasBasket(Transform t)
        {
            foreach (Transform child in t)
                if (Baskets.Contains(child.name) || HasBasket(child)) return true;
            return false;
        }

        private static void Add(List<(string, List<Box>)> items, GameObject go)
        {
            var boxes = new List<Box>();
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                if (!r.enabled || r.GetComponent<TextMesh>() != null) continue;
                if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;

                var lb = r.localBounds;
                var t = r.transform;
                var half = Vector3.Scale(lb.extents, Abs(t.lossyScale)) - Vector3.one * Slack;
                if (half.x <= 0f || half.y <= 0f || half.z <= 0f) continue;

                var mesh = r.GetComponent<MeshFilter>();

                // Курган (22 октября, DemoSceneBuilder.HillMesh) — земля, а не
                // коробка: рамка его склонов 22 × 22 м «пересекала» всё у подножия.
                // Меряем макушку — цилиндр, на котором стоит шатёр.
                if (mesh != null && mesh.sharedMesh != null && mesh.sharedMesh.name == "Холм")
                {
                    float top = lb.max.y * Mathf.Abs(t.lossyScale.y);
                    float plateau = 3.4f;
                    boxes.Add(new Box
                    {
                        Centre = t.position + Vector3.up * (top * 0.5f),
                        Half = new Vector3(plateau, top * 0.5f, plateau) - Vector3.one * Slack,
                        X = Vector3.right, Y = Vector3.up, Z = Vector3.forward,
                        Round = true,
                    });
                    continue;
                }

                bool round = mesh != null && mesh.sharedMesh != null && mesh.sharedMesh.name == PrimitiveType.Cylinder.ToString()
                          && Vector3.Dot(t.up, Vector3.up) > 0.99f;

                boxes.Add(new Box
                {
                    Centre = t.TransformPoint(lb.center),
                    Half = half,
                    X = t.right, Y = t.up, Z = t.forward,
                    Round = round,
                });
            }

            if (boxes.Count > 0) items.Add((go.name, boxes));
        }

        private static Vector3 Abs(Vector3 v) => new(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        private static bool Allowed(string a, string b)
        {
            foreach (var (x, y) in Joined)
                if ((a == x && b == y) || (a == y && b == x)) return true;
            return false;
        }

        private static bool Touch(List<Box> a, List<Box> b, out float depth)
        {
            depth = 0f;
            foreach (var p in a)
                foreach (var q in b)
                    if (Overlap(p, q, out float d)) { depth = Mathf.Max(depth, d); }
            return depth > 0f;
        }

        /// <summary>
        /// Две повёрнутые коробки — по разделяющим осям: три оси одной, три
        /// другой и девять их попарных произведений. Нашлась ось, где тени
        /// коробок не сходятся, — не пересекаются. Глубина — самое мелкое
        /// перекрытие теней.
        /// </summary>
        private static bool Overlap(Box p, Box q, out float depth)
        {
            if (q.Round && !p.Round) (p, q) = (q, p);
            if (p.Round) return Cylinder(p, q, out depth);

            depth = float.MaxValue;
            var axes = new List<Vector3> { p.X, p.Y, p.Z, q.X, q.Y, q.Z };
            foreach (var u in new[] { p.X, p.Y, p.Z })
                foreach (var v in new[] { q.X, q.Y, q.Z })
                {
                    var c = Vector3.Cross(u, v);
                    if (c.sqrMagnitude > 1e-6f) axes.Add(c.normalized);
                }

            var d = q.Centre - p.Centre;
            foreach (var axis in axes)
            {
                float rp = Mathf.Abs(Vector3.Dot(p.X, axis)) * p.Half.x
                         + Mathf.Abs(Vector3.Dot(p.Y, axis)) * p.Half.y
                         + Mathf.Abs(Vector3.Dot(p.Z, axis)) * p.Half.z;
                float rq = Mathf.Abs(Vector3.Dot(q.X, axis)) * q.Half.x
                         + Mathf.Abs(Vector3.Dot(q.Y, axis)) * q.Half.y
                         + Mathf.Abs(Vector3.Dot(q.Z, axis)) * q.Half.z;
                float gap = rp + rq - Mathf.Abs(Vector3.Dot(d, axis));
                if (gap <= 0f) { depth = 0f; return false; }
                depth = Mathf.Min(depth, gap);
            }
            return true;
        }

        /// <summary>
        /// Цилиндр и коробка: по высоте — тени на вертикали, в плане —
        /// ближайшая к оси цилиндра точка коробки ближе радиуса.
        /// </summary>
        private static bool Cylinder(Box c, Box b, out float depth)
        {
            depth = 0f;

            float cy0 = c.Centre.y - c.Half.y, cy1 = c.Centre.y + c.Half.y;
            float bh = Mathf.Abs(b.X.y) * b.Half.x + Mathf.Abs(b.Y.y) * b.Half.y + Mathf.Abs(b.Z.y) * b.Half.z;
            float vertical = Mathf.Min(cy1, b.Centre.y + bh) - Mathf.Max(cy0, b.Centre.y - bh);
            if (vertical <= 0f) return false;

            // Ось цилиндра на высоте середины коробки → ближайшая точка коробки.
            var axis = new Vector3(c.Centre.x, b.Centre.y, c.Centre.z);
            var d = axis - b.Centre;
            var near = b.Centre
                     + b.X * Mathf.Clamp(Vector3.Dot(d, b.X), -b.Half.x, b.Half.x)
                     + b.Y * Mathf.Clamp(Vector3.Dot(d, b.Y), -b.Half.y, b.Half.y)
                     + b.Z * Mathf.Clamp(Vector3.Dot(d, b.Z), -b.Half.z, b.Half.z);
            var flat = near - axis;
            flat.y = 0f;

            float radius = Mathf.Min(c.Half.x, c.Half.z);
            float gap = radius - flat.magnitude;
            if (gap <= 0f) return false;
            depth = Mathf.Min(gap, vertical);
            return true;
        }
    }
}
