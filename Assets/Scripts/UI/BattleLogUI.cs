// Assets/Scripts/UI/BattleLogUI.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.AOS;
using Sinbinder.Gameplay;

namespace Sinbinder.UI
{
    /// <summary>
    /// Третья ступень прозрачности: решение словами, в момент решения.
    ///
    /// Диздок относил журнал на «после боя». Для рассказа о бое это верно,
    /// для объяснения отказа — нет: объяснение, приехавшее через десять
    /// минут, объяснением уже не является. Поэтому здесь две вещи.
    /// Строка отказа выезжает сейчас и живёт пять секунд. Связный рассказ
    /// собирается после боя из того же списка.
    ///
    /// Настройка в сцене: повесить на объект Canvas, задать _line — Text,
    /// в котором будет появляться строка.
    /// </summary>
    public class BattleLogUI : MonoBehaviour
    {
        [SerializeField] private Text _line;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private float _holdSeconds = 5f;
        [SerializeField] private float _fadeSeconds = 0.6f;

        [Tooltip("Сколько держать строку, когда следом ждут другие. Когда "
               + "говорят все сразу, каждый говорит короче — иначе очередь "
               + "отстанет от боя на минуту.")]
        [SerializeField] private float _rushSeconds = 1.6f;

        private readonly List<string> _history = new();
        private readonly Queue<string> _pending = new();
        private Coroutine _showing;

        /// <summary>Всё, что случилось за бой. Для рассказа после боя.</summary>
        public IReadOnlyList<string> History => _history;

        void Start()
        {
            if (_group != null) _group.alpha = 0f;
            if (AOSEventHub.Instance != null)
                AOSEventHub.Instance.OnRefusal += OnRefusal;
        }

        void OnDestroy()
        {
            if (AOSEventHub.Instance != null)
                AOSEventHub.Instance.OnRefusal -= OnRefusal;
        }

        private void OnRefusal(Warrior warrior, Decision decision, DecisionContext context)
        {
            Write(PhraseGenerator.LogLine(warrior, context, decision));
        }

        /// <summary>
        /// Строки становятся в очередь, а не затирают друг друга.
        ///
        /// Раньше вторая строка, написанная в том же кадре, заменяла первую
        /// мгновенно: игрок не видел её ни одного кадра. Для игры, вся суть
        /// которой в том, чтобы игрок понял, почему воин поступил так, —
        /// это худший из возможных багов: объяснение было, и его съели.
        /// В истории оно при этом оставалось, поэтому и не замечалось.
        /// </summary>
        public void Write(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            _history.Add(text);

            if (_line == null) return;

            _pending.Enqueue(text);
            if (_showing == null) _showing = StartCoroutine(ShowRoutine());
        }

        /// <summary>
        /// Время нескалированное: панели пролога останавливают игру, а строка
        /// журнала обязана дочитываться и на паузе.
        /// </summary>
        private IEnumerator ShowRoutine()
        {
            while (_pending.Count > 0)
            {
                _line.text = _pending.Dequeue();

                if (_group != null) _group.alpha = 1f;

                // Ждут другие — эта уступает им место раньше.
                float hold = _pending.Count > 0 ? _rushSeconds : _holdSeconds;
                yield return new WaitForSecondsRealtime(hold);

                // Пока ждали, могла прийти следующая: тогда не гаснем,
                // а сразу показываем её — мигание между репликами одного
                // разговора выглядит как сбой.
                if (_pending.Count > 0) continue;

                // Гасить нечего — но очередь всё равно надо дочерпать,
                // иначе строки застряли бы в ней до следующей записи.
                if (_group == null) continue;

                float t = 0f;
                while (t < _fadeSeconds)
                {
                    // Успела прийти строка посреди угасания — вернуть свет.
                    if (_pending.Count > 0) break;

                    t += Time.unscaledDeltaTime;
                    _group.alpha = 1f - t / _fadeSeconds;
                    yield return null;
                }

                if (_pending.Count == 0) _group.alpha = 0f;
            }

            _showing = null;
        }

        public void ClearHistory() => _history.Clear();

        /// <summary>Связный рассказ о бое. Показывается после боя.</summary>
        public string BuildNarrative() => BattleNarrator.Build(_history);
    }
}
