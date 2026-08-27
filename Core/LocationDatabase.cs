using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.Core
{
    [CreateAssetMenu(fileName = "LocationDatabase", menuName = "Sinbinder/Location Database")]
    public class LocationDatabase : ScriptableObject
    {
        public List<LocationData> Locations = new();
    }

    [System.Serializable]
    public class LocationData
    {
        public string SceneName;   // Имя сцены Unity (например, "Crypt")
        public string DisplayName; // Отображаемое имя (например, "Склеп")
        public string Tag;         // Тег для перков ("Ruins", "Crypt", "Forest", "Village")
    }
}