// Assets/_Project/Scripts/Core/Soul/PerkEffect.cs
using Sinbinder.AOS;

namespace Sinbinder.Core
{
    /// <summary>
    /// Один эффект перка: при выполнении Condition к действию UnlockedAction
    /// прибавляется ScoreModifier. Условия разбирает PerkResolver.
    /// </summary>
    [System.Serializable]
    public class PerkEffect
    {
        public string Condition = "Always";
        public ActionType? UnlockedAction;
        public float ScoreModifier;
    }
}
