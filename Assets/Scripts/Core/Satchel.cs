// Assets/Scripts/Core/Satchel.cs
// Перевод: текст через Loc
namespace Sinbinder.Core
{
    /// <summary>Что в ячейке сумы.</summary>
    public enum CarryKind
    {
        /// <summary>Ячейки нет: банок меньше, чем мест.</summary>
        None = 0,

        /// <summary>Банка. Пустая, если душа в ней не лежит.</summary>
        Jar = 1,
    }

    /// <summary>
    /// Сума Греховода: банки для душ.
    ///
    /// Решение автора от 12 сентября. До неё руки были единственным
    /// вместилищем — одно за раз и больше нигде, — и «сколько душ можно
    /// унести» не имело ответа вовсе: жатва складывала их в общий список
    /// без предела.
    ///
    /// Теперь предел есть, и он физический: <b>душа живёт в банке</b>.
    /// Сколько банок в суме, столько душ и унесёшь.
    ///
    /// <b>Ячейка — это банка</b> (автор, 19 октября: «может делать количество
    /// ячеек по количеству банок с максимумом 6?»). Прежде мест было шесть
    /// всегда, и рядом с тремя «пустая банка» стояли три «— Пусто —»: два
    /// слова для разного, которые на вид одно (`46-UI.md` §1.3). Теперь
    /// сколько банок — столько ячеек, и предел душ виден числом ячеек.
    ///
    /// Отсюда два правила. <b>Банка из сумы не уходит</b>: вынимают душу,
    /// пустая банка остаётся. Прежде душа уходила в руки вместе с банкой,
    /// а новых банок в игре нет — с ячейками по банкам сума сжималась бы
    /// после каждого связывания и после трёх исчезла бы. <b>Тело в суму
    /// не кладётся</b>: его носят в руках (тело в мешке и прежде было
    /// странно).
    ///
    /// Руки остаются как были: одно за раз. Сума не отменяет их, а даёт
    /// куда переложить — иначе «принести душу к устройству» перестало бы
    /// быть ходьбой, ради которой связывание и вынималось из меню.
    ///
    /// Статика с <see cref="Forget"/>, как <c>CryptHands</c> и
    /// <c>SquadRoster</c>: Греховод один, а сцены меняются. Не забыть суму
    /// при новой игре значит начать её с чужим добром.
    ///
    /// Правило без Unity — проверяется самопроверкой (<c>SatchelJars</c>).
    /// </summary>
    public static class Satchel
    {
        /// <summary>Больше шести банок сума не вмещает (автор, 19 октября).</summary>
        public const int Max = 6;

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

            public bool Empty => Kind == CarryKind.None;
            public bool EmptyJar => Kind == CarryKind.Jar && Soul == null;
            public bool FullJar => Kind == CarryKind.Jar && Soul != null;
        }

        private static readonly Slot[] _jars = new Slot[Max];
        private static int _count;
        private static bool _ready;
        private static int _selected;

        /// <summary>Сколько банок при себе, пустых и полных. Столько и ячеек.</summary>
        public static int Jars
        {
            get { Ensure(); return _count; }
        }

        /// <summary>Какая банка сейчас под рукой.</summary>
        public static int Selected
        {
            get { Ensure(); return _selected; }
        }

        /// <summary>Перейти к следующей банке. По кругу.</summary>
        public static void Next()
        {
            Ensure();
            _selected = _count > 0 ? (_selected + 1) % _count : 0;
        }

        /// <summary>Банка по месту. За последней банкой — ячейки нет (<see cref="Slot.Empty"/>).</summary>
        public static Slot At(int index)
        {
            Ensure();
            return index >= 0 && index < _count ? _jars[index] : default;
        }

        /// <summary>
        /// Есть ли в суме хоть что-нибудь. Спрашивает интерфейс: сума
        /// без банок — не сума, а рамка, которую игрок читает поломкой.
        /// </summary>
        public static bool Anything() => Jars > 0;

        /// <summary>
        /// Куда положить свежую душу: первая пустая банка.
        /// Минус один — пустых банок нет, и жать некуда.
        /// </summary>
        public static int FreeJar()
        {
            Ensure();

            for (int i = 0; i < _count; i++)
                if (_jars[i].EmptyJar) return i;

            return -1;
        }

        /// <summary>
        /// Куда налить душу из рук: банка под рукой, если пуста, иначе
        /// первая пустая. Минус один — пустых нет.
        /// </summary>
        public static int JarFor()
        {
            Ensure();
            return At(_selected).EmptyJar ? _selected : FreeJar();
        }

        /// <summary>Налить душу в пустую банку. Ложь — банки нет или она занята.</summary>
        public static bool Fill(int index, SoulData soul, SoulQuality quality)
        {
            Ensure();

            if (soul == null) return false;
            if (index < 0 || index >= _count) return false;
            if (!_jars[index].EmptyJar) return false;

            _jars[index].Soul = soul;
            _jars[index].Quality = quality;
            return true;
        }

        /// <summary>
        /// Вынуть душу из банки. Банка остаётся в суме пустой; вернётся то,
        /// что в ней было. Пустая или её нет — вернётся без души.
        /// </summary>
        public static Slot TakeSoul(int index)
        {
            Ensure();

            if (index < 0 || index >= _count) return default;

            var what = _jars[index];
            _jars[index] = new Slot { Kind = CarryKind.Jar };
            return what;
        }

        /// <summary>Что в банке — словами, для подсказок.</summary>
        public static string Describe(int index)
        {
            var slot = At(index);

            if (slot.FullJar) return slot.Soul.Name;
            return slot.EmptyJar ? Loc.T("пустая банка") : "";
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
            for (int i = 0; i < Max; i++) _jars[i] = default;

            _count = System.Math.Min(StartingJars, Max);
            for (int i = 0; i < _count; i++)
                _jars[i] = new Slot { Kind = CarryKind.Jar };

            _selected = 0;
            _ready = true;
        }

        private static void Ensure()
        {
            if (!_ready) Forget();
        }
    }
}
