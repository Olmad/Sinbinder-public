// Assets/Scripts/Crypt/CryptHands.cs
using Sinbinder.Core;

namespace Sinbinder.Crypt
{
    /// <summary>
    /// Что Греховод несёт в руках.
    ///
    /// Одно за раз — и это не упрощение, а смысл зоны. «Принести душу
    /// к устройству» должно быть <b>ходьбой</b>, иначе связывание
    /// сворачивается обратно в меню, из которого его и вынимали.
    /// Руки, которые вмещают всё, — это инвентарь, а инвентарь ходить
    /// не заставляет.
    ///
    /// Статика с <see cref="Forget"/>, как <c>SquadRoster</c> и прочие
    /// в проекте: Греховод один, а сцена меняется. Не забыть руки при
    /// смене сцены — значит принести в новый склеп банку из старого.
    /// </summary>
    public static class CryptHands
    {
        public static SoulData Soul { get; private set; }
        public static SoulQuality Quality { get; private set; }

        public static bool HasShell { get; private set; }
        public static ShellType Shell { get; private set; }

        public static bool HasSoul => Soul != null;
        public static bool Empty => !HasSoul && !HasShell;

        /// <summary>Что именно в руках — словами, для подсказок.</summary>
        public static string What
        {
            get
            {
                if (HasSoul) return $"душа: {Soul.Name}";
                if (HasShell) return $"тело: {ShellName(Shell)}";
                return "руки пусты";
            }
        }

        public static bool TakeSoul(SoulData soul, SoulQuality quality)
        {
            if (!Empty || soul == null) return false;

            Soul = soul;
            Quality = quality;
            return true;
        }

        public static bool TakeShell(ShellType shell)
        {
            if (!Empty) return false;

            HasShell = true;
            Shell = shell;
            return true;
        }

        public static void Drop()
        {
            Soul = null;
            HasShell = false;
        }

        public static void Forget() => Drop();

        public static string ShellName(ShellType type)
        {
            var data = ShellLibrary.Get(type);
            if (data != null && !string.IsNullOrEmpty(data.shellName)) return data.shellName;

            switch (type)
            {
                case ShellType.Skeleton: return "Скелет";
                case ShellType.Zombie:   return "Зомби";
                case ShellType.Ghost:    return "Призрак";
                case ShellType.Golem:    return "Голем";
                default:                 return type.ToString();
            }
        }
    }
}
