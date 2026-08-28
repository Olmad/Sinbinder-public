// Assets/_Project/Scripts/AOS/Modules/GluttonyModule.cs
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
                    // Считается количество тел, а не их ценность.
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
            }

            return score * Weight;
        }
    }
}
