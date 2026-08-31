// Assets/Scripts/Gameplay/SquadOrders.cs
using System.Collections.Generic;
using Sinbinder.AOS;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Установка отряда — седьмой рычаг игрока.
    ///
    /// Одиннадцать стратегий и их поправки написаны давно
    /// (StrategyDatabase), но вызывались только из мёртвого автобоя.
    /// Игрок про них не знал, и в GDD напротив рычага стояло
    /// «не подключён».
    ///
    /// Это не приказ. Установка не говорит воину, что делать, — она
    /// меняет, к чему он склонен. Поправка складывается с голосами
    /// характера и вполне может им проиграть: жадный при «прикрывать
    /// своих» всё равно уйдёт за добычей, если золото ближе. В этом
    /// и разница между «командуй» и «искушай».
    ///
    /// Смысл рычага в том, что он общий. Приказ достаётся одному,
    /// плата одному, предмет одному — а установка меняет условия
    /// сразу для всех, и потому это единственный рычаг, которым
    /// игрок работает с отрядом как с отрядом.
    /// </summary>
    public static class SquadOrders
    {
        /// <summary>Текущая установка. Меняется игроком, читается резолвером.</summary>
        public static SquadStrategy Current { get; private set; } = SquadStrategy.Balanced;

        /// <summary>Кого-то поменяли — интерфейсу надо обновиться.</summary>
        public static System.Action<SquadStrategy> Changed;

        public static void Set(SquadStrategy strategy)
        {
            if (Current == strategy) return;
            Current = strategy;
            Changed?.Invoke(strategy);
        }

        public static void Reset() => Set(SquadStrategy.Balanced);

        /// <summary>Поправки текущей установки. Пусто для Balanced.</summary>
        public static List<StrategyModifier> CurrentModifiers()
            => StrategyDatabase.GetModifiers(Current);

        // ---------- слова для игрока ----------
        // Ни одной цифры: игрок видит склонность, а не бонус.

        public static string Name(SquadStrategy s)
        {
            switch (s)
            {
                case SquadStrategy.Balanced:     return "Без указаний";
                case SquadStrategy.Aggressive:   return "В атаку";
                case SquadStrategy.Defensive:    return "Прикрывать своих";
                case SquadStrategy.Cautious:     return "Беречь себя";
                case SquadStrategy.LootFocused:  return "Собирать добро";
                case SquadStrategy.Focused:      return "Держаться приказа";
                case SquadStrategy.Supportive:   return "Помогать раненым";
                case SquadStrategy.Envious:      return "Каждый за себя";
                case SquadStrategy.Attrition:    return "На измор";
                case SquadStrategy.Conservative: return "Не рисковать";
                case SquadStrategy.Relentless:   return "Без передышки";
                default:                         return "Без указаний";
            }
        }

        public static string Describe(SquadStrategy s)
        {
            switch (s)
            {
                case SquadStrategy.Balanced:
                    return "Отряд полагается на себя. Ничего не навязано.";
                case SquadStrategy.Aggressive:
                    return "Охотнее лезут в драку и неохотно отходят.";
                case SquadStrategy.Defensive:
                    return "Тянутся к раненым своим, в драку идут неохотно.";
                case SquadStrategy.Cautious:
                    return "Отходят охотнее, чем дерутся.";
                case SquadStrategy.LootFocused:
                    return "Добыча заботит их больше, чем бой и товарищи.";
                case SquadStrategy.Focused:
                    return "Держатся приказа и не отвлекаются на добычу.";
                case SquadStrategy.Supportive:
                    return "Помогают своим и не думают о добыче.";
                case SquadStrategy.Envious:
                    return "Каждый тянет к себе; за товарищем не пойдут.";
                case SquadStrategy.Attrition:
                    return "Держат удар и отвечают, а не наступают.";
                case SquadStrategy.Conservative:
                    return "Берегут своих и не рискуют.";
                case SquadStrategy.Relentless:
                    return "Не останавливаются, даже когда стоило бы.";
                default:
                    return "";
            }
        }

        /// <summary>Установки, вынесенные в интерфейс демо. Остальные — на потом.</summary>
        public static readonly SquadStrategy[] InDemo =
        {
            SquadStrategy.Balanced,
            SquadStrategy.Aggressive,
            SquadStrategy.Defensive,
            SquadStrategy.Cautious,
            SquadStrategy.LootFocused,
            SquadStrategy.Focused
        };
    }
}
