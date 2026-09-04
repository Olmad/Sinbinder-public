// Assets/Scripts/Dialogue/DialogueDatabase.cs
using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.Dialogue
{
    [CreateAssetMenu(fileName = "DialogueDatabase", menuName = "Sinbinder/Dialogue Database")]
    public class DialogueDatabase : ScriptableObject
    {
        public List<DialogueSituation> Situations = new();
    }
}