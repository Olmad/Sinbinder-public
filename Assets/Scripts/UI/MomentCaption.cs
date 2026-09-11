using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.AOS;
using Sinbinder.Gameplay;

namespace Sinbinder.UI
{
    /// <summary>
    /// Одно слово над воином, который решил сам.
    ///
    /// «Сбегает». «Спасает». «Грабит». Читается краем глаза за полсекунды —
    /// именно так, как автор и заметил первый побег, только теперь это
    /// не случайность.
    ///
    /// Подпись отдельно от журнала намеренно. Журнал — третья ступень
    /// прозрачности: прошедшее время и причина, «Беглец сбежал: их
    /// слишком много». Его читают, когда хотят понять. Подпись — первая
    /// ступень: её не читают, её замечают, и потому в ней одно слово,
    /// а не предложение.
    ///
    /// Слова берёт у <see cref="PhraseGenerator.Short"/>: там же, где
    /// живут два других регистра тех же действий.
    /// </summary>
    public class MomentCaption : MonoBehaviour
    {
        [Tooltip("Сколько держать слово на экране.")]
        [SerializeField] private float _hold = 1.4f;

        [Tooltip("Сколько гаснуть после этого.")]
        [SerializeField] private float _fade = 0.45f;

        [SerializeField] private Text _line;

        private Camera _cam;
        private Transform _target;
        private Coroutine _running;

        void Awake()
        {
            if (_line == null) _line = GetComponentInChildren<Text>(true);
            if (_line != null) _line.canvasRenderer.SetAlpha(0f);
        }

        void Start()
        {
            if (AOSEventHub.Instance != null)
                AOSEventHub.Instance.OnSelfWill += OnSelfWill;
        }

        void OnDestroy()
        {
            if (AOSEventHub.Instance != null)
                AOSEventHub.Instance.OnSelfWill -= OnSelfWill;
        }

        private void OnSelfWill(Warrior warrior, Decision decision, DecisionContext context)
        {
            if (_line == null || warrior == null || warrior.IsDead) return;

            string word = PhraseGenerator.Short(decision.Action, context);
            if (string.IsNullOrEmpty(word)) return;

            _line.text = word;
            _target = warrior.transform;

            // Второе слово перебивает первое: держать очередь здесь
            // незачем — очередь есть у журнала, и она там уместна.
            // На экране одновременно двух надписей быть не должно.
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(Show());
        }

        private IEnumerator Show()
        {
            _line.canvasRenderer.SetAlpha(1f);

            float until = Time.time + _hold;
            while (Time.time < until)
            {
                Follow();
                yield return null;
            }

            _line.CrossFadeAlpha(0f, _fade, true);
            _running = null;
        }

        /// <summary>
        /// Слово идёт за воином, пока висит.
        ///
        /// Камера в этот момент как раз наезжает, и надпись, застывшая
        /// там, где воин был секунду назад, указывала бы в пустоту.
        /// Цель держим как Transform: воин может лечь под собственной
        /// подписью, и обращение к уничтоженному уронило бы корутину.
        /// </summary>
        private void Follow()
        {
            if (_target == null) return;

            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            var world = _target.position + Vector3.up * 2.2f;
            var screen = _cam.WorldToScreenPoint(world);

            // За спиной камеры WorldToScreenPoint отражает точку вперёд:
            // надпись прыгнула бы на противоположный край экрана.
            if (screen.z < 0f) { _line.canvasRenderer.SetAlpha(0f); return; }

            _line.rectTransform.position = screen;
        }
    }
}
