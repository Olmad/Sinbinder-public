// Assets/Scripts/Gameplay/Leadership.cs
using System;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Навык командования: одна ось, одно следствие.
    ///
    /// Он определяет <b>только</b> то, скольких человек командир уведёт
    /// с собой. Больше ничего. В частности, он не прибавляет веса приказу:
    /// замер насыщения показал, что голос Верности упирается в потолок
    /// MaxVoice уже при 32 (docs/12-BALANCE.md), так что прибавка туда была
    /// бы неотличима от нуля. Игрок качал бы навык и не чувствовал разницы.
    ///
    /// Манеру отряда задаёт не навык, а грех командира — через
    /// <see cref="AOS.SquadStrategy"/>, где у каждого значения проставлено,
    /// чей это грех. Две оси не смешиваются: грех отвечает за то,
    /// <b>как</b> отряд себя ведёт, навык — за то, <b>сколько</b> в нём людей.
    ///
    /// Отсюда же и разница между командиром и рядовым. Рядовой может
    /// повести отряд — просто возьмёт троих. Опытный берёт больше.
    /// Никакого запрета вести, только разная вместимость.
    ///
    /// Класс намеренно без Unity: правило проверяется стендом
    /// (Tools/bench) наравне с боевыми модулями.
    /// </summary>
    public static class Leadership
    {
        /// <summary>Скольких уводит тот, кто не водил ни разу.</summary>
        public const int PrivateSquad = 3;

        /// <summary>Предел для самого опытного.</summary>
        public const int MaxSquad = 12;

        /// <summary>Выше этого — уже водил отряды, и это видно в списке.</summary>
        public const float ExperienceThreshold = 1f;

        /// <summary>Шкала навыка, как и все прочие в проекте.</summary>
        public const float MaxLeadership = 100f;

        public static bool IsExperienced(float leadership)
            => leadership >= ExperienceThreshold;

        /// <summary>
        /// Сколько человек уведёт. Ноль навыка — трое, полная сотня —
        /// двенадцать, между ними ровно.
        /// </summary>
        public static int SquadSize(float leadership)
        {
            if (leadership < 0f) leadership = 0f;
            if (leadership > MaxLeadership) leadership = MaxLeadership;

            float span = MaxSquad - PrivateSquad;
            int size = PrivateSquad + (int)Math.Round(leadership / MaxLeadership * span,
                                                      MidpointRounding.AwayFromZero);

            if (size < PrivateSquad) size = PrivateSquad;
            if (size > MaxSquad) size = MaxSquad;
            return size;
        }

        /// <summary>Хватит ли его навыка на отряд нужного размера.</summary>
        public static bool CanLead(float leadership, int required)
            => SquadSize(leadership) >= required;

        /// <summary>
        /// Человеческим языком, без цифр: игрок не должен видеть шкалу.
        /// Правило четвёртой ступени прозрачности — цифры остаются
        /// разработчику (docs/00-GDD.md §7).
        /// </summary>
        public static string Describe(float leadership)
        {
            int size = SquadSize(leadership);
            if (!IsExperienced(leadership)) return $"водит впервые, уведёт {Count(size)}";
            return $"уведёт {Count(size)}";
        }

        /// <summary>Почему его навыка не хватит на отряд такого размера.</summary>
        public static string Shortfall(float leadership, int required)
            => $"уведёт {Count(SquadSize(leadership))}, а нужно {Collective(required)}";

        private static string Collective(int n)
        {
            switch (n)
            {
                case 1:  return "один";
                case 2:  return "двое";
                case 3:  return "трое";
                case 4:  return "четверо";
                case 5:  return "пятеро";
                case 6:  return "шестеро";
                case 7:  return "семеро";
                default: return n.ToString();
            }
        }

        private static string Count(int n)
        {
            switch (n)
            {
                case 1:  return "одного";
                case 2:  return "двоих";
                case 3:  return "троих";
                case 4:  return "четверых";
                case 5:  return "пятерых";
                case 6:  return "шестерых";
                case 7:  return "семерых";
                case 8:  return "восьмерых";
                case 9:  return "девятерых";
                case 10: return "десятерых";
                case 11: return "одиннадцать";
                default: return "двенадцать";
            }
        }
    }
}
