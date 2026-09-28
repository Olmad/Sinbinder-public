// Assets/Scripts/Core/Loc.cs
using System.Collections.Generic;
using System.Text;

namespace Sinbinder.Core
{
    /// <summary>
    /// Перевод текста игрока на другие языки (docs/38-LANG.md). Просьба автора
    /// 28 сентября: «нам нужно сделать возможность перевода на другие языки».
    ///
    /// <b>Ключ — сама русская строка.</b> Как в gettext: в коде остаётся
    /// живой русский текст, который писали и правили месяцами, а перевод
    /// лежит рядом таблицей (<c>Resources/Lang/en.txt</c>). Символьные ключи
    /// («refusal.distance.pride») развели бы текст и код: правящий фразу
    /// правил бы одно, а игрок читал другое.
    ///
    /// Четыре вызова:
    /// <list type="bullet">
    /// <item><see cref="T"/> — готовая строка: <c>Loc.T("Пауза")</c>;</item>
    /// <item><see cref="F"/> — строка с местами: <c>Loc.F("{0} не выполнил приказ", name)</c>.
    /// Места — номерами, а не склейкой: в другом языке порядок слов другой;</item>
    /// <item><see cref="N"/> — пометка без перевода: строка остаётся русской
    /// там, где она данные (имя души в записи), а сборщик каталога её видит;</item>
    /// <item><see cref="Name"/> — имя при показе: «Карган Старый Ворон» →
    /// «Kargan Old Raven». Сравнения по имени в логике идут по-русски, как шли.</item>
    /// </list>
    ///
    /// Нет перевода — русская строка как есть: игра на недопереведённом
    /// языке остаётся игрой, а не рассыпается пустыми местами.
    ///
    /// Чистый C#, без Unity: стенд грузит таблицу сам и проверяет, что
    /// английский вывод генератора фраз не содержит русских букв. Загрузку
    /// в игре делает <c>LocSetup</c>.
    /// </summary>
    public static class Loc
    {
        /// <summary>Язык, на котором написан код.</summary>
        public const string Source = "ru";

        public static string Language { get; private set; } = Source;

        public static bool IsSource => Language == Source;

        /// <summary>Язык сменился — перерисовать то, что уже на экране.</summary>
        public static event System.Action Changed;

        private static readonly Dictionary<string, string> Table = new Dictionary<string, string>();

        /// <summary>Перевод готовой строки.</summary>
        public static string T(string ru)
        {
            if (string.IsNullOrEmpty(ru) || IsSource) return ru;
            return Table.TryGetValue(ru, out var done) && !string.IsNullOrEmpty(done) ? done : ru;
        }

        /// <summary>Перевод строки с местами {0}, {1}…</summary>
        public static string F(string ru, params object[] args)
        {
            string format = T(ru);
            try { return string.Format(format, args); }
            catch (System.FormatException)
            {
                // Перевод с ошибкой в местах не должен ронять игру:
                // тогда — русская строка, она проверена.
                return string.Format(ru, args);
            }
        }

        /// <summary>Пометка для сборщика каталога: строка остаётся как есть.</summary>
        public static string N(string ru) => ru;

        /// <summary>
        /// Имя при показе игроку. Целиком нет в таблице — по словам: имена
        /// душ собираются из частей («Гертон» + ремесло «Крестьянин»,
        /// «Ловчий» + прозвище «Рыжий»), и таблица знает части, а не все
        /// сочетания. Слова, которого нет и в частях, — латиницей: русские
        /// буквы посреди английского хуже, чем «Gerton».
        /// </summary>
        public static string Name(string ru)
        {
            if (string.IsNullOrEmpty(ru) || IsSource) return ru;
            if (Table.TryGetValue(ru, out var whole) && !string.IsNullOrEmpty(whole)) return whole;

            var words = ru.Split(' ');
            for (int i = 0; i < words.Length; i++)
                words[i] = Table.TryGetValue(words[i], out var w) && !string.IsNullOrEmpty(w) ? w : Latin(words[i]);
            return string.Join(" ", words);
        }

