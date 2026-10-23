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

        /// <summary>Надпись над лагерем: «Sinbinder» и «Греховод» (Tools/art/logo.py, --ru).</summary>
        private const string Logo = "Art/SinbinderLogo", LogoRu = "Art/SinbinderLogoRu";
        private static readonly string WakeLine = Loc.N("За пологом трещит костёр. Лагерь ждёт.");

        /// <summary>
        /// Мысль Греховода на выходе — слова автора, 23.10 (37-DEMO §8.7).
        /// Не в шатре: там «они» не на кого показать, а первая минута и так
        /// занята — сон, подъём, строка шатра, клавиши, задачи. Здесь — когда
        /// кран показал лагерь целиком, и следом провожатый сам просится рядом:
        /// «увязались» сказано — и тут же видно.
        /// </summary>
        private static readonly string ArrivalThought = Loc.N("Я хотел уйти один. Но они увязались за мной.");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() { Showing = false; Busy = false; Dreaming = false; Lying = false; Autopilot = false; }

        void OnDestroy() { Showing = false; Busy = false; Dreaming = false; Lying = false; }

        IEnumerator Start()
        {
            Showing = false;
            Busy = false;

            // Сперва — вопрос о сохранении и строка на чёрном: «Продолжить»
            // переносит Греховода туда, где его записали, и открытие
            // решается по тому, где он оказался.
            float waited = 0f;
            while ((!SinbinderPlayer.Exists || UI.StartPanel.Waiting || UI.PrologueTitleUI.Showing) && waited < 600f)
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

            // Сон и пробуждение на постели (п. 22 отзыва 21.10).
            var door = tent.Doorstep(0f);
            yield return Dream(view, tent, door);

            // Проснулся — и смотрит на выход, за которым горит костёр. Раньше
            // взгляд упирался в скат: две плоскости ткани, ни точки, куда
            // смотреть, — «читается как сбой отрисовки» (отзыв игрока 19.10).
            if (view != null) view.Face(door);
            var glow = Glow(tent);
            var hint = Hint(out var hintGroup);

            // Ждём, пока выйдет за порог. Строка проявляется не сразу:
            // первую секунду кадр принадлежит шатру.
            for (float t = 0f; SinbinderPlayer.Exists && tent != null && tent.Contains(SinbinderPlayer.Where); t += Time.unscaledDeltaTime)
            {
                // Пока вещь рассказывает (карта, постель — Inspectable), строка
                // пробуждения молчит: печаталась поверх её строк (прогон 22.10).
                hintGroup.alpha = Inspectable.Telling ? 0f : Mathf.Clamp01((t - 1.2f) / 1.0f);
                yield return null;
            }
            Destroy(hint);
            if (SinbinderPlayer.Exists && tent != null)
            {
                var local = tent.transform.InverseTransformPoint(SinbinderPlayer.Where);
                Debug.Log($"[ОТКРЫТИЕ] вышел из шатра: в осях шатра ({local.x:0.00}; {local.y:0.00}; {local.z:0.00})");
            }
            if (glow != null) StartCoroutine(Dim(glow));
            if (!SinbinderPlayer.Exists || RaidEvent.Running) { Busy = false; yield break; }

            yield return new WaitForSecondsRealtime(Settle);
            yield return Reveal(view);
            yield return Think(Loc.T(ArrivalThought));
            Busy = false;
        }

        // ───────────────────────────── сон ─────────────────────────────

        /// <summary>Идёт сон: темнота и огонёк. Спрашивает прогон.</summary>
        public static bool Dreaming { get; private set; }

        /// <summary>Проснулся и лежит — ждёт F. Спрашивает прогон.</summary>
        public static bool Lying { get; private set; }

        /// <summary>Насколько дошёл до огонька, 0–1. Спрашивает прогон: кадр на полпути.</summary>
        public static float DreamProgress { get; private set; }

        /// <summary>
        /// Прогон без клавиатуры: идёт к огоньку сам и поднимается сам —
        /// тем же путём, что и игрок, только вместо нажатий.
        /// </summary>
        public static bool Autopilot { get; set; }

        private static readonly string DreamLine = Loc.N("Иди к свету — W");
        private static readonly string RiseLine = Loc.N("F — подняться");

        private const float DreamWalk = 4.5f;   // секунд с зажатой W до света

        /// <summary>
        /// Сон. Автор, 21 октября, п. 22: «хочу в начале сон: темнота, белый
        /// огонёк, игрок тянет руку, и механически ему нужно подойти. При подходе
        /// игрок просыпается. Полноценно — он лежит на кровати, и ему нужно нажать
        /// F, чтобы подняться».
        ///
        /// Темнота во весь экран, в ней огонёк. Пока зажат шаг вперёд, огонёк
        /// приближается — растёт и светлеет; дошёл — свет заливает всё. Белое
        /// тает — Греховод лежит у постели, смотрит в полог. F — поднимается:
        /// глаза из-под подушки в рост, взгляд на выход. Ноги до того стоят.
        /// </summary>
        private IEnumerator Dream(RTS_Camera view, TentInterior tent, Vector3 door)
        {
            var hero = SinbinderPlayer.Instance;
            var walk = hero != null ? hero.GetComponent<PlayerWalk>() : null;
            if (walk != null) walk.enabled = false;

            // Лечь у постели — на метку «Где проснулся» (сборщик: на полу шатра,
            // рядом с постелью). Ближайшая к самой постели земля навмеша лежала
            // за скатом, снаружи: открытие считало, что Греховод уже вышел, и
            // надпись вставала посреди шатра (прогон 22.10).
            var wake = tent.transform.Find("Где проснулся");
            if (wake != null && hero != null)
            {
                var agent = hero.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.isOnNavMesh
                    && UnityEngine.AI.NavMesh.SamplePosition(wake.position, out var near, 0.6f, UnityEngine.AI.NavMesh.AllAreas)
                    && tent.Contains(near.position + Vector3.up * 0.1f))
                    agent.Warp(near.position);
            }
            if (view != null) { view.Lying = 1f; view.Face(door); view.Pitch = -58f; }

            var root = DreamCanvas(out var dark, out var light, out var hint, out var white);
            Dreaming = true;
            DreamProgress = 0f;

            for (float t = 0f; DreamProgress < 1f; t += Time.unscaledDeltaTime)
            {
                bool forward = Autopilot || Input.GetAxisRaw("Vertical") > 0.2f || Input.GetKey(KeyCode.W);
                if (forward) DreamProgress = Mathf.Min(1f, DreamProgress + Time.unscaledDeltaTime / DreamWalk);

                // Огонёк дышит, растёт к концу пути и светлеет.
                float breath = 1f + 0.06f * Mathf.Sin(t * 2.2f);
                float k = DreamProgress * DreamProgress;
                light.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(150f, 1700f, k) * breath;
                light.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.75f, 1f, DreamProgress));
                hint.color = new Color(0.85f, 0.82f, 0.76f, Mathf.Clamp01((t - 2.0f) / 1.2f) * (1f - DreamProgress * 2f));
                yield return null;
            }

            // Свет заливает всё.
            for (float t = 0f; t < 0.7f; t += Time.unscaledDeltaTime)
            {
                white.color = new Color(1f, 1f, 1f, Smooth(t / 0.7f));
                yield return null;
            }
            dark.color = Color.clear;
            light.color = Color.clear;
            Dreaming = false;

            // Белое тает — шатёр, полог над головой.
            hint.text = Loc.T(RiseLine);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            hint.rectTransform.anchoredPosition = new Vector2(0f, 140f);
            for (float t = 0f; t < 1.6f; t += Time.unscaledDeltaTime)
            {
                white.color = new Color(1f, 1f, 1f, 1f - Smooth(t / 1.6f));
                yield return null;
            }
            white.color = Color.clear;
            Lying = true;

            float waited = 0f;
            while (!(Input.GetKeyDown(KeyCode.F) || (Autopilot && waited > 1.2f)))
            {
                waited += Time.unscaledDeltaTime;
                hint.color = new Color(0.90f, 0.84f, 0.72f, Mathf.Clamp01((waited - 0.4f) / 0.8f));
                yield return null;
            }
            Destroy(root);

            // Поднимается: глаза в рост, взгляд на выход.
            float fromPitch = view != null ? view.Pitch : 0f;
            for (float t = 0f; t < 1.1f && view != null; t += Time.unscaledDeltaTime)
            {
                float k = Smooth(t / 1.1f);
                view.Lying = 1f - k;
                view.Pitch = Mathf.Lerp(fromPitch, 0f, k);
                yield return null;
            }
            if (view != null) { view.Lying = 0f; view.Pitch = 0f; }
            Lying = false;
            if (walk != null) walk.enabled = true;
        }

        private static Texture2D _glowTex;

        private static GameObject DreamCanvas(out Image dark, out Image light, out Text hint, out Image white)
        {
            var root = new GameObject("Сон");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UI.UiStyle.Reference;
            scaler.matchWidthOrHeight = 0.5f;

            Image Full(string name, Color c)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(root.transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                var img = go.GetComponent<Image>();
                img.color = c;
                img.raycastTarget = false;
                return img;
            }

            dark = Full("Темнота", Color.black);

            if (_glowTex == null)
            {
                const int n = 128;
                _glowTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        // Ядро и мягкий ореол без края: точка света, а не шар
                        // (первый заход линейным спадом читался серым диском).
                        float core = Mathf.Exp(-(r / 0.10f) * (r / 0.10f));
                        float halo = Mathf.Exp(-(r / 0.28f) * (r / 0.28f)) * 0.35f;
                        float a = Mathf.Clamp01(core + halo) * Mathf.Clamp01((1f - r) * 6f);
                        _glowTex.SetPixel(x, y, new Color(1f, 0.98f, 0.94f, a));
                    }
                _glowTex.Apply();
            }
            var lg = new GameObject("Огонёк", typeof(RectTransform), typeof(Image));
            lg.transform.SetParent(root.transform, false);
            light = lg.GetComponent<Image>();
            light.sprite = Sprite.Create(_glowTex, new Rect(0, 0, _glowTex.width, _glowTex.height), new Vector2(0.5f, 0.5f));
            light.raycastTarget = false;
            var lr = (RectTransform)lg.transform;
            lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.52f);
            lr.sizeDelta = Vector2.one * 150f;

            var tg = new GameObject("Строка сна", typeof(RectTransform), typeof(Text));
            tg.transform.SetParent(root.transform, false);
            hint = tg.GetComponent<Text>();
            hint.font = UI.UiStyle.Body;
            hint.fontSize = 28;
            hint.alignment = TextAnchor.MiddleCenter;
            hint.text = Loc.T(DreamLine);
            hint.color = Color.clear;
            hint.raycastTarget = false;
            var hr = (RectTransform)tg.transform;
            hr.anchorMin = hr.anchorMax = new Vector2(0.5f, 0.30f);
            hr.sizeDelta = new Vector2(900f, 60f);

            white = Full("Свет", Color.clear);
            return root;
        }

        private IEnumerator Reveal(RTS_Camera view)
        {
            // Надпись — на языке игры, выбранном у двери (автор, 23 октября:
            // «нужна такая же надпись над лагерем, но на русском»; «надпись
            // должна зависеть от языка игры, выбранного в начале»): по-русски —
            // «Греховод» (коса в «Г», душа в колбе вместо «о»), иначе — «Sinbinder».
            // Русской нет — английская, как прежде.
            Texture2D art = null;
            if (Loc.IsSource) art = Resources.Load<Texture2D>(LogoRu);
            if (art == null) art = Resources.Load<Texture2D>(Logo);
            if (art == null)
            {
                Debug.LogWarning("[ОТКРЫТИЕ] Надписи нет: Resources/" + Logo + ".png. "
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

        /// <summary>
        /// Отсвет костра на пологе: тёплое пятно у выхода, дышит, как огонь.
        /// Тени не бросает — он только говорит, куда идти.
        /// </summary>
        private static Light Glow(TentInterior tent)
        {
            var go = new GameObject("Отсвет костра у полога");
            go.transform.SetParent(tent.transform, false);
            go.transform.position = tent.Doorstep(-0.35f) + Vector3.up * tent.Top * 0.45f;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.58f, 0.26f);
            light.intensity = 1.8f;
            light.range = 2.8f;
            light.shadows = LightShadows.None;
            go.AddComponent<FireFlicker>().Tune(0.22f, 2.6f, 0.03f);
            return light;
        }

        private static IEnumerator Dim(Light light)
        {
            // Мерцание задаёт яркость каждый кадр — гасить поверх него нельзя.
            var flicker = light != null ? light.GetComponent<FireFlicker>() : null;
            if (flicker != null) flicker.enabled = false;
            float from = light != null ? light.intensity : 0f;
            for (float t = 0f; t < 1.5f && light != null; t += Time.unscaledDeltaTime)
            {
                light.intensity = from * (1f - t / 1.5f);
                yield return null;
            }
            if (light != null) Destroy(light.gameObject);
        }

        /// <summary>Одна строка внизу, пока Греховод в шатре: что там и как выйти.</summary>
        private static GameObject Hint(out CanvasGroup group)
            => Line("Строка пробуждения", Loc.T(WakeLine), UI.UiStyle.Body, Bone, out group);

        private static readonly Color Bone = new(0.90f, 0.84f, 0.72f);

        /// <summary>
        /// Мысль — та же строка внизу, курсивом и тусклее (как
        /// <see cref="UI.Letterbox.Think"/>): проступает, держится, чтобы
        /// прочли, и тает. Мир не стоит — это не урок, а взгляд.
        /// </summary>
        private static IEnumerator Think(string thought)
        {
            if (string.IsNullOrEmpty(thought)) yield break;

            var root = Line("Мысль Греховода", thought, UI.UiStyle.Thought,
                            UI.Letterbox.Thinking(Bone), out var group);
            for (float t = 0f; t < 0.7f; t += Time.unscaledDeltaTime)
            {
                group.alpha = Smooth(t / 0.7f);
                yield return null;
            }
            group.alpha = 1f;
            yield return new WaitForSecondsRealtime(Herald.Hold(thought) + 0.4f);
            for (float t = 0f; t < 0.7f; t += Time.unscaledDeltaTime)
            {
                group.alpha = 1f - Smooth(t / 0.7f);
                yield return null;
            }
            Destroy(root);
        }

        /// <summary>Свой холст и одна строка внизу по центру, прозрачная до показа.</summary>
        private static GameObject Line(string name, string words, Font font, Color colour, out CanvasGroup group)
        {
            var root = new GameObject(name);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 84;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Sinbinder.UI.UiStyle.Reference;
            scaler.matchWidthOrHeight = 0.5f;

            group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var line = new GameObject("Строка", typeof(RectTransform));
            line.transform.SetParent(root.transform, false);
            var rt = (RectTransform)line.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(1400f, 120f);
            rt.anchoredPosition = new Vector2(0f, 110f);

            var text = line.AddComponent<Text>();
            text.font = font;
            text.fontSize = 30;
            text.alignment = TextAnchor.LowerCenter;
            text.color = colour;
            text.raycastTarget = false;
            text.text = words;
            var shadow = line.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(2f, -2f);

            return root;
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
            scaler.referenceResolution = Sinbinder.UI.UiStyle.Reference;
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
