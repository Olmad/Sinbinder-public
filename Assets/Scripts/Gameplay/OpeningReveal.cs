// Assets/Scripts/Gameplay/OpeningReveal.cs
// Перевод: текст через Loc
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Открытие игры. Автор, 15 октября: «палатка закрыта тканью, при выходе
    /// Греховод должен отодвинуть ткань от первого лица и над лагерем должна
    /// появиться красивая стилизованная надпись Sinbinder… Вобщем наведи
    /// красоту».
    ///
    /// Проснулся в шатре — глазами Греховода. Подошёл к выходу — полог
    /// откидывается (<see cref="TentFlap"/>). Вышел за порог — над лагерем
    /// проявляется надпись: картинка <c>Resources/Art/SinbinderLogo</c>
    /// («S» — коса, точка над «i» — склянка с огоньком души; рисует
    /// <c>Tools/art/logo.py</c>) и девиз «Не командуй. Искушай.» Подержав,
    /// камера поднимается из глаз краном в вид сверху
    /// (<see cref="RTS_Camera.Crane"/>), надпись тает.
    ///
    /// Один раз за новую игру: загрузка посреди лагеря, разгром и «С начала
    /// доли» начинаются без него — Греховод там не в шатре. Время —
    /// настоящее: мир не стоит, лагерь живёт и под надписью.
    /// </summary>
    public class OpeningReveal : MonoBehaviour
    {
        /// <summary>Надпись на экране. Спрашивает прогон: снять кадр.</summary>
        public static bool Showing { get; private set; }

        /// <summary>С какого мига (настоящее время) надпись проявилась целиком.</summary>
        public static float ShownSince { get; private set; }

        /// <summary>
        /// Открытие идёт — от пробуждения в шатре до конца крана. Лагерь
        /// ждёт его (<see cref="CampOpening"/>): провожатый заговаривал
        /// в тот же миг, когда Греховод выходил, и надпись ложилась поверх
        /// наезда на него, а не над лагерем (кадр прогона 15 октября).
        /// </summary>
        public static bool Busy { get; private set; }

        private const float Settle = 0.6f;     // лагерь входит в кадр
        private const float FadeIn = 1.6f;
        private const float Hold = 2.4f;
        private const float CraneSeconds = 3.2f;
        private const float FadeOut = 1.8f;

        private static readonly string Motto = Loc.N("Не командуй. Искушай.");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() { Showing = false; Busy = false; }

        void OnDestroy() { Showing = false; Busy = false; }

        IEnumerator Start()
        {
            Showing = false;
            Busy = false;

            // Сперва — вопрос о сохранении и строка на чёрном: «Продолжить»
            // переносит Греховода туда, где его записали, и открытие
            // решается по тому, где он оказался.
            float waited = 0f;
            while ((!SinbinderPlayer.Exists || UI.StartPanel.Waiting) && waited < 600f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            if (!SinbinderPlayer.Exists || RaidEvent.Running) yield break;

            var tent = TentInterior.Around(SinbinderPlayer.Where);
            if (tent == null) yield break;     // проснулся не в шатре — открытия нет

            Busy = true;
            var view = Camera.main != null ? Camera.main.GetComponent<RTS_Camera>() : null;
            if (view != null && !view.FirstPersonNow) view.Switch();   // глазами Греховода

            // Ждём, пока выйдет за порог.
            while (SinbinderPlayer.Exists && tent != null && tent.Contains(SinbinderPlayer.Where))
                yield return null;
            if (!SinbinderPlayer.Exists || RaidEvent.Running) { Busy = false; yield break; }

            yield return new WaitForSecondsRealtime(Settle);
            yield return Reveal(view);
            Busy = false;
        }

        private IEnumerator Reveal(RTS_Camera view)
        {
            var art = Resources.Load<Texture2D>("Art/SinbinderLogo");
            if (art == null)
            {
                Debug.LogWarning("[ОТКРЫТИЕ] Надписи нет: Resources/Art/SinbinderLogo.png. "
                               + "Нарисовать — Tools/art/logo.py.");
                yield break;
            }

            var root = Build(art, out var group, out var logo);
            ShownSince = float.MaxValue;   // целиком ещё не проявилась
            Showing = true;

            for (float t = 0f; t < FadeIn; t += Time.unscaledDeltaTime)
            {
                float k = Smooth(t / FadeIn);
                group.alpha = k;
                logo.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, k);
                yield return null;
            }
            group.alpha = 1f;
            logo.localScale = Vector3.one;
            ShownSince = Time.realtimeSinceStartup;

            yield return new WaitForSecondsRealtime(Hold);

            // Кран и угасание разом: надпись тает, пока камера поднимается.
            if (view != null && view.FirstPersonNow) view.StartCoroutine(view.Crane(CraneSeconds));
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, CraneSeconds - FadeOut));

            for (float t = 0f; t < FadeOut; t += Time.unscaledDeltaTime)
            {
                group.alpha = 1f - Smooth(t / FadeOut);
                yield return null;
            }

            Destroy(root);
            Showing = false;
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// Свой холст поверх всего, кроме зерна кадра (<see cref="UI.ScreenGrain"/>):
        /// надпись — часть кадра, а не панель. Вверху по центру — над лагерем.
        /// </summary>
        private static GameObject Build(Texture2D art, out CanvasGroup group, out RectTransform logo)
        {
            var root = new GameObject("Надпись над лагерем");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 85;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            const float width = 940f;
            float height = width * art.height / art.width;

            var image = new GameObject("Sinbinder", typeof(RectTransform));
            image.transform.SetParent(root.transform, false);
            logo = (RectTransform)image.transform;
            logo.anchorMin = logo.anchorMax = new Vector2(0.5f, 1f);
            logo.pivot = new Vector2(0.5f, 1f);
            logo.sizeDelta = new Vector2(width, height);
            logo.anchoredPosition = new Vector2(0f, -60f);

            var img = image.AddComponent<Image>();
            img.sprite = Sprite.Create(art, new Rect(0f, 0f, art.width, art.height), new Vector2(0.5f, 0.5f));
            img.preserveAspect = true;
            img.raycastTarget = false;

            var motto = new GameObject("Девиз", typeof(RectTransform));
            motto.transform.SetParent(root.transform, false);
            var mt = (RectTransform)motto.transform;
            mt.anchorMin = mt.anchorMax = new Vector2(0.5f, 1f);
            mt.pivot = new Vector2(0.5f, 1f);
            mt.sizeDelta = new Vector2(900f, 60f);
            mt.anchoredPosition = new Vector2(0f, -60f - height * 0.86f);

            var text = motto.AddComponent<Text>();
            text.font = UI.UiStyle.Title;
            text.fontSize = 36;
            text.alignment = TextAnchor.UpperCenter;
            text.color = new Color(0.86f, 0.80f, 0.68f, 0.92f);
            text.text = Loc.T(Motto);
            text.raycastTarget = false;
            var shadow = motto.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(2f, -2f);

            return root;
        }
    }
}
