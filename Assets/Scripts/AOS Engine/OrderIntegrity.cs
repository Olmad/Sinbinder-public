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
        /// <summary>
        /// Reject an action decided before the latest order was issued or
        /// cancelled. The next AOS tick will vote again in the new context.
        /// </summary>
        public static bool MayExecuteDelayed(uint decidedForRevision, uint currentRevision)
            => decidedForRevision == currentRevision;
    }
}
