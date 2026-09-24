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

            // Греховод титулов не носит. Имя по заслугам — рычаг игрока
            // над душами отряда, а Греховод и есть игрок: он в бою, выживает,
            // жнёт души и первым подходит к сундуку, и каждый из семи путей
            // сюда вёл его к церемонии. Прогон 17 сентября: «Я — Греховод
            // Тень». Закрыто здесь, на единственном входе, а не на семи.
            if (warrior is SinbinderPlayer) return;

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
                var holder = FindWarriorWithTitle(best);
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
            // Слово берётся у правила по полу носителя: «Защитник Марга»
            // и «Защитница Сквип» — одно правило, один порог, две формы.
            string word = best.For(warrior.Gender);

            var now = Current(warrior);
            if (now == null || Stronger(best, now))
            {
                warrior.Reputation.CurrentName = $"{word} {warrior.DisplayName}";
                TitleCeremony.Start(warrior, word, false);
            }

            if (!warrior.Reputation.LegendaryUnlocked && Legendary(best))
            {
                warrior.Reputation.CurrentLegendaryTitle = $"{word} {warrior.DisplayName}";
                warrior.Reputation.LegendaryUnlocked = true;
                TitleCeremony.Start(warrior, word, true);
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
            => RuleFor(warrior, warrior.Reputation.CurrentName);

        /// <summary>
        /// Заслуженное имя без имени носителя: «Костекоп», а не
        /// «Костекоп Гертон».
        ///
        /// Заведено 17 сентября по случаю автора: «Копатель умер,
        /// но Греховод успел забрать душу и снова поднял». Титул всё
        /// это время жил в <see cref="ReputationData"/>, то есть
        /// <b>на теле</b>, и с телом же пропадал: поднятый заново
        /// не мог знать, кем был, потому что знать было нечему.
        /// Отсюда эта работа берёт имя и кладёт в душу
        /// (<see cref="SoulData.Remember"/>) — единственное, что
        /// переживает тело.
        ///
        /// Легендарное имя старше обычного: если воин дорос до него,
        /// помнить он будет его.
        /// </summary>
        public static string TitleOf(Warrior warrior)
        {
            if (warrior == null || warrior.Reputation == null) return "";

            var legend = RuleFor(warrior, warrior.Reputation.CurrentLegendaryTitle);
            if (legend != null) return legend.For(warrior.Gender);

            var now = RuleFor(warrior, warrior.Reputation.CurrentName);
            return now != null ? now.For(warrior.Gender) : "";
        }

        /// <summary>Правило, сложившее данное имя. Пусто или чужое — null.</summary>
        private static TitleRule RuleFor(Warrior warrior, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            return TitleDatabase.Rules
                .FirstOrDefault(r => name == $"{r.For(warrior.Gender)} {warrior.DisplayName}");
        }

        /// <summary>
        /// Кто сейчас носит имя по этому правилу. Сверяем по правилу,
        /// а не по слову: держателем «Защитника» может оказаться
        /// «Защитница», и спорный титул иначе достался бы обоим сразу.
        /// </summary>
        private static Warrior FindWarriorWithTitle(TitleRule rule)
        {
            var all = Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID);
            return all.FirstOrDefault(
                w => w.Reputation.CurrentName == $"{rule.For(w.Gender)} {w.DisplayName}");
        }
    }
}
