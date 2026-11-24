// Assets/Scripts/AOS Engine/OrderIntegrity.cs
// The same order-integrity rules run in the real AOS and the .NET CI harness.
using System.Collections.Generic;

namespace Sinbinder.AOS
{
    /// <summary>
    /// Refusal is a consequential event, not an entry to write on every
    /// one-second AOS tick. One order may be reconsidered many times, but
    /// the warrior and the commander remember that refusal only once.
    ///
    /// Distinct orders can cause distinct refusals; a short cooldown also
    /// prevents players from farming hostility by spamming identical orders.
    /// Keys are runtime warrior instance IDs, not soul IDs: a newly raised
    /// warrior must not inherit an old scene object's cooldown.
    /// </summary>
    public sealed class RefusalEventGate
    {
        private struct Stamp
        {
            public uint OrderRevision;
            public float At;
        }

        private readonly Dictionary<int, Stamp> _last = new Dictionary<int, Stamp>();

        public bool TryRecord(int warriorInstanceId, uint orderRevision, float now, float cooldown)
        {
            if (warriorInstanceId == 0 || float.IsNaN(now) || float.IsInfinity(now))
                return false;

            if (_last.TryGetValue(warriorInstanceId, out var previous))
            {
                // Same order, even long after its first refusal.
                if (previous.OrderRevision == orderRevision) return false;

                // Different order, but not enough time has elapsed to
                // count it as another meaningful relationship event.
                if (now < previous.At || now - previous.At < cooldown) return false;
            }

            _last[warriorInstanceId] = new Stamp { OrderRevision = orderRevision, At = now };
            return true;
        }

        public void Clear() => _last.Clear();
    }

    public static class OrderIntegrity
    {
        /// <summary>Unknown identity is never evidence of a personal relationship.</summary>
        public static bool SameKnownTarget(string expectedId, string actualId)
            => !string.IsNullOrEmpty(expectedId) && !string.IsNullOrEmpty(actualId)
                && string.Equals(expectedId, actualId, System.StringComparison.Ordinal);

        /// <summary>
        /// An Idle vote while a player's command is still pending is a
        /// hesitation/choice to stand, not permission for camp-life AI to
        /// start another move. Without orders, autonomous camp activity is
        /// legitimate.
        /// </summary>
        public static bool MayRoamCamp(bool decidedIdle, bool hasCommand)
            => decidedIdle && !hasCommand;

        /// <summary>
        /// A single repeated vote should not pause and announce the very
        /// same refusal every second. The next distinct order gets a fresh
        /// visible refusal even when its memory event is in cooldown.
        /// </summary>
        public static bool ShouldPresentRefusal(bool presented, uint presentedRevision, uint currentRevision)
            => !presented || presentedRevision != currentRevision;

        /// <summary>
        /// Reject an action decided before the latest order was issued or
        /// cancelled. The next AOS tick will vote again in the new context.
        /// </summary>
        public static bool MayExecuteDelayed(uint decidedForRevision, uint currentRevision)
            => decidedForRevision == currentRevision;

        /// <summary>
        /// A whispered bargain must change the effective decision ONLY when
        /// the soul was going to hesitate or act against the active command.
        /// An independent Attack under an Attack order is still the soul's
        /// own matching choice, not obedience purchased by Whisper.
        /// </summary>
        public static bool MustCompelByWhisper(
            bool hasCommand, bool persuaded, bool hesitated,
            bool chosenActionSatisfiesOrder)
            => hasCommand && persuaded &&
               (hesitated || !chosenActionSatisfiesOrder);

        /// <summary>
        /// Execute must not overwrite a naturally matching AOS action just
        /// because Whisper consent remains active. It also must not execute
        /// compelled obedience after the command revision has changed.
        /// </summary>
        public static bool MayApplyWhisperOverride(
            bool alreadyObeyAction, bool currentCommandDecision,
            bool lastDecisionWasCompelled, bool stillPersuaded)
            => !alreadyObeyAction && currentCommandDecision &&
               lastDecisionWasCompelled && stillPersuaded;

        /// <summary>
        /// A successful whisper buys obedience to ONE specific order, not
        /// to any future order during the next twenty seconds. The soul's
        /// natural vote still exists, but it must not be misreported as
        /// the physical action when the whisper is accepted.
        /// </summary>
        public static bool WhisperApplies(
            uint consentRevision, uint activeRevision, bool hasCommand,
            float until, float now)
            => hasCommand && consentRevision == activeRevision &&
               !float.IsNaN(now) && !float.IsInfinity(now) &&
               !float.IsNaN(until) && !float.IsInfinity(until) && now <= until;
    }
}
