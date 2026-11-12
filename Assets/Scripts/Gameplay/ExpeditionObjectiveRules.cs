// Deterministic, Unity-free expedition aftermath contract.
// A survival report alone is not proof that the guarded objective was captured.
using System;

namespace Sinbinder.Gameplay
{
    public static class ExpeditionObjectiveRules
    {
        /// <summary>
        /// A non-combat resolution can complete its chosen objective; after
        /// battle the party must survive AND remove all guarding resistance.
        /// </summary>
        public static bool Secured(bool fought, int foesFallen, int guards, int survivors)
        {
            if (guards < 0 || foesFallen < 0 || survivors <= 0) return false;
            return !fought || foesFallen >= guards;
        }

        /// <summary>No corpses or cargo are automatically looted after retreat.</summary>
        public static int DeliveredCoins(bool secured, int fallenCoins, int cargoCoins)
        {
            if (fallenCoins < 0 || cargoCoins < 0)
                throw new ArgumentOutOfRangeException(nameof(fallenCoins),
                    "Expeditions cannot create negative loot.");
            return secured ? checked(fallenCoins + cargoCoins) : 0;
        }
    }
}
