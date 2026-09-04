// Assets/Scripts/AOS Engine/StrategyDatabase.cs
using System.Collections.Generic;

namespace Sinbinder.AOS
{
    public static class StrategyDatabase
    {
        public static List<StrategyModifier> GetModifiers(SquadStrategy strategy)
        {
            switch (strategy)
            {
                case SquadStrategy.Aggressive:
                    return new List<StrategyModifier>
                    {
                        new() { Action = ActionType.Attack, Bonus = 30 },
                        new() { Action = ActionType.Flee, Bonus = -20 },
                        new() { Action = ActionType.SaveAlly, Bonus = 10 }
                    };
                case SquadStrategy.Defensive:
                    return new List<StrategyModifier>
                    {
                        new() { Action = ActionType.SaveAlly, Bonus = 40 },
                        new() { Action = ActionType.Attack, Bonus = -10 },
                        new() { Action = ActionType.Flee, Bonus = -30 }
                    };
                case SquadStrategy.LootFocused:
                    return new List<StrategyModifier>
                    {
                        new() { Action = ActionType.Loot, Bonus = 50 },
                        new() { Action = ActionType.Attack, Bonus = -10 },
                        new() { Action = ActionType.SaveAlly, Bonus = -20 }
                    };
                case SquadStrategy.Cautious:
                    return new List<StrategyModifier>
                    {
                        new() { Action = ActionType.Flee, Bonus = 40 },
                        new() { Action = ActionType.Attack, Bonus = -30 },
                        new() { Action = ActionType.SaveAlly, Bonus = 10 }
                    };
                case SquadStrategy.Supportive:
                    return new List<StrategyModifier>
                    {
                        new() { Action = ActionType.SaveAlly, Bonus = 30 },
                        new() { Action = ActionType.HealAlly, Bonus = 20 },
                        new() { Action = ActionType.Attack, Bonus = -10 },
                        new() { Action = ActionType.Loot, Bonus = -20 }
                    };
                case SquadStrategy.Envious:
                    return new List<StrategyModifier>
                    {
                        new() { Action = ActionType.Attack, Bonus = 20 },
                        new() { Action = ActionType.Loot, Bonus = 30 },
                        new() { Action = ActionType.SaveAlly, Bonus = -20 }
                    };
                case SquadStrategy.Attrition:
                    return new List<StrategyModifier>
                    {
                        new() { Action = ActionType.IronStance, Bonus = 40 },
                        new() { Action = ActionType.CounterAttack, Bonus = 30 },
                        new() { Action = ActionType.Attack, Bonus = -20 }
                    };
                case SquadStrategy.Focused:
                    return new List<StrategyModifier>
                    {
                        new() { Action = ActionType.ObeyCommand, Bonus = 50 },
                        new() { Action = ActionType.Loot, Bonus = -30 },
                        new() { Action = ActionType.Flee, Bonus = -10 }
                    };
                case SquadStrategy.Conservative:
                    return new List<StrategyModifier>
                    {
                        new() { Action = ActionType.SaveAlly, Bonus = 25 },
                        new() { Action = ActionType.HealAlly, Bonus = 25 },
                        new() { Action = ActionType.Attack, Bonus = -20 },
                        new() { Action = ActionType.Loot, Bonus = -10 }
                    };
                case SquadStrategy.Relentless:
                    return new List<StrategyModifier>
                    {
                        new() { Action = ActionType.Attack, Bonus = 20 },
                        new() { Action = ActionType.WorkSurge, Bonus = 30 },
                        new() { Action = ActionType.Idle, Bonus = -40 }
                    };
                default:
                    return new List<StrategyModifier>();
            }
        }
    }
}