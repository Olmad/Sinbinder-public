using System;
using System.Collections.Generic;
using System.IO;
using Sinbinder.AOS;

namespace Sinbinder.Core
{
    // Test double ONLY; actual gameplay builds use the real Loc.
    public static class Loc
    {
        public static string T(string source) => source;
    }
}

internal static class Program
{
    private static int _checks;

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + description);
        _checks++;
    }

    private static int Occurrences(string text, string token)
    {
        int count = 0;
        int at = 0;
        while ((at = text.IndexOf(token, at, StringComparison.Ordinal)) >= 0)
        {
            ++count;
            at += token.Length;
        }
        return count;
    }

    private static void Main()
    {
        const string noRecords = "Записей о ходе боя нет.";
        const string inventedObedience = "Отряд слушался";
        const string inventedCompliance = "В остальном приказы исполнялись";
        const string inventedBargaining = "не столько командовали, сколько договаривались";

        Check(BattleNarrator.Build(null) == noRecords,
              "null journal reports missing records, not obedience");
        Check(BattleNarrator.Build(Array.Empty<string>()) == noRecords,
              "empty journal reports missing records, not obedience");
        Check(BattleNarrator.Build(new[] { "", null, "   " }) == noRecords,
              "blank journal entries do not generate invented history");

        string refusal = BattleNarrator.Build(new[] { "Воин отказался выполнить приказ." });
        Check(refusal.Contains("Воин отказался выполнить приказ.", StringComparison.Ordinal) &&
              !refusal.Contains(inventedCompliance, StringComparison.Ordinal) &&
              !refusal.Contains(inventedObedience, StringComparison.Ordinal),
              "single refusal retains record and does not falsely claim compliance");

        string five = BattleNarrator.Build(new[] { "Дождь.", "Гром.", "Ветер.", "Туман.", "Холод." });
        Check(five.Contains("Дождь.", StringComparison.Ordinal) &&
              five.Contains("Холод.", StringComparison.Ordinal) &&
              !five.Contains(inventedBargaining, StringComparison.Ordinal) &&
              !five.Contains(inventedCompliance, StringComparison.Ordinal),
              "five unrelated events cannot be interpreted as bargaining");

        string repeated = BattleNarrator.Build(new[] { "Схватка.", "Схватка.",
                                                       "Схватка.", null, "Отступление." });
        Check(Occurrences(repeated, "— Схватка.") == 1 &&
              repeated.Contains("И так раз за разом.", StringComparison.Ordinal) &&
              repeated.Contains("— Отступление.", StringComparison.Ordinal),
              "existing repeated-event compression is preserved");

        string single = BattleNarrator.Build(new List<string> { "Отряд занял позицию." });
        Check(single.Contains("Как это было.", StringComparison.Ordinal) &&
              single.Contains("— Отряд занял позицию.", StringComparison.Ordinal) &&
              !single.Contains(inventedCompliance, StringComparison.Ordinal),
              "normal narration preserves header and factual events");

        string locale = File.ReadAllText("Assets/Resources/Lang/en.txt");
        Check(Occurrences(locale, "msgid \"" + noRecords + "\"") == 1 &&
              Occurrences(locale, "msgstr \"No battle events were recorded.\"") == 1,
              "new RU entry has exactly one nonempty EN translation");

        Console.WriteLine("PASS: " + _checks +
            " compiled BattleNarrator C# assertions (source behavior, NOT Unity PlayMode).");
    }
}
