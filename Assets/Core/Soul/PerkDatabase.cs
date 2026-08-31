// Assets/Core/Soul/PerkDatabase.cs
using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.Core
{
    /// <summary>
    /// Каталог всех SoulPerk в игре. Заполняется через
    /// Sinbinder/Initialize Perk Database (PerkDatabaseInitializer).
    /// </summary>
    [CreateAssetMenu(fileName = "PerkDatabase", menuName = "Sinbinder/Perk Database")]
    public class PerkDatabase : ScriptableObject
    {
        public List<SoulPerk> AllPerks = new();
    }
}
