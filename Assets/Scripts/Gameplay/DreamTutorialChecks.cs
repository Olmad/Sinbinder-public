// Assets/Scripts/Gameplay/DreamTutorialChecks.cs
// No UnityEngine dependency: production dream logic is compiled and executed by CI.
using System;
using System.Collections.Generic;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Pure rules shared by the dream and its automated checks. Keep all
    /// coordinates in world units (X/Z), not normalized direction vectors.
    /// </summary>
    public static class DreamTutorialChecks
    {
        public const float SealRadius = 1.35f;
        public const float SealHoldSeconds = 1.2f;

        public static bool OnSeal(float x, float z, float sealX, float sealZ)
        {
            float dx = x - sealX;
            float dz = z - sealZ;
            return dx * dx + dz * dz <= SealRadius * SealRadius;
        }

        /// <summary>
        /// Both living occupants must hold their plates continuously, AFTER
        /// the counterweight releases them. Leaving either plate resets time.
        /// The caller latches a successful result; this helper is not a latch.
        /// </summary>
        public static bool AdvanceSeals(
            ref float heldSeconds, bool mechanismReleased,
            bool warriorOnSeal, bool lordOnSeal, float deltaSeconds)
        {
            if (!mechanismReleased || !warriorOnSeal || !lordOnSeal)
            {
                heldSeconds = 0f;
                return false;
            }

            if (deltaSeconds > 0f && !float.IsNaN(deltaSeconds) && !float.IsInfinity(deltaSeconds))
                heldSeconds += deltaSeconds;

            return heldSeconds >= SealHoldSeconds;
        }

        /// <summary>
        /// Only accept a new decision after the expected player order.
        /// Reading LastDecisionDetail alone can accidentally credit a previous
        /// vote as proof that the new lesson was completed.
        /// </summary>
        public static bool FreshOrderedDecision(
            bool correctOrder, float issuedAt, float lessonBeganAt,
            bool newContext, bool contextHasCommand)
        {
            return correctOrder && issuedAt >= lessonBeganAt &&
                !float.IsNaN(issuedAt) && newContext && contextHasCommand;
        }
    }

    /// <summary>
    /// No reward for mere intent or approach. Called only after a concrete
    /// Bodyguard.Absorb interception; protects an incident from reward spam.
    /// Kept pure for deterministic CI execution without Unity.
    /// </summary>
    public sealed class RescueCreditGate
    {
        public const float CooldownSeconds = 15f;
        private readonly Dictionary<string, float> _last = new Dictionary<string, float>();

        public bool TryCredit(string pairKey, float now)
        {
            if (string.IsNullOrEmpty(pairKey) || float.IsNaN(now) || float.IsInfinity(now))
                return false;

            if (_last.TryGetValue(pairKey, out float last) && now - last < CooldownSeconds)
                return false;

            _last[pairKey] = now;
            return true;
        }

        public void Clear() => _last.Clear();
    }
}
