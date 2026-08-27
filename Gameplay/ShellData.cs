using UnityEngine;

namespace Sinbinder.Core
{
    [CreateAssetMenu(fileName = "ShellData", menuName = "Sinbinder/Shells/New Shell")]
    public class ShellData : ScriptableObject
    {
        public string shellName;
        public float baseHP;
        public float baseDefense;
        public float movementSpeed;
        public bool canBeRevived;
        // Дополнительные свойства оболочки
    }
}