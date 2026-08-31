using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Inventory
{
    public enum StorageItemType
    {
        Soul,
        Shell,
        Corpses,
        Bones,
        GolemCores,
        Provisions,
        Equipment
    }

    [System.Serializable]
    public class StorageItem
    {
        [SerializeField] private string _name;
        [SerializeField] private StorageItemType _type;
        [SerializeField] private int _quantity;
        [SerializeField] private int _maxStack;

        public string Name => _name;
        public StorageItemType Type => _type;
        public int Quantity { get => _quantity; set => _quantity = value; }
        public int MaxStack => _maxStack;

        public StorageItem(string name, StorageItemType type, int quantity, int maxStack)
        {
            _name = name;
            _type = type;
            _quantity = quantity;
            _maxStack = maxStack;
        }
    }
}