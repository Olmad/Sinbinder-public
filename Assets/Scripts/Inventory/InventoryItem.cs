// Assets/Scripts/Inventory/InventoryItem.cs
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Inventory
{
    public enum ItemType
    {
        Soul,
        Shell,
        Equipment,
        Artifact,
        Gold,
        Provision
    }

    /// <summary>
    /// Место на воине (docs/34-GEAR.md, решение автора 24 сентября):
    /// оружие, щит или второе оружие, шлем, броня, пояс. Одно место —
    /// одна вещь: второй шлем не прибавляет второй головы, он встаёт
    /// вместо первого.
    /// </summary>
    public enum GearSlot
    {
        None,       // не носится: золото, души, оболочки
        Weapon,
        Offhand,    // щит — или второе оружие
        Head,
        Body,
        Belt        // трофей, оберег, припас
    }

    [System.Serializable]
    public class InventoryItem
    {
        [SerializeField] private string _id;
        [SerializeField] private string _name;
        [SerializeField] private string _description;
        [SerializeField] private ItemType _type;
        [SerializeField] private int _quantity;

        // Новые поля для влияния на AOS
        [SerializeField] private SinType _temptationSin;  // Какой грех усиливает предмет
        [SerializeField] private float _temptationValue;  // На сколько усиливает (0..100)

        // Что вещь даёт телу в руках. Удар и защита воина — от оболочки
        // плюс от того, что он несёт (Warrior.Attack, Warrior.Defense).
        [SerializeField] private float _attackBonus;
        [SerializeField] private float _defenseBonus;

        // Куда вещь надевают. Ноль у старых записей — тогда место
        // выводится из того, что вещь даёт (см. Slot).
        [SerializeField] private GearSlot _slot;

        public string Id => _id;
        public string Name => _name;
        public string Description => _description;
        public ItemType Type => _type;
        public int Quantity => _quantity;
        public SinType TemptationSin => _temptationSin;
        public float TemptationValue => _temptationValue;
        public float AttackBonus => _attackBonus;
        public float DefenseBonus => _defenseBonus;

        /// <summary>
        /// Куда вещь надевают. Не названо — бьёт, значит оружие; держит
        /// удар — броня; прочее — на пояс. Золото, души и оболочки
        /// не надевают вовсе.
        /// </summary>
        public GearSlot Slot
        {
            get
            {
                if (_type == ItemType.Gold || _type == ItemType.Soul || _type == ItemType.Shell)
                    return GearSlot.None;
                if (_slot != GearSlot.None) return _slot;
                if (_attackBonus > 0f) return GearSlot.Weapon;
                if (_defenseBonus > 0f) return GearSlot.Body;
                return GearSlot.Belt;
            }
        }

        /// <summary>Встаёт ли вещь на это место. Оружие встаёт и во вторую руку.</summary>
        public bool Fits(GearSlot place)
        {
            var own = Slot;
            if (own == GearSlot.None || place == GearSlot.None) return false;
            if (place == GearSlot.Offhand) return own == GearSlot.Offhand || own == GearSlot.Weapon;
            return own == place;
        }

        public InventoryItem(string name, string description, ItemType type, int quantity = 1,
            SinType temptationSin = SinType.Greed, float temptationValue = 0f,
            float attack = 0f, float defense = 0f, GearSlot slot = GearSlot.None)
        {
            _id = System.Guid.NewGuid().ToString();
            _name = name;
            _description = description;
            _type = type;
            _quantity = quantity;
            _temptationSin = temptationSin;
            _temptationValue = temptationValue;
            _attackBonus = attack;
            _defenseBonus = defense;
            _slot = slot;
        }
    }
}