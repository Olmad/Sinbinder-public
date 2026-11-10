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

        private struct Watch { public Warrior Guard; public float Until; public bool Rescue; }
        // Одна награда за одну угрозу. Повторное голосование AOS каждую секунду
        // не должно производить сотни фиктивных подвигов.
        private static readonly Dictionary<string, float> LastRescueCredit = new();
        private const float RescueCreditCooldown = 15f;
        public static int Rescues { get; private set; }
        public static Warrior LastRescued { get; private set; }
        public static Warrior LastRescuer { get; private set; }
        private static readonly Dictionary<Damageable, Watch> Watched = new();

        // Строка — черновик D; облаку на сверку.
        private static readonly string Took = Loc.N("{0} принял удар на себя.");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm()
        {
            Watched.Clear();
            LastRescueCredit.Clear();
            Blocks = 0;
            Rescues = 0;
            LastRescued = null;
            LastRescuer = null;
        }

        /// <summary>Охранять этого столько секунд — до первого удара.</summary>
        public static void Guard(Damageable who, Warrior guard, float seconds)
        {
            if (who == null || guard == null) return;
            Watched[who] = new Watch { Guard = guard, Until = Time.time + seconds, Rescue = false };
        }

        /// <summary>
        /// AOS выбрала «Спасти товарища»: защитник уже дошёл до раненого.
        /// Подвиги НЕ начисляются за приближение. Только действительно
        /// перехваченный удар может превратиться в память о спасении.
        /// Возвращает false, когда защита пока невозможна или её место уже занято.
        /// </summary>
        public static bool GuardForRescue(Damageable who, Warrior guard, float seconds = 2.5f)
        {
            if (who == null || who.IsDead || who.Warrior == null
                || guard == null || guard.IsDead || who.Warrior == guard) return false;
            if (Vector3.Distance(who.transform.position, guard.transform.position) > 2.8f) return false;
            if (who.HP >= who.MaxHP * 0.3f) return false;
            // Дружба определяется системой отрядов, а не только одинаковым
            // Team. Для нейтральных персонажей команды могут различаться.
            if (CombatManager.Instance == null
                || !CombatManager.Instance.GetAllies(guard.gameObject).Contains(who)) return false;

            // Особый телохранитель (Карган) всегда сохраняет свою заранее
            // установленную защиту: AOS не перебивает сюжетную механику.
            if (Watched.TryGetValue(who, out var existing)
                && existing.Guard != null && !existing.Guard.IsDead
                && existing.Until > Time.time)
                return existing.Rescue && existing.Guard == guard;

            Watched[who] = new Watch { Guard = guard, Until = Time.time + seconds, Rescue = true };
            return true;
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
            if (guardBody == null || guardBody.IsDead || guardBody == target) return false;
            if (w.Rescue && (target.Warrior == null
                || Vector3.Distance(w.Guard.transform.position, target.transform.position) > 2.8f
                || CombatManager.Instance == null
                || !CombatManager.Instance.GetAllies(w.Guard.gameObject).Contains(target)))
                return false;

            // Встретил оружием: взмах, полудара — ему.
            if (w.Guard.TryGetComponent<WarriorAnimation>(out var arm)) arm.Struck();
            guardBody.TakeDamage(damage * Blocked, attacker);

            // Звать на себя — нет: только в арьергарде (28-ORDERS, «Карган разгружен»).
            Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(Loc.F(Took, Loc.Name(w.Guard.DisplayName)));
            Debug.Log($"[{(w.Rescue ? "AOS СПАСЕНИЕ" : "ВОРОН")}] {w.Guard.DisplayName} принял удар по {target.name} на себя.");
            Blocks++;
            if (w.Rescue && target.Warrior != null)
            {
                // Событие наступило ПОСЛЕ удара по защитнику, не после команды.
                // Повторные удары в одной схватке остаются защитой, но не
                // генерируют новые титулы и благодарность каждую секунду.
                string key = w.Guard.GetInstanceID() + ":" + target.GetInstanceID();
                if (!LastRescueCredit.TryGetValue(key, out float creditedAt)
                    || Time.time - creditedAt >= RescueCreditCooldown)
                {
                    LastRescueCredit[key] = Time.time;
                    Rescues++;
                    LastRescued = target.Warrior;
                    LastRescuer = w.Guard;
                    AOS.AOSEventHub.Instance?.OnAllySaved(w.Guard, target.Warrior);
                    Debug.Log($"[AOS] Спасение подтверждено реальным перехватом удара: {w.Guard.DisplayName} -> {target.Warrior.DisplayName}");
                }
            }
            return true;
        }

        /// <summary>Сколько ударов принято за другого — для прогона.</summary>
        public static int Blocks { get; private set; }
    }
}
