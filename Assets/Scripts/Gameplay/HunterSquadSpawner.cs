// Assets/Scripts/Gameplay/HunterSquadSpawner.cs
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Охотники — единственный противник демо (docs/00-GDD.md §8).
    ///
    /// Их задача в прологе двойная: на доле 4 проиграть, чтобы игрок
    /// поверил во всемогущество, и на доле 5 оказаться не четырьмя,
    /// а сорока. Поэтому число задаётся снаружи, а не зашито.
    ///
    /// Как и отряд игрока, охотники создаются в рантайме: личность
    /// и отношения у Warrior не сериализуются.
    /// </summary>
    public class HunterSquadSpawner : MonoBehaviour
    {
        [SerializeField] private int _count = 4;
        [SerializeField] private float _lineWidth = 6f;
        [SerializeField] private bool _spawnOnStart = true;

        [Tooltip("Уровень душ. С 24 сентября разницы между волнами он не делает: "
               + "запас тела — от оболочки (человек — 40), удар и защита до боя "
               + "пока не доходят (14-HANDOFF §60.4). Разница сейчас — в числе "
               + "и в здоровье первой волны (_firstWaveHealth).")]
        [SerializeField] private int _level = 1;

        [Tooltip("Выйти не сразу, а когда на поле не останется врагов. "
               + "Так делается вторая волна: первую надо сперва положить.")]
        [SerializeField] private bool _afterFieldClear;

        [Tooltip("Страховка второй волны: души с поля так и не собрали и они "
               + "не истлели. Волну ведёт опустевшее от душ поле, а не часы; "
               + "срок только не даёт доле зависнуть и, истекая, кричит.")]
        [SerializeField] private float _harvestSafety = 90f;

        [Tooltip("С какой долей здоровья выходит первая волна (та, что не ждёт "
               + "опустевшего поля). Охотники приходят, перебив отряды в поле, — "
               + "и первый бой лёгок их ранами, а не слабостью людей. Вторая "
               + "волна выходит свежей.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _firstWaveHealth = 0.4f;

        [Tooltip("Что говорит журнал, когда эта волна выходит. Пусто — молчит.")]
        [TextArea(1, 3)]
        [SerializeField] private string _announce = "";

        [Tooltip("Открыть ли край карты, выйдя. Ставить второй волне: "
               + "до неё бежать не полагается.")]
        [SerializeField] private bool _opensEscape;

        private Core.RelationshipSystem _relSystem;
        private bool _spawned;
        private bool _sawEnemies;

        /// <summary>
        /// Охотники берут в наём кого попало, поэтому души у них разные.
        /// Список фиксирован и перебирается по кругу: одинаковый вход
        /// обязан давать одинаковый выход, никакого Random.
        /// </summary>
        private static readonly Kind[] Kinds =
        {
            // Ростом, скоростью и крепостью — чтобы разницу было видно
            // на поле, а не только в душе. Драться с четырьмя одинаковыми
            // кубами неинтересно, а виды уже были: не хватало того,
            // чем они отличаются на глаз.
            new("Охотник",          SinType.Wrath,    MoralType.Vicious, 60f,
                height: 1.30f, girth: 0.55f, speed: 3.5f, toughness: 0),

            // Лёгкий и быстрый: догоняет отставших.
            new("Охотник-следопыт", SinType.Envy,     MoralType.Neutral, 45f,
                height: 1.15f, girth: 0.42f, speed: 4.8f, toughness: 0),

            // Инквизитор — рангом выше охотников, и видно это ростом
            // и крепостью (слово автора от 13 сентября: «не мясники,
            // а инквизиторы»). Гордыня, а не Чревоугодие: он пришёл
            // не есть, а судить.
            new("Инквизитор",       SinType.Pride,    MoralType.Vicious, 55f,
                height: 1.45f, girth: 0.62f, speed: 2.8f, toughness: 1,
                // Во второй волне ищет Греховода магией (Scryer): замысел
                // автора от 24 сентября. Самый медленный в стае — и потому
                // колдует из-за спин, до него надо прорваться.
                scries: true),

            // Средний во всём, и тем узнаваем.
            new("Ловчий",           SinType.Greed,    MoralType.Vicious, 50f,
                height: 1.25f, girth: 0.60f, speed: 3.9f, toughness: 0),
        };

        private readonly struct Kind
        {
            public readonly string Name;
            public readonly SinType Sin;
            public readonly MoralType Moral;
            public readonly float Intensity;

            public readonly float Height;
            public readonly float Girth;
            public readonly float Speed;

            /// <summary>
            /// Прибавка к уровню души. Жизнь, удар и защиту с 24 сентября даёт
            /// оболочка и надетое (CombatMath), а не уровень: здесь от неё
            /// осталась лишь плата и цена тела.
            /// </summary>
            public readonly int Toughness;

            /// <summary>Ищет Греховода магией, если идёт во второй волне.</summary>
            public readonly bool Scries;

            public Kind(string name, SinType sin, MoralType moral, float intensity,
                float height, float girth, float speed, int toughness, bool scries = false)
            {
                Scries = scries;
                Name = name;
                Sin = sin;
                Moral = moral;
                Intensity = intensity;
                Height = height;
                Girth = girth;
                Speed = speed;
                Toughness = toughness;
            }
        }

        /// <summary>
        /// Настроить волну, созданную не сборщиком, а по ходу сцены
        /// (<see cref="RaidEvent"/>). Звать сразу после AddComponent:
        /// Start ещё впереди.
        /// </summary>
        public void Configure(int count, float width, bool afterFieldClear,
                              bool opensEscape, string announce)
        {
            _count = count;
            _lineWidth = width;
            _afterFieldClear = afterFieldClear;
            _opensEscape = opensEscape;
            _announce = announce ?? "";
            _spawnOnStart = true;
        }

        void Start()
        {
            if (_afterFieldClear)
            {
                if (CombatManager.Instance != null)
                    CombatManager.Instance.OnUnitsChanged += OnUnitsChanged;
                else
                    // Ждать нечего и некого: без боевого менеджера событие
                    // не придёт никогда, и волна не выйдет молча.
                    Debug.LogWarning("[ПРОЛОГ] CombatManager в сцене нет — "
                                   + "вторая волна не дождётся своей очереди.");
                return;
            }

            if (_spawnOnStart) SpawnHunters();
        }

        void OnDestroy()
        {
            if (CombatManager.Instance != null)
                CombatManager.Instance.OnUnitsChanged -= OnUnitsChanged;
        }

        /// <summary>
        /// Поле опустело — значит первую волну положили, и пора выходить.
        ///
        /// Ноль врагов до первой встречи ничего не значит: в этот момент
        /// первая волна ещё только создаётся. Поэтому сперва дожидаемся,
        /// чтобы враги вообще появились.
        /// </summary>
        private void OnUnitsChanged()
        {
            if (_spawned) return;

            var combat = CombatManager.Instance;
            if (combat == null) return;

            if (combat.GetAliveEnemyCount() > 0) { _sawEnemies = true; return; }
            if (!_sawEnemies || _waiting) return;

            _waiting = true;
            StartCoroutine(AfterHarvest());
        }

        private bool _waiting;

        /// <summary>
        /// Порядок из прохождения автора: лёгкий бой → сбор → подкрепление
        /// и сразу побег. До 13 сентября вторая волна выходила в тот же
        /// миг, как падал последний охотник, и урок жатвы доли 4
        /// («успей собрать, пока гаснет») шёл под ногами у свежих врагов,
        /// то есть не шёл вовсе.
        ///
        /// Сбор кончается, когда на поле не осталось гаснущих душ —
        /// собранных или истлевших, всё равно: истлевшая душа — тоже
        /// исход, о котором игрок узнал. Жатвы в сцене нет — ждать
        /// некого, и волна выходит сразу.
        /// </summary>
        private System.Collections.IEnumerator AfterHarvest()
        {
            // Кадр ожидания: смерть последнего охотника сообщается двумя
            // событиями, и порядок между «поле опустело» и «душа пошла
            // гаснуть» не гарантирован. Без паузы счётчик душ мог оказаться
            // нулём ровно в миг, когда последняя ещё не легла, — и волна
            // вышла бы, не дав её собрать.
            yield return null;

            var souls = SoulManager.Instance;

            // Сбор кончается и тогда, когда нести больше не во что: банок
            // три, а душ на поле обычно четыре. Последнюю не собрать никак,
            // и ждать, пока она истлеет, значило бы минуту стоять без дела
            // (замер DemoWalkthrough: подкрепление вышло через 60 с вместо 1).
            if (souls != null)
                yield return Beat.Until(() => souls == null || souls.FadingCount == 0
                                              || Core.Satchel.FreeJar() < 0,
                    _harvestSafety,
                    "Души на поле не собраны и не истлели — подкрепление выходит, не дожидаясь.");

            SpawnHunters();
        }

        [ContextMenu("Выпустить охотников")]
        public void SpawnHunters()
        {
            if (_spawned) return;
            _spawned = true;

            _relSystem = new Core.RelationshipSystem(AOS.MemoryProcessor.Instance);

            var setup = Object.FindFirstObjectByType<AOS.AOSSceneSetup>();

            // Новая охота — новый след: прежний, из прошлой партии или
            // прошлой волны, ничего не значит.
            if (_afterFieldClear) SinbinderTrail.Forget();

            for (int i = 0; i < _count; i++)
            {
                var hunter = SpawnHunter(i);

                // Первая волна — побитая. До 24 сентября лёгкость первого
                // боя держалась на том, что здоровье у всех было 30: запас
                // оболочки до боя не доходил. Теперь люди крепче скелетов,
                // и «бой, который нельзя проиграть» держат раны.
                if (!_afterFieldClear && hunter != null
                    && hunter.TryGetComponent<Damageable>(out var body))
                    body.Wound(_firstWaveHealth);

                // Вторая волна выходит посреди боя, когда AOSSceneSetup
                // свою работу давно сделал. Без этого вызова она осталась
                // бы без AOSWarriorWrapper — шесть тел, которые не думают
                // и ни за что не голосуют.
                if (setup != null && hunter != null) setup.SetupWarrior(hunter.gameObject);
            }

            if (setup == null)
                Debug.LogWarning("[ПРОЛОГ] AOSSceneSetup в сцене нет: "
                               + "охотники выйдут без движка решений.");

            if (!string.IsNullOrEmpty(_announce))
                Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(_announce);

            // Волна, вышедшая по опустевшему полю, — вторая. После неё
            // бежать уже пора, и край карты открывает она.
            if (_opensEscape) EscapeZone.Active?.Arm();

            Debug.Log($"[ПРОЛОГ] Охотников выпущено: {_count}, уровень {_level}.");
        }

        /// <summary>
        /// Строй в шеренгу, а не в круг: охотники приходят извне и надвигаются,
        /// тогда как лагерь сидит вокруг огня. Разница в построении читается
        /// раньше, чем разница в цвете.
        /// </summary>
        private Vector3 PlaceInLine(int index)
        {
            if (_count <= 1) return transform.position;

            float t = index / (float)(_count - 1);
            return transform.position + transform.right * Mathf.Lerp(-_lineWidth * 0.5f, _lineWidth * 0.5f, t);
        }

        /// <summary>
        /// Сердце лагеря — костёр, у которого стоит отряд. Нет его — середина
        /// карты: цель первой волны обязана быть, иначе она встанет у края.
        /// </summary>
        private static Vector3 CampCentre()
        {
            var camp = Object.FindFirstObjectByType<PrologueCampSpawner>();
            return camp != null ? camp.transform.position : Vector3.zero;
        }

        private Warrior SpawnHunter(int index)
        {
            var kind = Kinds[index % Kinds.Length];
            string name = _count > Kinds.Length ? $"{kind.Name} {index + 1}" : kind.Name;

            var go = new GameObject(name);
            go.transform.SetParent(transform);
            go.transform.position = PlaceInLine(index);
            go.transform.rotation = transform.rotation;

            var warrior = go.AddComponent<Warrior>();

            // Куда идти, когда рядом драться не с кем (HunterGoal): первая
            // волна — к костру, вторая — по следу, который находит Инквизитор.
            // Ставится до движка решений: AOSWarriorWrapper читает цель
            // при появлении.
            go.AddComponent<HunterGoal>().Configure(
                _afterFieldClear ? HunterGoal.Aim.Trail : HunterGoal.Aim.CampCentre,
                CampCentre());

            if (_afterFieldClear && kind.Scries) go.AddComponent<Scryer>();

            var soul = new SoulData(name, kind.Sin, kind.Moral,
                                    _level + kind.Toughness, kind.Intensity);
            // Живое тело: охотники — люди. До 14 сентября здесь стоял зомби,
            // и оболочка тянула их души в Чревоугодие, а движок считал
            // людей нежитью.
            warrior.Initialize(soul, ShellType.Living, _relSystem, index == 0, Team.Enemy);

            // Инквизитор крепче прочих не уровнем — уровней в игре нет, —
            // а тем, что на нём надето. Защита вещи идёт в бой через
            // Warrior.Defense (CombatMath), пока удар и защита включены.
            if (kind.Scries)
                warrior.Give(new Inventory.InventoryItem(
                    "Освящённый нагрудник",
                    "Железо с выжженным знаком Ордена. Святость — это железо, которому поверили.",
                    Inventory.ItemType.Equipment, defense: 2f));

            // Та же оснастка, что и у своих: без агента охотники стояли
            // бы в двенадцати метрах при дальности удара в два, и бой
            // доли 4 не начался бы вовсе.
            WarriorRig.Attach(go, kind.Speed);

            WarriorLook.Build(go, ShellType.Living,
                                         kind.Height, kind.Girth,
                                         kind.Height * 0.62f);

            return warrior;
        }
    }
}
