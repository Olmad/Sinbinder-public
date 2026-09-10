// Assets/Scripts/AOS Engine/MissionAction.cs
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
        DestroyAltar,

        // Развилка обоза (docs/19-MISSIONS.md §4.1). Отдельные значения,
        // а не переиспользование деревенских: «обложить деревню данью»
        // и «взять товар и отпустить» — разные поступки, и склеить их
        // ради экономии значило бы завести вторую правду в названии.
        /// <summary>Взять товар, людей отпустить.</summary>
        TakeGoodsSparePeople,
        /// <summary>Взять всё: и товар, и жизни.</summary>
        TakeEverything,
        /// <summary>Пропустить. Уйти ни с чем.</summary>
        LetThemPass,
        /// <summary>Забрать людей живыми: оболочки дороже серебра.</summary>
        TakePeople
    }
}
