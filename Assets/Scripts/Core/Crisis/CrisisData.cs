// Assets/_Project/Scripts/Core/Crisis/CrisisData.cs
using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.Core
{
    [System.Serializable]
    public class CrisisChoice
    {
        public string Text;
        public string OutcomeText;
        public float VirtueChange;
        public float LoyaltyChange;
        public float RelationshipChange;
        public bool RequiresGold;
        public int GoldCost;
    }

    [System.Serializable]
    public class CrisisData
    {
        public string Id;
        public CrisisType Type;
        public SinType WarriorSin;          // <-- Точное указание греха воина
        public string Title;
        public string Description;
        public string WarriorQuote;
        public List<CrisisChoice> Choices;
        public string TriggerCondition;
    }
}