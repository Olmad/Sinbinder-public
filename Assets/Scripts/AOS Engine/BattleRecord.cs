// Assets/Scripts/AOS Engine/BattleRecord.cs
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
        /// <summary>Пол действующего: без него пересказ говорит «Марга ушёл».</summary>
        public Core.Gender ActorGender;

        public ActionType Action;
        public float DamageDealt;
        public float HealDealt;
        public string ResultDescription;
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
