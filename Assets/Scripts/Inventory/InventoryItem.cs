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

        public string Id => _id;
        public string Name => _name;
        public string Description => _description;
        public ItemType Type => _type;
        public int Quantity => _quantity;
        public SinType TemptationSin => _temptationSin;
        public float TemptationValue => _temptationValue;

        public InventoryItem(string name, string description, ItemType type, int quantity = 1,
            SinType temptationSin = SinType.Greed, float temptationValue = 0f)
        {
            _id = System.Guid.NewGuid().ToString();
            _name = name;
            _description = description;
            _type = type;
            _quantity = quantity;
            _temptationSin = temptationSin;
            _temptationValue = temptationValue;
        }
    }
}