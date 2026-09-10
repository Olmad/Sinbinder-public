namespace Sinbinder.Crypt
{
    /// <summary>Что можно принести с вылазки, кроме выживших.</summary>
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

    /// <summary>Одна точка на карте шара.</summary>
    public readonly struct Mission
    {
        public readonly string Name;
        public readonly string Rumour;
        public readonly int Squad;
        public readonly int Foes;
        public readonly Spoils Spoils;

        public Mission(string name, string rumour, int squad, int foes, Spoils spoils)
        {
            Name = name;
            Rumour = rumour;
            Squad = squad;
            Foes = foes;
            Spoils = spoils;
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
    /// <b>Ни одной цифры в тексте.</b> Сколько нужно людей и насколько
    /// там опасно, игрок читает словами — «нужно пятеро», «их там больше».
    /// Числа остаются внутри и в глаза не попадают.
    ///
    /// Таблица без Unity — её проверяет стенд.
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
                new Mission("Придорожная часовня",
                    "Говорят, там кто-то ходит по ночам. Немного, но ходит.",
                    squad: 3, foes: 2, Spoils.Souls),

                new Mission("Затопленная каменоломня",
                    "Вода поднялась и вынесла наверх то, что закапывали.",
                    squad: 4, foes: 4, Spoils.Shell),

                new Mission("Сожжённая застава",
                    "Охотники были здесь первыми. Кто-то из них остался.",
                    squad: 5, foes: 5, Spoils.Souls),

                new Mission("Старый гарнизон",
                    "Место держали долго и держат до сих пор — по привычке.",
                    squad: 5, foes: 7, Spoils.Upgrade),

                new Mission("Костяная топь",
                    "Туда уходят и не возвращаются. Причину никто не называет.",
                    squad: 6, foes: 8, Spoils.Upgrade),
            };
        }

        /// <summary>Насколько там опасно — словами, для карты.</summary>
        public static string Danger(Mission mission)
        {
            if (mission.Foes < mission.Squad) return "Их там меньше.";
            if (mission.Foes == mission.Squad) return "Их там столько же.";
            if (mission.Foes <= mission.Squad + 2) return "Их там больше.";
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
    }
}
