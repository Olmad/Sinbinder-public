// Assets/AOS Engine/MissionContext.cs
namespace Sinbinder.AOS
{
    /// <summary>
    /// Контекст автономной миссии. Расширяет боевой DecisionContext
    /// фактами уровня задания — их читают те же модули личности.
    /// </summary>
    public class MissionContext : DecisionContext
    {
        public MissionID MissionID;
        public bool HasInnocentVictims;
        public bool HasGuiltyParty;
        public bool HasTreasure;
        public bool HasAltar;
        public bool IsVillageIntact;
        public NPCData Traveler;
        public NPCData VillageElder;
    }
}
