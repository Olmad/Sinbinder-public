// Assets/Scripts/Gameplay/PrologueCampSpawner.cs
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Доля 1 пролога: пробуждение в лагере, восемь фигур вокруг костра
    /// (см. docs/09-PROLOGUE.md §3).
    ///
    /// Отряд создаётся в рантайме, а не запекается в сцену. У Warrior поля
    /// личности и отношений не помечены SerializeField, поэтому воин,
    /// собранный в редакторе, вышел бы в игру с пустыми Virtue и
    /// Relationships — то есть без души, ради которой всё и затевалось.
    ///
    /// Расстановка и характеры считаются по индексу, без Random:
    /// одинаковый вход обязан давать одинаковый выход.
    /// </summary>
    public class PrologueCampSpawner : MonoBehaviour
    {
        [SerializeField] private float _circleRadius = 3.5f;
        [SerializeField] private bool _spawnOnStart = true;

        [Tooltip("Уносить ли состав отряда в следующую сцену. Снимать только "
               + "для отладочных сцен, которым нужен полный отряд каждый раз.")]
        [SerializeField] private bool _carryOver = true;

        private Core.RelationshipSystem _relSystem;

        /// <summary>
        /// Отряд пролога. Трое отмечены командирами — это те самые три
        /// кандидата, из которых игрок выбирает на доле 3.
        ///
        /// Карган взят из docs/09-PROLOGUE.md §3: его пророчество
        /// «когда пора отходить — не отходит» должно сбыться на доле 6,
        /// и сбыться оно обязано голосованием Гордыни, а не сценарием.
        /// Поэтому спектр Гордыни у него выкручен, а Верность — нет.
        /// Остальные имена — заглушки, канон их не закрепляет.
        /// </summary>
        private static readonly CampMember[] Squad =
        {
            // Карган водит больше всех и старшим не идёт: он телохранитель.
            // Причина стоит здесь, а не в интерфейсе, потому что игрок
            // увидит её строкой в совете и не нажмёт кнопку, которая соврёт
            // (docs/09-PROLOGUE.md §6).
            new("Карган Старый Ворон", SinType.Pride,    MoralType.Neutral, 90f, 75f, 90f,
                "телохранитель, не отходит от вас"),

            // Трое опытных. Грехи взяты те, что канон закрепил за тройкой
            // кандидатов: Уныние, Жадность, Гнев. Имена канон не закрепляет.
            // Навыки разведены так, чтобы уводили по-разному, но все трое
            // проходили порог миссии доли 3 в пять человек.
            new("Вейн Тихий",          SinType.Sloth,    MoralType.Pious,   40f, 90f, 55f),
            new("Мара Сквалыга",       SinType.Greed,    MoralType.Vicious, 65f, 70f, 40f),
            new("Брат Хальд",          SinType.Wrath,    MoralType.Pious,   35f, 95f, 25f),

            // Рядовые. Повести отряд могут, но уведут троих — на миссию
            // доли 3, где нужно пятеро, их не хватит. Это и объясняет
            // игроку, зачем вообще нужен опытный.
            new("Одноглазый Хорь",     SinType.Envy,     MoralType.Vicious, 45f, 65f, 0f),
            new("Толстый Ю",           SinType.Gluttony, MoralType.Neutral, 55f, 80f, 0f),
            new("Лиска",               SinType.Lust,     MoralType.Neutral, 30f, 85f, 0f),
            // Уныние приспущено с сорока: на них Гурт не исполнял даже
            // первый безобидный приказ в лагере, и доля 2 — обучение
            // послушанием — ломалась об одного лентяя. Он остаётся вторым
            // по унынию после Вейна, но лагерный приказ ему уже по силам.
            new("Немой Гурт",          SinType.Sloth,    MoralType.Vicious, 20f, 85f, 0f),

            // Девятый. Пролог обещает, что «воинов видно девять»
            // (docs/09-PROLOGUE.md §4, сцена 1), и число это не
            // произвольное: с доли 2 уходят пятеро, и в лагере обязаны
            // остаться Карган и трое. На восьмерых сходилось трое.
            new("Косой Ждан",          SinType.Pride,    MoralType.Neutral, 30f, 80f, 0f),
        };

        private readonly struct CampMember
        {
            public readonly string Name;
            public readonly SinType Sin;
            public readonly MoralType Moral;
            public readonly float Intensity;

            /// <summary>
            /// Верность командиру на начало пролога.
            ///
            /// Задавать её обязательно. Warrior заводится с пятьюдесятью —
            /// это верность незнакомца, а отряд пролога знает Греховода
            /// не первую вылазку. Голос Верности — главный и почти
            /// единственный «за» приказ, и на пятидесяти доля 2, где приказ
            /// обязан исполниться мгновенно и трижды подряд, не срабатывала:
            /// воины отказывались ещё в лагере, до всякого отказа Каргана.
            ///
            /// Это и есть то, что демо тратит: к доле 6 верность уже
            /// подъедена долгом и усталостью, и приказ отойти встречает
            /// не тех людей, что слушались в начале.
            /// </summary>
            public readonly float Loyalty;

            /// <summary>
            /// Навык командования: только размер отряда, который он уведёт
            /// (<see cref="Leadership"/>). Ноль — рядовой. Он тоже может
            /// повести, просто возьмёт троих.
            /// </summary>
            public readonly float Leadership;

            /// <summary>Почему старшим его не поставить. Пусто — можно.</summary>
            public readonly string Unavailable;

            public CampMember(string name, SinType sin, MoralType moral,
                float intensity, float loyalty, float leadership,
                string unavailable = "")
            {
                Name = name;
                Sin = sin;
                Moral = moral;
                Intensity = intensity;
                Loyalty = loyalty;
                Leadership = leadership;
                Unavailable = unavailable;
            }
        }

        void Start()
        {
            if (_spawnOnStart) SpawnCamp();
        }

        /// <summary>
        /// Отряд уходит в следующую сцену в том составе, в каком вышел
        /// из этой. Сцена закрывается — снимаем с живых их состояние.
        /// </summary>
        void OnDestroy()
        {
            if (!_carryOver) return;
            SquadRoster.Remember(GetComponentsInChildren<Warrior>());
        }

        [ContextMenu("Собрать лагерь")]
        public void SpawnCamp()
        {
            // Отношения читаются из общей памяти. Синглтон процессора готов
            // только после всех Awake, поэтому система строится здесь.
            _relSystem = new Core.RelationshipSystem(AOS.MemoryProcessor.Instance);

            // Первая сцена пролога заводит канон, дальнейшие берут то, что
            // от отряда осталось: выбор командира на доле 3 и потери в бою
            // обязаны дожить до последней сцены (00-GDD.md §8).
            if (!SquadRoster.HasSquad) SquadRoster.Set(Canon());

            var squad = SquadRoster.Members;
            for (int i = 0; i < squad.Count; i++)
                SpawnMember(squad[i], i, squad.Count);

            Debug.Log($"[ПРОЛОГ] Лагерь собран: {squad.Count} фигур вокруг костра"
                + (string.IsNullOrEmpty(SquadRoster.CommanderName)
                    ? ", командир ещё не выбран." : $", командир — {SquadRoster.CommanderName}."));
        }

        /// <summary>Канонический отряд доли 1, из которого начинается всё.</summary>
        private static IEnumerable<SquadRoster.Member> Canon()
        {
            foreach (var m in Squad)
                yield return new SquadRoster.Member
                {
                    Name = m.Name,
                    Sin = m.Sin,
                    Moral = m.Moral,
                    Intensity = m.Intensity,
                    Loyalty = m.Loyalty,
                    UnpaidMissions = 0,

                    // Опытные помечены кандидатами, но командиром пока
                    // никто: до военного совета доли 3 отряд идёт без старшего.
                    IsCandidate = Leadership.IsExperienced(m.Leadership),
                    IsCommander = false,

                    Leadership = m.Leadership,
                    Unavailable = m.Unavailable
                };
        }

        /// <summary>
        /// Место в круге определяется индексом, а не случайностью: лагерь
        /// обязан выглядеть одинаково при каждом запуске, иначе игрок
        /// не сможет узнать своих в тумане на второй попытке.
        /// </summary>
        private Vector3 PlaceInCircle(int index, int total)
        {
            float angle = index * Mathf.PI * 2f / Mathf.Max(total, 1);
            return transform.position + new Vector3(
                Mathf.Cos(angle) * _circleRadius,
                0f,
                Mathf.Sin(angle) * _circleRadius);
        }

        private Warrior SpawnMember(SquadRoster.Member member, int index, int total)
        {
            var go = new GameObject(member.Name);
            go.transform.SetParent(transform);
            go.transform.position = PlaceInCircle(index, total);

            // Лицом к костру: в лагере смотрят на огонь, а не наружу.
            go.transform.LookAt(new Vector3(transform.position.x, go.transform.position.y, transform.position.z));

            var warrior = go.AddComponent<Warrior>();
            var soul = new SoulData(member.Name, member.Sin, member.Moral, 1, member.Intensity);
            warrior.Initialize(soul, ShellType.Skeleton, _relSystem, member.IsCommander, Team.Player);
            warrior.ChangeLoyalty(member.Loyalty - warrior.Loyalty);
            warrior.UnpaidMissions = member.UnpaidMissions;

            go.AddComponent<Damageable>();

            // Пол боя и цена приказа, список из docs/11-MISSING.md §2.3.
            // Без RefusalPresenter отказ — главный продукт демо — происходит,
            // но игрок его не видит: некому сменить значок и выдержать паузу.
            go.AddComponent<Fatigue>();
            go.AddComponent<Engagement>();
            go.AddComponent<AOS.RefusalPresenter>();

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Тело";
            body.transform.SetParent(go.transform);
            body.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            body.transform.localRotation = Quaternion.identity;

            // Опытного видно ростом ещё до совета: игрок должен успеть
            // разглядеть тех, из кого будет выбирать.
            body.transform.localScale = (member.IsCommander || member.IsCandidate)
                ? new Vector3(0.5f, 1.5f, 0.5f)
                : new Vector3(0.5f, 1.2f, 0.5f);

            return warrior;
        }
    }
}
