// Assets/AOS Engine/CombatDecisionContext.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.AOS
{
    public static class CombatDecisionContext
    {
        public static DecisionContext Create(Warrior warrior, string commandType = "")
        {
            // Используем CombatManager вместо FindObjectsOfType
            var enemies = GetNearbyEnemies(warrior);
            var allies = GetNearbyAllies(warrior);
            var loot = GetNearbyLoot(warrior);

            Warrior targetAlly = null;
            foreach (var ally in allies)
            {
                if (ally.HP < ally.MaxHP * 0.5f)
                {
                    targetAlly = ally;
                    break;
                }
            }

            float relationshipWithCommander = 50f;
            Warrior commander = null;
            if (warrior.Team == Team.Player)
            {
                commander = GetCommander(warrior);
                if (commander != null)
                    relationshipWithCommander = warrior.Relationships.GetRelationship(warrior.Id, commander.Id);
            }
            var context = new DecisionContext
            {
                CurrentHP = warrior.HP,
                MaxHP = warrior.MaxHP,
                NearbyEnemies = enemies.Count,
                NearbyAllies = allies.Count,
                AllyInDanger = allies.Exists(a => a.HP < a.MaxHP * 0.3f),
                NearbyLoot = loot.Count,
                DangerLevel = CalculateDanger(warrior, enemies),
                HasCommand = !string.IsNullOrEmpty(commandType),
                CommandType = commandType,
                UnpaidMissions = warrior.UnpaidMissions,
                TargetWarrior = targetAlly,
                RelationshipWithCommander = relationshipWithCommander,
                RecentMemories = MemoryProcessor.Instance?.GetMemories(warrior) ?? new List<MemoryRecord>(),
                Commander = commander // ← новое поле
            };

            // Положение на поле: усталость, зацепление, открытая спина.
            var fatigue = warrior.GetComponent<Fatigue>();
            if (fatigue != null)
            {
                context.Fatigue = fatigue.Spent;
                context.IsExhausted = fatigue.IsExhausted;
            }

            var engagement = warrior.GetComponent<Engagement>();
            if (engagement != null)
            {
                context.EngagedWith = engagement.Count;
                context.IsEngaged = engagement.IsEngaged;
                context.Surrounded = engagement.IsSurrounded;
            }

            // Есть ли рядом противник, которого можно ударить в спину.
            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy.IsDead) continue;
                if (Facing.IsFromBehind(enemy.transform, warrior.transform.position))
                {
                    context.TargetBackExposed = true;
                    break;
                }
            }

            // Типы противников. Пока читаются по оболочке и по имени;
            // когда у врагов появятся теги, переехать на них.
            context.EnemyIsUndead = enemies.Any(e => e.Shell == ShellType.Skeleton || e.Shell == ShellType.Zombie);
            context.EnemyIsPlant = enemies.Any(e => e.Shell == ShellType.Golem);
            context.EnemyIsBeast = enemies.Any(e => e.Shell == ShellType.Ghost);
            context.EnemyIsBandit = enemies.Any(e => e.DisplayName.Contains("Бандит"));
            context.EnemyIsHunter = enemies.Any(e => e.DisplayName.Contains("Охотник"));
            context.EnemyIsMagister = enemies.Any(e => e.DisplayName.Contains("Магистр"));
            context.EnemyIsPrisoner = enemies.Any(e => e.DisplayName.Contains("Заключённый"));

            // Брат рядом: перк "Брат по оружию" есть и у воина, и у кого-то из своих.
            if (warrior.Soul.Memory?.NarrativePerks != null
                && warrior.Soul.Memory.NarrativePerks.Exists(p => p.PerkName == "Брат по оружию"))
            {
                context.BrotherNearby = allies.Any(a => a != warrior
                    && a.Soul.Memory?.NarrativePerks != null
                    && a.Soul.Memory.NarrativePerks.Exists(p => p.PerkName == "Брат по оружию"));
            }

            // Последний живой: рядом не осталось своих на ногах.
            context.LastAlive = allies.Count == 0 && !warrior.IsDead;

            // Прокидываем предметы из инвентаря
            if (Inventory.PlayerInventory.Instance != null)
            {
                context.CarriedItems = Inventory.PlayerInventory.Instance.GetAllItems();
            }

            // Прокидываем сюжетные перки
            if (warrior.Soul.HasMemory && warrior.Soul.Memory.NarrativePerks.Count > 0)
            {
                context.AvailableNarrativePerks = warrior.Soul.Memory.NarrativePerks;
            }

            return context;
        }

        private static List<Warrior> GetNearbyEnemies(Warrior self)
        {
            List<Warrior> result = new();
            if (CombatManager.Instance == null) return result;

            foreach (var dmg in CombatManager.Instance.GetAliveEnemies())
            {
                if (dmg == null || dmg.IsDead || dmg.Warrior == null) continue;
                if (Vector3.Distance(self.transform.position, dmg.transform.position) < 10f)
                    result.Add(dmg.Warrior);
            }
            return result;
        }

        private static List<Warrior> GetNearbyAllies(Warrior self)
        {
            List<Warrior> result = new();
            if (CombatManager.Instance == null) return result;

            foreach (var dmg in CombatManager.Instance.GetAliveAllies())
            {
                if (dmg == null || dmg.IsDead || dmg.Warrior == null || dmg.Warrior == self) continue;
                if (Vector3.Distance(self.transform.position, dmg.transform.position) < 10f)
                    result.Add(dmg.Warrior);
            }
            return result;
        }

        private static List<HarvestableBody> GetNearbyLoot(Warrior self)
        {
            List<HarvestableBody> result = new();
            if (CombatManager.Instance == null) return result;

            foreach (var body in CombatManager.Instance.BodiesOnField)
            {
                if (body == null || body.IsCollected) continue;
                if (Vector3.Distance(self.transform.position, body.transform.position) < 8f)
                    result.Add(body);
            }
            return result;
        }

        private static float CalculateDanger(Warrior self, List<Warrior> enemies)
        {
            if (enemies.Count == 0) return 0f;
            float hpRatio = 1f - (self.HP / self.MaxHP);
            float enemyRatio = Mathf.Clamp01(enemies.Count / 3f);
            return Mathf.Clamp01((hpRatio + enemyRatio) / 2f);
        }

        private static Warrior GetCommander(Warrior warrior)
        {
            if (CombatManager.Instance == null) return null;
            foreach (var dmg in CombatManager.Instance.GetAliveAllies())
            {
                if (dmg.Warrior != null && dmg.Warrior.IsCommander && dmg.Warrior.Team == warrior.Team)
                    return dmg.Warrior;
            }
            return null;
        }
    }
}