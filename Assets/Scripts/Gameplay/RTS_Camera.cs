using UnityEngine;

namespace Sinbinder.Gameplay
{
    public class RTS_Camera : MonoBehaviour
    {
        /// <summary>Два взгляда на одну игру.</summary>
        public enum CameraView
        {
            /// <summary>Сверху и под углом: видно поле, видно отряд.</summary>
            Tactical = 0,

            /// <summary>За плечом: видно, куда идёшь и что написано на табличке.</summary>
            Shoulder = 1,
        }

        [Tooltip("С какого взгляда начинается сцена. Склеп — за плечом, бой — тактический.")]
        [SerializeField] private CameraView _mode = CameraView.Tactical;

        [SerializeField] private KeyCode _switchKey = KeyCode.V;

        [Header("За плечом")]
        [SerializeField] private float _shoulderDistance = 4.6f;
        [SerializeField] private float _shoulderHeight = 2.3f;
        [SerializeField] private float _shoulderLook = 1.3f;

        [Tooltip("Как быстро край экрана разворачивает взгляд, градусов в секунду.")]
        [SerializeField] private float _turnSpeed = 110f;

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

        /// <summary>
        /// Куда смотрит камера за плечом. <b>Её собственный курс,
        /// а не курс героя</b> — и это принципиально.
        ///
        /// Ходьба считается от направления камеры (<see cref="PlayerWalk"/>),
        /// и если бы камера считалась от направления героя, вышла бы петля:
        /// шаг вперёд поворачивает героя, поворот героя разворачивает
        /// камеру, разворот камеры меняет «вперёд». Герой крутился бы
        /// на месте, и виноватым выглядел бы навмеш.
        ///
        /// Разрывается она так: курс камеры меняет только игрок — краем
        /// экрана. Герой поворачивается следом за шагом и на камеру
        /// не влияет.
        /// </summary>
        private float _yaw;

        void Awake()
        {
            _cam = Camera.main;
            _targetPosition = transform.position;
            _targetZoom = _cam.fieldOfView;
            _yaw = transform.eulerAngles.y;

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

            if (Input.GetKeyDown(_switchKey)) Switch();

            if (_mode == CameraView.Shoulder && Following) Shoulder();
            else HandleMovement();

            HandleZoom();
            SmoothMove();
        }

        /// <summary>
        /// Сменить взгляд.
        ///
        /// За плечом — когда ходишь: видно, куда идёшь, и читаются
        /// таблички. Тактический — когда командуешь: видно поле и весь
        /// отряд. Ходить при тактическом всё равно можно, но это
        /// перетаскивание фишки, а не ходьба, — с этого и начался разговор.
        ///
        /// Без тела героя за плечом смотреть не на что: остаётся
        /// тактический.
        /// </summary>
        public void Switch()
        {
            if (!Following)
            {
                _mode = CameraView.Tactical;
                return;
            }

            _mode = _mode == CameraView.Tactical
                ? CameraView.Shoulder
                : CameraView.Tactical;

            // Возвращаясь в тактический, забываем старое смещение:
            // взгляд за плечом увёл камеру далеко от того места,
            // где смещение бралось, и она прыгнула бы рывком.
            if (_mode == CameraView.Tactical) _offsetTaken = false;
        }

        /// <summary>Задать взгляд из сборщика сцены.</summary>
        public void SetView(CameraView view) => _mode = view;

        /// <summary>
        /// Взгляд за плечом: камера держится позади героя на своём курсе.
        ///
        /// Курс меняет край экрана, а не герой (см. пояснение у
        /// <c>_yaw</c>). Высота и расстояние постоянные: качающаяся
        /// за спиной камера в игре, где читают таблички и значки над
        /// головами, мешает читать.
        /// </summary>
        private void Shoulder()
        {
            if (Input.mousePosition.x < _edgeScrollSize) _yaw -= _turnSpeed * Time.deltaTime;
            if (Input.mousePosition.x > Screen.width - _edgeScrollSize)
                _yaw += _turnSpeed * Time.deltaTime;

            var hero = SinbinderPlayer.Where;
            var back = Quaternion.Euler(0f, _yaw, 0f) * Vector3.back;

            _targetPosition = hero + back * _shoulderDistance
                            + Vector3.up * _shoulderHeight;

            // Смотрим не в ноги, а на уровень головы и чуть дальше: иначе
            // половину кадра занимает пол, а таблички уходят за верхний край.
            var look = hero + Vector3.up * _shoulderLook;
            var toLook = look - transform.position;

            if (toLook.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(toLook), 0.35f);
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
            _yaw = transform.eulerAngles.y;

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