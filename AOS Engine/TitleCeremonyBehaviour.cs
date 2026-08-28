// Assets/_Project/Scripts/AOS Engine/TitleCeremonyBehaviour.cs
// Вынесен из TitleCeremony.cs: Unity требует, чтобы имя файла
// совпадало с именем MonoBehaviour, иначе скрипт нельзя повесить на объект.
using System.Collections;
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.AOS
{
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
