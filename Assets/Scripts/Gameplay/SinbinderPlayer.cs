// Assets/_Project/Scripts/Gameplay/SinbinderPlayer.cs
using UnityEngine;

namespace Sinbinder.Gameplay
{
    public class SinbinderPlayer : Warrior
    {
        /// <summary>
        /// Игрок в сцене один, и диалоги обращаются к нему напрямую,
        /// не имея ссылки. Базовый Warrior своих Awake/OnDestroy не
        /// определяет, поэтому здесь ничего не перекрывается.
        /// </summary>
        public static SinbinderPlayer Instance { get; private set; }

        void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}