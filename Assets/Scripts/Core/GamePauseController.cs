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
            // Конец игры держит паузу сам: церемония или разговор, начатые
            // рядом со смертью Греховода, по своему концу позвали бы Resume
            // и пустили бы бой дальше прямо под экраном конца.
            if (Halted) return;

            IsPaused = false;
            Time.timeScale = 1f;
        }

        /// <summary>Игра окончена: пауза, которую никто, кроме конца, не снимет.</summary>
        public bool Halted { get; private set; }

        /// <summary>Остановить мир насовсем — до <see cref="Unhalt"/>.</summary>
        public void Halt()
        {
            Halted = true;
            Pause();
        }

        /// <summary>Снять конец игры: начинают сначала.</summary>
        public void Unhalt()
        {
            Halted = false;
            Resume();
        }
    }
}
