// Assets/_Project/Scripts/UI/BattleLogUI.cs
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

        private readonly List<string> _history = new();
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

        public void Write(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            _history.Add(text);

            if (_line == null) return;
            _line.text = text;

            if (_showing != null) StopCoroutine(_showing);
            _showing = StartCoroutine(ShowRoutine());
        }

        private IEnumerator ShowRoutine()
        {
            if (_group == null) yield break;

            _group.alpha = 1f;
            yield return new WaitForSeconds(_holdSeconds);

            float t = 0f;
            while (t < _fadeSeconds)
            {
                t += Time.deltaTime;
                _group.alpha = 1f - t / _fadeSeconds;
                yield return null;
            }
            _group.alpha = 0f;
            _showing = null;
        }

        public void ClearHistory() => _history.Clear();

        /// <summary>Связный рассказ о бое. Показывается после боя.</summary>
        public string BuildNarrative() => BattleNarrator.Build(_history);
    }
}
