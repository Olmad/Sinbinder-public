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
            Loc.N("Вид сверху. V — снова глазами Греховода. Осмотрите лагерь: в нём есть что найти.");

        private const float FadeIn = 0.8f, Hold = 6.5f, FadeOut = 1.4f;

        public static bool Shown { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Shown = false;

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
        private CanvasGroup _group;

        void Update()
        {
            if (_since >= 0f) { Fade(); return; }
            if (Shown) { Destroy(gameObject); return; }

            var cam = Camera.main != null ? Camera.main.GetComponent<RTS_Camera>() : null;
            if (cam == null) return;

            bool eyes = cam.FirstPersonNow;
            bool busy = OpeningReveal.Busy || Lesson.Holding
                     || (Letterbox.Instance != null && Letterbox.Instance.Shown);

            // Из глаз — наверх: запомнить всегда (выход из шатра идёт под сценой
            // открытия), сказать — когда сцена кончилась и ничего не мешает.
            if (_wasEyes && !eyes) _pending = true;
            _wasEyes = eyes;
            if (_pending && !eyes && !busy) Show();
        }

        private bool _pending;

        private void Show()
        {
            Shown = true;
            _since = Time.unscaledTime;
            Build();
            Debug.Log("[ПОДСКАЗКА] вид сверху: тихая строка о V и секретах лагеря.");
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;

            var go = new GameObject("Строка", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -120f);
            rt.sizeDelta = new Vector2(1100f, 60f);

            var text = go.AddComponent<Text>();
            text.font = UiStyle.Thought;   // курсив — голосом мысли, не приказом
            text.fontSize = 26;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.86f, 0.83f, 0.76f, 1f);
            text.text = Loc.T(Line);
            text.raycastTarget = false;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
        }

        private void Fade()
        {
            float t = Time.unscaledTime - _since;
            float a = t < FadeIn ? t / FadeIn
                    : t < FadeIn + Hold ? 1f
                    : 1f - (t - FadeIn - Hold) / FadeOut;
            if (_group != null) _group.alpha = Mathf.Clamp01(a) * 0.9f;
            if (t > FadeIn + Hold + FadeOut) Destroy(gameObject);
        }
    }
}
