// Assets/Scripts/Core/WarriorGenerator.cs
// Перевод: текст через Loc
using System.Collections.Generic;

namespace Sinbinder.Core
{
    /// <summary>Откуда душа (docs/53-GENERATOR.md §11): из этого выводится всё остальное.</summary>
    public enum SoulOrigin
    {
        /// <summary>Пал в бою, где были мы, — помнит, кто его убил.</summary>
        Harvest,
        /// <summary>Отдал душу сам, ещё живым, — помнит цену и условие.</summary>
        Pact,
        /// <summary>Собран давно и не нами — банка с полки.</summary>
        Jar,
        /// <summary>Принесён с вылазки — ремесло по месту.</summary>
        Sortie,
    }

    /// <summary>Человек, которого сложил генератор. Воина из него ставит полигон.</summary>
    public sealed class Sketch
    {
        public int Number;
        public SoulOrigin Origin;
        public string Given;
        public string Nickname;
        public string Name => string.IsNullOrEmpty(Nickname) ? Given : Given + " " + Nickname;
        public SinType Sin, Second;
        public float Strength;
        public MoralType Moral;
        public Trade Trade;
        public ShellType Shell;
        /// <summary>Почему так зовут, первая фраза, гибель — уже на языке игры.</summary>
        public string Why, First, Death;
        /// <summary>История: как умер и как душа попала к нам.</summary>
        public MemorySeed Seed;
    }

    /// <summary>
    /// Генератор воинов, шаг первый (docs/53-GENERATOR.md). Автор, 27 октября:
    /// «Самое главное — генерация воинов, не уступающих качеству и уникальности тем,
    /// что есть сейчас».
    ///
    /// Человек складывается цепочкой, где каждое выводится из прежнего (§2), —
    /// а не броском костей на каждый слой: так выходит набор, а не человек.
    /// Откуда душа → ремесло и тело → главный и второй грех (наклон ремесла) →
    /// мораль → примета из каталога (написана руками, с условием) → имя → история.
    ///
    /// <b>Без жребия.</b> Номер и происхождение сворачиваются в одну цепочку чисел:
    /// одинаковый номер — один и тот же человек, на любой машине.
    ///
    /// <b>Чего здесь нет — нарочно.</b> Женщин (правило <c>unique_women</c>) и легенд:
    /// легенды, как и женщины, — лица, а не порода (§4). Особенностей (§10) — пока:
    /// у них ещё нет рычагов в движке, а строка, которой не отвечает поведение,
    /// учит игрока не верить строкам (§0, п. 3).
    /// </summary>
    public static class WarriorGenerator
    {
        /// <summary>Имена нашей стороны — короткие, северные и славянские (автор, 27.10: «да»).</summary>
        public static readonly string[] Names =
        {
            Loc.N("Бран"), Loc.N("Ольст"), Loc.N("Тихон"), Loc.N("Ратко"), Loc.N("Сбыслав"), Loc.N("Ивар"),
            Loc.N("Мирош"), Loc.N("Гостята"), Loc.N("Ульв"), Loc.N("Добрыня"), Loc.N("Хотен"), Loc.N("Свен"),
            Loc.N("Будимир"), Loc.N("Кнут"), Loc.N("Ярош"), Loc.N("Нежата"), Loc.N("Торн"), Loc.N("Вышата"),
            Loc.N("Ерш"), Loc.N("Олаф"), Loc.N("Житко"), Loc.N("Ставр"), Loc.N("Хёгни"), Loc.N("Путята"),
        };

        /// <summary>Охотники Ордена по имени — цель мести у тех, кто отдал душу за месть.</summary>
        private static readonly string[] OrderNames =
        {
            Loc.N("Ульрих"), Loc.N("Конрад"), Loc.N("Дитрих"), Loc.N("Готфрид"), Loc.N("Рупрехт"), Loc.N("Вильгельм"),
        };

        private static readonly Trade[] Ours = { Trade.Hunter, Trade.Peasant, Trade.Archer, Trade.Alchemist, Trade.Mage };
        private static readonly Trade[] Ranks = { Trade.OrderHunter, Trade.Tracker, Trade.Inquisitor, Trade.Catcher };

