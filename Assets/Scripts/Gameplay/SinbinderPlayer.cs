// Assets/Scripts/Gameplay/SinbinderPlayer.cs
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Греховод — тот, кем играют.
    ///
    /// До 8 сентября этот класс существовал только как объявление:
    /// ни один спавнер его не создавал, ни в одной сцене его не было,
    /// и «игрок» на деле означал камеру. Отсюда всё разом: подойти
    /// к столу значило навести взгляд, командование по локации не имело
    /// смысла (некому быть рядом), а доля 1 пролога — «вышел из палатки,
    /// под ним лагерь» — не могла быть поставлена, потому что выходить
    /// было некому (docs/13-DRIFT.md §1 и §2).
    ///
    /// Решение о теле принято давно и записано: <c>SinbinderPlayer</c>
    /// наследует <see cref="Warrior"/>, то есть у него есть место в сцене,
    /// здоровье и ноги. Здесь это решение наконец исполняется.
    ///
    /// <b>Он не голосует.</b> Тело — не повод давать ему бюллетень:
    /// весь замысел в том, что приказ игрока проходит через чужие души,
    /// а не через его собственную. <c>AOSSceneSetup.SetupWarrior</c>
    /// его пропускает, и это единственное исключение во всей сцене.
    /// </summary>
    public class SinbinderPlayer : Warrior
    {
        /// <summary>
        /// Игрок в сцене один, и диалоги обращаются к нему напрямую,
        /// не имея ссылки. Базовый Warrior своих Awake/OnDestroy не
        /// определяет, поэтому здесь ничего не перекрывается.
        /// </summary>
        public static SinbinderPlayer Instance { get; private set; }

        /// <summary>
        /// Где он стоит. Спрашивают все, кому нужно «игрок рядом»:
        /// шар, стол совета, зона побега. Пока тела не было, каждый
        /// из них спрашивал камеру — и «подойти» значило «посмотреть».
        /// </summary>
        public static Vector3 Where => Instance != null
            ? Instance.transform.position
            : Vector3.zero;

        public static bool Exists => Instance != null;

        void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Поставить Греховода в мир.
        ///
        /// Тело собирается тем же <see cref="WarriorRig"/>, что и у отряда:
        /// он ходит теми же ногами и по тому же навмешу, иначе «юнит»
        /// был бы на словах. Отличие — не в устройстве, а в облике,
        /// и оно намеренно грубое: игрок обязан находить себя на экране
        /// мгновенно, без поисков.
        /// </summary>
        public static SinbinderPlayer Spawn(Vector3 position, Transform parent = null)
        {
            if (Instance != null) return Instance;

            var go = new GameObject("Греховод");
            if (parent != null) go.transform.SetParent(parent);
            go.transform.position = position;

            var player = go.AddComponent<SinbinderPlayer>();

            // Душа у него есть, потому что Warrior без неё не живёт,
            // но она ничего не решает: голосования он не проходит.
            // Гордыня — не характеристика героя, а просто непустая шкала;
            // морали у него нет вовсе (её носит Морган, не он).
            var soul = new SoulData("Греховод", SinType.Pride, MoralType.Neutral, 1, 0f);

            // isCommander: false — и это не описка. Признак читает
            // CombatDecisionContext.GetCommander: он берёт первого
            // помеченного союзника и от него считает «верность командиру».
            // Греховод оказался бы первым, а общей памяти с отрядом у него
            // нет — верность у всех девятерых схлопнулась бы в нейтраль,
            // и доля отказов уехала бы молча, мимо всех замеров стенда.
            //
            // Он командует приказом (ActionType.ObeyCommand), а не флагом.
            // Старшего отряду выбирают на совете — вот он и помечается.
            player.Initialize(soul, ShellType.Skeleton, null, isCommander: false, team: Team.Player);

            // Ноги, урон, выделение — как у всех. Скорость чуть выше
            // отрядной: за героем, который ходит медленнее свиты,
            // неприятно вести камеру.
            WarriorRig.Attach(go, speed: 4.2f);

            // Жатва — его заклинание, а не отрядное. Связывание с него
            // снято: воин создаётся у устройства руками, а не клавишей
            // из любой точки мира (SoulBinding.cs.later).
            go.AddComponent<SoulHarvester>();

            // Сума и руки: чем он перекладывает. Висит на нём, а не
            // на суме — сума статическая, в сцене её нет, а нажатия
            // слушать кто-то должен.
            go.AddComponent<SatchelHands>();

            // Ходит сам, с клавиш. Это и есть «юнит, а не камера».
            go.AddComponent<PlayerWalk>();

            BuildBody(go.transform);

            return player;
        }

        /// <summary>
        /// Облик. Отряд — белые кубы 0,5 × 1,2; старшие 1,5 ростом.
        /// Греховод обязан читаться на их фоне с одного взгляда, поэтому
        /// отличий сразу три, а не одно: он выше всех, он тёмно-багровый
        /// там, где все белые, и под ним лежит кольцо, которое видно
        /// даже когда его самого заслонили палатки.
        ///
        /// Три признака, а не один, потому что каждый по отдельности
        /// теряется: рост — среди старших, цвет — в сумерках лагеря,
        /// кольцо — за спинами. Вместе они не теряются нигде.
        /// </summary>
        private static void BuildBody(Transform root)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Тело";
            body.transform.SetParent(root);
            body.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            body.transform.localScale = new Vector3(0.55f, 0.95f, 0.55f);
            Paint(body, new Color(0.42f, 0.06f, 0.12f));

            // Плащ: узкий столб над головой, чтобы силуэт не читался
            // как «просто высокий воин».
            var mantle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mantle.name = "Плащ";
            mantle.transform.SetParent(root);
            mantle.transform.localPosition = new Vector3(0f, 1.95f, 0f);
            mantle.transform.localScale = new Vector3(0.62f, 0.22f, 0.62f);
            Paint(mantle, new Color(0.14f, 0.04f, 0.08f));

            // Кольцо под ногами. Плоский цилиндр, чуть над землёй, чтобы
            // не тонуть в ней; коллайдер снят — по нему не ходят и в него
            // не стреляют, он только метка.
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Знак";
            ring.transform.SetParent(root);
            ring.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            ring.transform.localScale = new Vector3(1.5f, 0.01f, 1.5f);
            Paint(ring, new Color(0.75f, 0.16f, 0.22f));
            Object.Destroy(ring.GetComponent<Collider>());
        }

        private static void Paint(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;

            // sharedMaterial трогать нельзя: он общий для всех примитивов
            // сцены, и покраска Греховода перекрасила бы весь отряд.
            renderer.material.color = color;
        }
    }
}
