// Assets/_Project/Scripts/Core/Soul/SoulPerk.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Audio;

namespace Sinbinder.Core
{
    [CreateAssetMenu(fileName = "SoulPerk", menuName = "Sinbinder/Soul Perk")]
    public class SoulPerk : ScriptableObject
    {
        public string PerkName;
        public PerkType Type;
        public string Description;
        public string Title;

        // Привязка к месту
        public string RequiredLocationTag; // "Ruins", "Crypt", "Forest"

        // Привязка к персонажу
        public string RelatedCharacterName;

        // Эффекты на поведение
        public List<PerkEffect> Effects = new();

        // Эффект на голос
        public VoiceModifier VoiceModifier = new();
    }
}