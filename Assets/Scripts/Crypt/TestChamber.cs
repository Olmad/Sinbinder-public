// Assets/Scripts/Crypt/TestChamber.cs
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.AOS;
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.Crypt
{
    /// <summary>
    /// Тренировочная площадка: опыт над душой, который можно повторить.
    ///
    /// <b>Зачем она вообще.</b> Пролог показывал отказ один раз: сцена
    /// случилась, и повторить её нельзя. Если игрок не понял, почему воин
    /// не послушался, — второго раза не будет, и отказ прочтётся как
    /// сломанный ИИ. Это записано как риск номер три в `07-VERDICT.md`:
    /// «отказ — машина фрустрации».
    ///
    /// Здесь наоборот. Игрок ставит условия рычагами, отдаёт приказ,
    /// смотрит, что вышло, **меняет ровно одно** и повторяет.
    ///
    /// <b>И это возможно только потому, что движок повторяем.</b> Правило
    /// «одинаковый вход даёт одинаковый выход» держится в проекте
    /// с самого начала — ради того, чтобы игрок мог учиться
    /// на объяснениях. Здесь оно превращается в механику: тот же мир,
    /// одна изменённая переменная, другой исход. Игра со случайностью
    /// внутри такого показать не может физически.
    ///
    /// Поэтому площадка держит **два** результата: прошлый и этот.
    /// Один результат — наблюдение. Два — причина.
    ///
    /// <b>Что здесь не измеряется.</b> Бой. Опыт кончается на первом
    /// решении подопытного под приказом: дальше начинают двигаться тела,
    /// а положения по-разному складываются из шагов агента, и повтор
    /// перестал бы быть повтором. Первое решение — это и есть ответ.
    /// </summary>
    public class TestChamber : MonoBehaviour
    {
        [Header("Подопытный")]
        [SerializeField] private SinType _sin = SinType.Greed;
        [SerializeField] private MoralType _moral = MoralType.Vicious;
        [SerializeField, Range(0f, 100f)] private float _intensity = 65f;
        [SerializeField, Range(0f, 100f)] private float _loyalty = 70f;

        [Header("Где что стоит")]
        [SerializeField] private Transform _subjectSpot;
        [SerializeField] private Transform _propSpot;
        [SerializeField] private Transform _enemySpot;

        [Tooltip("Сколько врагов выпускает рычаг окружения.")]
        [SerializeField] private int _enemyCount = 3;

        private readonly HashSet<Trial> _on = new();
        private readonly List<GameObject> _spawned = new();

        private Warrior _subject;
        private AOSWarriorWrapper _mind;
        private bool _recorded;

        /// <summary>Что вышло в прошлый раз и в этот. Ради них всё и стоит.</summary>
        public string Previous { get; private set; } = "";
        public string Current { get; private set; } = "";

        /// <summary>Подопытный — чтобы доска и рычаги знали, о ком речь.</summary>
        public Warrior Subject => _subject;

        /// <summary>Стоит ли сейчас это условие.</summary>
        public bool IsOn(Trial trial) => _on.Contains(trial);

        void Start() => Rebuild();

        /// <summary>Внести или убрать условие. Опыт при этом ставится заново.</summary>
        public void Toggle(Trial trial)
        {
            if (!_on.Remove(trial)) _on.Add(trial);
            Rebuild();
        }

        /// <summary>Повторить с тем же набором. Главный рычаг площадки.</summary>
        public void Repeat() => Rebuild();

        /// <summary>
        /// Сменить подопытного, не трогая условия. Второй способ менять
        /// одну переменную: те же условия, другая душа.
        /// </summary>
        public void NextSubject()
        {
            _sin = (SinType)(((int)_sin + 1) % 7);
            Rebuild();
        }

        /// <summary>
        /// Поставить всё заново из текущего набора.
        ///
        /// Позиции жёсткие, душа собирается из тех же чисел, условия
        /// вносятся в том же порядке — значит первое решение подопытного
        /// обязано совпасть с прошлым разом, если не менялось ничего.
        /// Это и есть проверяемость опыта.
        /// </summary>
        public void Rebuild()
        {
            Clear();

            // Прошлый результат переезжает в память до того, как этот
            // затрётся: сравнивать не с чем, если хранить только один.
            if (!string.IsNullOrEmpty(Current)) Previous = Current;
            Current = "";
            _recorded = false;

            _subject = SpawnSubject();
            if (_subject == null) return;

            if (_on.Contains(Trial.Unpaid)) _subject.UnpaidMissions = 3;

            if (_on.Contains(Trial.Exhausted))
            {
                var fatigue = _subject.GetComponent<Fatigue>();
                if (fatigue != null) fatigue.Spend(fatigue.Max * 0.85f);
            }

            if (_on.Contains(Trial.Loot)) SpawnLoot();
            if (_on.Contains(Trial.Wounded)) SpawnWounded();
            if (_on.Contains(Trial.Surrounded)) SpawnEnemies();
        }

        void Update()
        {
            if (_recorded || _mind == null || _subject == null) return;

            // Ждём решения, принятого при стоящем приказе: без приказа
            // спорить не с чем, и записывать нечего.
            var context = _mind.LastContext;
            if (context == null || !context.HasCommand) return;

            // Проверять решение на null нельзя: Decision — структура,
            // и `== null` даже не компилируется. Проверять и не нужно:
            // AOSWarriorWrapper.Decide кладёт контекст и решение подряд,
            // одной парой строк, так что непустой контекст выше уже
            // означает, что решение принято.
            var decision = _mind.LastDecisionDetail;

            _recorded = true;

            bool obeyed = context.SatisfiedBy(decision.Action);
            string what = obeyed ? "послушался" : "не послушался";

            // Объяснение считает настоящий PhraseGenerator, а не эта
            // площадка. Своя формулировка была бы второй правдой: игрок
            // читал бы на полигоне одно, а в бою другое.
            string why = PhraseGenerator.Explain(_subject, context, decision);

            Current = string.IsNullOrEmpty(why)
                ? $"{_subject.DisplayName} {what}."
                : $"{_subject.DisplayName} {what}. {why}";

            Log(Current);
        }

        private Warrior SpawnSubject()
        {
            var spot = _subjectSpot != null ? _subjectSpot.position : transform.position;

            var go = new GameObject("Подопытный");
            go.transform.SetParent(transform);
            go.transform.position = spot;
            _spawned.Add(go);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Тело";
            body.transform.SetParent(go.transform);
            body.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            body.transform.localScale = new Vector3(0.5f, 1.05f, 0.5f);

            var warrior = go.AddComponent<Warrior>();

            // Конструктор с процессором, а не пустой: пустой существует
            // только у заглушки стенда, и однажды из-за этого не собралась
            // вся ветка. Урок записан в 14-HANDOFF §7.1.
            warrior.Initialize(new SoulData("Подопытный", _sin, _moral, 1, _intensity),
                               ShellType.Skeleton,
                               new RelationshipSystem(MemoryProcessor.Instance),
                               false, Team.Player);

            warrior.ChangeLoyalty(_loyalty - warrior.Loyalty);

            WarriorRig.Attach(go);

            var setup = Object.FindFirstObjectByType<AOSSceneSetup>();
            if (setup != null) setup.SetupWarrior(go);
            else Debug.LogWarning("[ПОЛИГОН] AOSSceneSetup в сцене нет: "
                                + "подопытный не будет ничего решать.");

            _mind = go.GetComponent<AOSWarriorWrapper>();
            return warrior;
        }

        private void SpawnLoot()
        {
            var spot = _propSpot != null ? _propSpot.position : transform.position;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Сундук";
            go.transform.SetParent(transform);
            go.transform.position = spot;
            go.transform.localScale = new Vector3(0.9f, 0.6f, 0.6f);
            _spawned.Add(go);

            var body = go.AddComponent<HarvestableBody>();

            // Золото постоянное, а не случайное. Случайность здесь
            // отменила бы весь смысл площадки: два одинаковых опыта
            // дали бы разные исходы, и игрок научился бы неверному.
            body.Initialize(ShellType.Skeleton, 12, true, "Сломанный меч");

            // Бюллетень видит добычу через CombatManager, а не через сцену.
            var combat = CombatManager.Instance;
            if (combat != null) combat.BodiesOnField.Add(body);
        }

        private void SpawnWounded()
        {
            var spot = _propSpot != null
                ? _propSpot.position + Vector3.left * 2f
                : transform.position + Vector3.left * 2f;

            var go = new GameObject("Раненый");
            go.transform.SetParent(transform);
            go.transform.position = spot;
            _spawned.Add(go);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.transform.SetParent(go.transform);
            body.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            body.transform.localScale = new Vector3(0.5f, 1.05f, 0.5f);

            var ally = go.AddComponent<Warrior>();
            // Гнев низкий и мораль благочестивая: раненому важно только
            // быть раненым, спорить за него никто не будет.
            ally.Initialize(new SoulData("Раненый", SinType.Wrath, MoralType.Pious, 1, 20f),
                            ShellType.Skeleton,
                            new RelationshipSystem(MemoryProcessor.Instance),
                            false, Team.Player);

            WarriorRig.Attach(go);

            // Ниже трети — только тогда контекст считает своего «в беде».
            ally.TakeDamage(ally.MaxHP * 0.8f);
        }

        private void SpawnEnemies()
        {
            var centre = _enemySpot != null ? _enemySpot.position : transform.position;

            for (int i = 0; i < _enemyCount; i++)
            {
                // Расстановка по кругу и по счётчику, без случайности:
                // повтор обязан ставить их туда же.
                float angle = i * (360f / Mathf.Max(_enemyCount, 1));
                var offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 2.2f;

                var go = new GameObject($"Чужой {i + 1}");
                go.transform.SetParent(transform);
                go.transform.position = centre + offset;
                _spawned.Add(go);

                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.transform.SetParent(go.transform);
                body.transform.localPosition = new Vector3(0f, 0.65f, 0f);
                body.transform.localScale = new Vector3(0.5f, 1.05f, 0.5f);

                var foe = go.AddComponent<Warrior>();
                foe.Initialize(new SoulData($"Чужой {i + 1}", SinType.Wrath, MoralType.Vicious, 1, 50f),
                               ShellType.Skeleton,
                               new RelationshipSystem(MemoryProcessor.Instance),
                               false, Team.Enemy);

                WarriorRig.Attach(go);

                var setup = Object.FindFirstObjectByType<AOSSceneSetup>();
                if (setup != null) setup.SetupWarrior(go);
            }
        }

        private void Clear()
        {
            var combat = CombatManager.Instance;

            foreach (var go in _spawned)
            {
                if (go == null) continue;

                // Тело из списка боя надо снять руками: список переживает
                // уничтожение объекта, и следующий опыт увидел бы добычу,
                // которой в комнате уже нет.
                if (combat != null)
                {
                    var body = go.GetComponent<HarvestableBody>();
                    if (body != null) combat.BodiesOnField.Remove(body);
                }

                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }

            _spawned.Clear();
            _subject = null;
            _mind = null;
        }

        private static void Log(string line)
            => Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(line);
    }
}
