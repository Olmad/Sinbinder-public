// Assets/AOS Engine/DeedType.cs
namespace Sinbinder.AOS
{
    /// <summary>
    /// Тип деяния воина. Деяния копятся в ReputationData и превращаются
    /// в титулы через TitleManager. Это внешняя система: на голосование
    /// модулей AOS она не влияет.
    /// </summary>
    public enum DeedType
    {
        Kill,
        KillCommander,
        SaveAlly,
        ProtectCommander,
        CollectMostLoot,
        FindTreasure,
        DigMostSouls,
        SurviveMission,
        LastStand,
        NeverRetreat,
        Escape,
        ExecuteEnemy,
        RecruitWarrior
    }
}
