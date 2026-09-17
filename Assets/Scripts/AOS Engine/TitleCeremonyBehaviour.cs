// Assets/Scripts/AOS Engine/TitleCeremonyBehaviour.cs
// Вынесен из TitleCeremony.cs: Unity требует, чтобы имя файла
// совпадало с именем MonoBehaviour, иначе скрипт нельзя повесить на объект.
using System.Collections;
using UnityEngine;
using Sinbinder.Gameplay;
using Sinbinder.Core;

namespace Sinbinder.AOS
{
    public class TitleCeremonyBehaviour : MonoBehaviour
    {
        public IEnumerator PlayCeremony(Warrior warrior, string title, bool isLegendary)
        {
            // Титул присуждается за деяние, а деяние бывает последним:
            // воин может не дожить до собственной церемонии. Наводить
            // камеру на уничтоженного нельзя — transform обращается
            // к нативной части и бросает MissingReferenceException,
            // а пауза при этом уже поставлена, и игра застыла бы навсегда.
            if (warrior == null) yield break;

            GamePauseController.Instance?.Pause();

            // Фраза церемонии до 13 сентября уходила в Debug.Log и только
            // туда: камера подъезжала к воину, тот молчал, игрок не узнавал
            // ни за что титул, ни какой. Теперь она на нижней полосе —
            // там же, где реплики и слова поступков.
            // Было две строки на всех: «Я вошёл в легенды!» и «Я заслужил
            // это!». Одинаковые у гордого и у ленивого, у первого титула
            // и у сотого. Теперь голос зависит от греха (<see cref="TitleWords"/>),
            // и жребия там нет — два одинаковых прохода дают одну церемонию.
            string line = TitleWords.Answer(warrior, title, isLegendary);
            string shout = TitleWords.Shout(warrior, title, isLegendary);

            var cameraController = FindFirstObjectByType<Dialogue.DialogueCameraController>();
            if (cameraController != null)
            {
                cameraController.SaveCameraPosition();
                yield return cameraController.FocusOn(warrior.transform);

                // Сначала кричит отряд, потом отвечает он. Порядок важен:
                // титул — приговор окружающих, и услышать его игрок должен
                // от них, а не от самого получившего.
                UI.Letterbox.Instance?.Say("Отряд", shout);
                yield return new WaitForSecondsRealtime(1.4f);

                UI.Letterbox.Instance?.Say(warrior.DisplayName, line);
            }

            Debug.Log($"[TITLE CEREMONY] [{warrior.DisplayName}]: {line}");
            yield return new WaitForSecondsRealtime(3f);
            if (cameraController != null)
                yield return cameraController.RestoreCamera();
            GamePauseController.Instance?.Resume();
        }
    }
}
