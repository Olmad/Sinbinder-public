// Assets/_Project/Scripts/AOS/AOSConfig.cs
using UnityEngine;

namespace Sinbinder.AOS
{
    [CreateAssetMenu(fileName = "AOSConfig", menuName = "Sinbinder/AOS Config")]
    public class AOSConfig : ScriptableObject
    {
        [Header("Жадность")]
        public float GreedLootPerItem = 15f;
        public float GreedSinMultiplier = 0.5f;
        public float GreedAttackPenaltyWhenLoot = -10f;
        public float GreedSaveAllySinMultiplier = 0.3f;
        public float GreedSaveAllyHighSinPenalty = -20f;
        public float GreedAttackGoodVirtueBonus = 15f;
        public float GreedObeyUnpaidPenalty = -40f;

        [Header("Гнев")]
        public float WrathAttackPerEnemy = 15f;
        public float WrathSinMultiplier = 0.3f;
        public float WrathAllyInDangerBonus = 20f;
        public float WrathFleeSinMultiplier = 0.6f;
        public float WrathFleeHighSinPenalty = -60f;
        public float WrathIdleHighSinPenalty = -40f;

        [Header("Уныние")]
        public float SlothIdleSinMultiplier = 0.5f;
        public float SlothIdleLowDangerBonus = 20f;
        public float SlothFleeDangerThreshold = 55f;
        public float SlothAttackSinMultiplier = 0.3f;
        public float SlothAttackDangerPenalty = -30f;
        public float SlothSaveAllySinMultiplier = 0.4f;

        [Header("Страх")]
        public float FearFleeLowHpBonus = 85f;
        public float FearAttackLowHpPenalty = -55f;
        public float FearFleeDangerMultiplier = 90f;
        public float FearIdleDangerPenalty = -20f;
        public float FearAttackGlobalPenalty = -0.2f;

        [Header("Добродетель")]
        public float VirtueSaveAllySinMultiplier = 0.7f;
        public float VirtueSaveAllyDangerBonus = 50f;
        public float VirtueLootHighVirtuePenalty = -25f;
        public float VirtueObeySinMultiplier = 0.3f;

        [Header("Верность")]
        public float LoyaltyObeySinMultiplier = 0.5f;

        [Header("Мораль")]
        public float MoralityPiousSaveAlly = 40f;
        public float MoralityPiousLootPenalty = -20f;
        public float MoralityPiousFleePenalty = -15f;
        public float MoralityViciousAttack = 20f;
        public float MoralityViciousLoot = 15f;
        public float MoralityViciousSaveAllyPenalty = -20f;

        [Header("Память")]
        public float MemorySaveAllyStrengthMultiplier = 50f;
        public float MemoryBetrayalStrengthMultiplier = -40f;
        public float MemoryKillStrengthMultiplier = 30f;
        public float MemoryLootStrengthMultiplier = 20f;
        public float MemoryWeight = 1.5f;
    }
}