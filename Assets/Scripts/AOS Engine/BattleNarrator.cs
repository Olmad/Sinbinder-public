// Assets/Scripts/AOS Engine/BattleNarrator.cs
using System.Collections.Generic;
using System.Text;

namespace Sinbinder.AOS
{
    /// <summary>
    /// Рассказ о бое из строк журнала.
    ///
    /// Не сводка и не статистика: игроку не нужно знать, сколько раз кто
    /// ослушался. Ему нужно понять, что это был за бой и кто в нём каким
    /// оказался. Поэтому рассказ короткий и без чисел, а повторы
    /// сворачиваются в одну строку.
    /// </summary>
    public static class BattleNarrator
    {
        public static string Build(IReadOnlyList<string> entries)
        {
            if (entries == null || entries.Count == 0)
                return "Бой прошёл без единого спора. Отряд слушался.";

            var sb = new StringBuilder();
            sb.AppendLine("Как это было.");
            sb.AppendLine();

            // Повтор одной и той же строки — не событие, а привычка.
            string previous = null;
            int repeats = 0;

            for (int i = 0; i <= entries.Count; i++)
            {
                string current = i < entries.Count ? entries[i] : null;

                if (current == previous) { repeats++; continue; }

                if (previous != null)
                {
                    sb.Append("— ").Append(previous);
                    if (repeats > 0) sb.Append(repeats == 1 ? " И снова." : " И так раз за разом.");
                    sb.AppendLine();
                }

                previous = current;
                repeats = 0;
            }

            sb.AppendLine();
            sb.Append(entries.Count > 4
                ? "Отряд, которым не столько командовали, сколько договаривались."
                : "В остальном приказы исполнялись.");

            return sb.ToString();
        }
    }
}
