// Assets/Scripts/UI/ViewHintUI.cs
// Перевод: текст через Loc
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.UI
{
    /// <summary>
    /// Тихая строка о виде сверху. Автор, 26 октября: «при выходе Греховода
    /// из палатки и переходе в тактический вид — должно аккуратно появиться тихая
    /// подсказка о переключении вида, может даже с советом осмотреть лагерь
    /// на секреты».
    ///
    /// Один раз: в первый миг, когда камера после шатра встала сверху
    /// (<see cref="RTS_Camera.FirstPersonNow"/> сменилось на «нет»). Тихо — не
    /// прилетает из центра, как разовые подсказки (<see cref="HintArrival"/>),
    /// а проявляется вверху по центру, стоит и гаснет: это не правило, без
    /// которого игра не пойдёт, а приглашение. Ждёт, пока идёт сцена или урок.
    /// Собирает себя сама — сцены пересобирать не нужно.
    /// </summary>
    public class ViewHintUI : MonoBehaviour
    {
        private static readonly string Line =
            Loc.N("Вид сверху. Tab — снова глазами Греховода. Осмотрите лагерь: в нём есть что найти.");

        private const float FadeIn = 0.8f, Hold = 6.5f, FadeOut = 1.4f;

        public static bool Shown { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Shown = false;

        /// <summary>Новая игра: строка о виде сверху — снова.</summary>
        public static void Forget() => Shown = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Shown || Object.FindFirstObjectByType<ViewHintUI>() != null) return;
            new GameObject("Подсказка вида").AddComponent<ViewHintUI>();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= Again;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += Again;
        }

        private static void Again(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
        {
            if (!Shown && Object.FindFirstObjectByType<ViewHintUI>() == null)
                new GameObject("Подсказка вида").AddComponent<ViewHintUI>();
        }

        private bool _wasEyes;
        private float _since = -1f;

        void Update()
        {
            if (_since >= 0f) { Fade(); return; }
            if (Shown) { Destroy(gameObject); return; }

            var cam = Camera.main != null ? Camera.main.GetComponent<RTS_Camera>() : null;
            if (cam == null) return;
            // Во сне лагеря нет: «Осмотрите лагерь» звучала у врат склепа (w0811c). Строка ждёт лагеря.
            if (DreamTutorialDirector.Instance != null) { _wasEyes = false; _pending = false; return; }

            bool eyes = cam.FirstPersonNow;
            bool busy = OpeningReveal.Busy || Lesson.Holding
                     || (Letterbox.Instance != null && Letterbox.Instance.Covering);

            // Из глаз — наверх: запомнить всегда (выход из шатра идёт под сценой
            // открытия), сказать — когда сцена кончилась и ничего не мешает.
            if (_wasEyes && !eyes) _pending = true;
            _wasEyes = eyes;
            if (_pending && !eyes && !busy) Show();
        }

        private bool _pending;

        /// <summary>
        /// На плашке урока — Tab в рамке, как подсказки сна (автор, 17.11: «в лагере и в сне
        /// обучение и плашки с заданиями отличаются»). До того — курсивная строка вверху.
        /// </summary>
        private void Show()
        {
            Shown = true;
            _since = Time.unscaledTime;
            Lesson.Hint(this, "Tab", Loc.T(Line));
            Debug.Log("[ПОДСКАЗКА] вид сверху: строка о Tab и секретах лагеря.");
        }

        void OnDestroy() => Lesson.Unhint(this);

        private void Fade()
        {
            if (Time.unscaledTime - _since > FadeIn + Hold) Destroy(gameObject);
        }
    }
}