        // ──────────────────────────────────
        // Свёртка номера
        // ──────────────────────────────────

        /// <summary>
        /// Цепочка чисел от номера и происхождения: FNV-1a, потом сдвиги. Своя,
        /// а не <c>GetHashCode</c> и не жребий — как <c>Wardrobe.Stamp</c>.
        /// </summary>
        private struct Chain
        {
            private uint _s;

            public Chain(int number, int salt)
            {
                _s = 2166136261u;
                Mix(number);
                Mix(salt);
                if (_s == 0) _s = 1;
            }

            private void Mix(int v)
            {
                unchecked
                {
                    for (int i = 0; i < 4; i++) { _s ^= (uint)((v >> (8 * i)) & 0xFF); _s *= 16777619u; }
                }
            }

            /// <summary>Следующее число от 0 до n − 1.</summary>
            public int Next(int n)
            {
                unchecked
                {
                    _s ^= _s << 13;
                    _s ^= _s >> 17;
                    _s ^= _s << 5;
                    return n <= 1 ? 0 : (int)(_s % (uint)n);
                }
            }
        }

        // ──────────────────────────────────
        // Каталог примет (§3) — написан руками; генератор выбирает, а не сочиняет
        // ──────────────────────────────────

        private sealed class Mark
        {
            public string Nick;
            public System.Func<Sketch, bool> Fits;
            public string Why, First, Death;
        }

