// Assets/Core/GamePauseController.cs
using UnityEngine;

namespace Sinbinder.Core
{
    public class GamePauseController : MonoBehaviour
    {
        public static GamePauseController Instance { get; private set; }
        public bool IsPaused { get; private set; }

        void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void Pause()
        {
            IsPaused = true;
            Time.timeScale = 0f;
        }

        public void Resume()
        {
            IsPaused = false;
            Time.timeScale = 1f;
        }
    }
}