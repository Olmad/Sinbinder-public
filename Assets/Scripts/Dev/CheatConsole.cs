// Assets/Scripts/Dev/CheatConsole.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Sinbinder.Dev
{
    /// <summary>
    /// Консоль на ~ — строка вверху экрана, куда пишут команду.
    ///
    /// Автор, 24 сентября: «Консоль должна открываться на ~». Клавиша
    /// была запретной («на ё ничего не планировать» — это та же клавиша)
    /// именно ради неё; запрет снят.
    ///
    /// Команд пока две: <c>aos</c> — съёмка (<see cref="Shooting"/>),
    /// <c>помощь</c> — список. Секретной консоли внутри этой нет и пока
    /// не будет: автор отложил её, «посмотрим, как получится с игрой».
    ///
    /// Ставит себя сама в первой сцене и живёт между сценами, как
    /// <see cref="CaptureMode"/>: сборщик сцен о ней не знает, и
    /// пересобирать ради неё ничего не нужно. Пока открыта, клавиши игры
    /// молчат (<see cref="InputHush"/>): иначе «aos» водило бы камеру.
    /// </summary>
    public class CheatConsole : MonoBehaviour
    {
        [SerializeField] private KeyCode _key = KeyCode.BackQuote;

        /// <summary>Открыта ли консоль. Съёмка на это время замолкает.</summary>
        public static bool Open { get; private set; }

        /// <summary>
        /// Кадр, в котором консоль закрылась. Esc закрывает консоль, и тот же
        /// Esc в том же кадре не должен открыть меню паузы.
        /// </summary>
        public static int ClosedAt { get; private set; } = -1;

        /// <summary>Esc этого кадра принадлежит консоли.</summary>
        public static bool OwnsEscape => Open || ClosedAt == Time.frameCount;

        private const int Remembered = 6;

        private GameObject _bar;
        private InputField _input;
        private Text _log;
        private readonly List<string> _lines = new();
        private GameObject _ownSystem;
        private int _openedAt = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm()
        {
            Open = false;
            ClosedAt = -1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<CheatConsole>(FindObjectsInactive.Include) != null) return;

            var go = new GameObject("Консоль");
            go.AddComponent<CheatConsole>();
            DontDestroyOnLoad(go);
        }

        void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Open) Close();
        }

        void Update()
        {
            if (!Open)
            {
                if (Input.GetKeyDown(_key)) Show();
                return;
            }

            // Кадр открытия пропускаем: та же ~ иначе закрыла бы консоль
            // сразу, а поле успело бы поймать её символом.
            if (Time.frameCount == _openedAt) return;

            if (Input.GetKeyDown(_key) || Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Run(_input.text);
                _input.text = "";
                _input.ActivateInputField();
            }
        }

        // ──────────────────────────────────
        // Команды
        // ──────────────────────────────────

        /// <summary>
        /// Команды: слово → что делать. Таблица, а не ветки: команду пишет
        /// игрок, и новая добавляется одной строкой. «Съёмка» через ё
        /// не наберётся вовсе — ё закрывает консоль, — поэтому «съемка».
        /// </summary>
        private static readonly Dictionary<string, System.Action<CheatConsole>> Commands = new()
        {
            ["aos"] = c => c.Shoot(),
            ["аос"] = c => c.Shoot(),
            ["съемка"] = c => c.Shoot(),
            ["помощь"] = c => c.Help(),
            ["help"] = c => c.Help(),
            ["?"] = c => c.Help(),
        };

        private void Run(string typed)
        {
            string command = Clean(typed);
            if (command.Length == 0) return;

            Write("> " + command);

            if (Commands.TryGetValue(command, out var act)) act(this);
            else Write("Такой команды нет. «помощь» — список.");
        }

        private void Shoot()
        {
            if (!Shooting.Toggle())
            {
                Write("Съёмка окончена.");
                return;
            }

            Write("Съёмка. Клавиши — на экране, F1 прячет их.");
            Close();
        }

        private void Help()
        {
            Write("aos — съёмка: буквы A, O, S, свободная камера, позы.");
            Write("помощь — этот список. ~ или Esc — закрыть.");
        }

        /// <summary>
        /// Строка без мусора. ~ и ё — та же клавиша, что открывает консоль,
        /// и поле иногда ловит её символом; регистр игроку не важен.
        /// </summary>
        private static string Clean(string typed)
        {
            if (string.IsNullOrEmpty(typed)) return "";
            return typed.Replace("`", "").Replace("~", "")
                        .Trim().ToLowerInvariant();
        }

        private void Write(string line)
        {
            _lines.Add(line);
            while (_lines.Count > Remembered) _lines.RemoveAt(0);
            if (_log != null) _log.text = string.Join("\n", _lines);
        }

        // ──────────────────────────────────
        // Показ
        // ──────────────────────────────────

        private void Show()
        {
            if (_bar == null) Build();

            Open = true;
            _openedAt = Time.frameCount;
            _bar.SetActive(true);
            InputHush.Hold(this, menuToo: true);

            // Поле ввода работает только при EventSystem. В сценах демо он
            // есть; в пустой сцене ставим свой и убираем, закрываясь.
            if (EventSystem.current == null)
            {
                _ownSystem = new GameObject("События консоли", typeof(EventSystem),
                                            typeof(StandaloneInputModule));
            }

            _input.text = "";
            _input.ActivateInputField();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_input.gameObject);
        }

        private void Close()
        {
            Open = false;
            ClosedAt = Time.frameCount;
            if (_bar != null) _bar.SetActive(false);
            InputHush.Release(this);

            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (_ownSystem != null) Destroy(_ownSystem);
            _ownSystem = null;
        }

        private void Build()
        {
            var font = Fonts.Find();

            var canvasGo = new GameObject("Холст консоли", typeof(Canvas),
                                          typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            _bar = new GameObject("Строка", typeof(RectTransform), typeof(Image));
            var bar = (RectTransform)_bar.transform;
            bar.SetParent(canvasGo.transform, false);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.sizeDelta = new Vector2(0f, 190f);
            _bar.GetComponent<Image>().color = new Color(0.04f, 0.035f, 0.03f, 0.92f);

            _log = Label("Журнал консоли", bar, font, 20, new Color(0.72f, 0.68f, 0.60f));
            var logRt = _log.rectTransform;
            logRt.anchorMin = new Vector2(0f, 0f);
            logRt.anchorMax = new Vector2(1f, 1f);
            logRt.offsetMin = new Vector2(24f, 52f);
            logRt.offsetMax = new Vector2(-24f, -10f);
            _log.alignment = TextAnchor.LowerLeft;

            var fieldGo = new GameObject("Ввод", typeof(RectTransform), typeof(Image), typeof(InputField));
            var field = (RectTransform)fieldGo.transform;
            field.SetParent(bar, false);
            field.anchorMin = new Vector2(0f, 0f);
            field.anchorMax = new Vector2(1f, 0f);
            field.pivot = new Vector2(0.5f, 0f);
            field.sizeDelta = new Vector2(-32f, 40f);
            field.anchoredPosition = new Vector2(0f, 8f);
            fieldGo.GetComponent<Image>().color = new Color(0.12f, 0.10f, 0.09f, 1f);

            var text = Label("Текст", field, font, 22, new Color(0.94f, 0.92f, 0.86f));
            Stretch(text.rectTransform, 10f);
            text.supportRichText = false;

            var hint = Label("Подсказка", field, font, 22, new Color(0.45f, 0.42f, 0.38f));
            Stretch(hint.rectTransform, 10f);
            hint.text = "команда… «помощь» — список";
            hint.fontStyle = FontStyle.Italic;

            _input = fieldGo.GetComponent<InputField>();
            _input.textComponent = text;
            _input.placeholder = hint;
            _input.lineType = InputField.LineType.SingleLine;

            Write("Консоль. «помощь» — список команд.");
            _bar.SetActive(false);
        }

        private static Text Label(string name, RectTransform parent, Font font, int size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAnchor.MiddleLeft;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            return t;
        }

        private static void Stretch(RectTransform rt, float pad)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(pad, 0f);
            rt.offsetMax = new Vector2(-pad, 0f);
        }
    }

    /// <summary>Шрифт для того, что собирается кодом: как у сцены, иначе встроенный.</summary>
    internal static class Fonts
    {
        public static Font Find()
        {
            var any = Object.FindFirstObjectByType<Text>(FindObjectsInactive.Include);
            if (any != null && any.font != null) return any.font;

            var builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (builtin == null)
                Debug.LogWarning("[КОНСОЛЬ] Шрифта нет: ни одного Text в сцене, ни встроенного.");
            return builtin;
        }
    }
}
