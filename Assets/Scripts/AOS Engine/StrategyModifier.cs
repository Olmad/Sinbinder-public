// Assets/Scripts/AOS Engine/StrategyModifier.cs
namespace Sinbinder.AOS
{
    /// <summary>
    /// Сдвиг оценки одного действия, задаваемый стратегией отряда.
    /// Командир не приказывает — он смещает веса. Итоговый выбор
    /// всё равно за личностью воина.
    /// </summary>
    [System.Serializable]
    public class StrategyModifier
    {
        public ActionType Action;
        public float Bonus;
    }
}
