// Assets/_Project/Scripts/AOS/TitleCeremony.cs
using System.Collections;
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.AOS
{
    public static class TitleCeremony
    {
        public static void Start(Warrior warrior, string title, bool isLegendary)
        {
            var ceremony = Object.FindObjectOfType<TitleCeremonyBehaviour>();
            if (ceremony == null)
            {
                Debug.LogWarning("[TITLE] TitleCeremonyBehaviour не найден на сцене!");
                return;
            }
            ceremony.StartCoroutine(ceremony.PlayCeremony(warrior, title, isLegendary));
        }
    }

    public class TitleCeremonyBehaviour : MonoBehaviour
    {
        public IEnumerator PlayCeremony(Warrior warrior, string title, bool isLegendary)
        {
            GamePauseController.Instance?.Pause();
            var cameraController = FindObjectOfType<Dialogue.DialogueCameraController>();
            if (cameraController != null)
            {
                cameraController.SaveCameraPosition();
                yield return cameraController.FocusOn(warrior.transform);
            }
            string line = isLegendary
                ? $"[{warrior.DisplayName}]: Я вошёл в легенды!"
                : $"[{warrior.DisplayName}]: Я заслужил это! Теперь я — {title}!";
            Debug.Log($"[TITLE CEREMONY] {line}");
            yield return new WaitForSecondsRealtime(3f);
            if (cameraController != null)
                yield return cameraController.RestoreCamera();
            GamePauseController.Instance?.Resume();
        }
    }
}