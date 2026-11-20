// Shared by the real Unity developer console and .NET regression checks.
// Keep keyboard-layout handling and command normalization deterministic.
using System;

namespace Sinbinder.Dev
{
    public static class ConsoleCommandSyntax
    {
        // BackQuote is the physical key. Windows/Russian input may instead
        // report the glyph ё/Ё, or shifted ~/unshifted backtick.
        public static bool IsToggleCharacter(char value)
            => value == 'ё' || value == 'Ё' || value == '~' || value == '`';

        public static string Normalize(string typed)
        {
            if (string.IsNullOrWhiteSpace(typed)) return "";
            string cleaned = typed.Replace("`", "").Replace("~", "")
                .Replace("ё", "").Replace("Ё", "");
            return string.Join(" ", cleaned.Split(
                new[] { ' ', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
        }
    }
}
