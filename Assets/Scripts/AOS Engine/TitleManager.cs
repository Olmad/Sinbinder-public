// Assets/Scripts/AOS Engine/TitleManager.cs
using System.Linq;
using UnityEngine;
using Sinbinder.Gameplay;
using Sinbinder.Core;

namespace Sinbinder.AOS
{
    public static class TitleManager
    {
        public static void UpdateTitle(Warrior warrior)
        {
            var deeds = warrior.Reputation.Deeds;

            foreach (var rule in TitleDatabase.Rules)
            {
                int count = deeds.Count(d => d.Type == rule.MainDeed);
                float importance = deeds.Where(d => d.Type == rule.MainDeed).Sum(d => d.Importance);

                if (count >= rule.RequiredCount &&
                    importance >= rule.RequiredImportance &&
                    warrior.Reputation.Respect >= rule.RequiredRespect &&
                    warrior.Reputation.Fear >= rule.RequiredFear)
                {
                    // Проверка уникальности для конкурентных титулов
                    if (rule.MainDeed == DeedType.CollectMostLoot || rule.MainDeed == DeedType.DigMostSouls)
                    {
                        var currentHolder = FindWarriorWithTitle(rule.Title);
                        if (currentHolder != null && currentHolder != warrior)
                        {
                            float currentImportance = currentHolder.Reputation.Deeds
                                .Where(d => d.Type == rule.MainDeed).Sum(d => d.Importance);
                            float newImportance = deeds.Where(d => d.Type == rule.MainDeed).Sum(d => d.Importance);
                            if (newImportance <= currentImportance)
                                continue;
                            currentHolder.Reputation.CurrentName = currentHolder.DisplayName;
                        }
                    }

                    // Присваиваем титул
                    if (string.IsNullOrEmpty(warrior.Reputation.CurrentName))
                    {
                        warrior.Reputation.CurrentName = $"{rule.Title} {warrior.DisplayName}";
                        TitleCeremony.Start(warrior, rule.Title, false);
                    }

                    if (!warrior.Reputation.LegendaryUnlocked && 
                        (rule.RequiresCoreMemory || rule.RequiresLastAlive || rule.RequiresNearAltar || rule.RequiresSoulCollector))
                    {
                        warrior.Reputation.CurrentLegendaryTitle = $"{rule.Title} {warrior.DisplayName}";
                        warrior.Reputation.LegendaryUnlocked = true;
                        TitleCeremony.Start(warrior, rule.Title, true);
                    }

                    return;
                }
            }
        }

        private static Warrior FindWarriorWithTitle(string title)
        {
            var all = Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID);
            return all.FirstOrDefault(w => w.Reputation.CurrentName == $"{title} {w.DisplayName}");
        }
    }
}