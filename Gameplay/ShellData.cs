using UnityEngine;

namespace Sinbinder.Core
{
    /// <summary>
    /// Оболочка: тело, в которое связывают душу.
    ///
    /// До сих пор здесь были имя, здоровье, защита и скорость, и Warrior
    /// эти поля вообще не читал — оболочка была чистой косметикой.
    ///
    /// Главное здесь не характеристики, а смещение спектров. Плоть тянет
    /// душу на себя: тело волка добавляет Гнева и отнимает Терпения.
    /// Диздок говорит «волк в теле человека ведёт себя как волк» —
    /// переворачиваем: душа в теле волка со временем дрейфует к волку.
    ///
    /// Из-за этого сборка воина перестаёт быть таблицей и становится
    /// переговорами. У вас терпеливая душа и только волчья оболочка —
    /// получите терпеливого воина, который чуть менее терпелив, чем был
    /// вчера. И это ваше решение, а не случайность.
    /// </summary>
    [CreateAssetMenu(fileName = "ShellData", menuName = "Sinbinder/Shells/New Shell")]
    public class ShellData : ScriptableObject
    {
        public string shellName;
        public ShellType type = ShellType.Skeleton;

        [Header("Тело")]
        public float baseHP = 20f;
        public float baseDefense = 1f;
        public float movementSpeed = 3.5f;
        public bool canBeRevived;

        [Tooltip("Насколько тело изношено, 0…1. Ветхая оболочка держит хуже.")]
        [Range(0f, 1f)] public float wear;

        [Header("Как плоть тянет душу")]
        [Tooltip("Смещение по семи спектрам. Индекс — SinType: "
               + "0 Жадность, 1 Гордыня, 2 Гнев, 3 Зависть, 4 Похоть, "
               + "5 Чревоугодие, 6 Уныние.")]
        public float[] spectrumBias = new float[SoulData.SpectrumCount];

        [Tooltip("Какая доля смещения оседает при каждом связывании.")]
        [Range(0f, 1f)] public float bindStrength = 0.35f;

        /// <summary>Здоровье с учётом износа.</summary>
        public float EffectiveHP => baseHP * Mathf.Lerp(1f, 0.5f, wear);

        public float GetBias(SinType sin)
        {
            int i = (int)sin;
            if (spectrumBias == null || i < 0 || i >= spectrumBias.Length) return 0f;
            return spectrumBias[i];
        }

        /// <summary>Смещение словами — для экрана сборки, без чисел.</summary>
        public string DescribeBias(float threshold = 5f)
        {
            if (spectrumBias == null) return "Тело ничего не навязывает.";

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < spectrumBias.Length && i < SoulData.SpectrumCount; i++)
            {
                float v = spectrumBias[i];
                if (Mathf.Abs(v) < threshold) continue;

                var sin = (SinType)i;
                sb.AppendLine(v > 0f
                    ? $"Тянет к: {SoulData.GetSinName(sin)}"
                    : $"Отнимает: {SoulData.GetSinName(sin)}");
            }

            return sb.Length == 0 ? "Тело ничего не навязывает." : sb.ToString().TrimEnd();
        }
    }
}
