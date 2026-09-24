// Assets/Scripts/Gameplay/SinbinderTrail.cs
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Что охотники знают о том, где Греховод. Одно знание на стаю:
    /// его находит магия Инквизитора (<see cref="Scryer"/>), а бегут
    /// на него все охотники второй волны (<see cref="HunterGoal"/>).
    ///
    /// Замысел автора, 24 сентября: «Во второй волне Инквизиторы при помощи
    /// магии будут разведывать позицию Греховода, и воины будут бежать
    /// к нему». Знание — не взгляд: стая знает место, где Греховода нашли
    /// в последний раз, а не где он сейчас. Между двумя заклинаниями можно
    /// успеть уйти, а убив Инквизитора — оборвать след совсем.
    /// </summary>
    public static class SinbinderTrail
    {
        /// <summary>Находили ли Греховода хоть раз за эту охоту.</summary>
        public static bool Known { get; private set; }

        /// <summary>Где нашли в последний раз.</summary>
        public static Vector3 Where { get; private set; }

        /// <summary>
        /// Свеж ли след: жив ли хоть один, кто умеет искать. Остывший след
        /// ведёт к последнему месту — и там обрывается.
        /// </summary>
        public static bool Fresh => Scryer.AnyAlive;

        /// <summary>Сказано ли уже, что Греховода нашли, и что след потерян.</summary>
        internal static bool ToldFound;
        internal static bool ToldLost;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Forget();

        /// <summary>Нашли — стая знает, где он.</summary>
        public static void Mark(Vector3 at)
        {
            Known = true;
            Where = at;
        }

        /// <summary>Новая охота: прежний след ничего не значит.</summary>
        public static void Forget()
        {
            Known = false;
            ToldFound = false;
            ToldLost = false;
        }
    }
}
