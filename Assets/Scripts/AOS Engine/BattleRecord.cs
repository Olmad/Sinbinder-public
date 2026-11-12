// Assets/Scripts/AOS Engine/BattleRecord.cs
// Перевод: текст через Loc
using System.Collections.Generic;

namespace Sinbinder.AOS
{
    /// <summary>
    /// Одно событие боя: кто, что и с каким исходом сделал.
    /// Это сырьё для журнала решений — игрок должен читать не цифры,
    /// а «почему он так поступил».
    /// </summary>
    [System.Serializable]
    public class BattleEvent
    {
        public string ActorId;

        /// <summary>
        /// Имя действующего. Id — это Guid, свой у каждой машины
        /// и бессмысленный для пересказа: по нему не скажешь, кто
        /// свернул за добычей.
        /// </summary>
        public string ActorName;
        public string TargetId;
        // Human-readable identity captured at the moment of the action.
        // The target's ephemeral runtime Guid alone is not a replay name.
        public string TargetName;
        /// <summary>Пол действующего: без него пересказ говорит «Марга ушёл».</summary>
        public Core.Gender ActorGender;

        public ActionType Action;
        public float DamageDealt;
        public float HealDealt;
        // True only when the executor applied the chosen action.
        // AOS intent and completed world action are different facts.
        public bool Resolved;
        // True only when this action itself brought its target to zero HP.
        public bool TargetKilled;
        // Actual target HP at the instant this action finished. The damage
        // or healing fields describe measured HP changes, not intent.
        public float TargetHealthAfter;
        // One-based actual resolver round. Zero means legacy/unknown.
        public int Round;
    }

    /// <summary>
    /// Полная запись авто-боя. Заполняется AutoBattleResolver
    /// и показывается игроку после миссии.
    /// </summary>
    [System.Serializable]
    public class BattleRecord
    {
        public string Winner;
        public int TotalTurns;
        public List<BattleEvent> Events = new();

        public void AddEvent(BattleEvent evt)
        {
            if (evt != null) Events.Add(evt);
        }
    }
}
