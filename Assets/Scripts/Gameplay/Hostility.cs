// Assets/Scripts/Gameplay/Hostility.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Греховоды партии «Перетягивания» (54-TUG.md): у кого какое имя-голос. Приказ несёт это имя
    /// (<see cref="PendingCommand.Lord"/>), верность ничьих считается к нему (<see cref="Allegiance"/>).
    /// В демо пусто — Греховод один, и ничего не меняется.
    /// </summary>
    public static class Lords
    {
        /// <summary>Голос игрока на этой карте; null — не «Перетягивание».</summary>
        public static string Player { get; set; }

        public static bool Active => !string.IsNullOrEmpty(Player);

        /// <summary>Чей это Греховод: игрок или Греховод-ИИ (RivalLord); не Греховод — null.</summary>
        public static string Of(Warrior w) => w is SinbinderPlayer ? Player : RivalLord.LordOf(w);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Player = null;
    }

    /// <summary>
    /// Кого бить ничьему (автор, 27.10: «кого бить — решает воин по своим отношениям: тех, кого
    /// ненавидит, и тех, на кого указал тот, кого он любит»). Без жребия, по формуле:
    ///
    /// * охотники — враги всем;
    /// * кто ударил его — враг на <see cref="Grudge"/> секунд;
    /// * кого ненавидит по памяти (отношение не выше <see cref="Hate"/>);
    /// * Греховода, к которому верность упала до <see cref="Despised"/>;
    /// * на кого указал Греховод, которому он верней всех, — если верен ему хотя бы на <see cref="Follows"/>.
    ///
    /// Греховоду и его людям враги — охотники, ничьи, что враждуют с ними, и те, на кого Греховод указал.
    /// Верность движут поступки: послушал — привыкает (<see cref="ObeyHabit"/>); ударил сам Греховод —
    /// много теряет; ударил тот, кого Греховод послал, — меньше.
    ///
    /// Спрашивают десятки раз за такт — ответ ничьему держится полсекунды и ищется в пределах
    /// <see cref="Sight"/>: врага за полкарты воин не знает.
    /// </summary>
    public static class Hostility
    {
        public const float Hate = -25f;
        public const float Despised = 10f;
        // «Верен» — с середины: к незнакомцу верность теперь 30–68 (Allegiance.StrangerShare), и при 35
        // верными становились после первого же послушания.
        public const float Follows = 50f;
        public const float ObeyHabit = 4f;
        public const float StruckByLord = -20f;
        public const float SentByLord = -8f;
        public const float Grudge = 20f;
        public const float Sight = 40f;
        private const float Keep = 0.5f;

        private static readonly Dictionary<string, HashSet<string>> Marks = new();
        private static readonly Dictionary<(string victim, string attacker), float> Blows = new();
        private static readonly Dictionary<Damageable, (float until, List<Damageable> list)> Foes = new();
        private static readonly Dictionary<Damageable, (float until, List<Damageable> list)> Friends = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Forget();

        /// <summary>Новая партия — с чистого листа.</summary>
        public static void Forget()
        {
            Marks.Clear();
            Blows.Clear();
            Foes.Clear();
            Friends.Clear();
            Killers.Clear();
        }

        /// <summary>Греховод указал на него — бить: так слышат все, кто ему верен.</summary>
        public static void Mark(string lord, Warrior target)
        {
            if (string.IsNullOrEmpty(lord) || target == null) return;
            if (!Marks.TryGetValue(lord, out var set)) Marks[lord] = set = new HashSet<string>();
            set.Add(target.Id);
        }

        public static bool Marked(string lord, Warrior target)
            => !string.IsNullOrEmpty(lord) && target != null && Marks.TryGetValue(lord, out var set) && set.Contains(target.Id);

        /// <summary>Удар: ударивший — враг ударенному; Греховод, чей это удар, теряет верность ударенного.</summary>
        public static void Struck(Warrior victim, Warrior attacker)
        {
            if (!Lords.Active || victim == null || attacker == null || victim == attacker) return;
            Blows[(victim.Id, attacker.Id)] = Time.time;
            if (victim.Team != Team.Neutral) return;

            string lord = Lords.Of(attacker);
            if (lord != null) Allegiance.Change(victim.Id, victim.Loyalty, lord, StruckByLord);
            else if (attacker.HasCommand && !string.IsNullOrEmpty(attacker.Command.Lord))
                Allegiance.Change(victim.Id, victim.Loyalty, attacker.Command.Lord, SentByLord);
        }

        private static readonly Dictionary<string, string> Killers = new();

        /// <summary>Душа помнит, кто её убил (алтарь: поднятый своим убийцей верен ему меньше).</summary>
        public static void NoteKill(Warrior dead, Warrior killer)
        {
            if (!Lords.Active || dead == null || killer == null || string.IsNullOrEmpty(dead.DisplayName)) return;
            Killers[dead.DisplayName] = killer.DisplayName;
        }

        public static string KillerOf(string soulName)
            => !string.IsNullOrEmpty(soulName) && Killers.TryGetValue(soulName, out var k) ? k : null;

        /// <summary>Бил ли <paramref name="attacker"/> этого воина недавно (за <see cref="Grudge"/> секунд).</summary>
        public static bool StruckRecently(Warrior victim, Warrior attacker)
            => victim != null && attacker != null
               && Blows.TryGetValue((victim.Id, attacker.Id), out var at) && Time.time - at < Grudge;

        /// <summary>Хочет ли <paramref name="a"/> бить <paramref name="b"/>.</summary>
        public static bool Wants(Warrior a, Warrior b) => Wants(a, b, null, null);

        private static bool Wants(Warrior a, Warrior b, Dictionary<string, float> feel, string dear)
        {
            if (a == null || b == null || a == b || b.IsDead) return false;
            if (b.Team == Team.Enemy) return true;
            if (Blows.TryGetValue((a.Id, b.Id), out var at) && Time.time - at < Grudge) return true;

            if (a.Team != Team.Neutral)
            {
                // Сторона Греховода: ничей — враг, если враждует с нами или на него указали.
                if (b.Team != Team.Neutral) return false;
                string mine = Lords.Of(a) ?? (a.HasCommand ? a.Command.Lord : null) ?? Lords.Player;
                return Marked(mine, b) || Wants(b, a);
            }

            float rel = feel != null ? (feel.TryGetValue(b.Id, out var f) ? f : 0f) : Relation(a, b);
            if (rel <= Hate) return true;

            string theirs = Lords.Of(b);
            if (theirs != null && Allegiance.Of(a.Id, a.Loyalty, theirs) <= Despised) return true;

            dear ??= Allegiance.Dearest(a.Id);
            return dear != null && Allegiance.Of(a.Id, a.Loyalty, dear) >= Follows && Marked(dear, b);
        }

        /// <summary>Враги спросившего (CombatManager.GetEnemies при ничьих на поле).</summary>
        public static List<Damageable> EnemiesOf(Damageable asker, List<Damageable> players,
                                                 List<Damageable> hunters, List<Damageable> neutrals)
        {
            if (Foes.TryGetValue(asker, out var kept) && Time.time < kept.until) return kept.list;

            var a = asker.Warrior;
            var list = new List<Damageable>();
            if (a != null)
            {
                Dictionary<string, float> feel = a.Team == Team.Neutral ? Feelings(a) : null;
                string dear = a.Team == Team.Neutral ? Allegiance.Dearest(a.Id) : null;
                var at = asker.transform.position;
                void Scan(List<Damageable> from)
                {
                    foreach (var d in from)
                    {
                        if (d == null || d == asker || d.IsDead) continue;
                        if ((d.transform.position - at).sqrMagnitude > Sight * Sight) continue;
                        if (Wants(a, d.Warrior, feel, dear)) list.Add(d);
                    }
                }
                Scan(hunters);
                Scan(neutrals);
                if (a.Team == Team.Neutral) Scan(players);
            }
            // Разнести обновления по кадрам: у каждого своя доля, а не все разом.
            Foes[asker] = (Time.time + Keep + (asker.GetInstanceID() & 7) * 0.03f, list);
            return list;
        }

        /// <summary>Свои ничьего — те, кто верней всех тому же Греховоду; никому не верен — один.</summary>
        public static List<Damageable> FriendsOf(Damageable asker, List<Damageable> neutrals)
        {
            if (Friends.TryGetValue(asker, out var kept) && Time.time < kept.until) return kept.list;
            var list = new List<Damageable> { asker };
            var a = asker.Warrior;
            string dear = a != null ? Allegiance.Dearest(a.Id) : null;
            if (dear != null)
            {
                var at = asker.transform.position;
                foreach (var d in neutrals)
                    if (d != null && d != asker && !d.IsDead && d.Warrior != null
                        && (d.transform.position - at).sqrMagnitude <= Sight * Sight
                        && Allegiance.Dearest(d.Warrior.Id) == dear)
                        list.Add(d);
            }
            Friends[asker] = (Time.time + Keep * 2f, list);
            return list;
        }

        /// <summary>Отношение к одному — по памяти, как RelationshipSystem.</summary>
        private static float Relation(Warrior a, Warrior b)
            => a.Relationships != null ? a.Relationships.GetRelationship(a.Id, b.Id) : 0f;

        /// <summary>
        /// Отношения ко всем сразу — один проход по памяти вместо прохода на каждого соседа
        /// (то же, что RelationshipSystem.GetRelationship, но для всех целей).
        /// </summary>
        private static Dictionary<string, float> Feelings(Warrior a)
        {
            var feel = new Dictionary<string, float>();
            var memory = AOS.MemoryProcessor.Instance;
            if (memory == null) return feel;
            float now = Time.time;
            foreach (var m in memory.GetMemoriesById(a.Id))
            {
                if (string.IsNullOrEmpty(m.TargetID)) continue;
                feel.TryGetValue(m.TargetID, out var v);
                feel[m.TargetID] = Mathf.Clamp(v + m.GetCurrentStrength(now), -100f, 100f);
            }
            return feel;
        }
    }
}
