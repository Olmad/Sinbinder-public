using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.Core
{
    /// <summary>
    /// Оболочки как ассеты: Resources/Shells/&lt;ShellType&gt;.
    ///
    /// До этого класса <see cref="ShellData"/> был написан, прочитан
    /// экраном сборки и <see cref="ShellBinder"/>, но не существовал:
    /// все спавнеры звали перегрузку <c>Initialize(..., ShellType, ...)</c>,
    /// а она про ассеты не знала. Оболочка снова оказывалась косметикой —
    /// на этот раз не потому, что её не читали, а потому, что её нечем
    /// было заполнить.
    ///
    /// Отсутствие ассетов не выключает игру: тело берётся по умолчанию,
    /// а предупреждение выдаётся один раз — тем же правилом, что и
    /// <see cref="AOS.AOSConfig.Load"/>.
    /// </summary>
    public static class ShellLibrary
    {
        private const string Folder = "Shells/";

        private static readonly Dictionary<ShellType, ShellData> _cache = new();
        private static bool _warned;

        /// <summary>Оболочка по типу или null, если ассета нет.</summary>
        public static ShellData Get(ShellType type)
        {
            if (_cache.TryGetValue(type, out var cached)) return cached;

            var shell = Resources.Load<ShellData>(Folder + type);
            _cache[type] = shell;

            if (shell == null && !_warned)
            {
                _warned = true;
                Debug.LogWarning($"[Оболочки] Resources/{Folder}{type} не найден. "
                    + "Тела берутся по умолчанию, смещение спектров не оседает — "
                    + "собери оболочки через Sinbinder → Собрать ассеты демо.");
            }

            return shell;
        }

        /// <summary>Сбросить кэш — нужен после пересборки ассетов в редакторе.</summary>
        public static void Forget()
        {
            _cache.Clear();
            _warned = false;
        }
    }
}
