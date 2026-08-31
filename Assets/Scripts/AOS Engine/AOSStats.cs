// Assets/Scripts/AOS Engine/AOSStats.cs
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Sinbinder.AOS
{
    /// <summary>
    /// Счётчик решений. Одно число, ради которого он написан:
    /// **как часто воин не выполняет отданный приказ.**
    ///
    /// До сих пор его нельзя было узнать. В логе оставались действия
    /// и очки, но не оставалось того, был ли в этот момент приказ.
    /// Из-за этого доля ObeyCommand допускала два противоположных
    /// прочтения: то ли отряд слушается почти идеально, то ли не слушается
    /// в пяти случаях из шести. Разница между хорошей игрой и машиной
    /// фрустрации — а различить было нечем.
    ///
    /// Ориентир, к которому стоит стремиться: отказ примерно в одном
    /// случае из четырёх при отданном приказе. Реже — игрок не заметит
    /// главную механику. Чаще — возненавидит её.
    /// </summary>
    public static class AOSStats
    {
        /// <summary>Разумная доля отказов: нижняя и верхняя границы.</summary>
        public const float HealthyRefusalLow = 0.15f;
        public const float HealthyRefusalHigh = 0.35f;

        private static int _decisions;
        private static int _withCommand;
        private static int _obeyed;
        private static int _refused;
        private static int _hesitated;

        private static readonly Dictionary<ActionType, int> _actions = new();
        private static readonly Dictionary<string, int> _voices = new();

        public static int Decisions => _decisions;
        public static int WithCommand => _withCommand;

        /// <summary>Доля отказов среди решений, принятых при отданном приказе.</summary>
        public static float RefusalRate => _withCommand > 0 ? (float)_refused / _withCommand : 0f;

        public static void Record(Decision decision, DecisionContext context)
        {
            _decisions++;

            Bump(_actions, decision.Action);
            if (!string.IsNullOrEmpty(decision.TopModule)) Bump(_voices, decision.TopModule);
            if (decision.Hesitated) _hesitated++;

            if (context == null || !context.HasCommand) return;

            _withCommand++;
            if (decision.RefusedCommand) _refused++;
            else if (!decision.Hesitated) _obeyed++;
        }

        public static void Reset()
        {
            _decisions = _withCommand = _obeyed = _refused = _hesitated = 0;
            _actions.Clear();
            _voices.Clear();
        }

        /// <summary>Сводка словами. Печатается в конце боя.</summary>
        public static string Report()
        {
            if (_decisions == 0) return "[AOS] Решений не принималось.";

            var sb = new StringBuilder();
            sb.AppendLine($"[AOS] Решений: {_decisions}. Из них при отданном приказе: {_withCommand}.");

            if (_withCommand > 0)
            {
                float rate = RefusalRate;
                string verdict =
                    rate < HealthyRefusalLow ? "редко — главную механику могут не заметить" :
                    rate > HealthyRefusalHigh ? "часто — рискует читаться как поломка" :
                                                "в разумных пределах";
                sb.AppendLine($"[AOS] Приказ выполнен {_obeyed}, отказ {_refused}, "
                            + $"оцепенение {_hesitated}. Отказов {rate * 100f:F0}% — {verdict}.");
            }
            else
            {
                sb.AppendLine("[AOS] Приказов не отдавали — доля отказов не измерена.");
            }

            sb.Append("[AOS] Действия: ").AppendLine(Top(_actions));
            sb.Append("[AOS] Голоса: ").Append(Top(_voices));
            return sb.ToString();
        }

        private static void Bump<T>(Dictionary<T, int> map, T key)
        {
            map.TryGetValue(key, out int n);
            map[key] = n + 1;
        }

        private static string Top<T>(Dictionary<T, int> map)
        {
            if (map.Count == 0) return "нет";

            var items = new List<KeyValuePair<T, int>>(map);
            items.Sort((a, b) => b.Value.CompareTo(a.Value));

            var sb = new StringBuilder();
            int total = 0;
            foreach (var kv in items) total += kv.Value;

            for (int i = 0; i < items.Count && i < 6; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append($"{items[i].Key} {items[i].Value * 100 / Mathf.Max(total, 1)}%");
            }
            return sb.ToString();
        }
    }
}
