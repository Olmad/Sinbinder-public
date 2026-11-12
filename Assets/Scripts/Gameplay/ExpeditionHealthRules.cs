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
