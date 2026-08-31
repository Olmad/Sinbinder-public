// Assets/_Project/Scripts/AOS/AOSConfig.cs
using UnityEngine;

namespace Sinbinder.AOS
{
    [CreateAssetMenu(fileName = "AOSConfig", menuName = "Sinbinder/AOS Config")]
    public class AOSConfig : ScriptableObject
    {
        private static AOSConfig _fallback;

        /// <summary>
        /// Конфиг из Resources, а если ассета нет — рабочий экземпляр
        /// со значениями по умолчанию.
        ///
        /// Раньше каждый модуль сам звал Resources.Load и при отсутствии
        /// ассета возвращал ноль на любое действие. Отсутствие одного
        /// файла бесшумно выключало всю личность разом: воины двигались,
        /// решений не принимали, в консоли — одно предупреждение.
        /// Теперь игра работает и без ассета, а предупреждение выдаётся
        /// один раз и по делу.
        /// </summary>
        public static AOSConfig Load()
        {
            var asset = Resources.Load<AOSConfig>("AOSConfig");
            if (asset != null) return asset;

            if (_fallback == null)
            {
                _fallback = CreateInstance<AOSConfig>();
                _fallback.name = "AOSConfig (значения по умолчанию)";
                Debug.LogWarning("[AOS] Resources/AOSConfig не найден. "
                    + "Работаю на значениях по умолчанию — создай ассет "
                    + "через Assets → Create → Sinbinder → AOS Config "
                    + "и положи в папку Resources, чтобы настраивать баланс.");
            }
            return _fallback;
        }

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

        [Header("Страх: чем его пересиливают")]
        [Tooltip("Своей шкалы у Страха нет — храбрость собирается из трёх "
               + "имеющихся. Доли складываются в «стойкость», и весь голос "
               + "Страха делится на неё. Ноль во всех трёх снова уравняет "
               + "гордеца с унылым.")]
        public float NervePrideShare = 0.4f;
        public float NerveWrathShare = 0.3f;
        public float NerveSlothShare = 0.3f;

        [Tooltip("Границы множителя страха. Меньше единицы — храбрее "
               + "среднего. Ноль снизу означал бы душу, не знающую страха "
               + "вовсе, — такой в игре быть не должно.")]
        public float NerveFloor = 0.4f;
        public float NerveCeiling = 1.6f;

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
        public float MoralityPiousObey = 35f;
        public float MoralityViciousAttack = 20f;
        public float MoralityViciousLoot = 15f;
        public float MoralityViciousSaveAllyPenalty = -20f;
        public float MoralityViciousObeyPenalty = -15f;

        [Header("Голосование")]
        [Tooltip("Насколько второй голос должен отставать от первого, чтобы "
               + "решение считалось принятым. Доля, а не очки: 0.15 значит "
               + "«второй тише первого меньше чем на шестую часть — воин "
               + "колеблется». Больше значение — чаще колебание. "
               + "Первое, что придётся крутить руками (11-MISSING §2.4).")]
        [Range(0f, 1f)] public float HesitationShare = 0.10f;

        [Tooltip("Цена одной единицы искушения от вещи. Предмет обязан быть "
               + "слабее характера, но сильнее шума: он переворачивает только "
               + "то решение, которое и так было близким. Подняв это число, "
               + "вы сделаете снаряжение сильнее души — а это уже другая игра.")]
        [Range(0f, 1f)] public float TemptationScale = 0.2f;

        [Header("Пол: усталость, зацепление, спина")]
        public float SlothIdleFatigueMultiplier = 55f;
        public float SlothAttackFatiguePenalty = -30f;
        public float PrideIdleFatigueRefusal = -35f;
        public float PrideRearStrikeRefusal = -30f;
        public float FearFleeSurroundedBonus = 60f;
        public float WrathAttackFatigueIgnore = 20f;
        public float LoyaltyObeyEngagedPenalty = -35f;

        [Header("Гордыня")]
        public float PrideAttackSinMultiplier = 0.4f;
        public float PrideFleeSinMultiplier = 0.8f;
        public float PrideFleeHighSinPenalty = -70f;
        public float PrideObeySinMultiplier = 0.35f;

        [Tooltip("Какая доля штрафа Гордыни остаётся на приказе, который "
               + "не унижает: «встань здесь» в лагере против «отойди» под "
               + "взглядом врага. Единица снова уравняет любые приказы.")]
        [Range(0f, 1f)] public float PrideObeyPlainOrderShare = 0.3f;
        public float PrideLastAliveBonus = 45f;
        public float PrideSaveAllySinMultiplier = 0.2f;

        [Header("Зависть")]
        public float EnvyLootSinMultiplier = 0.4f;
        public float EnvySaveAllySinMultiplier = 0.5f;
        public float EnvyAttackCommanderRelationMultiplier = 0.25f;
        public float EnvyObeyLowRelationPenalty = -25f;

        [Header("Похоть")]
        public float LustLootSinMultiplier = 0.35f;
        public float LustObeySinMultiplier = 0.25f;
        public float LustSaveAllyBondBonus = 30f;
        public float LustIdleSinMultiplier = 0.2f;

        [Header("Чревоугодие")]
        public float GluttonyLootSinMultiplier = 0.45f;
        public float GluttonyIdleSinMultiplier = 0.3f;
        public float GluttonyAttackSinMultiplier = 0.2f;
        public float GluttonyLootPerBody = 8f;

        [Header("Память: чем держатся за прошлое")]
        [Tooltip("Своей шкалы у памяти нет — цепкость собирается из "
               + "имеющихся, своя на каждый род воспоминания. Ноль во всех "
               + "долях снова уравняет злопамятного с отходчивым.")]
        public float RecallBondShare = 0.6f;      // долг благодарности держит Похоть-привязанность
        public float RecallPrideShare = 0.4f;     // и отпускает Гордыня: быть обязанным унизительно
        public float RecallGrudgeEnvyShare = 0.5f;
        public float RecallGrudgeWrathShare = 0.4f;
        public float RecallVengeanceShare = 0.7f;
        public float RecallGreedShare = 0.6f;

        [Tooltip("Границы множителя памяти. Ноль снизу означал бы душу, "
               + "не помнящую вовсе, — такой в игре быть не должно.")]
        public float RecallFloor = 0.3f;
        public float RecallCeiling = 1.8f;

        [Header("Память")]
        public float MemorySaveAllyStrengthMultiplier = 50f;
        public float MemoryBetrayalStrengthMultiplier = -40f;
        public float MemoryKillStrengthMultiplier = 30f;
        public float MemoryLootStrengthMultiplier = 20f;
        public float MemoryWeight = 1.5f;
    }
}