using UnityEngine;
using System.Collections;

namespace Sinbinder.Dialogue
{
    public class DialogueCameraController : MonoBehaviour
    {
        public static DialogueCameraController Instance { get; private set; }

        [SerializeField] private float _transitionSpeed = 5f;

        [Header("Кадр говорящего")]
        [Tooltip("С какого расстояния начинается план. Кадр берёт верх "
               + "туловища, поэтому счёт на метры, а не на десятки.")]
        [SerializeField] private float _cameraDistance = 1.95f;

        [Tooltip("Вбок от оси взгляда: кадр в три четверти, а не в упор. "
               + "Ноль ставит камеру ровно перед лицом, и разговор читается "
               + "как допрос.")]
        [SerializeField] private float _sideOffset = 0.62f;

        [Tooltip("Высота камеры. На уровне плеча: снизу выходит героика, "
               + "сверху — жалость, а разговор равных снимают с глаз.")]
        [SerializeField] private float _cameraHeight = 1.48f;

        [Tooltip("Куда смотрит камера — высота точки на говорящем.")]
        [SerializeField] private float _lookHeight = 1.56f;

        [Tooltip("Поле зрения. Узкое сжимает перспективу — именно оно, "
               + "а не близость, делает план кинематографичным.")]
        [SerializeField] private float _dialogueFOV = 36f;

        [Tooltip("На сколько камера подъезжает за реплику. Медленный наезд "
               + "держит внимание там, где слова.")]
        [SerializeField] private float _pushIn = 0.45f;

        [Tooltip("За сколько секунд наезд доходит до конца.")]
        [SerializeField] private float _pushSeconds = 4.5f;

        [SerializeField] private float _swaySpeed = 0.5f;

        [Tooltip("Покачивание. Мелкое: наезд теперь несёт движение сам, "
               + "и качка поверх него читается как дрожь в руках.")]
        [SerializeField] private float _swayAmount = 0.07f;

        private Camera _cam;
        private Vector3 _originalPosition;
        private Quaternion _originalRotation;
        private float _originalFOV;
        private bool _inDialogue = false;
        private Coroutine _swayCoroutine;

        public bool InDialogue => _inDialogue;

        void Awake()
        {
            Instance = this;
            _cam = Camera.main;
        }

        public void SaveCameraPosition()
        {
            _originalPosition = _cam.transform.position;
            _originalRotation = _cam.transform.rotation;
            _originalFOV = _cam.fieldOfView;
        }

        public IEnumerator FocusOn(Transform target)
        {
            if (target == null) yield break;

            _inDialogue = true;
            _cam.fieldOfView = _dialogueFOV;

            // Перед говорящим, а не за ним. Здесь стояло -target.forward,
            // то есть камера заходила со спины и наводилась на затылок:
            // весь разговор игрок смотрел людям в затылки.
            Vector3 anchor = target.position;
            Vector3 face = Flat(target.forward);
            Vector3 side = Flat(target.right);

            Vector3 lookTarget = anchor + Vector3.up * _lookHeight;
            Vector3 baseCamPos = Frame(anchor, face, side, _cameraDistance, 0f);

            yield return MoveCamera(baseCamPos,
                Quaternion.LookRotation((lookTarget - baseCamPos).normalized));

            // Дальше живём на числах, а не на ссылке: говорящий может
            // погибнуть посреди собственной реплики, и наезд, тянущийся
            // к уничтоженному transform, упал бы вместе с разговором.
            // На этом уже обожглись в DialogueUI.
            _swayCoroutine = StartCoroutine(PushIn(anchor, face, side, lookTarget));
        }

        /// <summary>Направление по земле: наклон говорящего кадру не нужен.</summary>
        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 0.0001f ? Vector3.forward : v.normalized;
        }

        /// <summary>Где стоит камера при данном отдалении и сдвиге вбок.</summary>
        private Vector3 Frame(Vector3 anchor, Vector3 face, Vector3 side,
                              float distance, float sway)
        {
            return anchor
                 + face * distance
                 + side * (_sideOffset + sway)
                 + Vector3.up * _cameraHeight;
        }

        /// <summary>
        /// Медленный наезд на верх туловища, с еле заметной качкой.
        ///
        /// Наезд, а не стояние: план, который не движется, читается как
        /// пауза в игре, а план, который подъезжает, — как то, что сейчас
        /// скажут важное. Это и есть весь приём.
        ///
        /// Сторона качки берётся из положения говорящего, а не жребием:
        /// правило проекта — одинаковый вход даёт одинаковый выход,
        /// и кадр под него подпадает так же, как решение.
        /// </summary>
        private IEnumerator PushIn(Vector3 anchor, Vector3 face, Vector3 side,
                                   Vector3 lookTarget)
        {
            float t = 0f;
            float elapsed = 0f;
            float direction = anchor.x + anchor.z >= 0f ? 1f : -1f;

            while (_inDialogue)
            {
                elapsed += Time.unscaledDeltaTime;
                t += _swaySpeed * Time.unscaledDeltaTime;

                float k = _pushSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / _pushSeconds);

                // Плавно на входе и на выходе: равномерный наезд заметен
                // как движение техники, сглаженный — как внимание.
                k = k * k * (3f - 2f * k);

                float distance = Mathf.Lerp(_cameraDistance, _cameraDistance - _pushIn, k);
                float sway = Mathf.Sin(t) * _swayAmount * direction;

                Vector3 pos = Frame(anchor, face, side, distance, sway);

                _cam.transform.position = pos;
                _cam.transform.rotation = Quaternion.LookRotation((lookTarget - pos).normalized);

                yield return null;
            }
        }

        public void StopSway()
        {
            _inDialogue = false;
            if (_swayCoroutine != null)
            {
                StopCoroutine(_swayCoroutine);
                _swayCoroutine = null;
            }
        }

        public IEnumerator RestoreCamera()
        {
            _inDialogue = false;
            if (_swayCoroutine != null)
            {
                StopCoroutine(_swayCoroutine);
                _swayCoroutine = null;
            }
            _cam.fieldOfView = _originalFOV;
            yield return MoveCamera(_originalPosition, _originalRotation);
        }

        private IEnumerator MoveCamera(Vector3 targetPos, Quaternion targetRot)
        {
            float duration = 1f / _transitionSpeed;
            float elapsed = 0f;

            Vector3 startPos = _cam.transform.position;
            Quaternion startRot = _cam.transform.rotation;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                t = t * t * (3f - 2f * t);

                _cam.transform.position = Vector3.Lerp(startPos, targetPos, t);
                _cam.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }

            _cam.transform.position = targetPos;
            _cam.transform.rotation = targetRot;
        }
    }
}