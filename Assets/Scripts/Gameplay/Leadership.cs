// Assets/Scripts/Gameplay/Leadership.cs
// Перевод: текст через Loc
using System;

using Sinbinder.Core;
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
            if (!IsExperienced(leadership)) return Loc.F("водит впервые, уведёт {0}", Count(size));
            return Loc.F("уведёт {0}", Count(size));
        }

        /// <summary>Почему его навыка не хватит на отряд такого размера.</summary>
        public static string Shortfall(float leadership, int required)
            => Loc.F("уведёт {0}, а нужно {1}", Count(SquadSize(leadership)), Collective(required));

        /// <summary>
        /// «Двое», «девятеро» — счёт словами в именительном.
        ///
        /// Открыт наружу по той же причине, что и <see cref="Count"/>:
        /// второй словарь чисел разошёлся бы с этим на первой правке.
        /// Досчитан до двенадцати: обрывался на семи и дальше отдавал
        /// цифру, а отряд в прологе — девять. Ловушка не выстрелила
        /// только потому, что самая большая вылазка требует шестерых.
        /// </summary>
        public static string Collective(int n)
        {
            switch (n)
            {
                case 0:  return Loc.T("никого");
                case 1:  return Loc.T("один");
                case 2:  return Loc.T("двое");
                case 3:  return Loc.T("трое");
                case 4:  return Loc.T("четверо");
                case 5:  return Loc.T("пятеро");
                case 6:  return Loc.T("шестеро");
                case 7:  return Loc.T("семеро");
                case 8:  return Loc.T("восьмеро");
                case 9:  return Loc.T("девятеро");
                case 10: return Loc.T("десятеро");
                case 11: return Loc.T("одиннадцать");
                default: return Loc.T("двенадцать");
            }
        }

        /// <summary>
        /// «Пятерых», «троих» — счёт словами. Открыт наружу: панель
        /// совета говорит о том же отряде теми же словами, и второй
        /// словарь чисел разошёлся бы с этим на первой же правке.
        /// </summary>
        public static string Count(int n)
        {
            switch (n)
            {
                case 1:  return Loc.T("одного");
                case 2:  return Loc.T("двоих");
                case 3:  return Loc.T("троих");
                case 4:  return Loc.T("четверых");
                case 5:  return Loc.T("пятерых");
                case 6:  return Loc.T("шестерых");
                case 7:  return Loc.T("семерых");
                case 8:  return Loc.T("восьмерых");
                case 9:  return Loc.T("девятерых");
                case 10: return Loc.T("десятерых");
                case 11: return Loc.T("одиннадцать");
                default: return Loc.T("двенадцать");
            }
        }
    }
}
