// Assets/Scripts/Gameplay/Bodyguard.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Принять удар за другого — один раз. Автор, 26 октября: «нужно, чтобы Карган
    /// телепортировался и блокировал первую атаку по Греховоду, а дальше перенять
    /// внимание атакующего на себя» (<see cref="CrowStep"/>).
    ///
    /// Охраняемый ставится на короткий срок; первый удар по нему, пока срок идёт,
    /// приходится по охраннику — вполсилы: тот встретил его оружием. Дальше охранник
    /// зовёт нападавшего на себя (<see cref="Provocation"/>) — поведётся ли тот,
    /// решает его душа. Зовёт <see cref="Damageable.TakeDamage"/> первым делом.
    /// </summary>
    public static class Bodyguard
    {
        private const float Blocked = 0.5f;     // какая доля удара доходит до охранника

        private struct Watch { public Warrior Guard; public float Until; }
        private static readonly Dictionary<Damageable, Watch> Watched = new();

        // Строка — черновик D; облаку на сверку.
        private static readonly string Took = Loc.N("{0} принял удар на себя.");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Watched.Clear();

        /// <summary>Охранять этого столько секунд — до первого удара.</summary>
        public static void Guard(Damageable who, Warrior guard, float seconds)
        {
            if (who == null || guard == null) return;
            Watched[who] = new Watch { Guard = guard, Until = Time.time + seconds };
        }

        /// <summary>
        /// Удар по охраняемому — принять на себя. Правда — удар поглощён и бить
        /// охраняемого не надо. Сам охранник себя не охраняет: без петли.
        /// </summary>
        public static bool Absorb(Damageable target, GameObject attacker, float damage)
        {
            if (target == null || !Watched.TryGetValue(target, out var w)) return false;
            Watched.Remove(target);
            if (w.Guard == null || w.Guard.IsDead || Time.time > w.Until) return false;

            var guardBody = w.Guard.GetComponent<Damageable>();
            if (guardBody == null || guardBody == target) return false;

            // Встретил оружием: взмах, полудара — ему.
            if (w.Guard.TryGetComponent<WarriorAnimation>(out var arm)) arm.Struck();
            guardBody.TakeDamage(damage * Blocked, attacker);

            // И зовёт нападавшего на себя.
            Provocation.Begin(w.Guard);
            Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(Loc.F(Took, Loc.Name(w.Guard.DisplayName)));
            Debug.Log($"[ВОРОН] {w.Guard.DisplayName} принял удар по {target.name} на себя и зовёт врага.");
            Blocks++;
            return true;
        }

        /// <summary>Сколько ударов принято за другого — для прогона.</summary>
        public static int Blocks { get; private set; }
    }
}
