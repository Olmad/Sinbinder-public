// Assets/_Project/Scripts/AOS Engine/TemptationResolver.cs
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.AOS
{
    /// <summary>
    /// Снаряжение как рычаг игрока.
    ///
    /// Игрок не может переписать душу — но может вложить ей в руки золотой
    /// клинок и посмотреть, что из этого выйдет. Предмет с TemptationSin
    /// усиливает те действия, к которым этот грех и так тянет.
    ///
    /// Считается один раз на решение, централизованно. Модули о предметах
    /// не знают: у модуля одна работа — переводить характер в очки.
    /// </summary>
    public static class TemptationResolver
    {
        /// <summary>Во что грех толкает воина в боевом голосовании.</summary>
        private static readonly Dictionary<SinType, (ActionType action, float pull)[]> Pull =
            new Dictionary<SinType, (ActionType, float)[]>
            {
                { SinType.Greed,    new[] { (ActionType.Loot, 1.0f), (ActionType.ObeyCommand, -0.3f) } },
                { SinType.Pride,    new[] { (ActionType.Attack, 0.8f), (ActionType.Flee, -1.0f) } },
                { SinType.Wrath,    new[] { (ActionType.Attack, 1.0f), (ActionType.Idle, -0.5f) } },
                { SinType.Envy,     new[] { (ActionType.Attack, 0.6f), (ActionType.Loot, 0.4f) } },
                { SinType.Lust,     new[] { (ActionType.Loot, 0.5f), (ActionType.ObeyCommand, -0.2f) } },
                { SinType.Gluttony, new[] { (ActionType.Loot, 0.7f), (ActionType.Idle, 0.3f) } },
                { SinType.Sloth,    new[] { (ActionType.Idle, 1.0f), (ActionType.Attack, -0.5f) } },
            };

        /// <summary>
        /// Прибавляет к очкам влияние всего, что воин несёт на себе.
        /// scale — цена одной единицы искушения; 0.2 держит предмет
        /// слабее характера, но сильнее шума.
        /// </summary>
        public static void Apply(Dictionary<ActionType, float> scores, DecisionContext context, float scale = 0.2f)
        {
            if (scores == null || context == null || context.CarriedItems == null) return;

            foreach (var item in context.CarriedItems)
            {
                // Отрицательное искушение тянет от греха, а не к нему:
                // верёвка монаха — это минус Гордыня (08-FLOOR §3.3).
                // Раньше такие предметы молча отбрасывались, и половина
                // задуманного контента была невыразима.
                if (item == null || Mathf.Approximately(item.TemptationValue, 0f)) continue;
                if (!Pull.TryGetValue(item.TemptationSin, out var pulls)) continue;

                foreach (var p in pulls)
                {
                    if (!scores.ContainsKey(p.action)) continue;
                    scores[p.action] += item.TemptationValue * p.pull * scale;
                }
            }
        }

        /// <summary>
        /// Суммарное искушение конкретным грехом — для подсказок интерфейса
        /// и для панели предсказания темперамента.
        /// </summary>
        public static float Sum(DecisionContext context, SinType sin)
        {
            if (context == null || context.CarriedItems == null) return 0f;

            float total = 0f;
            foreach (var item in context.CarriedItems)
                if (item != null && item.TemptationSin == sin)
                    total += item.TemptationValue;
            return total;
        }
    }
}
