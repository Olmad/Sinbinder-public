// Assets/Scripts/AOS Engine/TitleRule.cs
namespace Sinbinder.AOS
{
    /// <summary>
    /// Условие получения титула. Проверяется в TitleManager против
    /// накопленных DeedRecord воина. Флаги Requires* помечают легендарные
    /// титулы — те, что требуют особых обстоятельств, а не счётчика.
    /// </summary>
    [System.Serializable]
    public class TitleRule
    {
        public string Title;
        public DeedType MainDeed;
        public int RequiredCount;
        public float RequiredImportance;
        public float RequiredRespect;
        public float RequiredFear;

        public bool RequiresSoulCollector;
        public bool RequiresNearAltar;
        public bool RequiresLastAlive;
        public bool RequiresCoreMemory;
    }
}
