// Assets/Scripts/Gameplay/Expedition.cs
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.AOS;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Чем кончилась вылазка отряда, ушедшего на доле 2.
    ///
    /// Раньше это была таблица (<c>Homecoming.Returned</c>): сценарист
    /// заранее решил, что Жадность возвращает четверых, а Уныние одного.
    /// Правило пролога §2 говорит обратное: ни одна постановочная сцена
    /// не имеет права показать то, чего движок не мог бы решить сам.
    ///
    /// Стенд проверил, умеет ли движок то же самое (docs/12-BALANCE.md,
    /// «Вылазка»). Умеет — и на равном бою различает командиров лучше
    /// таблицы: разброс 2.07 из пяти. Но порядок у них разный, и совпадало
    /// только Уныние: таблица делала Жадность лучшим исходом, движок —
    /// одним из худших, потому что собиратель отвлекается на добычу.
    ///
    /// Выбран второй из трёх выходов, записанных в разборе: считать
    /// эпилог боем и переписать объяснения под то, что движок правда
    /// делает. Здесь этот бой и проводится.
    ///
    /// <b>Повторяемость.</b> Ни одного случайного числа: состав известен,
    /// противник строится по счёту, <see cref="AutoBattleResolver"/>
    /// кубиков не бросает. Один и тот же выбор игрока даёт один и тот же
    /// исход — иначе объяснение «он свернул за блеском» было бы враньём
    /// через раз.
    /// </summary>
    public static class Expedition
    {
        /// <summary>
        /// Чем кончилось: кто вернулся и сколько сняли с павших чужих.
        ///
        /// Добыча считается <see cref="BodyWorth"/> — тем же, чем её
        /// считает бой на сцене. Пока вылазка платила «врагов × монету»,
        /// в игре было две правды о мертвеце: пятнадцать монет за Ловчего
        /// на поле и плоские шесть за «Чужого» на карте.
        /// </summary>
        public readonly struct Outcome
        {
            public readonly List<string> Survivors;
            public readonly int Taken;
            public readonly int FoesFallen;

            public Outcome(List<string> survivors, int taken, int foesFallen)
            {
                Survivors = survivors;
                Taken = taken;
                FoesFallen = foesFallen;
            }
        }

        /// <summary>
        /// Провести вылазку и вернуть имена выживших в порядке отряда.
        /// Пустой список — не вернулся никто.
        /// </summary>
        public static List<string> Resolve(List<SquadRoster.Member> away)
            => Resolve(away, away != null ? away.Count : 0);

        /// <summary>
        /// То же, но противник задан отдельно.
        ///
        /// В прологе он был вровень по числу ушедших: там точка одна,
        /// и выбирать не из чего. На карте склепа точки разной тяжести,
        /// и «их там больше» обязано что-то значить — иначе выбор точки
        /// это выбор названия.
        /// </summary>
        public static List<string> Resolve(List<SquadRoster.Member> away, int foeCount)
            => Fight(away, foeCount).Survivors;

        /// <summary>То же, но с добычей: её считает вызывающий.</summary>
        public static Outcome Fight(List<SquadRoster.Member> away, int foeCount)
        {
            var survivors = new List<string>();
            int taken = 0, fallen = 0;
            if (away == null || away.Count == 0) return new Outcome(survivors, 0, 0);

            var holder = new GameObject("Вылазка");
            holder.SetActive(false);   // ничьих Awake и Update: это счёт, не сцена

            try
            {
                var relations = new RelationshipSystem(AOS.MemoryProcessor.Instance);
                var squad = new List<Warrior>();
                var foes = new List<Warrior>();

                Warrior commander = null;

                foreach (var m in away)
                {
                    var w = Make(holder, m.Name, m.Sin, m.Moral, m.Intensity,
                                 m.Loyalty, m.IsCommander, Team.Player, relations);
                    w.UnpaidMissions = m.UnpaidMissions;

                    squad.Add(w);
                    if (m.IsCommander) commander = w;
                }

                // Противник вровень: «там довольно опасно, нужно пятеро»
                // (пролог §4, сцена 2). Это тот самый расклад, на котором
                // стенд мерил, и единственный, где командиры вообще
                // различимы: на слабом враге выживают почти все.
                //
                // Грехи чужих разведены по кругу, а не одинаковы: отряд
                // из семи одинаковых душ вёл бы себя как один воин.
                for (int i = 0; i < Mathf.Max(foeCount, 1); i++)
                {
                    var sin = (SinType)(i % 7);
                    foes.Add(Make(holder, "Чужой", sin, MoralType.Vicious,
                                  40f + i * 5f, 50f, false, Team.Enemy, relations));
                }

                var strategy = commander != null && commander.Soul != null
                    ? SquadOrders.FromSin(commander.Soul.Sin)
                    : SquadStrategy.Balanced;

                AutoBattleResolver.Resolve(commander, squad, foes, strategy);

                for (int i = 0; i < squad.Count; i++)
                    if (!squad[i].IsDead) survivors.Add(away[i].Name);

                // С павших снимают то же, что снимали бы на поле.
                foreach (var foe in foes)
                {
                    if (!foe.IsDead) continue;
                    fallen++;
                    taken += BodyWorth.Gold(foe.Shell, foe.Soul);
                }
            }
            finally
            {
                // Самопроверку движка запускают из меню редактора, а там
                // Destroy запрещён и оставил бы за собой мусор в сцене.
                if (Application.isPlaying) Object.Destroy(holder);
                else Object.DestroyImmediate(holder);
            }

            return new Outcome(survivors, taken, fallen);
        }

        /// <summary>
        /// Собрать временного воина по записи отряда.
        ///
        /// Нужно тем, кому воин требуется на один вопрос, а не на бой:
        /// развилка спрашивает командира, а спросить может только
        /// движок решений, и ему нужен настоящий воин. Одна правда
        /// о том, как запись отряда становится воином, — здесь.
        /// Держатель обязан быть выключен: это счёт, а не сцена.
        /// </summary>
        public static Warrior Summon(GameObject holder, SquadRoster.Member m,
                                     RelationshipSystem relations)
        {
            var w = Make(holder, m.Name, m.Sin, m.Moral, m.Intensity,
                         m.Loyalty, m.IsCommander, Team.Player, relations);
            w.UnpaidMissions = m.UnpaidMissions;
            return w;
        }

        private static Warrior Make(GameObject holder, string name, SinType sin,
            MoralType moral, float intensity, float loyalty, bool isCommander,
            Team team, RelationshipSystem relations)
        {
            var go = new GameObject(name);
            go.transform.SetParent(holder.transform);

            var w = go.AddComponent<Warrior>();
            w.Initialize(new SoulData(name, sin, moral, 1, intensity),
                         ShellType.Skeleton, relations, isCommander, team);
            // Верность задаётся сдвигом от полусотни: своего сеттера
            // у неё нет, и заводить его ради счётной вылазки незачем.
            w.ChangeLoyalty(loyalty - w.Loyalty);

            return w;
        }
    }
}
