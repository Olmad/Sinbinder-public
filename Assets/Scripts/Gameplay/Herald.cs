// Assets/Scripts/Gameplay/Herald.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Реплика, которая ведёт долю: строкой в журнал и кадром на того,
    /// кто её говорит.
    ///
    /// Автор, 26 сентября, пройдя демо: «Мне нравится, что от первого лица
    /// можно управлять войсками на мышку. Но некоторые моменты требуют
    /// камеры. Например почти все фразы Каргана». Они шли только журналом —
    /// строкой слева внизу, — и от первого лица, где Каргана в кадре нет,
    /// их пропускали. Теперь такая реплика приходит с наездом: камера
    /// разговора подъезжает к говорящему, строка ложится на нижнюю полосу,
    /// мир стоит, пока её можно прочесть. В журнале она остаётся — перечитать.
    ///
    /// Напоминания («Владыка, шар. Скорее») сюда не идут: повторённое
    /// перестаёт быть событием — то же правило, что у <see cref="MomentCamera"/>.
    /// </summary>
    public class Herald : MonoBehaviour
    {
        /// <summary>Сколько ждать занятого кадра, секунд. Дольше — реплика опоздала.</summary>
        private const float Patience = 6f;

        private static Herald _host;

        private readonly Queue<(string Label, string Words, Warrior Speaker)> _queue = new();
        private bool _speaking;

        /// <summary>
        /// Говорит ли кто-то сейчас или ждёт очереди — до конца возврата
        /// камеры. Спрашивает прогон демо: пока кадр занят, игрок ждёт.
        /// </summary>
        public static bool Busy => _host != null && _host._speaking;

        /// <summary>
        /// Сказать реплику вида «Имя: «слова»» — как она пишется в журнал.
        /// Строка уходит в журнал сразу и целиком; если говорящий стоит
        /// в сцене — следом наезд на него, слова на нижней полосе. Не того
        /// вида или говорящего нет — остаётся строка журнала.
        /// </summary>
        public static void Line(string line)
        {
            if (string.IsNullOrEmpty(line)) return;

            Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(line);

            if (!Split(line, out string label, out string words)) return;

            var speaker = Find(label);
            if (speaker == null) return;

            if (_host == null) _host = new GameObject("Глашатай").AddComponent<Herald>();

            _host._queue.Enqueue((label, words, speaker));
            if (!_host._speaking) _host.StartCoroutine(_host.Speak());
        }

        /// <summary>
        /// «Карган: «Владыка, шар». Он ждёт.» → «Карган» и «Владыка, шар».
        /// Слова — до последней закрывающей кавычки: ремарка после неё
        /// принадлежит журналу, а не голосу.
        /// </summary>
        public static bool Split(string line, out string label, out string words)
        {
            label = words = null;
            if (string.IsNullOrEmpty(line)) return false;

            int open = line.IndexOf(": «", System.StringComparison.Ordinal);
            int close = line.LastIndexOf('»');
            if (open <= 0 || close <= open + 3) return false;

            label = line.Substring(0, open);
            words = line.Substring(open + 3, close - open - 3);
            return true;
        }

        /// <summary>
        /// Сколько держать кадр: чтобы реплику успели прочесть, и не дольше.
        /// Около шестнадцати букв в секунду — медленное чтение, с запасом
        /// на то, что глаз сперва ищет строку.
        /// </summary>
        public static float Hold(string words)
            => Mathf.Clamp(1.2f + (words?.Length ?? 0) / 16f, 2.4f, 7f);

        /// <summary>
        /// Говорящий по имени из строки: живой свой, чьё имя совпадает
        /// или с него начинается — «Карган» зовёт Каргана Старого Ворона.
        /// </summary>
        private static Warrior Find(string label)
        {
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
            {
                if (w == null || w.IsDead || w.Team != Team.Player) continue;
                string name = w.DisplayName;
                if (name == label || name.StartsWith(label + " ", System.StringComparison.Ordinal))
                    return w;
            }
            return null;
        }

        private IEnumerator Speak()
        {
            _speaking = true;

            while (_queue.Count > 0)
            {
                var next = _queue.Dequeue();
                yield return Frame(next.Label, next.Words, next.Speaker);
            }

            _speaking = false;
        }

        private static IEnumerator Frame(string label, string words, Warrior speaker)
        {
            var camera = Dialogue.DialogueCameraController.Instance;
            if (camera == null) yield break;

            // Кадр занят разговором или поступком — подождём, но недолго:
            // реплика уже в журнале, и наезд через полминуты опоздал бы.
            float waited = 0f;
            while (camera != null && camera.InDialogue && waited < Patience)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            if (camera == null || camera.InDialogue) yield break;

            // Игра уже стоит — руки у игрока заняты панелью, и отнимать
            // у панели кадр нельзя. Так решает и MomentCamera.
            var pause = Core.GamePauseController.Instance;
            if (pause != null && pause.IsPaused) yield break;

            // Ждали в живой игре: говорящий мог пасть или уйти со сценой.
            if (speaker == null || speaker.IsDead) yield break;

            int mine = -1;
            if (pause != null) { pause.Pause(); mine = pause.Stamp; }

            camera.SaveCameraPosition();
            yield return camera.FocusOn(speaker.transform);

            UI.Letterbox.Instance?.Say(label, words);

            // Реальное время: игровое стоит.
            yield return new WaitForSecondsRealtime(Hold(words));

            if (camera != null)
            {
                camera.StopSway();
                yield return camera.RestoreCamera();
            }

            if (pause != null && pause.IsPaused && pause.Stamp == mine)
                pause.Resume();
        }
    }
}
