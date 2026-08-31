using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.AOS.Modules
{
    /// <summary>
    /// Память: прошлое, которое тянет за руку в настоящем.
    ///
    /// Своей шкалы у памяти нет и быть не должно — злопамятность
    /// не восьмой грех. Но и одинаковой у всех она быть не может:
    /// до появления <see cref="Recall"/> этот модуль (вес 1.5, третий
    /// по тяжести) не обращался к душе ни разу — параметр soul в Evaluate
    /// не использовался. Гордый и смиренный помнили спасение одинаково
    /// сильно, завистливый и щедрый одинаково держали обиду.
    ///
    /// Держится за прошлое каждый своим местом, и места эти уже есть
    /// на семи шкалах:
    ///
    /// — долг благодарности помнит привязчивый, а гордый норовит забыть:
    ///   быть обязанным унизительно;
    /// — обиду ведёт счётом завистливый, а гневный не даёт ей остыть;
    /// — за убитого своего мстит гнев;
    /// — где взял однажды, помнит жадность.
    /// </summary>
    public class MemoryModule : IPersonalityModule
    {
        public string ModuleID => "Memory";
        public float Weight => 1.5f;

        private AOSConfig _config;

        public MemoryModule()
        {
            _config = AOSConfig.Load();
        }

        /// <summary>
        /// Множитель к силе воспоминания: насколько эта душа вообще
        /// держится за прошлое такого рода. Единица — как у всех,
        /// меньше — забывает, больше — не отпускает.
        /// </summary>
        private float Recall(float hold)
            => Mathf.Clamp(1f + hold / 100f, _config.RecallFloor, _config.RecallCeiling);

        public float Evaluate(Soul soul, DecisionContext context, ActionType action)
        {
            if (context.RecentMemories == null) return 0f;

            float score = 0f;
            foreach (var memory in context.RecentMemories)
            {
                float strength = memory.Strength;
                switch (memory.EventType)
                {
                    // Меня спасли. Привязчивый помнит долг, гордый — что был должен.
                    case "AllySavedMe" when action == ActionType.SaveAlly:
                        score += strength * _config.MemorySaveAllyStrengthMultiplier
                               * Recall(soul.Get(SinType.Lust) * _config.RecallBondShare
                                      - soul.Get(SinType.Pride) * _config.RecallPrideShare);
                        break;

                    // Меня предали. Обиду держат завистью и гневом.
                    case "AllyBetrayedMe" when action == ActionType.SaveAlly:
                        score += strength * _config.MemoryBetrayalStrengthMultiplier
                               * Recall(soul.Get(SinType.Envy) * _config.RecallGrudgeEnvyShare
                                      + soul.Get(SinType.Wrath) * _config.RecallGrudgeWrathShare);
                        break;

                    // Убили моего. Мстит гнев.
                    case "EnemyKilledAlly" when action == ActionType.Attack:
                        score += strength * _config.MemoryKillStrengthMultiplier
                               * Recall(soul.Get(SinType.Wrath) * _config.RecallVengeanceShare);
                        break;

                    // Здесь однажды взял. Помнит жадность.
                    case "FoundLoot" when action == ActionType.Loot:
                        score += strength * _config.MemoryLootStrengthMultiplier
                               * Recall(soul.Get(SinType.Greed) * _config.RecallGreedShare);
                        break;
                }
            }
            return score * Weight;
        }
    }
}
