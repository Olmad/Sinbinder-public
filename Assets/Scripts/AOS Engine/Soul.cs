// Assets/Scripts/AOS Engine/Soul.cs
using UnityEngine;
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.AOS
{
    /// <summary>
    /// Срез души для модулей: то, на что они имеют право смотреть.
    ///
    /// Модуль не должен знать про Warrior, сцену и Unity — он переводит
    /// характер в очки, и всё. Поэтому между ними стоит этот срез.
    /// </summary>
    public class Soul
    {
        public string Name;

        /// <summary>Семь спектров, −100…+100. Индекс — (int)SinType.</summary>
        public float[] Spectra = new float[SoulData.SpectrumCount];

        public MoralityType Morality;
        public float Loyalty;

        /// <summary>Значение одного спектра.</summary>
        public float Get(SinType sin)
        {
            int i = (int)sin;
            if (Spectra == null || i < 0 || i >= Spectra.Length) return 0f;
            return Spectra[i];
        }

        /// <summary>Доминирующий спектр — наиболее удалённый от нуля.</summary>
        public float SinIntensity
        {
            get
            {
                if (Spectra == null) return 0f;
                float best = 0f;
                foreach (var v in Spectra)
                    if (Mathf.Abs(v) > Mathf.Abs(best)) best = v;
                return best;
            }
        }

        /// <summary>Общая склонность ко греху: среднее по спектрам.</summary>
        public float Average
        {
            get
            {
                if (Spectra == null || Spectra.Length == 0) return 0f;
                float sum = 0f;
                foreach (var v in Spectra) sum += v;
                return sum / Spectra.Length;
            }
        }

        public static Soul FromWarrior(Warrior warrior)
        {
            return new Soul
            {
                Name = warrior.DisplayName,
                Spectra = warrior.Soul.CopySpectra(),
                Morality = (MoralityType)(int)warrior.Soul.Moral,
                Loyalty = warrior.Loyalty
            };
        }
    }

    /// <summary>Временный enum. В будущем будет заменён на MoralType из Core.</summary>
    public enum MoralityType
    {
        Vicious,
        Neutral,
        Pious
    }
}
