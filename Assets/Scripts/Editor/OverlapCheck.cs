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

                if (Baskets.Contains(root.name))
                {
                    foreach (Transform child in root.transform) Add(items, child.gameObject);
                    continue;
                }

                Add(items, root);
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

            Debug.Log($"[ПЕРЕСЕЧЕНИЯ] {scene}: предметов {items.Count}, пересечений {found}");
            return found;
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
