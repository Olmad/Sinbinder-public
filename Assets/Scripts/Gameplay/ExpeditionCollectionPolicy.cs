// Independent expedition reward rules. No Unity dependency.
// A future origin/ability must explicitly supply the specialist capability.
// Currently no NPC in the roster has a soul-collector or secret-finder skill.
namespace Sinbinder.Gameplay
{
    public enum ExpeditionFind { Ordinary, Secret, Soul }

    public static class ExpeditionCollectionPolicy
    {
        public static bool CanCollect(ExpeditionFind find, bool personallyPresent,
                                      bool soulCollector, bool secretFinder)
        {
            switch (find)
            {
                case ExpeditionFind.Ordinary: return true;
                case ExpeditionFind.Secret: return personallyPresent || secretFinder;
                case ExpeditionFind.Soul: return personallyPresent || soulCollector;
                default: return false;
            }
        }

        // Until the designed origin/acquired trait actually exists, no
        // soldier may silently inherit this permission from a Unity component.
        public const bool CurrentSquadHasSoulCollector = false;
        public const bool CurrentSquadHasSecretFinder = false;

        public static bool RemoteSquadCanCollect(ExpeditionFind find)
            => CanCollect(find, false, CurrentSquadHasSoulCollector,
                          CurrentSquadHasSecretFinder);
    }
}