        /// <summary>Русские буквы — латиницей, по-простому (Ждан → Zhdan).</summary>
        public static string Latin(string ru)
        {
            if (string.IsNullOrEmpty(ru)) return ru;
            var b = new StringBuilder(ru.Length + 4);
            foreach (char c in ru)
            {
                int i = Cyrillic.IndexOf(char.ToLowerInvariant(c));
                if (i < 0) { b.Append(c); continue; }
                string lat = LatinOf[i];
                if (lat.Length > 0 && char.IsUpper(c))
                    lat = char.ToUpperInvariant(lat[0]) + lat.Substring(1);
                b.Append(lat);
            }
            return b.ToString();
        }

        private const string Cyrillic = "абвгдеёжзийклмнопрстуфхцчшщъыьэюя";

        private static readonly string[] LatinOf =
        {
            "a", "b", "v", "g", "d", "e", "yo", "zh", "z", "i", "y", "k", "l", "m", "n", "o", "p",
            "r", "s", "t", "u", "f", "kh", "ts", "ch", "sh", "shch", "", "y", "", "e", "yu", "ya",
        };

        /// <summary>Есть ли у строки перевод на текущий язык.</summary>
        public static bool Has(string ru) => IsSource || (ru != null && Table.TryGetValue(ru, out var s) && !string.IsNullOrEmpty(s));

        /// <summary>
        /// Включить язык. <paramref name="catalog"/> — текст таблицы
        /// (<c>Resources/Lang/xx.txt</c>); для русского не нужен.
        /// Возвращает, сколько строк переведено.
        /// </summary>
        public static int Use(string language, string catalog)
        {
            Table.Clear();
            Language = string.IsNullOrEmpty(language) ? Source : language;
            int n = IsSource || catalog == null ? 0 : Parse(catalog, Table);
            Changed?.Invoke();
            return n;
        }

        // ---------- таблица ----------

        /// <summary>
        /// Разбор таблицы. Формат — подмножество PO (gettext), чтобы
        /// переводчик мог открыть её обычным редактором переводов:
        /// <code>
        /// #: AOS Engine/PhraseGenerator.cs
        /// msgid "Приказ был."
        /// msgstr "An order was given."
        /// </code>
        /// Строки продолжаются следующей строкой в кавычках; экранирование —
        /// обратной косой чертой, как в C#: кавычка, сама черта, n и t.
        /// Пустой msgstr — перевода нет.
        /// </summary>
        public static int Parse(string text, Dictionary<string, string> into)
        {
            into.Clear();
            if (string.IsNullOrEmpty(text)) return 0;

            string id = null, str = null;
            StringBuilder current = null;
            int count = 0;

            void Flush()
            {
                if (id != null && str != null && id.Length > 0)
                {
                    into[id] = str;
                    if (str.Length > 0) count++;
                }
                id = null; str = null; current = null;
            }

            foreach (var raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0) { Flush(); continue; }
                if (line[0] == '#') continue;

                if (line.StartsWith("msgid "))
                {
                    Flush();
                    current = new StringBuilder(Unquote(line.Substring(6)));
                    id = current.ToString();
                    continue;
                }
                if (line.StartsWith("msgstr "))
                {
                    if (current != null) id = current.ToString();
                    current = new StringBuilder(Unquote(line.Substring(7)));
                    str = current.ToString();
                    continue;
                }
                if (line[0] == Quote && current != null)
                {
                    current.Append(Unquote(line));
                    if (str != null) str = current.ToString(); else id = current.ToString();
                }
            }
            Flush();
            return count;
        }

        private const char Quote = '\u0022';

        private static string Unquote(string s)
        {
            s = s.Trim();
            if (s.Length >= 2 && s[0] == Quote && s[s.Length - 1] == Quote) s = s.Substring(1, s.Length - 2);

            var b = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char e = s[++i];
                    b.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e);
                }
                else b.Append(c);
            }
            return b.ToString();
        }
    }
}
