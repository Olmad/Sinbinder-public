// Assets/AOS Engine/MissionAction.cs
namespace Sinbinder.AOS
{
    /// <summary>
    /// Что командир решает сделать на автономной миссии.
    /// Аналог ActionType, но на уровне задания, а не тика боя:
    /// выбирается один раз и определяет исход (MissionOutcome).
    /// </summary>
    public enum MissionAction
    {
        HelpVillage,
        IgnoreVillage,
        TaxVillage,
        EnslaveVillage,
        KillEveryone,
        KillTraveler,
        SanctifyAltar,
        DestroyAltar
    }
}
