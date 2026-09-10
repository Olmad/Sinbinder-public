// Assets/Scripts/AOS Engine/MissionContext.cs
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

        /// <summary>
        /// Игрок что-то предложил на развилке.
        ///
        /// Именно предложил: на этом уровне у него ровно та же власть,
        /// что и в бою, — один голос. Его подаёт LoyaltyModule, и
        /// проиграть этот голос может так же, как проигрывает приказ.
        /// </summary>
        public bool HasSuggestion;
        public MissionAction SuggestedAction;
    }
}
