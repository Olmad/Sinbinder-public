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

        [Tooltip("Уровень душ. Он и есть разница между волнами: жизнь "
               + "20 + уровень×10, удар 3 + уровень×2, защита 1 + уровень. "
               + "Первая волна доли 4 — бой, который нельзя проиграть; "
               + "вторая обязана быть заметно сильнее.")]
        [SerializeField] private int _level = 1;

        [Tooltip("Выйти не сразу, а когда на поле не останется врагов. "
               + "Так делается вторая волна: первую надо сперва положить.")]
        [SerializeField] private bool _afterFieldClear;

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
        private static readonly (string Name, SinType Sin, MoralType Moral, float Intensity)[] Kinds =
        {
            ("Охотник",          SinType.Wrath,    MoralType.Vicious, 60f),
            ("Охотник-следопыт", SinType.Envy,     MoralType.Neutral, 45f),
            ("Охотник-мясник",   SinType.Gluttony, MoralType.Vicious, 55f),
            ("Ловчий",           SinType.Greed,    MoralType.Vicious, 50f),
        };

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
            if (!_sawEnemies) return;

            SpawnHunters();
        }

        [ContextMenu("Выпустить охотников")]
        public void SpawnHunters()
        {
            if (_spawned) return;
            _spawned = true;

            _relSystem = new Core.RelationshipSystem(AOS.MemoryProcessor.Instance);

            var setup = Object.FindFirstObjectByType<AOS.AOSSceneSetup>();

            for (int i = 0; i < _count; i++)
            {
                var hunter = SpawnHunter(i);

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

        private Warrior SpawnHunter(int index)
        {
            var kind = Kinds[index % Kinds.Length];
            string name = _count > Kinds.Length ? $"{kind.Name} {index + 1}" : kind.Name;

            var go = new GameObject(name);
            go.transform.SetParent(transform);
            go.transform.position = PlaceInLine(index);
            go.transform.rotation = transform.rotation;

            var warrior = go.AddComponent<Warrior>();
            var soul = new SoulData(name, kind.Sin, kind.Moral, _level, kind.Intensity);
            warrior.Initialize(soul, ShellType.Zombie, _relSystem, index == 0, Team.Enemy);

            // Та же оснастка, что и у своих: без агента охотники стояли
            // бы в двенадцати метрах при дальности удара в два, и бой
            // доли 4 не начался бы вовсе.
            WarriorRig.Attach(go);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Тело";
            body.transform.SetParent(go.transform);
            body.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = new Vector3(0.55f, 1.3f, 0.55f);

            return warrior;
        }
    }
}
