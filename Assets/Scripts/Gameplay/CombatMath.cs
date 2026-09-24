// Assets/Scripts/Gameplay/CombatMath.cs
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Удар и защита в бою (14-HANDOFF §60.4, решение автора 24 сентября):
    /// <b>удар и защита — от оболочки, плюс от вещей в руках</b>.
    ///
    /// До этого в бою у всех был удар 5 и не было защиты: число сидело
    /// в <see cref="AutoAttack"/>, а удар и защита воина считались
    /// от уровня, которого в игре нет, и бой их не читал. Та же болезнь,
    /// что была со здоровьем.
    ///
    /// <b>Защита гасит удар долей, а не вычитанием.</b> Вычитание делает
    /// броню всесильной при малом ударе: голем с защитой 4 против удара 5
    /// получал бы единицу — неуязвимость, а не крепость. Доля
    /// <c>K / (K + защита)</c> плавная: защита 1 гасит около десятой части,
    /// 4 — около трёх десятых, и ни одна защита не гасит удар целиком.
    ///
    /// <b>Выключатель.</b> До вечернего прогона 24 сентября выключен: бой
    /// прежний, удар 5 без защиты. Включается командой «удар» в консоли (~),
    /// третьим проходом прогона.
    /// </summary>
    public static class CombatMath
    {
        /// <summary>Считать ли удар и защиту. Выключено — бой как до 24 сентября.</summary>
        public static bool Enabled { get; set; }

        /// <summary>Защита, которая гасит ровно половину удара.</summary>
        public const float Scale = 10f;

        /// <summary>
        /// Какую долю удара даёт оружие во второй руке. Не целиком: два
        /// топора — не два бойца, вторая рука слабее и занята защитой себя.
        /// </summary>
        public const float OffhandShare = 0.5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Enabled = false;

        /// <summary>Сколько удара дойдёт до тела через такую защиту.</summary>
        public static float Absorb(float damage, float defense)
            => damage * Scale / (Scale + Mathf.Max(0f, defense));
    }
}
