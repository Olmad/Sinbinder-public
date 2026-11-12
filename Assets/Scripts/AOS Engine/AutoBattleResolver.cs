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
        /// One genuine AOS battle, advanced ONE resolver round at a time.
        /// The same path is used by the traditional one-shot Resolve call.
        /// A client may keep this object alive and call StepRound gradually.
        /// This object contains live Warrior state: it is NOT a serializable
        /// cross-scene checkpoint. Do not represent it as one.
        /// </summary>
        public sealed class Session
        {
            private readonly BehaviourResolver _resolver;
            private readonly List<Warrior> _squad;
            private readonly List<Warrior> _enemies;
            private readonly SquadStrategy _strategy;
            private readonly BattleRecord _record;
            private readonly AutoBattleResult _result = new AutoBattleResult();
            public int CompletedRounds { get; private set; }
            public bool IsComplete { get; private set; }
            public AutoBattleResult Result => IsComplete ? _result : null;
            public BattleRecord Record => _record;

            public Session(Warrior commander, List<Warrior> squad,
                           List<Warrior> enemies, SquadStrategy strategy,
                           BattleRecord record = null)
            {
                _squad=squad;
                _enemies=enemies;
                _strategy=strategy;
                _record=record ?? new BattleRecord();
                // A single BehaviourResolver per encounter, not per actor.
                _resolver=new BehaviourResolver();
                if (!CanAdvance()) Complete();
            }

            private bool CanAdvance()
            {
                if(_squad==null || _enemies==null ||
                   CompletedRounds>=MaxTurns) return false;
                return _squad.Any(w => w != null && !w.IsDead)
                    && _enemies.Any(w => w != null && !w.IsDead);
            }

            /// <returns>True only if one real round was performed.</returns>
            public bool StepRound()
            {
                if(IsComplete) return false;
                if(!CanAdvance()) { Complete(); return false; }

                // The command influences votes through the existing AOS
                // order module. Never substitute an action after voting.
                var previousOrders=SquadOrders.Current;
                try
                {
                    SquadOrders.Set(_strategy);
                    int round=CompletedRounds+1;
                    foreach(var warrior in _squad.Where(w=>w!=null && !w.IsDead).ToList())
                        if(_squad.Contains(warrior) && !warrior.IsDead)
                            Act(_resolver,warrior,_squad,_enemies,_record,round);

                    foreach(var enemy in _enemies.Where(w=>w!=null && !w.IsDead).ToList())
                        if(_enemies.Contains(enemy) && !enemy.IsDead)
                            Act(_resolver,enemy,_enemies,_squad,_record,round);

                    CompletedRounds++;
                }
                finally
                {
                    SquadOrders.Set(previousOrders);
                }

                if(!CanAdvance()) Complete();
                return true;
            }

            private void Complete()
            {
                if(IsComplete) return;
                IsComplete=true;
                _result.SquadSurvived=_squad==null ? 0
                    : _squad.Count(w=>w!=null && !w.IsDead);
                _result.EnemiesDefeated=_enemies==null ? 0
                    : _enemies.Count(w=>w!=null && w.IsDead);
                _result.TotalTurns=CompletedRounds;
                _record.TotalTurns=CompletedRounds;
                bool enemiesLeft=_enemies!=null && _enemies.Any(w=>w!=null && !w.IsDead);
                _record.Winner=_result.SquadSurvived==0 ? "Enemy"
                    : enemiesLeft ? "Draw" : "Squad";
            }
        }

        /// <summary>
        /// Legacy synchronous entry. Its events and result are now produced
        /// by the SAME StepRound implementation used for gradual simulation.
        /// </summary>
        public static AutoBattleResult Resolve(Warrior commander, List<Warrior> squad,
            List<Warrior> enemies, SquadStrategy strategy, BattleRecord record = null)
        {
            if(squad==null || enemies==null) return new AutoBattleResult();
            var session=new Session(commander,squad,enemies,strategy,record);
            while(session.StepRound()) { }
            return session.Result;
        }

        private static void Act(BehaviourResolver resolver, Warrior warrior,
            List<Warrior> allies, List<Warrior> foes, BattleRecord record, int round)
        {
            // Контекст собирается из списков боя, а не из сцены:
            // сцены здесь нет, и прежний CombatDecisionContext.Create
            // возвращал мир без врагов и без союзников. В таком мире
            // в бюллетене остаётся одно «стоять», и весь бой проходил
            // в неподвижности — двадцать ходов, ноль урона, «победа».
            var context = AutoBattleContext.Create(warrior, allies, foes);
            var action = resolver.Decide(warrior, context);
            Execute(warrior, action, foes, allies, record, round);
        }

        private static void Execute(Warrior warrior, ActionType action,
            List<Warrior> foes, List<Warrior> allies, BattleRecord record, int round)
        {
            var evt = new BattleEvent
            {
                ActorId = warrior.Id,
                ActorName = warrior.DisplayName,
                ActorGender = warrior.Gender,
                Action = action,
                Round = round,
            };

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

                        float before=target.HP;
                        bool wasAlive=!target.IsDead;
                        target.TakeDamage(warrior.Attack);
                        evt.TargetId=target.Id;
                        evt.TargetName=target.DisplayName;
                        evt.TargetHealthAfter=target.HP;
                        // Damageable and combat armour may absorb part or
                        // all of the attempted hit. Record the ACTUAL loss.
                        evt.DamageDealt=ExpeditionCombatFacts.ActualDamage(before,target.HP);
                        evt.Resolved=evt.DamageDealt>0f;
                        evt.TargetKilled=wasAlive && target.IsDead;
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

                        float before=ally.HP;
                        ally.Heal(5f);
                        evt.TargetId=ally.Id;
                        evt.TargetName=ally.DisplayName;
                        evt.TargetHealthAfter=ally.HP;
                        evt.HealDealt=ExpeditionCombatFacts.ActualHealing(before,ally.HP);
                        evt.Resolved=evt.HealDealt>0f;
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


                        evt.TargetId=target.Id;
                        evt.TargetName=target.DisplayName;
                        evt.TargetHealthAfter=target.HP;
                        evt.Resolved=true;
                    }
                    break;
                }

                case ActionType.Flee:
                    // With no 3D position to flee to, withdrawal removes the
                    // fighter from this encounter. They remain alive and can
                    // return home, but can no longer attack or be targeted.
                    evt.Resolved = allies.Remove(warrior);
                    break;

                case ActionType.Loot:
                case ActionType.Idle:
                    // Ход записан действием: пересказ (Crypt/Retelling)
                    // строит фразу сам, словами игрока и по роду.
                    break;

                default:
                    // Умения сюда не доходят: вылазка строит своих воинов
                    // без наборов умений (см. Gameplay/Expedition), и в
                    // бюллетене остаются шесть базовых действий. Если
                    // когда-нибудь дойдут — ход всё равно записан ниже,
                    // со своим действием, и не пропадёт молча. Молчаливая
                    // потеря хода — это ровно тот класс ошибок, из-за
                    // которого весь автобой когда-то проходил в
                    // неподвижности и назывался победой.
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
