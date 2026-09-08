// Assets/Scripts/UI/CommandHintUI.cs
using UnityEngine;
using UnityEngine.UI;

namespace Sinbinder.UI
{
    /// <summary>
    /// Вторая ступень обучения: приказывать.
    ///
    /// Первая («как ходить») учила двигаться, третья — жать души.
    /// Между ними зияла дыра: <b>нигде не сказано, что воинам можно
    /// приказывать вообще.</b> А это не одна из механик, это главная:
    /// вся игра — «отдай приказ и посмотри, послушают ли». Игрок,
    /// не отдавший ни одного приказа, не увидит ни одного отказа,
    /// то есть не увидит игры.
    ///
    /// Появляется, только если приказа всё ещё не было, и гаснет
    /// на первом же — не по времени, а по делу: научившемуся подсказка
    /// не показывается вовсе.
    ///
    /// Ждёт, пока игрок научится ходить: две подсказки разом читаются
    /// как список требований, а не как урок. Порядок тот же, в каком
    /// они нужны — сначала дойти до отряда, потом им командовать.
    /// </summary>
    public class CommandHintUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Text _text;

        [Tooltip("Сколько ждать после того, как игрок пошёл, прежде чем "
               + "подсказать про приказы.")]
        [SerializeField] private float _afterSeconds = 6f;

        [TextArea(1, 3)]
        [SerializeField] private string _line =
            "Щёлкните по воину — выделить. Правой кнопкой по земле — идти туда.";

        /// <summary>
        /// Показывали ли уже. Как и у подсказки движения — одна на пролог,
        /// а не одна на сцену.
        /// </summary>
        public static bool Shown { get; private set; }

        /// <summary>Отдавал ли игрок хоть один приказ за пролог.</summary>
        public static bool Ordered { get; private set; }

        /// <summary>Забыть обучение. Начало пролога.</summary>
        public static void Forget()
        {
            Shown = false;
            Ordered = false;
        }

        private float _waited;
        private bool _showing;

        void OnEnable()
        {
            Gameplay.SelectionManager.OnPlayerOrder += Noticed;
        }

        void OnDisable()
        {
            Gameplay.SelectionManager.OnPlayerOrder -= Noticed;
        }

        void Start()
        {
            if (_panel != null) _panel.SetActive(false);
            if (_text != null) _text.text = _line;

            if (Shown || Ordered) { enabled = false; return; }
        }

        void Update()
        {
            if (Ordered) { Hide(); return; }

            // Пока не пошёл — молчим: первый урок ещё не сдан.
            if (!MovementHintUI.Shown && !Walking()) return;

            _waited += Time.deltaTime;
            if (_waited < _afterSeconds) return;

            Show();
        }

        /// <summary>
        /// Идёт ли Греховод прямо сейчас. Нужно на случай, когда игрок
        /// пошёл сам, не дождавшись первой подсказки: тогда она никогда
        /// не показывалась, и ждать её было бы вечно.
        /// </summary>
        private bool Walking()
        {
            var player = Gameplay.SinbinderPlayer.Instance;
            if (player == null) return false;

            var walk = player.GetComponent<Gameplay.PlayerWalk>();
            return walk != null && walk.Walking;
        }

        private void Show()
        {
            if (_showing || _panel == null) return;

            _panel.SetActive(true);
            _showing = true;
            Shown = true;
        }

        private void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
            _showing = false;
            enabled = false;
        }

        /// <summary>
        /// Приказ отдан — урок сдан. Подписка статическая, поэтому ловит
        /// приказ любого рода: идти, держать, обороняться.
        /// </summary>
        private void Noticed(Gameplay.CommandKind kind, int count)
        {
            Ordered = true;
            Hide();
        }
    }
}
