// Assets/Scripts/AOS Engine/TitleManager.cs
using System.Linq;
using UnityEngine;
using Sinbinder.Gameplay;
using Sinbinder.Core;

namespace Sinbinder.AOS
{
    /// <summary>
    /// Присуждение титула по деяниям.
    ///
    /// <b>Переписано 17 сентября.</b> До этого метод шёл по правилам
    /// по порядку и делал <c>return</c> на первом подошедшем, а охрана
    /// «имя ещё пустое» после этого не пропускала второй титул никогда.
    /// Следствия были тяжёлыми и невидимыми: титул не повышался ни разу,
    /// «Гроза Охотников» была недостижима вовсе (потому что «Убийца
    /// Охотников» стоит в списке раньше), и **ни один из шести
    /// легендарных титулов не мог быть выдан** — цикл до них не доходил.
    ///
    /// Теперь берётся не первый подошедший, а **лучший**, и титул
    /// повышается, когда воин дорос.
    /// </summary>
    public static class TitleManager
    {
        /// <summary>Сказано один раз: иначе жалуются все девять разом.</summary>
        private static bool _toldAboutUncheckable;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => _toldAboutUncheckable = false;

        public static void UpdateTitle(Warrior warrior)
        {
            if (warrior == null) return;

            var deeds = warrior.Reputation.Deeds;

            TitleRule best = null;
            foreach (var rule in TitleDatabase.Rules)
            {
                if (!Earned(warrior, deeds, rule)) continue;
                if (best == null || Stronger(rule, best)) best = rule;
            }

            if (best == null) return;

            // Спорные титулы — их держит кто-то один, и отнимаются они
            // только превосходством. Правило прежнее, перенесено как есть.
            if (best.MainDeed == DeedType.CollectMostLoot
             || best.MainDeed == DeedType.DigMostSouls)
            {
                var holder = FindWarriorWithTitle(best.Title);
                if (holder != null && holder != warrior)
                {
                    float his = Sum(holder.Reputation.Deeds, best.MainDeed);
                    float ours = Sum(deeds, best.MainDeed);
                    if (ours <= his) return;

                    holder.Reputation.CurrentName = holder.DisplayName;
                }
            }

            // Повышение: имя меняется, только когда новый титул строго
            // сильнее нынешнего. Без этого воин застревал на первом
            // заслуженном имени навсегда.
            var now = Current(warrior);
            if (now == null || Stronger(best, now))
            {
                warrior.Reputation.CurrentName = $"{best.Title} {warrior.DisplayName}";
                TitleCeremony.Start(warrior, best.Title, false);
            }

            if (!warrior.Reputation.LegendaryUnlocked && Legendary(best))
            {
                warrior.Reputation.CurrentLegendaryTitle = $"{best.Title} {warrior.DisplayName}";
                warrior.Reputation.LegendaryUnlocked = true;
                TitleCeremony.Start(warrior, best.Title, true);
            }
        }

        /// <summary>
        /// Заслужен ли титул. Счёт, важность, уважение, страх — и особые
        /// обстоятельства легендарных, которые до 17 сентября
        /// <b>не проверялись вовсе</b>: четыре флага <c>Requires*</c>
        /// встречались ровно в одном выражении, решавшем «легендарный ли
        /// титул», и никто не спрашивал, последний ли он выживший.
        /// </summary>
        private static bool Earned(Warrior warrior, System.Collections.Generic.List<DeedRecord> deeds,
                                   TitleRule rule)
        {
            int count = deeds.Count(d => d.Type == rule.MainDeed);

            return count >= rule.RequiredCount
                && Sum(deeds, rule.MainDeed) >= rule.RequiredImportance
                && warrior.Reputation.Respect >= rule.RequiredRespect
                && warrior.Reputation.Fear >= rule.RequiredFear
                && Circumstance(warrior, rule);
        }

        /// <summary>
        /// Особое обстоятельство легендарного титула.
        ///
        /// Два проверяются: «последний, кто стоял» — по счёту живых своих;
        /// «это уже в его памяти» — по <see cref="MemoryProcessor"/>.
        /// Два других спросить пока не у кого: ни жнеца душ, ни алтаря
        /// в игре нет. Это **не тихое «нет»**: о неспрашиваемом условии
        /// говорится вслух, иначе титул был бы недостижим молча — ровно
        /// та беда, из-за которой это и переписывалось.
        /// </summary>
        private static bool Circumstance(Warrior warrior, TitleRule rule)
        {
            if (rule.RequiresLastAlive)
                return CombatManager.Instance != null
                    && CombatManager.Instance.GetAlivePlayerCount() <= 1;

            if (rule.RequiresCoreMemory)
                return MemoryProcessor.Instance != null
                    && MemoryProcessor.Instance.HasCoreMemory(warrior, rule.MainDeed);

            if (rule.RequiresSoulCollector || rule.RequiresNearAltar)
            {
                if (!_toldAboutUncheckable)
                {
                    _toldAboutUncheckable = true;
                    Debug.LogWarning($"[ТИТУЛ] «{rule.Title}» требует обстоятельства, "
                                   + "которого игра пока не знает (жнец душ или алтарь). "
                                   + "Титул недостижим, и это сказано вслух, "
                                   + "а не умолчано.");
                }
                return false;
            }

            return true;
        }

        /// <summary>
        /// Сильнее ли один титул другого. Цена титула — прежде всего счёт
        /// деяний: он и есть задуманное «сколько раз». Важность разводит
        /// равные, легендарность — равные и по ней.
        /// </summary>
        private static bool Stronger(TitleRule a, TitleRule b)
        {
            if (a.RequiredCount != b.RequiredCount)
                return a.RequiredCount > b.RequiredCount;

            if (!Mathf.Approximately(a.RequiredImportance, b.RequiredImportance))
                return a.RequiredImportance > b.RequiredImportance;

            return Legendary(a) && !Legendary(b);
        }

        private static bool Legendary(TitleRule rule)
            => rule.RequiresCoreMemory || rule.RequiresLastAlive
            || rule.RequiresNearAltar || rule.RequiresSoulCollector;

        private static float Sum(System.Collections.Generic.List<DeedRecord> deeds, DeedType type)
            => deeds.Where(d => d.Type == type).Sum(d => d.Importance);

        /// <summary>Правило, по которому воин носит нынешнее имя.</summary>
        private static TitleRule Current(Warrior warrior)
        {
            string name = warrior.Reputation.CurrentName;
            if (string.IsNullOrEmpty(name)) return null;

            return TitleDatabase.Rules
                .FirstOrDefault(r => name == $"{r.Title} {warrior.DisplayName}");
        }

        private static Warrior FindWarriorWithTitle(string title)
        {
            var all = Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID);
            return all.FirstOrDefault(w => w.Reputation.CurrentName == $"{title} {w.DisplayName}");
        }
    }
}
