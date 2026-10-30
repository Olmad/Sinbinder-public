// Assets/Scripts/Gameplay/HandsDrop.cs
// Перевод: текст через Loc
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// G — отпустить то, что в руках (автор, 29.10: «надо сделать отпускание предмета в руках на G»):
    /// пластинку — на землю перед собой (RecordHands.PutDown), тело с плеч — тоже на землю (Remains.PutDown),
    /// поднять снова — F. Руки пусты — G остаётся «обороной» выделенных (SelectionManager спрашивает
    /// <see cref="Busy"/>). Ставит себя сам.
    /// </summary>
    public class HandsDrop : MonoBehaviour
    {
        /// <summary>Что-то в руках — G отпускает, а не командует.</summary>
        public static bool Busy => !RecordHands.Empty || Remains.Carrying || CampfireStoke.Carrying;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<HandsDrop>() != null) return;
            var go = new GameObject("Руки — отпустить");
            go.AddComponent<HandsDrop>();
            DontDestroyOnLoad(go);
        }

        void Update()
        {
            if (!Input.GetKeyDown(KeyCode.G) || Dev.PhotoMode.Active || !SinbinderPlayer.Exists) return;
            if (Core.GamePauseController.Instance != null && Core.GamePauseController.Instance.IsPaused) return;
            if (!RecordHands.Empty) RecordHands.PutDown();
            else if (Remains.Carrying) Remains.PutDown();
            else if (CampfireStoke.Carrying) CampfireStoke.PutBack();
        }
    }
}
