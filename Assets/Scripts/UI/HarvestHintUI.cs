// Assets/Scripts/UI/HarvestHintUI.cs
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.Gameplay;

namespace Sinbinder.UI
{
    /// <summary>
    /// Подсказка о жатве. Сцена 4 пролога: «здесь же обучение жатве душ
    /// и связыванию» (docs/09-PROLOGUE.md §4).
    ///
    /// Появляется в тот единственный миг, когда она осмысленна, — когда
    /// на поле есть угасающая душа. До этого «нажмите E» ничего не значит,
    /// после — уже поздно.
    ///
    /// Раньше эту работу делал OnGUI на каждом своём воине сразу, и текст
    /// был «доступно: N» — то есть игроку показывалась цифра, чего в этой
    /// игре не бывает нигде (00-GDD.md §7). Здесь цифр нет, и подсказка
    /// одна на сцену.
    ///
    /// Вторая строка — и есть урок. Она говорит не «нажмите», а «спешите»:
    /// цена промедления в <see cref="Core.SoulDecay"/> расписана подробно,
    /// и игрок должен узнать о ней до того, как заплатит.
    /// </summary>
    public class HarvestHintUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Text _text;

        /// <summary>
        /// Что говорит подсказка. Строка — в коде, а не в поле сцены: сцены
        /// строит DemoSceneBuilder, и сериализованное поле запоминало текст
        /// на миг сборки. Так подсказка обещала «потом R — чтобы вложить её
        /// в тело» и после того, как R стала перекладывать между рукой
        /// и сумой (<see cref="SatchelHands"/>), а связывание ушло
        /// в мастерскую склепа, которой в демо нет. Жатва же кладёт душу
        /// прямо в пустую банку (<see cref="SoulHarvester"/>) — это и сказано.
        /// </summary>
        public const string Line =
            "Подойдите и нажмите E, чтобы забрать душу в банку. Чем дольше "
          + "она гаснет, тем меньше от неё останется.";

        [Tooltip("Сколько держать подсказку на экране.")]
        [SerializeField] private float _holdSeconds = 8f;

        /// <summary>Показывали ли уже. Один раз за пролог, как и «как ходить».</summary>
        public static bool Shown { get; private set; }

        /// <summary>Забыть подсказку. Начало пролога.</summary>
        public static void Forget() => Shown = false;

        private float _hideAt = -1f;

        void Start()
        {
            if (_panel != null) _panel.SetActive(false);
            if (_text != null) _text.text = Line;

            if (SoulManager.Instance == null)
                Debug.LogWarning("[ПОДСКАЗКА] SoulManager в сцене нет: "
                               + "о жатве рассказать будет нечем.");
        }

        void Update()
        {
            if (_hideAt > 0f)
            {
                // Реальное время: панели пролога останавливают игру,
                // а подсказка обязана дочитываться и на паузе.
                if (Time.unscaledTime >= _hideAt) Hide();
                return;
            }

            if (Shown) { enabled = false; return; }

            var souls = SoulManager.Instance;
            if (souls == null || souls.FadingCount == 0) return;

            Show();
        }

        private void Show()
        {
            Shown = true;
            _hideAt = Time.unscaledTime + _holdSeconds;
            if (_panel != null) _panel.SetActive(true);
        }

        private void Hide()
        {
            _hideAt = -1f;
            if (_panel != null) _panel.SetActive(false);
            enabled = false;
        }
    }
}
