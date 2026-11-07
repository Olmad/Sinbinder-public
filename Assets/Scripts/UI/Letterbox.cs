// Assets/Scripts/UI/Letterbox.cs
// Перевод: текст через Loc
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Sinbinder.UI
{
    /// <summary>
    /// Чёрные полосы сверху и снизу — на время, пока камера наезжает.
    ///
    /// Наезд у нас уже был, и он один на всю игру
    /// (<see cref="Dialogue.DialogueCameraController"/>): разговор,
    /// поступок по своей воле, вручение титула. Не хватало того, что
    /// делает наезд <b>кадром</b>, а не просто движением техники:
    /// рамки. Полосы обрезают экран, интерфейс за ними прячется,
    /// и игрок понимает без слов — сейчас показывают, а не играют.
    ///
    /// <b>Слово живёт на нижней полосе.</b> Реплика в разговоре, «Сбегает»
    /// в бою, «Я вошёл в легенды» на церемонии — всё это одна строка
    /// в одном месте экрана. Раньше реплика лежала на своей панели
    /// посреди экрана, слово поступка бегало над головой воина, а фраза
    /// церемонии не показывалась вовсе и уходила в <c>Debug.Log</c>.
    /// Три подачи одного и того же — это три места, куда игроку надо
    /// смотреть, и он не смотрит ни в одно.
    ///
    /// <b>Время нескалированное.</b> Разговор и церемония ставят игру
    /// на паузу (<c>Time.timeScale = 0</c>), и полосы, считающие обычное
    /// время, не выехали бы вовсе.
    ///
    /// Живёт на Canvas и умирает вместе со сценой — поэтому
    /// <see cref="Instance"/> обнуляется за собой, а вызывающие
    /// спрашивают её через <c>?.</c>: контроллер камеры переживает
    /// смену сцен, полосы — нет.
    /// </summary>
    public class Letterbox : MonoBehaviour
    {
        public static Letterbox Instance { get; private set; }

        [Tooltip("Верхняя и нижняя полосы. Растянуты по ширине; меняется "
               + "только высота, и она же — вся анимация.")]
        [SerializeField] private RectTransform _top;
        [SerializeField] private RectTransform _bottom;

        [Tooltip("Кто говорит. Пусто — строка прячется: у поступка "
               + "(«Сбегает») говорящего нет, есть только действие.")]
        [SerializeField] private Text _speaker;

        [Tooltip("Что говорят или что происходит. Одна строка на всё: "
               + "реплика, слово поступка, фраза церемонии.")]
        [SerializeField] private Text _line;

        [Tooltip("Высота полосы долей от высоты экрана. 0,12 даёт кадр "
               + "примерно 2.35:1 из 16:9 — то самое широкоэкранное "
               + "соотношение, по которому кино узнаётся мгновенно.")]
        [Range(0.04f, 0.25f)]
        [SerializeField] private float _height = 0.12f;

        [Tooltip("За сколько выезжают. Быстрее — читается как рывок, "
               + "медленнее — игрок успевает заскучать до первой реплики.")]
        [SerializeField] private float _slide = 0.35f;

        private Coroutine _running;
        private bool _shown;

        /// <summary>Подняты ли полосы. Спрашивает <see cref="MomentCaption"/>:
        /// пока кадр в рамке, слово живёт на нижней полосе, и второй
        /// раз то же слово над головой читалось бы как поломка.</summary>
        public bool Shown => _shown;

        /// <summary>
        /// Стоят ли полосы на экране — подняты или ещё уезжают. Спрашивают
        /// панели, когда им вернуться: до 7 ноября журнал возвращался в миг
        /// <see cref="Hide"/>, и три десятых секунды висел поверх уходящего
        /// кадра (КТ1, кадр «у огня: Хорь назвал отряд Крысами»).
        /// </summary>
        public bool Covering => _shown || Current() > 0.001f;

        /// <summary>Напечатано ли что-нибудь на нижней полосе. Спрашивает прогон: снимать кадр.</summary>
        public bool Typed => _line != null && !string.IsNullOrEmpty(_line.text) && _typing == null;

        /// <summary>
        /// Щелчков «дальше»: строка дочитана, игрок просит следующую
        /// (автор, 05.11: «переходить на диалоге к следующей фразе при клике
        /// мыши»). Кто держит реплику, ждёт через <see cref="Hold"/>: срок
        /// вышел или счёт сменился — пора.
        /// </summary>
        public static int Nexts { get; private set; }

        /// <summary>
        /// Щелчков вообще — и «дальше», и «допечатать». Голос по нему
        /// замолкает (<see cref="Audio.VoiceGenerator"/>): строка уже
        /// на полосе целиком, бормотать её дальше незачем.
        /// </summary>
        public static int Clicks { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() { Nexts = 0; Clicks = 0; }

        /// <summary>
        /// Держать реплику: столько-то секунд настоящего времени (под наездом
        /// игра стоит) — или пока игрок не щёлкнет «дальше». Щелчок по
        /// печатающейся строке только допечатывает её, и срок идёт дальше:
        /// прочесть её всё равно надо.
        /// </summary>
        public static IEnumerator Hold(float seconds)
        {
            int next = Nexts;
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end && Nexts == next) yield return null;
        }

        void Update()
        {
            if (!_shown || !Input.GetMouseButtonDown(0) || OverButton()) return;

            // Разговор вблизи ждёт ответа, а не срока: щелчок мимо ответов
            // только допечатывает реплику.
            if (_typing != null)
            {
                StopCoroutine(_typing);
                _typing = null;
                if (_line != null) _line.text = _full ?? "";
                Clicks++;
                return;
            }

            // Пустая полоса тоже считается: окно разговора отряда (DialogueUI)
            // печатает строку само, мимо Type, и спрашивает этот же счёт.
            // Щелчок до реплики никому не мешает: Hold считает от своего начала.
            if (TalkView.Shown) return;
            Clicks++;
            Nexts++;
        }

        private static readonly System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult> _hits = new();

        /// <summary>
        /// Под курсором кнопка: ответ в разговоре, вопрос под полосами
        /// (совет, плата). Её щелчок — её, а не «дальше». Погашенные панели
        /// кнопок не держат (<see cref="Others"/>), так что ищем именно кнопку,
        /// а не «что-нибудь интерфейсное».
        /// </summary>
        private static bool OverButton()
        {
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (events == null) return false;
            _hits.Clear();
            events.RaycastAll(new UnityEngine.EventSystems.PointerEventData(events) { position = Input.mousePosition }, _hits);
            foreach (var hit in _hits)
                if (hit.gameObject != null && hit.gameObject.GetComponentInParent<Selectable>() != null) return true;
            return false;
        }

        void Awake()
        {
            Instance = this;

            // Полосы не ловят мышь: под ними край экрана, и в тактическом
            // режиме игрок водит там рамкой выделения.
            Block(_top);
            Block(_bottom);

            Set(0f);
            Clear();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Держать погашенное погашенным, пока полосы подняты. У панелей бывает
        /// своя прозрачность, и они ставят её каждый кадр: панель установок
        /// отряда ставила себе единицу поверх наезда — на паузе её срок
        /// не истекает (автор, 26 сентября: «при наезде камеры видны слева
        /// сверху панели приказов»). После всех Update — последнее слово наше.
        /// </summary>
        void LateUpdate()
        {
            if (!_shown) return;
            foreach (var d in _dimmed)
                if (d.Group != null) d.Group.alpha = 0f;
        }

        private static void Block(RectTransform bar)
        {
            if (bar == null) return;

            var image = bar.GetComponent<Graphic>();
            if (image != null) image.raycastTarget = false;
        }

        /// <summary>
        /// Поднять полосы. Повторный вызов ничего не перезапускает:
        /// в разговоре наезд случается на каждую реплику, и полосы
        /// дёргались бы туда-сюда между фразами.
        /// </summary>
        public void Show(string caption = null)
        {
            if (!string.IsNullOrEmpty(caption)) Say(null, caption);

            if (_shown) return;
            _shown = true;

            // Подняли снова, пока полосы уезжали, — интерфейс ещё погашен.
            if (_dimmed.Count == 0) Others(false);
            Run(_height);
        }

        /// <summary>Опустить полосы и стереть слово.</summary>
        public void Hide()
        {
            if (!_shown) return;
            _shown = false;

            // Интерфейс возвращается, когда полосы ушли (Slide), а не в первый
            // же кадр: иначе он встаёт поверх кино, которое ещё на экране.
            Clear();
            Run(0f);
        }

        private struct Dimmed
        {
            public CanvasGroup Group;
            public float Alpha;
        }

        private readonly System.Collections.Generic.List<Dimmed> _dimmed = new();

        /// <summary>
        /// Пока полосы подняты, остального интерфейса не видно.
        ///
        /// Полосы закрывают экран сверху и снизу, и панели, стоящие там
        /// же, не исчезают, а <b>обрезаются</b>: на снимке набега журнал
        /// и подпись выделенного торчали из-под нижней полосы наполовину.
        /// Кинематографический миг тем и отличается от игры, что в нём
        /// нет интерфейса.
        ///
        /// Гасим прозрачностью, а не выключением: панели ищут по типу,
        /// а выключенный объект не находится.
        /// </summary>
        private void Others(bool show)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            if (show)
            {
                // Возвращаем каждому его прежнюю прозрачность, а не
                // единицу всем подряд: пустой журнал прятался сам
                // и так же вернулся бы на экран пустой рамкой.
                foreach (var was in _dimmed)
                    if (was.Group != null) was.Group.alpha = was.Alpha;

                _dimmed.Clear();
                return;
            }

            foreach (Transform child in canvas.transform)
            {
                var go = child.gameObject;
                if (go == gameObject || !go.activeInHierarchy) continue;

                // Гасим только то, что нельзя нажать. Панель с кнопками —
                // это вопрос игроку (совет, плата, конец демо), и спрятать
                // её значило бы спрятать вопрос, на который он обязан
                // ответить. А спрашивают как раз под полосами.
                if (go.GetComponentInChildren<Button>(true) != null) continue;

                var group = go.GetComponent<CanvasGroup>();
                if (group == null) group = go.AddComponent<CanvasGroup>();

                _dimmed.Add(new Dimmed { Group = group, Alpha = group.alpha });
                group.alpha = 0f;
            }
        }


        /// <summary>
        /// Каждая строка полосы, как её сказали: прогон демо пишет их в отчёт —
        /// сценарий целиком, по порядку и по шагам, для вычитки (58-ROADMAP, КТ1 п. 12).
        /// </summary>
        public static event System.Action<string, string, bool> Said;

        private bool _thought;

        /// <summary>
        /// Что написано на нижней полосе. Говорящий может быть пуст —
        /// тогда строка имени прячется совсем, а не висит пустой:
        /// пустая строка сдвигает реплику вниз и кадр перекашивает.
        /// </summary>
        public void Say(string speaker, string line)
        {
            Plain();
            Said?.Invoke(speaker, line, _thought);

            if (_speaker != null)
            {
                _speaker.text = speaker ?? "";
                _speaker.gameObject.SetActive(!string.IsNullOrEmpty(speaker));
            }

            if (_line == null) return;

            if (_typing != null) { StopCoroutine(_typing); _typing = null; }
            _full = line;

            // По букве, как в Undertale (автор, 21 октября: «сейчас это
            // выглядит очень тупо» — строка падала на полосу целиком).
            // Пустое, разметка или выключенный объект — сразу целиком.
            if (string.IsNullOrEmpty(line) || line.IndexOf('<') >= 0 || !isActiveAndEnabled)
            {
                _line.text = line ?? "";
                return;
            }

            _typing = StartCoroutine(Type(line));
        }

        /// <summary>
        /// Мысль Греховода: без подписи, курсивом и тусклее, чем реплика.
        /// Автор, 21.10: «как будто чего-то не хватает… голос Греховоду, чтобы
        /// он „читал“ или „думал“» — проба без голоса (37-DEMO §8.7). Говорят
        /// все, кроме героя; мысль — его отношение, но не его лицо. Следующая
        /// реплика (<see cref="Say"/>) возвращает строке обычный вид.
        /// </summary>
        public void Think(string line)
        {
            _thought = true;
            try { Say(null, line); } finally { _thought = false; }
            if (_line == null) return;

            _plainFont ??= _line.font;
            _plainColor ??= _line.color;
            _line.font = UiStyle.Thought;
            _line.color = Thinking(_plainColor.Value);
        }

        /// <summary>Цвет мысли: та же кость, на треть тусклее — тише реплики.</summary>
        public static Color Thinking(Color plain)
            => new(plain.r * 0.78f, plain.g * 0.76f, plain.b * 0.72f, plain.a);

        private Font _plainFont;
        private Color? _plainColor;

        /// <summary>Вернуть строке вид реплики после мысли.</summary>
        private void Plain()
        {
            if (_line == null) return;
            if (_plainFont != null) _line.font = _plainFont;
            if (_plainColor.HasValue) _line.color = _plainColor.Value;
        }

        [Tooltip("Букв в секунду. Читают ~16 в секунду (Herald.Hold) — печать "
               + "обгоняет чтение, но видна.")]
        [SerializeField] private float _lettersPerSecond = 40f;

        private Coroutine _typing;
        private string _full;

        /// <summary>Скорость печати. Голос Смерти печатает медленнее (Inspectable).</summary>
        public float LettersPerSecond
        {
            get => _lettersPerSecond;
            set => _lettersPerSecond = Mathf.Max(1f, value);
        }

        /// <summary>
        /// Печатаем, а не дописываем: вся строка стоит на месте с первого
        /// кадра, ненапечатанный хвост — прозрачный. Иначе строка по центру
        /// переносилась бы заново на каждой букве и прыгала. Время —
        /// настоящее: под наездом игра стоит.
        /// </summary>
        private IEnumerator Type(string full)
        {
            _line.supportRichText = true;

            int shown = 0;
            float wait = 0f;
            while (shown < full.Length)
            {
                wait -= Time.unscaledDeltaTime;
                while (wait <= 0f && shown < full.Length)
                    wait += 1f / Mathf.Max(1f, _lettersPerSecond) + Breath(full[shown++]);

                _line.text = shown >= full.Length
                    ? full
                    : full.Substring(0, shown) + "<color=#00000000>" + full.Substring(shown) + "</color>";
                yield return null;
            }

            _line.text = full;
            _typing = null;
        }

        /// <summary>
        /// Сколько строка будет печататься — тем же счётом, что <see cref="Type"/>.
        /// Миг, который сам решает, когда кончиться (RearguardMoment), держится по нему.
        /// </summary>
        public float TypeSeconds(string line)
        {
            if (string.IsNullOrEmpty(line) || line.IndexOf('<') >= 0) return 0f;
            float seconds = 0f;
            foreach (char c in line) seconds += 1f / Mathf.Max(1f, _lettersPerSecond) + Breath(c);
            return seconds;
        }

        /// <summary>Запинка после знака: точка — вдох, запятая — полвдоха.</summary>
        private static float Breath(char c)
        {
            switch (c)
            {
                case '.': case '!': case '?': case '…': return 0.22f;
                case ',': case ';': case ':': case '—': return 0.09f;
                default: return 0f;
            }
        }

        private void Clear() => Say(null, null);

        private void Run(float target)
        {
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(Slide(target));
        }

        private IEnumerator Slide(float target)
        {
            float from = Current();
            float elapsed = 0f;

            while (elapsed < _slide)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = _slide <= 0f ? 1f : Mathf.Clamp01(elapsed / _slide);

                // Плавно на входе и на выходе, как у наезда камеры:
                // равномерное движение читается как работа техники.
                t = t * t * (3f - 2f * t);

                Set(Mathf.Lerp(from, target, t));
                yield return null;
            }

            Set(target);
            _running = null;
            if (!_shown) Others(true);
        }

        void OnDisable()
        {
            // Полосы выключили на полпути вниз — интерфейс не должен остаться погашенным.
            if (!_shown && _running != null) { _running = null; Set(0f); Others(true); }
        }

        private float Current()
            => _top != null && _top.parent is RectTransform canvas && canvas.rect.height > 0f
                ? _top.sizeDelta.y / canvas.rect.height
                : 0f;

        /// <summary>
        /// Высота полос долей экрана. Считается от высоты холста,
        /// а не в пикселях: холст масштабируется под разрешение,
        /// и полоса в пикселях на большом экране стала бы ниточкой.
        /// </summary>
        private void Set(float fraction)
        {
            float height = fraction * Height();

            if (_top != null) _top.sizeDelta = new Vector2(_top.sizeDelta.x, height);
            if (_bottom != null) _bottom.sizeDelta = new Vector2(_bottom.sizeDelta.x, height);
        }

        private float Height()
        {
            if (_top != null && _top.parent is RectTransform canvas && canvas.rect.height > 0f)
                return canvas.rect.height;

            return Screen.height;
        }
    }
}
