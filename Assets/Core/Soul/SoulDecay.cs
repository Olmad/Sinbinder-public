// Assets/Core/Soul/SoulDecay.cs
using UnityEngine;

namespace Sinbinder.Core
{
    /// <summary>
    /// Цена потери души.
    ///
    /// Качество уже считалось по времени с момента смерти — и ни на что
    /// не влияло: собранная душа была той же самой, хоть через минуту,
    /// хоть через час. Смерть воина не стоила ничего.
    ///
    /// Теперь стоит. Личность — это спектры и память, и по мере распада
    /// уходит и то и другое: сначала притупляются крайности, потом
    /// осыпается память, под конец остаётся ровная пустая душа без
    /// характера и без прошлого. Собрать её можно, но это уже не тот,
    /// кого вы знали.
    ///
    /// Отсюда у боя появляется вторая ставка помимо победы: успеть.
    /// </summary>
    public static class SoulDecay
    {
        /// <summary>Во сколько раз тускнеют спектры при этом качестве.</summary>
        public static float SpectrumFactor(SoulQuality quality)
        {
            switch (quality)
            {
                case SoulQuality.Shock:      return 1.00f;  // всё при ней
                case SoulQuality.Acceptance: return 0.85f;  // крайности сглаживаются
                case SoulQuality.Fading:     return 0.55f;  // характер тускнеет
                case SoulQuality.Dissolved:  return 0.20f;  // почти ничего не осталось
                default:                     return 1.00f;
            }
        }

        /// <summary>Сохраняется ли врождённая история.</summary>
        public static bool KeepsMemory(SoulQuality quality)
            => quality == SoulQuality.Shock || quality == SoulQuality.Acceptance;

        /// <summary>Сохраняются ли сюжетные перки.</summary>
        public static bool KeepsPerks(SoulQuality quality)
            => quality != SoulQuality.Dissolved;

        public static string Describe(SoulQuality quality)
        {
            switch (quality)
            {
                case SoulQuality.Shock:      return "Ещё не поняла, что мертва";
                case SoulQuality.Acceptance: return "Смирилась. Помнит себя";
                case SoulQuality.Fading:     return "Гаснет. Прошлое расплывается";
                case SoulQuality.Dissolved:  return "Распалась. Осталась оболочка воли";
                default:                     return "";
            }
        }

        /// <summary>
        /// Собранная душа: копия с потерями по качеству.
        /// Возвращает новый экземпляр, исходный не трогает.
        /// </summary>
        public static SoulData Harvest(SoulData source, SoulQuality quality)
        {
            if (source == null) return null;

            float factor = SpectrumFactor(quality);

            var spectra = source.CopySpectra();
            for (int i = 0; i < spectra.Length; i++)
                spectra[i] = Mathf.Round(spectra[i] * factor);

            MemorySeed memory = null;
            if (KeepsMemory(quality) && source.Memory != null)
            {
                memory = source.Memory;
            }
            else if (KeepsPerks(quality) && source.Memory != null)
            {
                // История стёрлась, перки ещё держатся: воин не помнит,
                // откуда у него эта повадка, но повадка осталась.
                memory = new MemorySeed
                {
                    Story = "",
                    NarrativePerks = source.Memory.NarrativePerks
                };
            }

            return new SoulData(source.Name, source.Moral, source.Level, spectra, memory);
        }
    }
}
