// Assets/_Project/Scripts/AOS/AutoBattleResolver.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.AOS
{
    public static class AutoBattleResolver
    {
        public static int SquadGold = 0;

        /// <summary>
        /// Стратегия передаётся снаружи, а не спрашивается у командира:
        /// отдельный член Warrior обслуживал бы только этот резолвер,
        /// у которого пока нет ни одного вызывающего.
        /// </summary>
        public static AutoBattleResult Resolve(Warrior commander, List<Warrior> squad, List<Warrior> enemies,
            SquadStrategy strategy, BattleRecord record = null)
        {
            var modifiers = StrategyDatabase.GetModifiers(strategy);
            int turns = 0;
            int maxTurns = 20;
            var result = new AutoBattleResult();

            while (turns < maxTurns && squad.Any(w => !w.IsDead) && enemies.Any(e => !e.IsDead))
            {
                foreach (var warrior in squad.Where(w => !w.IsDead).ToList())
                {
                    var context = CombatDecisionContext.Create(warrior);
                    var resolver = new BehaviourResolver();
                    var action = resolver.Decide(warrior, context);

                    foreach (var mod in modifiers)
                        if (mod.Action == action)
                            action = AdjustActionByRelationship(warrior, commander, action, mod.Bonus);

                    ExecuteAction(warrior, action, enemies, squad, record);
                }

                foreach (var enemy in enemies.Where(e => !e.IsDead).ToList())
                {
                    var context = CombatDecisionContext.Create(enemy);
                    var resolver = new BehaviourResolver();
                    var action = resolver.Decide(enemy, context);
                    ExecuteAction(enemy, action, squad, enemies, record);
                }

                turns++;
            }

            result.SquadSurvived = squad.Count(w => !w.IsDead);
            result.EnemiesDefeated = enemies.Count(e => e.IsDead);
            result.TotalTurns = turns;

            if (record != null)
            {
                record.TotalTurns = turns;
                record.Winner = result.SquadSurvived > 0 ? "Squad" : "Enemy";
            }

            return result;
        }

        private static ActionType AdjustActionByRelationship(Warrior warrior, Warrior commander, ActionType action, float bonus)
        {
            float rel = warrior.Relationships.GetRelationship(warrior.Id, commander.Id);
            if (rel > 50f && action == ActionType.Attack && warrior.HP < warrior.MaxHP * 0.3f)
                return ActionType.Flee;
            if (rel < -50f && action == ActionType.Flee)
                return ActionType.Attack;
            return action;
        }

        private static void ExecuteAction(Warrior warrior, ActionType action, List<Warrior> enemies, List<Warrior> allies, BattleRecord record)
        {
            var evt = new BattleEvent { ActorId = warrior.Id, Action = action };

            switch (action)
            {
                case ActionType.Attack:
                    var target = enemies.FirstOrDefault(e => !e.IsDead);
                    if (target != null)
                    {
                        float dmg = warrior.Attack;
                        target.TakeDamage(dmg);
                        evt.TargetId = target.Id;
                        evt.DamageDealt = dmg;
                        evt.ResultDescription = $"{warrior.DisplayName} атакует {target.DisplayName} на {dmg} урона";
                    }
                    break;

                case ActionType.SaveAlly:
                    var ally = allies.FirstOrDefault(a => !a.IsDead && a.HP < a.MaxHP * 0.5f);
                    if (ally != null)
                    {
                        ally.Heal(5);
                        evt.TargetId = ally.Id;
                        evt.HealDealt = 5;
                        evt.ResultDescription = $"{warrior.DisplayName} лечит {ally.DisplayName} на 5 HP";
                    }
                    break;

                case ActionType.BribeEnemy:
                    int cost = CalculateBribeCost(warrior);
                    if (SquadGold >= cost && Random.value < 0.6f)
                    {
                        SquadGold -= cost;
                        var bribedTarget = enemies.FirstOrDefault(e => !e.IsDead);
                        if (bribedTarget != null)
                        {
                            bribedTarget.Team = Team.Player;
                            enemies.Remove(bribedTarget);
                            allies.Add(bribedTarget);
                            evt.ResultDescription = $"{warrior.DisplayName} подкупает {bribedTarget.DisplayName} за {cost} золота!";
                        }
                    }
                    else
                    {
                        evt.ResultDescription = $"{warrior.DisplayName} пытается подкупить врага, но безуспешно.";
                    }
                    break;

                case ActionType.Flee:
                    evt.ResultDescription = $"{warrior.DisplayName} отступает";
                    break;

                case ActionType.Loot:
                    evt.ResultDescription = $"{warrior.DisplayName} собирает добычу";
                    break;
            }

            record?.AddEvent(evt);
        }

        public static int CalculateBribeCost(Warrior warrior)
        {
            float sin = warrior.Soul.SinIntensity;
            float loyalty = warrior.Loyalty;
            int level = warrior.Soul.Level;
            return Mathf.RoundToInt(level * 20 * (1.5f - sin / 100f) * (1f + loyalty / 100f));
        }
    }

    public class AutoBattleResult
    {
        public int SquadSurvived;
        public int EnemiesDefeated;
        public int TotalTurns;
    }
}