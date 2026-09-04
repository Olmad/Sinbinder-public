// Assets/Scripts/Dialogue/DialogueLoader.cs
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.Dialogue
{
    public static class DialogueLoader
    {
        private static DialogueDatabase _database;
        private static bool _lookedUp;

        public static void LoadDatabase(DialogueDatabase database)
        {
            _database = database;
            _lookedUp = true;
        }

        /// <summary>
        /// База из Resources, если её никто не подал руками.
        ///
        /// Ссылку в инспекторе легко забыть, и тогда все реплики
        /// вырождались в «[Имя]: ...» без единой ошибки в консоли.
        /// Правило проекта одно для всех баз: работать без ассета,
        /// но сказать об этом один раз.
        /// </summary>
        private static DialogueDatabase Database
        {
            get
            {
                if (_lookedUp) return _database;
                _lookedUp = true;

                _database = Resources.Load<DialogueDatabase>("DialogueDatabase");
                if (_database == null)
                    Debug.LogWarning("[Диалоги] Resources/DialogueDatabase не найден — "
                        + "воины молчат многоточиями. Собери базу через "
                        + "Sinbinder → Собрать ассеты демо.");

                return _database;
            }
        }

        public static bool TryGetLine(Warrior speaker, string situationName, out string text)
        {
            text = $"[{speaker.DisplayName}]: ...";

            var database = Database;
            if (database == null) return false;

            var situation = database.Situations.Find(s => s.SituationName == situationName);
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