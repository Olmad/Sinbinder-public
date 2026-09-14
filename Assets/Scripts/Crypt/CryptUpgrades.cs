namespace Sinbinder.Crypt
{
    /// <summary>Что можно поставить в склепе.</summary>
    public enum Upgrade
    {
        /// <summary>Ледник: собранные души держатся дольше.</summary>
        Cellar = 0,

        /// <summary>Казна: с вылазок несут золото, и отряду есть чем заплатить.</summary>
        Treasury = 1,

        /// <summary>
        /// Каменные оковы: устройство связывания выдерживает голема.
        ///
        /// Слово автора от 13 сентября: «големов в лагере быть не должно —
        /// их можно получить только с улучшением устройства для связывания
        /// душ». До этого голем стоял на столе тел наравне с остальными
        /// и выбирался с первой души.
        /// </summary>
        Shackles = 2,
    }

    /// <summary>
    /// Улучшения склепа.
    ///
    /// Их два, и оба выбраны не за пользу вообще, а за то, что каждое
    /// берётся за <b>свой конец главной механики</b>:
    ///
    /// <list type="bullet">
    /// <item>Ледник продлевает жизнь собранной души — значит расширяет
    /// выбор тела, потому что тяжёлое тело истлевшую душу не примет
    /// (<see cref="Sinbinder.Core.ShellChoice"/>). Улучшение, которое
    /// возвращает игроку решение, а не прибавляет число.</item>
    /// <item>Казна даёт чем заплатить — то есть включает единственный
    /// рычаг, которым игрок может убрать причину отказа заранее.
    /// Долг измерен (<c>12-BALANCE.md</c>): без уплаты отказы у должника
    /// доходят до трёх четвертей.</item>
    /// </list>
    ///
    /// Мета-прогрессия склепа отложена целиком (<c>05-BOUNDS.md</c>),
    /// и это исключение сделано намеренно: два улучшения — не система
    /// строительства, а два рычага, которых игроку не хватало.
    ///
    /// Правило без Unity — проверяется стендом.
    /// </summary>
    public static class CryptUpgrades
    {
        private static readonly bool[] _installed = new bool[3];
        private static readonly bool[] _brought = new bool[3];

        public const int Limit = 3;

        /// <summary>
        /// Можно ли связать душу с этим телом при нынешних улучшениях.
        /// Голем — только с оковами; остальные — всегда.
        /// </summary>
        public static bool AllowsShell(Sinbinder.Core.ShellType shell)
            => shell != Sinbinder.Core.ShellType.Golem || Installed(Upgrade.Shackles);

        /// <summary>Почему нельзя — словами, для журнала и устройства.</summary>
        public static string WhyNot(Sinbinder.Core.ShellType shell)
            => AllowsShell(shell) ? "" : "Устройство не выдержит камня. Нужны каменные оковы.";

        /// <summary>Поставлено ли.</summary>
        public static bool Installed(Upgrade what) => _installed[(int)what];

        /// <summary>Принесено с вылазки, но ещё не поставлено.</summary>
        public static bool Brought(Upgrade what)
            => _brought[(int)what] && !_installed[(int)what];

        /// <summary>Есть ли хоть что-то, ждущее гнезда.</summary>
        public static bool AnyBrought
        {
            get
            {
                for (int i = 0; i < _brought.Length; i++)
                    if (_brought[i] && !_installed[i]) return true;

                return false;
            }
        }

        /// <summary>Первое из принесённого. Гнездо берёт то, что лежит.</summary>
        public static Upgrade FirstBrought()
        {
            for (int i = 0; i < _brought.Length; i++)
                if (_brought[i] && !_installed[i]) return (Upgrade)i;

            return Upgrade.Cellar;
        }

        /// <summary>
        /// Принести с вылазки. Возвращает, что именно принесли, или
        /// <c>false</c>, если нести уже нечего: второго ледника не бывает.
        /// </summary>
        public static bool Bring(out Upgrade what)
        {
            for (int i = 0; i < _brought.Length; i++)
            {
                if (_brought[i]) continue;

                _brought[i] = true;
                what = (Upgrade)i;
                return true;
            }

            what = Upgrade.Cellar;
            return false;
        }

        public static bool Install(Upgrade what)
        {
            if (!Brought(what)) return false;

            _installed[(int)what] = true;
            return true;
        }

        public static string Name(Upgrade what)
        {
            switch (what)
            {
                case Upgrade.Cellar:   return "Ледник";
                case Upgrade.Treasury: return "Казна";
                default:               return "Каменные оковы";
            }
        }

        /// <summary>Что оно делает — словами и без цифр.</summary>
        public static string Does(Upgrade what)
        {
            switch (what)
            {
                case Upgrade.Cellar:
                    return "Собранные души держатся дольше. Значит и тел им доступно больше.";
                case Upgrade.Treasury:
                    return "С вылазок несут золото. Значит отряду есть чем заплатить.";
                default:
                    return "Устройство выдерживает камень. Значит душу можно вселить в голема.";
            }
        }

        /// <summary>
        /// Во сколько раз ледник продлевает жизнь души. Читает
        /// <c>SoulManager</c>: срок остаётся его, а множитель — здешний.
        /// </summary>
        public static float FadeMultiplier => Installed(Upgrade.Cellar) ? 2f : 1f;

        /// <summary>Забыть при новой игре.</summary>
        /// <summary>Что стоит и что принесено — копией, для сохранения.</summary>
        public static bool[] InstalledAll() => (bool[])_installed.Clone();

        /// <summary>То же про принесённое, но не поставленное.</summary>
        public static bool[] BroughtAll() => (bool[])_brought.Clone();

        /// <summary>
        /// Восстановить из сохранения. Короткий массив не ошибка,
        /// а старый файл: чего в нём нет, того не было.
        /// </summary>
        public static void Restore(bool[] installed, bool[] brought)
        {
            Forget();

            for (int i = 0; i < _installed.Length; i++)
            {
                if (installed != null && i < installed.Length) _installed[i] = installed[i];
                if (brought != null && i < brought.Length) _brought[i] = brought[i];
            }
        }

        public static void Forget()
        {
            for (int i = 0; i < _installed.Length; i++)
            {
                _installed[i] = false;
                _brought[i] = false;
            }
        }
    }
}
