// Assets/Scripts/Gameplay/Facing.cs
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Спина. Третья механика пола.
    ///
    /// Удар в задний сектор бьёт сильнее и не встречает ответа. Отсюда
    /// у игрока появляется первый настоящий тактический глагол: одним
    /// сковать, вторым обойти.
    ///
    /// И отсюда же — лучшая строчка во всей боевой системе: гордый воин
    /// не бьёт в спину и теряет на этом урон. Добродетель, за которую
    /// платят, — это и есть игра.
    /// </summary>
    public static class Facing
    {
        /// <summary>Ширина заднего сектора в градусах.</summary>
        public const float RearArc = 120f;

        /// <summary>Во сколько раз больнее удар в спину.</summary>
        public const float RearMultiplier = 1.5f;

        /// <summary>Бьют ли жертву сзади.</summary>
        public static bool IsFromBehind(Transform victim, Vector3 attackerPosition)
        {
            if (victim == null) return false;

            Vector3 toAttacker = attackerPosition - victim.position;
            toAttacker.y = 0f;
            if (toAttacker.sqrMagnitude < 0.0001f) return false;

            float angle = Vector3.Angle(victim.forward, toAttacker.normalized);
            return angle > 180f - RearArc * 0.5f;
        }

        /// <summary>Множитель урона с учётом того, откуда бьют.</summary>
        public static float DamageMultiplier(Transform victim, Vector3 attackerPosition)
            => IsFromBehind(victim, attackerPosition) ? RearMultiplier : 1f;
    }
}
