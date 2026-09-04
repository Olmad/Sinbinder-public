// Assets/Scripts/AOS Engine/Modules/LustModule.cs
using Sinbinder.Core;

namespace Sinbinder.AOS.Modules
{
    /// <summary>
    /// Похоть в Sinbinder — не про постель, а про одержимость.
    ///
    /// Это неспособность отложить желаемое. Воин видит то, чего хочет,
    /// и перестаёт слышать всё остальное, включая приказ. Обратная сторона
    /// та же одержимость: к кому привязался — за того пойдёт в огонь.
    /// Целомудрие (отрицательная половина) — не холодность, а способность
    /// удержать себя.
    /// </summary>
    public class LustModule : IPersonalityModule
    {
        public string ModuleID => "Lust";
        public float Weight => 0.9f;

        private AOSConfig _config;

        public LustModule()
        {
            _config = AOSConfig.Load();
        }

        public float Evaluate(Soul soul, DecisionContext context, ActionType action)
        {
            float score = 0f;
            float lust = soul.Get(SinType.Lust);

            switch (action)
            {
                case ActionType.Loot:
                    // Желаемое, до которого можно дотянуться прямо сейчас.
                    if (context.NearbyLoot > 0)
                        score += lust * _config.LustLootSinMultiplier;
                    break;

                case ActionType.ObeyCommand:
                    // Одержимый плохо слышит, когда рядом то, чего он хочет.
                    if (context.NearbyLoot > 0)
                        score -= lust * _config.LustObeySinMultiplier;
                    break;

                case ActionType.SaveAlly:
                    // Привязанность работает в обе стороны: к своим —
                    // сильнее, чем к приказу.
                    if (context.BrotherNearby || context.RelationshipWithCommander > 70f)
                        score += _config.LustSaveAllyBondBonus + lust * 0.2f;
                    break;

                case ActionType.Idle:
                    // Некуда потратить желание — воин застревает.
                    if (context.NearbyEnemies == 0 && context.NearbyLoot == 0)
                        score += lust * _config.LustIdleSinMultiplier;
                    break;
            }

            return score * Weight;
        }
    }
}
