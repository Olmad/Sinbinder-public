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

        /// <summary>
        /// На сколько Греховод отходит от полога палатки в сторону лагеря.
        /// Не ноль: стоя в самой палатке, он был бы ею закрыт, и первое,
        /// что игрок увидел бы о себе, — что себя не видно.
        /// </summary>
        [SerializeField] private float _playerStepFromTent = 1.6f;

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
            // Долг в три вылазки — не случайность, а завязка. Строка, которой
            // игра продаётся дословно («ему не платили третью вылазку подряд»),
            // рождается только при долге больше двух, а демо заводило всех
            // с нулём: флагманская реплика была недостижима структурно.
            // Отряду задолжали до пробуждения — тем же приёмом, что и пять
            // пустых палаток: лагерь жил до того, как игрок открыл глаза.
            new("Марга Копатель",      SinType.Greed,    MoralType.Vicious, 65f, 70f, 40f,
                unpaid: 3),
            new("Брат Хальд",          SinType.Wrath,    MoralType.Pious,   35f, 95f, 25f),

            // Рядовые. Повести отряд могут, но уведут троих — на миссию
            // доли 3, где нужно пятеро, их не хватит. Это и объясняет
            // игроку, зачем вообще нужен опытный.
            // Двое братьев по оружию. Хорь завидует всем, Гурт не говорит
            // ни с кем — и оба держатся друг друга. Пара выбрана из рядовых
            // нарочно: кандидаты в старшие уходят с отрядом, а братство
            // видно только пока оба на виду.
            new("Одноглазый Хорь",     SinType.Envy,     MoralType.Vicious, 45f, 65f, 0f,
                brother: true),
            new("Толстый Ю",           SinType.Gluttony, MoralType.Neutral, 55f, 80f, 0f),
            new("Лиска",               SinType.Lust,     MoralType.Neutral, 30f, 85f, 0f),
            // Уныние приспущено с сорока: на них Гурт не исполнял даже
            // первый безобидный приказ в лагере, и доля 2 — обучение
            // послушанием — ломалась об одного лентяя. Он остаётся вторым
            // по унынию после Вейна, но лагерный приказ ему уже по силам.
            new("Немой Гурт",          SinType.Sloth,    MoralType.Vicious, 20f, 85f, 0f,
                brother: true),

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

            /// <summary>
            /// Сколько вылазок ему не заплатили до начала пролога.
            ///
            /// Ноль у всех, кроме одного, и это не мелочь: Жадность
            /// начинает роптать после второй невыплаты, а строка,
            /// которой игра продаётся дословно, рождается после третьей.
            /// С нулём у всех она была недостижима за всё демо.
            /// </summary>
            public readonly int Unpaid;

            /// <summary>
            /// Пол.
            ///
            /// <b>В демо женщин нет.</b> Женские лица появятся только
            /// в основной игре и только уникальными: их не выдаёт ни
            /// жатва, ни полка, ни вылазка. Правило держится не памятью,
            /// а проверкой — см. <c>unique_women</c> в Tools/check.py,
            /// и отряд пролога в её списке разрешённых мест не стоит.
            ///
            /// Поле здесь всё равно нужно: без него текст о воине
            /// не согласуется, а согласовывать его придётся в тот же
            /// день, когда появится первая женщина.
            ///
            /// Марга — мужчина, несмотря на имя. Я прочитал имя вместо
            /// канона и ошибся; пометка стоит здесь, чтобы следующий
            /// не повторил.
            /// </summary>
            public readonly Gender Gender;

            /// <summary>
            /// Носит ли перк «Брат по оружию».
            ///
            /// Братство считает CombatDecisionContext: брат рядом —
            /// это когда перк есть и у самого воина, и у кого-то
            /// из своих поблизости. Перк лежал в базе и читался
            /// движком, но не было его ни у одной души: ветка Похоти
            /// «он не бросит своего» и прибавка к удару за брата
            /// не срабатывали ни разу за всё время.
            ///
            /// Носителей ровно двое, и это не мелочь: с одним
            /// братство не бывает, с тремя перестаёт быть парой.
            /// </summary>
            public readonly bool Brother;

            public CampMember(string name, SinType sin, MoralType moral,
                float intensity, float loyalty, float leadership,
                string unavailable = "", int unpaid = 0,
                Gender gender = Gender.Male, bool brother = false)
            {
                Name = name;
                Sin = sin;
                Moral = moral;
                Gender = gender;
                Intensity = intensity;
                Loyalty = loyalty;
                Leadership = leadership;
                Unavailable = unavailable;
                Unpaid = unpaid;
                Brother = brother;
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

            var squad = GetComponentsInChildren<Warrior>();

            // Если на сцене был край карты — дальше идут только те, кого
            // игрок успел до него довести. Побег это отбор, а не переход
            // (docs/09-PROLOGUE.md §4, сцена 5).
            //
            // Спрашиваем итог отбора, а не саму зону: порядок уничтожения
            // объектов не гарантирован, и живой ссылки здесь может уже
            // не быть.
            if (EscapeZone.SelectionMade)
            {
                var escaped = new List<Warrior>();
                foreach (var w in squad)
                {
                    if (w == null || w.IsDead) continue;
                    foreach (var name in EscapeZone.EscapedNames)
                        if (w.DisplayName == name) { escaped.Add(w); break; }
                }

                SquadRoster.Remember(escaped);
                return;
            }

            SquadRoster.Remember(squad);
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

            SpawnPlayer();

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
                    Gender = m.Gender,
                    Intensity = m.Intensity,
                    Loyalty = m.Loyalty,
                    UnpaidMissions = m.Unpaid,

                    // Опытные помечены кандидатами, но командиром пока
                    // никто: до военного совета доли 3 отряд идёт без старшего.
                    IsCandidate = Leadership.IsExperienced(m.Leadership),
                    IsCommander = false,

                    Leadership = m.Leadership,
                    Unavailable = m.Unavailable,
                    Brother = m.Brother
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

        /// <summary>
        /// Поставить Греховода у его палатки на холме.
        ///
        /// Место не выдумано: доля 1 пролога — «вышел из палатки на холме,
        /// под ним лагерь» (docs/13-DRIFT.md §2). Палатка в сцене уже стоит,
        /// её ставит сборщик; ищем её по имени, а не по координатам, чтобы
        /// холм можно было двигать, не трогая этот файл.
        ///
        /// Не нашли — ставим у костра и говорим об этом вслух: Греховод
        /// без места хуже, чем Греховод не на своём месте.
        /// </summary>
        private void SpawnPlayer()
        {
            var tent = GameObject.Find("Палатка Греховода");
            Vector3 where;

            if (tent != null)
            {
                // Перед входом, лицом к лагерю: он только что вышел.
                Vector3 toCamp = transform.position - tent.transform.position;
                toCamp.y = 0f;
                where = tent.transform.position
                      + toCamp.normalized * _playerStepFromTent;
            }
            else
            {
                Debug.LogWarning("[ПРОЛОГ] Палатки Греховода в сцене нет: "
                               + "ставлю его у костра. Доля 1 задумана "
                               + "с холма — пересоберите сцены.");
                where = transform.position + new Vector3(0f, 0f, -_circleRadius * 1.6f);
            }

            var player = SinbinderPlayer.Spawn(where, transform.parent);
            if (player == null) return;

            player.transform.LookAt(new Vector3(
                transform.position.x, player.transform.position.y, transform.position.z));
        }

        /// <summary>
        /// Память души. Пока нужна ровно для одного — перка «Брат
        /// по оружию», по которому движок и узнаёт братьев
        /// (<c>CombatDecisionContext</c>).
        ///
        /// Пустая память и отсутствие памяти — разные вещи: проверка
        /// в движке смотрит <c>Memory?.NarrativePerks</c>, и лишний
        /// пустой мешок у восьмерых ничего не стоит, зато у двоих
        /// в нём лежит то, из-за чего они держатся вместе.
        /// </summary>
        private static MemorySeed Memory(SquadRoster.Member member)
        {
            if (!member.Brother) return null;

            var seed = new MemorySeed
            {
                Object = "брат по оружию",
                Emotion = "Привязанность",
                Story = "Они пришли в отряд вдвоём и с тех пор держатся рядом.",
            };

            seed.NarrativePerks.Add(new NarrativePerk
            {
                PerkName = "Брат по оружию",
                IsFound = true,
            });

            return seed;
        }
        private Warrior SpawnMember(SquadRoster.Member member, int index, int total)
        {
            var go = new GameObject(member.Name);
            go.transform.SetParent(transform);
            go.transform.position = PlaceInCircle(index, total);

            // Лицом к костру: в лагере смотрят на огонь, а не наружу.
            go.transform.LookAt(new Vector3(transform.position.x, go.transform.position.y, transform.position.z));

            var warrior = go.AddComponent<Warrior>();
            var soul = new SoulData(member.Name, member.Sin, member.Moral, 1,
                                    member.Intensity, Memory(member), member.Gender);
            warrior.Initialize(soul, ShellType.Skeleton, _relSystem, member.IsCommander, Team.Player);
            warrior.ChangeLoyalty(member.Loyalty - warrior.Loyalty);
            warrior.UnpaidMissions = member.UnpaidMissions;

            // Ноги, урон, усталость, значок над головой — всё общее сразу.
            // Собиралось это только в UnitFactory, которым пролог
            // не пользуется: воины выходили без агента и не могли сделать
            // ни шага, а без значка не работала первая ступень лестницы.
            WarriorRig.Attach(go);

            // Жатва душ: сцена 4 учит ей, а научить некому, если её
            // некому и делать. Раньше жнец висел только в тестовой сцене.
            go.AddComponent<SoulHarvester>();

            // И вторая половина того же урока: собранную душу надо во что-то
            // вложить, иначе жатва — просто исчезновение трупа.

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

            // Братьев видно без наведения: у обоих над головой одна
            // и та же бирюзовая метка. Подпись при взгляде — вторая
            // ступень, а здесь нужна первая: игрок должен заметить пару
            // до того, как задумается, кто есть кто.
            if (member.Brother) BrotherMark(go.transform);

            return warrior;
        }

        /// <summary>Метка брата по оружию. Одна на двоих, потому и узнаётся.</summary>
        private static void BrotherMark(Transform root)
        {
            var mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mark.name = "Брат по оружию";
            mark.transform.SetParent(root);
            mark.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            mark.transform.localScale = new Vector3(0.34f, 0.1f, 0.34f);
            mark.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);

            var renderer = mark.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = new Color(0.25f, 0.80f, 0.74f);

            // Коллайдер снят: метка не должна ловить ни луч выделения,
            // ни удар. Она знак, а не часть тела.
            Object.Destroy(mark.GetComponent<Collider>());
        }
    }
}
