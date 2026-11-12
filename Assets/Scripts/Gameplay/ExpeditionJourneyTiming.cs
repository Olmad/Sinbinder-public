// Pure time policy for autonomous expedition travel and leader decisions.
// Time is GAME time. Pausing the game does not silently run missions.
using System;

namespace Sinbinder.Gameplay
{
    public static class ExpeditionJourneyTiming
    {
        public const float OutboundSeconds = 15f;
        public const float AdviceSeconds = 20f;
        public const float ReturnSeconds = 15f;

        /// <summary>
        /// Used independently for each squad. Older junction saves had
        /// SecondsRemaining=0: these safely proceed to an AOS decision on
        /// the next positive game tick, rather than waiting indefinitely.
        /// </summary>
        public static float Advance(float remaining, float gameDelta)
        {
            if (float.IsNaN(gameDelta) || float.IsInfinity(gameDelta) ||
                gameDelta <= 0f)
                return remaining;
            if (float.IsNaN(remaining) || float.IsInfinity(remaining))
                return 0f;
            return Math.Max(0f, remaining-gameDelta);
        }

        public static bool Finished(float remaining)
            => remaining <= 0f;
    }
}
