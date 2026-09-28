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

        /// <summary>
        /// Имя в дательном падеже: «бросился к Косому Ждану», а не «к Косой
        /// Ждан» (журнал прогона 26 сентября, <c>14-HANDOFF.md</c> §111).
        ///
        /// Склоняется каждое слово имени и каждая часть через дефис:
        /// «Карган Старый Ворон» → «Каргану Старому Ворону», «Охотник-следопыт
        /// Хромой» → «Охотнику-следопыту Хромому». Род — того, <b>к кому</b>
        /// бросились: «Лиска» → «Лиске», и мужское «Марга» → «Марге» тоже.
        ///
        /// Имена в игре — закрытый набор: отряд, ремёсла, прозвища охотников.
        /// Правило покрывает их все, и стенд сверяет каждое. Чего оно
        /// не различает: существительное на «-ий» («Василий») склонит как
        /// прилагательное. Таких имён в игре нет; появится — сюда, в исключения,
        /// а не в правило, которое начнёт врать другим.
        ///
        /// После предлога имя дальше не склоняется: «Гертон из Вельска» →
        /// «Гертону из Вельска».
        /// </summary>
        public static string Dative(string name, Gender gender)
        {
            if (string.IsNullOrWhiteSpace(name)) return name;

            var words = name.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (System.Array.IndexOf(Preposition, words[i].ToLowerInvariant()) >= 0) break;

                var parts = words[i].Split('-');
                for (int j = 0; j < parts.Length; j++)
                    parts[j] = DativeWord(parts[j], gender);
                words[i] = string.Join("-", parts);
            }
            return string.Join(" ", words);
        }

        private static string DativeWord(string word, Gender gender)
        {
            // «Ю», инициалы и всё не по-русски — не склоняются.
            if (word.Length < 2 || !IsCyrillic(word)) return word;

            string low = word.ToLowerInvariant();
            string Cut(int n) => word.Substring(0, word.Length - n);

            if (gender == Gender.Female)
            {
                if (low.EndsWith("ая")) return Cut(2) + "ой";   // Косая → Косой
                if (low.EndsWith("яя")) return Cut(2) + "ей";   // Синяя → Синей
                if (low.EndsWith("ия")) return Cut(1) + "и";    // Мария → Марии
                if (low.EndsWith("а") || low.EndsWith("я")) return Cut(1) + "е";   // Лиска → Лиске
                if (low.EndsWith("ь")) return Cut(1) + "и";     // Любовь → Любови
                return word;                                    // женское на согласную не склоняется
            }

            if (low.EndsWith("ый") || low.EndsWith("ой")) return Cut(2) + "ому";    // Старый, Косой
            if (low.EndsWith("ий") && low.Length >= 3)                              // Тихий → Тихому,
                return Cut(2) + ("гкх".IndexOf(low[low.Length - 3]) >= 0 ? "ому" : "ему"); // Ловчий → Ловчему
            if (low.EndsWith("ия")) return Cut(1) + "и";
            if (low.EndsWith("а") || low.EndsWith("я")) return Cut(1) + "е";        // Марга → Марге
            if (low.EndsWith("ь") || low.EndsWith("й")) return Cut(1) + "ю";        // Хорь → Хорю
            if ("аеёиоуыэюя".IndexOf(low[low.Length - 1]) >= 0) return word;        // на гласную — не склоняется
            return word + "у";                                                      // Карган → Каргану
        }

        private static bool IsCyrillic(string word)
        {
            foreach (char c in word)
                if (char.IsLetter(c) && (c < '\u0400' || c > '\u04FF')) return false;
            return true;
        }

        private static readonly string[] Preposition =
        {
            "из", "с", "со", "от", "у", "в", "во", "на", "под", "над", "за", "при", "до",
        };

        private static readonly Regex Pronoun =
            new Regex(@"\b(он|его|ему|него|нему|нём|нем|ним)\b",
                      RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }
}
