using UnityEngine;
using System.Collections;

namespace Sinbinder.Dialogue
{
    public class DialogueCameraController : MonoBehaviour
    {
        public static DialogueCameraController Instance { get; private set; }

        [SerializeField] private float _transitionSpeed = 5f;
        [SerializeField] private float _cameraDistance = 2f;
        [SerializeField] private float _cameraHeight = 0.3f;
        [SerializeField] private float _lookHeight = 1.7f;
        [SerializeField] private float _dialogueFOV = 60f;
        [SerializeField] private float _swaySpeed = 0.5f;
        [SerializeField] private float _swayAmount = 0.2f;

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
            _inDialogue = true;
            _cam.fieldOfView = _dialogueFOV;

            Vector3 lookTarget = target.position + Vector3.up * _lookHeight;
            Vector3 baseCamPos = target.position
                               + (-target.forward * _cameraDistance)
                               + Vector3.up * _cameraHeight;

            yield return MoveCamera(baseCamPos, Quaternion.LookRotation((lookTarget - baseCamPos).normalized));

            float direction = Random.value > 0.5f ? 1f : -1f;
            _swayCoroutine = StartCoroutine(SwayLoop(lookTarget, baseCamPos, direction));
        }

        private IEnumerator SwayLoop(Vector3 lookTarget, Vector3 baseCamPos, float direction)
        {
            float t = 0f;
            while (_inDialogue)
            {
                t += _swaySpeed * Time.unscaledDeltaTime;
                float offset_x = Mathf.Sin(t) * _swayAmount;

                Vector3 swayPos = baseCamPos + _cam.transform.right * offset_x;
                Vector3 dir = (lookTarget - swayPos).normalized;

                _cam.transform.position = swayPos;
                _cam.transform.rotation = Quaternion.LookRotation(dir);

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