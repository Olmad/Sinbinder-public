using UnityEngine;

namespace Sinbinder.Gameplay
{
    [System.Serializable]
    public class BattlefieldData
    {
        public string Id;
        public string Name;
        public Vector3 Position;
        public float RemainingTime;
        public int TotalBodies;
        public int TotalSouls;
        public int GoldValue;
        public bool HasEquipment;
        public bool IsCollected;

        public BattlefieldData(string name, Vector3 position, int bodies, int souls, int gold, bool equipment)
        {
            Id = System.Guid.NewGuid().ToString();
            Name = name;
            Position = position;
            RemainingTime = 300f;
            TotalBodies = bodies;
            TotalSouls = souls;
            GoldValue = gold;
            HasEquipment = equipment;
            IsCollected = false;
        }
    }
}