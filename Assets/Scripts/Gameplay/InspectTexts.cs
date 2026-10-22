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
                case "Pawn": return (PawnPrompt, PawnSpeaker, Pawn);
                case "Map": return (MapPrompt, MapSpeaker, Map);
                case "Jars": return (JarsPrompt, JarsSpeaker, Jars);
                case "Bed": return (BedPrompt, BedSpeaker, Bed);
                default: return null;
            }
        }

        // ── Дерево с топором (запад лагеря, за палатками) ──────────────────

        public static readonly string AxeTreePrompt = Loc.N("F — прочесть вырезанное");
        public static readonly string AxeTreeSpeaker = Loc.N("Вырезано на коре");

        // Сверено облачной 22.10: всё вырезал один человек, «Т.», — поэтому
        // ни строки голосом рассказчика («Шестую вырезать было некому» —
        // а кто тогда вырезал это?). Записи — по порядку ночей; что он
        // не вернулся, говорит сам топор, который так и торчит в дереве.
        public static readonly string[] AxeTree =
        {
            Loc.N("Пятая ночь у этого дерева. Зарубка за каждую."),
            Loc.N("По ночам за лесом кто-то двигает фигуры. Слышно, как они стучат о доску."),
            Loc.N("Топор не вынимай. Дерево мёртвое, а держит крепче живого."),
            Loc.N("Ушли за солью на восток. Вернусь — заберу топор. Т."),
        };

        // ── Пешка (пасхалка в одной из палаток) ────────────────────────────
        // Сверено облачной 22.10 — оставить как есть: первая строка — тезис
        // игры («не командуй, искушай») устами фигуры; вторая и третья —
        // намёк на других Греховодов и на то, что Греховод сам — фигура
        // (00-GDD §3: пешки Бога Смерти, обретшие волю).

        public static readonly string PawnPrompt = Loc.N("F — присмотреться");
        public static readonly string PawnSpeaker = Loc.N("Пешка");

        public static readonly string[] Pawn =
        {
            Loc.N("Ты думаешь, что двигаешь их."),
            Loc.N("Он тоже так думал — тот, кто вырезал меня из кости и оставил здесь."),
            Loc.N("Каждая фигура однажды перестаёт слушаться руки. Даже та, что держит доску."),
        };

        // ── Шатёр Греховода (автор, 21 октября, п. 24: «хочется больше
        // взаимодействий с предметами, вероятно, даже в палатке Греховода»).
        // ЧЕРНОВИК работника D, 22.10 — на суд облачной (мир — её).

        public static readonly string MapPrompt = Loc.N("F — рассмотреть карту");
        public static readonly string MapSpeaker = Loc.N("Карта на коже");

        public static readonly string[] Map =
        {
            Loc.N("Склеп обведён углём. Рядом приписка: «Только если всё потеряно»."),
            Loc.N("Застава на перевале отмечена дважды. Над ней — знак охотников, перечёркнутый."),
            Loc.N("На краю, где кожа истёрлась, — три фигуры. Одна стёрта пальцем."),
        };

        public static readonly string JarsPrompt = Loc.N("F — взглянуть на банки");
        public static readonly string JarsSpeaker = Loc.N("Банки для душ");

        public static readonly string[] Jars =
        {
            Loc.N("Стекло холодное. Пустые банки ждут, как ждут пустые места у костра."),
            Loc.N("В одной огонёк. Жадный — льнёт к стенке, за которой лежит монета."),
        };

        public static readonly string BedPrompt = Loc.N("F — постель");
        public static readonly string BedSpeaker = Loc.N("Постель");

        public static readonly string[] Bed =
        {
            Loc.N("Шкуры ещё тёплые. Свет из сна не уходит из-под век."),
            Loc.N("Под изголовьем что-то белеет. Нет — просто узел на ремне."),
        };
    }
}
