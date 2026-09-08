namespace Sinbinder.Core
{
    /// <summary>
    /// Какие оболочки готовы принять эту душу.
    ///
    /// До сих пор выбора не было вовсе: <c>SoulBinding</c> держал зашитый
    /// <c>ShellType.Zombie</c>, хотя четыре оболочки собраны как ассеты,
    /// смещение спектров у каждой настоящее, а необратимый дрейф
    /// (<see cref="ShellBinder"/>) работает. Система была собрана целиком
    /// и пользовалась одной своей четвертью.
    ///
    /// <b>Правило: оболочка требует воли.</b> Чем сильнее плоть тянет
    /// душу на себя (<c>bindStrength</c>), тем больше души нужно, чтобы
    /// в ней остаться собой. У истлевшей души воли не осталось — сильное
    /// тело перепишет её целиком, и подниматься будет уже некому.
    ///
    /// Отсюда выбор оболочки перестаёт быть меню и становится следствием
    /// того, насколько игрок торопился. Урок сцены 4 пролога — «спеши» —
    /// был до сих пор словом рассказчика; теперь это механика:
    /// свежая душа принимает любое тело, истлевшая — только то,
    /// которое почти ничего ей не навязывает.
    ///
    /// Правило намеренно без Unity и без ScriptableObject: его проверяет
    /// стенд (Tools/bench, «ОБОЛОЧКИ»).
    /// </summary>
    public static class ShellChoice
    {
        /// <summary>
        /// Сколько чужой воли душа этого качества ещё выдержит.
        ///
        /// Числа те же по смыслу, что у <see cref="SoulDecay"/>: там
        /// качество решает, насколько тускнеют спектры, здесь — насколько
        /// тяжёлое тело душа удержит. Одна шкала, два следствия.
        /// </summary>
        public static float Endures(SoulQuality quality)
        {
            switch (quality)
            {
                case SoulQuality.Shock:      return 1.00f;  // выдержит что угодно
                case SoulQuality.Acceptance: return 0.40f;  // всё, кроме камня
                case SoulQuality.Fading:     return 0.30f;  // только лёгкое
                case SoulQuality.Dissolved:  return 0.20f;  // почти ничего
                default:                     return 1.00f;
            }
        }

        /// <summary>Примет ли эта оболочка душу такого качества.</summary>
        public static bool Allows(ShellData shell, SoulQuality quality)
        {
            if (shell == null) return false;

            // Тело, которое ничего не навязывает, возьмёт кого угодно.
            if (shell.bindStrength <= 0f) return true;

            return shell.bindStrength <= Endures(quality) + 0.0001f;
        }

        /// <summary>
        /// Почему не примет — словами и без чисел. Игрок обязан понимать
        /// отказ, иначе выбор выглядит произволом интерфейса.
        /// </summary>
        public static string Refusal(ShellData shell, SoulQuality quality)
        {
            if (shell == null) return "Такого тела нет.";
            if (Allows(shell, quality)) return "";

            switch (quality)
            {
                case SoulQuality.Dissolved:
                    return "От неё осталась одна воля. Это тело её сотрёт.";
                case SoulQuality.Fading:
                    return "Она уже тускнеет. Такое тело перепишет её под себя.";
                default:
                    return "Ей не хватит себя, чтобы остаться собой в этом теле.";
            }
        }

        /// <summary>
        /// Что тело сделает с этой душой — словами. Для экрана выбора:
        /// игрок решает, а не угадывает.
        ///
        /// Считает не своей формулой, а <see cref="ShellBinder"/> на копии
        /// души: экран обязан показывать то, что случится на самом деле.
        /// Своя формула здесь была бы второй правдой и разошлась бы
        /// с игрой при первой же правке смещений.
        /// </summary>
        public static SoulData Preview(SoulData soul, ShellData shell)
        {
            if (soul == null) return null;

            var copy = new SoulData(soul);
            ShellBinder.Bind(copy, shell);
            return copy;
        }
    }
}
