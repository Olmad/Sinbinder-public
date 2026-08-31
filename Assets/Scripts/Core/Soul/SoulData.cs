using UnityEngine;

namespace Sinbinder.Core
{
    /// <summary>
    /// Душа: семь спектров, мораль, уровень, врождённая история.
    ///
    /// Каждый спектр от −100 до +100. Положительная половина — грех,
    /// отрицательная — соответствующая добродетель. Щедрость это Жадность
    /// со знаком минус, а не отдельная сущность: семь шкал, не четырнадцать.
    ///
    /// Раньше здесь лежали один грех и одна интенсивность, и все модули
    /// читали одно поле. Из-за этого воин со значением 80 был одновременно
    /// предельно жаден, гневлив и уныл, а все воины отличались только
    /// громкостью. Старые сериализованные души переносятся автоматически:
    /// прежняя интенсивность уезжает в спектр прежнего греха.
    /// </summary>
    [System.Serializable]
    public class SoulData
    {
        public const int SpectrumCount = 7;

        [SerializeField] private string _id;
        [SerializeField] private string _name;
        [SerializeField] private int _moralType;
        [SerializeField] private int _level;
        [SerializeField] private MemorySeed _memory;

        [SerializeField] private float[] _spectra;

        // Наследие одномерной души. Читается только при переносе.
        [SerializeField] private int _sinType;
        [SerializeField] private float _sinIntensity;

        public string Id => _id;
        public string Name => _name;
        public MoralType Moral => (MoralType)_moralType;
        public int Level => _level;
        public MemorySeed Memory => _memory;
        public bool HasMemory => _memory != null && !string.IsNullOrEmpty(_memory.Story);

        // ---------- спектры ----------

        /// <summary>Значение одного спектра, −100…+100.</summary>
        public float Get(SinType sin)
        {
            EnsureSpectra();
            return _spectra[(int)sin];
        }

        public void Set(SinType sin, float value)
        {
            EnsureSpectra();
            _spectra[(int)sin] = Mathf.Clamp(value, -100f, 100f);
        }

        public void Change(SinType sin, float amount)
        {
            EnsureSpectra();
            _spectra[(int)sin] = Mathf.Clamp(_spectra[(int)sin] + amount, -100f, 100f);
        }

        /// <summary>
        /// Доминирующий грех — спектр, наиболее удалённый от нуля.
        /// Именно «наиболее удалённый», а не «наибольший»: святая душа
        /// определяется своей добродетелью так же, как порочная — пороком.
        /// </summary>
        public SinType Sin
        {
            get
            {
                EnsureSpectra();
                int best = 0;
                float bestAbs = Mathf.Abs(_spectra[0]);
                for (int i = 1; i < SpectrumCount; i++)
                {
                    float abs = Mathf.Abs(_spectra[i]);
                    if (abs > bestAbs) { bestAbs = abs; best = i; }
                }
                // Пустая душа: сохраняем прежнее поведение вместо «всегда Жадность».
                if (bestAbs <= 0f && _sinType >= 0 && _sinType < SpectrumCount)
                    return (SinType)_sinType;
                return (SinType)best;
            }
        }

        /// <summary>Величина доминирующего спектра. Оставлено для совместимости.</summary>
        public float SinIntensity => Get(Sin);

        /// <summary>
        /// Общая склонность души ко греху: среднее по всем спектрам.
        /// Отрицательное значение — добродетельная душа.
        /// </summary>
        public float AverageSpectrum
        {
            get
            {
                EnsureSpectra();
                float sum = 0f;
                for (int i = 0; i < SpectrumCount; i++) sum += _spectra[i];
                return sum / SpectrumCount;
            }
        }

        /// <summary>Копия спектров. Менять надо через Set и Change.</summary>
        public float[] CopySpectra()
        {
            EnsureSpectra();
            var copy = new float[SpectrumCount];
            System.Array.Copy(_spectra, copy, SpectrumCount);
            return copy;
        }

        private void EnsureSpectra()
        {
            if (_spectra != null && _spectra.Length == SpectrumCount) return;

            var old = _spectra;
            _spectra = new float[SpectrumCount];

            bool carried = false;
            if (old != null)
            {
                int n = Mathf.Min(old.Length, SpectrumCount);
                for (int i = 0; i < n; i++)
                {
                    _spectra[i] = old[i];
                    if (old[i] != 0f) carried = true;
                }
            }

            // Перенос одномерной души: старая интенсивность в спектр старого греха.
            if (!carried && _sinIntensity != 0f && _sinType >= 0 && _sinType < SpectrumCount)
                _spectra[_sinType] = Mathf.Clamp(_sinIntensity, -100f, 100f);
        }

        // ---------- создание ----------

        /// <summary>Душа с одним выраженным грехом. Прежняя сигнатура.</summary>
        public SoulData(string name, SinType sin, MoralType moral, int level,
            float sinIntensity = 0f, MemorySeed memory = null)
        {
            _id = System.Guid.NewGuid().ToString();
            _name = name;
            _moralType = (int)moral;
            _level = level;
            _memory = memory;

            _sinType = (int)sin;
            _sinIntensity = Mathf.Clamp(sinIntensity, -100f, 100f);

            _spectra = new float[SpectrumCount];
            _spectra[(int)sin] = _sinIntensity;
        }

