// Assets/Scripts/Gameplay/DreamRoad.cs
// Перевод: строк нет
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Дорога сна (docs/64-DREAM-ROAD.md; автор, 07.11: «чтобы игроку нужно было идти по дороге
    /// и выполнять обучения. В конце пути игрок открывает большие ворота склепа»).
    ///
    /// Строится при запуске сна, до выпечки навмеша (<see cref="GroundNavMesh"/> печёт в Start,
    /// мы — в Awake сна): обочины из сухой кладки держат Греховода на дороге. Старый форт сцены
    /// прячется. С юга на север: начало дороги, умирающий и убийцы (суд души), полуразрушенный
    /// форт (автор, 07.11: «чтобы игрок проходил через полуразрушенный форт по дороге в большой
    /// склеп») — во дворе соломенное чучело и камень с банками, — пролом в задней стене, склеп.
    /// Склеп виден с первого шага — над фортом, у дверей холодная щель света. Свечи вдоль дороги
    /// загораются, когда Греховод подходит.
    ///
    /// Ворота (<see cref="OpenCrypt"/>): створки расходятся внутрь, экран гаснет раньше, чем
    /// успеешь рассмотреть склеп, — видно только силуэт Бога Смерти против холодного света.
    /// </summary>
    public class DreamRoad : MonoBehaviour
    {
        public static DreamRoad Instance { get; private set; }

        public const float From = -31f, GateZ = 30f, Half = 7f;
        /// <summary>Форт: передняя стена с воротами, задняя с проломом, полуширина двора.</summary>
        public const float FortFront = 14f, FortBack = 24f, FortHalf = 13f;
        public static readonly Vector3 DummyAt = new Vector3(4.2f, 0f, 17.5f);
        public static readonly Vector3 ShrineAt = new Vector3(-4.6f, 0f, 20.5f);
        public static readonly Vector3 GateFront = new Vector3(0f, 0f, 28.4f);

        /// <summary>Ворота склепа: −1 — не открывали; 0…1 — сколько погас экран. Спрашивает прогон.</summary>
        public static float CryptFade { get; private set; } = -1f;

        private class Candle { public Vector3 At; public Transform Flame; public Light Light; public float Lit = -1f; public float Power; public Vector3 Size; }
        private readonly List<Candle> _candles = new List<Candle>();
        private Transform _dummy, _doorLeft, _doorRight;
        private GameObject _seam;
        private Light _cryptLight;
        private Quaternion _dummyRest = Quaternion.identity;
        private Vector2 _tilt, _tiltVel;      // наклон чучела, градусы по x и z мира
        private float _spin, _spinVel;        // поворот на коле
        private readonly List<GameObject> _soulJar = new List<GameObject>();

        // Силуэт и его жизнь — Death, Haunt.
        private Transform _death, _deathHood, _deathArm, _deathScythe, _deathHem;
        private Vector3 _deathAt;
        private readonly List<Transform> _deathEyes = new List<Transform>();
        private ParticleSystem _deathSmoke, _cryptMist;
        private Material _backGlow;
        private Color _backColour;

        /// <summary>Сколько раз удар Греховода пришёлся в чучело. Урок считает попадания, а не взмахи.</summary>
        public static int DummyHits { get; private set; }

        /// <summary>Банку с душой забрали с камня.</summary>
        public bool SoulJarTaken { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() { CryptFade = -1f; DummyHits = 0; }

        private void OnDestroy()
        {
            FirstPersonCombat.Swung -= OnSwung;
            if (Instance == this) Instance = null;
        }

        /// <summary>Построить дорогу. Зовёт сон из Awake — до выпечки навмеша.</summary>
        public static DreamRoad Build()
        {
            if (Instance != null) return Instance;
            CryptFade = -1f;
            var fort = GameObject.Find("Стены и ворота старого форта");
            if (fort != null) fort.SetActive(false);

            var road = new GameObject("Дорога сна").AddComponent<DreamRoad>();
            Instance = road;
            var t = road.transform;
            var rnd = new System.Random(7117);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

            var earth = Lit(new Color(0.11f, 0.09f, 0.075f), 0.05f);
            var rut = Lit(new Color(0.075f, 0.06f, 0.05f), 0.05f);
            var bark = Lit(new Color(0.07f, 0.06f, 0.06f), 0.05f);

            // Полотно и колеи — без коллайдеров: по ним ходят, как по земле.
            float len = GateZ - From;
            Flat(t, "Полотно дороги", new Vector3(0f, 0.012f, From + len * 0.5f), new Vector2(4.8f, len), earth);
            foreach (float s in new[] { -1f, 1f })
                Flat(t, "Колея", new Vector3(0.8f * s, 0.016f, From + len * 0.5f), new Vector2(0.35f, len), rut);

            // Обочины — сухая кладка выше шага агента: с дороги не сойти. Камни внахлёст.
            var stones = new Material[4];
            for (int i = 0; i < stones.Length; i++)
            {
                float tone = 0.16f + i * 0.025f;
                stones[i] = Lit(new Color(tone, tone * 1.02f, tone * 1.08f), 0.1f);
            }
            // До форта и от форта до склепа; во дворе дорогу держат стены форта.
            foreach (float s in new[] { -1f, 1f })
                foreach (var (z0, z1) in new[] { (From - 0.5f, FortFront), (FortBack, GateZ + 0.5f) })
                {
                    for (float z = z0; z < z1;)
                    {
                        float l = Mathf.Min(R(1.6f, 2.4f), z1 - z + 0.3f);
                        var stone = stones[rnd.Next(stones.Length)];
                        var rock = Piece(t, PrimitiveType.Cube, "Кладка", new Vector3(Half * s + R(-0.15f, 0.15f), 0f, z + l * 0.5f),
                                         new Vector3(R(0.8f, 1.0f), R(0.6f, 1.0f), l + 0.25f), stone);
                        rock.transform.position += Vector3.up * rock.transform.localScale.y * 0.5f;
                        rock.transform.rotation = Quaternion.Euler(0f, R(-5f, 5f), R(-3f, 3f));
                        z += l;
                    }
                }
            road.Fort(t, rnd, stones);
            // Позади — завал: назад дороги нет.
            for (float x = -Half; x <= Half; x += 1.7f)
            {
                var rock = Piece(t, PrimitiveType.Cube, "Завал", new Vector3(x, 0f, From - 0.8f + R(-0.2f, 0.2f)),
                                 new Vector3(2.0f, R(0.9f, 1.6f), 1.2f), stones[rnd.Next(stones.Length)]);
                rock.transform.position += Vector3.up * rock.transform.localScale.y * 0.5f;
                rock.transform.rotation = Quaternion.Euler(R(-6f, 6f), R(-20f, 20f), R(-6f, 6f));
            }

            // Мёртвые деревья за обочинами — силуэты в тумане.
            for (int i = 0; i < 16; i++)
            {
                float s = i % 2 == 0 ? -1f : 1f;
                var at = new Vector3(s * R(9.5f, 16f), 0f, Mathf.Lerp(From + 2f, GateZ - 3f, i / 15f) + R(-2f, 2f));
                road.Tree(t, at, R(4f, 6.5f), rnd, bark);
            }

            // Свечи на столбиках, через шесть шагов, то слева, то справа.
            var post = Lit(new Color(0.22f, 0.21f, 0.22f), 0.1f);
            for (int i = 0; i < 10; i++)
            {
                float s = i % 2 == 0 ? 1f : -1f;
                road.AddCandle(t, new Vector3(5.5f * s, 0f, From + 4f + i * 6f), post, 3.5f);
            }

            DummyHits = 0;
            FirstPersonCombat.Swung += road.OnSwung;
            road.BuildDummy(t, DummyAt);
            road.BuildShrine(t, ShrineAt, post);
            road.BuildCrypt(t);

            // Жаровня во дворе у чучела — тёплая; у склепа две — холодным огнём: цель видна издалека.
            var iron = Lit(new Color(0.12f, 0.11f, 0.11f), 0.4f);
            // Свет у точечных огней падает с квадратом расстояния: при силе 1–2 жаровня не доставала
            // и до соседнего камня, а ключевой свет сцены бьёт с севера, и склеп накрывает тенью весь
            // двор форта — двор на кадрах был чёрным (w0711zi, w0711zj). Отсюда сила 9–10.
            road.Brazier(t, new Vector3(6.4f, 0f, 16.2f), new Color(1f, 0.62f, 0.30f), 10f, iron);
            foreach (float s in new[] { -1f, 1f })
                road.Brazier(t, new Vector3(3.8f * s, 0f, GateZ - 1.7f), new Color(0.55f, 0.75f, 1f), 9f, iron);
            // Факелы на надвратных башнях — снаружи, к дороге, и один во двор.
            var warm = new Color(1f, 0.64f, 0.32f);
            foreach (float s in new[] { -1f, 1f })
                road.Torch(t, new Vector3(3.9f * s, 2.9f, FortFront - 1.3f), Vector3.back, warm, iron);
            road.Torch(t, new Vector3(-3.9f, 2.9f, FortFront + 1.3f), Vector3.forward, warm, iron);
            return road;
        }

        /// <summary>Жаровня: чаша на столбе, огонь горит всегда.</summary>
        private void Brazier(Transform parent, Vector3 at, Color fire, float power, Material iron)
        {
            var root = new GameObject("Жаровня").transform;
            root.SetParent(parent, false);
            root.position = at;
            Piece(root, PrimitiveType.Cylinder, "Столб жаровни", at + Vector3.up * 0.5f, new Vector3(0.14f, 0.5f, 0.14f), iron);
            Piece(root, PrimitiveType.Cylinder, "Чаша жаровни", at + Vector3.up * 1.05f, new Vector3(0.7f, 0.1f, 0.7f), iron);
            Piece(root, PrimitiveType.Sphere, "Угли", at + Vector3.up * 1.15f, new Vector3(0.55f, 0.12f, 0.55f), Glowing(fire * 0.6f, 1.2f), false);
            var flame = Flame(root, "Огонь", at + Vector3.up * 1.45f, new Vector3(0.36f, 0.6f, 0.36f), fire, 1.15f);
            var go = new GameObject("Свет жаровни");
            go.transform.SetParent(root, false);
            go.transform.position = at + Vector3.up * 1.9f;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = fire;
            light.range = 12f;
            light.intensity = power;
            light.shadows = LightShadows.None;
            _candles.Add(new Candle { At = at, Flame = flame, Light = light, Power = power, Lit = 0f, Size = new Vector3(0.36f, 0.6f, 0.36f) });
        }

        /// <summary>Факел на стене: древко наискось от кладки, пламя, свет чуть впереди.</summary>
        private void Torch(Transform parent, Vector3 at, Vector3 outward, Color fire, Material iron)
        {
            var root = new GameObject("Факел").transform;
            root.SetParent(parent, false);
            root.position = at;
            Piece(root, PrimitiveType.Cube, "Скоба", at, new Vector3(0.12f, 0.3f, 0.12f), iron);
            var stick = Piece(root, PrimitiveType.Cylinder, "Древко", at + outward * 0.18f + Vector3.up * 0.18f, new Vector3(0.07f, 0.28f, 0.07f),
                              Lit(new Color(0.22f, 0.15f, 0.09f), 0.1f), false).transform;
            stick.rotation = Quaternion.FromToRotation(Vector3.up, (Vector3.up + outward * 0.6f).normalized);
            var tip = at + outward * 0.34f + Vector3.up * 0.48f;
            var flame = Flame(root, "Пламя", tip, new Vector3(0.16f, 0.28f, 0.16f), fire, 1.15f);
            var go = new GameObject("Свет факела");
            go.transform.SetParent(root, false);
            go.transform.position = tip + outward * 0.4f;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = fire;
            light.range = 10f;
            light.intensity = 7f;
            light.shadows = LightShadows.None;
            _candles.Add(new Candle { At = at, Flame = flame, Light = light, Power = 7f, Lit = 0f, Size = new Vector3(0.16f, 0.28f, 0.16f) });
        }

        /// <summary>Прогон: зажечь все свечи разом — для кадров издалека.</summary>
        public void LightAll()
        {
            foreach (var c in _candles)
            {
                if (c.Lit < 0f) c.Lit = 0f;
                if (c.Light != null) c.Light.intensity = c.Power;
                if (c.Flame != null) c.Flame.localScale = c.Size;
            }
        }

        // ───────────────────────────── форт ─────────────────────────────

        /// <summary>
        /// Полуразрушенный форт поперёк дороги: передняя стена с воротами меж двух башен (одна
        /// обрушена), боковые стены с обвалами, задняя почти вся легла — сквозь пролом виден склеп.
        /// Обвалы не ниже 0,7 м: двор не выпускает Греховода с дороги.
        /// </summary>
        private void Fort(Transform parent, System.Random rnd, Material[] stones)
        {
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var root = new GameObject("Полуразрушенный форт").transform;
            root.SetParent(parent, false);
            var dressed = Lit(new Color(0.25f, 0.25f, 0.27f), 0.12f);
            var dark = Lit(new Color(0.17f, 0.17f, 0.19f), 0.1f);

            // Передняя стена: ворота ±2,6, справа большой обвал.
            WallX(root, FortFront, -FortHalf, -2.6f, 2.6f, 4.6f, rnd, dressed, dark, null);
            WallX(root, FortFront, 2.6f, FortHalf, 2.4f, 4.4f, rnd, dressed, dark, new Vector2(6f, 9.5f));
            // Боковые: слева обвал посередине.
            WallZ(root, -FortHalf, FortFront, FortBack, 1.8f, 4.0f, rnd, dressed, dark, new Vector2(17.5f, 20.5f));
            WallZ(root, FortHalf, FortFront, FortBack, 2.0f, 3.8f, rnd, dressed, dark, null);
            // Задняя почти вся легла: пролом ±4,5 — по нему дорога к склепу.
            WallX(root, FortBack, -FortHalf, -4.5f, 0.8f, 2.2f, rnd, dressed, dark, null);
            WallX(root, FortBack, 4.5f, FortHalf, 0.8f, 2.0f, rnd, dressed, dark, null);

            // Башни по углам: две стоят, две обрушены.
            Tower(root, new Vector3(-FortHalf, 0f, FortFront), 3.2f, 6.8f, true, dressed, dark);
            Tower(root, new Vector3(FortHalf, 0f, FortFront), 3.2f, 3.0f, false, dressed, dark);
            Tower(root, new Vector3(-FortHalf, 0f, FortBack), 3.0f, 4.4f, false, dressed, dark);
            Tower(root, new Vector3(FortHalf, 0f, FortBack), 3.0f, 1.8f, false, dressed, dark);
            // Надвратные башни: левая цела, у правой снесён верх, обломок перемычки — во дворе (перед воротами встают убийцы).
            Tower(root, new Vector3(-3.9f, 0f, FortFront), 2.5f, 6.2f, true, dressed, dark);
            Tower(root, new Vector3(3.9f, 0f, FortFront), 2.5f, 3.9f, false, dressed, dark);
            Piece(root, PrimitiveType.Cube, "Обломок перемычки — на башне", new Vector3(-2.35f, 5.3f, FortFront), new Vector3(1.0f, 0.6f, 1.1f), dark);
            var beam = Piece(root, PrimitiveType.Cube, "Перемычка ворот — упала", new Vector3(-6.4f, 0.35f, FortFront + 1.9f), new Vector3(4.0f, 0.7f, 0.9f), dark);
            beam.transform.rotation = Quaternion.Euler(0f, 70f, 6f);
            // Задний пролом — два столба бывших ворот.
            foreach (float s in new[] { -1f, 1f })
                Piece(root, PrimitiveType.Cube, "Столб пролома", new Vector3(4.9f * s, 1.6f, FortBack), new Vector3(1.1f, 3.2f, 1.4f), dark);

            // Во дворе: груды камня у стен, флагшток с изодранным знаменем.
            foreach (var at in new[] { new Vector3(-9.5f, 0f, 16.5f), new Vector3(9.8f, 0f, 22f), new Vector3(-8.5f, 0f, 22.6f), new Vector3(8.5f, 0f, 15.6f) })
                for (int i = 0; i < 6; i++)
                {
                    var size = new Vector3(R(0.5f, 1.3f), R(0.3f, 0.8f), R(0.5f, 1.2f));
                    var rock = Piece(root, PrimitiveType.Cube, "Обломки", at + new Vector3(R(-1.4f, 1.4f), size.y * 0.5f, R(-1.2f, 1.2f)), size,
                                     stones[rnd.Next(stones.Length)]);
                    rock.transform.rotation = Quaternion.Euler(R(-15f, 15f), R(0f, 360f), R(-15f, 15f));
                }
            var wood = Lit(new Color(0.20f, 0.14f, 0.09f), 0.1f);
            var pole = new Vector3(-10f, 0f, 15.8f);
            Piece(root, PrimitiveType.Cylinder, "Флагшток", pole + Vector3.up * 3.5f, new Vector3(0.14f, 3.5f, 0.14f), wood)
                .transform.rotation = Quaternion.Euler(0f, 0f, 4f);
            var cloth = Lit(new Color(0.22f, 0.06f, 0.05f), 0.05f);
            foreach (float face in new[] { 0f, 180f })
            {
                var flag = Piece(root, PrimitiveType.Quad, "Знамя", pole + new Vector3(0.95f, 5.6f, 0f), new Vector3(1.7f, 1.2f, 1f), cloth, false);
                flag.transform.rotation = Quaternion.Euler(0f, face, -8f);
            }
            var rag = Piece(root, PrimitiveType.Quad, "Знамя — лоскут", pole + new Vector3(1.45f, 4.75f, 0f), new Vector3(0.6f, 0.7f, 1f), cloth, false);
            rag.transform.rotation = Quaternion.Euler(0f, 0f, -20f);
        }

        /// <summary>Стена вдоль оси x на z: блоки разной высоты, внахлёст; на высоких — зубцы.
        /// В <paramref name="fallen"/> (от–до по x) — обвал: высота 0,7–1,1.</summary>
        private static void WallX(Transform parent, float z, float x0, float x1, float lo, float hi, System.Random rnd,
                                  Material face, Material dark, Vector2? fallen)
        {
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            for (float x = x0; x < x1;)
            {
                float l = Mathf.Min(R(1.4f, 2.6f), x1 - x);
                if (l < 0.3f) break;
                float c = x + l * 0.5f;
                bool down = fallen.HasValue && c > fallen.Value.x && c < fallen.Value.y;
                float h = down ? R(0.7f, 1.1f) : R(lo, hi);
                Block(parent, new Vector3(c, 0f, z), new Vector3(l + 0.2f, h, 1.2f), rnd.Next(5) == 0 ? dark : face, rnd, !down && h > 3f, true);
                if (down) Rubble(parent, new Vector3(c, 0f, z), rnd, face);
                x += l;
            }
        }

        /// <summary>Стена вдоль оси z на x — как <see cref="WallX"/>.</summary>
        private static void WallZ(Transform parent, float x, float z0, float z1, float lo, float hi, System.Random rnd,
                                  Material face, Material dark, Vector2? fallen)
        {
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            for (float z = z0; z < z1;)
            {
                float l = Mathf.Min(R(1.4f, 2.6f), z1 - z);
                if (l < 0.3f) break;
                float c = z + l * 0.5f;
                bool down = fallen.HasValue && c > fallen.Value.x && c < fallen.Value.y;
                float h = down ? R(0.7f, 1.1f) : R(lo, hi);
                Block(parent, new Vector3(x, 0f, c), new Vector3(1.2f, h, l + 0.2f), rnd.Next(5) == 0 ? dark : face, rnd, !down && h > 3f, false);
                if (down) Rubble(parent, new Vector3(x, 0f, c), rnd, face);
                z += l;
            }
        }

        private static void Block(Transform parent, Vector3 at, Vector3 size, Material m, System.Random rnd, bool merlons, bool alongX)
        {
            var b = Piece(parent, PrimitiveType.Cube, "Стена форта", at + Vector3.up * size.y * 0.5f, size, m);
            b.transform.rotation = Quaternion.Euler(0f, (float)(rnd.NextDouble() * 2 - 1) * 1.5f, 0f);
            if (!merlons) return;
            float len = alongX ? size.x : size.z;
            for (float u = -len * 0.5f + 0.35f; u < len * 0.5f - 0.3f; u += 1.0f)
            {
                var off = alongX ? new Vector3(u, 0f, 0f) : new Vector3(0f, 0f, u);
                Piece(parent, PrimitiveType.Cube, "Зубец", at + off + Vector3.up * (size.y + 0.32f),
                      alongX ? new Vector3(0.55f, 0.64f, 1.1f) : new Vector3(1.1f, 0.64f, 0.55f), m);
            }
        }

        /// <summary>Осыпь у обвала — по обе стороны стены.</summary>
        private static void Rubble(Transform parent, Vector3 at, System.Random rnd, Material m)
        {
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            for (int i = 0; i < 3; i++)
            {
                var size = new Vector3(R(0.4f, 0.9f), R(0.25f, 0.55f), R(0.4f, 0.9f));
                var p = at + new Vector3(R(-1.6f, 1.6f), size.y * 0.5f, R(-1.6f, 1.6f));
                Piece(parent, PrimitiveType.Cube, "Осыпь", p, size, m).transform.rotation =
                    Quaternion.Euler(R(-20f, 20f), R(0f, 360f), R(-20f, 20f));
            }
        }

        /// <summary>Башня: квадратная; целая — с зубцами, обрушенная — со скошенным обломком наверху.</summary>
        private static void Tower(Transform parent, Vector3 at, float w, float h, bool whole, Material face, Material dark)
        {
            Piece(parent, PrimitiveType.Cube, "Башня", at + Vector3.up * h * 0.5f, new Vector3(w, h, w), face);
            if (whole)
            {
                Piece(parent, PrimitiveType.Cube, "Пояс башни", at + Vector3.up * (h - 0.15f), new Vector3(w + 0.3f, 0.3f, w + 0.3f), dark);
                for (int i = 0; i < 4; i++)
                    foreach (float s in new[] { -1f, 1f })
                    {
                        var off = i < 2 ? new Vector3(s * (w * 0.5f - 0.25f), 0f, (i == 0 ? -1f : 1f) * w * 0.2f)
                                        : new Vector3((i == 2 ? -1f : 1f) * w * 0.2f, 0f, s * (w * 0.5f - 0.25f));
                        Piece(parent, PrimitiveType.Cube, "Зубец башни", at + off + Vector3.up * (h + 0.35f), new Vector3(0.5f, 0.7f, 0.5f), face);
                    }
                // Бойница — тёмная щель на лицевой стороне.
                Piece(parent, PrimitiveType.Cube, "Бойница", at + new Vector3(0f, h * 0.62f, -w * 0.5f - 0.01f), new Vector3(0.18f, 0.9f, 0.04f),
                      Glowing(Color.black, 1f), false);
            }
            else
            {
                var cap = Piece(parent, PrimitiveType.Cube, "Обломок башни", at + new Vector3(w * 0.15f, h + 0.3f, 0f), new Vector3(w * 0.6f, 0.8f, w * 0.7f), dark);
                cap.transform.rotation = Quaternion.Euler(0f, 12f, 22f);
            }
        }

        // ───────────────────────────── вещи у дороги ─────────────────────────────

        private void Tree(Transform parent, Vector3 at, float h, System.Random rnd, Material bark)
        {
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var root = new GameObject("Мёртвое дерево").transform;
            root.SetParent(parent, false);
            root.position = at;
            root.rotation = Quaternion.Euler(R(-6f, 6f), R(0f, 360f), R(-6f, 6f));
            var trunk = Piece(root, PrimitiveType.Cylinder, "Ствол", at + Vector3.up * h * 0.5f, new Vector3(0.32f, h * 0.5f, 0.32f), bark, false);
            trunk.transform.localPosition = Vector3.up * h * 0.5f;
            trunk.transform.localRotation = Quaternion.identity;
            for (int b = 0; b < 4; b++)
            {
                float y = h * R(0.45f, 0.9f), l = R(1.0f, 2.2f), yaw = R(0f, 360f), up = R(30f, 60f);
                var branch = Piece(root, PrimitiveType.Cylinder, "Сук", at, new Vector3(0.1f, l * 0.5f, 0.1f), bark, false).transform;
                var dir = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, -(90f - up)) * Vector3.up;
                branch.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
                branch.localPosition = Vector3.up * y + dir * l * 0.5f;
            }
        }

        private void AddCandle(Transform parent, Vector3 at, Material stone, float power)
        {
            var root = new GameObject("Свеча у дороги").transform;
            root.SetParent(parent, false);
            root.position = at;
            Piece(root, PrimitiveType.Cube, "Столбик", at + Vector3.up * 0.45f, new Vector3(0.34f, 0.9f, 0.34f), stone);
            Piece(root, PrimitiveType.Cylinder, "Свеча", at + Vector3.up * 0.97f, new Vector3(0.09f, 0.07f, 0.09f),
                  Lit(new Color(0.85f, 0.80f, 0.68f), 0.2f), false);
            var flame = Flame(root, "Пламя", at + Vector3.up * 1.1f, new Vector3(0.06f, 0.11f, 0.06f), new Color(1f, 0.70f, 0.36f), 1.3f);
            flame.localScale = Vector3.zero;
            var go = new GameObject("Свет свечи");
            go.transform.SetParent(root, false);
            go.transform.position = at + Vector3.up * 1.3f;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.72f, 0.42f);
            light.range = 7.5f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            _candles.Add(new Candle { At = at, Flame = flame, Light = light, Power = power, Size = new Vector3(0.06f, 0.11f, 0.06f) });
        }

        /// <summary>Соломенное чучело: удар принимает оно — качается.</summary>
        private void BuildDummy(Transform parent, Vector3 at)
        {
            var wood = Lit(new Color(0.26f, 0.18f, 0.11f), 0.1f);
            var straw = Lit(new Color(0.60f, 0.50f, 0.28f), 0.05f);
            var sack = Lit(new Color(0.50f, 0.42f, 0.30f), 0.05f);
            foreach (float r in new[] { 0f, 90f })
                Piece(parent, PrimitiveType.Cube, "Крестовина", at + Vector3.up * 0.06f, new Vector3(1.2f, 0.12f, 0.14f), wood)
                    .transform.rotation = Quaternion.Euler(0f, r + 20f, 0f);
            _dummy = new GameObject("Чучело").transform;
            _dummy.SetParent(parent, false);
            _dummy.position = at;
            // Ростом с человека: взгляд от первого лица — на 1,65 м, и прежнее тулово (до 1,65)
            // с головой от 1,73 пропускало прямой взгляд в щель — удар шёл мимо (w0811c).
            Piece(_dummy, PrimitiveType.Cylinder, "Кол", at + Vector3.up * 1.05f, new Vector3(0.12f, 1.05f, 0.12f), wood);
            Piece(_dummy, PrimitiveType.Cylinder, "Тулово", at + Vector3.up * 1.38f, new Vector3(0.62f, 0.5f, 0.5f), straw);
            Piece(_dummy, PrimitiveType.Cube, "Руки", at + Vector3.up * 1.66f, new Vector3(1.5f, 0.12f, 0.12f), wood);
            foreach (float s in new[] { -1f, 1f })
                Piece(_dummy, PrimitiveType.Sphere, "Пучок", at + new Vector3(0.78f * s, 1.66f, 0f), new Vector3(0.22f, 0.2f, 0.2f), straw, false);
            Piece(_dummy, PrimitiveType.Cylinder, "Пояс", at + Vector3.up * 1.0f, new Vector3(0.64f, 0.03f, 0.52f), wood, false);
            Piece(_dummy, PrimitiveType.Sphere, "Голова", at + Vector3.up * 2.12f, new Vector3(0.42f, 0.46f, 0.42f), sack);
            // Грудью к дороге: Греховод подходит с дороги, с запада (автор, 08.11: «пугало слегка
            // отвёрнуто» — стояло на 20° наискось).
            _dummyRest = Quaternion.Euler(0f, -90f, 0f);
            _dummy.rotation = _dummyRest;
        }

        /// <summary>Придорожный камень с полкой: на полке банки — урок «J».</summary>
        private void BuildShrine(Transform parent, Vector3 at, Material stoneMat)
        {
            var stone = Lit(new Color(0.24f, 0.24f, 0.26f), 0.1f);
            var root = new GameObject("Камень с банками").transform;
            root.SetParent(parent, false);
            root.position = at;
            Piece(root, PrimitiveType.Cube, "Камень", at + new Vector3(0f, 1.0f, 0.3f), new Vector3(1.3f, 2.0f, 0.7f), stone)
                .transform.rotation = Quaternion.Euler(0f, 0f, -3f);
            Piece(root, PrimitiveType.Cube, "Полка", at + new Vector3(0f, 0.95f, -0.1f), new Vector3(1.1f, 0.07f, 0.36f), stoneMat);
            // Пустые банки — тусклое стекло; средняя — с чужой душой: её и берут в суму
            // (автор, 08.11: «зачем банки по пути, если мы просто нажимаем кнопку на этом месте»).
            var glass = Lit(new Color(0.30f, 0.36f, 0.42f), 0.85f);
            var soul = Glowing(new Color(0.55f, 0.75f, 1f), 1.1f);
            var lid = Lit(new Color(0.30f, 0.22f, 0.14f), 0.2f);
            for (int i = 0; i < 3; i++)
            {
                bool full = i == 1;
                var jarAt = at + new Vector3(-0.32f + i * 0.32f, 1.11f, -0.12f);
                var jar = Piece(root, PrimitiveType.Cylinder, full ? "Банка с душой" : "Банка", jarAt, new Vector3(0.16f, 0.12f, 0.16f), full ? soul : glass, false);
                var cap = Piece(root, PrimitiveType.Cylinder, "Крышка", jarAt + Vector3.up * 0.13f, new Vector3(0.18f, 0.025f, 0.18f), lid, false);
                if (!full) continue;
                var glow = new GameObject("Свет души в банке");
                glow.transform.SetParent(root, false);
                glow.transform.position = jarAt + Vector3.up * 0.05f;
                var l = glow.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(0.55f, 0.75f, 1f);
                l.range = 2.4f;
                l.intensity = 1.4f;
                l.shadows = LightShadows.None;
                _soulJar.AddRange(new[] { jar, cap, glow });
            }
            // Свеча на полке — без столбика: камень и есть подставка.
            AddCandle(root, at + new Vector3(0.45f, 0f, -0.1f), stoneMat, 3f);
            var postPiece = _candles[_candles.Count - 1].Flame.parent.Find("Столбик");
            if (postPiece != null) DestroyImmediate(postPiece.gameObject);
            // Лицом к дороге и к идущему с юга.
            root.rotation = Quaternion.Euler(0f, -18f, 0f);
        }

        // ───────────────────────────── склеп ─────────────────────────────

        private void BuildCrypt(Transform parent)
        {
            var root = new GameObject("Склеп").transform;
            root.SetParent(parent, false);
            var stone = Lit(new Color(0.20f, 0.20f, 0.22f), 0.12f);
            var dark = Lit(new Color(0.14f, 0.14f, 0.16f), 0.1f);
            var black = Glowing(Color.black, 1f);
            var cold = new Color(0.68f, 0.80f, 1f);
            float z = GateZ;

            // Курган — за склепом и по бокам. Прежний шар выпирал внутрь склепа перед силуэтом и
            // светом в глубине: в открытых дверях стояла тёмная земля (w0711zj).
            var soil = Lit(new Color(0.10f, 0.09f, 0.08f), 0.05f);
            Piece(root, PrimitiveType.Sphere, "Курган", new Vector3(0f, -3f, z + 24f), new Vector3(46f, 24f, 26f), soil, false);
            foreach (float s in new[] { -1f, 1f })
                Piece(root, PrimitiveType.Sphere, "Курган — плечо", new Vector3(12.5f * s, -1.5f, z + 5f), new Vector3(15f, 11f, 13f), soil, false);

            // Лицо: два крыла, перемычка, ступенчатый фронтон, колонны, порог.
            Piece(root, PrimitiveType.Cube, "Крыло левое", new Vector3(-5.65f, 3.2f, z + 0.7f), new Vector3(6.7f, 6.4f, 1.4f), stone);
            Piece(root, PrimitiveType.Cube, "Крыло правое", new Vector3(5.65f, 3.2f, z + 0.7f), new Vector3(6.7f, 6.4f, 1.4f), stone);
            Piece(root, PrimitiveType.Cube, "Перемычка", new Vector3(0f, 6.05f, z + 0.7f), new Vector3(4.6f, 0.7f, 1.4f), dark);
            Piece(root, PrimitiveType.Cube, "Фронтон 1", new Vector3(0f, 6.9f, z + 0.75f), new Vector3(12.5f, 1.0f, 1.3f), stone);
            Piece(root, PrimitiveType.Cube, "Фронтон 2", new Vector3(0f, 7.9f, z + 0.8f), new Vector3(7.5f, 1.0f, 1.1f), stone);
            Piece(root, PrimitiveType.Cube, "Фронтон 3", new Vector3(0f, 8.85f, z + 0.85f), new Vector3(2.8f, 0.9f, 0.9f), dark);
            foreach (float s in new[] { -1f, 1f })
            {
                Piece(root, PrimitiveType.Cube, "Колонна", new Vector3(2.95f * s, 3.15f, z - 0.25f), new Vector3(0.95f, 6.3f, 0.95f), dark);
                Piece(root, PrimitiveType.Cube, "Капитель", new Vector3(2.95f * s, 6.35f, z - 0.25f), new Vector3(1.25f, 0.3f, 1.25f), stone);
                Piece(root, PrimitiveType.Cube, "База", new Vector3(2.95f * s, 0.15f, z - 0.25f), new Vector3(1.25f, 0.3f, 1.25f), stone);
            }
            Piece(root, PrimitiveType.Cube, "Порог", new Vector3(0f, 0.03f, z - 0.6f), new Vector3(4.8f, 0.06f, 1.6f), dark);

            // Знак над дверью — серп луны: светлый круг, на нём тёмный круг со сдвигом.
            var medal = Piece(root, PrimitiveType.Cylinder, "Медальон", new Vector3(0f, 7.3f, z + 0.12f), new Vector3(1.5f, 0.04f, 1.5f), dark, false);
            medal.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var moon = Piece(root, PrimitiveType.Cylinder, "Серп", new Vector3(0f, 7.3f, z + 0.08f), new Vector3(0.95f, 0.02f, 0.95f), Glowing(cold, 0.9f), false);
            moon.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var bite = Piece(root, PrimitiveType.Cylinder, "Серп — тень", new Vector3(0.17f, 7.36f, z + 0.05f), new Vector3(0.82f, 0.02f, 0.82f), dark, false);
            bite.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // Створки: петли у косяков, открываются внутрь.
            var wood = Lit(new Color(0.15f, 0.10f, 0.065f), 0.2f);
            var iron = Lit(new Color(0.10f, 0.10f, 0.11f), 0.5f);
            _doorLeft = Leaf(root, "Створка левая", new Vector3(-2.3f, 0f, z + 0.15f), 1f, wood, iron);
            _doorRight = Leaf(root, "Створка правая", new Vector3(2.3f, 0f, z + 0.15f), -1f, wood, iron);

            // Внутри — чёрная пасть: свет туда не доходит. Щель между створками светится холодом.
            _seam = Piece(root, PrimitiveType.Cube, "Щель", new Vector3(0f, 2.85f, z + 0.45f), new Vector3(0.10f, 5.5f, 0.02f), Glowing(cold, 1.2f), false);
            Piece(root, PrimitiveType.Cube, "Пол склепа", new Vector3(0f, 0.02f, z + 5f), new Vector3(4.8f, 0.04f, 9f), black, false);
            Piece(root, PrimitiveType.Cube, "Стена склепа левая", new Vector3(-2.5f, 3f, z + 5f), new Vector3(0.4f, 6f, 9f), black, false);
            Piece(root, PrimitiveType.Cube, "Стена склепа правая", new Vector3(2.5f, 3f, z + 5f), new Vector3(0.4f, 6f, 9f), black, false);
            Piece(root, PrimitiveType.Cube, "Свод склепа", new Vector3(0f, 5.9f, z + 5f), new Vector3(5.4f, 0.4f, 9f), black, false);
            Piece(root, PrimitiveType.Cube, "Глубь склепа", new Vector3(0f, 3f, z + 9.7f), new Vector3(5.4f, 6f, 0.4f), black, false);

            // В глубине — бледный холодный проём, против него стоит Он.
            _backGlow = Glowing(cold, 1.5f);
            _backColour = _backGlow.color;
            var back = Piece(root, PrimitiveType.Quad, "Свет в глубине", new Vector3(0f, 2.8f, z + 9.45f), new Vector3(3.8f, 5.6f, 1f), _backGlow, false);
            back.transform.rotation = Quaternion.identity;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader != null)
            {
                var halo = new Material(shader) { name = "Ореол проёма" };
                var tex = Soft(64);
                halo.SetTexture("_BaseMap", tex);
                halo.mainTexture = tex;
                halo.SetFloat("_Surface", 1f);
                halo.SetFloat("_Blend", 2f);
                halo.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                halo.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                halo.SetFloat("_ZWrite", 0f);
                halo.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                halo.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                halo.SetColor("_BaseColor", new Color(cold.r, cold.g, cold.b, 0.55f));
                Piece(root, PrimitiveType.Quad, "Ореол проёма", new Vector3(0f, 2.8f, z + 9.35f), new Vector3(6.0f, 7.2f, 1f), halo, false);
            }
            Death(root, new Vector3(0f, 0f, z + 6.6f), black);
            // Холодный туман из дверей — ползёт к вошедшему по полу.
            var mist = Haze(new Color(0.60f, 0.72f, 0.95f), additive: true);
            if (mist != null)
                _cryptMist = Wisps(root, "Туман склепа", new Vector3(0f, 0.2f, z + 3.6f), mist, new Vector3(4.2f, 0.15f, 6.2f), 30f,
                                   new Vector2(4f, 6f), new Vector2(1.4f, 2.8f), new Color(0.60f, 0.72f, 0.95f),
                                   new Vector3(-0.15f, 0f, -1.0f), new Vector3(0.15f, 0.06f, -0.45f), 0.16f, prewarm: false);

            var go = new GameObject("Холод из склепа");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(0f, 2.6f, z + 2.2f);
            _cryptLight = go.AddComponent<Light>();
            _cryptLight.type = LightType.Point;
            _cryptLight.color = cold;
            _cryptLight.range = 12f;
            _cryptLight.intensity = 0f;
            _cryptLight.shadows = LightShadows.None;
        }

        private static Transform Leaf(Transform parent, string name, Vector3 hinge, float side, Material wood, Material iron)
        {
            const float w = 2.27f, h = 5.66f;
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.position = hinge;
            var c = hinge + new Vector3(side * w * 0.5f, 0.04f + h * 0.5f, 0.11f);
            Piece(pivot, PrimitiveType.Cube, "Доски", c, new Vector3(w, h, 0.22f), wood);
            foreach (float y in new[] { 0.8f, 2.85f, 4.9f })
                Piece(pivot, PrimitiveType.Cube, "Полоса", new Vector3(c.x, y, hinge.z - 0.02f), new Vector3(w - 0.08f, 0.13f, 0.04f), iron, false);
            var ring = Piece(pivot, PrimitiveType.Cylinder, "Кольцо", new Vector3(hinge.x + side * (w - 0.35f), 2.5f, hinge.z - 0.05f),
                             new Vector3(0.28f, 0.02f, 0.28f), iron, false);
            ring.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            return pivot;
        }

        /// <summary>
        /// Силуэт Бога Смерти: высокий, в капюшоне, с косой — весь чёрный. Свет за ним, не на нём.
        /// </summary>
        /// <summary>
        /// Бог Смерти в глубине склепа (автор, 08.11: «сделай силуэт Бога Смерти величественнее и более
        /// жутким, может с какими-нибудь эффектами»). Выше дверного проёма в глубине — 4,7 м с косой;
        /// висит над полом, подол — лохмотьями, под ними видно свет. Голова опущена, левая рука висит —
        /// <see cref="Haunt"/> поднимает голову к вошедшему, зажигает глаза и тянет руку вперёд.
        /// От него поднимается чёрный дым — против света он виден, на чёрном нет.
        /// </summary>
        private void Death(Transform parent, Vector3 at, Material black)
        {
            var root = new GameObject("Бог Смерти — силуэт").transform;
            root.SetParent(parent, false);
            root.position = at;
            _death = root;
            _deathAt = at;
            const float hem = 0.4f;
            Shape(root, "Балахон", Cone(1.3f, 0.46f, 3.0f, 24), at + Vector3.up * hem, Quaternion.identity, black);
            _deathHem = Shape(root, "Лохмотья", Tatters(1.3f, 26, 0.75f, 13), at + Vector3.up * hem, Quaternion.identity, black);
            // Плечи — покатая накидка, не шар: шар читался воротником-бубликом (w0811c).
            Shape(root, "Плечи", Cone(0.8f, 0.36f, 0.75f, 20), at + Vector3.up * 2.95f, Quaternion.identity, black);

            // Голова — на шее, целиком с глазами: поднимается к вошедшему.
            _deathHood = new GameObject("Голова").transform;
            _deathHood.SetParent(root, false);
            _deathHood.position = at + Vector3.up * 3.5f;
            // Глубокий капюшон: круглый верх, острие откинуто назад. Прямое острие делало
            // колпак — силуэт выходил мультяшным (w0811c).
            Piece(_deathHood, PrimitiveType.Sphere, "Капюшон", at + Vector3.up * 3.98f, new Vector3(0.92f, 1.12f, 0.95f), black, false);
            Shape(_deathHood, "Острие капюшона", Cone(0.3f, 0f, 0.42f, 12), at + new Vector3(0f, 4.3f, 0.22f), Quaternion.Euler(55f, 0f, 0f), black);
            var eyeGlow = Glowing(new Color(0.50f, 0.72f, 1f), 3f);
            foreach (float s in new[] { -1f, 1f })
                _deathEyes.Add(Piece(_deathHood, PrimitiveType.Sphere, "Глаз", at + new Vector3(0.15f * s, 3.92f, -0.43f), Vector3.zero, eyeGlow, false).transform);
            _deathHood.localRotation = Quaternion.Euler(18f, 0f, 0f);

            // Левая рука: рукав и кость кисти с длинными пальцами.
            _deathArm = new GameObject("Левая рука").transform;
            _deathArm.SetParent(root, false);
            _deathArm.position = at + new Vector3(-0.72f, 3.3f, 0f);
            Shape(_deathArm, "Рукав", Sheet(new Vector3(0.12f, 0.15f, 0f), new Vector3(-0.32f, -0.3f, -0.04f), new Vector3(-0.86f, -1.0f, -0.08f),
                                            new Vector3(-0.74f, -1.72f, -0.05f), new Vector3(-0.42f, -1.3f, 0f), new Vector3(0.06f, -0.62f, 0f)),
                  _deathArm.position, Quaternion.identity, black);
            var wrist = _deathArm.position + new Vector3(-0.84f, -1.22f, -0.08f);
            Piece(_deathArm, PrimitiveType.Sphere, "Ладонь", wrist, new Vector3(0.13f, 0.16f, 0.07f), black, false);
            for (int f = 0; f < 5; f++)
            {
                float ang = Mathf.Lerp(-150f, -80f, f / 4f) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), -0.15f).normalized;
                float len = f == 4 ? 0.2f : 0.34f;
                var finger = Piece(_deathArm, PrimitiveType.Cylinder, "Палец", wrist + dir * (0.07f + len * 0.5f), new Vector3(0.035f, len * 0.5f, 0.035f), black, false);
                finger.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir);
            }

            // Правая рука держит косу; коса выше его самого.
            Shape(root, "Правый рукав", Sheet(new Vector3(0.55f, 3.5f, 0f), new Vector3(1.2f, 2.75f, -0.08f), new Vector3(1.28f, 2.28f, -0.1f),
                                              new Vector3(0.96f, 2.2f, -0.05f), new Vector3(0.6f, 2.9f, 0f)),
                  at, Quaternion.identity, black);
            _deathScythe = new GameObject("Коса").transform;
            _deathScythe.SetParent(root, false);
            _deathScythe.position = at + new Vector3(1.15f, 2.5f, -0.12f);
            Piece(_deathScythe, PrimitiveType.Sphere, "Кулак", at + new Vector3(1.15f, 2.5f, -0.12f), new Vector3(0.2f, 0.24f, 0.18f), black, false);
            var snath = Piece(_deathScythe, PrimitiveType.Cylinder, "Косовище", at + new Vector3(1.2f, 2.6f, -0.12f), new Vector3(0.075f, 2.3f, 0.075f), black, false).transform;
            snath.rotation = Quaternion.Euler(0f, 0f, -6f);
            Shape(_deathScythe, "Лезвие", Blade(1.9f, 0.3f), snath.position + snath.up * 2.3f, Quaternion.identity, black);

            // Чёрный дым от него: виден против света, на чёрном пропадает.
            var smoke = Haze(new Color(0f, 0f, 0f), additive: false);
            if (smoke != null)
                _deathSmoke = Wisps(root, "Дым", at + Vector3.up * 2.3f, smoke, new Vector3(2.6f, 3.6f, 0.7f), 12f,
                                    new Vector2(2.5f, 4f), new Vector2(0.6f, 1.2f), Color.black,
                                    new Vector3(-0.15f, 0.25f, -0.05f), new Vector3(0.15f, 0.55f, 0.05f), 0.5f, prewarm: true);
        }

        /// <summary>Жизнь силуэта, пока гаснет экран: t — секунды от скрипа дверей.</summary>
        private void Haunt(float t, float breath)
        {
            if (_death == null) return;
            _death.position = _deathAt + Vector3.up * (0.07f * Mathf.Sin(t * 1.25f));
            if (_deathHem != null) _deathHem.localRotation = Quaternion.Euler(0f, 5f * Mathf.Sin(t * 0.8f), 0f);
            if (_deathHood != null)
                _deathHood.localRotation = Quaternion.Euler(Mathf.Lerp(18f, -4f, Smooth(Mathf.InverseLerp(0.6f, 2.3f, t))), 0f, 0f);
            float eyes = Smooth(Mathf.InverseLerp(1.2f, 1.7f, t)) * (0.9f + 0.1f * Mathf.Sin(t * 19f));
            foreach (var e in _deathEyes) if (e != null) e.localScale = Vector3.one * (0.065f * eyes);
            float reach = Smooth(Mathf.InverseLerp(1.0f, 3.2f, t));
            if (_deathArm != null) _deathArm.localRotation = Quaternion.Euler(48f * reach, 0f, -14f * reach);
            if (_deathScythe != null) _deathScythe.localRotation = Quaternion.Euler(0f, 0f, 2.5f * Mathf.Sin(t * 0.7f));
            if (_backGlow != null) _backGlow.color = _backColour * breath;
        }

        /// <summary>Гул его присутствия: низкий, с медленным выдохом посередине.</summary>
        private void Presence()
        {
            const int rate = 22050;
            const float seconds = 4.8f;
            int n = (int)(rate * seconds);
            var data = new float[n];
            var rnd = new System.Random(7);
            float ph = 0f, lp = 0f, br = 0f, br2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Clamp01(t / 1.2f) * Mathf.Clamp01((seconds - t) / 0.7f);
                ph += 42f * (1f + 0.01f * Mathf.Sin(t * 0.9f)) / rate;
                float saw = (ph % 1f) * 2f - 1f;
                lp += (saw - lp) * 0.08f;
                float hum = lp * 0.9f + 0.35f * Mathf.Sin(2f * Mathf.PI * 84f * t) + 0.16f * Mathf.Sin(2f * Mathf.PI * 126f * t);
                float noise = (float)rnd.NextDouble() * 2f - 1f;
                br += (noise - br) * 0.18f;
                br2 += (br - br2) * 0.5f;
                float exhale = Mathf.Exp(-Mathf.Pow((t - 2.0f) / 0.55f, 2f));
                data[i] = env * 0.55f * hum + 1.1f * exhale * (br - br2);
            }
            var clip = AudioClip.Create("Присутствие Смерти", n, 1, rate, false);
            clip.SetData(data, 0);
            var src = gameObject.AddComponent<AudioSource>();
            src.clip = clip;
            src.spatialBlend = 0f;
            src.volume = 0.8f;
            src.PlayDelayed(0.5f);
        }

        /// <summary>Полотно по точкам (веер от первой), видно с обеих сторон: рукава.</summary>
        private static Mesh Sheet(params Vector3[] p)
        {
            var tri = new List<int>();
            for (int i = 1; i < p.Length - 1; i++) tri.AddRange(new[] { 0, i, i + 1, 0, i + 1, i });
            var m = new Mesh { name = "Полотно" };
            m.SetVertices(new List<Vector3>(p));
            m.SetTriangles(tri, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Лохмотья подола: зубцы вниз по кругу, разной длины и чуть наружу.</summary>
        private static Mesh Tatters(float r, int n, float len, int seed)
        {
            var rnd = new System.Random(seed);
            var v = new List<Vector3>();
            var tri = new List<int>();
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                float am = (a0 + a1) * 0.5f + ((float)rnd.NextDouble() - 0.5f) * 0.2f;
                float l = len * (0.45f + 0.55f * (float)rnd.NextDouble());
                int k = v.Count;
                v.Add(new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * r);
                v.Add(new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * r);
                v.Add(new Vector3(Mathf.Cos(am), 0f, Mathf.Sin(am)) * (r * 1.08f) + Vector3.down * l);
                tri.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 1 });
            }
            var m = new Mesh { name = "Лохмотья" };
            m.SetVertices(v);
            m.SetTriangles(tri, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static Transform Shape(Transform parent, string name, Mesh mesh, Vector3 at, Quaternion rot, Material m)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(at, rot);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        /// <summary>Мягкие пятна частиц: additive — светятся (туман), иначе — заслоняют (дым).</summary>
        private static Material Haze(Color colour, bool additive)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) return null;
            var m = new Material(shader) { name = additive ? "Туман" : "Дым" };
            var tex = Soft(64);
            m.SetTexture("_BaseMap", tex);
            m.mainTexture = tex;
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetColor("_BaseColor", new Color(colour.r, colour.g, colour.b, 1f));
            return m;
        }

        private static ParticleSystem Wisps(Transform parent, string name, Vector3 at, Material m, Vector3 box, float rate,
                                            Vector2 life, Vector2 size, Color colour, Vector3 velLo, Vector3 velHi, float alpha, bool prewarm)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.prewarm = prewarm;
            main.duration = 4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(colour.r, colour.g, colour.b, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;
            main.maxParticles = 300;
            var em = ps.emission;
            em.rateOverTime = rate;
            var sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = box;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(velLo.x, velHi.x);
            vel.y = new ParticleSystem.MinMaxCurve(velLo.y, velHi.y);
            vel.z = new ParticleSystem.MinMaxCurve(velLo.z, velHi.z);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(alpha, 0.3f), new GradientAlphaKey(alpha, 0.65f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.4f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        /// <summary>Усечённый конус без донца: балахон, остриё капюшона.</summary>
        private static Mesh Cone(float r0, float r1, float h, int n)
        {
            var v = new List<Vector3>();
            var tri = new List<int>();
            for (int i = 0; i <= n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v.Add(d * r0);
                v.Add(d * r1 + Vector3.up * h);
            }
            for (int i = 0; i < n; i++)
            {
                int k = i * 2;
                tri.AddRange(new[] { k, k + 1, k + 3, k, k + 3, k + 2 });
                tri.AddRange(new[] { k, k + 3, k + 1, k, k + 2, k + 3 });
            }
            var m = new Mesh { name = "Конус" };
            m.SetVertices(v);
            m.SetTriangles(tri, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Лезвие косы: серп в плоскости зала, сужается к обоим концам; видно с обеих сторон.</summary>
        private static Mesh Blade(float reach, float width)
        {
            var v = new List<Vector3>();
            var tri = new List<int>();
            const int n = 18;
            var centre = new Vector3(-reach * 0.5f, -reach * 0.42f, 0f);
            float r = reach * 0.66f;
            for (int i = 0; i <= n; i++)
            {
                float k = i / (float)n;
                float a = Mathf.Lerp(20f, 150f, k) * Mathf.Deg2Rad;
                float wdt = width * Mathf.Sin(Mathf.PI * Mathf.Lerp(0.15f, 1f, k));
                var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                v.Add(centre + d * r);
                v.Add(centre + d * (r - wdt));
            }
            for (int i = 0; i < n; i++)
            {
                int k = i * 2;
                tri.AddRange(new[] { k, k + 1, k + 3, k, k + 3, k + 2 });
                tri.AddRange(new[] { k, k + 3, k + 1, k, k + 2, k + 3 });
            }
            var m = new Mesh { name = "Лезвие" };
            m.SetVertices(v);
            m.SetTriangles(tri, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        // ───────────────────────────── жизнь дороги ─────────────────────────────

        /// <summary>
        /// Удар пришёлся в чучело (<see cref="FirstPersonCombat.Swung"/>): оно отшатывается по ходу удара,
        /// а удар мимо середины ещё и крутит его на коле (автор, 08.11: «когда я бью, оно отшатывается
        /// в определённую сторону, а должно от удара»). Прежде качалось от любого взмаха рядом —
        /// и всегда от того места, где стоял Греховод.
        /// </summary>
        private void OnSwung(RaycastHit hit, Vector3 dir)
        {
            if (_dummy == null || hit.collider == null || !hit.collider.transform.IsChildOf(_dummy)) return;
            DummyHits++;
            var d = new Vector2(dir.x, dir.z);
            if (d.sqrMagnitude < 1e-4f) return;
            d.Normalize();
            _tiltVel += d * 150f;
            var arm = hit.point - _dummy.position;
            arm.y = 0f;
            _spinVel += Vector3.Cross(arm, new Vector3(d.x, 0f, d.y)).y * 260f;
        }

        /// <summary>Взять банку с душой с камня: прячется с полки. false — уже взята.</summary>
        public bool TakeSoulJar()
        {
            if (SoulJarTaken) return false;
            SoulJarTaken = true;
            foreach (var go in _soulJar) if (go != null) go.SetActive(false);
            return true;
        }

        public static bool NearDummy(Vector3 at, float reach = 3.2f) => Flat(at - DummyAt).magnitude <= reach;
        public static bool NearShrine(Vector3 at, float reach = 3.4f) => Flat(at - ShrineAt).magnitude <= reach;
        public static bool NearGate(Vector3 at, float reach = 3.6f) => Flat(at - GateFront).magnitude <= reach;

        /// <summary>
        /// Дорога разведана с начала — серым, как свой лагерь (<see cref="FogOfWar"/>, KnownHome).
        /// Неразведанное туман войны красит чёрным, и с начала дороги не было видно ни форта,
        /// ни холодных жаровен у склепа — цели, к которой она ведёт; кадры издалека выходили
        /// чёрными (w0711zk). Туман ставится после Awake, поэтому — здесь, в Start.
        /// </summary>
        private void Start()
        {
            for (float z = From; z <= GateZ + 12f; z += 4f)
                FogOfWar.Reveal(new Vector3(0f, 0f, z), 15f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_dummy != null && (_tilt.sqrMagnitude > 1e-4f || _tiltVel.sqrMagnitude > 1e-4f
                                   || Mathf.Abs(_spin) > 0.01f || Mathf.Abs(_spinVel) > 0.01f))
            {
                _tiltVel += (-90f * _tilt - 6f * _tiltVel) * dt;
                _tilt = Vector2.ClampMagnitude(_tilt + _tiltVel * dt, 25f);
                _spinVel += (-25f * _spin - 3f * _spinVel) * dt;
                _spin = Mathf.Clamp(_spin + _spinVel * dt, -40f, 40f);
                var d3 = new Vector3(_tilt.x, 0f, _tilt.y);
                var lean = d3.sqrMagnitude > 1e-6f ? Quaternion.AngleAxis(d3.magnitude, Vector3.Cross(Vector3.up, d3.normalized)) : Quaternion.identity;
                _dummy.localRotation = lean * Quaternion.Euler(0f, _spin, 0f) * _dummyRest;
            }

            var hero = SinbinderPlayer.Instance;
            float now = Time.unscaledTime;
            foreach (var c in _candles)
            {
                if (c.Lit < 0f && hero != null && Mathf.Abs(hero.transform.position.z - c.At.z) < 10f) c.Lit = now;
                if (c.Lit < 0f || c.Flame == null) continue;
                float k = Mathf.Clamp01((now - c.Lit) / 0.6f);
                float flick = 1f + 0.08f * Mathf.Sin(now * 9f + c.At.z) + 0.05f * Mathf.Sin(now * 23f + c.At.x);
                c.Flame.localScale = new Vector3(c.Size.x, c.Size.y * flick, c.Size.z) * k;
                c.Light.intensity = c.Power * k * flick;
            }
        }

        /// <summary>
        /// Ворота склепа: камера встаёт глазами к дверям, створки расходятся, экран гаснет
        /// раньше, чем можно что-то рассмотреть, — только силуэт против холодного света.
        /// Кончается чёрным экраном; дальше сон грузит лагерь.
        /// </summary>
        public IEnumerator OpenCrypt(SinbinderPlayer hero)
        {
            CryptFade = 0f;
            Vector3 heroAt = hero != null ? hero.transform.position : GateFront;
            var cam = Camera.main;
            var rts = cam != null ? cam.GetComponent<RTS_Camera>() : null;
            if (rts != null) rts.enabled = false;

            var overlay = new GameObject("Склеп — затемнение");
            var canvas = overlay.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            var img = new GameObject("Чёрное", typeof(RectTransform), typeof(Image));
            img.transform.SetParent(overlay.transform, false);
            var rt = (RectTransform)img.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var black = img.GetComponent<Image>();
            black.color = Color.clear;
            black.raycastTarget = true;

            // Глазами Греховода перед дверями.
            // Пять шагов до дверей: видно проём целиком и колонны; ближе створки занимали весь кадр (w0711zi).
            UI.Letterbox.Instance?.Show();
            var eye = new Vector3(Mathf.Clamp(heroAt.x, -1.0f, 1.0f), 1.75f, GateZ - 5.4f);
            var look = Quaternion.LookRotation(new Vector3(0f, 2.3f, GateZ + 6.5f) - eye);
            // Глазами Греховода: тело не видно и стоит под глазами — в прогоне он заслонял ворота спиной.
            if (hero != null)
            {
                foreach (var r in hero.GetComponentsInChildren<Renderer>()) r.enabled = false;
                var agent = hero.GetComponent<UnityEngine.AI.NavMeshAgent>();
                var feet = new Vector3(eye.x, 0f, eye.z);
                if (agent != null && agent.isOnNavMesh) agent.Warp(feet); else hero.transform.position = feet;
            }
            if (cam != null)
            {
                var fromPos = cam.transform.position;
                var fromRot = cam.transform.rotation;
                for (float t = 0f; t < 1.3f; t += Time.unscaledDeltaTime)
                {
                    float k = Smooth(t / 1.3f);
                    cam.transform.SetPositionAndRotation(Vector3.Lerp(fromPos, eye, k), Quaternion.Slerp(fromRot, look, k));
                    yield return null;
                }
                cam.transform.SetPositionAndRotation(eye, look);
            }
            yield return new WaitForSecondsRealtime(0.4f);

            Creak();
            Presence();
            if (_deathSmoke != null) _deathSmoke.Play();
            if (_cryptMist != null) _cryptMist.Play();
            // Тема сна уходит вместе с дверьми: к силуэту — тишина, скрип и его гул.
            Audio.Music.Stop(3f);
            // Взгляд поднимается за ним: он выше дверей в глубине.
            var lookUp = Quaternion.LookRotation(new Vector3(0f, 2.9f, GateZ + 6.5f) - eye);
            if (_seam != null) _seam.SetActive(false);
            const float open = 3.4f, fade = 3.8f;
            for (float t = 0f; t < fade + 0.9f; t += Time.unscaledDeltaTime)
            {
                float d = Mathf.Pow(Smooth(Mathf.Clamp01(t / open)), 1.3f);
                if (_doorLeft != null) _doorLeft.localRotation = Quaternion.Euler(0f, -88f * d, 0f);
                if (_doorRight != null) _doorRight.localRotation = Quaternion.Euler(0f, 88f * d, 0f);
                float breath = 1f + 0.16f * Mathf.Sin(t * 1.6f) + 0.05f * Mathf.Sin(t * 5.3f);
                if (_cryptLight != null) _cryptLight.intensity = 10f * d * breath;
                Haunt(t, breath);
                float a = Mathf.Pow(Mathf.Clamp01(t / fade), 1.6f);
                black.color = new Color(0f, 0f, 0f, a);
                CryptFade = a;
                if (cam != null)
                {
                    float push = Smooth(Mathf.Clamp01(t / fade));
                    cam.transform.SetPositionAndRotation(eye + look * Vector3.forward * (1.8f * push), Quaternion.Slerp(look, lookUp, push));
                }
                yield return null;
            }
            // Чёрное остаётся до загрузки лагеря: уйдёт вместе со сценой сна.
            CryptFade = 1f;
        }

        /// <summary>
        /// Створки отворяются со стоном (автор, 08.11: «при открытии врат большого склепа нужен
        /// скрип, чтобы он напоминал стон умирающего»). Петля скрипит толчками с частотой голоса:
        /// вдох вверх (118 → 156 Гц), долгий спад (→ 84) и обрыв (→ 58). Толчки идут через гласные —
        /// «о» к «у» (три резонанса, как горло), рвутся рывками трения, а к концу сбиваются и
        /// тонут в выдохе. Под стоном — гул камня.
        /// </summary>
        private void Creak()
        {
            const int rate = 22050;
            const float seconds = 4.4f;
            int n = (int)(rate * seconds);
            var voice = new float[n];
            var rnd = new System.Random(31);
            float phase = 0f, jitter = 0f, pulse = 0f, peak = 1e-6f;
            Resonator f1 = default, f2 = default, f3 = default;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float f0 = t < 0.6f ? Mathf.Lerp(118f, 156f, Smooth(t / 0.6f))
                         : t < 3.3f ? Mathf.Lerp(156f, 84f, (t - 0.6f) / 2.7f)
                         : Mathf.Lerp(84f, 58f, (t - 3.3f) / 1.1f);
                f0 *= 1f + 0.025f * Mathf.Sin(t * 2f * Mathf.PI * 4.6f);
                float dying = Smooth((t - 2.6f) / 1.6f);
                phase += f0 * (1f + jitter) / rate;
                if (phase >= 1f)
                {
                    phase -= 1f;
                    jitter = ((float)rnd.NextDouble() * 2f - 1f) * Mathf.Lerp(0.06f, 0.24f, dying);
                    pulse = 1f;
                }
                float noise = (float)rnd.NextDouble() * 2f - 1f;
                // Трение петли: голос цепляется рывками, пять-семь раз в секунду.
                float stick = 0.5f + 0.5f * Mathf.Pow(Mathf.Abs(Mathf.Sin(t * Mathf.PI * (5.5f + 2f * Mathf.Sin(t * 1.3f)))), 0.6f);
                float src = pulse * stick * (1f - 0.6f * dying) + noise * Mathf.Lerp(0.03f, 0.25f, dying);
                pulse *= 0.55f;
                if ((i & 31) == 0)
                {
                    float v = Smooth((t - 0.8f) / 2.8f);
                    f1.Tune(Mathf.Lerp(520f, 340f, v), 90f, rate);
                    f2.Tune(Mathf.Lerp(880f, 680f, v), 110f, rate);
                    f3.Tune(Mathf.Lerp(2450f, 2250f, v), 160f, rate);
                }
                float env = Smooth(t / 0.3f) * Mathf.Clamp01((seconds - t) / 0.9f);
                voice[i] = env * (f1.Step(src) + 0.55f * f2.Step(src) + 0.2f * f3.Step(src));
                peak = Mathf.Max(peak, Mathf.Abs(voice[i]));
            }
            float rumble = 0f, gain = 0.7f / peak;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                rumble += (((float)rnd.NextDouble() * 2f - 1f) - rumble) * 0.02f;
                float env = Mathf.Clamp01(t / 0.15f) * Mathf.Clamp01((seconds - t) / 0.8f);
                voice[i] = voice[i] * gain + 1.4f * rumble * env;
            }
            var clip = AudioClip.Create("Стон ворот склепа", n, 1, rate, false);
            clip.SetData(voice, 0);
            var src2 = gameObject.AddComponent<AudioSource>();
            src2.clip = clip;
            src2.spatialBlend = 0f;
            src2.volume = 0.6f;
            src2.Play();
        }

        /// <summary>Резонанс горла (полосовой, пик 0 дБ): гласную задают три таких.</summary>
        private struct Resonator
        {
            private float _b0, _a1, _a2, _x1, _x2, _y1, _y2;

            public void Tune(float f, float width, int rate)
            {
                float w = 2f * Mathf.PI * f / rate;
                float alpha = Mathf.Sin(w) * width / (2f * f);
                float a0 = 1f + alpha;
                _b0 = alpha / a0;
                _a1 = -2f * Mathf.Cos(w) / a0;
                _a2 = (1f - alpha) / a0;
            }

            public float Step(float x)
            {
                float y = _b0 * (x - _x2) - _a1 * _y1 - _a2 * _y2;
                _x2 = _x1; _x1 = x; _y2 = _y1; _y1 = y;
                return y;
            }
        }

        // ───────────────────────────── помощники ─────────────────────────────

        private static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        private static GameObject Piece(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale,
                                        Material m, bool solid = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            if (!solid) DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static void Flat(Transform parent, string name, Vector3 at, Vector2 size, Material m)
        {
            var go = Piece(parent, PrimitiveType.Quad, name, at, new Vector3(size.x, size.y, 1f), m, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var r = go.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static Material Lit(Color colour, float smooth)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(shader) { color = colour };
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            return m;
        }

        /// <summary>
        /// Огонь — капля: круглый низ и язык к острию. Прежний шар силой 2 уходил в белое,
        /// и жаровни на кадрах стояли с белыми яйцами (w0811a): цвет огня держится, только
        /// пока он не ярче себя самого, — отсюда сила около единицы.
        /// </summary>
        private static Transform Flame(Transform parent, string name, Vector3 at, Vector3 size, Color fire, float bright)
        {
            var flame = new GameObject(name).transform;
            flame.SetParent(parent, false);
            flame.position = at;
            flame.localScale = size;
            var glow = Glowing(fire, bright);
            var bulb = Piece(flame, PrimitiveType.Sphere, "Низ огня", at, Vector3.one, glow, false);
            bulb.transform.localPosition = new Vector3(0f, -0.18f, 0f);
            bulb.transform.localScale = Vector3.one * 0.8f;
            bulb.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var tongue = new GameObject("Язык огня");
            tongue.transform.SetParent(flame, false);
            tongue.transform.localPosition = new Vector3(0f, -0.18f, 0f);
            tongue.AddComponent<MeshFilter>().sharedMesh = Cone(0.4f, 0f, 0.75f, 12);
            var r = tongue.AddComponent<MeshRenderer>();
            r.sharedMaterial = glow;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return flame;
        }

        private static Material Glowing(Color colour, float bright)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            return new Material(shader) { color = colour * bright };
        }

        private static Texture2D Soft(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2f);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            t.Apply();
            return t;
        }
    }
}
