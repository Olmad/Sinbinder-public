// Assets/Scripts/Gameplay/Armament.cs
// Перевод: текст через Loc
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Оружие воина — в руке или в ножнах.
    ///
    /// Автор, 29 сентября: «сделай возможность доставать и убирать оружие».
    /// Вещей две: оружие на кости кисти и то же оружие на тазу
    /// (<c>…Stowed</c>, <c>Tools/blender/wear.py</c>), переведённое туда,
    /// где кисть отпускает рукоять в клипе «убрать». Видна всегда одна:
    /// подмена в нужный кадр клипа не видна — на этом кадре они стоят
    /// в одном месте (проверка — <c>Tools/blender/armscheck.py</c>).
    /// Ножны (<c>Scabbard</c>) висят всегда, пустые — когда меч в руке.
    ///
    /// Когда доставать и убирать, решает тело (<see cref="WarriorAnimation"/>):
    /// этот класс только показывает. Вешает его гардероб
    /// (<see cref="Wardrobe.Dress"/>), если у воина есть оружие.
    /// </summary>
    public class Armament : MonoBehaviour
    {
        private GameObject _inHand;
        private GameObject _stowed;

        /// <summary>Оружие сейчас в руке.</summary>
        public bool Drawn { get; private set; }

        /// <summary>
        /// Есть что убирать: и оружие в руке, и оно же в ножнах. Нет второй
        /// вещи (старая сборка гардероба) — оружие остаётся в руке, как было
        /// до 1 октября, а не пропадает.
        /// </summary>
        public bool CanSheathe => _inHand != null && _stowed != null;

        /// <summary>Связать с надетыми вещами и показать как сказано.</summary>
        public void Bind(GameObject inHand, GameObject stowed, bool drawn)
        {
            _inHand = inHand;
            _stowed = stowed;
            Show(drawn || !CanSheathe);
        }

        /// <summary>Показать оружие в руке (true) или в ножнах (false).</summary>
        public void Show(bool drawn)
        {
            if (!CanSheathe) drawn = true;
            Drawn = drawn;
            if (_inHand != null) _inHand.SetActive(drawn);
            if (_stowed != null) _stowed.SetActive(!drawn);
        }
    }
}
