// Assets/Scripts/AOS Engine/AutoBattleResolver.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.AOS
{
    /// <summary>
    /// Бой, которого игрок не видит: отряд ушёл на вылазку, исход
    /// определяют характеры.
    ///
    /// Код был написан давно и не вызывался ни разу — из-за этого в нём
    /// накопились дефекты, которых не бывает у работающего кода.
    /// Разобраны в docs/12-BALANCE.md; здесь исправлены.
    /// </summary>
    public static class AutoBattleResolver
    {
        /// <summary>Казна отряда. Из неё платят за подкуп.</summary>
        public static int SquadGold = 0;

        /// <summary>Сколько ходов длится бой, прежде чем разойтись.</summary>
        public const int MaxTurns = 20;

        /// <summary>
        /// Провести бой. Стратегия действует так же, как в живом бою —
        /// через голосование, а не подменой уже выбранного действия.
        /// </summary>
        public static AutoBattleResult Resolve(Warrior commander, List<Warrior> squad,
            List<Warrior> enemies, SquadStrategy strategy, BattleRecord record = null)
        {
            var result = new AutoBattleResult();
            if (squad == null || enemies == null) return result;

            // Резолвер строится один раз на бой. Прежде он создавался
            // заново для каждого воина каждый ход: тринадцать модулей,
            // и каждый в конструкторе грузит конфиг. В бою восемь на
            // восемь это больше четырёх тысяч загрузок.
            var resolver = new BehaviourResolver();

            // Установка отряда — одна на игру, и в автономном бою она
            // должна работать так же, как в живом. Прежде здесь был
            // отдельный путь, который подменял выбранное действие уже
            // после голосования: тем самым решение принимал не совет,
            // а таблица, и вся архитектура обходилась стороной.
            var previousOrders = SquadOrders.Current;
            SquadOrders.Set(strategy);

            int turns = 0;
            try
            {
                while (turns < MaxTurns
                       && squad.Any(w => w != null && !w.IsDead)
                       && enemies.Any(e => e != null && !e.IsDead))
                {
                    foreach (var warrior in squad.Where(w => w != null && !w.IsDead).ToList())
                        Act(resolver, warrior, squad, enemies, record);

                    foreach (var enemy in enemies.Where(e => e != null && !e.IsDead).ToList())
                        Act(resolver, enemy, enemies, squad, record);

                    turns++;
                }
            }
            finally
            {
                SquadOrders.Set(previousOrders);
            }

            result.SquadSurvived = squad.Count(w => w != null && !w.IsDead);
            result.EnemiesDefeated = enemies.Count(e => e != null && e.IsDead);
            result.TotalTurns = turns;

            if (record != null)
            {
                record.TotalTurns = turns;

                // Ничья — это ничья. Прежде любой исход, в котором выжил
                // хоть один свой, записывался победой отряда, включая
                // случай, когда обе стороны просто не добили друг друга
                // за отведённые ходы.
                bool enemiesLeft = enemies.Any(e => e != null && !e.IsDead);
                record.Winner = result.SquadSurvived == 0 ? "Enemy"
                              : enemiesLeft ? "Draw" : "Squad";
            }

            return result;
        }

        private static void Act(BehaviourResolver resolver, Warrior warrior,
            List<Warrior> allies, List<Warrior> foes, BattleRecord record)
        {
            // Контекст собирается из списков боя, а не из сцены:
            // сцены здесь нет, и прежний CombatDecisionContext.Create
            // возвращал мир без врагов и без союзников. В таком мире
            // в бюллетене остаётся одно «стоять», и весь бой проходил
            // в неподвижности — двадцать ходов, ноль урона, «победа».
            var context = AutoBattleContext.Create(warrior, allies, foes);
            var action = resolver.Decide(warrior, context);
            Execute(warrior, action, foes, allies, record);
        }

        private static void Execute(Warrior warrior, ActionType action,
            List<Warrior> foes, List<Warrior> allies, BattleRecord record)
        {
            var evt = new BattleEvent { ActorId = warrior.Id, Action = action };

            switch (action)
            {
                case ActionType.Attack:
                {
                    // Бьют самого слабого из живых, а не первого попавшегося:
                    // так поступил бы кто угодно, и так бой заканчивается.
                    var target = foes.Where(e => e != null && !e.IsDead)
                                     .OrderBy(e => e.HP).FirstOrDefault();
                    if (target != null)
                    {
                        float dmg = warrior.Attack;
                        target.TakeDamage(dmg);
                        evt.TargetId = target.Id;
                        evt.DamageDealt = dmg;
                        evt.ResultDescription =
                            $"{warrior.DisplayName} бьёт {target.DisplayName}";
                    }
                    break;
                }

                case ActionType.SaveAlly:
                {
                    var ally = allies.Where(a => a != null && !a.IsDead && a != warrior
                                                 && a.HP < a.MaxHP * 0.5f)
                                     .OrderBy(a => a.HP).FirstOrDefault();
                    if (ally != null)
                    {
                        ally.Heal(5f);
                        evt.TargetId = ally.Id;
                        evt.HealDealt = 5f;
                        evt.ResultDescription =
                            $"{warrior.DisplayName} вытаскивает {ally.DisplayName}";
                    }
                    break;
                }

                case ActionType.BribeEnemy:
                {
                    var target = foes.Where(e => e != null && !e.IsDead)
                                     .OrderByDescending(e => e.Soul != null
                                         ? e.Soul.Get(Core.SinType.Greed) : 0f)
                                     .FirstOrDefault();

                    // Кубика здесь больше нет. Подкуп удаётся, если хватило
                    // золота и если тот, кого покупают, вообще продаётся:
                    // жадный и невернувший — да, преданный — нет. Случайный
                    // исход противоречил бы обещанию, что одинаковый вход
                    // даёт одинаковый выход.
                    int cost = CalculateBribeCost(target ?? warrior);
                    bool sells = target != null
                        && target.Soul != null
                        && target.Soul.Get(Core.SinType.Greed) > 20f
                        && target.Loyalty < 60f;

                    if (target != null && sells && SquadGold >= cost)
                    {
                        SquadGold -= cost;

                        // Перебежчик уходит на сторону того, кто платил,
                        // а не всегда на сторону игрока: подкупать могут обе.
                        target.Team = warrior.Team;
                        foes.Remove(target);
                        allies.Add(target);

                        evt.TargetId = target.Id;
                        evt.ResultDescription =
                            $"{warrior.DisplayName} покупает {target.DisplayName} за {cost}";
                    }
                    else
                    {
                        evt.ResultDescription = target == null
                            ? $"{warrior.DisplayName} некого подкупать"
                            : $"{warrior.DisplayName} предлагает золото, но {target.DisplayName} не берёт";
                    }
                    break;
                }

                case ActionType.Flee:
                    evt.ResultDescription = $"{warrior.DisplayName} отходит";
                    break;

                case ActionType.Loot:
                    evt.ResultDescription = $"{warrior.DisplayName} обирает павших";
                    break;

                case ActionType.Idle:
                    evt.ResultDescription = $"{warrior.DisplayName} медлит";
                    break;
            }

            record?.AddEvent(evt);
        }

        /// <summary>
        /// Во сколько обойдётся перекупить воина. Жадный дешевле,
        /// преданный дороже.
        /// </summary>
        public static int CalculateBribeCost(Warrior warrior)
        {
            if (warrior == null || warrior.Soul == null) return int.MaxValue;

            float greed = warrior.Soul.Get(Core.SinType.Greed);
            int level = Mathf.Max(1, warrior.Soul.Level);
            return Mathf.RoundToInt(level * 20 * (1.5f - greed / 100f)
                                    * (1f + warrior.Loyalty / 100f));
        }
    }

    public class AutoBattleResult
    {
        public int SquadSurvived;
        public int EnemiesDefeated;
        public int TotalTurns;
    }
}
