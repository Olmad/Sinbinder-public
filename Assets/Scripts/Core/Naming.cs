// Assets/Scripts/Core/Naming.cs
namespace Sinbinder.Core
{
    /// <summary>
    /// Как воина зовут на экране.
    ///
    /// Замысел автора: пока титула нет, после имени стоит ремесло —
    /// «Гертон Крестьянин», «Сквип Паук». Заработал титул — и порядок
    /// слов переворачивается: «Костекоп Гертон».
    ///
    /// Приём держится сам, без объяснений. Игрок видит, что слово
    /// переехало вперёд, и понимает, что оно теперь другого сорта:
    /// ремесло у души было всегда, титул она нажила при нём. Ни одной
    /// подсказки для этого не нужно — нужен только порядок слов.
    ///
    /// Правило без Unity — проверяется стендом.
    /// </summary>
    public static class Naming
    {
        /// <summary>
        /// Полное имя. <paramref name="title"/> — то, что уже сложил
        /// <c>TitleManager</c> в виде «Титул Имя»; пусто — титула нет.
        /// </summary>
        public static string Full(string name, Trade trade, string title)
        {
            if (!string.IsNullOrEmpty(title)) return title;
            if (string.IsNullOrEmpty(name)) return name;

            string craft = Trades.Name(trade);
            return string.IsNullOrEmpty(craft) ? name : name + " " + craft;
        }
    }
}
