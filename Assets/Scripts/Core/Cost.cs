// Assets/Scripts/Core/Cost.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using System.Diagnostics;

namespace Sinbinder.Core
{
    /// <summary>
    /// Счётчик цены: сколько миллисекунд за окно ушло на «решения», «гибель» и прочее.
    /// Включает его только нагрузочный стенд (<see cref="Gameplay.TugBench"/>): выключенный
    /// он стоит одну проверку флага. Нужен потому, что в бою на 400 кадр вырастал вдесятеро,
    /// а раскладка выключателями путала слои с самим боем (27.10).
    ///
    /// <code>using (Cost.Of("решение")) { ... }</code>
    /// </summary>
    public static class Cost
    {
        public static bool On { get; set; }

        private static readonly Dictionary<string, (long ticks, int calls)> Spent = new();

        public readonly struct Span : System.IDisposable
        {
            private readonly string _what;
            private readonly long _from;

            internal Span(string what) { _what = what; _from = what != null ? Stopwatch.GetTimestamp() : 0; }

            public void Dispose()
            {
                if (_what == null) return;
                long dt = Stopwatch.GetTimestamp() - _from;
                Spent.TryGetValue(_what, out var was);
                Spent[_what] = (was.ticks + dt, was.calls + 1);
            }
        }

        public static Span Of(string what) => new(On && what != null ? what : null);

        /// <summary>Сводка за окно и обнулить: «решение 41,2 мс / 1180 раз; …».</summary>
        public static string Drain()
        {
            var parts = new List<string>();
            foreach (var kv in Spent)
                parts.Add(Loc.F("{0} {1:F0} мс/{2}", kv.Key, kv.Value.ticks * 1000.0 / Stopwatch.Frequency, kv.Value.calls));
            Spent.Clear();
            parts.Sort(System.StringComparer.Ordinal);
            return string.Join("; ", parts);
        }
    }
}
