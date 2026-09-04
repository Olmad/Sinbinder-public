// Assets/Scripts/Dialogue/DialogueSituation.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Dialogue
{
    [CreateAssetMenu(fileName = "DialogueSituation", menuName = "Sinbinder/Dialogue Situation")]
    public class DialogueSituation : ScriptableObject
    {
        public string SituationName;

        [Serializable]
        public struct LineEntry
        {
            public SinType Sin;
            public MoralType Moral;
            [TextArea(1, 3)] public string Text;
        }

        public List<LineEntry> Lines = new();

        public string GetLine(SinType sin, MoralType moral)
        {
            // Сначала ищем точное совпадение
            var match = Lines.Find(l => l.Sin == sin && l.Moral == moral);
            if (!string.IsNullOrEmpty(match.Text)) return match.Text;

            // Затем ищем только по греху
            match = Lines.Find(l => l.Sin == sin);
            if (!string.IsNullOrEmpty(match.Text)) return match.Text;

            return null;
        }
    }
}