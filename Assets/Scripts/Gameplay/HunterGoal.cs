// Assets/Scripts/Gameplay/HunterGoal.cs
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Куда идёт охотник, когда рядом драться не с кем.
    ///
    /// Замысел автора, 24 сентября: «Первая волна двигается к центру лагеря.
    /// А во второй волне Инквизиторы при помощи магии будут разведывать
    /// позицию Греховода, и воины будут бежать к нему».
    ///
    /// <list type="bullet">
    /// <item><b>Первая волна — к костру.</b> Дошла — цель исполнена, дальше
    /// решает голос: бьют тех, кого видят, гонятся за видимым.</item>
    /// <item><b>Вторая — по следу</b> (<see cref="SinbinderTrail"/>): туда,
    /// где Инквизитор нашёл Греховода в последний раз. Пока Инквизитор жив,
    /// след свежий, и стая ждёт на месте нового заклинания; убит — след
    /// остыл, стая доходит до последнего места, и дальше решает голос.</item>
    /// </list>
    ///
    /// Цель, а не поведение: исполняет её <see cref="AOS.AOSWarriorWrapper"/>,
    /// и только когда рядом никого. Кто рядом — с тем решает голос, как
    /// у всех: драться, бежать раненым, спасать своего. Ставит
    /// <see cref="HunterSquadSpawner"/>; враги полигона и перебежчики
    /// целей не имеют.
    /// </summary>
    [DisallowMultipleComponent]
    public class HunterGoal : MonoBehaviour
    {
        public enum Aim
        {
            /// <summary>К костру — сердцу лагеря.</summary>
            CampCentre,

            /// <summary>По следу, который находит Инквизитор.</summary>
            Trail,
        }

        /// <summary>Дошёл — значит дошёл: ближе этого к цели не толпятся.</summary>
        public const float ArriveRadius = 4f;

        private Aim _aim;
        private Vector3 _centre;
        private bool _arrived;

        /// <summary>Задать цель. Зовёт спавнер, выпуская волну.</summary>
        public void Configure(Aim aim, Vector3 centre)
        {
            _aim = aim;
            _centre = centre;
            _arrived = false;
        }

        /// <summary>
        /// Куда идти сейчас. Ложь — цели нет или она исполнена, и тогда
        /// решает голос.
        /// </summary>
        public bool TryGet(Vector3 from, out Vector3 where)
        {
            where = default;

            switch (_aim)
            {
                case Aim.CampCentre:
                    if (_arrived) return false;
                    if (Near(from, _centre)) { _arrived = true; return false; }
                    where = _centre;
                    return true;

                case Aim.Trail:
                    if (!SinbinderTrail.Known) return false;

                    // Остывший след ведёт к последнему месту и там обрывается.
                    // Свежий держит стаю у места, пока Инквизитор не найдёт
                    // Греховода снова.
                    if (!SinbinderTrail.Fresh && Near(from, SinbinderTrail.Where)) return false;

                    where = SinbinderTrail.Where;
                    return true;
            }

            return false;
        }

        private static bool Near(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return dx * dx + dz * dz <= ArriveRadius * ArriveRadius;
        }
    }
}