        /// <summary>Душа со всеми семью спектрами сразу.</summary>
        public SoulData(string name, MoralType moral, int level, float[] spectra,
            MemorySeed memory = null)
        {
            _id = System.Guid.NewGuid().ToString();
            _name = name;
            _moralType = (int)moral;
            _level = level;
            _memory = memory;

            _spectra = new float[SpectrumCount];
            if (spectra != null)
            {
                int n = Mathf.Min(spectra.Length, SpectrumCount);
                for (int i = 0; i < n; i++)
                    _spectra[i] = Mathf.Clamp(spectra[i], -100f, 100f);
            }

            _sinType = (int)Sin;
            _sinIntensity = SinIntensity;
        }

        /// <summary>
        /// Копия чужой души с новым идентификатором. Нужна при жатве:
        /// вынутая душа — та же личность, но уже другой экземпляр.
        /// </summary>
        public SoulData(SoulData other)
        {
            _id = System.Guid.NewGuid().ToString();
            _name = other._name;
            _moralType = other._moralType;
            _level = other._level;
            _memory = other._memory;

            _spectra = other.CopySpectra();
            _sinType = (int)other.Sin;
            _sinIntensity = other.SinIntensity;
        }

        // ---------- описания ----------

        public string GetSinName() => GetSinName(Sin);

        public static string GetSinName(SinType sin)
        {
            string[] names = { "Жадность", "Гордыня", "Гнев", "Зависть", "Похоть", "Чревоугодие", "Уныние" };
            return names[(int)sin];
        }

        public static string GetVirtueName(SinType sin)
        {
            string[] names = { "Щедрость", "Смирение", "Терпение", "Доброжелательность", "Целомудрие", "Умеренность", "Усердие" };
            return names[(int)sin];
        }

        public string GetMoralName()
        {
            string[] names = { "Злобная", "Нейтральная", "Благочестивая" };
            return names[_moralType];
        }

        public VirtueType GetVirtueType() => (VirtueType)(int)Sin;

        /// <summary>Изменить доминирующий спектр. Оставлено для совместимости.</summary>
        public void ChangeIntensity(float amount) => Change(Sin, amount);

        public string GetIntensityDescription() => GetIntensityDescription(Sin);

        public string GetIntensityDescription(SinType sin)
        {
            float value = Get(sin);
            if (value > 60f) return GetIntensityText(sin, 2);
            if (value > 10f) return GetIntensityText(sin, 1);
            if (value > -10f) return GetIntensityText(sin, 0);
            if (value > -60f) return GetIntensityText(sin, -1);
            return GetIntensityText(sin, -2);
        }

        private string GetIntensityText(SinType sin, int tier)
        {
            string[][] descriptions = {
                new[] { "Одержим золотом до безумия", "Любит золото", "Ценит золото", "Равнодушен к золоту", "Презирает богатство" },
                new[] { "Считает себя богом", "Высокомерен", "Уверен в себе", "Признаёт других", "Скромен до самоуничижения" },
                new[] { "Неудержим в ярости", "Вспыльчив", "Сдержан", "Терпелив", "Невозмутим как камень" },
                new[] { "Завидует всему живому", "Завидует молча", "Нейтрален", "Рад за других", "Восхищается другими" },
                new[] { "Ненасытен в желаниях", "Падок на соблазны", "Сдержан", "Верен", "Неприступен" },
                new[] { "Готов сожрать всё", "Любит поесть", "Ест в меру", "Умерен", "Воздержан" },
                new[] { "Апатичен ко всему", "Ленив", "Работает без огня", "Старателен", "Неутомим" }
            };
            return descriptions[(int)sin][tier + 2];
        }

        public string GetFullDescription()
        {
            string desc = $"Душа: {Name}\n";
            desc += $"Грех: {GetSinName()}\n";
            desc += $"Степень: {GetIntensityDescription()}\n";
            desc += $"Мораль: {GetMoralName()}\n";
            desc += $"Уровень: {Level}";
            return desc;
        }

        /// <summary>
        /// Все семь спектров словами — для экрана сборки и осмотра души.
        /// Показываются только выраженные: пустые строки читателю не нужны.
        /// </summary>
        public string GetSpectraDescription(float threshold = 10f)
        {
            EnsureSpectra();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < SpectrumCount; i++)
            {
                float v = _spectra[i];
                if (Mathf.Abs(v) < threshold) continue;
                var sin = (SinType)i;
                string label = v > 0f ? GetSinName(sin) : GetVirtueName(sin);
                sb.AppendLine($"{label}: {GetIntensityDescription(sin)}");
            }
            if (sb.Length == 0) sb.AppendLine("Ничем не выделяется.");
            return sb.ToString().TrimEnd();
        }
    }
}
