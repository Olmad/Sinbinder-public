// Assets/Scripts/UI/Letterbox.cs
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

            Others(false);
            Run(_height);
        }

        /// <summary>Опустить полосы и стереть слово.</summary>
        public void Hide()
        {
            if (!_shown) return;
            _shown = false;

            Others(true);
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
        /// Что написано на нижней полосе. Говорящий может быть пуст —
        /// тогда строка имени прячется совсем, а не висит пустой:
        /// пустая строка сдвигает реплику вниз и кадр перекашивает.
        /// </summary>
        public void Say(string speaker, string line)
        {
            if (_speaker != null)
            {
                _speaker.text = speaker ?? "";
                _speaker.gameObject.SetActive(!string.IsNullOrEmpty(speaker));
            }

            if (_line != null) _line.text = line ?? "";
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
