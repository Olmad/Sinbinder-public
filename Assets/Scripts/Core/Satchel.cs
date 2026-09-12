// Assets/Scripts/Core/Satchel.cs
namespace Sinbinder.Core
{
    /// <summary>Что лежит в ячейке сумы.</summary>
    public enum CarryKind
    {
        None = 0,

        /// <summary>Банка. Пустая, если душа в ней не лежит.</summary>
        Jar = 1,

        /// <summary>Оболочка.</summary>
        Shell = 2,
    }

    /// <summary>
    /// Сума Греховода: шесть ячеек.
    ///
    /// Решение автора от 12 сентября. До неё руки были единственным
    /// вместилищем — одно за раз и больше нигде, — и «сколько душ можно
    /// унести» не имело ответа вовсе: жатва складывала их в общий список
    /// без предела.
    ///
    /// Теперь предел есть, и он физический: <b>душа живёт в банке</b>.
    /// Сколько банок в суме, столько душ и унесёшь. Пустая банка —
    /// это место под душу, а не пустая ячейка: ячейку можно занять
    /// и оболочкой.
    ///
    /// Руки остаются как были: одно за раз. Сума не отменяет их, а даёт
    /// куда переложить — иначе «принести душу к устройству» перестало бы
    /// быть ходьбой, ради которой связывание и вынималось из меню.
    ///
    /// Статика с <see cref="Forget"/>, как <c>CryptHands</c> и
    /// <c>SquadRoster</c>: Греховод один, а сцены меняются. Не забыть суму
    /// при новой игре значит начать её с чужим добром.
    ///
    /// Правило без Unity — проверяется стендом.
    /// </summary>
    public static class Satchel
    {
        /// <summary>Сколько ячеек. Шесть — пока что; автор сказал «там посмотрим».</summary>
        public const int Size = 6;

        /// <summary>
        /// Сколько пустых банок выдаётся на старте.
        ///
        /// Не шесть: предел должен чувствоваться с первой жатвы, иначе
        /// он не предел, а формальность. И не одна: одна банка означала бы
        /// «неси по одной», то есть ту же старую механику, только медленнее.
        /// </summary>
        public const int StartingJars = 3;

        public struct Slot
        {
            public CarryKind Kind;
            public SoulData Soul;
            public SoulQuality Quality;
            public ShellType Shell;

            public bool Empty => Kind == CarryKind.None;
            public bool EmptyJar => Kind == CarryKind.Jar && Soul == null;
            public bool FullJar => Kind == CarryKind.Jar && Soul != null;
        }

        private static readonly Slot[] _slots = new Slot[Size];
        private static bool _ready;
        private static int _selected;

        /// <summary>Какая ячейка сейчас под рукой.</summary>
        public static int Selected
        {
            get { Ensure(); return _selected; }
        }

        /// <summary>Перейти к следующей ячейке. По кругу.</summary>
        public static void Next()
        {
            Ensure();
            _selected = (_selected + 1) % Size;
        }

        public static Slot At(int index)
        {
            Ensure();
            return index >= 0 && index < Size ? _slots[index] : default;
        }

        /// <summary>Сколько банок при себе, пустых и полных.</summary>
        public static int Jars
        {
            get
            {
                Ensure();

                int n = 0;
                for (int i = 0; i < Size; i++)
                    if (_slots[i].Kind == CarryKind.Jar) n++;

                return n;
            }
        }

        /// <summary>
        /// Куда положить свежую душу: первая пустая банка.
        /// Минус один — банок при себе нет, и жать некуда.
        /// </summary>
        public static int FreeJar()
        {
            Ensure();

            for (int i = 0; i < Size; i++)
                if (_slots[i].EmptyJar) return i;

            return -1;
        }

        /// <summary>Налить душу в пустую банку. Ложь — банка не та или занята.</summary>
        public static bool Fill(int index, SoulData soul, SoulQuality quality)
        {
            Ensure();

            if (soul == null) return false;
            if (index < 0 || index >= Size) return false;
            if (!_slots[index].EmptyJar) return false;

            _slots[index].Soul = soul;
            _slots[index].Quality = quality;
            return true;
        }

        /// <summary>Положить в свободную ячейку. Ложь — места нет.</summary>
        public static bool Put(Slot what)
        {
            Ensure();

            if (what.Empty) return false;

            for (int i = 0; i < Size; i++)
            {
                if (!_slots[i].Empty) continue;

                _slots[i] = what;
                return true;
            }

            return false;
        }

        /// <summary>Положить именно в эту ячейку. Ложь — занята.</summary>
        public static bool PutAt(int index, Slot what)
        {
            Ensure();

            if (what.Empty) return false;
            if (index < 0 || index >= Size) return false;
            if (!_slots[index].Empty) return false;

            _slots[index] = what;
            return true;
        }

        /// <summary>Вынуть из ячейки. Пустая — вернётся пустая.</summary>
        public static Slot Take(int index)
        {
            Ensure();

            if (index < 0 || index >= Size) return default;

            var what = _slots[index];
            _slots[index] = default;
            return what;
        }

        /// <summary>Что лежит в ячейке — словами, для подсказок.</summary>
        public static string Describe(int index)
        {
            var slot = At(index);

            switch (slot.Kind)
            {
                case CarryKind.Jar:
                    return slot.Soul != null ? slot.Soul.Name : "пустая банка";
                case CarryKind.Shell:
                    return ShellWord(slot.Shell);
                default:
                    return "пусто";
            }
        }

        /// <summary>
        /// Новая игра: сума своя, не чужая.
        ///
        /// Заодно первое заполнение — сюда же, а не в статический
        /// инициализатор: так «забыть» и «завести» — одно место,
        /// и они не могут разойтись.
        /// </summary>
        public static void Forget()
        {
            for (int i = 0; i < Size; i++) _slots[i] = default;

            for (int i = 0; i < StartingJars && i < Size; i++)
                _slots[i] = new Slot { Kind = CarryKind.Jar };

            _selected = 0;
            _ready = true;
        }

        private static void Ensure()
        {
            if (!_ready) Forget();
        }

        /// <summary>
        /// Имя оболочки без обращения к Unity: сума проверяется стендом,
        /// а <c>ShellLibrary</c> лезет в <c>Resources</c>.
        /// </summary>
        private static string ShellWord(ShellType type)
        {
            switch (type)
            {
                case ShellType.Skeleton: return "тело: скелет";
                case ShellType.Zombie:   return "тело: зомби";
                case ShellType.Ghost:    return "тело: призрак";
                case ShellType.Golem:    return "тело: голем";
                default:                 return "тело";
            }
        }
    }
}
