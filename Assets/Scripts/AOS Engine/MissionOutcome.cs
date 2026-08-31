// Assets/_Project/Scripts/AOS Engine/MissionOutcome.cs
namespace Sinbinder.AOS
{
    /// <summary>Чем закончилась миссия после решения командира.</summary>
    public enum MissionOutcome
    {
        VillageSaved,
        VillageDestroyed,
        VillageAbandoned,
        NewCultEstablished,
        TravelerEscaped,
        TravelerKilled,

        /// <summary>
        /// Ничего не произошло: командир прошёл мимо, и деревня осталась
        /// ровно такой, какой была. Стоит последним, а не первым, чтобы не
        /// сдвинуть номера уже сериализованных значений в ассетах квестов.
        /// </summary>
        None
    }
}
