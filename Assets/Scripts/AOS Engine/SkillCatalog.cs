using Sinbinder.Core;

namespace Sinbinder.AOS
{
    /// <summary>
    /// Какие умения чьи. Одна таблица на весь проект.
    ///
    /// Раньше список действий лежал внутри каждого набора умений
    /// (<c>WrathSkills._actions</c> и пять таких же). Пока их читал только
    /// сам набор, это было незаметно; как только тем же списком захотел
    /// пользоваться кто-то ещё — проводка, стенд, — появилась бы вторая
    /// правда. А стенд, который сам решает, что воин умеет, меряет себя,
    /// а не игру. Это уже стоило проекту всего документа по балансу.
    ///
    /// Поэтому наборы умений теперь спрашивают отсюда, а не хранят у себя.
    ///
    /// Полюса. Добродетель — отрицательная половина шкалы греха, и умения
    /// подчиняются тому же правилу: Терпение это Гнев со знаком минус,
    /// Усердие — Уныние со знаком минус. Отсюда ровно шесть наборов
    /// на четыре шкалы, а не шесть отдельных сущностей.
    ///
    /// У Жадности, Гордыни и Зависти умений не написано. Это честная
    /// пустота, а не ошибка: <see cref="For"/> вернёт пустой список,
    /// и в бюллетене у такого воина будет только шесть базовых действий.
    /// </summary>
    public static class SkillCatalog
    {
        private static readonly ActionType[] None = new ActionType[0];

        private static readonly ActionType[] Wrath =
            { ActionType.Berserk, ActionType.PowerStrike };

        private static readonly ActionType[] Patience =
            { ActionType.IronStance, ActionType.CounterAttack,
              ActionType.SecondWind, ActionType.Unshakable };

        private static readonly ActionType[] Sloth =
            { ActionType.Yawn, ActionType.LazyHeal,
              ActionType.AuraOfApathy, ActionType.EternalSleep };

        private static readonly ActionType[] Diligence =
            { ActionType.WorkSurge, ActionType.WorkInspiration, ActionType.Tireless };

        private static readonly ActionType[] Lust =
            { ActionType.Charm, ActionType.KissOfDeath,
              ActionType.Seduce, ActionType.FatalPassion };

        private static readonly ActionType[] Gluttony =
            { ActionType.Devour, ActionType.Vomit, ActionType.InsatiableHunger };

        /// <summary>
        /// Умения одной шкалы. Знак решает полюс: положительный —
        /// грех, отрицательный — добродетель того же спектра.
        /// </summary>
        public static System.Collections.Generic.IReadOnlyList<ActionType> For(
            SinType spectrum, float value)
        {
            switch (spectrum)
            {
                case SinType.Wrath:    return value >= 0f ? Wrath : Patience;
                case SinType.Sloth:    return value >= 0f ? Sloth : Diligence;
                case SinType.Lust:     return value >= 0f ? Lust : None;
                case SinType.Gluttony: return value >= 0f ? Gluttony : None;
                default:               return None;
            }
        }

        /// <summary>
        /// Умения, которые воин носит с собой: те, что стоят за его
        /// самой громкой шкалой.
        ///
        /// Один набор, а не все сразу. Воин с семью наборами вынес бы
        /// в бюллетень два десятка действий и утопил бы в них шесть
        /// базовых — включая приказ. Отказ, который не с чем сравнить,
        /// перестаёт что-либо значить.
        /// </summary>
        public static System.Collections.Generic.IReadOnlyList<ActionType> Dominant(Soul soul)
        {
            if (soul == null) return None;

            SinType loudest = SinType.Greed;
            float best = 0f;

            foreach (SinType s in System.Enum.GetValues(typeof(SinType)))
            {
                float v = soul.Get(s);
                float loud = v < 0f ? -v : v;
                if (loud > best) { best = loud; loudest = s; }
            }

            return For(loudest, soul.Get(loudest));
        }
    }
}
