// Assets/Scripts/Core/Transparency.cs
namespace Sinbinder.Core
{
    /// <summary>
    /// Ступень прозрачности: сколько игре позволено объяснять.
    /// Лестница из 00-GDD.md §7 и 02-AOS.md.
    /// </summary>
    public enum Clarity
    {
        /// <summary>Ничего. Игра молчит и выглядит сломанной.</summary>
        Silent = 0,

        /// <summary>Значок намерения над головой — в момент решения.</summary>
        Icons = 1,

        /// <summary>Фраза при наведении: почему он так решил.</summary>
        Tooltips = 2,

        /// <summary>Журнал слева внизу: связный рассказ словами.</summary>
        Log = 3,

        /// <summary>Голоса модулей, веса, разрыв. Только разработчику.</summary>
        Trace = 4,
    }

    /// <summary>
    /// Отдельная вещь, которую можно показать или не показывать.
    ///
    /// Ступени <see cref="Clarity"/> остаются: это готовые наборы, между
    /// которыми игрок выбирает одним движением. Но ступень — лестница,
    /// а лестница вынуждает брать всё сразу: захотел причину под подписью
    /// — получи и журнал. Флажки разбирают набор на части, не отменяя
    /// набора.
    ///
    /// Порядок значений — порядок громкости, от самого тихого к самому
    /// шумному. По нему строятся ступени, и по нему же идут галочки
    /// в настройках: сверху то, что мешает меньше.
    /// </summary>
    [System.Flags]
    public enum Detail
    {
        None = 0,

        /// <summary>Значок намерения над головой.</summary>
        Icons = 1 << 0,

        /// <summary>Подпись одним словом и наезд камеры на поступок.</summary>
        Moments = 1 << 1,

        /// <summary>Фраза при наведении: почему он так решил.</summary>
        Tooltips = 1 << 2,

        /// <summary>Причина под подписью, вполсилы. Читается, не мешая.</summary>
        MomentCause = 1 << 3,

        /// <summary>Журнал слева внизу.</summary>
        Log = 1 << 4,

        /// <summary>Голоса, веса, разрыв. Цифры. Только разработчику.</summary>
        Trace = 1 << 5,
    }

    /// <summary>
    /// Кому что показывать.
    ///
    /// Правило без исключений: <b>игрок не видит цифр</b> (00-GDD.md §7).
    /// Оно держится не на аккуратности того, кто пишет строку, а на этой
    /// границе: цифры живут на четвёртой ступени, а четвёртая ступень
    /// игроку недоступна вообще — не «выключена по умолчанию», а заперта.
    /// Выключенное однажды включают по ошибке; запертое — нет.
    ///
    /// До сих пор лестница существовала только в документах. Ступени были
    /// написаны все четыре, но включены всегда и все сразу: трассировка
    /// с очками и весами сыпалась в консоль и в сборке для игрока тоже.
    ///
    /// Отсутствие настройки — событие, а не ноль. Без сохранённого выбора
    /// игрок получает <see cref="Default"/> — третью ступень, с журналом:
    /// игра, которая продаёт понятный отказ, не имеет права начинаться
    /// с непонятного.
    ///
    /// Правило намеренно без Unity — проверяется стендом.
    /// </summary>
    public static class Transparency
    {
        /// <summary>Что видит игрок, ничего не настраивавший.</summary>
        public const Clarity Default = Clarity.Log;

        /// <summary>Выше этого игроку нельзя ни при каких настройках.</summary>
        public const Clarity PlayerCeiling = Clarity.Log;

        private static Clarity _level = Default;
        private static bool _developer;

        /// <summary>
        /// Свой набор галочек. Пусто — значит игрок ничего не разбирал
        /// и смотрит готовую ступень.
        ///
        /// Отдельным полем, а не подменой ступени: игрок, повозившийся
        /// с галочками и передумавший, обязан вернуться туда же, откуда
        /// уходил. Ступень для этого должна пережить его опыты.
        /// </summary>
        private static Detail? _custom;

        /// <summary>Текущая ступень.</summary>
        public static Clarity Level => _level;

        /// <summary>Открыта ли четвёртая ступень. В сборке для игрока — нет.</summary>
        public static bool DeveloperUnlocked => _developer;

        /// <summary>
        /// Потолок, доступный сейчас. Игроку — третья ступень,
        /// разработчику — четвёртая.
        /// </summary>
        public static Clarity Ceiling => _developer ? Clarity.Trace : PlayerCeiling;

        /// <summary>
        /// Открыть или запереть трассировку. Запирая, опускаем и текущую
        /// ступень: иначе разработчик, выключивший себе доступ, продолжал
        /// бы видеть цифры — то есть замок не запирал бы ничего.
        /// </summary>
        public static void SetDeveloper(bool unlocked)
        {
            _developer = unlocked;
            if (_level > Ceiling) _level = Ceiling;
            if (_custom.HasValue) _custom = Clamp(_custom.Value);
        }

        /// <summary>
        /// Поставить ступень. Возвращает ту, что получилась: просьба выше
        /// потолка не отклоняется молча, а зажимается — и вызывающий видит,
        /// что получил не то, что просил.
        /// </summary>
        public static Clarity Set(Clarity wanted)
        {
            if (wanted < Clarity.Silent) wanted = Clarity.Silent;
            if (wanted > Ceiling) wanted = Ceiling;

            _level = wanted;

            // Выбрал готовую ступень — значит отказался от своих галочек.
            // Иначе ползунок двигался бы, а картинка не менялась.
            _custom = null;

            return _level;
        }

