// Production arithmetic for factual AOS expedition combat records.
// Tests use this exact code, not an independently duplicated formula.
using System;

namespace Sinbinder.Gameplay
{
    public static class ExpeditionCombatFacts
    {
        /// <summary>
        /// Damage is a change to the target's HP AFTER defence and
        /// absorption, never the attacker's nominal Attack statistic.
        /// </summary>
        public static float ActualDamage(float beforeHp, float afterHp)
        {
            if (float.IsNaN(beforeHp) || float.IsNaN(afterHp) ||
                float.IsInfinity(beforeHp) || float.IsInfinity(afterHp))
                return 0f;
            return Math.Max(0f, beforeHp-afterHp);
        }

        /// <summary>
        /// Healing is actual restored HP, not an attempted 5 HP when
        /// only 1 HP is missing or no healing was possible.
        /// </summary>
        public static float ActualHealing(float beforeHp, float afterHp)
        {
            if (float.IsNaN(beforeHp) || float.IsNaN(afterHp) ||
                float.IsInfinity(beforeHp) || float.IsInfinity(afterHp))
                return 0f;
            return Math.Max(0f, afterHp-beforeHp);
        }
    }
}
