// Assets/Scripts/Gameplay/DreamRoadMap.cs
// Перевод: строк нет
using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Карта обучения (docs/68-TUTORIAL-MAP.md) — по рисунку автора, 17.11: опушка (звезда), умирающий,
    /// стоянка убийц, мост с рычагом через реку, засада за рекой, лес серпантином, расщелина и бревно
    /// через неё, колонны — и дальше привычная дорога к склепу (форт, площадь печатей, ворота).
    /// Автор: «Ты её можешь менять как хочешь, главное — чтобы карта обучала и держала интерес».
    ///
    /// Лежит к западу от старой дороги: площадь с колоннами встаёт в проломе её западной обочины,
    /// у южного конца. Строится в Awake сна вместе с дорогой — до выпечки навмеша. Дороги — коридоры
    /// в сетке клеток по метру; клетка леса, соседняя с дорогой, — бурелом (валежина, камни, сухой куст —
    /// выше шага агента): с дороги не сойти. Вода и расщелина — объёмы «не пройти»; мост и бревно — проходы.
    ///
    /// Маршрут (<see cref="Route"/>) — ломаная главного пути от звезды до ворот: сон меряет по ней,
    /// докуда пускать (<see cref="Progress"/>), — дорога больше не прямая по z.
    /// </summary>
    public partial class DreamRoad
    {
        // ── Точки карты: x — восток, z — север. Старая дорога — x = 0, от z = −31 к воротам. ──
        public static readonly Vector3 StarAt = new Vector3(-121f, 0f, 29f);
        public static readonly Vector3 DyingAt = new Vector3(-107f, 0f, 29f);
        public static readonly Vector3 CampAt = new Vector3(-84f, 0f, 31.5f);
        /// <summary>Развилка: на север — мост, на юг — лес (ветка отказа, этап 6).</summary>
        public static readonly Vector3 ForkAt = new Vector3(-70f, 0f, 29f);
        public static readonly Vector3 BridgeAt = new Vector3(-57.2f, 0f, 29f);
        public static readonly Vector3 LeverAt = new Vector3(-49.2f, 0f, 32.1f);
        public static readonly Vector3 AmbushAt = new Vector3(-32f, 0f, 11f);
        public static readonly Vector3 LogAt = new Vector3(-21f, 0f, -24f);

        public const float ChasmX = -21f, ChasmHalf = 2.6f, LogHalf = 1.4f, RiverHalf = 2.8f;
        public const float PlazaWest = ChasmX + ChasmHalf, PlazaEast = -7.2f, PlazaSouth = -28f, PlazaNorth = -20f;
        // Настил 8,2 м над рекой в 5,6 м (до 19.11 — 12,8 м: поднятый стоял стеной в 12 м посреди кадра).
        private const float DeckWest = -61.4f, DeckEast = -53.2f, DeckHalf = 2f;
        private const float MapWest = -136f, MapEast = -6f, MapSouth = -46f, MapNorth = 46f, ChasmNorth = 72f;

        /// <summary>Главный путь: звезда → развилка → мост → поворот к засаде → ряд бревна → старая дорога → ворота.</summary>
        public static readonly Vector3[] Route =
        {
            new Vector3(-121f, 0f, 29f), new Vector3(-70f, 0f, 29f), new Vector3(-31f, 0f, 29f),
            new Vector3(-31f, 0f, -24f), new Vector3(0f, 0f, -24f), new Vector3(0f, 0f, GateZ - 1.6f),
        };

        private struct Lane
        {
            public Vector2 A, B;
            public float Half;
            public bool Main;
            public Lane(float ax, float az, float bx, float bz, float half, bool main)
            { A = new Vector2(ax, az); B = new Vector2(bx, bz); Half = half; Main = main; }
        }

        private static readonly Lane[] Lanes =
        {
            // Главный путь: опушка → умирающий → стоянка → мост → вдоль реки к засаде и вниз, к бревну.
            new Lane(-124f, 29f, -31f, 29f, 3.5f, true),
            new Lane(-31f, 29f, -31f, -34f, 3.5f, true),
            new Lane(-31f, -24f, PlazaWest, -24f, LogHalf + 0.2f, true),
            // Лес серпантином — в обход реки и озера; сходится с главным путём внизу, у бревна.
            new Lane(-70f, 29f, -70f, 1f, 3f, false),
            new Lane(-70f, 1f, -124f, 1f, 3f, false),
            new Lane(-124f, 1f, -124f, -17f, 3f, false),
            new Lane(-124f, -17f, -67f, -17f, 3f, false),
            new Lane(-67f, -17f, -67f, -34f, 3f, false),
            new Lane(-67f, -34f, -31f, -34f, 3f, false),
        };

        /// <summary>Поляны: опушка (звезда), стоянка убийц, засада. y — радиус.</summary>
        private static readonly Vector3[] Glades = { new Vector3(-122f, 5.5f, 29f), new Vector3(-84f, 7f, 31.5f), new Vector3(-32f, 6.5f, 11f) };

        /// <summary>Река — с севера, под мостом, в озеро. Озеро — эллипс.</summary>
        private static readonly Vector2[] River = { new Vector2(-57.2f, 50f), new Vector2(-57.2f, 20f), new Vector2(-53f, 6f), new Vector2(-49.5f, -9.5f) };
        private static readonly Vector2 LakeAt = new Vector2(-46.2f, -17.7f), LakeRadius = new Vector2(8.4f, 10.5f);

        // Мост поворачивается у восточного края — подъёмный (этап 3); рычаг — у ворота.
        private Transform _bridge, _leverArm;

        // ───────────────────────────── маршрут ─────────────────────────────

        private static float _routeLength = -1f;

        public static float RouteLength
        {
            get
            {
                if (_routeLength < 0f)
                {
                    _routeLength = 0f;
                    for (int i = 0; i + 1 < Route.Length; i++) _routeLength += Flat(Route[i + 1] - Route[i]).magnitude;
                }
                return _routeLength;
            }
        }

        /// <summary>Сколько метров главного пути пройдено, если стоять в точке p (ближайшая точка ломаной).</summary>
        public static float Progress(Vector3 p) => Nearest(p, out _);

        /// <summary>Как далеко точка от главного пути.</summary>
        public static float OffRoute(Vector3 p) { Nearest(p, out float d); return d; }

        /// <summary>
        /// Как далеко точка от ЛЮБОЙ тропы сна: главного пути, лесного обхода, полян (0 — на поляне).
        /// Автор, 26.11: «не могу пройти дальше моста — меня возвращает». Мост остался поднят — путь лесом,
        /// а сон мерил коридор только от главного пути: лес уходит от него на 30–50 м, и рука тащила назад.
        /// </summary>
        public static float OffPaths(Vector3 p)
        {
            float best = OffRoute(p);
            var q = new Vector2(p.x, p.z);
            foreach (var l in Lanes) best = Mathf.Min(best, SegmentDistance(q, l.A, l.B));
            foreach (var g in Glades) best = Mathf.Min(best, Mathf.Max(0f, new Vector2(p.x - g.x, p.z - g.z).magnitude - g.y));
            return best;
        }

        private static float Nearest(Vector3 p, out float distance)
        {
            float best = float.MaxValue, at = 0f, run = 0f;
            var q = new Vector2(p.x, p.z);
            for (int i = 0; i + 1 < Route.Length; i++)
            {
                var a = new Vector2(Route[i].x, Route[i].z);
                var ab = new Vector2(Route[i + 1].x, Route[i + 1].z) - a;
                float len = ab.magnitude;
                float t = len > 1e-4f ? Mathf.Clamp01(Vector2.Dot(q - a, ab) / (len * len)) : 0f;
                float d = (a + ab * t - q).sqrMagnitude;
                if (d < best) { best = d; at = run + t * len; }
                run += len;
            }
            distance = Mathf.Sqrt(best);
            return at;
        }

        /// <summary>Точка главного пути в s метрах от звезды.</summary>
        public static Vector3 RoutePoint(float s)
        {
            s = Mathf.Max(0f, s);
            for (int i = 0; i + 1 < Route.Length; i++)
            {
                float len = Flat(Route[i + 1] - Route[i]).magnitude;
                if (s <= len || i + 2 == Route.Length)
                    return Vector3.Lerp(Route[i], Route[i + 1], len > 1e-4f ? Mathf.Clamp01(s / len) : 0f);
                s -= len;
            }
            return Route[Route.Length - 1];
        }

        /// <summary>Куда ведёт главный путь в s метрах от звезды.</summary>
        public static Vector3 RouteForward(float s)
        {
            s = Mathf.Max(0f, s);
            for (int i = 0; i + 1 < Route.Length; i++)
            {
                var seg = Flat(Route[i + 1] - Route[i]);
                if (s < seg.magnitude || i + 2 == Route.Length) return seg.normalized;
                s -= seg.magnitude;
            }
            return Vector3.forward;
        }

        /// <summary>Точка на главном пути в d метрах позади точки p.</summary>
        public static Vector3 Behind(Vector3 p, float d) => RoutePoint(Progress(p) - d);

        /// <summary>На площади печатей (старая дорога к склепу), а не где-то на той же широте карты.</summary>
        public static bool OnSquare(Vector3 p, float margin = 0f) => p.z >= SquareFrom + margin && Mathf.Abs(p.x) <= SquareHalf + 1f;

        /// <summary>Место перед грудью чучела в d метрах.</summary>
        public static Vector3 DummyFront(float d) => DummyAt + Quaternion.Euler(0f, DummyYaw, 0f) * Vector3.forward * d;

        // ───────────────────────────── сетка ─────────────────────────────

        private enum Ground : byte { Wood, Open, Water, Chasm, Off }

        private static float SegmentDistance(Vector2 q, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude) : 0f;
            return (a + ab * t - q).magnitude;
        }

        private static bool InPlaza(float x, float z) => x >= PlazaWest && x <= PlazaEast && z >= PlazaSouth && z <= PlazaNorth;
        private static bool OnDeck(float x, float z) => x >= DeckWest && x <= DeckEast && Mathf.Abs(z - BridgeAt.z) <= DeckHalf;
        private static bool OnLog(float x, float z) => Mathf.Abs(x - ChasmX) <= ChasmHalf + 0.3f && Mathf.Abs(z - LogAt.z) <= LogHalf;

        private static bool OnLane(float x, float z)
        {
            var q = new Vector2(x, z);
            foreach (var l in Lanes) if (SegmentDistance(q, l.A, l.B) <= l.Half) return true;
            foreach (var g in Glades) if (new Vector2(x - g.x, z - g.z).magnitude <= g.y) return true;
            return false;
        }

        private static bool InWater(float x, float z)
        {
            var q = new Vector2(x, z);
            for (int i = 0; i + 1 < River.Length; i++)
                if (SegmentDistance(q, River[i], River[i + 1]) <= RiverHalf) return true;
            var d = new Vector2((x - LakeAt.x) / LakeRadius.x, (z - LakeAt.y) / LakeRadius.y);
            return d.sqrMagnitude <= 1f;
        }

        private static Ground Classify(float x, float z)
        {
            if (OnLog(x, z)) return Ground.Open;
            if (Mathf.Abs(x - ChasmX) <= ChasmHalf) return Ground.Chasm;
            if (x > ChasmX)
            {
                // За расщелиной — только площадь у колонн и её края; дальше — старая дорога со своими обочинами.
                if (x > PlazaEast - 0.4f || z < PlazaSouth - 1.5f || z > PlazaNorth + 1.5f) return Ground.Off;
                return InPlaza(x, z) ? Ground.Open : Ground.Wood;
            }
            if (OnDeck(x, z)) return Ground.Open;
            if (InWater(x, z)) return Ground.Water;
            return OnLane(x, z) ? Ground.Open : Ground.Wood;
        }

        // ───────────────────────────── постройка ─────────────────────────────

        /// <summary>Построить карту обучения. Зовёт <see cref="Build"/> — до выпечки навмеша.</summary>
        private void BuildMap(Transform parent, Material earth, Material rut, Material bark, Material[] stones, Material post)
        {
            var root = new GameObject("Карта обучения").transform;
            root.SetParent(parent, false);
            var rnd = new System.Random(1711);   // свой жребий: старая дорога не сдвигается
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

            var leaf = Lit(new Color(0.055f, 0.07f, 0.055f), 0.05f);
            var mud = Lit(new Color(0.055f, 0.047f, 0.04f), 0.12f);
            // Вода без отражения неба: с ним река на низких кадрах горела белым, как снег (прогон 19.11).
            var water = Lit(new Color(0.018f, 0.034f, 0.055f), 0.7f);
            water.SetFloat("_EnvironmentReflections", 0f);
            water.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            mud.SetFloat("_EnvironmentReflections", 0f);
            mud.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            var wood = Lit(new Color(0.20f, 0.14f, 0.09f), 0.12f);
            var plank = Lit(new Color(0.15f, 0.10f, 0.065f), 0.1f);
            var iron = Lit(new Color(0.12f, 0.11f, 0.11f), 0.4f);
            var slab = Lit(new Color(0.15f, 0.15f, 0.16f), 0.15f);
            var pillar = Lit(new Color(0.25f, 0.25f, 0.26f), 0.12f);
            var dark = Lit(new Color(0.13f, 0.13f, 0.14f), 0.1f);
            var rope = Lit(new Color(0.22f, 0.18f, 0.12f), 0.05f);
            var reed = Lit(new Color(0.12f, 0.12f, 0.07f), 0.05f);
            var abyss = Glowing(Color.black, 1f);

            Widen(out float groundEast, out float groundSouth);

            // ── сетка: дорога, лес, вода, расщелина ──
            int nx = Mathf.RoundToInt(MapEast - MapWest), nz = Mathf.RoundToInt(MapNorth - MapSouth);
            var cells = new Ground[nx, nz];
            var dist = new int[nx, nz];
            var queue = new Queue<Vector2Int>();
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    cells[i, j] = Classify(MapWest + i + 0.5f, MapSouth + j + 0.5f);
                    dist[i, j] = cells[i, j] == Ground.Open ? 0 : int.MaxValue;
                    if (dist[i, j] == 0) queue.Enqueue(new Vector2Int(i, j));
                }
            // Лес — расстояние от дороги в клетках: край — бурелом, дальше — деревья.
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                for (int di = -1; di <= 1; di++)
                    for (int dj = -1; dj <= 1; dj++)
                    {
                        int i = c.x + di, j = c.y + dj;
                        if (i < 0 || j < 0 || i >= nx || j >= nz || cells[i, j] != Ground.Wood) continue;
                        if (dist[i, j] <= dist[c.x, c.y] + 1) continue;
                        dist[i, j] = dist[c.x, c.y] + 1;
                        if (dist[i, j] < 14) queue.Enqueue(new Vector2Int(i, j));
                    }
            }
            // Бурелом — «не ходить по нему»: иначе навмеш взбирался на валежины и камни ступенями по 0,75 м и
            // переводил с дороги в чащу (прогон 19.11: путь «(−37,8; 9,3) 0,75 → 1,08 → 0,08»).
            var rubble = new GameObject("Бурелом").transform;
            rubble.SetParent(root, false);
            var no = rubble.gameObject.AddComponent<NavMeshModifier>();
            no.overrideArea = true;
            no.area = UnityEngine.AI.NavMesh.GetAreaFromName("Not Walkable");
            no.applyToChildren = true;
            var used = new bool[nx, nz];
            bool Free(bool alongX, int a, int b) { int i = alongX ? b : a, j = alongX ? a : b; return dist[i, j] == 1 && !used[i, j]; }
            void Use(bool alongX, int a, int b) { int i = alongX ? b : a, j = alongX ? a : b; used[i, j] = true; }
            Vector3 At(bool alongX, int a, int b) { int i = alongX ? b : a, j = alongX ? a : b; return new Vector3(MapWest + i + 0.5f, 0f, MapSouth + j + 0.5f); }

            // Бурелом: подряд идущие клетки края — валежиной, грудой камней, сухим кустарником (по 2–4 клетки);
            // сперва вдоль x, остаток — вдоль z, одиночные — пнями.
            foreach (bool alongX in new[] { true, false })
            {
                int outer = alongX ? nz : nx, inner = alongX ? nx : nz;
                for (int a = 0; a < outer; a++)
                    for (int b = 0; b < inner;)
                    {
                        int n = 0;
                        while (b + n < inner && Free(alongX, a, b + n)) n++;
                        if (n < 2) { b++; continue; }
                        for (int k = 0; k < n;)
                        {
                            int len = Mathf.Min(n - k, rnd.Next(2, 5));
                            if (n - k - len == 1) len += len < 4 ? 1 : -1;
                            Windfall(rubble, At(alongX, a, b + k), At(alongX, a, b + k + len - 1), rnd, bark, stones, leaf);
                            for (int m = 0; m < len; m++) Use(alongX, a, b + k + m);
                            k += len;
                        }
                        b += n;
                    }
            }
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    if (dist[i, j] != 1 || used[i, j]) continue;
                    var at = new Vector3(MapWest + i + 0.5f, 0f, MapSouth + j + 0.5f);
                    if (rnd.NextDouble() < 0.5)
                    {
                        float h = R(0.56f, 0.7f);
                        Piece(rubble, PrimitiveType.Cylinder, "Пень", at + Vector3.up * h, new Vector3(R(0.75f, 0.95f), h, R(0.75f, 0.95f)), bark);
                    }
                    else
                    {
                        float h = R(1.2f, 1.6f);
                        Piece(rubble, PrimitiveType.Sphere, "Куст", at + Vector3.up * h * 0.4f, new Vector3(R(1.1f, 1.4f), h, R(1.1f, 1.4f)), leaf);
                    }
                }

            // Деревья: густо у края, редко в глубине. За краем земли не сажаем.
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    if (cells[i, j] != Ground.Wood) continue;
                    float x = MapWest + i + 0.5f, z = MapSouth + j + 0.5f;
                    if (x > ChasmX || (x > groundEast && z < groundSouth)) continue;
                    int d = dist[i, j];
                    double roll = rnd.NextDouble();
                    if (d >= 2 && d <= 7 ? roll < 0.05 : d > 7 && roll < 0.014)
                        Tree(root, new Vector3(x + R(-0.4f, 0.4f), 0f, z + R(-0.4f, 0.4f)), R(4f, 7.5f), rnd, bark);
                    else if (d == 2 && roll > 0.88)
                        Piece(root, PrimitiveType.Sphere, "Подлесок", new Vector3(x, 0.3f, z), new Vector3(R(1.0f, 1.6f), R(0.6f, 1.0f), R(1.0f, 1.6f)), leaf, false);
                }

            // ── дороги, поляны, площадь ──
            foreach (var l in Lanes)
            {
                var a = new Vector3(l.A.x, 0f, l.A.y);
                var b = new Vector3(l.B.x, 0f, l.B.y);
                float w = l.Half < 2.5f ? 2.6f : l.Main ? 3.6f : 2.6f;
                Strip(root, "Дорога карты", a, b, w, 0.012f, earth, w * 0.5f);
                if (!l.Main || l.Half < 2.5f) continue;
                var side = Vector3.Cross(Vector3.up, (b - a).normalized);
                foreach (float s in new[] { -0.8f, 0.8f })
                    Strip(root, "Колея", a + side * s, b + side * s, 0.3f, 0.016f, rut, 0f);
            }
            foreach (var g in Glades) Disc(root, "Поляна", new Vector3(g.x, 0.01f, g.z), (g.y - 1.2f) * 2f, earth);
            Flat(root, "Мощение у колонн", new Vector3((PlazaWest - 2.4f) * 0.5f, 0.02f, (PlazaSouth + PlazaNorth) * 0.5f),
                 new Vector2(-2.4f - PlazaWest, PlazaNorth - PlazaSouth), slab);

            // ── вода: река с севера под мостом — в озеро ──
            for (int i = 0; i + 1 < River.Length; i++)
            {
                var a = new Vector3(River[i].x, 0f, River[i].y);
                var b = new Vector3(River[i + 1].x, 0f, River[i + 1].y);
                Strip(root, "Берег", a, b, RiverHalf * 2f + 1.4f, 0.022f, mud, 0f);
                Strip(root, "Река", a, b, RiverHalf * 2f, 0.03f, water, 0f);
                if (i == 0) continue;
                Disc(root, "Берег", new Vector3(a.x, 0.022f, a.z), RiverHalf * 2f + 1.4f, mud);
                Disc(root, "Река", new Vector3(a.x, 0.03f, a.z), RiverHalf * 2f, water);
            }
            Ellipse(root, "Берег озера", LakeAt, LakeRadius * 2f + Vector2.one * 1.6f, 0.022f, mud);
            Ellipse(root, "Озеро", LakeAt, LakeRadius * 2f, 0.03f, water);
            // Камыш у моста: видно, где вода, а где настил.
            foreach (float s in new[] { -1f, 1f })
                for (float z = 21f; z < 37f; z += R(0.8f, 1.5f))
                {
                    if (Mathf.Abs(z - BridgeAt.z) < DeckHalf + 0.6f) continue;
                    var at = new Vector3(BridgeAt.x + s * (RiverHalf + R(0.1f, 0.6f)), 0f, z);
                    for (int k = 0; k < 3; k++)
                    {
                        float h = R(0.5f, 0.95f);
                        var stem = Piece(root, PrimitiveType.Cylinder, "Камыш", at + new Vector3(R(-0.25f, 0.25f), h, R(-0.25f, 0.25f)), new Vector3(0.05f, h, 0.05f), reed, false);
                        stem.transform.rotation = Quaternion.Euler(R(-12f, 12f), 0f, R(-12f, 12f));
                    }
                }
            // Вода — «не пройти». Верхнее колено идёт под мостом: объём в обход настила.
            RiverVolume(root, River[0], new Vector2(River[0].x, BridgeAt.z + DeckHalf), true, false);
            RiverVolume(root, new Vector2(River[1].x, BridgeAt.z - DeckHalf), River[1], false, true);
            for (int i = 1; i + 1 < River.Length; i++) RiverVolume(root, River[i], River[i + 1], true, true);
            NoWalk(root, "Озеро — не пройти", new Vector3(LakeAt.x, 0f, LakeAt.y), new Vector3(LakeRadius.x * 2f, 6f, LakeRadius.y * 2f));

            // ── расщелина: чёрная щель с рваными краями; пройти — только по бревну ──
            Strip(root, "Расщелина", new Vector3(ChasmX, 0f, MapSouth), new Vector3(ChasmX, 0f, ChasmNorth), ChasmHalf * 2f, 0.04f, abyss, 0f);
            foreach (float s in new[] { -1f, 1f })
                for (float z = MapSouth; z < ChasmNorth;)
                {
                    z += R(0.9f, 1.5f);
                    float sx = R(0.7f, 1.4f), sy = R(0.25f, 0.65f), sz = R(0.8f, 1.5f), off = R(-0.15f, 0.35f);
                    var turn = Quaternion.Euler(R(-10f, 10f), R(0f, 90f), -s * R(5f, 15f));
                    var stone = stones[rnd.Next(stones.Length)];
                    if (Mathf.Abs(z - LogAt.z) < LogHalf + 0.5f) continue;
                    Piece(root, PrimitiveType.Cube, "Край расщелины", new Vector3(ChasmX + s * (ChasmHalf + off), sy * 0.3f, z), new Vector3(sx, sy, sz), stone)
                        .transform.rotation = turn;
                }
            float lo = LogAt.z - LogHalf, hi = LogAt.z + LogHalf;
            NoWalk(root, "Расщелина — не пройти", new Vector3(ChasmX, 0f, (MapSouth + lo) * 0.5f), new Vector3(ChasmHalf * 2f + 0.5f, 6f, lo - MapSouth));
            NoWalk(root, "Расщелина — не пройти", new Vector3(ChasmX, 0f, (hi + ChasmNorth) * 0.5f), new Vector3(ChasmHalf * 2f + 0.5f, 6f, ChasmNorth - hi));
            // Бревно — три ствола, связанные верёвкой; верх — чуть выше земли, шагу не мешает.
            float logLen = ChasmHalf * 2f + 2.6f;
            foreach (float dz in new[] { -0.92f, 0f, 0.92f })
                Piece(root, PrimitiveType.Cylinder, "Бревно через расщелину", new Vector3(ChasmX + R(-0.25f, 0.25f), -0.2f, LogAt.z + dz),
                      new Vector3(0.92f, logLen * 0.5f + R(-0.15f, 0.25f), 0.92f), bark)
                    .transform.rotation = Quaternion.Euler(0f, R(-1.5f, 1.5f), 90f);
            foreach (float x in new[] { ChasmX - ChasmHalf - 0.55f, ChasmX + ChasmHalf + 0.55f })
                Piece(root, PrimitiveType.Cube, "Вязка", new Vector3(x, -0.2f, LogAt.z), new Vector3(0.14f, 1.0f, 2.9f), rope, false);

            // ── мост с рычагом ──
            _bridge = new GameObject("Мост").transform;
            _bridge.SetParent(root, false);
            _bridge.position = new Vector3(DeckEast, 0f, BridgeAt.z);
            float deckLen = DeckEast - DeckWest, deckMid = (DeckWest + DeckEast) * 0.5f;
            Piece(_bridge, PrimitiveType.Cube, "Настил моста", new Vector3(deckMid, 0.06f, BridgeAt.z), new Vector3(deckLen, 0.2f, DeckHalf * 2f), wood);
            for (float x = DeckWest + 0.3f; x < DeckEast - 0.2f; x += 0.62f)
                Piece(_bridge, PrimitiveType.Cube, "Доска", new Vector3(x, 0.166f, BridgeAt.z + R(-0.06f, 0.06f)), new Vector3(0.5f, 0.02f, DeckHalf * 2f - 0.12f), plank, false);
            foreach (float s in new[] { -1f, 1f })
            {
                float z = BridgeAt.z + s * (DeckHalf - 0.15f);
                for (float x = DeckWest + 0.2f; x <= DeckEast; x += 2.1f)
                    Piece(_bridge, PrimitiveType.Cube, "Столб перил", new Vector3(x, 0.7f, z), new Vector3(0.18f, 1.1f, 0.18f), wood);
                Piece(_bridge, PrimitiveType.Cube, "Перила", new Vector3(deckMid, 1.18f, z), new Vector3(deckLen, 0.1f, 0.12f), wood);
            }
            foreach (float x in new[] { BridgeAt.x - RiverHalf - 0.4f, BridgeAt.x + RiverHalf + 0.4f })
                Piece(root, PrimitiveType.Cube, "Устой моста", new Vector3(x, -0.1f, BridgeAt.z), new Vector3(1.3f, 0.4f, DeckHalf * 2f + 0.8f), dark);
            // Ворот на восточном берегу: два столба с перекладиной, цепи — к дальнему краю настила.
            float gx = DeckEast + 0.5f;
            foreach (float s in new[] { -1f, 1f })
                Piece(root, PrimitiveType.Cube, "Столб ворота", new Vector3(gx, 2.1f, BridgeAt.z + s * (DeckHalf + 0.35f)), new Vector3(0.4f, 4.2f, 0.4f), wood);
            Piece(root, PrimitiveType.Cube, "Перекладина ворота", new Vector3(gx, 4.25f, BridgeAt.z), new Vector3(0.45f, 0.4f, DeckHalf * 2f + 1.3f), wood);
            // Цепи держат настил за треть длины: поднятый мост висит на них почти ровно.
            for (int i = 0; i < 2; i++)
            {
                float z = BridgeAt.z + (i == 0 ? -1f : 1f) * (DeckHalf - 0.3f);
                var end = new Vector3(DeckEast - 4.2f, 0.3f, z);
                _chainTop[i] = new Vector3(gx, 4.1f, z);
                _chainEnd[i] = _bridge.InverseTransformPoint(end);
                _chains[i] = Rod(root, "Цепь моста", _chainTop[i], end, 0.05f, iron).transform;
            }
            // Поднят — над рекой прохода нет: навмеш вырезается, пока настил не ляжет обратно.
            var gap = new GameObject("Мост поднят — прохода нет").transform;
            gap.SetParent(root, false);
            float gapW = BridgeAt.x - RiverHalf - 0.4f, gapE = BridgeAt.x + RiverHalf + 0.8f;
            gap.position = new Vector3((gapW + gapE) * 0.5f, 0.6f, BridgeAt.z);
            _gap = gap.gameObject.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            _gap.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            _gap.size = new Vector3(gapE - gapW, 2.4f, DeckHalf * 2f + 0.8f);
            _gap.carving = true;
            _gap.carveOnlyStationary = false;
            _gap.enabled = false;
            var lever = new GameObject("Рычаг моста").transform;
            lever.SetParent(root, false);
            lever.position = LeverAt;
            Piece(lever, PrimitiveType.Cube, "Колода рычага", LeverAt + Vector3.up * 0.3f, new Vector3(0.7f, 0.6f, 0.55f), dark);
            _leverArm = new GameObject("Рукоять").transform;
            _leverArm.SetParent(lever, false);
            _leverArm.position = LeverAt + Vector3.up * 0.6f;
            Piece(_leverArm, PrimitiveType.Cylinder, "Рукоять рычага", LeverAt + Vector3.up * 1.15f, new Vector3(0.09f, 0.55f, 0.09f), iron, false);
            Piece(_leverArm, PrimitiveType.Sphere, "Набалдашник", LeverAt + Vector3.up * 1.72f, Vector3.one * 0.16f, iron, false);
            _leverArm.rotation = Quaternion.Euler(-35f, 0f, 0f);   // опущен: мост лежит

            // ── опушка: круг стоячих камней, отсюда путь на восток ──
            foreach (float deg in new[] { 75f, 115f, 155f, 180f, 205f, 245f, 285f })
            {
                float a = deg * Mathf.Deg2Rad, h = R(1.3f, 2.4f);
                var at = new Vector3(Glades[0].x + Mathf.Cos(a) * 4.6f, h * 0.5f, Glades[0].z + Mathf.Sin(a) * 4.6f);
                var menhir = Piece(root, PrimitiveType.Cube, "Стоячий камень", at, new Vector3(R(0.6f, 0.85f), h, R(0.4f, 0.55f)), stones[rnd.Next(stones.Length)]);
                menhir.transform.rotation = Quaternion.Euler(R(-6f, 6f), -deg + 90f, R(-6f, 6f));
            }

            // ── стоянка убийц: огонь ещё горит, у дороги их чучело ──
            var pit = CampAt + new Vector3(-2.8f, 0f, 1.6f);
            for (int i = 0; i < 9; i++)
            {
                float a = i * 40f * Mathf.Deg2Rad;
                Piece(root, PrimitiveType.Cube, "Камень очага", pit + new Vector3(Mathf.Cos(a) * 0.75f, 0.1f, Mathf.Sin(a) * 0.75f),
                      new Vector3(0.32f, 0.22f, 0.28f), stones[rnd.Next(stones.Length)], false).transform.rotation = Quaternion.Euler(0f, R(0f, 90f), 0f);
            }
            Piece(root, PrimitiveType.Sphere, "Угли", pit + Vector3.up * 0.04f, new Vector3(1.0f, 0.12f, 1.0f), Glowing(new Color(1f, 0.30f, 0.08f), 1.1f), false);
            Flame(root, "Огонь стоянки", pit + Vector3.up * 0.32f, new Vector3(0.38f, 0.55f, 0.38f), new Color(1f, 0.55f, 0.22f), 1.1f);
            var fire = new GameObject("Свет стоянки");
            fire.transform.SetParent(root, false);
            fire.transform.position = pit + Vector3.up * 0.9f;
            var fl = fire.AddComponent<Light>();
            fl.type = LightType.Point;
            fl.color = new Color(1f, 0.6f, 0.3f);
            fl.range = 10f;
            fl.intensity = 6f;
            fl.shadows = LightShadows.None;
            foreach (var (dx, dz, yaw) in new[] { (-1.9f, -0.2f, 100f), (0.3f, 1.9f, 5f) })
                Piece(root, PrimitiveType.Cylinder, "Бревно у огня", pit + new Vector3(dx, 0.24f, dz), new Vector3(0.48f, 0.85f, 0.48f), bark)
                    .transform.rotation = Quaternion.Euler(0f, yaw, 90f);
            Piece(root, PrimitiveType.Cube, "Подстилка", CampAt + new Vector3(4.6f, 0.06f, 3.0f), new Vector3(0.85f, 0.12f, 1.9f),
                  Lit(new Color(0.20f, 0.09f, 0.07f), 0.05f), false).transform.rotation = Quaternion.Euler(0f, 18f, 0f);
            Piece(root, PrimitiveType.Sphere, "Мешок", CampAt + new Vector3(4.4f, 0.3f, 1.2f), new Vector3(0.6f, 0.6f, 0.55f),
                  Lit(new Color(0.32f, 0.26f, 0.18f), 0.05f));

            // ── блуждающие огни лесной тропы (26.11): лес был чёрным, обход поднятого моста не читался глазом.
            // Холодные, у края тропы, через 14 м — путь видно и без стрелки; ночь остаётся ночью.
            var wisp = Glowing(new Color(0.55f, 0.82f, 1f), 1.3f);
            float since = 0f;
            for (int i = 0; i + 1 < ForestTrail.Length; i++)
            {
                var a = new Vector3(ForestTrail[i].x, 0f, ForestTrail[i].y);
                var b = new Vector3(ForestTrail[i + 1].x, 0f, ForestTrail[i + 1].y);
                float len = Flat(b - a).magnitude;
                var side = Vector3.Cross(Vector3.up, (b - a).normalized);
                for (float s = 14f - since; s < len; s += 14f)
                {
                    int n = Mathf.RoundToInt(s);
                    var at = Vector3.Lerp(a, b, s / len) + side * ((n & 1) == 0 ? 2.3f : -2.3f) + Vector3.up * R(1.2f, 1.7f);
                    Piece(root, PrimitiveType.Sphere, "Блуждающий огонь", at, Vector3.one * 0.2f, wisp, false);
                    var go = new GameObject("Свет огня тропы");
                    go.transform.SetParent(root, false);
                    go.transform.position = at;
                    var l = go.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = new Color(0.55f, 0.82f, 1f);
                    l.range = 7f;
                    l.intensity = 2.4f;
                    l.shadows = LightShadows.None;
                    since = len - s;
                }
                since = Mathf.Repeat(since, 14f);
            }
            // ── засада: валуны на краях поляны — за ними прячутся ──
            foreach (var (dx, dz, sc) in new[] { (-4.6f, 2.4f, 2.2f), (-4.1f, -3.3f, 1.8f), (4.3f, -0.8f, 2.0f) })
            {
                var boulder = Piece(root, PrimitiveType.Sphere, "Валун", AmbushAt + new Vector3(dx, sc * 0.38f, dz),
                                    new Vector3(sc * R(1.0f, 1.3f), sc * 0.9f, sc), stones[rnd.Next(stones.Length)]);
                boulder.transform.rotation = Quaternion.Euler(0f, R(0f, 180f), 0f);
            }

            // ── колонны у площади: начало привычной дороги к склепу ──
            Column(root, new Vector3(PlazaWest + 1.3f, 0f, PlazaSouth + 0.9f), 4.6f, false, pillar, dark, rnd);
            Column(root, new Vector3(PlazaWest + 1.3f, 0f, PlazaNorth - 0.9f), 2.9f, true, pillar, dark, rnd);
            foreach (float z in new[] { PlazaSouth + 0.7f, PlazaNorth - 0.7f })
                Column(root, new Vector3(PlazaEast - 1.0f, 0f, z), 5.4f, false, pillar, dark, rnd);
            Piece(root, PrimitiveType.Cube, "Перемычка колонн", new Vector3(PlazaEast - 1.0f, 6.45f, (PlazaSouth + PlazaNorth) * 0.5f),
                  new Vector3(1.0f, 0.55f, PlazaNorth - PlazaSouth), pillar);
            Brazier(root, new Vector3(PlazaWest + 3.2f, 0f, PlazaSouth + 1.1f), new Color(1f, 0.62f, 0.30f), 8f, iron);

            // ── свечи вдоль главного пути — через тринадцать шагов, то слева, то справа ──
            float lastCandle = Progress(new Vector3(PlazaWest, 0f, LogAt.z)) - 10f;
            int nth = 0;
            for (float s = 9f; s < lastCandle; s += 13f, nth++)
            {
                var p = RoutePoint(s);
                var f = RouteForward(s);
                var at = p + new Vector3(f.z, 0f, -f.x) * (nth % 2 == 0 ? 2.8f : -2.8f);
                if (Flat(at - CampAt).magnitude < 7.5f || Flat(at - AmbushAt).magnitude < 7f || Flat(at - DyingAt).magnitude < 3f) continue;
                if (at.x > DeckWest - 1.5f && at.x < DeckEast + 1.5f && Mathf.Abs(at.z - BridgeAt.z) < 4f) continue;
                AddCandle(root, at, post, 3.2f);
            }
        }

        /// <summary>
        /// Земля сна — плоскость вокруг старой дороги (80 × 110 м); карта западнее. Плоскость растягиваем на
        /// карту — по ней печётся навмеш и по её рамке ставится туман войны (FogOfWar ставится после Awake),
        /// а плитку текстуры сохраняем прежней.
        /// </summary>
        private static void Widen(out float oldEast, out float oldSouth)
        {
            oldEast = -40f; oldSouth = -41f;
            var ground = GameObject.Find("Старая дорога");
            var r = ground != null ? ground.GetComponent<Renderer>() : null;
            if (r == null) { Debug.LogWarning("[СОН] Земля «Старая дорога» не найдена — карта обучения без земли."); return; }
            var b = r.bounds;
            oldEast = b.min.x; oldSouth = b.min.z;
            if (b.min.x <= MapWest) return;   // уже широка
            float x0 = MapWest, x1 = b.max.x, z0 = Mathf.Min(b.min.z, MapSouth), z1 = Mathf.Max(b.max.z, ChasmNorth);
            var t = ground.transform;
            var tiling = r.sharedMaterial != null ? r.sharedMaterial.mainTextureScale : Vector2.one;
            var m = r.material;   // свой экземпляр: плитка — под новый размер
            m.mainTextureScale = new Vector2(tiling.x * (x1 - x0) / b.size.x, tiling.y * (z1 - z0) / b.size.z);
            t.position = new Vector3((x0 + x1) * 0.5f, t.position.y, (z0 + z1) * 0.5f);
            t.localScale = new Vector3(t.localScale.x * (x1 - x0) / b.size.x, t.localScale.y, t.localScale.z * (z1 - z0) / b.size.z);
        }

        /// <summary>Туман войны: дороги карты разведаны с начала, как старая дорога.</summary>
        private static void RevealMap()
        {
            foreach (var l in Lanes)
            {
                var a = new Vector3(l.A.x, 0f, l.A.y);
                var b = new Vector3(l.B.x, 0f, l.B.y);
                float len = Mathf.Max((b - a).magnitude, 0.01f);
                for (float t = 0f; t <= len; t += 4f) FogOfWar.Reveal(Vector3.Lerp(a, b, t / len), 13f);
            }
            FogOfWar.Reveal(new Vector3(LakeAt.x, 0f, LakeAt.y), 13f);
            FogOfWar.Reveal(new Vector3((PlazaWest + PlazaEast) * 0.5f, 0f, LogAt.z), 12f);
        }

        // ───────────────────────────── детали ─────────────────────────────

        /// <summary>Кусок края дороги от клетки a до клетки b (включительно): валежина, груда камней или кусты.</summary>
        private static void Windfall(Transform root, Vector3 a, Vector3 b, System.Random rnd, Material bark, Material[] stones, Material leaf)
        {
            float R(float lo, float hi) => lo + (float)rnd.NextDouble() * (hi - lo);
            var dir = (b - a).normalized;
            float len = (b - a).magnitude + 1f;
            var mid = (a + b) * 0.5f;
            var side = Vector3.Cross(Vector3.up, dir);
            double kind = rnd.NextDouble();
            if (kind < 0.55)
            {
                // Лежачий ствол выше шага агента (0,75 м) — не перешагнуть. Тоньше метра — перешагивали (прогон 19.11).
                float r = R(0.53f, 0.62f);
                Piece(root, PrimitiveType.Cylinder, "Валежина", mid + Vector3.up * r, new Vector3(r * 2f, len * 0.5f + 0.3f, r * 2f), bark)
                    .transform.rotation = Quaternion.Euler(0f, R(-7f, 7f), 0f) * Quaternion.FromToRotation(Vector3.up, dir);
                if (rnd.NextDouble() < 0.35)
                {
                    // Вывороченный корень на конце — силуэт выше края, виден издали.
                    var end = mid + dir * (len * 0.5f + 0.2f) * (rnd.NextDouble() < 0.5 ? 1f : -1f);
                    Piece(root, PrimitiveType.Cylinder, "Корневище", end + Vector3.up * 0.72f, new Vector3(1.6f, 0.12f, 1.45f), bark, false)
                        .transform.rotation = Quaternion.FromToRotation(Vector3.up, dir) * Quaternion.Euler(0f, R(0f, 360f), 0f);
                }
                return;
            }
            var start = mid - dir * len * 0.5f;
            if (kind < 0.8)
            {
                int count = Mathf.Max(2, Mathf.RoundToInt(len / 1.2f));
                for (int i = 0; i < count; i++)
                {
                    float h = R(1.05f, 1.4f);
                    var at = start + dir * (len * (i + 0.5f) / count) + side * R(-0.2f, 0.2f);
                    Piece(root, PrimitiveType.Cube, "Камни у дороги", at + Vector3.up * h * 0.5f, new Vector3(R(0.9f, 1.3f), h, R(0.9f, 1.3f)), stones[rnd.Next(stones.Length)])
                        .transform.rotation = Quaternion.Euler(R(-8f, 8f), R(0f, 90f), R(-8f, 8f));
                }
                return;
            }
            int bushes = Mathf.Max(2, Mathf.RoundToInt(len / 1.1f));
            for (int i = 0; i < bushes; i++)
            {
                float h = R(1.2f, 1.6f);
                var at = start + dir * (len * (i + 0.5f) / bushes) + side * R(-0.2f, 0.2f);
                Piece(root, PrimitiveType.Sphere, "Куст", at + Vector3.up * h * 0.4f, new Vector3(R(1.1f, 1.5f), h, R(1.1f, 1.5f)), leaf);
            }
        }

        private void Column(Transform root, Vector3 at, float h, bool broken, Material stone, Material dark, System.Random rnd)
        {
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            Piece(root, PrimitiveType.Cube, "База колонны", at + Vector3.up * 0.2f, new Vector3(1.4f, 0.4f, 1.4f), dark);
            Piece(root, PrimitiveType.Cylinder, "Колонна", at + Vector3.up * (0.4f + h * 0.5f), new Vector3(0.85f, h * 0.5f, 0.85f), stone);
            if (!broken)
            {
                Piece(root, PrimitiveType.Cube, "Капитель", at + Vector3.up * (0.4f + h + 0.18f), new Vector3(1.25f, 0.36f, 1.25f), dark);
                return;
            }
            // Обломанная: верхний барабан лежит рядом, за краем площади.
            Piece(root, PrimitiveType.Cube, "Скол колонны", at + Vector3.up * (0.4f + h), new Vector3(0.7f, 0.3f, 0.6f), stone, false)
                .transform.rotation = Quaternion.Euler(R(12f, 22f), R(0f, 90f), R(-10f, 10f));
            Piece(root, PrimitiveType.Cylinder, "Барабан колонны", at + new Vector3(-0.4f, 0.43f, 1.7f), new Vector3(0.85f, 0.8f, 0.85f), stone)
                .transform.rotation = Quaternion.Euler(0f, R(60f, 120f), 90f);
        }

        /// <summary>Полоса по земле от a до b шириной w на высоте y; extend — запас на концах (стыки на поворотах).</summary>
        private static void Strip(Transform parent, string name, Vector3 a, Vector3 b, float w, float y, Material m, float extend)
        {
            var d = Flat(b - a);
            var go = Piece(parent, PrimitiveType.Quad, name, (a + b) * 0.5f + Vector3.up * y, new Vector3(w, d.magnitude + extend * 2f, 1f), m, false);
            go.transform.rotation = Quaternion.Euler(90f, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 0f);
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static void Disc(Transform parent, string name, Vector3 at, float diameter, Material m)
            => Ellipse(parent, name, new Vector2(at.x, at.z), new Vector2(diameter, diameter), at.y, m);

        private static void Ellipse(Transform parent, string name, Vector2 at, Vector2 size, float y, Material m)
        {
            var go = Piece(parent, PrimitiveType.Cylinder, name, new Vector3(at.x, y, at.y), new Vector3(size.x, 0.002f, size.y), m, false);
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Тонкий стержень (цепь, жердь) от a до b.</summary>
        // ── подъёмный мост (этап 2–3, 19.11) ──
        // Рычаг общий для всех: кто дошёл до него по приказу (и ноги ведут к рычагу) — тот и тянет.
        // Убийц ведёт сон — они тянут через PullLever. Мост поднят — над рекой навмеш вырезан.
        public const float BridgeLiftDeg = 72f;
        public static readonly Vector3 LeverStand = LeverAt + new Vector3(0f, 0f, -1.1f);
        public static readonly Vector3 BridgeWestEnd = new Vector3(DeckWest, 0f, BridgeAt.z);
        private readonly Transform[] _chains = new Transform[2];
        private readonly Vector3[] _chainTop = new Vector3[2], _chainEnd = new Vector3[2];
        private UnityEngine.AI.NavMeshObstacle _gap;
        private float _bridgeLift, _bridgeGoal, _leverSwing, _nearAt;
        private Warrior[] _near = new Warrior[0];
        private readonly System.Collections.Generic.Dictionary<Warrior, uint> _leverServed =
            new System.Collections.Generic.Dictionary<Warrior, uint>();

        public static bool BridgeRaised => Instance != null && Instance._bridgeGoal > 0.5f;
        public static bool BridgeFullyUp => Instance != null && Instance._bridgeLift > 0.98f;
        public static bool BridgeFullyDown => Instance == null || (Instance._bridgeGoal < 0.5f && Instance._bridgeLift < 0.02f);
        /// <summary>Над рекой (под настилом) — тут стоящему мост не поднять.</summary>
        public static bool OnRiverSpan(Vector3 p)
            => p.x > BridgeAt.x - RiverHalf - 0.6f && p.x < BridgeAt.x + RiverHalf + 1.5f && Mathf.Abs(p.z - BridgeAt.z) < DeckHalf + 0.8f;
        /// <summary>Перешёл реку по мосту — на восточном берегу.</summary>
        public static bool PastRiver(Vector3 p) => p.x > BridgeAt.x + RiverHalf + 1.5f;
        // 2,4 м: ноги встают у колоды в 1,8 м от её середины (прогон 19.11: 1,80 — «не дошёл»).
        public static bool AtLever(Vector3 p, float r = 2.4f) { var d = p - LeverAt; d.y = 0f; return d.magnitude < r; }

        /// <summary>Лесная тропа (ветка отказа): от развилки серпантином в обход реки и озера — к бревну.</summary>
        private static readonly Vector2[] ForestTrail =
        {
            new Vector2(-70f, 29f), new Vector2(-70f, 1f), new Vector2(-124f, 1f), new Vector2(-124f, -17f),
            new Vector2(-67f, -17f), new Vector2(-67f, -34f), new Vector2(-31f, -34f),
        };

        /// <summary>
        /// Куда идти в обход реки, когда мост поднят: с дороги у стоянки — в начало лесной тропы, с тропы — к её
        /// следующему повороту. null — уже за рекой и озером: к цели прямо.
        /// </summary>
        public static Vector3? Detour(Vector3 p)
        {
            if (p.x > -36f) return null;   // восточнее озера и реки
            var q = new Vector2(p.x, p.z);
            int best = -1;
            float bestD = 4.5f;
            for (int i = 0; i + 1 < ForestTrail.Length; i++)
            {
                var a = ForestTrail[i]; var ab = ForestTrail[i + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude);
                float d = (a + ab * t - q).magnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            if (best >= 0)
            {
                int next = best + 1;
                if ((ForestTrail[next] - q).magnitude < 3.5f && next + 1 < ForestTrail.Length) next++;
                return new Vector3(ForestTrail[next].x, 0f, ForestTrail[next].y);
            }
            if (p.x < BridgeAt.x - RiverHalf && p.z > 15f) return new Vector3(ForestTrail[0].x, 0f, ForestTrail[0].y - 6f);
            return null;
        }

        public void PullLever(string who)
        {
            _bridgeGoal = _bridgeGoal > 0.5f ? 0f : 1f;
            Debug.Log($"[СОН] Рычаг моста: {who} — мост {(_bridgeGoal > 0.5f ? "поднимается" : "опускается")}");
        }

        private void BridgeTick(float dt)
        {
            if (_bridge == null) return;
            LeverCalls();
            _leverSwing = Mathf.MoveTowards(_leverSwing, _bridgeGoal, dt / 0.45f);
            _leverArm.rotation = Quaternion.Euler(Mathf.Lerp(-35f, 35f, Mathf.SmoothStep(0f, 1f, _leverSwing)), 0f, 0f);
            // Настил трогается, когда рычаг дошёл.
            if (Mathf.Abs(_leverSwing - _bridgeGoal) < 0.01f)
                _bridgeLift = Mathf.MoveTowards(_bridgeLift, _bridgeGoal, dt / 2.6f);
            _bridge.rotation = Quaternion.Euler(0f, 0f, -BridgeLiftDeg * Mathf.SmoothStep(0f, 1f, _bridgeLift));
            for (int i = 0; i < _chains.Length; i++)
                if (_chains[i] != null) PlaceRod(_chains[i], _chainTop[i], _bridge.TransformPoint(_chainEnd[i]));
            bool cut = _bridgeGoal > 0.5f || _bridgeLift > 0.02f;
            if (_gap != null && _gap.enabled != cut) _gap.enabled = cut;
        }

        private void LeverCalls()
        {
            if (Time.time > _nearAt)
            {
                _nearAt = Time.time + 0.8f;
                _near = FindObjectsByType<Warrior>(FindObjectsSortMode.None);
            }
            foreach (var w in _near)
            {
                if (w == null || w.IsDead || !w.HasCommand) continue;
                var c = w.Command;
                if (c.Kind != CommandKind.Move || !AtLever(c.Point, 3f)) continue;
                uint revision = w.CommandRevision;
                bool alreadyServed = _leverServed.TryGetValue(w, out uint served) && served == revision;
                // The effective decision (including an accepted whisper) must belong to this order.
                // A whisper flag alone or a stale vote cannot operate the physical lever.
                var mind = w.GetComponent<AOS.AOSWarriorWrapper>();
                if (mind == null) continue;
                var d = mind.LastDecisionDetail;
                if (!DreamTutorialChecks.MayOperateLever(alreadyServed, mind.HasCurrentCommandDecision,
                    d.RefusedCommand, d.Hesitated, d.Action == AOS.ActionType.ObeyCommand)) continue;
                if (!AtLever(w.transform.position)) continue;   // ещё не дошёл
                // Ноги ведут не к рычагу — душа решила другое (гонится, отступает): мимо рычага не тянут.
                var legs = w.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (legs != null && legs.enabled && legs.hasPath && !AtLever(legs.destination, 3f)) continue;
                _leverServed[w] = revision;
                PullLever(w.DisplayName);
            }
        }

        private static void PlaceRod(Transform rod, Vector3 a, Vector3 b)
        {
            rod.position = (a + b) * 0.5f;
            rod.rotation = Quaternion.FromToRotation(Vector3.up, b - a);
            var s = rod.localScale;
            s.y = (b - a).magnitude * 0.5f;
            rod.localScale = s;
        }

        private static GameObject Rod(Transform parent, string name, Vector3 a, Vector3 b, float thick, Material m)
        {
            var go = Piece(parent, PrimitiveType.Cylinder, name, (a + b) * 0.5f, new Vector3(thick, (b - a).magnitude * 0.5f, thick), m, false);
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, b - a);
            return go;
        }

        /// <summary>Объём «не пройти» навмеша (печётся вместе с землёй).</summary>
        private static void NoWalk(Transform parent, string name, Vector3 center, Vector3 size, float yaw = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, yaw, 0f));
            var v = go.AddComponent<NavMeshModifierVolume>();
            v.center = Vector3.zero;
            v.size = size;
            v.area = UnityEngine.AI.NavMesh.GetAreaFromName("Not Walkable");
        }

        /// <summary>Колено реки — «не пройти»; на открытых концах — запас, чтобы на изгибах не было щелей.</summary>
        private static void RiverVolume(Transform parent, Vector2 a, Vector2 b, bool padA, bool padB)
        {
            var d = (b - a).normalized;
            if (padA) a -= d * 1.5f;
            if (padB) b += d * 1.5f;
            var mid = (a + b) * 0.5f;
            NoWalk(parent, "Река — не пройти", new Vector3(mid.x, 0f, mid.y), new Vector3(RiverHalf * 2f + 0.4f, 6f, (b - a).magnitude),
                   Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg);
        }
    }
}
