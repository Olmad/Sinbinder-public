namespace Sinbinder.Crypt
{
    /// <summary>Что можно принести с вылазки, кроме выживших и денег.</summary>
    public enum Spoils
    {
        /// <summary>Ничего, кроме людей. Тоже исход.</summary>
        None = 0,

        /// <summary>Души в сосудах — новые банки на полке.</summary>
        Souls = 1,

        /// <summary>Тело: оболочка, которой в склепе не было.</summary>
        Shell = 2,

        /// <summary>Улучшение склепа. Их всего два, и оба ставятся в зону гнёзд.</summary>
        Upgrade = 3,
    }

    /// <summary>
    /// Что там везут или хранят — деньгами.
    ///
    /// Отдельно от охраны намеренно. Пока добыча считалась как
    /// «врагов × монету», богатство и опасность были одной осью:
    /// сказать «слабо охраняется, но везёт серебро» было нечем,
    /// а это половина всех решений игрока. И экономика от этого
    /// не сходилась — каждая вылазка была убыточной.
    /// </summary>
    public enum Prize
    {
        /// <summary>Денег там нет.</summary>
        None = 0,

        /// <summary>Кошели у пояса. Немного.</summary>
        Purses = 1,

        /// <summary>Обозное серебро. Ради такого и ходят.</summary>
        Silver = 2,
    }

    /// <summary>
    /// Чем это является как поступок.
    ///
    /// Третье поле миссии и единственное, ради которого стоило менять
    /// форму: его читают модули личности и оно ложится в память. Набег
    /// на вооружённых, грабёж беззащитных и разрытие могил — три разных
    /// дела, и семь модулей имеют о них разное мнение. Благочестивый
    /// пойдёт на заставу и упрётся у обоза.
    /// </summary>
    public enum Deed
    {
        /// <summary>Набег на тех, кто дерётся в ответ.</summary>
        ArmedRaid = 0,

        /// <summary>Грабёж тех, кто не дерётся.</summary>
        Robbery = 1,

        /// <summary>Разрытие могил.</summary>
        GraveRobbing = 2,
    }

    /// <summary>
    /// Какая развилка ждёт на этой точке.
    ///
    /// Перечисление, а не флажок: развилок будет столько же, сколько
    /// точек, и каждая своя. В демо подключена одна — обоз. Остальные
    /// описаны в docs/19-MISSIONS.md и лежат в Crypt/Junctions.cs.later.
    /// </summary>
    public enum Junction
    {
        /// <summary>Вылазка проходит без вопросов.</summary>
        None = 0,

        /// <summary>Обоз остановлен. Возчики не дерутся.</summary>
        Caravan = 1,
    }

    /// <summary>Одна точка на карте шара.</summary>
    public readonly struct Mission
    {
        public readonly string Name;
        public readonly string Rumour;
        public readonly int Squad;

        /// <summary>Сколько там дерётся. Это опасность, а не богатство.</summary>
        public readonly int Guards;

        /// <summary>Сколько там денег. Это богатство, а не опасность.</summary>
        public readonly Prize Prize;

        public readonly Spoils Spoils;
        public readonly Deed Deed;
        public readonly Junction Junction;

        public Mission(string name, string rumour, int squad, int guards,
                       Prize prize, Spoils spoils, Deed deed,
                       Junction junction = Junction.None)
        {
            Name = name;
            Rumour = rumour;
            Squad = squad;
            Guards = guards;
            Prize = prize;
            Spoils = spoils;
            Deed = deed;
            Junction = junction;
        }
    }

    /// <summary>
    /// Карта в хрустальном шаре.
    ///
    /// Миссии — данные, а не сцены. Сцену пришлось бы ставить, освещать
    /// и вести, а данные складываются в список и решаются движком:
    /// <see cref="Sinbinder.Gameplay.Expedition"/> проводит настоящий бой
    /// без единой модели. Отсюда и цена: карта на десяток точек стоит
    /// столько же, сколько на одну.
    ///
    /// <b>Ни одной цифры в тексте.</b> Сколько нужно людей, насколько там
    /// опасно и что оттуда несут, игрок читает словами. Числа остаются
    /// внутри и в глаза не попадают.
    ///
    /// Замысел и развилки — docs/19-MISSIONS.md. Таблица без Unity —
    /// её проверяет стенд.
    /// </summary>
    public static class MissionCatalog
    {
        /// <summary>
        /// Точки карты. Порядок постоянный: карта, которая тасуется
        /// сама, отняла бы у игрока возможность сравнить два похода.
        /// </summary>
        public static Mission[] All()
        {
            return new[]
            {
                // Единственная точка, где богатство и опасность разошлись,
                // и оттого единственная прибыльная. Она же первое искушение:
                // чтобы заплатить отряду, надо ограбить тех, кто не дерётся.
                new Mission("Соляной обоз",
                    "По старой соляной дороге ходит обоз. Охраны при нём двое, и те за деньги.",
                    squad: 3, guards: 2, Prize.Silver, Spoils.None, Deed.Robbery,
                    Junction.Caravan),

                new Mission("Придорожная часовня",
                    "Говорят, там кто-то ходит по ночам. Немного, но ходит.",
                    squad: 3, guards: 2, Prize.None, Spoils.Souls, Deed.GraveRobbing),

                new Mission("Затопленная каменоломня",
                    "Вода поднялась и вынесла наверх то, что закапывали.",
                    squad: 4, guards: 4, Prize.None, Spoils.Shell, Deed.GraveRobbing),

                new Mission("Сожжённая застава",
                    "Охотники были здесь первыми. Кто-то из них остался.",
                    squad: 5, guards: 5, Prize.Purses, Spoils.Souls, Deed.ArmedRaid),

                new Mission("Старый гарнизон",
                    "Место держали долго и держат до сих пор — по привычке.",
                    squad: 5, guards: 7, Prize.Purses, Spoils.Upgrade, Deed.ArmedRaid),

                new Mission("Костяная топь",
                    "Туда уходят и не возвращаются. Причину никто не называет.",
                    squad: 6, guards: 8, Prize.None, Spoils.Upgrade, Deed.GraveRobbing),
            };
        }

        /// <summary>Насколько там опасно — словами, для карты.</summary>
        public static string Danger(Mission mission)
        {
            if (mission.Guards < mission.Squad) return "Их там меньше.";
            if (mission.Guards == mission.Squad) return "Их там столько же.";
            if (mission.Guards <= mission.Squad + 2) return "Их там больше.";
            return "Их там намного больше.";
        }

        /// <summary>Что обещают принести — словами.</summary>
        public static string Promise(Spoils spoils)
        {
            switch (spoils)
            {
                case Spoils.Souls:   return "Оттуда несут души.";
                case Spoils.Shell:   return "Оттуда несут тело.";
                case Spoils.Upgrade: return "Оттуда несут то, что ставят в склепе.";
                default:             return "Оттуда несут только своих.";
            }
        }

        /// <summary>Сколько там денег — словами.</summary>
        public static string Riches(Prize prize)
        {
            switch (prize)
            {
                case Prize.Purses: return "Кошели у пояса, не больше.";
                case Prize.Silver: return "Там серебро.";
                default:           return "Денег там нет.";
            }
        }

        /// <summary>
        /// Чем это будет названо, когда вернутся. Отсюда же берётся
        /// слово для памяти: одно дело описывается одинаково и на карте,
        /// и в голове воина.
        /// </summary>
        public static string Named(Deed deed)
        {
            switch (deed)
            {
                case Deed.Robbery:      return "грабёж безоружных";
                case Deed.GraveRobbing: return "разрытие могил";
                default:                return "набег на вооружённых";
            }
        }

        /// <summary>Сколько монет за добычу. Числа наружу не выходят.</summary>
        public static int Coin(Prize prize)
        {
            switch (prize)
            {
                case Prize.Purses: return PursesCoin;
                case Prize.Silver: return SilverCoin;
                default:           return 0;
            }
        }

        // Подобрано замером: Tools/bench → ВЫЛАЗКИ. Цель не «побольше»,
        // а «петля сходится»: долг обязан гаситься, но не сам собой.
        private const int PursesCoin = 14;
        private const int SilverCoin = 55;
    }
}
