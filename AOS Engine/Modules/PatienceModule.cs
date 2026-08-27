// Assets/_Project/Scripts/AOS/Modules/PatienceModule.cs
namespace Sinbinder.AOS.Modules
{
    public class PatienceModule : IPersonalityModule
    {
        public string ModuleID => "Patience";
        public float Weight => 1.2f;

        public float Evaluate(Soul soul, DecisionContext context, ActionType action)
        {
            float score = 0f;
            float patience = -soul.SinIntensity; // Чем меньше Гнев, тем больше Терпение

            switch (action)
            {
                case ActionType.IronStance:
                    if (patience > 30f && context.NearbyEnemies > 0)
                        score += 40f + patience * 0.3f;
                    break;

                case ActionType.CounterAttack:
                    if (patience > 40f && context.CurrentHP < context.MaxHP * 0.7f)
                        score += 35f + patience * 0.2f;
                    break;

                case ActionType.SecondWind:
                    if (patience > 50f && context.CurrentHP < context.MaxHP * 0.5f && context.NearbyEnemies == 0)
                        score += 50f + patience * 0.4f;
                    break;

                case ActionType.Unshakable:
                    if (patience > 60f && context.DangerLevel > 0.6f)
                        score += 60f + patience * 0.3f;
                    break;

                case ActionType.Attack:
                    score -= patience * 0.2f; // Терпеливый не спешит атаковать
                    if (patience > 50f) score -= 15f;
                    break;

                case ActionType.Flee:
                    if (patience > 40f) score -= 20f; // Терпеливый не бежит без причины
                    break;
            }

            return score * Weight;
        }
    }
}