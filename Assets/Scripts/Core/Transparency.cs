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
            return _level;
        }

        /// <summary>Показывать ли эту ступень сейчас.</summary>
        public static bool Shows(Clarity stage)
        {
            // Трассировка не показывается никогда, пока замок закрыт, —
            // даже если ступень каким-то путём оказалась выше потолка.
            if (stage >= Clarity.Trace && !_developer) return false;

            return stage <= _level && stage > Clarity.Silent;
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
        }
    }
}
