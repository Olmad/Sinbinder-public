using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.Core
{
    public static class CrisisResolver
    {
        public static bool WillSaveComrade(Warrior savior, Warrior wounded)
        {
            float chance = 30f;
            chance -= savior.Virtue.Value * 0.5f;
            chance += savior.Relationships.GetRelationship(savior.Id, wounded.Id) * 0.5f;
            chance -= savior.Soul.Moral == MoralType.Vicious ? 30f : 0f;
            chance += savior.Soul.Moral == MoralType.Pious ? 30f : 0f;
            return Random.Range(0f, 100f) < chance;
        }

        public static bool WillBetray(Warrior warrior)
        {
            float chance = 30f;
            chance += warrior.Virtue.Value * 0.4f;
            chance += (100f - warrior.Loyalty) * 0.3f;
            chance += warrior.Soul.Moral == MoralType.Vicious ? 25f : 0f;
            chance -= warrior.Soul.Moral == MoralType.Pious ? 25f : 0f;
            return Random.Range(0f, 100f) < Mathf.Clamp(chance, 5f, 90f);
        }

        public static Warrior ResolveDuel(Warrior a, Warrior b)
        {
            float powerA = a.Attack + Random.Range(0f, 10f);
            float powerB = b.Attack + Random.Range(0f, 10f);
            Warrior winner = powerA >= powerB ? a : b;
            Warrior loser = winner == a ? b : a;
            winner.Virtue.Change(20f);
            loser.Virtue.Change(-20f);
            winner.Relationships.Change(winner.Id, loser.Id, -15f);
            return winner;
        }

        public static bool WillBeSeduced(Warrior warrior, SinType enemySin, float enemyPower)
        {
            float chance = 15f;
            chance += warrior.Virtue.Value * 0.4f;
            chance += warrior.Soul.Sin == enemySin ? 40f : 0f;
            chance += warrior.Soul.Moral == MoralType.Vicious ? 25f : 0f;
            chance -= warrior.Soul.Moral == MoralType.Pious ? 25f : 0f;
            chance += enemyPower * 5f;
            return Random.Range(0f, 100f) < Mathf.Clamp(chance, 5f, 90f);
        }
    }
}