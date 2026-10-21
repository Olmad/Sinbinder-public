// Assets/Scripts/Gameplay/InspectTexts.cs
// Перевод: текст через Loc
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// ТЕКСТЫ ВЕЩЕЙ, КОТОРЫЕ РАССМАТРИВАЮТ (клавиша F, наезд камеры).
    ///
    /// Правьте прямо здесь — в Visual Studio Code или любом редакторе.
    /// Сцены пересобирать НЕ нужно: игра берёт строки отсюда при запуске.
    ///
    /// Как править:
    ///   • меняйте только текст в кавычках внутри Loc.N("…");
    ///   • новая строка массива — новая запись (каждая печатается отдельно);
    ///   • Loc.N(...) не удаляйте — по нему находят строку для перевода.
    ///     Английская версия покажет старый перевод, пока его не обновят
    ///     (Resources/Lang/en.txt).
    ///
    /// Автор, 21 октября: «я могу и в коде сделать, главное, чтобы это было
    /// понятно и доступно с Visual Code».
    /// </summary>
    public static class InspectTexts
    {
        /// <summary>Тексты по имени вещи. Имя вещи ставит сборщик сцены.</summary>
        public static (string Prompt, string Speaker, string[] Lines)? For(string key)
        {
            switch (key)
            {
                case "AxeTree": return (AxeTreePrompt, AxeTreeSpeaker, AxeTree);
                // Пешка — ветка появится вместе с пешкой в сцене (строки ниже уже есть).
                default: return null;
            }
        }

        // ── Дерево с топором (запад лагеря, за палатками) ──────────────────

        public static readonly string AxeTreePrompt = Loc.N("F — прочесть вырезанное");
        public static readonly string AxeTreeSpeaker = Loc.N("Вырезано на коре");

        public static readonly string[] AxeTree =
        {
            Loc.N("Пять зарубок — пять ночей у этого дерева. Шестую вырезать было некому."),
            Loc.N("Топор не вынимай. Дерево мёртвое, а держит крепче живого."),
            Loc.N("Ушли за солью на восток. Вернусь — заберу топор. Т."),
            Loc.N("По ночам за лесом кто-то двигает фигуры. Слышно, как они стучат о доску."),
        };

        // ── Пешка (пасхалка в одной из палаток) ────────────────────────────
        // Текст — на сверку облачной (28-ORDERS, раздел 21 октября, п. 5).

        public static readonly string PawnPrompt = Loc.N("F — присмотреться");
        public static readonly string PawnSpeaker = Loc.N("Пешка");

        public static readonly string[] Pawn =
        {
            Loc.N("Ты думаешь, что двигаешь их. Я тоже так думала."),
        };
    }
}
