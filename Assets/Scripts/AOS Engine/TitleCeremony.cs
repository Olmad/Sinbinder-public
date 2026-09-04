// Assets/Scripts/AOS Engine/TitleCeremony.cs
using System.Collections;
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.AOS
{
    public static class TitleCeremony
    {
        public static void Start(Warrior warrior, string title, bool isLegendary)
        {
            var ceremony = Object.FindObjectOfType<TitleCeremonyBehaviour>();
            if (ceremony == null)
            {
                Debug.LogWarning("[TITLE] TitleCeremonyBehaviour не найден на сцене!");
                return;
            }
            ceremony.StartCoroutine(ceremony.PlayCeremony(warrior, title, isLegendary));
        }
    }
}
