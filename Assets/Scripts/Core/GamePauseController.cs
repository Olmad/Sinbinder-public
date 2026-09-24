// Assets/Scripts/Core/GamePauseController.cs
using UnityEngine;

namespace Sinbinder.Core
{
    public class GamePauseController : MonoBehaviour
    {
        public static GamePauseController Instance { get; private set; }
        public bool IsPaused { get; private set; }

        /// <summary>
        /// Сколько раз ставили паузу. Нужен тому, кто останавливает мир
        /// ненадолго и сам (<see cref="Gameplay.MomentCamera"/>): запомнил
        /// число, поставив паузу, — и снимает её, только если число
        /// с тех пор не сменилось. Сменилось — значит паузу поверх взял
        /// разговор или церемония, и снимать её теперь им.
        ///
        /// Хозяина у паузы нет, и до 24 сентября это было не нужно: наезд
        /// на своеволие мир не останавливал вовсе. Потому воин, решивший
        /// бежать, за время наезда убегал, а камера подъезжала к месту,
        /// где его уже не было.
        /// </summary>
        public int Stamp { get; private set; }

        void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void Pause()
        {
            IsPaused = true;
            Stamp++;
            Time.timeScale = 0f;
        }

        public void Resume()
        {
            IsPaused = false;
            Time.timeScale = 1f;
        }
    }
}