        private static readonly Mark[] Marks =
        {
            new() { Nick = Loc.N("Силок"),
                    Fits = k => k.Sin == SinType.Greed && (k.Trade == Trade.Hunter || k.Trade == Trade.Peasant || k.Trade == Trade.Catcher),
                    Why = Loc.N("Ставит силки на краю лагеря и всегда что-нибудь приносит. Принесённое делит не со всеми."),
                    First = Loc.N("Заяц сегодня был, владыка. Был."),
                    Death = Loc.N("Силки на краю лагеря так и стоят.") },
            new() { Nick = Loc.N("Щипач"),
                    Fits = k => k.Sin == SinType.Greed && k.Moral == MoralType.Vicious,
                    Why = Loc.N("Шарит по чужим палаткам ночью. Говорит, что ищет своё."),
                    First = Loc.N("Это не ваше, владыка? Тогда и не моё."),
                    Death = Loc.N("В палатках никто больше не шарит. Непривычно тихо.") },
            new() { Nick = Loc.N("Скопидом"),
                    Fits = k => k.Sin == SinType.Greed,
                    Why = Loc.N("Копит всё, что блестит, в узелке под рубахой."),
                    First = Loc.N("Своё не отдам, владыка. А чужое — посмотрим."),
                    Death = Loc.N("Узелок под рубахой так никто и не развязал.") },
            new() { Nick = Loc.N("Звездочёт"),
                    Fits = k => k.Sin == SinType.Pride && (k.Trade == Trade.Mage || k.Trade == Trade.Novice || k.Trade == Trade.Alchemist),
                    Why = Loc.N("Ночами стоит и смотрит на звёзды. По ним знает, куда идти — и куда не идти."),
                    First = Loc.N("Звёзды сегодня против, владыка. Но вы ведь всё равно пойдёте."),
                    Death = Loc.N("Звёзды на месте. Смотреть на них некому.") },
            new() { Nick = Loc.N("Чистюля"),
                    Fits = k => k.Sin == SinType.Pride,
                    Why = Loc.N("Чистит оружие до блеска и никому не даёт его тронуть."),
                    First = Loc.N("Руки, владыка. Не трогайте клинок."),
                    Death = Loc.N("Его клинок так и блестит. Чистить его больше некому.") },
            new() { Nick = Loc.N("Кремень"),
                    Fits = k => k.Trade == Trade.Quarryman,
                    Why = Loc.N("Каменотёс. Говорит мало, бьёт тяжело."),
                    First = Loc.N("Камень не спорит, владыка. Я тоже."),
                    Death = Loc.N("Кремень раскололся.") },
            new() { Nick = Loc.N("Задира"),
                    Fits = k => k.Sin == SinType.Wrath && k.Moral == MoralType.Vicious,
                    Why = Loc.N("Ищет, с кем подраться у огня. Находит всегда."),
                    First = Loc.N("А вы, владыка, драться умеете? Нет? Жаль."),
                    Death = Loc.N("У огня тихо. Подраться не с кем.") },
            new() { Nick = Loc.N("Бешеный"),
                    Fits = k => k.Sin == SinType.Wrath,
                    Why = Loc.N("Рубит пни и точит нож, пока другие спят. Злость надо куда-то девать."),
                    First = Loc.N("Кого бить, владыка? Только скажите — кого."),
                    Death = Loc.N("Пни на краю лагеря стоят целыми.") },
            new() { Nick = Loc.N("Глазастый"),
                    Fits = k => k.Sin == SinType.Envy,
                    Why = Loc.N("Смотрит, кому что дали, и помнит, кому — больше."),
                    First = Loc.N("Другим вы давали, владыка. Я видел."),
                    Death = Loc.N("Никто больше не считает, кому что досталось.") },
            new() { Nick = Loc.N("Певун"),
                    Fits = k => k.Sin == SinType.Lust,
                    Why = Loc.N("Поёт у огня и тянет за собой всех, кто рядом."),
                    First = Loc.N("Споёте с нами, владыка? Нет? Тогда я за вас."),
                    Death = Loc.N("У огня никто не поёт.") },
            new() { Nick = Loc.N("Сторож"),
                    Fits = k => k.Sin == SinType.Gluttony && k.Shell == ShellType.Zombie,
                    Why = Loc.N("Не отходит от котла. Говорит, что сторожит."),
                    First = Loc.N("Похлёбка ещё не готова, владыка. Я проверяю."),
                    Death = Loc.N("Котёл никто не сторожит.") },
            // Автор, 27.10: еда нужна и чревоугодникам в любом теле — скелет ест всё равно.
            new() { Nick = Loc.N("Решето"),
                    Fits = k => k.Sin == SinType.Gluttony && k.Shell == ShellType.Skeleton,
                    Why = Loc.N("Ест, хотя еда проваливается сквозь рёбра. Голод — не в желудке."),
                    First = Loc.N("Не смотрите, владыка. Я всё равно голоден."),
                    Death = Loc.N("Его миска стоит у котла пустая.") },
            new() { Nick = Loc.N("Голодный"),
                    Fits = k => k.Sin == SinType.Gluttony,
                    Why = Loc.N("Призрак, а всё голоден. Стоит у котла и смотрит."),
                    First = Loc.N("Пахнет, владыка. Хоть запах мне оставьте."),
                    Death = Loc.N("У котла больше никто не стоит.") },
            new() { Nick = Loc.N("Сонный"),
                    Fits = k => k.Sin == SinType.Sloth && (k.Trade == Trade.Peasant || k.Trade == Trade.Carter),
                    Why = Loc.N("Спит где придётся, днём и ночью. Просыпается не сразу."),
                    First = Loc.N("А? Уже? Я не спал, владыка."),
                    Death = Loc.N("Он спит. На этот раз — совсем.") },
            new() { Nick = Loc.N("Лежебока"),
                    Fits = k => k.Sin == SinType.Sloth,
                    Why = Loc.N("Лежит, пока можно лежать, — а можно, по его словам, всегда."),
                    First = Loc.N("Успеется, владыка."),
                    Death = Loc.N("Лежебока лёг надолго.") },
            new() { Nick = Loc.N("Молельник"),
                    Fits = k => k.Moral == MoralType.Pious,
                    Why = Loc.N("Молится на краю лагеря на рассвете. Кому — не говорит."),
                    First = Loc.N("Я помолюсь и за вас, владыка. Хоть вы и не просили."),
                    Death = Loc.N("На рассвете на краю лагеря никого.") },
            new() { Nick = Loc.N("Бывший"),
                    Fits = k => k.Origin == SoulOrigin.Harvest,
                    Why = Loc.N("Был охотником Ордена. Вздрагивает от колокола и чистит доспех, которого у него больше нет."),
                    First = Loc.N("Я вас ловил, владыка. Теперь — вот."),
                    Death = Loc.N("Колокол больше никого не пугает.") },
            new() { Nick = Loc.N("Вёрстка"),
                    Fits = k => k.Trade == Trade.Carter,
                    Why = Loc.N("Возил обозы, пока обоз не остановили. Всё ещё считает вёрсты."),
                    First = Loc.N("До места далеко, владыка? Я посчитаю."),
                    Death = Loc.N("Вёрсты больше никто не считает.") },
        };

