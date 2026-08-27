using UnityEngine;

namespace Sinbinder.Core
{
    [System.Serializable]
    public class SoulData
    {
        [SerializeField] private string _id;
        [SerializeField] private string _name;
        [SerializeField] private int _sinType;
        [SerializeField] private int _moralType;
        [SerializeField] private int _level;
        [SerializeField] private float _sinIntensity;
        [SerializeField] private MemorySeed _memory;

        public string Id => _id;
        public string Name => _name;
        public SinType Sin => (SinType)_sinType;
        public MoralType Moral => (MoralType)_moralType;
        public int Level => _level;
        public float SinIntensity => _sinIntensity;
        public MemorySeed Memory => _memory;
        public bool HasMemory => _memory != null && !string.IsNullOrEmpty(_memory.Story);

        public SoulData(string name, SinType sin, MoralType moral, int level, float sinIntensity = 0f, MemorySeed memory = null)
        {
            _id = System.Guid.NewGuid().ToString();
            _name = name;
            _sinType = (int)sin;
            _moralType = (int)moral;
            _level = level;
            _sinIntensity = Mathf.Clamp(sinIntensity, -100f, 100f);
            _memory = memory;
        }

        public string GetSinName()
        {
            string[] names = { "Жадность", "Гордыня", "Гнев", "Зависть", "Похоть", "Чревоугодие", "Уныние" };
            return names[_sinType];
        }

        public string GetMoralName()
        {
            string[] names = { "Злобная", "Нейтральная", "Благочестивая" };
            return names[_moralType];
        }

        public VirtueType GetVirtueType()
        {
            return (VirtueType)_sinType;
        }

        public void ChangeIntensity(float amount)
        {
            _sinIntensity = Mathf.Clamp(_sinIntensity + amount, -100f, 100f);
        }

        public string GetIntensityDescription()
        {
            if (_sinIntensity > 60f) return GetIntensityText(2);
            if (_sinIntensity > 10f) return GetIntensityText(1);
            if (_sinIntensity > -10f) return GetIntensityText(0);
            if (_sinIntensity > -60f) return GetIntensityText(-1);
            return GetIntensityText(-2);
        }

        private string GetIntensityText(int tier)
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

            int idx = tier + 2;
            return descriptions[_sinType][idx];
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
    }
}