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
        private Renderer _warriorSeal, _lordSeal;
        private readonly List<Transform> _sealBolts = new List<Transform>();
        private float _sealTogether;
        private bool _sealsAwake;
        private bool _mechanismReleased;
        private bool _mechanismMoving;
        private GameObject _bowOnStand;
        private bool _bowTaken;
        private Sturdy _hangingTarget;

        public static readonly Vector3 BowStandAt = new Vector3(-7f, 0f, 18.2f);
        public static readonly Vector3 BowTargetAt = new Vector3(8f, 4.6f, 21.2f);
        public bool BowTaken => _bowTaken;
        public bool BowTargetBroken => _hangingTarget != null && _hangingTarget.Broken;
        public Sturdy BowTarget => _hangingTarget;

        // Загадка перед склепом: союзник на одной печати, сам Греховод на другой.
        // Вся проверка проходит по реальным позициям, не по нажатию кнопки.
        public static readonly Vector3 WarriorSealAt = new Vector3(-1.8f, 0f, 25.8f);
        public static readonly Vector3 LordSealAt = new Vector3(1.8f, 0f, 25.8f);
        public bool SealsAwake => _sealsAwake;
        /// <summary>Мишень упала на приёмную плиту, и каменные засовы отпустили две печати.</summary>
        public bool MechanismReleased => _mechanismReleased;
        private GameObject _seam;
        private Light _cryptLight;
        private Vector3 _tipAxis = Vector3.right;
        private Quaternion _dummyRest = Quaternion.identity;
        private float _tip, _tipVel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() { CryptFade = -1f; }

        private void OnDestroy() { if (Instance == this) Instance = null; }

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

            road.BuildDummy(t, DummyAt);
            road.BuildShrine(t, ShrineAt, post);
            road.BuildCrypt(t);
            road.BuildSeals(t);
            road.BuildBowChallenge(t);

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
            var flame = Piece(root, PrimitiveType.Sphere, "Огонь", at + Vector3.up * 1.42f, new Vector3(0.36f, 0.6f, 0.36f),
                              Glowing(fire, 2.0f), false).transform;
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
            var flame = Piece(root, PrimitiveType.Sphere, "Пламя", tip, new Vector3(0.16f, 0.28f, 0.16f), Glowing(fire, 2.2f), false).transform;
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

        /// <summary>
        /// Лук-искуситель на низкой высоте, а мишень на недосягаемой мечом цепи.
        /// Стрела летит только после настоящего приказа, который исполняет AOS.
        /// </summary>
        private void BuildBowChallenge(Transform parent)
        {
            var stone = Lit(new Color(0.22f, 0.22f, 0.25f), 0.3f);
            var wood = Lit(new Color(0.46f, 0.28f, 0.12f), 0.15f);
            var golden = Glowing(new Color(0.94f, 0.67f, 0.29f), 1.7f);
            Piece(parent, PrimitiveType.Cube, "Возвышение с луком", BowStandAt + Vector3.up * 0.48f,
                new Vector3(2.6f, 0.95f, 2.1f), stone);
            _bowOnStand = new GameObject("Лук тщеславия на пьедестале");
            _bowOnStand.transform.SetParent(parent, false);
            var bow = Piece(_bowOnStand.transform, PrimitiveType.Cylinder, "Изогнутое древко",
                BowStandAt + Vector3.up * 1.35f, new Vector3(0.08f, 0.75f, 0.08f), golden, false);
            bow.transform.rotation = Quaternion.Euler(0f, 0f, -25f);
            var stringLine = Piece(_bowOnStand.transform, PrimitiveType.Cylinder, "Тетива",
                BowStandAt + new Vector3(0.36f, 1.35f, 0f),
                new Vector3(0.015f, 0.78f, 0.015f), wood, false);
            stringLine.transform.rotation = Quaternion.Euler(0f, 0f, -2f);
            Piece(parent, PrimitiveType.Cube, "Знак искушения", BowStandAt + new Vector3(0f, 1.05f, -0.55f),
                new Vector3(0.95f, 0.15f, 0.2f), golden, false);

            // Подвес на высоте: обычный ближний удар и осада мечом до него не достают.
            var root = new GameObject("Замок подвесного противовеса").transform;
            root.SetParent(parent, false);
            root.position = BowTargetAt;
            var sturdy = root.gameObject.AddComponent<Sturdy>();
            sturdy.Configure("Замок противовеса", 5f, -1);
            _hangingTarget = sturdy;
            var gong = Piece(root, PrimitiveType.Cylinder, "Тяжёлая печать противовеса", BowTargetAt,
                new Vector3(1.25f, 0.12f, 1.25f), golden);
            gong.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            // Цель видна в перспективе и выделяется физическим лучом.
            Piece(root, PrimitiveType.Cube, "Узор подвесного замка", BowTargetAt + Vector3.back * 0.15f,
                new Vector3(0.7f, 0.12f, 0.18f), stone);
            Piece(parent, PrimitiveType.Cylinder, "Подвесная цепь", BowTargetAt + Vector3.up * 2.2f,
                new Vector3(0.04f, 2.2f, 0.04f), stone, false);
            // Под подвесом явно стоит чаша противовеса. Стрела сбивает груз,
            // тот падает на чашу и освобождает механическую блокировку двух плит.
            Piece(parent, PrimitiveType.Cylinder, "Приёмная чаша противовеса",
                new Vector3(BowTargetAt.x, 0.06f, BowTargetAt.z),
                new Vector3(2.5f, 0.12f, 2.5f), stone, false);
            Piece(parent, PrimitiveType.Cylinder, "Сердцевина механизма",
                new Vector3(BowTargetAt.x, 0.135f, BowTargetAt.z),
                new Vector3(0.8f, 0.025f, 0.8f),
                Glowing(new Color(0.23f, 0.42f, 0.48f), 0.9f), false);
            root.gameObject.AddComponent<DreamBowTarget>();
        }

        public bool TryTakeBow()
        {
            if (_bowTaken || !SinbinderPlayer.Exists) return false;
            if (Flat(SinbinderPlayer.Where - BowStandAt).magnitude > 2.8f) return false;
            var bag = Sinbinder.Inventory.PlayerInventory.Instance;
            if (bag == null) return false;
            var bow = new Sinbinder.Inventory.InventoryItem(
                DreamBowTarget.BowName,
                "Оружие-искуситель. Усиливает Гордыню: в споре души толкает к нападению и против бегства. "
                    + "Это не послушание: воин может выбрать не ваш приказ, а собственную битву.",
                Sinbinder.Inventory.ItemType.Equipment, 1,
                Sinbinder.Core.SinType.Pride, 32f, attack: 3f,
                slot: Sinbinder.Inventory.GearSlot.Weapon);
            if (!bag.AddItem(bow)) return false;
            _bowTaken = true;
            if (_bowOnStand != null) _bowOnStand.SetActive(false);
            Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(
                "Взяли лук тщеславия. Он искушает Гордыню. Передайте его союзнику через F — снаряжение.");
            return true;
        }

        // Печати плоские: не мешают NavMesh и не становятся препятствиями для луча приказа.
        private void BuildSeals(Transform parent)
        {
            var dormant = Glowing(new Color(0.16f, 0.32f, 0.46f), 0.8f);
            var stoneLock = Lit(new Color(0.27f, 0.28f, 0.32f), 0.08f);
            foreach (var (at, name, ally) in new[]
            {
                (WarriorSealAt, "Печать соратника", true),
                (LordSealAt, "Печать Греховода", false)
            })
            {
                var root = new GameObject(name).transform;
                root.SetParent(parent, false);
                Piece(root, PrimitiveType.Cylinder, "Круг", at + Vector3.up * 0.045f,
                    new Vector3(2.75f, 0.045f, 2.75f), dormant, false);
                var sign = Piece(root, PrimitiveType.Cube, ally ? "Знак союзника" : "Знак владыки",
                    at + Vector3.up * 0.11f,
                    ally ? new Vector3(0.22f, 0.04f, 1.65f) : new Vector3(1.65f, 0.04f, 0.22f),
                    Glowing(new Color(0.36f, 0.62f, 0.82f), 1.15f), false);
                if (ally) _warriorSeal = sign.GetComponent<Renderer>();
                else _lordSeal = sign.GetComponent<Renderer>();
                // Поперечный засов виден до попадания по подвесу. После падения
                // груза засов действительно уходит в камень, открывая знак.
                var bolt = Piece(root, PrimitiveType.Cube, "Запор печати",
                    at + new Vector3(0f, 0.24f, 0f),
                    new Vector3(2.2f, 0.30f, 0.28f), stoneLock, false);
                _sealBolts.Add(bolt.transform);
            }
        }

        /// <summary>
        /// Падение подвесной печати запускает освобождение затворов.
        /// Перед этим две напольные печати физически перекрыты засовами;
        /// выстрел не просто переключает учебную стадию, а меняет мир.
        /// </summary>
        public void ReleaseMechanism()
        {
            if (_mechanismReleased || _mechanismMoving || !BowTargetBroken) return;
            _mechanismMoving = true;
            StartCoroutine(UnlockAfterWeightFalls());
        }

        private IEnumerator UnlockAfterWeightFalls()
        {
            // 4,6 метра свободного падения, около одной секунды.
            // Игрок видит падение прежде, чем заметит открытие печатей.
            yield return new WaitForSeconds(0.95f);
            var from = new Vector3[_sealBolts.Count];
            for (int i = 0; i < _sealBolts.Count; ++i)
                if (_sealBolts[i] != null) from[i] = _sealBolts[i].position;
            const float duration = 0.65f;
            for (float time = 0f; time < duration; time += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / duration));
                for (int i = 0; i < _sealBolts.Count; ++i)
                    if (_sealBolts[i] != null)
                        _sealBolts[i].position = Vector3.Lerp(from[i], from[i] - Vector3.up * 0.75f, k);
                yield return null;
            }
            foreach (var bolt in _sealBolts) if (bolt != null) bolt.gameObject.SetActive(false);
            if (_warriorSeal != null) _warriorSeal.material.color = new Color(0.48f, 0.9f, 1f);
            if (_lordSeal != null) _lordSeal.material.color = new Color(0.48f, 0.9f, 1f);
            _mechanismReleased = true;
            _mechanismMoving = false;
            FindFirstObjectByType<UI.BattleLogUI>()?.Write(
                "Груз ударил в чашу. Каменные засовы ушли вниз. Две печати у ворот теперь доступны.");
            Debug.Log("[СОН] Подвес сбит: чаша противовеса освободила две печати у склепа.");
        }

        /// <summary>
        /// Ворота откроются, если союзник и Греховод одновременно стоят на
        /// разных печатях 1.2 секунды. Переступил с печати — отсчёт сброшен.
        /// Воин должен дойти своим движением: здесь нет телепорта и команды мимо AOS.
        /// </summary>
        public bool CheckSeals(Warrior ally, Vector3 lord)
        {
            // Пока стрелой не освобождён противовес, стояние на плитах
            // ничего не даёт. Это причинная связь между двумя загадками.
            if (!_mechanismReleased) return false;
            if (_sealsAwake) return true;
            bool warrior = ally != null && !ally.IsDead &&
                           Flat(ally.transform.position - WarriorSealAt).magnitude <= 1.35f;
            bool owner = Flat(lord - LordSealAt).magnitude <= 1.35f;
            _sealTogether = warrior && owner ? _sealTogether + Time.deltaTime : 0f;
            if (_warriorSeal != null)
                _warriorSeal.material.color = warrior ? new Color(0.28f, 1f, 0.68f) : new Color(0.36f, 0.62f, 0.82f);
            if (_lordSeal != null)
                _lordSeal.material.color = owner ? new Color(0.28f, 1f, 0.68f) : new Color(0.36f, 0.62f, 0.82f);
            if (_sealTogether >= 1.2f) _sealsAwake = true;
            return _sealsAwake;
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
            var flame = Piece(root, PrimitiveType.Sphere, "Пламя", at + Vector3.up * 1.1f, new Vector3(0.06f, 0.11f, 0.06f),
                              Glowing(new Color(1f, 0.70f, 0.36f), 2.2f), false).transform;
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
            Piece(_dummy, PrimitiveType.Cylinder, "Кол", at + Vector3.up * 1.0f, new Vector3(0.12f, 1.0f, 0.12f), wood);
            Piece(_dummy, PrimitiveType.Cylinder, "Тулово", at + Vector3.up * 1.2f, new Vector3(0.62f, 0.45f, 0.5f), straw);
            Piece(_dummy, PrimitiveType.Cube, "Руки", at + Vector3.up * 1.5f, new Vector3(1.5f, 0.12f, 0.12f), wood);
            foreach (float s in new[] { -1f, 1f })
                Piece(_dummy, PrimitiveType.Sphere, "Пучок", at + new Vector3(0.78f * s, 1.5f, 0f), new Vector3(0.22f, 0.2f, 0.2f), straw, false);
            Piece(_dummy, PrimitiveType.Cylinder, "Пояс", at + Vector3.up * 1.05f, new Vector3(0.64f, 0.03f, 0.52f), wood, false);
            Piece(_dummy, PrimitiveType.Sphere, "Голова", at + Vector3.up * 1.95f, new Vector3(0.42f, 0.46f, 0.42f), sack);
            // Руками к дороге, чуть наискось.
            _dummyRest = Quaternion.Euler(0f, 20f, 0f);
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
            var glass = Glowing(new Color(0.55f, 0.75f, 1f), 0.7f);
            var lid = Lit(new Color(0.30f, 0.22f, 0.14f), 0.2f);
            for (int i = 0; i < 3; i++)
            {
                var jarAt = at + new Vector3(-0.32f + i * 0.32f, 1.11f, -0.12f);
                Piece(root, PrimitiveType.Cylinder, "Банка", jarAt, new Vector3(0.16f, 0.12f, 0.16f), glass, false);
                Piece(root, PrimitiveType.Cylinder, "Крышка", jarAt + Vector3.up * 0.13f, new Vector3(0.18f, 0.025f, 0.18f), lid, false);
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
            var back = Piece(root, PrimitiveType.Quad, "Свет в глубине", new Vector3(0f, 2.4f, z + 9.45f), new Vector3(3.0f, 4.6f, 1f), Glowing(cold, 1.5f), false);
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
                Piece(root, PrimitiveType.Quad, "Ореол проёма", new Vector3(0f, 2.4f, z + 9.35f), new Vector3(5.2f, 6.4f, 1f), halo, false);
            }
            Death(root, new Vector3(0f, 0f, z + 6.6f), black);

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
        private static void Death(Transform parent, Vector3 at, Material black)
        {
            var root = new GameObject("Бог Смерти — силуэт").transform;
            root.SetParent(parent, false);
            root.position = at;
            var robe = new GameObject("Балахон", typeof(MeshFilter), typeof(MeshRenderer));
            robe.transform.SetParent(root, false);
            robe.GetComponent<MeshFilter>().sharedMesh = Cone(0.95f, 0.36f, 2.75f, 20);
            robe.GetComponent<MeshRenderer>().sharedMaterial = black;
            Piece(root, PrimitiveType.Sphere, "Плечи", at + Vector3.up * 2.7f, new Vector3(1.15f, 0.45f, 0.62f), black, false);
            Piece(root, PrimitiveType.Sphere, "Капюшон", at + Vector3.up * 3.12f, new Vector3(0.62f, 0.74f, 0.66f), black, false);
            var tip = new GameObject("Острие капюшона", typeof(MeshFilter), typeof(MeshRenderer));
            tip.transform.SetParent(root, false);
            tip.transform.position = at + new Vector3(0f, 3.3f, 0.12f);
            tip.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
            tip.GetComponent<MeshFilter>().sharedMesh = Cone(0.26f, 0.0f, 0.5f, 12);
            tip.GetComponent<MeshRenderer>().sharedMaterial = black;
            // Рука к косовищу.
            var arm = Piece(root, PrimitiveType.Cylinder, "Рука", at, new Vector3(0.2f, 0.45f, 0.2f), black, false).transform;
            arm.position = at + new Vector3(0.62f, 2.35f, -0.1f);
            arm.rotation = Quaternion.Euler(0f, 0f, 35f);
            // Коса: косовище чуть наискось, лезвие — серп поверху.
            var snath = Piece(root, PrimitiveType.Cylinder, "Косовище", at, new Vector3(0.07f, 1.9f, 0.07f), black, false).transform;
            snath.position = at + new Vector3(0.9f, 1.85f, -0.15f);
            snath.rotation = Quaternion.Euler(0f, 0f, -6f);
            var top = snath.position + snath.up * 1.9f;
            var blade = new GameObject("Лезвие", typeof(MeshFilter), typeof(MeshRenderer));
            blade.transform.SetParent(root, false);
            blade.transform.position = top;
            blade.GetComponent<MeshFilter>().sharedMesh = Blade(1.25f, 0.2f);
            blade.GetComponent<MeshRenderer>().sharedMaterial = black;
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

        /// <summary>Греховод ударил рядом с чучелом — оно качается от него.</summary>
        public void Strike(Vector3 from)
        {
            if (_dummy == null) return;
            var d = _dummy.position - from;
            d.y = 0f;
            if (d.sqrMagnitude < 1e-4f) d = Vector3.forward;
            _tipAxis = Vector3.Cross(Vector3.up, d.normalized);
            _tipVel += 140f;
        }

        public static bool NearDummy(Vector3 at, float reach = 3.2f) => Flat(at - DummyAt).magnitude <= reach;
        public static bool NearShrine(Vector3 at, float reach = 3.4f) => Flat(at - ShrineAt).magnitude <= reach;
        public static bool NearGate(Vector3 at, float reach = 3.6f) => Flat(at - GateFront).magnitude <= reach;

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_dummy != null && (Mathf.Abs(_tip) > 0.01f || Mathf.Abs(_tipVel) > 0.01f))
            {
                _tipVel += (-90f * _tip - 6f * _tipVel) * dt;
                _tip += _tipVel * dt;
                _dummy.localRotation = Quaternion.AngleAxis(Mathf.Clamp(_tip, -25f, 25f), _tipAxis) * _dummyRest;
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
            // Тема сна уходит вместе с дверьми: к силуэту — тишина и скрип.
            Audio.Music.Stop(3f);
            if (_seam != null) _seam.SetActive(false);
            const float open = 3.4f, fade = 3.8f;
            for (float t = 0f; t < fade + 0.9f; t += Time.unscaledDeltaTime)
            {
                float d = Mathf.Pow(Smooth(Mathf.Clamp01(t / open)), 1.3f);
                if (_doorLeft != null) _doorLeft.localRotation = Quaternion.Euler(0f, -88f * d, 0f);
                if (_doorRight != null) _doorRight.localRotation = Quaternion.Euler(0f, 88f * d, 0f);
                if (_cryptLight != null) _cryptLight.intensity = 10f * d;
                float a = Mathf.Pow(Mathf.Clamp01(t / fade), 1.6f);
                black.color = new Color(0f, 0f, 0f, a);
                CryptFade = a;
                if (cam != null) cam.transform.position = eye + look * Vector3.forward * (1.8f * Smooth(Mathf.Clamp01(t / fade)));
                yield return null;
            }
            // Чёрное остаётся до загрузки лагеря: уйдёт вместе со сценой сна.
            CryptFade = 1f;
        }

        /// <summary>Скрип тяжёлых створок: низкий гул и прерывистый скрежет петель.</summary>
        private void Creak()
        {
            const int rate = 22050;
            const float seconds = 3.6f;
            int n = (int)(rate * seconds);
            var data = new float[n];
            var rnd = new System.Random(31);
            float phase = 0f, low = 0f, rumble = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Clamp01(t / 0.15f) * Mathf.Clamp01((seconds - t) / 0.8f);
                // Петли: скрежет рывками — частота ходит, громкость пульсирует.
                float f = 95f + 40f * Mathf.Sin(t * 2.3f) + 18f * Mathf.Sin(t * 7.1f);
                phase += f / rate;
                float saw = (phase % 1f) * 2f - 1f;
                float stick = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI * 2f * (6f + 3f * Mathf.Sin(t * 1.7f)))), 3f);
                low += (saw - low) * 0.25f;
                // Гул камня.
                float noise = (float)rnd.NextDouble() * 2f - 1f;
                rumble += (noise - rumble) * 0.02f;
                data[i] = env * (0.32f * low * (0.35f + 0.65f * stick) + 1.6f * rumble);
            }
            var clip = AudioClip.Create("Скрип ворот склепа", n, 1, rate, false);
            clip.SetData(data, 0);
            var src = gameObject.AddComponent<AudioSource>();
            src.clip = clip;
            src.spatialBlend = 0f;
            src.volume = 0.55f;
            src.Play();
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
