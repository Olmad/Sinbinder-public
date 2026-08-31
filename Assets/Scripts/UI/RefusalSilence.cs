using System.Collections;
using UnityEngine;
using Sinbinder.AOS;
using Sinbinder.Gameplay;

namespace Sinbinder.UI
{
    /// <summary>
    /// Доля 6: звук выключается целиком на полторы секунды.
    ///
    /// «Тишина — самый громкий инструмент в наборе и стоит ноль»
    /// (docs/09-PROLOGUE.md §4.3). Из шести приёмов дешёвой эпики этот
    /// единственный относится прямо к моменту отказа, и до сих пор его
    /// не существовало: AudioListener.volume не трогала ни одна строка
    /// в проекте.
    ///
    /// По умолчанию срабатывает один раз за сцену. Отказ, оглушающий
    /// каждый раз, перестаёт оглушать к третьему разу, а доля 6 —
    /// это один определённый отказ, первый.
    /// </summary>
    public class RefusalSilence : MonoBehaviour
    {
        [SerializeField] private float _seconds = 1.5f;

        [Tooltip("Только первый отказ в сцене. Снимать не советую: "
               + "тишина на каждом отказе перестаёт быть событием.")]
        [SerializeField] private bool _onlyFirst = true;

        private bool _spent;
        private Coroutine _running;
        private float _restoreTo = 1f;

        void Start()
        {
            if (AOSEventHub.Instance != null)
                AOSEventHub.Instance.OnRefusal += OnRefusal;
        }

        void OnDestroy()
        {
            if (AOSEventHub.Instance != null)
                AOSEventHub.Instance.OnRefusal -= OnRefusal;

            // Сцену могли закрыть посреди тишины. Уносить её в следующую
            // сцену нельзя: там звук уже не вернёт никто.
            if (_running != null) AudioListener.volume = _restoreTo;
        }

        private void OnRefusal(Warrior warrior, Decision decision, DecisionContext context)
        {
            if (_onlyFirst && _spent) return;
            _spent = true;

            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(Silence());
        }

        private IEnumerator Silence()
        {
            _restoreTo = AudioListener.volume;
            AudioListener.volume = 0f;

            // Реальное время: пауза на отказе может остановить игровое,
            // а тишина обязана длиться ровно столько, сколько задумано.
            yield return new WaitForSecondsRealtime(_seconds);

            AudioListener.volume = _restoreTo;
            _running = null;
        }
    }
}
