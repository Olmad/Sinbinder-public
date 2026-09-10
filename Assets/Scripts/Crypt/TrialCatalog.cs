using Sinbinder.Core;

namespace Sinbinder.Crypt
{
    /// <summary>Условие, которое рычаг вносит в опыт.</summary>
    public enum Trial
    {
        /// <summary>Рядом лежит добыча. Будит Жадность.</summary>
        Loot = 0,

        /// <summary>Свой при смерти. Будит Добродетель.</summary>
        Wounded = 1,

        /// <summary>Трое врагов вокруг. Будит Страх.</summary>
        Surrounded = 2,

        /// <summary>Силы на исходе. Будит Уныние.</summary>
        Exhausted = 3,

        /// <summary>Ему не заплатили. Будит Жадность через долг.</summary>
        Unpaid = 4,
    }

    /// <summary>
    /// Что делает каждый рычаг — словами.
    ///
    /// Тренировочная площадка нужна не для того, чтобы посмотреть на бой,
    /// а чтобы игрок **собрал причину руками**. Поэтому у каждого рычага
    /// названо не действие («поставить сундук»), а голос, который он
    /// будит: площадка — это схема души, разложенная по комнате.
    ///
    /// Правило игры не отменяется на полигоне: **ни одной цифры**.
    ///
    /// Таблица без Unity — её проверяет стенд.
    /// </summary>
    public static class TrialCatalog
    {
        /// <summary>Надпись на самом рычаге.</summary>
        public static string Title(Trial trial)
        {
            switch (trial)
            {
                case Trial.Loot:       return "Положить добычу";
                case Trial.Wounded:    return "Ранить своего";
                case Trial.Surrounded: return "Выпустить врагов";
                case Trial.Exhausted:  return "Вымотать его";
                case Trial.Unpaid:     return "Не заплатить";
                default:               return "";
            }
        }

        /// <summary>Чей голос этот рычаг будит. Вторая строка на табличке.</summary>
        public static string Wakes(Trial trial)
        {
            switch (trial)
            {
                case Trial.Loot:       return "Жадность";
                case Trial.Wounded:    return "Добродетель";
                case Trial.Surrounded: return "Страх";
                case Trial.Exhausted:  return "Уныние";
                case Trial.Unpaid:     return "Жадность — через долг";
                default:               return "";
            }
        }

        /// <summary>
        /// Какой грех стоит взять подопытному, чтобы этот рычаг было
        /// на ком показывать. Полигон, где рычаг ничего не меняет,
        /// учит ровно обратному тому, ради чего он поставлен.
        /// </summary>
        public static SinType Suits(Trial trial)
        {
            switch (trial)
            {
                case Trial.Loot:       return SinType.Greed;
                case Trial.Unpaid:     return SinType.Greed;
                case Trial.Wounded:    return SinType.Pride;
                case Trial.Surrounded: return SinType.Sloth;
                case Trial.Exhausted:  return SinType.Sloth;
                default:               return SinType.Greed;
            }
        }
    }
}