        // ──────────────────────────────────
        // Сборка
        // ──────────────────────────────────

        /// <summary>
        /// Сложить человека. <paramref name="present"/> — имена тех, кто уже есть
        /// (без титулов): имя и прозвище не повторяются, а убийцу и врага генератор
        /// берёт из живых (§11).
        /// </summary>
        public static Sketch Make(int number, SoulOrigin origin, IList<string> present)
        {
            present ??= new List<string>();
            var chain = new Chain(number, (int)origin);
            var k = new Sketch { Number = number, Origin = origin };

            // 1. Откуда душа — ремесло, тело и место.
            string place = "";
            switch (origin)
            {
                case SoulOrigin.Sortie:
                    int p = chain.Next(3);
                    k.Trade = p == 0 ? Trade.Carter : p == 1 ? Trade.Novice : Trade.Quarryman;
                    place = p == 0 ? Loc.T("на обозе") : p == 1 ? Loc.T("в часовне") : Loc.T("в каменоломне");
                    k.Shell = chain.Next(2) == 0 ? ShellType.Zombie : ShellType.Skeleton;
                    break;
                case SoulOrigin.Harvest:
                    k.Trade = Ranks[chain.Next(Ranks.Length)];
                    k.Shell = chain.Next(3) == 0 ? ShellType.Skeleton : ShellType.Zombie;   // тело свежее
                    break;
                case SoulOrigin.Pact:
                    k.Trade = Ours[chain.Next(Ours.Length)];
                    k.Shell = chain.Next(3) == 0 ? ShellType.Ghost : ShellType.Skeleton;
                    break;
                default:
                    k.Trade = Ours[chain.Next(Ours.Length)];
                    k.Shell = chain.Next(2) == 0 ? ShellType.Ghost : ShellType.Skeleton;   // банка старая
                    break;
            }

            // 2. Грехи: наклон ремесла и номер. Второй — следующий по силе: с ним он спорит.
            var score = new float[SoulData.SpectrumCount];
            for (int i = 0; i < score.Length; i++) score[i] = Trades.Pull(k.Trade, (SinType)i) + chain.Next(40);
            int first = 0, second = 1;
            for (int i = 0; i < score.Length; i++) if (score[i] > score[first]) first = i;
            if (second == first) second = 0;
            for (int i = 0; i < score.Length; i++) if (i != first && score[i] > score[second]) second = i;
            k.Sin = (SinType)first;
            k.Second = (SinType)second;
            k.Strength = 40f + chain.Next(46);

            // 3. Мораль — из ремесла и греха.
            if (k.Trade == Trade.Novice || k.Trade == Trade.Inquisitor)
                k.Moral = chain.Next(3) == 0 ? MoralType.Neutral : MoralType.Pious;
            else if (k.Sin == SinType.Wrath || k.Sin == SinType.Greed || k.Sin == SinType.Lust)
                k.Moral = chain.Next(2) == 0 ? MoralType.Vicious : MoralType.Neutral;
            else
                k.Moral = (MoralType)chain.Next(3);

            // 4. Примета — из подходящих, по номеру; прозвище не повторяется.
            var fits = new List<Mark>();
            foreach (var m in Marks)
                if (m.Fits(k) && !Taken(present, m.Nick)) fits.Add(m);
            Mark mark = fits.Count > 0 ? fits[chain.Next(fits.Count)] : null;

            // 5. Имя — из пула; занятые пропускаем. Имена внутри игры — ключами
            // на русском, как у девяти лагеря; на экран их выводит Loc.Name.
            int start = chain.Next(Names.Length);
            k.Given = Names[start];
            for (int i = 0; i < Names.Length; i++)
            {
                var name = Names[(start + i) % Names.Length];
                if (!Taken(present, name)) { k.Given = name; break; }
            }

            if (mark != null)
            {
                k.Nickname = mark.Nick;
                k.Why = Loc.T(mark.Why);
                k.First = Loc.T(mark.First);
                k.Death = Loc.T(mark.Death);
            }

            // 6. История — как умер и как душа попала к нам (§11).
            k.Seed = Story(k, ref chain, place, present);
            return k;
        }

