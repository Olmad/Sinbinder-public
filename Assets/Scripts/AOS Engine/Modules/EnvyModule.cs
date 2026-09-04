// Assets/Scripts/AOS Engine/Modules/EnvyModule.cs
using Sinbinder.Core;

namespace Sinbinder.AOS.Modules
{
    /// <summary>
    /// Зависть смотрит не на мир, а на своих.
    ///
    /// Она тянет к добыче не потому, что нужна добыча, а потому, что
    /// достанется другому. Она не спасает того, кому и так хорошо.
    /// И она хуже всех переносит командира, с которым отношения скверные:
    /// чужая власть раздражает завистника сильнее, чем чужое золото.
    /// Доброжелательность (отрицательная половина) делает обратное.
    /// </summary>
    public class EnvyModule : IPersonalityModule, IMissionModule
    {
        public string ModuleID => "Envy";
        public float Weight => 1.0f;

        private AOSConfig _config;

        public EnvyModule()
        {
            _config = AOSConfig.Load();
        }

        public float Evaluate(Soul soul, DecisionContext context, ActionType action)
        {
            float score = 0f;
            float envy = soul.Get(SinType.Envy);

            switch (action)
            {
                case ActionType.Loot:
                    // Важно не золото, а то, что оно достанется кому-то ещё.
                    if (context.NearbyAllies > 0)
                        score += envy * _config.EnvyLootSinMultiplier;
                    break;

                case ActionType.SaveAlly:
                    score -= envy * _config.EnvySaveAllySinMultiplier;
                    break;

                case ActionType.Attack:
                    // Чем хуже отношения с командиром, тем охотнее завистник
                    // доказывает, что он-то лучше.
                    float distance = 50f - context.RelationshipWithCommander;
                    if (envy > 20f && distance > 0f)
                        score += distance * _config.EnvyAttackCommanderRelationMultiplier;
                    break;

                case ActionType.ObeyCommand:
                    if (envy > 40f && context.RelationshipWithCommander < 40f)
                        score += _config.EnvyObeyLowRelationPenalty;
                    break;

                case ActionType.ShareGold:
                    // Доброжелательность делится, зависть — никогда.
                    score -= envy * 0.4f;
                    break;
            }

            return score * Weight;
        }

        /// <summary>
        /// Мирная миссия. По таблице завистливый обращает деревню
        /// в рабство — кроме благочестивого, который помогает.
        /// Это расхождение создаёт Мораль, а не Зависть.
        /// </summary>
        public float EvaluateMission(Soul soul, MissionContext context, MissionAction action)
        {
            float sin = soul.Get(SinType.Envy) * _config.MissionSinScale;

            switch (action)
            {
                case MissionAction.EnslaveVillage: return sin;
                case MissionAction.TaxVillage:     return sin * 0.3f;   // дань — слабое утешение
                case MissionAction.HelpVillage:    return -sin * 0.4f;
                default:                           return 0f;
            }
        }

    }
}