        /// <summary>Идёт ли сейчас свой набор, а не готовая ступень.</summary>
        public static bool IsCustom => _custom.HasValue;

        /// <summary>Что показывается прямо сейчас — набором флажков.</summary>
        public static Detail Shown => Clamp(_custom ?? Preset(_level));

        /// <summary>
        /// Набор, который даёт готовая ступень.
        ///
        /// Ступени накопительные: каждая добавляет к предыдущей, ничего
        /// не отнимая. Иначе «повысить прозрачность» могло бы что-то
        /// спрятать, и лестница перестала бы быть лестницей.
        /// </summary>
        public static Detail Preset(Clarity level)
        {
            switch (level)
            {
                case Clarity.Silent:
                    return Detail.None;

                // Намерения. Подпись поступка сюда же: она и есть
                // намерение, названное вслух, а не объяснение.
                case Clarity.Icons:
                    return Detail.Icons | Detail.Moments;

                // Причины. Подсказка при наведении — по требованию,
                // причина под подписью — сама, в момент поступка.
                case Clarity.Tooltips:
                    return Preset(Clarity.Icons) | Detail.Tooltips | Detail.MomentCause;

                case Clarity.Log:
                    return Preset(Clarity.Tooltips) | Detail.Log;

                case Clarity.Trace:
                    return Preset(Clarity.Log) | Detail.Trace;

                default:
                    return Detail.None;
            }
        }

        /// <summary>
        /// Срезать то, на что нет права. Замок на трассировке держится
        /// здесь, и потому его не обойти ни ступенью, ни галочкой.
        /// </summary>
        private static Detail Clamp(Detail wanted)
        {
            return _developer ? wanted : wanted & ~Detail.Trace;
        }

        /// <summary>
        /// Поставить свой набор. Возвращает тот, что получился:
        /// просьба показать цифры не отклоняется молча, а обрезается.
        /// </summary>
        public static Detail SetCustom(Detail wanted)
        {
            _custom = Clamp(wanted);
            return _custom.Value;
        }

        /// <summary>Одна галочка, не трогая остальные.</summary>
        public static Detail Toggle(Detail one, bool on)
        {
            var now = Shown;
            return SetCustom(on ? now | one : now & ~one);
        }

        /// <summary>Вернуться к готовой ступени, забыв галочки.</summary>
        public static void DropCustom() => _custom = null;

        /// <summary>Показывать ли это сейчас.</summary>
        public static bool Shows(Detail what)
        {
            if (what == Detail.None) return false;
            return (Shown & what) == what;
        }

        /// <summary>Показывать ли эту ступень сейчас.</summary>
        public static bool Shows(Clarity stage)
        {
            // Трассировка не показывается никогда, пока замок закрыт, —
            // даже если ступень каким-то путём оказалась выше потолка.
            if (stage >= Clarity.Trace && !_developer) return false;
            if (stage <= Clarity.Silent) return false;

            return Shows(Piece(stage));
        }

        /// <summary>Флажок, отвечающий за эту ступень.</summary>
        private static Detail Piece(Clarity stage)
        {
            switch (stage)
            {
                case Clarity.Icons:    return Detail.Icons;
                case Clarity.Tooltips: return Detail.Tooltips;
                case Clarity.Log:      return Detail.Log;
                case Clarity.Trace:    return Detail.Trace;
                default:               return Detail.None;
            }
        }

        /// <summary>Как назвать галочку игроку. Без цифр, как и всё здесь.</summary>
        public static string Describe(Detail what)
        {
            switch (what)
            {
                case Detail.Icons:       return "Значок над головой";
                case Detail.Moments:     return "Слово о поступке";
                case Detail.Tooltips:    return "Причина при наведении";
                case Detail.MomentCause: return "Причина под словом";
                case Detail.Log:         return "Журнал внизу";
                case Detail.Trace:       return "Внутренности — для разработчика";
                default:                 return "Ничего";
            }
        }

        /// <summary>Все галочки по порядку громкости. Для настроек и проверок.</summary>
        public static Detail[] Pieces()
        {
            return new[]
            {
                Detail.Icons, Detail.Moments, Detail.Tooltips,
                Detail.MomentCause, Detail.Log, Detail.Trace,
            };
        }

        /// <summary>
        /// Как назвать ступень игроку. Без цифр: «уровень 3» — это цифра,
        /// а цифр он не видит нигде, включая настройки.
        /// </summary>
        public static string Describe(Clarity level)
        {
            switch (level)
            {
                case Clarity.Silent:
                    return "Молча";
                case Clarity.Icons:
                    return "Только намерения";
                case Clarity.Tooltips:
                    return "Намерения и причины";
                case Clarity.Log:
                    return "Намерения, причины и рассказ";
                case Clarity.Trace:
                    return "Всё, включая внутренности — для разработчика";
                default:
                    return "Неизвестно";
            }
        }

        /// <summary>Вернуть всё как при первом запуске. Для проверок.</summary>
        public static void Reset()
        {
            _developer = false;
            _level = Default;
            _custom = null;
        }
    }
}
