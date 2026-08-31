using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.Inventory
{
    public class PlayerInventory : MonoBehaviour
    {
        public static PlayerInventory Instance { get; private set; }

        [SerializeField] private int _maxSlots = 8;
        [SerializeField] private int _baseMaxSlots = 50;

        private List<InventoryItem> _items = new();
        private List<StorageItem> _storage = new();
        private int _gold = 0;

        public System.Action OnInventoryChanged;
        public System.Action OnStorageChanged;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public bool AddItem(InventoryItem newItem)
        {
            if (newItem.Type == ItemType.Gold)
            {
                _gold += newItem.Quantity;
                OnInventoryChanged?.Invoke();
                return true;
            }

            if (_items.Count >= _maxSlots)
            {
                Debug.Log("[INVENTORY] Нет места!");
                return false;
            }

            _items.Add(newItem);
            OnInventoryChanged?.Invoke();
            return true;
        }

        public bool RemoveItem(string itemId)
        {
            var item = _items.Find(i => i.Id == itemId);
            if (item == null) return false;
            _items.Remove(item);
            OnInventoryChanged?.Invoke();
            return true;
        }

        public List<InventoryItem> GetAllItems() => _items;
        public int Count => _items.Count;
        public int MaxSlots => _maxSlots;
        public int Gold => _gold;

        public void AddGold(int amount)
        {
            _gold += amount;
            OnInventoryChanged?.Invoke();
        }

        public bool SpendGold(int amount)
        {
            if (_gold < amount) return false;
            _gold -= amount;
            OnInventoryChanged?.Invoke();
            return true;
        }

        public bool AddToStorage(StorageItem newItem)
        {
            foreach (var item in _storage)
            {
                if (item.Name == newItem.Name && item.Type == newItem.Type)
                {
                    int space = item.MaxStack - item.Quantity;
                    int toAdd = Mathf.Min(space, newItem.Quantity);
                    item.Quantity += toAdd;
                    newItem.Quantity -= toAdd;
                    if (newItem.Quantity <= 0)
                    {
                        OnStorageChanged?.Invoke();
                        return true;
                    }
                }
            }

            if (_storage.Count >= _baseMaxSlots) return false;
            _storage.Add(newItem);
            OnStorageChanged?.Invoke();
            return true;
        }

        public List<StorageItem> GetAllStorage() => _storage;
    }
}