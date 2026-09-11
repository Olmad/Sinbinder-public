using System.Text.RegularExpressions;

namespace Sinbinder.Core
{
    /// <summary>Пол души. От него зависит половина строк о воине.</summary>
    public enum Gender
    {
        Male = 0,
        Female = 1,
    }

    /// <summary>
    /// Согласование по роду.
    ///
    /// Без пола русский текст врёт в каждой второй строке: «Марга ушёл
    /// за добычей» стояло даже в самопроверке движка, и стояло месяцами.
    /// Отряд с двумя женщинами задуман с самого начала.
    ///
    /// <b>Две разные задачи, и решаются они по-разному.</b>
    ///
    /// Местоимения — закрытый и правильный набор: он→она, его→её,
    /// ему→ей. Их меняем механически, и это надёжно.
    ///
    /// Глаголы — нет. «Пошёл» даёт «пошла», а «лёг» — «легла», и никакое
    /// правило по окончанию этого не берёт. Правило, которое почти
    /// работает, хуже отсутствующего: оно выдаёт «пошёла» и молчит.
    /// Поэтому глаголы пишутся парами, обе формы на одной строке —
    /// разойтись им негде.
    ///
    /// Правило без Unity — проверяется стендом.
    /// </summary>
    public static class Grammar
    {
        /// <summary>Выбрать форму. Пары пишутся рядом, обе на виду.</summary>
        public static string Pick(Gender gender, string he, string she)
            => gender == Gender.Female ? she : he;

        /// <summary>
        /// Перевести местоимения в женский род.
        ///
        /// Только целые слова: «сторону» содержит «он», и слепая замена
        /// сделала бы из неё «сторану».
        ///
        /// «Им» сюда не входит намеренно. У него два разных хозяина —
        /// творительный от «он» и дательный от «они», — и различить их
        /// по строке нельзя. Фразы с ним переписаны так, чтобы его
        /// не было: одна правка текста дешевле, чем правило, которое
        /// иногда врёт.
        /// </summary>
        public static string Her(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            return Pronoun.Replace(text, m =>
            {
                switch (m.Value.ToLowerInvariant())
                {
                    case "он":   return Keep(m.Value, "она");
                    case "его":  return Keep(m.Value, "её");
                    case "ему":  return Keep(m.Value, "ей");
                    case "него": return Keep(m.Value, "неё");
                    case "нему": return Keep(m.Value, "ней");
                    case "нём":  return Keep(m.Value, "ней");
                    case "нем":  return Keep(m.Value, "ней");
                    case "ним":  return Keep(m.Value, "ней");
                    default:     return m.Value;
                }
            });
        }

        /// <summary>Применить род к готовой строке: мужской не трогаем.</summary>
        public static string For(Gender gender, string text)
            => gender == Gender.Female ? Her(text) : text;

        /// <summary>Сохранить заглавную букву, если она была.</summary>
        private static string Keep(string was, string now)
        {
            if (string.IsNullOrEmpty(was) || !char.IsUpper(was[0])) return now;
            return char.ToUpperInvariant(now[0]) + now.Substring(1);
        }

        private static readonly Regex Pronoun =
            new Regex(@"\b(он|его|ему|него|нему|нём|нем|ним)\b",
                      RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }
}
