// Assets/AOS Engine/MissionDatabase.cs
using Sinbinder.Core;

namespace Sinbinder.AOS
{
    /// <summary>
    /// Запасной справочник «грех + мораль -> действие на миссии».
    /// Используется, когда у квеста нет своей таблицы CommanderOutcome.
    ///
    /// TODO(архитектура): это хардкод, который дублирует данные из
    /// Quest.commanderOutcomes. По принципу «данные управляют поведением»
    /// таблица должна жить в ScriptableObject. Пока оставлено как fallback.
    /// </summary>
    public static class MissionDatabase
    {
        public static MissionAction GetAction(SinType sin, MoralType moral)
        {
            switch (sin)
            {
                case SinType.Wrath:
                    if (moral == MoralType.Vicious) return MissionAction.KillEveryone;
                    if (moral == MoralType.Pious) return MissionAction.KillTraveler;
                    return MissionAction.TaxVillage;

                case SinType.Greed:
                    if (moral == MoralType.Vicious) return MissionAction.EnslaveVillage;
                    if (moral == MoralType.Pious) return MissionAction.TaxVillage;
                    return MissionAction.TaxVillage;

                case SinType.Sloth:
                    return MissionAction.IgnoreVillage;

                case SinType.Pride:
                    if (moral == MoralType.Pious) return MissionAction.SanctifyAltar;
                    return MissionAction.DestroyAltar;

                default:
                    return moral == MoralType.Pious
                        ? MissionAction.HelpVillage
                        : MissionAction.IgnoreVillage;
            }
        }
    }
}
