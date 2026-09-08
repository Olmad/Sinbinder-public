// Assets/Scripts/AOS Engine/Modules/GluttonyModule.cs
using Sinbinder.Core;

namespace Sinbinder.AOS.Modules
{
    /// <summary>
    /// Чревоугодие — это про запас, а не про еду.
    ///
    /// Оно собирает всё, до чего дотянется, и не различает нужное
    /// и ненужное. От Жадности отличается тем, что Жадность считает
    /// ценность, а Чревоугодие считает количество: там, где жадный берёт
    /// золото, ненасытный тащит и золото, и тряпьё, и мешает всем.
    /// Умеренность (отрицательная половина) берёт ровно столько,
    /// сколько унесёт.
    /// </summary>
    public class GluttonyModule : IPersonalityModule
    {
        public string ModuleID => "Gluttony";
        public float Weight => 0.9f;

        private AOSConfig _config;

        public GluttonyModule()
        {
            _config = AOSConfig.Load();
        }

        public float Evaluate(Soul soul, DecisionContext context, ActionType action)
        {
            float score = 0f;
            float gluttony = soul.Get(SinType.Gluttony);

            switch (action)
            {
                case ActionType.Loot:
                    // Считается количество тел, а не их ценность. Что тела
                    // вообще есть, обеспечивает бюллетень — см. Жадность.
                    score += gluttony * _config.GluttonyLootSinMultiplier;
                    score += context.NearbyLoot * _config.GluttonyLootPerBody
                             * (gluttony > 0f ? 1f : 0f);
                    break;

                case ActionType.Idle:
                    // Набитый ленив. Умеренный собран.
                    score += gluttony * _config.GluttonyIdleSinMultiplier;
                    break;

                case ActionType.Attack:
                    score -= gluttony * _config.GluttonyAttackSinMultiplier;
                    break;

                // ---------- собственные умения ----------
                //
                // Пороги стоят в разных местах шкалы намеренно. Замер
                // чувствительности (docs/12-BALANCE.md) показал, что шкалы
                // работают плато: одна единица не значит почти нигде.
                // Порог — единственное место, где она значит всё сразу,
                // и умения дают их по нескольку на каждую шкалу.

                case ActionType.Devour:
                    // Труп рядом обеспечивает бюллетень: CanUseSkill
                    // не предлагает Пожирание, когда есть нечего.
                    if (gluttony > 25f && context.CurrentHP < context.MaxHP * 0.7f)
                        score += 30f + gluttony * 0.3f;
                    break;

                case ActionType.Vomit:
                    if (gluttony > 50f && context.NearbyEnemies > 0)
                        score += 35f + gluttony * 0.2f;
                    break;

                case ActionType.InsatiableHunger:
                    if (gluttony > 70f)
                        score += 40f + gluttony * 0.25f;
                    break;
            }

            return score * Weight;
        }
    }
}
