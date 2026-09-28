// Перевод: текст через Loc
using System.Collections.Generic;
using Sinbinder.AOS;

using Sinbinder.Core;
namespace Sinbinder.Crypt
{
    /// <summary>
    /// Развилки: положение, предложения игрока и слова для отчёта.
    ///
    /// Развилка — не диалог. Диалоговое дерево было бы второй игрой сбоку;
    /// здесь всё устроено как везде: вылазка останавливается, положение
    /// названо словами, игрок <b>предлагает</b>, а решает командир —
    /// через <see cref="BehaviorResolver.DecideMission"/>, слой, который
    /// в проекте уже был и которым пользовалась одна миссия из всех.
    ///
    /// Предложение игрока входит в голосование как <c>LoyaltyModule</c>,
    /// то есть ровно на тех же правах, что приказ в бою: один голос,
    /// который можно перекричать. «Не командуй. Искушай.» — только
    /// не в бою, а на дороге.
    ///
    /// Ни одной цифры наружу. Таблица без Unity — её проверяет стенд.
    /// Замысел и остальные развилки — docs/19-MISSIONS.md.
    /// </summary>
    public static class JunctionCatalog
    {
        /// <summary>Что случилось. Игрок читает это и решает, что предложить.</summary>
        public static string Situation(Junction junction)
        {
            switch (junction)
            {
                case Junction.Caravan:
                    return Loc.T("Обоз остановлен. Возчики бросили поводья и стоят. "
                         + "Купец предлагает откуп и смотрит на командира.");
                default:
                    return null;
            }
        }

        /// <summary>
        /// Что можно предложить. Порядок постоянный: развилка, которая
        /// тасует ответы, отняла бы у игрока возможность научиться.
        /// </summary>
        public static List<MissionAction> Options(Junction junction)
        {
            var list = new List<MissionAction>();

            if (junction == Junction.Caravan)
            {
                list.Add(MissionAction.TakeGoodsSparePeople);
                list.Add(MissionAction.TakeEverything);
                list.Add(MissionAction.LetThemPass);
                list.Add(MissionAction.TakePeople);
            }

            return list;
        }

        /// <summary>Как предложение звучит на кнопке.</summary>
        public static string Offer(MissionAction action)
        {
            switch (action)
            {
                case MissionAction.TakeGoodsSparePeople: return Loc.T("Возьмите товар. Людей не трогайте.");
                case MissionAction.TakeEverything:       return Loc.T("Возьмите всё.");
                case MissionAction.LetThemPass:          return Loc.T("Пропустите их.");
                case MissionAction.TakePeople:           return Loc.T("Людей — с собой.");
                default:                                 return Loc.T("Решайте сами.");
            }
        }

        /// <summary>
        /// Что рассказали, вернувшись. Отсюда же берётся строка памяти:
        /// одно и то же дело обязано называться одинаково и в отчёте,
        /// и в голове воина.
        /// </summary>
        public static string Told(MissionAction action)
        {
            switch (action)
            {
                case MissionAction.TakeGoodsSparePeople:
                    return Loc.T("Обоз разгрузили и отпустили. Возчики шли пешком и оглядывались.");
                case MissionAction.TakeEverything:
                    return Loc.T("С дороги не ушёл никто. Серебро вынесли вместе с душами.");
                case MissionAction.LetThemPass:
                    return Loc.T("Обоз пропустили. Отряд вернулся ни с чем и молчал всю дорогу.");
                case MissionAction.TakePeople:
                    return Loc.T("Товар бросили. Привели людей — живых, связанных и целых.");
                default:
                    return Loc.T("На дороге что-то случилось, и рассказывать об этом не стали.");
            }
        }

        /// <summary>Одно слово для памяти: чем это было.</summary>
        public static string Remembered(MissionAction action)
        {
            switch (action)
            {
                case MissionAction.TakeGoodsSparePeople: return Loc.T("ГрабёжБезКрови");
                case MissionAction.TakeEverything:       return Loc.T("РезняНаДороге");
                case MissionAction.LetThemPass:          return Loc.T("ОбозОтпущен");
                case MissionAction.TakePeople:           return Loc.T("ЛюдиУведены");
                default:                                 return Loc.T("Вылазка");
            }
        }
    }
}
