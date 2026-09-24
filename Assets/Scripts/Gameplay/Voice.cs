// Assets/Scripts/Gameplay/Voice.cs
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Голос Греховода: приказ слышен тем лучше, чем ближе Греховод к воину
    /// <b>в миг приказа</b> (docs/31-VOICE.md). Первая механика по решению
    /// автора «тело — микро, души — тренер».
    ///
    /// Три ступени, словами: <b>рядом</b> — в полную силу; <b>издали</b> —
    /// тише, плавно до края; <b>не слышно</b> — приказа нет вовсе.
    /// Граница — край зрения Греховода (<see cref="FogOfWar.HeroSight"/>):
    /// он командует тем, что видит, а круг зрения игрок и так видит
    /// в тумане.
    ///
    /// Здесь только мера. Что громкость значит для души, решают модули:
    /// она ложится в контекст решения (<see cref="AOS.DecisionContext.CommandVolume"/>),
    /// и верность, гордыня и уныние читают её каждый по-своему.
    ///
    /// <b>Выключатель.</b> До вечернего прогона 24 сентября голос выключен:
    /// прогон проверяет правки дня, а не их вместе с новой механикой.
    /// Включается командой «голос» в консоли (~). Проверен — выключатель
    /// снимается, и голос работает всегда.
    /// </summary>
    public static class Voice
    {
        /// <summary>Работает ли голос. Выключен — любой приказ слышен в полную силу.</summary>
        public static bool Enabled { get; set; }

        /// <summary>До скольких метров приказ слышен в полную силу.</summary>
        public const float Near = FogOfWar.HeroSight * 0.5f;

        /// <summary>Дальше этого приказ не слышен вовсе.</summary>
        public const float Far = FogOfWar.HeroSight;

        /// <summary>Громкость у самого края: тихо, но слышно.</summary>
        public const float Faintest = 0.35f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Enabled = false;

        /// <summary>
        /// Насколько приглушён приказ на таком расстоянии: 0 — в полную силу,
        /// 1 — не слышен. Плавно, без ступенек: шаг через невидимую черту
        /// не должен менять решение рывком.
        /// </summary>
        public static float Muffle(float distance)
        {
            if (!Enabled || distance <= Near) return 0f;
            if (distance > Far) return 1f;

            float k = (distance - Near) / (Far - Near);
            return Mathf.Lerp(0f, 1f - Faintest, k);
        }

        /// <summary>
        /// Приглушённость приказа этому воину сейчас. Греховода в сцене
        /// нет — мерить не от кого, и приказ слышен в полную силу.
        /// </summary>
        public static float MuffleFor(Warrior warrior)
        {
            if (warrior == null || !SinbinderPlayer.Exists) return 0f;
            return Muffle(CampFocus.GroundDistance(SinbinderPlayer.Where, warrior.transform.position));
        }

        public static bool Heard(float muffle) => muffle < 1f;
    }
}
