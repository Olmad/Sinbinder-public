// Assets/Scripts/UI/GameOverUI.cs
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Sinbinder.UI
{
    /// <summary>
    /// Конец игры: Греховод пал, или отряд лёг там, где экрана конца нет.
    ///
    /// Слово автора, 24 сентября: «После смерти Греховода полный game
    /// over». До этого мёртвый Греховод просто переставал действовать,
    /// а бой шёл дальше без того, кем играют.
    ///
    /// Экран собирается в коде, в любой сцене, а не сборщиком: конец
    /// игры случается там, где случается бой, и экран, который есть
    /// только в склепе, для набега не существует. Ровно так и было
    /// с гибелью отряда: <c>DemoEndUI</c> стоит лишь в склепе, и в набеге
    /// «Отряд не вернулся» уходило в консоль, а игрок оставался на пустом
    /// поле без единой кнопки.
    ///
    /// Кнопок две: начать сначала и выйти. Загрузить запись предлагает
    /// стартовый экран лагеря — второе место с тем же выбором только
    /// разошлось бы с ним. В ответственной игре записи к этому мигу уже
    /// нет: переиграть смерть нельзя, в этом уговор (<see cref="Core.Commitment"/>).
    /// </summary>
    public class GameOverUI : MonoBehaviour
    {
        private const string FirstScene = "Prologue_Camp";

        private static readonly Color Ink = new Color(0.05f, 0.05f, 0.06f);
        private static readonly Color Edge = new Color(0.62f, 0.58f, 0.50f);
        private static readonly Color Bone = new Color(0.94f, 0.90f, 0.80f);

        /// <summary>Показан ли уже. Второй конец поверх первого не нужен.</summary>
        public static bool Shown { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Shown = false;

        /// <summary>
        /// Показать конец игры и остановить мир. Зовут: Греховод, когда
        /// пал (<see cref="Gameplay.SinbinderPlayer"/>), и ведущий пролога,
        /// когда отряд лёг, а своего экрана конца в сцене нет.
        /// </summary>
        public static void Show(string title, string body)
        {
            if (Shown) return;
            Shown = true;

            // Кадр, который держал камеру, отпускаем: иначе флаг «в разговоре»
            // пережил бы смену сцены, и в новой игре Греховод не смог бы
            // ходить (PlayerWalk молчит, пока камера занята).
            Dialogue.DialogueCameraController.Instance?.StopSway();

            var go = new GameObject("Конец игры");
            go.AddComponent<GameOverUI>().Build(title, body);

            Core.GamePauseController.Instance?.Halt();
        }

        void OnDestroy()
        {
            // Сцену сменили — экран ушёл вместе с ней, и следующий конец
            // обязан показаться.
            Shown = false;
        }

        private void Build(string title, string body)
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            gameObject.AddComponent<GraphicRaycaster>();

            // Без EventSystem кнопки не нажимаются. Сцены демо его несут,
            // но экран конца обязан работать и там, где его забыли.
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            var font = UIFont();

            // Затемнение всего экрана: мир позади ещё виден, но уже не свой.
            var shade = Rect("Затемнение", transform, Vector2.zero, Vector2.one, Vector2.zero);
            shade.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

            var panel = Rect("Панель", transform, new Vector2(0.5f, 0.5f),
                             new Vector2(0.5f, 0.5f), new Vector2(900f, 460f));
            panel.gameObject.AddComponent<Image>().color = new Color(Ink.r, Ink.g, Ink.b, 0.96f);
            Frame(panel);

            Line("Заголовок", panel, font, title, 40, new Vector2(0f, -36f), 60f);
            Line("Слова", panel, font, body, 24, new Vector2(0f, -116f), 190f);

            Choice("Начать сначала", panel, font, new Vector2(-190f, 70f), Again);
            Choice("Выйти из игры", panel, font, new Vector2(190f, 70f), Application.Quit);
        }

        /// <summary>
        /// Новая игра с лагеря. Сброс отряда и прочей статики делает ведущий
        /// первой доли — ровно тем путём, что и при первом запуске.
        /// </summary>
        private static void Again()
        {
            Core.GamePauseController.Instance?.Unhalt();
            SceneManager.LoadScene(FirstScene);
        }

        // ──────────────────────────────────
        // Сборка
        // ──────────────────────────────────

        private static RectTransform Rect(string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        /// <summary>Волосяная рамка, как у прочих панелей во весь экран.</summary>
        private static void Frame(RectTransform panel)
        {
            const float Hair = 1.5f;
            var color = new Color(Edge.r, Edge.g, Edge.b, 0.30f);

            (Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)[] sides =
            {
                (new Vector2(0f, 1f), Vector2.one,         new Vector2(0f, -Hair), Vector2.zero),
                (Vector2.zero,        new Vector2(1f, 0f), Vector2.zero,           new Vector2(0f, Hair)),
                (Vector2.zero,        new Vector2(0f, 1f), Vector2.zero,           new Vector2(Hair, 0f)),
                (new Vector2(1f, 0f), Vector2.one,         new Vector2(-Hair, 0f), Vector2.zero),
            };

            foreach (var side in sides)
            {
                var go = new GameObject("Край", typeof(RectTransform));
                go.transform.SetParent(panel, false);

                var rt = (RectTransform)go.transform;
                rt.anchorMin = side.min;
                rt.anchorMax = side.max;
                rt.offsetMin = side.offMin;
                rt.offsetMax = side.offMax;

                var image = go.AddComponent<Image>();
                image.color = color;
                image.raycastTarget = false;
            }
        }

        private static void Line(string name, RectTransform panel, Font font,
            string text, int size, Vector2 position, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(panel, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(48f, 0f);
            rt.offsetMax = new Vector2(-48f, 0f);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
            rt.anchoredPosition = new Vector2(0f, position.y);

            var label = go.AddComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.color = Bone;
            label.alignment = TextAnchor.UpperLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.text = text;
        }

        private static void Choice(string title, RectTransform panel, Font font,
            Vector2 position, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(title, typeof(RectTransform));
            go.transform.SetParent(panel, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(320f, 84f);
            rt.anchoredPosition = position;

            var plate = go.AddComponent<Image>();
            plate.color = new Color(0.13f, 0.12f, 0.11f, 1f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = plate;
            button.onClick.AddListener(onClick);

            var label = new GameObject("Подпись", typeof(RectTransform));
            label.transform.SetParent(go.transform, false);

            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            var text = label.AddComponent<Text>();
            text.font = font;
            text.fontSize = 26;
            text.color = Bone;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;    // клик обязан доходить до кнопки
            text.text = title;
        }

        /// <summary>
        /// Шрифт — тот же, что у прочего интерфейса сцены; нет его —
        /// встроенный. Не нашлось ни одного — говорим вслух: экран конца
        /// без букв хуже, чем без экрана.
        /// </summary>
        private static Font UIFont()
        {
            var any = Object.FindFirstObjectByType<Text>(FindObjectsInactive.Include);
            if (any != null && any.font != null) return any.font;

            var builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (builtin == null)
                Debug.LogWarning("[КОНЕЦ] Шрифта нет: ни одного Text в сцене, "
                               + "ни встроенного. Экран конца будет без букв.");
            return builtin;
        }
    }
}