        private static MemorySeed Story(Sketch k, ref Chain chain, string place, IList<string> present)
        {
            switch (k.Origin)
            {
                case SoulOrigin.Harvest:
                {
                    // Убийца — из живых, а если никого нет — Греховод.
                    string killer = present.Count > 0 ? present[chain.Next(present.Count)] : Loc.T("Греховод");
                    string feeling = k.Sin == SinType.Wrath ? Loc.T("Ненависть")
                                   : k.Sin == SinType.Pride ? Loc.T("Уважение")
                                   : k.Sin == SinType.Sloth ? Loc.T("Страх")
                                   : Loc.T("Обида");
                    return new MemorySeed
                    {
                        Object = killer,
                        Emotion = feeling,
                        Trigger = killer,
                        Story = Loc.F("Служил Ордену. Его убил {0} — и душу собрали на том же поле.", Loc.Name(killer)),
                    };
                }
                case SoulOrigin.Pact:
                {
                    int deal = chain.Next(4);
                    string foe = OrderNames[chain.Next(OrderNames.Length)];
                    return deal switch
                    {
                        0 => new MemorySeed { Object = Loc.T("карты"), Emotion = Loc.T("Азарт"), Trigger = Loc.T("карты"),
                                              Story = Loc.T("Отдал душу за карточный долг, ещё живым. Надеется отыграться.") },
                        1 => new MemorySeed { Object = foe, Emotion = Loc.T("Ненависть"), Trigger = foe,
                                              Story = Loc.F("Отдал душу за месть: {0} из Ордена должен умереть.", Loc.Name(foe)) },
                        2 => new MemorySeed { Object = Loc.T("сын"), Emotion = Loc.T("Тоска"), Trigger = Loc.T("сын"),
                                              Story = Loc.T("Отдал душу за жизнь сына. Не знает, жив ли тот.") },
                        _ => new MemorySeed { Object = Loc.T("сила"), Emotion = Loc.T("Гордость"), Trigger = Loc.T("признание"),
                                              Story = Loc.T("Отдал душу за силу — и хочет, чтобы все видели: не зря.") },
                    };
                }
                case SoulOrigin.Jar:
                    return new MemorySeed
                    {
                        Object = Loc.T("банка"),
                        Emotion = Loc.T("Память"),
                        Trigger = Loc.T("прежний хозяин"),
                        Story = Loc.T("Его собрали давно и не здесь. Банка была старой; он помнит другого хозяина и говорит по-старому."),
                    };
                default:
                    return new MemorySeed
                    {
                        Object = place,
                        Emotion = Loc.T("Последний час"),
                        Trigger = place,
                        Story = Loc.F("Умер {0} — там его и нашли, и принесли.", place),
                    };
            }
        }

        private static bool Taken(IList<string> present, string word)
        {
            if (string.IsNullOrEmpty(word)) return false;
            foreach (var p in present)
            {
                if (string.IsNullOrEmpty(p)) continue;
                if (p == word || p.StartsWith(word + " ") || p.EndsWith(" " + word) || p.Contains(" " + word + " ")) return true;
            }
            return false;
        }
    }
}
