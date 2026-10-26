// Assets/Scripts/UI/HintArrival.cs
using System.Collections;
using UnityEngine;

namespace Sinbinder.UI
{
    /// <summary>
    /// Подсказка прилетает из центра экрана. Автор, 26 октября: «нужно, чтобы
    /// плашка появлялась в центре экрана и улетала в бок. Чтобы игрок не мог
    /// НЕ заметить». Плашка подсказки стоит внизу, в стопке над панелью
    /// выделенного, — туда игрок в бою не смотрит, и строка о силах или жатве
    /// успевала погаснуть непрочитанной.
    ///
    /// Как: при включении плашки — её копия поверх всего, в центре, крупнее;
    /// держится, пока глаз её поймает, и летит на место настоящей. Настоящая
    /// всё это время прозрачна и проявляется, когда копия долетела. Сама плашка
    /// не двигается: её место считает стопка, и тянуть её по экрану значило бы
    /// спорить с раскладкой.
    ///
    /// Время — реальное: подсказки читаются и на паузе. Повесить на плашку
    /// (сборщик сцены, <c>DemoSceneBuilder</c>).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class HintArrival : MonoBehaviour
    {
        [Tooltip("Сколько держаться в центре, секунд.")]
        [SerializeField] private float _hold = 1.1f;
        [Tooltip("Сколько лететь на место, секунд.")]
        [SerializeField] private float _flight = 0.55f;
        [Tooltip("Во сколько раз крупнее в центре.")]
        [SerializeField] private float _scale = 1.35f;

        /// <summary>Копия создаётся из этой же плашки — у копии свой OnEnable не должен лететь.</summary>
        private static bool _copying;

        private CanvasGroup _group;
        private GameObject _copy;

        void OnEnable()
        {
            if (_copying) return;
            // Не «??»: у Unity уничтоженный компонент не null для C#.
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            StartCoroutine(Arrive());
        }

        void OnDisable()
        {
            if (_copy != null) Destroy(_copy);
            _copy = null;
            if (_group != null) _group.alpha = 1f;
        }

        private IEnumerator Arrive()
        {
            // Кадр — чтобы стопка разложила плашку и текст уже стоял.
            yield return null;

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) { _group.alpha = 1f; yield break; }
            var root = canvas.rootCanvas.transform as RectTransform;
            var self = (RectTransform)transform;

            _copying = true;
            _copy = Instantiate(gameObject, root, true);
            _copying = false;
            Destroy(_copy.GetComponent<HintArrival>());
            var copyGroup = _copy.GetComponent<CanvasGroup>();
            if (copyGroup != null) copyGroup.alpha = 1f;
            _copy.name = name + " (прилёт)";
            Debug.Log($"[ПОДСКАЗКА] прилёт из центра: «{name}»");
            _copy.transform.SetAsLastSibling();

            var rt = (RectTransform)_copy.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = self.rect.size;

            // Куда лететь — середина настоящей плашки, в координатах корня.
            Vector3 target = root.InverseTransformPoint(self.TransformPoint(self.rect.center));
            Vector3 centre = Vector3.zero;

            rt.anchoredPosition = centre;
            rt.localScale = Vector3.one * _scale;

            // Проявиться и подержаться в центре.
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < _hold)
            {
                if (copyGroup != null) copyGroup.alpha = Mathf.Clamp01((Time.unscaledTime - t0) / 0.2f);
                yield return null;
            }

            // Лететь на место: быстро в начале, мягко в конце.
            t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < _flight)
            {
                float k = Mathf.Clamp01((Time.unscaledTime - t0) / _flight);
                float e = 1f - (1f - k) * (1f - k);
                rt.anchoredPosition = Vector3.Lerp(centre, target, e);
                rt.localScale = Vector3.one * Mathf.Lerp(_scale, 1f, e);
                yield return null;
            }

            Destroy(_copy);
            _copy = null;
            _group.alpha = 1f;
        }
    }
}
