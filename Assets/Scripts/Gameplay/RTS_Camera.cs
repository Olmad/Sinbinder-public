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

        private Camera _cam;
        private Vector3 _targetPosition;
        private float _targetZoom;

        /// <summary>
        /// Смещение камеры относительно Греховода. Берётся не из настроек,
        /// а из кадра, который поставил сборщик сцены: камера уже стоит
        /// там, где надо, и отбирать у постановщика ракурс нельзя
        /// (то же правило, что и с потолком зума ниже).
        ///
        /// Считается лениво, при первой встрече с героем: спавнер лагеря
        /// создаёт его в Start, а камера просыпается в Awake — в Awake
        /// героя ещё нет.
        /// </summary>
        private Vector3 _offset;
        private bool _offsetTaken;

        void Awake()
        {
            _cam = Camera.main;
            _targetPosition = transform.position;
            _targetZoom = _cam.fieldOfView;

            // Кадр, поставленный в сцене, — это решение постановщика,
            // и отбирать его нельзя. Потолок 30 ниже авторских 55, а зум
            // подрезает цель каждый кадр: стоило повесить эту камеру
            // на сцену, и вид молча сужался на первом же кадре.
            // Ставим потолком то, с чего кадр начат.
            if (_cam.fieldOfView > _maxZoom) _maxZoom = _cam.fieldOfView;
            if (_cam.fieldOfView < _minZoom) _minZoom = _cam.fieldOfView;
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

        /// <summary>
        /// Есть ли за кем следовать. Пока Греховода в сценах не было,
        /// камера была игроком; теперь она — взгляд на игрока.
        /// </summary>
        private bool Following => SinbinderPlayer.Exists;

        private void HandleMovement()
        {
            Vector3 move = Vector3.zero;

            // W, A, S, D читает Греховод (PlayerWalk), а не камера.
            // Одни и те же клавиши на двух хозяевах — это тот же род
            // поломки, что был у D: нажатие делало бы два дела разом.
            if (!Following)
            {
                if (Input.GetKey(KeyCode.W)) move.z += 1;
                if (Input.GetKey(KeyCode.S)) move.z -= 1;
                if (Input.GetKey(KeyCode.A)) move.x -= 1;
                if (Input.GetKey(KeyCode.D)) move.x += 1;
            }

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

            Vector3 shift = (forward * move.z + right * move.x).normalized
                          * (_moveSpeed * Time.deltaTime);

            if (Following)
            {
                // Край экрана не уводит камеру от героя, а разворачивает
                // взгляд вокруг него: смещение меняется, привязка остаётся.
                // Иначе игрок случайно уехал бы от собственного тела
                // и не понял, как вернуться.
                TakeOffset();
                _offset += shift;
                _targetPosition = SinbinderPlayer.Where + _offset;
            }
            else
            {
                _targetPosition += shift;
            }
        }

        /// <summary>
        /// Запомнить, как камера стоит относительно героя, — один раз,
        /// при первой встрече. См. пояснение у поля <c>_offset</c>.
        /// </summary>
        private void TakeOffset()
        {
            if (_offsetTaken) return;

            _offset = transform.position - SinbinderPlayer.Where;
            _offsetTaken = true;
        }

        private void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            _targetZoom -= scroll * _scrollSpeed * Time.deltaTime;
            _targetZoom = Mathf.Clamp(_targetZoom, _minZoom, _maxZoom);
        }

        /// <summary>
        /// Принять нынешнее положение камеры за своё.
        ///
        /// Цель ставится один раз в Awake и больше ниоткуда не берётся.
        /// Пока камеру никто не двигал мимо этого компонента, всё сходится;
        /// стоит кому-то отвести её самому — отъезду сцены 5, например, —
        /// и включённая обратно камера прыгнула бы назад, на цель
        /// полуторной давности. Поэтому тот, кто двигал, обязан сказать.
        /// </summary>
        public void Resync()
        {
            _targetPosition = transform.position;
            if (_cam != null) _targetZoom = _cam.fieldOfView;

            // И смещение относительно героя тоже: после отъезда сцены 5
            // камера стоит уже не там, где встала при первой встрече,
            // а старое смещение вернуло бы её рывком назад.
            _offsetTaken = false;
        }

        private void SmoothMove()
        {
            transform.position = Vector3.Lerp(transform.position, _targetPosition, 0.9f);
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, _targetZoom, 0.9f);
        }
    }
}