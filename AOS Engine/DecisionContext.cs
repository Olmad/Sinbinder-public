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
        public Warrior Commander;
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

        // ---------- положение на поле ----------
        // Пол игры: величины, которые есть у боя сами по себе, и которые
        // при этом читают модули личности. Механика, которую грехи не
        // читают, была бы второй игрой сбоку.

        /// <summary>Насколько воин вымотан, 0…1.</summary>
        public float Fatigue;

        /// <summary>Сил почти не осталось.</summary>
        public bool IsExhausted;

        /// <summary>Со сколькими противниками воин сцеплен в ближнем бою.</summary>
        public int EngagedWith;

        /// <summary>Сцеплен хотя бы с одним: уйти отсюда стоит удара вслед.</summary>
        public bool IsEngaged;

        /// <summary>Двое и больше: защита падает.</summary>
        public bool Surrounded;

        /// <summary>У ближайшей цели открыта спина.</summary>
        public bool TargetBackExposed;

        // Предметы, которые несёт воин
        public List<InventoryItem> CarriedItems = new List<InventoryItem>();

        // Сюжетные перки воина. Читает PerkResolver.
        public List<Sinbinder.Core.NarrativePerk> AvailableNarrativePerks
            = new List<Sinbinder.Core.NarrativePerk>();
    }
}