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

        /// <summary>
        /// Приказ уводит из боя. Гордыня и Верность читают именно это,
        /// а не строку CommandType: модуль не должен разбирать приказы
        /// игрока по названию.
        /// </summary>
        public bool CommandLeavesFight;

        /// <summary>
        /// Приказ был именно «отходи», а не «иди туда».
        ///
        /// Единственный приказ, который исполняется и бегством: воин,
        /// побежавший от врага, когда велено отходить, сделал то, о чём
        /// просили. Отсюда это поле читает резолвер, решая, был ли отказ.
        /// </summary>
        public bool CommandIsFallBack;

        /// <summary>
        /// Сделал ли воин то, о чём просили, — даже если выбрал это
        /// по своей причине, а не из послушания.
        ///
        /// Приказ «в атаку» гневному, который и так рвётся драться,
        /// раньше засчитывался отказом: голосование выбирало Attack,
        /// а не ObeyCommand, и воин с командиром получали обоюдную
        /// память о непослушании за то, что приказ был исполнен.
        /// На прогоне таких ложных отказов оказалось около восьмой части.
        ///
        /// Для отхода это уже было разобрано (Flee при CommandIsFallBack).
        /// Здесь то же правило распространено на остальные приказы.
        /// </summary>
        public bool SatisfiedBy(ActionType action)
        {
            if (!HasCommand) return false;
            if (action == ActionType.ObeyCommand) return true;

            switch (CommandType)
            {
                case "Attack":   return action == ActionType.Attack;
                case "Hold":     return action == ActionType.Idle;
                case "Defend":   return action == ActionType.Idle || action == ActionType.Attack;
                case "FallBack": return action == ActionType.Flee;
                default:         return CommandIsFallBack && action == ActionType.Flee;
            }
        }

        // Предметы, которые несёт воин
        public List<InventoryItem> CarriedItems = new List<InventoryItem>();

        // Сюжетные перки воина. Читает PerkResolver.
        public List<Sinbinder.Core.NarrativePerk> AvailableNarrativePerks
            = new List<Sinbinder.Core.NarrativePerk>();
    }
}