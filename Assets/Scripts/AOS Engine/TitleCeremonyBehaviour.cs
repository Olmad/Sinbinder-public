// Assets/Scripts/AOS Engine/TitleCeremonyBehaviour.cs
// Вынесен из TitleCeremony.cs: Unity требует, чтобы имя файла
// совпадало с именем MonoBehaviour, иначе скрипт нельзя повесить на объект.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Gameplay;
using Sinbinder.Core;

namespace Sinbinder.AOS
{
    /// <summary>
    /// Вручение титула: наезд камеры, крик отряда, ответ получившего.
    ///
    /// <b>Очередь, а не пачка.</b> Конец боя присуждает титулы в одном
    /// цикле по всем выжившим (<c>AOSEventHub</c>), и «Тень» приходит
    /// за одну уцелевшую вылазку — то есть после обычного набега
    /// церемония полагается **каждому**. Раньше каждая из них заводила
    /// свою корутину на этом же объекте, и все они стартовали в одном
    /// кадре:
    ///
    /// <list type="bullet">
    /// <item>наезды дрались за камеру — <c>DialogueCameraController</c>
    /// держит <c>_swayCoroutine</c> одним полем, и последний наезд
    /// затирал ссылку, а брошенные продолжали тянуть камеру;</item>
    /// <item>строки затирали друг друга на нижней полосе: восемь реплик
    /// за две секунды вместо четырёх церемоний;</item>
    /// <item>пауза снималась первой же кончившейся, пока остальные
    /// ещё шли.</item>
    /// </list>
    ///
    /// Теперь церемонии встают в очередь и играются по одной. Ничего,
    /// кроме порядка, это не меняет — но именно это и увидела бы камера
    /// на показе.
    /// </summary>
    public class TitleCeremonyBehaviour : MonoBehaviour
    {
        /// <summary>Отложенное вручение. Слово уже выбрано по полу носителя.</summary>
        private readonly struct Pending
        {
            public readonly Warrior Warrior;
            public readonly string Title;
            public readonly bool Legendary;

            public Pending(Warrior warrior, string title, bool legendary)
            {
                Warrior = warrior;
                Title = title;
                Legendary = legendary;
            }
        }

        private readonly Queue<Pending> _waiting = new();
        private bool _playing;

        /// <summary>
        /// Поставить вручение в очередь. Играется сразу, если никто
        /// не занимает камеру, и после предыдущего — если занимает.
        /// </summary>
        public void Enqueue(Warrior warrior, string title, bool legendary)
        {
            if (warrior == null) return;

            _waiting.Enqueue(new Pending(warrior, title, legendary));
            if (!_playing) StartCoroutine(Drain());
        }

        private IEnumerator Drain()
        {
            _playing = true;

            while (_waiting.Count > 0)
            {
                var next = _waiting.Dequeue();
                yield return PlayCeremony(next.Warrior, next.Title, next.Legendary);
            }

            _playing = false;
        }

        /// <summary>
        /// Объект унесли вместе со сценой посреди очереди. Корутина
        /// уже не идёт, и флаг «играю» остался бы поднятым навсегда:
        /// следующая церемония встала бы в очередь, которую некому
        /// разгребать.
        /// </summary>
        void OnDisable()
        {
            _waiting.Clear();
            _playing = false;
        }

        private IEnumerator PlayCeremony(Warrior warrior, string title, bool isLegendary)
        {
            // Титул присуждается за деяние, а деяние бывает последним:
            // воин может не дожить до собственной церемонии. Наводить
            // камеру на уничтоженного нельзя — transform обращается
            // к нативной части и бросает MissingReferenceException,
            // а пауза при этом уже поставлена, и игра застыла бы навсегда.
            //
            // С очередью это стало вероятнее, а не реже: между
            // присуждением и наездом теперь стоят чужие церемонии.
            if (warrior == null) yield break;

            // Кадр делят пятеро, и ждать освобождения надо **до паузы**.
            // Пауза ставит timeScale в ноль, а чужой наезд (MomentCamera)
            // держит план через WaitForSeconds, то есть по игровому
            // времени: поставь мы паузу первой — он не кончился бы уже
            // никогда, и зритель смотрел бы в замерший кадр весь срок
            // ожидания. Сначала дожидаемся, потом останавливаем игру.
            var cameraController = FindFirstObjectByType<Dialogue.DialogueCameraController>();
            if (cameraController != null)
                yield return cameraController.WaitUntilFree();

            // Ждали в живой игре — за это время он мог погибнуть.
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
