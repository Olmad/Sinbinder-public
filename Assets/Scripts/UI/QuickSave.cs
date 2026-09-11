using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.UI
{
    /// <summary>
    /// Быстрое сохранение на F5, быстрая загрузка на F6.
    ///
    /// Одно гнездо, не десять: игра про то, что сделанного не вернуть,
    /// а десять гнёзд — это приглашение вернуть. Гнездо всё же есть,
    /// потому что закрыть игру и продолжить завтра надо уметь в любом
    /// режиме.
    ///
    /// <b>F6 в режиме обязательств не работает</b>, и об этом говорится
    /// вслух. Молчащая клавиша читается как поломка, а этот запрет —
    /// половина замысла: если после отказа можно перезагрузиться, отказ
    /// не стоит ничего. Правило живёт в <see cref="Commitment"/>,
    /// а здесь только клавиши.
    /// </summary>
    public class QuickSave : MonoBehaviour
    {
        [SerializeField] private KeyCode _save = KeyCode.F5;
        [SerializeField] private KeyCode _load = KeyCode.F6;

        void Update()
        {
            if (Input.GetKeyDown(_save)) Save();
            else if (Input.GetKeyDown(_load)) Load();
        }

        private void Save()
        {
            bool ok = SaveSystem.Write(SaveSystem.Snapshot(), SaveSystem.QuickPath);

            Say(ok
                ? "Записано."
                : "Записать не вышло — некуда или не даёт.");
        }

        private void Load()
        {
            if (!Commitment.CanLoad) { Say(Commitment.WhyNoLoad); return; }

            if (!SaveSystem.Exists(SaveSystem.QuickPath))
            {
                Say("Возвращаться некуда: ничего не записано.");
                return;
            }

            var save = SaveSystem.Read(SaveSystem.QuickPath);

            // Прочитать половину хуже, чем не прочитать ничего: половина
            // выглядит целой, и игрок узнает о потере через час.
            Say(SaveSystem.Restore(save)
                ? "Вернулись к записанному."
                : "Эта запись не от нынешней игры.");
        }

        private static void Say(string line)
            => Object.FindFirstObjectByType<BattleLogUI>()?.Write(line);
    }
}
