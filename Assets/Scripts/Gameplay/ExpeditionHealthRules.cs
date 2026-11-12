// A pure compatibility rule for injuries created by actual expedition combat.
using System;

namespace Sinbinder.Gameplay
{
    public static class ExpeditionHealthRules
    {
        /// <summary>
        /// Zero or absent in pre-wound saves means full health, not a dead unit.
        /// A living survivor never receives a zero-hp value via restore.
        /// </summary>
        public static float RestoreFraction(float stored)
        {
            if(float.IsNaN(stored) || float.IsInfinity(stored) || stored<=0f)
                return 1f;
            return Math.Max(0.01f,Math.Min(1f,stored));
        }

        /// <summary>
        /// Actual crypt upkeep: repairing a heavily damaged body costs
        /// more than patching a small wound. Zero is a legacy save default
        /// meaning "not recorded", not a nearly dead survivor.
        /// </summary>
        public static int TreatmentCost(float recordedFraction)
        {
            float health=RestoreFraction(recordedFraction);
            if(health>=0.999f) return 0;
            return Math.Max(1,(int)Math.Ceiling((1f-health)*20f));
        }

        public static float Capture(float hp, float maximum)
        {
            if(float.IsNaN(hp) || float.IsInfinity(hp) ||
               float.IsNaN(maximum) || float.IsInfinity(maximum) ||
               maximum<=0f || hp<=0f)
                return 1f;
            return Math.Max(0.01f,Math.Min(1f,hp/maximum));
        }
    }
}
