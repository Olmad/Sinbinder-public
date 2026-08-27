// Assets/_Project/Scripts/AOS/DecisionContext.cs
using System.Collections.Generic;
using Sinbinder.Gameplay;
using Sinbinder.Inventory;

namespace Sinbinder.AOS
{
    public class DecisionContext
    {
        public float CurrentHP;
        public float MaxHP;
        public int NearbyEnemies;
        public int NearbyAllies;
        public bool AllyInDanger;
        public int NearbyLoot;
        public float DangerLevel;
        public bool HasCommand;
        public string CommandType;
        public int UnpaidMissions;
        public Warrior TargetWarrior;
        public float RelationshipWithCommander;
        public List<MemoryRecord> RecentMemories;
        public bool EnemyIsUndead;
public bool EnemyIsPlant;
public bool EnemyIsBeast;
public bool EnemyIsBandit;
public bool EnemyIsHunter;
public bool EnemyIsMagister;
public bool EnemyIsPrisoner;
public bool BrotherNearby;
public bool AllyDamagedRecently;
public bool LastAlive;

        // НОВОЕ: Предметы, которые несёт воин
        public List<InventoryItem> CarriedItems = new List<InventoryItem>();
    }
}