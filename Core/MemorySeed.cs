// Assets/_Project/Scripts/Core/MemorySeed.cs
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.AOS;

namespace Sinbinder.Core
{
    [System.Serializable]
    public class MemorySeed
    {
        public string Object;
        public string Emotion;
        public string Story;
        public string Trigger;

        [Tooltip("Врождённые модификаторы действий, которые не меняются со временем.")]
        public List<ActionModifier> ActionModifiers = new();
    }

    [System.Serializable]
    public class ActionModifier
    {
        public ActionType Action;
        public float ScoreBonus;
        public bool Blocked;
    }
}