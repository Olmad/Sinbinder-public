using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.Gameplay
{
    [System.Serializable]
    public class LootResult
    {
        public int SoulsCollected;
        public int BodiesCollected;
        public int GoldCollected;
        public bool EquipmentFound;
        public string EquipmentName;
    }

    public static class LootTable
    {
        /// <summary>
        /// Рассчитать лут для ручного сбора (ГГ на поле)
        /// </summary>
        public static LootResult CalculateManualLoot(int enemiesKilled, int alliesLost, bool hasSinbinder)
        {
            var result = new LootResult();

            result.SoulsCollected = enemiesKilled + alliesLost;
            result.BodiesCollected = enemiesKilled + alliesLost;
            result.GoldCollected = Random.Range(10, 31) * enemiesKilled;

            if (Random.value < 0.3f * (enemiesKilled / 3f) || (hasSinbinder && Random.value < 0.5f))
            {
                result.EquipmentFound = true;
                result.EquipmentName = GetRandomEquipment();
            }

            return result;
        }

        /// <summary>
        /// Рассчитать лут для автобоя (отряд без ГГ)
        /// </summary>
        public static LootResult CalculateAutoLoot(int enemiesKilled, int alliesLost, List<Warrior> squad)
        {
            var result = new LootResult();

            float soulChance = 0.4f + squad.Count * 0.05f;
            result.SoulsCollected = 0;
            for (int i = 0; i < enemiesKilled + alliesLost; i++)
            {
                if (Random.value < soulChance)
                    result.SoulsCollected++;
            }

            float goldBonus = 1f;
            foreach (var w in squad)
            {
                if (w.Soul.Sin == Core.SinType.Greed)
                    goldBonus += 0.3f;
                if (w.Virtue.Value < -50f)
                    goldBonus += 0.2f;
            }
            result.GoldCollected = Mathf.CeilToInt(Random.Range(5, 21) * enemiesKilled * goldBonus);

            result.BodiesCollected = enemiesKilled + alliesLost;

            float equipChance = 0.15f;
            foreach (var w in squad)
            {
                if (w.Soul.Sin == Core.SinType.Envy)
                    equipChance += 0.1f;
            }
            if (Random.value < equipChance * (enemiesKilled / 2f))
            {
                result.EquipmentFound = true;
                result.EquipmentName = GetRandomEquipment();
            }

            return result;
        }

        private static string GetRandomEquipment()
        {
            string[] items = {
                "Ржавый меч", "Старый щит", "Кожаный доспех",
                "Костяной шлем", "Железный нагрудник", "Кольцо силы",
                "Амулет защиты", "Плащ невидимки"
            };
            return items[Random.Range(0, items.Length)];
        }
    }
}