// Assets/Scripts/AOS Engine/ReputationData.cs
using System;
using System.Collections.Generic;

namespace Sinbinder.AOS
{
    /// <summary>
    /// Одно зафиксированное деяние. Importance складывается по типу деяния
    /// и сравнивается с порогом в TitleRule.
    /// </summary>
    [Serializable]
    public class DeedRecord
    {
        public DeedType Type;
        public float Importance;
        public DateTime Time;
    }

    /// <summary>
    /// Долгосрочная репутация воина: что он сделал и как его за это зовут.
    /// Живёт на Warrior, обновляется через AOSEventHub и TitleManager.
    /// </summary>
    [Serializable]
    public class ReputationData
    {
        public string CurrentName;
        public string CurrentLegendaryTitle;
        public bool LegendaryUnlocked;
        public float Respect;
        public float Fear;
        public List<DeedRecord> Deeds = new();
    }
}
