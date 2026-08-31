using UnityEngine;

namespace Sinbinder.Gameplay
{
    public class RTS_Camera : MonoBehaviour
    {
        [SerializeField] private float _moveSpeed = 20f;
        [SerializeField] private float _scrollSpeed = 500f;
        [SerializeField] private float _edgeScrollSize = 20f;
        [SerializeField] private float _minZoom = 5f;
        [SerializeField] private float _maxZoom = 30f;
        [SerializeField] private float _rotateSpeed = 100f;
        [SerializeField] private float _pitchAngle = 55f;

        private Camera _cam;
        private Vector3 _targetPosition;
        private float _targetZoom;

        void Awake()
        {
            _cam = Camera.main;
            _targetPosition = transform.position;
            _targetZoom = _cam.fieldOfView;
        }

        void Update()
        {
            if (Dialogue.DialogueCameraController.Instance != null && 
                Dialogue.DialogueCameraController.Instance.InDialogue)
                return;

            HandleMovement();
            HandleZoom();
            SmoothMove();
        }

        private void HandleMovement()
        {
            Vector3 move = Vector3.zero;

            if (Input.GetKey(KeyCode.W)) move.z += 1;
            if (Input.GetKey(KeyCode.S)) move.z -= 1;
            if (Input.GetKey(KeyCode.A)) move.x -= 1;
            if (Input.GetKey(KeyCode.D)) move.x += 1;

            if (Input.mousePosition.x < _edgeScrollSize) move.x -= 1;
            if (Input.mousePosition.x > Screen.width - _edgeScrollSize) move.x += 1;
            if (Input.mousePosition.y < _edgeScrollSize) move.z -= 1;
            if (Input.mousePosition.y > Screen.height - _edgeScrollSize) move.z += 1;

            // Двигаем вперёд относительно направления взгляда (по горизонтали)
            Vector3 forward = _cam.transform.forward;
            forward.y = 0;
            forward.Normalize();
            Vector3 right = _cam.transform.right;
            right.y = 0;
            right.Normalize();

            _targetPosition += (forward * move.z + right * move.x).normalized * (_moveSpeed * Time.deltaTime);
        }

        private void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            _targetZoom -= scroll * _scrollSpeed * Time.deltaTime;
            _targetZoom = Mathf.Clamp(_targetZoom, _minZoom, _maxZoom);
        }

        private void SmoothMove()
        {
            transform.position = Vector3.Lerp(transform.position, _targetPosition, 0.9f);
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, _targetZoom, 0.9f);
        }
    }
}