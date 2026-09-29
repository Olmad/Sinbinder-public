// Assets/Scripts/Crypt/CryptWorkshop.cs
// Перевод: текст через Loc
using UnityEngine;
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.Crypt
{
    /// <summary>
    /// Мастерская связывания в склепе демо (docs/37-DEMO.md §4, шаг 2).
    /// Автор, 25 сентября: «в самом склепе создание воина».
    ///
    /// Механизм готов и проверен запуском на полигоне (14-HANDOFF §110.1):
    /// банка → руки → гнездо души, тело со стола → ложе, рычаг → поднятый.
    /// Места в демо у него не было — мастерская стояла только в полигоне,
    /// куда игрок не попадает. Здесь она встаёт в склеп, куда отряд
    /// приходит с душами, собранными в набеге.
    ///
    /// <b>Шаг игрока, а не витрина.</b> Пока в суме есть душа, а воин
    /// не поднят, эпилог ждёт (<see cref="Waiting"/>, спрашивает
    /// <see cref="PrologueDirector"/>): создание воина и есть эта часть
    /// демо. Душ нет — ждать нечего, эпилог идёт как прежде.
    ///
    /// Пока не проверено в игре — за выключателем консоли «связывание»:
    /// без него мастерская спрятана, и склеп тот же, что был.
    ///
    /// Сама мастерская — дочерний объект (<see cref="_zone"/>): выключенный
    /// объект не получает Update и не включил бы себя сам.
    /// </summary>
    public class CryptWorkshop : MonoBehaviour
    {
        [Tooltip("Зона связывания: устройство, полка, стол тел.")]
        [SerializeField] private GameObject _zone;

        /// <summary>Выключатель консоли «связывание».</summary>
        public static bool Switch { get; set; }

        private static CryptWorkshop _here;
        private BindingDevice _device;
        private bool _shown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm()
        {
            _here = null;
            Switch = false;
        }

        /// <summary>Устройство мастерской. Автопрогону: что нажимать.</summary>
        public static BindingDevice Device => _here != null ? _here._device : null;

        /// <summary>
        /// Эпилогу ждать: мастерская открыта, воин не поднят, а поднять есть
        /// из кого — душа в суме, в руках, в гнезде или на полке, и хоть одно
        /// тело на столе её примет. Истлевшая душа, которую не примет ни одно,
        /// ждать не заставляет: иначе эпилог не пришёл бы никогда.
        /// </summary>
        public static bool Waiting
            => _here != null && Switch && _here._device != null
            && _here._device.Raised == 0 && SoulAtHand(_here._device);

        private static bool SoulAtHand(BindingDevice device)
        {
            if (device.HasSoul && Bindable(device.Quality)) return true;
            if (CryptHands.HasSoul && Bindable(CryptHands.Quality)) return true;

            for (int i = 0; i < Satchel.Size; i++)
            {
                var slot = Satchel.At(i);
                if (slot.FullJar && Bindable(slot.Quality)) return true;
            }

            var souls = SoulManager.Instance;
            if (souls != null)
                foreach (var kept in souls.Harvested)
                    if (kept.Soul != null && Bindable(kept.Quality)) return true;

            return false;
        }

        /// <summary>Примет ли душу такой свежести хоть одно тело со стола — тем же правилом, что устройство.</summary>
        public static bool Bindable(SoulQuality quality)
        {
            foreach (ShellType type in System.Enum.GetValues(typeof(ShellType)))
            {
                if (!ShellKinds.Bindable(type) || !CryptUpgrades.AllowsShell(type)) continue;
                var data = ShellLibrary.Get(type);
                if (data != null && ShellChoice.Allows(data, quality)) return true;
            }
            return false;
        }

        void Awake()
        {
            _here = this;
            if (_zone != null) _device = _zone.GetComponentInChildren<BindingDevice>(true);

            if (_zone == null || _device == null)
                Debug.LogWarning("[СКЛЕП] Мастерская без зоны или без устройства: "
                               + "связывать в склепе будет нечем.");
            Show(Switch);
        }

        void OnDestroy()
        {
            if (_here == this) _here = null;
        }

        void Update()
        {
            if (_shown != Switch) Show(Switch);
        }

        private void Show(bool on)
        {
            _shown = on;
            if (_zone != null) _zone.SetActive(on);
        }
    }
}
