// Assets/Scripts/Gameplay/DreamRoad.cs
// Перевод: строк нет
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.Core;

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
    public partial class DreamRoad : MonoBehaviour
    {
        public static DreamRoad Instance { get; private set; }

        // Площадь печатей (12.11, план 66): между фортом и склепом — мощёный двор для уроков
        // лука, противовеса, двух душ, последнего щита и печатей. Ворота отодвинуты на 28 м.
        public const float From = -31f, GateZ = 58f, Half = 7f;
        public const float SquareFrom = 30f, SquareTo = 54f, SquareHalf = 11f;
        public static readonly Vector3 AltarAt = new Vector3(-6.8f, 0f, 38f);
        public static readonly Vector3 ArchZ = new Vector3(0f, 0f, 46f);
        public static readonly Vector3 FallenColumnAt = new Vector3(6.8f, 0f, 42f);
        public static readonly Vector3 WarriorSealAt = new Vector3(-1.9f, 0f, 53.4f);
        public static readonly Vector3 LordSealAt = new Vector3(1.9f, 0f, 53.4f);
        /// <summary>Форт: передняя стена с воротами, задняя с проломом, полуширина двора.</summary>
        public const float FortFront = 14f, FortBack = 24f, FortHalf = 13f;
        // Карта обучения (19.11, DreamRoadMap): чучело — на стоянке убийц у дороги, грудью к ней (на юг);
        // камень с банками — на площади у колонн, за бревном через расщелину.
        public static readonly Vector3 DummyAt = new Vector3(-82.6f, 0f, 34.4f);
        public const float DummyYaw = 180f;
        public static readonly Vector3 ShrineAt = new Vector3(-12.6f, 0f, -21.1f);
        public static readonly Vector3 GateFront = new Vector3(0f, 0f, GateZ - 1.6f);

        /// <summary>Ворота склепа: −1 — не открывали; 0…1 — сколько погас экран. Спрашивает прогон.</summary>
        public static float CryptFade { get; private set; } = -1f;

        private class Candle { public Vector3 At; public Transform Flame; public Light Light; public float Lit = -1f; public float Power; public Vector3 Size; }
        private readonly List<Candle> _candles = new List<Candle>();
        private Transform _dummy, _doorLeft, _doorRight;
        private GameObject _seam;
        private Light _cryptLight;
        private Quaternion _dummyRest = Quaternion.identity;
        // Ответный замах чучела (план 66, сцена 5: «блок должен быть виден»): после удара
        // перекладина с пучками описывает дугу в сторону бьющего; встретил блоком — засчитано.
        private Transform _dummyArms;
        private float _riposteAt = -1f;
        private bool _riposteChecked;
        /// <summary>Сколько ответных замахов чучела встречено блоком / пропущено.</summary>
        public static int DummyBlocks { get; private set; }
        public static int DummyRipostes { get; private set; }
        private Vector2 _tilt, _tiltVel;      // наклон чучела, градусы по x и z мира
        private float _spin, _spinVel;        // поворот на коле
        private readonly List<GameObject> _soulJar = new List<GameObject>();

        // Силуэт и его жизнь — Death, Haunt.
        private Transform _death, _deathHood, _deathScythe, _deathHem;
        private float _deathYaw;
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
        private static void Rearm() { CryptFade = -1f; DummyHits = 0; DummyBlocks = 0; DummyRipostes = 0; }

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
                foreach (var (z0, z1) in new[] { (From - 0.5f, FortFront), (FortBack, SquareFrom), (SquareTo, GateZ + 0.5f) })
                {
                    for (float z = z0; z < z1;)
                    {
                        float l = Mathf.Min(R(1.6f, 2.4f), z1 - z + 0.3f);
                        var stone = stones[rnd.Next(stones.Length)];
                        var pos = new Vector3(Half * s + R(-0.15f, 0.15f), 0f, z + l * 0.5f);
                        var size = new Vector3(R(0.8f, 1.0f), R(0.6f, 1.0f), l + 0.25f);
                        var turn = Quaternion.Euler(0f, R(-5f, 5f), R(-3f, 3f));
                        z += l;
                        // Пролом к колоннам карты обучения: камней западной обочины напротив площади нет, жребий тот же.
                        if (s < 0f && pos.z > PlazaSouth + 0.6f && pos.z < PlazaNorth - 0.6f) continue;
                        var rock = Piece(t, PrimitiveType.Cube, "Кладка", pos, size, stone);
                        rock.transform.position += Vector3.up * size.y * 0.5f;
                        rock.transform.rotation = turn;
                    }
                }
            road.Fort(t, rnd, stones);
            road.Square(t, rnd, stones);
            // Позади — завал: назад дороги нет.
            for (float x = -Half; x <= Half; x += 1.7f)
            {
                var rock = Piece(t, PrimitiveType.Cube, "Завал", new Vector3(x, 0f, From - 0.8f + R(-0.2f, 0.2f)),
                                 new Vector3(2.0f, R(0.9f, 1.6f), 1.2f), stones[rnd.Next(stones.Length)]);
                rock.transform.position += Vector3.up * rock.transform.localScale.y * 0.5f;
                rock.transform.rotation = Quaternion.Euler(R(-6f, 6f), R(-20f, 20f), R(-6f, 6f));
            }

            // Мёртвые деревья за обочинами — силуэты в тумане.
            for (int i = 0; i < 24; i++)
            {
                float s = i % 2 == 0 ? -1f : 1f;
                var at = new Vector3(s * R(12.5f, 18f), 0f, Mathf.Lerp(From + 2f, GateZ - 3f, i / 23f) + R(-2f, 2f));
                road.Tree(t, at, R(4f, 6.5f), rnd, bark);
                // Площадь у колонн — без деревьев; жребий тот же.
                if (at.x > PlazaWest - 1.5f && at.x < PlazaEast && at.z > PlazaSouth - 1.5f && at.z < PlazaNorth + 1.5f)
                    DestroyImmediate(t.GetChild(t.childCount - 1).gameObject);
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
            // Карта обучения — западнее (docs/68): опушка, стоянка, мост, засада, лес, бревно, колонны.
            road.BuildMap(t, earth, rut, bark, stones, post);
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


        /// <summary>
        /// Площадь печатей: мощёный двор от форта до склепа (план 66, сцены 7–10). Низкая
        /// разрушенная ограда и обломки колоннады по бокам; слева — каменный алтарь (на нём
        /// ляжет лук); посередине — арка с бронзовым противовесом на цепи, под ним чаша;
        /// справа — поваленная колонна; перед воротами — две печати, резанные в камне.
        /// Здесь только камень и бронза — никаких дисков и шаров-маркеров.
        /// </summary>
        private void Square(Transform parent, System.Random rnd, Material[] stones)
        {
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var root = new GameObject("Площадь печатей").transform;
            root.SetParent(parent, false);
            var dressed = Lit(new Color(0.25f, 0.25f, 0.27f), 0.12f);
            var dark = Lit(new Color(0.17f, 0.17f, 0.19f), 0.1f);
            var slab = Lit(new Color(0.145f, 0.14f, 0.15f), 0.08f);
            var bronze = Lit(new Color(0.42f, 0.30f, 0.14f), 0.62f);
            float mid = (SquareFrom + SquareTo) * 0.5f, len = SquareTo - SquareFrom;

            // Мощение — один настил. Сорок плит вразнобой поверх него убраны (автор, 17.11: «плиты,
            // которые просто есть»): они читались вещами, с которыми что-то делают. Жребий
            // прокручиваем, как раньше, — ограда и колонны стоят там же, где стояли.
            Flat(root, "Мощение площади", new Vector3(0f, 0.02f, mid), new Vector2(SquareHalf * 2f, len), slab);
            for (int i = 0; i < 40; i++)
            {
                R(0f, 1f); R(0f, 1f); R(0f, 1f); R(0f, 1f); R(0f, 1f); rnd.Next(stones.Length);
                R(0f, 1f); R(0f, 1f); R(0f, 1f);
            }

            // Ограда: невысокая, с провалами; углы — обломки башенок.
            WallZ(root, -SquareHalf, SquareFrom, SquareTo, 0.7f, 1.7f, rnd, dressed, dark, new Vector2(36f, 41f));
            WallZ(root, SquareHalf, SquareFrom, SquareTo, 0.7f, 1.7f, rnd, dressed, dark, new Vector2(44f, 48f));
            WallX(root, SquareFrom, -SquareHalf, -2.6f, 0.6f, 1.4f, rnd, dressed, dark, new Vector2(-9f, -6f));
            WallX(root, SquareFrom, 2.6f, SquareHalf, 0.6f, 1.4f, rnd, dressed, dark, null);
            foreach (float s in new[] { -1f, 1f })
            {
                Tower(root, new Vector3(s * SquareHalf, 0f, SquareFrom), 2.2f, R(2.2f, 3.4f), false, dressed, dark);
                Tower(root, new Vector3(s * SquareHalf, 0f, SquareTo), 2.2f, R(1.6f, 2.8f), false, dressed, dark);
            }

            // Колоннада: пни колонн по обе стороны, две-три уцелели повыше.
            for (float z = SquareFrom + 3f; z < SquareTo - 2f; z += 4.2f)
                foreach (float s in new[] { -1f, 1f })
                {
                    float h = rnd.Next(4) == 0 ? R(3.6f, 4.8f) : R(0.9f, 2.4f);
                    var col = Piece(root, PrimitiveType.Cylinder, "Колонна", new Vector3(s * 8.6f, h * 0.5f, z + R(-0.3f, 0.3f)), new Vector3(0.75f, h * 0.5f, 0.75f), dressed);
                    col.transform.rotation = Quaternion.Euler(R(-2f, 2f), 0f, R(-2f, 2f));
                    Piece(root, PrimitiveType.Cube, "База колонны", new Vector3(s * 8.6f, 0.17f, z), new Vector3(1.1f, 0.34f, 1.1f), dark);
                }

            // Алтарь слева: плита на двух блоках, на ней бронзовая пластина с руной Гордыни.
            Piece(root, PrimitiveType.Cube, "Алтарь — опора", AltarAt + new Vector3(-0.6f, 0.4f, 0f), new Vector3(0.6f, 0.8f, 0.9f), dark);
            Piece(root, PrimitiveType.Cube, "Алтарь — опора", AltarAt + new Vector3(0.6f, 0.4f, 0f), new Vector3(0.6f, 0.8f, 0.9f), dark);
            Piece(root, PrimitiveType.Cube, "Алтарь — плита", AltarAt + Vector3.up * 0.9f, new Vector3(2.2f, 0.2f, 1.1f), dressed);
            foreach (float s in new[] { -1f, 1f })
                AddCandle(root, AltarAt + new Vector3(s * 1.5f, 0f, -0.9f), dark, 2.5f);

            // Арка противовеса: два пилона, перемычка, цепь, бронзовый груз; под ним каменная чаша.
            foreach (float s in new[] { -1f, 1f })
            {
                Piece(root, PrimitiveType.Cube, "Пилон арки", new Vector3(s * 4.6f, 3.3f, ArchZ.z), new Vector3(1.0f, 6.6f, 1.0f), dressed);
                Piece(root, PrimitiveType.Cube, "База пилона", new Vector3(s * 4.6f, 0.2f, ArchZ.z), new Vector3(1.5f, 0.4f, 1.5f), dark);
                Piece(root, PrimitiveType.Cube, "Капитель пилона", new Vector3(s * 4.6f, 6.75f, ArchZ.z), new Vector3(1.4f, 0.3f, 1.4f), dark);
            }
            Piece(root, PrimitiveType.Cube, "Перемычка арки", new Vector3(0f, 7.15f, ArchZ.z), new Vector3(10.6f, 0.5f, 1.1f), dressed);
            Piece(root, PrimitiveType.Cylinder, "Цепь противовеса", new Vector3(0f, 5.9f, ArchZ.z), new Vector3(0.06f, 1.0f, 0.06f), dark, false);
            var weight = Piece(root, PrimitiveType.Cylinder, "Бронзовый противовес", new Vector3(0f, 4.4f, ArchZ.z), new Vector3(0.9f, 0.55f, 0.9f), bronze);
            Piece(root, PrimitiveType.Cylinder, "Кольцо груза", new Vector3(0f, 5.0f, ArchZ.z), new Vector3(0.3f, 0.06f, 0.3f), dark, false)
                .transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Piece(root, PrimitiveType.Cylinder, "Чаша противовеса", new Vector3(0f, 0.14f, ArchZ.z), new Vector3(2.4f, 0.14f, 2.4f), dark);
            // Дно чаши — камень, а не светящийся круг: светящийся читался кнопкой в полу.
            Piece(root, PrimitiveType.Cylinder, "Чаша — дно", new Vector3(0f, 0.29f, ArchZ.z), new Vector3(1.8f, 0.02f, 1.8f), slab, false);

            // Поваленная колонна справа — у неё ляжет раненый.
            var fallen = Piece(root, PrimitiveType.Cylinder, "Поваленная колонна", FallenColumnAt + Vector3.up * 0.4f, new Vector3(0.8f, 2.2f, 0.8f), dressed);
            fallen.transform.rotation = Quaternion.Euler(0f, 20f, 90f);
            Piece(root, PrimitiveType.Cylinder, "Барабан колонны", FallenColumnAt + new Vector3(-1.6f, 0.3f, 1.4f), new Vector3(0.8f, 0.3f, 0.8f), dressed)
                .transform.rotation = Quaternion.Euler(8f, 0f, 12f);

            // Печати перед воротами: резьба в камне — диск, кольцо и знак, свет едва тлеет.
            // Это замок ворот (сцена 10): встал на печать — её знак разгорается (LightSeal).
            _warriorRune = Seal(root, WarriorSealAt, true, dark, dressed);
            _lordRune = Seal(root, LordSealAt, false, dark, dressed);

            // Четыре холодных фонаря по углам площади.
            var iron = Lit(new Color(0.12f, 0.11f, 0.11f), 0.4f);
            foreach (float s in new[] { -1f, 1f })
            {
                Brazier(root, new Vector3(s * 9.2f, 0f, SquareFrom + 2.2f), new Color(0.55f, 0.75f, 1f), 6f, iron);
                Brazier(root, new Vector3(s * 9.2f, 0f, SquareTo - 2.2f), new Color(0.55f, 0.75f, 1f), 6f, iron);
            }
        }

        private static readonly Color RuneDim = new Color(0.22f, 0.40f, 0.56f) * 0.7f;
        private static readonly Color RuneLit = new Color(0.62f, 0.90f, 1f) * 1.6f;
        private Material _warriorRune, _lordRune;

        /// <summary>Знак печати разгорается, пока на ней стоят (DreamTutorialDirector.Seals).</summary>
        public void LightSeal(bool warrior, bool on)
        {
            var rune = warrior ? _warriorRune : _lordRune;
            if (rune != null) rune.color = on ? RuneLit : RuneDim;
        }

        /// <summary>Печать в полу: каменный диск, кольцо, знак воина (меч) или владыки (венец). Возвращает знак.</summary>
        private static Material Seal(Transform parent, Vector3 at, bool warrior, Material dark, Material dressed)
        {
            var root = new GameObject(warrior ? "Печать воина" : "Печать владыки").transform;
            root.SetParent(parent, false);
            Piece(root, PrimitiveType.Cylinder, "Диск", at + Vector3.up * 0.03f, new Vector3(2.6f, 0.03f, 2.6f), dressed, false);
            Piece(root, PrimitiveType.Cylinder, "Кольцо", at + Vector3.up * 0.065f, new Vector3(2.2f, 0.012f, 2.2f), dark, false);
            Piece(root, PrimitiveType.Cylinder, "Поле", at + Vector3.up * 0.07f, new Vector3(1.9f, 0.012f, 1.9f), dressed, false);
            var rune = Glowing(new Color(0.22f, 0.40f, 0.56f), 0.7f);
            if (warrior)
            {
                Piece(root, PrimitiveType.Cube, "Клинок", at + new Vector3(0f, 0.085f, 0.1f), new Vector3(0.14f, 0.012f, 1.3f), rune, false);
                Piece(root, PrimitiveType.Cube, "Гарда", at + new Vector3(0f, 0.085f, -0.35f), new Vector3(0.7f, 0.012f, 0.12f), rune, false);
            }
            else
            {
                Piece(root, PrimitiveType.Cube, "Обод венца", at + new Vector3(0f, 0.085f, -0.3f), new Vector3(1.2f, 0.012f, 0.14f), rune, false);
                foreach (float x in new[] { -0.45f, 0f, 0.45f })
                    Piece(root, PrimitiveType.Cube, "Зубец венца", at + new Vector3(x, 0.085f, 0.1f), new Vector3(0.14f, 0.012f, 0.7f), rune, false);
            }
            return rune;
        }

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
            // Перекладина и пучки — на своей оси: ответный замах крутит их вокруг кола.
            _dummyArms = new GameObject("Размах").transform;
            _dummyArms.SetParent(_dummy, false);
            _dummyArms.localPosition = Vector3.up * 1.66f;
            for (int i = _dummy.childCount - 1; i >= 0; i--)
            {
                var part = _dummy.GetChild(i);
                if (part.name == "Руки" || part.name == "Пучок") part.SetParent(_dummyArms, true);
            }
            _dummyRest = Quaternion.Euler(0f, DummyYaw, 0f);
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
            Piece(root, PrimitiveType.Cube, "Пол склепа", new Vector3(0f, 0.02f, z + 5f), new Vector3(13f, 0.04f, 9f), black, false);
            // Свод — десять метров (12.11): величие — это когда фигура не помещается в проём ворот,
            // а внутри ей есть где стоять. Снаружи высоты не видно: вход тот же.
            // Зал за воротами шире ворот (12.11): Смерть стоит сбоку, и мимо неё идут к свету.
            Piece(root, PrimitiveType.Cube, "Стена склепа левая", new Vector3(-6.7f, 5f, z + 5f), new Vector3(0.4f, 10f, 9f), black, false);
            Piece(root, PrimitiveType.Cube, "Стена склепа правая", new Vector3(6.7f, 5f, z + 5f), new Vector3(0.4f, 10f, 9f), black, false);
            Piece(root, PrimitiveType.Cube, "Свод склепа", new Vector3(0f, 10.1f, z + 5f), new Vector3(13.8f, 0.4f, 9f), black, false);
            Piece(root, PrimitiveType.Cube, "Глубь склепа", new Vector3(0f, 5f, z + 9.7f), new Vector3(13.8f, 10f, 0.4f), black, false);
            // Изнанка ворот: за крыльями фасада зал шире, и щели по бокам надо закрыть.
            foreach (float s in new[] { -1f, 1f })
                Piece(root, PrimitiveType.Cube, "Стена у входа", new Vector3(s * 4.6f, 5f, z + 1.6f), new Vector3(4.6f, 10f, 0.4f), black, false);

            // В глубине — бледный холодный проём, против него стоит Он.
            _backGlow = Glowing(cold, 1.5f);
            _backColour = _backGlow.color;
            var back = Piece(root, PrimitiveType.Quad, "Свет в глубине", new Vector3(0f, 4.9f, z + 9.45f), new Vector3(7.6f, 9.8f, 1f), _backGlow, false);
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
                Piece(root, PrimitiveType.Quad, "Ореол проёма", new Vector3(0f, 4.9f, z + 9.35f), new Vector3(10.0f, 11.5f, 1f), halo, false);
                Rays(root, new Vector3(0f, 5.2f, z + 9.25f), cold);
            }
            // Смерть не заперта и не держит (автор, 12.11): стоит сбоку в глубине, провожает идущего.
            // Автор, 12.11: «мы идём к нему» — стоит посередине, лицом к вошедшему; слово своё
            // скажет не здесь, а когда Безымянный пойдёт к свету уже в лагере (OpeningReveal).
            Death(root, new Vector3(0f, 0f, z + 6.6f), black);
            _deathYaw = 0f;
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
        /// висит над полом, подол — лохмотьями, под ними видно свет. Голова опущена; руки не движутся —
        /// <see cref="Haunt"/> поднимает голову к вошедшему и зажигает глаза.
        /// От него поднимается чёрный дым — против света он виден, на чёрном нет.
        /// </summary>
        private void Death(Transform parent, Vector3 at, Material black)
        {
            var root = new GameObject("Бог Смерти — силуэт").transform;
            root.SetParent(parent, false);
            root.position = at;
            _death = root;
            _deathAt = at;
            // Третий заход (автор, 12.11: «сделать силуэт более величественным»; план 66 §5).
            // Он в полтора раза выше прежнего — семь с половиной метров, выше ворот: виден от
            // груди, капюшон режет перемычка, когда камера подходит. Не вырезка из картона:
            // ткань чуть серая и гладкая, сзади у него свой свет — по краю балахона идёт ободок,
            // складки читаются по нему. Руки спрятаны в рукавах; видна одна кисть — на косовище.
            // Капюшон глубокий, клювом вперёд, без острия. Коса в полтора роста, лезвие широкой
            // дугой над капюшоном. Подол не касается пола — уходит в туман.
            // Чёрный без блеска: в v3 ткань с подсветкой вышла серебряной и перестала быть силуэтом.
            // Объём дают лучи за спиной, которые он режет собой, и форма.
            var cloth = black;
            var steel = black;
            const float hem = 0.6f;
            Shape(root, "Балахон", Lathe(new[] { hem, 1.86f, 1.40f, 1.67f, 2.50f, 1.44f, 3.60f, 1.30f,
                                                 4.35f, 1.36f, 4.80f, 1.33f, 5.10f, 0.93f, 5.30f, 0.53f },
                                         36, 0.62f, 13, 0.07f), at, Quaternion.identity, cloth);
            _deathHem = Shape(root, "Лохмотья", Tatters(1.86f, 34, 1.1f, 13), at + Vector3.up * hem, Quaternion.identity, cloth);
            _deathHem.localScale = new Vector3(1f, 1f, 0.62f);
            Shape(root, "Накидка", Lathe(new[] { 3.90f, 1.75f, 4.35f, 1.70f, 4.75f, 1.55f, 5.05f, 1.15f,
                                                 5.30f, 0.70f, 5.45f, 0.45f },
                                         30, 0.66f, 9, 0.05f, ragged: 0.45f), at, Quaternion.identity, cloth);

            _deathHood = new GameObject("Голова").transform;
            _deathHood.SetParent(root, false);
            _deathHood.position = at + Vector3.up * 5.4f;
            // Куколь: ткань от плеч вверх, сходится клювом вперёд над лицом; спереди вырез — тьма и глаза.
            Shape(_deathHood, "Куколь", Lathe(new[] { 5.05f, 1.10f, 5.60f, 1.06f, 6.30f, 0.98f, 6.90f, 0.78f, 7.30f, 0.45f, 7.58f, 0.10f },
                                            30, 1.0f, 0, 0f, lean: -0.55f, arc0: 300f, arc1: 600f), at, Quaternion.identity, cloth);
            Piece(_deathHood, PrimitiveType.Sphere, "Голова в тени", at + new Vector3(0f, 6.25f, 0.05f), new Vector3(0.9f, 1.05f, 0.9f), cloth, false);
            var eyeGlow = Glowing(new Color(0.50f, 0.72f, 1f), 3f);
            foreach (float s in new[] { -1f, 1f })
                _deathEyes.Add(Piece(_deathHood, PrimitiveType.Sphere, "Глаз", at + new Vector3(0.26f * s, 6.30f, -0.50f), Vector3.zero, eyeGlow, false).transform);
            _deathHood.localRotation = Quaternion.Euler(16f, 0f, 0f);

            // Левая рука в рукаве до земли — кисти не видно.
            Shape(root, "Левый рукав", Sheet(new Vector3(-1.47f, 4.19f, -0.15f), new Vector3(-1.70f, 3.56f, -0.19f),
                                             new Vector3(-1.75f, 2.71f, -0.19f), new Vector3(-1.61f, 2.25f, -0.15f),
                                             new Vector3(-1.40f, 2.63f, -0.12f), new Vector3(-1.30f, 3.72f, -0.09f)),
                  at, Quaternion.identity, cloth);
            // Правая — на косе; рукав свисает с предплечья.
            Shape(root, "Правый рукав", Sheet(new Vector3(1.20f, 4.42f, -0.14f), new Vector3(1.50f, 4.12f, -0.20f),
                                              new Vector3(1.56f, 3.87f, -0.26f), new Vector3(1.48f, 3.66f, -0.22f),
                                              new Vector3(1.30f, 2.51f, -0.16f), new Vector3(1.05f, 3.10f, -0.10f)),
                  at, Quaternion.identity, cloth);
            _deathScythe = new GameObject("Коса").transform;
            _deathScythe.SetParent(root, false);
            _deathScythe.position = at + new Vector3(1.45f, 3.9f, -0.25f);
            for (int f = 0; f < 4; f++)
            {
                var knuckle = Piece(_deathScythe, PrimitiveType.Cylinder, "Палец", at + new Vector3(1.42f, 3.72f + f * 0.11f, -0.37f),
                                    new Vector3(0.05f, 0.18f - 0.018f * f, 0.05f), cloth, false);
                knuckle.transform.rotation = Quaternion.Euler(0f, 0f, 90f - 8f * f);
            }
            var snath = Piece(_deathScythe, PrimitiveType.Cylinder, "Косовище", at + new Vector3(1.45f, 4.2f, -0.25f), new Vector3(0.12f, 3.9f, 0.12f), cloth, false).transform;
            snath.rotation = Quaternion.Euler(0f, 0f, 4f);
            Shape(_deathScythe, "Лезвие", Blade(3.2f, 0.8f), snath.position + snath.up * 3.9f, Quaternion.identity, steel);

            // Свет за спиной: ободок по краю ткани и блик по кромке лезвия.
            var rimGo = new GameObject("Свет за спиной");
            rimGo.transform.SetParent(root, false);
            rimGo.transform.position = at + new Vector3(0f, 5.2f, 2.4f);
            var rim = rimGo.AddComponent<Light>();
            rim.type = LightType.Point;
            rim.color = new Color(0.62f, 0.76f, 1f);
            rim.range = 9f;
            rim.intensity = 4f;
            rim.shadows = LightShadows.None;

            // Чёрный дым от него: виден против света, на чёрном пропадает.
            var smoke = Haze(new Color(0f, 0f, 0f), additive: false);
            if (smoke != null)
                _deathSmoke = Wisps(root, "Дым", at + Vector3.up * 3.6f, smoke, new Vector3(3.8f, 5.0f, 0.9f), 12f,
                                    new Vector2(2.5f, 4f), new Vector2(0.8f, 1.6f), Color.black,
                                    new Vector3(-0.15f, 0.25f, -0.05f), new Vector3(0.15f, 0.55f, 0.05f), 0.5f, prewarm: true);
            // Подол уходит в холодный туман.
            var mist = Haze(new Color(0.60f, 0.72f, 0.95f), additive: true);
            if (mist != null)
                Wisps(root, "Туман у подола", at + Vector3.up * 0.45f, mist, new Vector3(3.6f, 0.5f, 2.2f), 10f,
                      new Vector2(2f, 3.5f), new Vector2(1.0f, 2.0f), new Color(0.60f, 0.72f, 0.95f),
                      new Vector3(-0.05f, 0.05f, -0.05f), new Vector3(0.05f, 0.15f, 0.05f), 0.12f, prewarm: true);
        }

        /// <summary>
        /// Лучи из проёма: полосы света веером от его середины, в плоскости проёма. Силуэт
        /// стоит перед ними и режет их собой — так он читается объёмом, а не дырой в свете.
        /// </summary>
        private static void Rays(Transform parent, Vector3 origin, Color cold)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) return;
            var m = new Material(shader) { name = "Луч" };
            var tex = Streak(64);
            m.SetTexture("_BaseMap", tex);
            m.mainTexture = tex;
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetColor("_BaseColor", new Color(cold.r, cold.g, cold.b, 0.32f));
            var rnd = new System.Random(11);
            for (int i = 0; i < 11; i++)
            {
                float ang = Mathf.Lerp(-78f, 78f, i / 10f) + ((float)rnd.NextDouble() - 0.5f) * 8f;
                float len = 9f + 5f * (float)rnd.NextDouble();
                float wide = 0.45f + 0.7f * (float)rnd.NextDouble();
                var dir = Quaternion.Euler(0f, 0f, ang) * Vector3.up;
                var ray = Piece(parent, PrimitiveType.Quad, "Луч", origin + dir * (len * 0.5f), new Vector3(wide, len, 1f), m, false);
                ray.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir);
            }
        }

        /// <summary>Полоса: мягкая по ширине, гаснет к концу.</summary>
        private static Texture2D Streak(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float w = 1f - Mathf.Abs((x + 0.5f) / size * 2f - 1f);
                    float l = 1f - (y + 0.5f) / size;
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, w * w * l * l));
                }
            t.Apply();
            return t;
        }

        /// <summary>Жизнь силуэта, пока гаснет экран: t — секунды от скрипа дверей.</summary>
        private void Haunt(float t, float breath)
        {
            if (_death == null) return;
            _death.position = _deathAt + Vector3.up * (0.07f * Mathf.Sin(t * 1.25f));
            if (_deathHem != null) _deathHem.localRotation = Quaternion.Euler(0f, 5f * Mathf.Sin(t * 0.8f), 0f);
            if (_deathHood != null)
                _deathHood.localRotation = Quaternion.Euler(Mathf.Lerp(14f, -5f, Smooth(Mathf.InverseLerp(0.6f, 2.3f, t))), 0f, 0f);
            float eyes = Smooth(Mathf.InverseLerp(1.2f, 1.7f, t)) * (0.9f + 0.1f * Mathf.Sin(t * 19f));
            foreach (var e in _deathEyes) if (e != null) e.localScale = Vector3.one * (0.12f * eyes);
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

        /// <summary>
        /// Тело вращения по профилю (y, r, y, r…) снизу вверх; в глубину — эллипс (depth).
        /// Продольные складки (folds, fold — доля радиуса) сильнее книзу: у плеч ткань натянута.
        /// ragged — рваный нижний край: нижний ряд опущен зубцами разной длины. Видно с обеих сторон.
        /// </summary>
        private static Mesh Lathe(float[] profile, int n, float depth, int folds, float fold, float ragged = 0f,
                                  float lean = 0f, float arc0 = 0f, float arc1 = 360f)
        {
            var v = new List<Vector3>();
            var tri = new List<int>();
            int rows = profile.Length / 2;
            var rnd = new System.Random(5);
            var drop = new float[n + 1];
            for (int i = 0; i < n; i++) drop[i] = ragged * (0.3f + 0.7f * (float)rnd.NextDouble());
            drop[n] = drop[0];
            for (int r = 0; r < rows; r++)
            {
                float y = profile[r * 2], rad = profile[r * 2 + 1];
                float amp = fold * (1f - r / (float)Mathf.Max(1, rows - 1));
                float part = r / (float)Mathf.Max(1, rows - 1);
                for (int i = 0; i <= n; i++)
                {
                    float a = Mathf.Deg2Rad * Mathf.Lerp(arc0, arc1, i / (float)n);
                    float k = folds > 0 ? rad * (1f + amp * Mathf.Sin(a * folds)) : rad;
                    v.Add(new Vector3(Mathf.Cos(a) * k, r == 0 ? y - drop[i] : y, Mathf.Sin(a) * k * depth + lean * part));
                }
            }
            for (int r = 0; r < rows - 1; r++)
                for (int i = 0; i < n; i++)
                {
                    int a0 = r * (n + 1) + i, a1 = a0 + 1, b0 = a0 + n + 1, b1 = b0 + 1;
                    tri.AddRange(new[] { a0, b0, b1, a0, b1, a1 });
                    tri.AddRange(new[] { a0, b1, b0, a0, a1, b1 });
                }
            var m = new Mesh { name = "Тело вращения" };
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
            _riposteAt = Time.time + 0.5f;
            _riposteChecked = false;
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
            RevealMap();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_dummy != null && (_tilt.sqrMagnitude > 1e-4f || _tiltVel.sqrMagnitude > 1e-4f
                                   || Mathf.Abs(_spin) > 0.01f || Mathf.Abs(_spinVel) > 0.01f))
            {
                // Мелкими шагами и с сильным гашением: шаг в кадр и гашение 6 давали отдачу
                // сильнее толчка — через долю секунды чучело валилось на бьющего (кадр w0811g).
                int steps = Mathf.Clamp(Mathf.CeilToInt(dt * 120f), 1, 12);
                float h = dt / steps;
                for (int k = 0; k < steps; k++)
                {
                    _tiltVel += (-90f * _tilt - 13f * _tiltVel) * h;
                    _tilt = Vector2.ClampMagnitude(_tilt + _tiltVel * h, 25f);
                    _spinVel += (-25f * _spin - 6f * _spinVel) * h;
                    _spin = Mathf.Clamp(_spin + _spinVel * h, -40f, 40f);
                }
                var d3 = new Vector3(_tilt.x, 0f, _tilt.y);
                var lean = d3.sqrMagnitude > 1e-6f ? Quaternion.AngleAxis(d3.magnitude, Vector3.Cross(Vector3.up, d3.normalized)) : Quaternion.identity;
                // В мировых осях: покой задан мировым поворотом, а родитель повёрнут — в его
                // осях чучело валилось на бьющего (кадр w0811f «удар»).
                _dummy.rotation = lean * Quaternion.Euler(0f, _spin, 0f) * _dummyRest;
            }
            if (_dummyArms != null && _riposteAt > 0f)
            {
                float ph = (Time.time - _riposteAt) / 0.55f;
                if (ph >= 1f) { _dummyArms.localRotation = Quaternion.identity; _riposteAt = -1f; }
                else if (ph >= 0f)
                {
                    _dummyArms.localRotation = Quaternion.Euler(0f, Mathf.Sin(ph * Mathf.PI) * 80f, 0f);
                    if (!_riposteChecked && ph > 0.45f)
                    {
                        _riposteChecked = true;
                        var fighter = SinbinderPlayer.Instance;
                        var fists = fighter != null ? fighter.GetComponent<FirstPersonCombat>() : null;
                        bool near = fighter != null && NearDummy(fighter.transform.position, 3.6f);
                        if (near && fists != null && fists.Blocking) { DummyBlocks++; Audio.Sfx.ShieldStrike(fighter.transform.position, 1); }
                        else if (near) DummyRipostes++;
                    }
                }
            }

            var hero = SinbinderPlayer.Instance;
            float now = Time.unscaledTime;
            foreach (var c in _candles)
            {
                if (c.Lit < 0f && hero != null && Flat(hero.transform.position - c.At).magnitude < 11.5f) c.Lit = now;
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
        private const float CryptFov = 55f;

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
            // Камера ниже глаз и смотрит вверх (12.11): величие читается снизу.
            var eye = new Vector3(Mathf.Clamp(heroAt.x, -1.0f, 1.0f), 1.25f, GateZ - 5.4f);
            var look = Quaternion.LookRotation(new Vector3(0f, 3.4f, GateZ + 6.5f) - eye);
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
                // Обзор — свой, 55°, а не тот, что был у игрока: 16.11 сон начинался с 32°, и заставка
                // взяла их — Смерть не влезла в проём, от неё осталась чёрная грудь с двумя точками
                // (автор, 16.11: «Смерть не величественная»; кадр 14.11 — целиком, с косой и капюшоном).
                float fromFov = cam.fieldOfView;
                for (float t = 0f; t < 1.3f; t += Time.unscaledDeltaTime)
                {
                    float k = Smooth(t / 1.3f);
                    cam.transform.SetPositionAndRotation(Vector3.Lerp(fromPos, eye, k), Quaternion.Slerp(fromRot, look, k));
                    cam.fieldOfView = Mathf.Lerp(fromFov, CryptFov, k);
                    yield return null;
                }
                cam.transform.SetPositionAndRotation(eye, look);
                cam.fieldOfView = CryptFov;
            }
            yield return new WaitForSecondsRealtime(0.4f);

            Creak();
            Presence();
            if (_deathSmoke != null) _deathSmoke.Play();
            if (_cryptMist != null) _cryptMist.Play();
            // Тема сна уходит вместе с дверьми: к силуэту — тишина, скрип и его гул.
            Audio.Music.Stop(3f);
            if (_seam != null) _seam.SetActive(false);

            // К нему: створки расходятся, он поднимает голову, глаза; взгляд идёт вверх, шаг
            // вперёд — и темнота. Экран гаснет раньше, чем можно рассмотреть склеп.
            var lookUp = Quaternion.LookRotation(new Vector3(0f, 5.6f, GateZ + 6.5f) - eye);
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
                    cam.transform.SetPositionAndRotation(eye + look * Vector3.forward * (2.6f * push), Quaternion.Slerp(look, lookUp, push));
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
