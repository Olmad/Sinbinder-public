// Assets/Dialogue/DialogueLoader.cs
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.Dialogue
{
    public static class DialogueLoader
    {
        private static DialogueDatabase _database;

        public static void LoadDatabase(DialogueDatabase database)
        {
            _database = database;
        }

        public static bool TryGetLine(Warrior speaker, string situationName, out string text)
        {
            text = $"[{speaker.DisplayName}]: ...";

            if (_database == null) return false;

            var situation = _database.Situations.Find(s => s.SituationName == situationName);
            if (situation == null) return false;

            string line = situation.GetLine(speaker.Soul.Sin, speaker.Soul.Moral);
            if (!string.IsNullOrEmpty(line))
            {
                text = line;
                return true;
            }

            return false;
        }
    }
}