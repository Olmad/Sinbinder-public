// Assets/Scripts/Core/SinPalette.cs
using UnityEngine;

namespace Sinbinder.Core
{
    /// <summary>
    /// Цвет греха — один на всю игру.
    ///
    /// Жил в <c>UI/WarriorTooltipUI</c> и был там один-единственный, пока
    /// цвет требовался только подсказке. С 15 сентября его просит и трёхмерный
    /// слой: по слову автора у воинов горят глаза под цвет греха. Держать
    /// две таблицы нельзя — разъедутся на первой же правке, и рамка подсказки
    /// перестанет совпадать с огнём в глазах того же воина.
    ///
    /// Игрок связи не осознаёт, но набирает её за десяток подсказок
    /// и потом читает цвет быстрее текста.
    /// </summary>
    public static class SinPalette
    {
        public static Color Of(SinType sin)
        {
            switch (sin)
            {
                case SinType.Greed:    return new Color(0.85f, 0.68f, 0.24f); // старое золото
                case SinType.Pride:    return new Color(0.62f, 0.45f, 0.78f); // фиолетовый
                case SinType.Wrath:    return new Color(0.78f, 0.25f, 0.20f); // тёмно-красный
                case SinType.Envy:     return new Color(0.36f, 0.62f, 0.42f); // болотный
                case SinType.Lust:     return new Color(0.80f, 0.40f, 0.55f); // тусклый розовый
                case SinType.Gluttony: return new Color(0.72f, 0.52f, 0.30f); // ржавый
                case SinType.Sloth:    return new Color(0.48f, 0.50f, 0.54f); // серый
                default:               return new Color(0.80f, 0.79f, 0.76f); // пепел
            }
        }
    }
}
