using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Sinbinder.UI
{
    /// <summary>
    /// Доля 0: чёрный экран, одна строка, три секунды.
    ///
    /// > Греху всё равно, чьё это тело.
    ///
    /// Стоимость — ноль, эффект — тон задан до первого кадра
    /// (docs/09-PROLOGUE.md §3). Приём из §4.4: текст крупно и редко,
    /// четыре полноэкранные строки на весь пролог, по три секунды.
    ///
    /// Строка гаснет вместе с чёрным полотном, а полотно после этого
    /// выключается целиком — иначе прозрачная картинка во весь экран
    /// осталась бы висеть поверх боя и глотать клики.
    /// </summary>
    public class PrologueTitleUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Text _line;

        [TextArea(1, 3)]
        [SerializeField] private string _text = "Греху всё равно, чьё это тело.";

        [SerializeField] private float _holdSeconds = 3f;
        [SerializeField] private float _fadeSeconds = 1.2f;

        [Tooltip("Держать ли игру на паузе, пока строка на экране.")]
        [SerializeField] private bool _pauseWhileShown = true;

        /// <summary>
        /// Строка на экране. Спрашивает прогон демо: пока она висит,
        /// игрок ничего сделать не может, и прогон не имеет права
        /// делать что-то за него — иначе он проверяет не ту игру.
        /// </summary>
        public static bool Showing { get; private set; }

        void Start()
        {
            // Лагерь открыт записью посреди разгрома: на полотне своя строка,
            // «Лагерь знали не только свои», и лагерной поверх неё не место.
            if (Gameplay.RaidEvent.Running) return;

            StartCoroutine(Show(_text));
        }

        /// <summary>
        /// Показать строку ещё раз — другую. Набег стал событием лагеря
        /// (<see cref="Gameplay.RaidEvent"/>), и его строка «Лагерь знали
        /// не только свои» идёт на том же полотне, что и первая.
        /// </summary>
        public void Again(string text)
        {
            if (Showing) return;
            StartCoroutine(Show(text));
        }

        private IEnumerator Show(string text)
        {
            if (_panel == null || _group == null) yield break;

            // Сперва отвечают на вопрос о сохранении, потом начинается
            // игра. Иначе две паузы накладываются: вопрос ставит свою,
            // строка снимает её за обоих — и дальше игра идёт под висящим
            // вопросом, которого никто уже не может нажать.
            while (StartPanel.Waiting) yield return null;

            Showing = true;

            if (_line != null) _line.text = text;
            _panel.SetActive(true);
            _group.alpha = 1f;

            if (_pauseWhileShown) Core.GamePauseController.Instance?.Pause();

            // Реальное время: на паузе игровое стоит, а строка обязана
            // висеть ровно три секунды и уйти сама.
            yield return new WaitForSecondsRealtime(_holdSeconds);

            float t = 0f;
            while (t < _fadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = 1f - t / _fadeSeconds;
                yield return null;
            }

            _group.alpha = 0f;
            _panel.SetActive(false);
            Showing = false;

            if (_pauseWhileShown) Core.GamePauseController.Instance?.Resume();
        }
    }
}
