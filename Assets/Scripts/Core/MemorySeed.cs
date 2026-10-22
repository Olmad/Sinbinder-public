// Assets/Scripts/Core/MemorySeed.cs
// Перевод: текст через Loc
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

        [Tooltip("Сколько ночей наказания душа отбыла за это деяние (Core/Punishment). "
               + "Отбыла предел — наказание за него только копит обиду.")]
        public int Atoned;

        [Tooltip("Врождённые модификаторы действий, которые не меняются со временем.")]
        public List<ActionModifier> ActionModifiers = new();

        [Tooltip("Нарративные перки души: личная история, которую разбирает PerkResolver.")]
        public List<NarrativePerk> NarrativePerks = new();
    }

    [System.Serializable]
    public class ActionModifier
    {
        public ActionType Action;
        public float ScoreBonus;
        public bool Blocked;
    }
}