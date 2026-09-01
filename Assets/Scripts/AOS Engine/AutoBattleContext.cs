// Assets/Scripts/AOS Engine/AutoBattleContext.cs
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Gameplay;
using Sinbinder.Inventory;

namespace Sinbinder.AOS
{
    /// <summary>
    /// Контекст решения для боя, которого нет на сцене.
    ///
    /// CombatDecisionContext читает живую сцену: спрашивает у
    /// CombatManager, кто рядом, и меряет расстояния по transform.
    /// Для автономной вылазки это не работает по определению —
    /// отряд ушёл, сцены нет. Если CombatManager при этом пуст,
    /// воин видит мир без врагов, без союзников и без добычи;
    /// в бюллетене остаётся одно действие «стоять», и весь бой
    /// проходит в неподвижности.
    ///
    /// Здесь тот же контекст собирается из списков: кто в отряде,
    /// кто против, у кого сколько здоровья. Никаких координат —
    /// в автономном бою нет ни позиций, ни спины, ни зацепления.
    /// </summary>
    public static class AutoBattleContext
    {
        public static DecisionContext Create(Warrior warrior, List<Warrior> allies, List<Warrior> enemies)
        {
            int liveEnemies = 0;
            foreach (var e in enemies) if (e != null && !e.IsDead) liveEnemies++;

            int liveAllies = 0;
            bool allyInDanger = false;
            Warrior wounded = null;

            foreach (var a in allies)
            {
                if (a == null || a.IsDead || a == warrior) continue;
                liveAllies++;
                if (a.HP < a.MaxHP * 0.5f)
                {
                    allyInDanger = true;
                    if (wounded == null || a.HP < wounded.HP) wounded = a;
                }
            }

            float hpRatio = warrior.MaxHP > 0f ? warrior.HP / warrior.MaxHP : 1f;

            var context = new DecisionContext
            {
                CurrentHP = warrior.HP,
                MaxHP = warrior.MaxHP,
                NearbyEnemies = liveEnemies,
                NearbyAllies = liveAllies,
                AllyInDanger = allyInDanger,
                TargetWarrior = wounded,
                UnpaidMissions = warrior.UnpaidMissions,
                DangerLevel = Mathf.Clamp01((1f - hpRatio) * 0.5f + Mathf.Clamp01(liveEnemies / 4f) * 0.5f),
                RecentMemories = MemoryProcessor.Instance != null
                    ? MemoryProcessor.Instance.GetMemories(warrior)
                    : new List<MemoryRecord>(),
                CarriedItems = new List<InventoryItem>()
            };

            // Отряд без командира — тоже положение, и его надо отразить честно.
            foreach (var a in allies)
            {
                if (a == null || a.IsDead || !a.IsCommander) continue;
                context.Commander = a;
                context.RelationshipWithCommander = warrior.Relationships != null
                    ? warrior.Relationships.GetRelationship(warrior.Id, a.Id)
                    : 50f;
                break;
            }

            // Последний на ногах — это читают Гордыня и Страх.
            context.LastAlive = liveAllies == 0 && liveEnemies > 0 && !warrior.IsDead;

            if (warrior.Soul != null && warrior.Soul.HasMemory
                && warrior.Soul.Memory.NarrativePerks != null)
                context.AvailableNarrativePerks = warrior.Soul.Memory.NarrativePerks;

            return context;
        }
    }
}
