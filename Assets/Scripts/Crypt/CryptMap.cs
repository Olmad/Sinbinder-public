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
    /// <b>Песочница — после истории</b> (docs/37-DEMO.md §0, автор 10 октября:
    /// «нужны вылазки после основной игры — это песочница, где игрок может
    /// получить улучшения, посмотреть на своих воинов вне скриптовых боёв
    /// и в спокойной обстановке потыкать кнопки сам»). Стол появляется,
    /// когда история кончилась и игрок решил остаться в склепе
    /// (<see cref="Open"/>, кнопка эпилога <see cref="UI.DemoEndUI"/>).
    /// До 10 октября вылазка была шагом перед эпилогом: отряд из лагеря
    /// ещё не вернулся, а склеп уже работал как база.
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

        /// <summary>История кончилась, склеп — песочница: стол на месте.</summary>
        public static bool Opened { get; private set; }

        private static CryptMap _here;
        private MissionBoard _board;
        private bool _shown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm()
        {
            _here = null;
            Switch = false;
            Opened = false;
        }

        /// <summary>Открыть песочницу: стол встаёт, если выключатель включён.</summary>
        public static void Open() => Opened = true;

        /// <summary>Доска вылазок склепа. Автопрогону: куда посылать.</summary>
        public static MissionBoard Board => _here != null ? _here._board : null;

        /// <summary>
        /// Песочница открыта, с доски не ушло ни одной вылазки, а людей
        /// хватает хоть на одну и есть кому её вести. Спрашивает автопрогон:
        /// послать первую.
        /// </summary>
        public static bool Waiting
            => _here != null && Switch && Opened && _here._board != null
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
            Opened = false;   // новая сцена склепа — история сначала
            if (_zone != null) _board = _zone.GetComponentInChildren<MissionBoard>(true);

            if (_zone == null || _board == null)
                Debug.LogWarning("[СКЛЕП] Карта без стола или без доски вылазок: "
                               + "посылать из склепа будет некуда.");
            Show(Switch && Opened);
        }

        void OnDestroy()
        {
            if (_here == this) _here = null;
        }

        void Update()
        {
            bool on = Switch && Opened;
            if (_shown != on) Show(on);
        }

        private void Show(bool on)
        {
            _shown = on;
            if (_zone != null) _zone.SetActive(on);
        }
    }
}
