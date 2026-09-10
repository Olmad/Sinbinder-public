using Sinbinder.AOS;
using Sinbinder.Core;

namespace Sinbinder.Crypt
{
    /// <summary>
    /// Что отряд думает о том, что сделал.
    ///
    /// Без этого развилка была бы выбором из четырёх наград: игрок
    /// нажал, получил серебро, забыл. Поступок обязан оставаться
    /// на людях — иначе это не поступок, а кнопка.
    ///
    /// <b>Почему верность, а не память.</b> Настоящее место такому —
    /// <c>MemoryProcessor</c>: он умеет и след, и отношения, и слухи.
    /// Но воины вылазки живут только внутри <see cref="Gameplay.Expedition"/>
    /// и уничтожаются вместе с ней, а между вылазками отряд существует
    /// как данные (<c>SquadRoster.Member</c>), у которых памяти нет.
    /// Пока это так, след кладётся в верность: она переживает вылазку
    /// и её читает боевой движок. Разбор и то, что с этим делать, —
    /// docs/19-MISSIONS.md.
    ///
    /// Числа наружу не выходят: игрок читает <see cref="Judged"/>.
    /// Проверяется стендом.
    /// </summary>
    public static class Aftermath
    {
        /// <summary>
        /// Насколько это было грязно. Ноль — не в чем себя упрекнуть.
        ///
        /// Увести живых грязнее, чем убить: мёртвых хотя бы отпустили.
        /// Тем же порядком это оценивает и Мораль на голосовании —
        /// одно дело обязано весить одинаково до поступка и после.
        /// </summary>
        public static float Filth(MissionAction action)
        {
            switch (action)
            {
                case MissionAction.LetThemPass:          return 0f;
                case MissionAction.TakeGoodsSparePeople: return 0.35f;
                case MissionAction.TakeEverything:       return 1f;
                case MissionAction.TakePeople:           return 1.2f;
                default:                                 return 0f;
            }
        }

        /// <summary>
        /// На сколько сдвинется верность одного вернувшегося.
        ///
        /// Благочестивый теряет тем больше, чем грязнее дело. Порочный
        /// прибавляет — но вдвое тише: мерзость радует слабее, чем
        /// возмущает. А за отпущенный обоз порочный теряет: он шёл
        /// не за этим.
        /// </summary>
        public static float LoyaltyShift(MoralType moral, MissionAction action)
        {
            float filth = Filth(action);

            if (action == MissionAction.LetThemPass)
            {
                switch (moral)
                {
                    case MoralType.Pious:   return CleanReward;
                    case MoralType.Vicious: return -CleanReward;
                    default:                return 0f;
                }
            }

            switch (moral)
            {
                case MoralType.Pious:   return -filth * PiousWeight;
                case MoralType.Vicious: return filth * ViciousWeight;
                default:                return 0f;
            }
        }

        /// <summary>Что он об этом скажет. Без цифр.</summary>
        public static string Judged(MoralType moral, MissionAction action)
        {
            if (action == MissionAction.LetThemPass)
            {
                switch (moral)
                {
                    case MoralType.Pious:   return "не жалеет, что отпустили";
                    case MoralType.Vicious: return "не понимает, зачем ходили";
                    default:                return "молчит";
                }
            }

            float filth = Filth(action);

            if (moral == MoralType.Pious)
                return filth >= 1f ? "не смотрит на командира" : "старается не вспоминать";

            if (moral == MoralType.Vicious)
                return filth >= 1f ? "доволен и не скрывает" : "считает, что можно было и больше";

            return "молчит";
        }

        // Подобрано замером: Tools/bench → РАЗВИЛКА. Резня обязана
        // раскалывать смешанный отряд за одну вылазку, а не за десять.
        private const float PiousWeight = 14f;
        private const float ViciousWeight = 7f;
        private const float CleanReward = 4f;
    }
}
