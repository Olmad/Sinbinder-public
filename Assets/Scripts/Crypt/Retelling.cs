using System.Collections.Generic;
using Sinbinder.AOS;

namespace Sinbinder.Crypt
{
    /// <summary>
    /// Пересказ вылазки: что там случилось, пока игрок не смотрел.
    ///
    /// <b>Запись боя не работала ни разу.</b> AutoBattleResolver умеет
    /// её заполнять с самого начала — события, ходы, победитель, —
    /// но единственный вызывающий передавал четыре аргумента из пяти,
    /// и пятый, запись, всегда оставался null. Читать её тоже было
    /// некому. Шестой случай одной болезни: система написана, звена
    /// до игрока нет.
    ///
    /// Смысл ровно тот же, что у подписи над головой, только для боя,
    /// которого не видно. На поле игрок читает «Сбегает» и видит, кто.
    /// С вылазки он получал список выживших — и ни слова о том, почему
    /// кто-то из них не вернулся.
    ///
    /// <b>Что считать достойным пересказа, решает тот же
    /// <see cref="Moment"/>.</b> Второй список «что заметно» разошёлся
    /// бы с первым, и тогда в бою заметным было бы одно, а в пересказе
    /// другое.
    ///
    /// Чистый класс — проверяется стендом.
    /// </summary>
    public static class Retelling
    {
        /// <summary>
        /// Сколько случаев пересказывать. Полный список событий — это
        /// протокол, а не рассказ: с восьмерых стражей их набегает
        /// под сотню, и читать их не станет никто.
        /// </summary>
        public const int Most = 3;

        /// <summary>
        /// Две-три фразы о том, что случилось. Пусто — значит ничего
        /// примечательного: все просто дрались, и это тоже ответ.
        /// </summary>
        public static string Tell(BattleRecord record)
        {
            var picked = Pick(record);
            if (picked.Count == 0) return "";

            var sb = new System.Text.StringBuilder();

            foreach (var ev in picked)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(Line(ev));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Что достойно упоминания. Сначала громкое, потом остальное:
        /// если места хватит только на три случая, побег обязан попасть
        /// в них раньше, чем поход за добычей.
        /// </summary>
        public static List<BattleEvent> Pick(BattleRecord record)
        {
            var picked = new List<BattleEvent>();
            if (record == null || record.Events == null) return picked;

            foreach (var loud in new[] { Notice.Scene, Notice.Word })
            {
                foreach (var ev in record.Events)
                {
                    if (ev == null) continue;
                    if (Moment.Loudness(ev.Action) != loud) continue;

                    // Один воин, повторивший то же самое, — не новость.
                    // Пересказ говорит, что случилось, а не сколько раз.
                    if (Already(picked, ev)) continue;

                    picked.Add(ev);
                    if (picked.Count >= Most) return picked;
                }
            }

            return picked;
        }

        private static bool Already(List<BattleEvent> picked, BattleEvent ev)
        {
            foreach (var seen in picked)
                if (seen.Action == ev.Action && seen.ActorName == ev.ActorName)
                    return true;

            return false;
        }

        /// <summary>
        /// Одна фраза. Положение не передаётся, и это правильный ответ:
        /// приказов на вылазке не отдают, значит отход был побегом.
        /// </summary>
        private static string Line(BattleEvent ev)
        {
            string who = string.IsNullOrEmpty(ev.ActorName) ? "Кто-то" : ev.ActorName;
            return $"{who} {PhraseGenerator.Did(ev.Action, null, ev.ActorGender)}.";
        }
    }
}
