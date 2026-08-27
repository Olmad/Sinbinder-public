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