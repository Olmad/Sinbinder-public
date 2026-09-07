// Assets/Scripts/UI/MovementHintUI.cs
using UnityEngine;
using UnityEngine.UI;

namespace Sinbinder.UI
{
    /// <summary>
    /// Подсказка «как ходить». Сцена 1 пролога (docs/09-PROLOGUE.md §4):
    /// «Если игрок не двигается десять секунд, всплывает подсказка WASD».
    ///
    /// До сих пор это была строчка сценария и ничего больше. Пока лагерь
    /// был витриной, ею можно было пренебречь; теперь нельзя. Совет
    /// открывается подходом к столу, сундук — подходом к сундуку, и игрок,
    /// не знающий, что камера ходит, заперт в первой же сцене навсегда:
    /// вокруг него лагерь, в котором всё работает и ничего не происходит.
    ///
    /// Показывается один раз за пролог. Научившемуся ходить незачем
    /// напоминать об этом в каждой сцене — это раздражает ровно тех,
    /// кому подсказка уже не нужна.
    ///
    /// Буквы, а не цифры: правило «игрок не видит цифр» касается и
    /// подсказок, поэтому здесь «десять секунд» нигде не написано.
    /// </summary>
    public class MovementHintUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Text _text;

        [Tooltip("Сколько ждать неподвижности, прежде чем подсказать.")]
        [SerializeField] private float _afterSeconds = 10f;

        [Tooltip("Насколько сдвинуться, чтобы это считалось движением. "
               + "Камера дрожит на сглаживании, и ноль здесь не годится.")]
        [SerializeField] private float _moved = 0.25f;

        [TextArea(1, 3)]
        [SerializeField] private string _line = "W, A, S, D — осмотреться.";

        /// <summary>
        /// Показывали ли уже. Статично и переживает смену сцен: подсказка
        /// одна на пролог, а не одна на сцену.
        /// </summary>
        public static bool Shown { get; private set; }

        /// <summary>Забыть подсказку. Начало пролога.</summary>
        public static void Forget() => Shown = false;

        private Transform _eye;
        private Vector3 _wasAt;
        private float _still;
        private bool _showing;

        void Start()
        {
            if (_panel != null) _panel.SetActive(false);

            var cam = Camera.main;
            if (cam == null)
            {
                // Камеры нет — следить не за чем. Молчать об этом нельзя:
                // подсказка не появится, и никто не поймёт почему.
                Debug.LogWarning("[ПОДСКАЗКА] Камеры в сцене нет: "
                               + "неподвижность отслеживать не по чему.");
                enabled = false;
                return;
            }

            // Камера может быть неподвижной по замыслу сцены. Подсказать
            // «W, A, S, D» там значит соврать: игрок нажмёт и решит,
            // что игра сломана. Молча выключаемся — это не ошибка.
            if (cam.GetComponent<Gameplay.RTS_Camera>() == null) { enabled = false; return; }

            _eye = cam.transform;
            _wasAt = _eye.position;

            if (_text != null) _text.text = _line;
        }

        void Update()
        {
            if (Shown && !_showing) { enabled = false; return; }

            float went = Vector3.Distance(_eye.position, _wasAt);

            if (went >= _moved)
            {
                _wasAt = _eye.position;
                _still = 0f;

                // Пошёл — значит понял. Больше не напоминаем.
                if (_showing) { Hide(); Shown = true; enabled = false; }
                return;
            }

            if (_showing) return;

            // Реальное время: заставка доли 0 держит игру на паузе,
            // и отсчитывать неподвижность под ней было бы нечестно —
            // игрок в этот момент читает, а не бездействует.
            if (Core.GamePauseController.Instance != null
                && Core.GamePauseController.Instance.IsPaused) return;

            _still += Time.unscaledDeltaTime;
            if (_still < _afterSeconds) return;

            Show();
        }

        private void Show()
        {
            _showing = true;
            if (_panel != null) _panel.SetActive(true);
        }

        private void Hide()
        {
            _showing = false;
            if (_panel != null) _panel.SetActive(false);
        }
    }
}
