using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    public class HarvestableBody : MonoBehaviour
    {
        [SerializeField] private ShellType _shell;
        [SerializeField] private int _goldValue;
        [SerializeField] private bool _hasEquipment;
        [SerializeField] private string _equipmentName;

        private bool _collected = false;

        public ShellType Shell => _shell;
        public int GoldValue => _goldValue;
        public bool HasEquipment => _hasEquipment;
        public string EquipmentName => _equipmentName;
        public bool IsCollected => _collected;

        public void Initialize(ShellType shell, int gold, bool equipment, string equipName)
        {
            _shell = shell;
            _goldValue = gold;
            _hasEquipment = equipment;
            _equipmentName = equipName;
        }

        public int CollectGold()
        {
            if (_collected) return 0;
            int gold = _goldValue;
            _goldValue = 0;
            Debug.Log($"[LOOT] +{gold} золота");
            return gold;
        }

        public string CollectEquipment()
        {
            if (_collected || !_hasEquipment) return null;
            _hasEquipment = false;
            Debug.Log($"[LOOT] +{_equipmentName}");
            return _equipmentName;
        }

        public void MarkCollected()
        {
            _collected = true;
        }
    }
}