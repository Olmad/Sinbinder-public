// Assets/Scripts/Crypt/CryptMap.cs
// Перевод: текст через Loc
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.Crypt
{
    /// <summary>
    /// Карта вылазок в склепе демо (docs/39-PLACES.md §2). Автор,
    /// 29 сентября: «было бы хорошо, если бы была миссия с караваном».
    ///
    /// Обоз с развилкой написан давно (<see cref="MissionCatalog"/>,
    /// <see cref="JunctionCatalog"/>) и стоял только на карте полигона, куда
    /// игрок не попадает. Здесь стол с шаром встаёт в склеп — у правой
    /// стены, напротив мастерской (<see cref="CryptWorkshop"/>).
    ///
    /// <b>Шаг игрока.</b> Пока с доски не ушло ни одной вылазки, а людей
    /// хватает хоть на одну, эпилог ждёт (<see cref="Waiting"/>, спрашивает
    /// <see cref="PrologueDirector"/>): выбор дороги — рычаг этой части.
    ///
    /// Пока не проверено в игре — за выключателем консоли «вылазки»: без
    /// него стол спрятан, и склеп тот же. Карта сама на отдельном объекте:
    /// выключенный не включил бы себя.
    /// </summary>
    public class CryptMap : MonoBehaviour
    {
        [Tooltip("Стол с шаром и доска вылазок.")]
        [SerializeField] private GameObject _zone;

        /// <summary>Выключатель консоли «вылазки».</summary>
        public static bool Switch { get; set; }

        private static CryptMap _here;
        private MissionBoard _board;
        private bool _shown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm()
        {
            _here = null;
            Switch = false;
        }

        /// <summary>Доска вылазок склепа. Автопрогону: куда посылать.</summary>
        public static MissionBoard Board => _here != null ? _here._board : null;

        /// <summary>
        /// Эпилогу ждать: карта открыта, с доски не ушло ни одной вылазки,
        /// а людей хватает хоть на одну и есть кому её вести. Не на что
        /// идти — ждать нечего: иначе эпилог не пришёл бы никогда.
        /// </summary>
        public static bool Waiting
            => _here != null && Switch && _here._board != null
            && _here._board.Sent == 0 && Possible(_here._board);

        private static bool Possible(MissionBoard board)
        {
            foreach (var mission in board.Missions)
            {
                if (!board.EnoughPeople(mission)) continue;
                foreach (var m in SquadRoster.Members)
                    if (string.IsNullOrEmpty(board.WhyNot(m, mission))) return true;
            }
            return false;
        }

        void Awake()
        {
            _here = this;
            if (_zone != null) _board = _zone.GetComponentInChildren<MissionBoard>(true);

            if (_zone == null || _board == null)
                Debug.LogWarning("[СКЛЕП] Карта без стола или без доски вылазок: "
                               + "посылать из склепа будет некуда.");
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
