// Assets/Core/Crisis/CrisisResolver.cs
using UnityEngine;
using Sinbinder.AOS;
using Sinbinder.Gameplay;

namespace Sinbinder.Core
{
    /// <summary>
    /// Ответы на вопросы вида «поступит ли воин так-то».
    ///
    /// Раньше здесь было пять бросков Random.Range. Это противоречило
    /// двум решениям сразу: поведение рождается голосованием модулей,
    /// а не кубиком, и одинаковый вход обязан давать одинаковый выход.
    /// Кубик выкинут. Там, где вопрос выразим в боевом голосовании,
    /// спрашиваем движок. Где не выразим — считаем детерминированно
    /// и помечаем как временное.
    /// </summary>
    public static class CrisisResolver
    {
        private static BehaviourResolver _resolver;

        private static BehaviourResolver Resolver
        {
            get
            {
                if (_resolver == null) _resolver = new BehaviourResolver();
                return _resolver;
            }
        }

        /// <summary>
        /// Бросится ли спасать. Вопрос выразим напрямую: строим контекст,
        /// в котором товарищ в беде, и смотрим, победит ли SaveAlly.
        /// </summary>
        public static bool WillSaveComrade(Warrior savior, Warrior wounded)
        {
            if (savior == null || wounded == null) return false;

            var context = CombatDecisionContext.Create(savior);
            context.AllyInDanger = true;
            context.TargetWarrior = wounded;

            return Resolver.Decide(savior, context) == ActionType.SaveAlly;
        }

        /// <summary>
        /// Предаст ли. ВРЕМЕННО: предательства нет среди действий
        /// голосования, поэтому считаем порогом. Когда в ActionType
        /// появится Betray и модули начнут за него голосовать —
        /// заменить на вызов резолвера, как выше.
        /// </summary>
        public static bool WillBetray(Warrior warrior)
        {
            if (warrior == null) return false;

            float pressure = 0f;
            pressure += (100f - warrior.Loyalty) * 0.4f;        // нелюбовь к командиру
            pressure += warrior.Virtue.Value * 0.3f;            // порочность
            pressure += Mathf.Min(warrior.UnpaidMissions, 5) * 8f; // невыплаченное
            if (warrior.Soul.Moral == MoralType.Vicious) pressure += 20f;
            if (warrior.Soul.Moral == MoralType.Pious) pressure -= 20f;

            return pressure >= 60f;
        }

        /// <summary>
        /// Поддастся ли на посулы врага. ВРЕМЕННО, по той же причине:
        /// среди действий голосования нет AcceptBribe как исхода кризиса.
        /// Совпадение греха воина с грехом искусителя весит больше всего —
        /// врага слушают тем охотнее, чем ближе он говорит к собственной язве.
        /// </summary>
        public static bool WillBeSeduced(Warrior warrior, SinType enemySin, float enemyPower)
        {
            if (warrior == null) return false;

            float pressure = 0f;
            if (warrior.Soul.Sin == enemySin) pressure += 40f;
            pressure += warrior.Virtue.Value * 0.4f;
            pressure += (100f - warrior.Loyalty) * 0.2f;
            pressure += Mathf.Clamp(enemyPower, 0f, 10f) * 4f;
            if (warrior.Soul.Moral == MoralType.Vicious) pressure += 20f;
            if (warrior.Soul.Moral == MoralType.Pious) pressure -= 20f;

            return pressure >= 60f;
        }

        /// <summary>
        /// Исход поединка. Побеждает сильнейший; при точном равенстве
        /// решает устойчивое сравнение идентификаторов, а не кубик,
        /// иначе один и тот же вход давал бы разный выход.
        /// </summary>
        public static Warrior ResolveDuel(Warrior a, Warrior b)
        {
            if (a == null) return b;
            if (b == null) return a;

            Warrior winner;
            if (a.Attack > b.Attack) winner = a;
            else if (b.Attack > a.Attack) winner = b;
            else winner = string.CompareOrdinal(a.Id, b.Id) <= 0 ? a : b;

            Warrior loser = winner == a ? b : a;

            winner.Virtue.Change(20f);
            loser.Virtue.Change(-20f);
            winner.Relationships.Change(winner.Id, loser.Id, -15f);
            return winner;
        }
    }
}
