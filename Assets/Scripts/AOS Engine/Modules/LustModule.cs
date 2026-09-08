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

                // ---------- собственные умения ----------
                //
                // Пороги стоят в разных местах шкалы намеренно. Замер
                // чувствительности (docs/12-BALANCE.md) показал, что шкалы
                // работают плато: одна единица не значит почти нигде.
                // Порог — единственное место, где она значит всё сразу,
                // и умения дают их по нескольку на каждую шкалу.

                case ActionType.Charm:
                    if (lust > 30f && context.NearbyEnemies > 0)
                        score += 30f + lust * 0.25f;
                    break;

                case ActionType.Seduce:
                    if (lust > 50f && context.NearbyEnemies > 0)
                        score += 35f + lust * 0.2f;
                    break;

                case ActionType.KissOfDeath:
                    // Отнимает жизнь у врага и отдаёт себе. Нужен тогда,
                    // когда своей уже мало.
                    if (lust > 60f && context.NearbyEnemies > 0
                        && context.CurrentHP < context.MaxHP * 0.5f)
                        score += 45f + lust * 0.3f;
                    break;

                case ActionType.FatalPassion:
                    // Бьёт и себя тоже. На это идут от избытка, а не от нужды.
                    if (lust > 80f && context.NearbyEnemies > 0
                        && context.CurrentHP > context.MaxHP * 0.5f)
                        score += 50f + lust * 0.25f;
                    break;
            }

            return score * Weight;
        }
    }
}
