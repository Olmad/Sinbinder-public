// Pure, Unity-independent retention rule for a bag left on another map.
// At most 60 seconds away; returning early protects the bag for as long
// as the player remains on that map. Every new departure gets a new window.
using System;

namespace Sinbinder.Gameplay
{
    public sealed class FallenBagRetention
    {
        public const float AwayLifetime = 60f;
        private float _leftAt;
        public bool OnHomeMap { get; private set; } = true;
        public bool Expired { get; private set; }

        public float Remaining(float now)
        {
            if (Expired) return 0f;
            if (OnHomeMap) return AwayLifetime;
            return Math.Max(0f, AwayLifetime - Math.Max(0f, now - _leftAt));
        }

        public void ChangeMap(bool home, float now)
        {
            if (Expired || home == OnHomeMap) return;
            if (!home)
            {
                OnHomeMap = false;
                _leftAt = now;
                return;
            }
            // Never show an already-expired bag even for one rendered frame.
            if (now - _leftAt >= AwayLifetime)
            {
                Expired = true;
                return;
            }
            OnHomeMap = true;
            _leftAt = 0f;
        }

        public void Tick(float now)
        {
            // No expiry on the home map: an early return freezes the clock.
            if (!Expired && !OnHomeMap && now - _leftAt >= AwayLifetime)
                Expired = true;
        }

        public static FallenBagRetention FromSave(bool onHomeMap, float remaining, float now)
        {
            var state = new FallenBagRetention();
            if (onHomeMap) return state;
            float left = Math.Max(0f, Math.Min(AwayLifetime, remaining));
            state.OnHomeMap = false;
            state._leftAt = now - (AwayLifetime - left);
            if (left <= 0f) state.Expired = true;
            return state;
        }
    }
}
