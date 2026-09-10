using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Два взгляда, и у каждого свои руки.
    ///
    /// Прежняя пара — «тактический» и «за плечом» — управлялась одинаково:
    /// краем экрана. Это не жанр ни одной из двух игр, которыми она
    /// притворяется. В стратегии камеру водят клавишами и смотрят почти
    /// отвесно; в игре от первого лица мышь вертит головой, а курсора
    /// на экране нет вовсе. Смешение давало худшее от обоих: в бою камера
    /// уезжала от края экрана сама, а в склепе повернуться можно было
    /// только уводом мыши в угол.
    ///
    /// Теперь режимы разведены до конца, вплоть до курсора.
    /// </summary>
    public class RTS_Camera : MonoBehaviour
    {
        public enum CameraView
        {
            /// <summary>Почти отвесно сверху. WASD водит камеру, курсор свободен.</summary>
            Tactical = 0,

            /// <summary>Глазами героя. Мышь вертит голову, курсор захвачен.</summary>
            FirstPerson = 1,
        }

        [Tooltip("С какого взгляда начинается сцена. Склеп — от первого лица, бой — тактический.")]
        [SerializeField] private CameraView _mode = CameraView.Tactical;

        [SerializeField] private KeyCode _switchKey = KeyCode.V;

        [Header("Первое лицо")]
        [Tooltip("Высота глаз над ногами.")]
        [SerializeField] private float _eyeHeight = 1.65f;

        [Tooltip("Чувствительность мыши, градусов на единицу оси.")]
        [SerializeField] private float _mouseSensitivity = 2.2f;

        [Tooltip("Насколько можно задрать и опустить взгляд, градусов.")]
        [SerializeField] private float _pitchLimit = 82f;

        [Header("Тактический")]
        [Tooltip("Наклон. Девяносто — строго вниз; чуть меньше оставляет "
               + "тени и высоту читаемыми.")]
        [SerializeField] private float _tacticalPitch = 84f;

        [Tooltip("Высота над землёй, когда возвращаемся из первого лица.")]
        [SerializeField] private float _tacticalHeight = 22f;

        [SerializeField] private float _moveSpeed = 20f;
        [SerializeField] private float _scrollSpeed = 500f;
        [SerializeField] private float _edgeScrollSize = 20f;
        [SerializeField] private float _minZoom = 5f;
        [SerializeField] private float _maxZoom = 30f;

        private Camera _cam;
        private Vector3 _targetPosition;
        private float _targetZoom;

        private float _yaw;
        private float _pitch;

        /// <summary>Виден ли сейчас взгляд от первого лица. Спрашивают другие.</summary>
        public bool FirstPersonNow => _mode == CameraView.FirstPerson && Following;

        /// <summary>Есть ли за кем следовать: без тела первое лицо бессмысленно.</summary>
        private bool Following => SinbinderPlayer.Exists;

        void Awake()
        {
            _cam = Camera.main;
            _targetPosition = transform.position;
            _targetZoom = _cam.fieldOfView;
            _yaw = transform.eulerAngles.y;
            _pitch = transform.eulerAngles.x;

            // Кадр, поставленный в сцене, — решение постановщика, и зум
            // не имеет права его подрезать на первом же кадре.
            if (_cam.fieldOfView > _maxZoom) _maxZoom = _cam.fieldOfView;
            if (_cam.fieldOfView < _minZoom) _minZoom = _cam.fieldOfView;
        }

        void Start()
        {
            // В Awake героя ещё нет — его лепят спавнеры в своих Start.
            // Решать про курсор до этого рано: без тела первого лица
            // не бывает, и мышь пришлось бы отпускать обратно.
            ApplyCursor();
            if (_mode == CameraView.Tactical) LookDown();
        }

        void OnDisable()
        {
            // Отдать мышь. Компонент выключают на время разговора и при
            // смене сцены, и захваченный курсор пережил бы и то и другое:
            // игрок остался бы без указателя в панели, которую сам открыл.
            Release();
        }

        void Update()
        {
            if (Dialogue.DialogueCameraController.Instance != null &&
                Dialogue.DialogueCameraController.Instance.InDialogue)
                return;

            // На паузе игрок разговаривает с панелью, и мышь нужна ему,
            // а не камере.
            bool paused = Core.GamePauseController.Instance != null
                       && Core.GamePauseController.Instance.IsPaused;

            if (paused) { Release(); return; }

            if (Input.GetKeyDown(_switchKey)) Switch();

            ApplyCursor();

            if (FirstPersonNow) FirstPerson();
            else Tactical();

            HandleZoom();
        }

        /// <summary>
        /// Сменить взгляд. Без тела героя первое лицо невозможно —
        /// остаёмся в тактическом и молчим: это не ошибка сцены,
        /// а сцена без Греховода.
        /// </summary>
        public void Switch()
        {
            if (!Following) { _mode = CameraView.Tactical; LookDown(); return; }

            _mode = _mode == CameraView.Tactical
                ? CameraView.FirstPerson
                : CameraView.Tactical;

            if (_mode == CameraView.Tactical) LookDown();
            else _pitch = 0f;   // из-под потолка голова не начинает смотреть в пол

            ShowHero(_mode == CameraView.Tactical);
        }

        /// <summary>Задать взгляд из сборщика сцены.</summary>
        public void SetView(CameraView view)
        {
            _mode = view;
            if (view == CameraView.Tactical) LookDown();
        }

        // ---------- первое лицо ----------

        /// <summary>
        /// Глазами героя. Мышь вертит голову, ноги слушают WASD
        /// (<see cref="PlayerWalk"/>), край экрана не делает ничего.
        ///
        /// Положение ставится прямо, без сглаживания: сглаженная голова
        /// плывёт за шагом и читается как качка, а не как ходьба.
        /// </summary>
        private void FirstPerson()
        {
            _yaw += Input.GetAxisRaw("Mouse X") * _mouseSensitivity;
            _pitch -= Input.GetAxisRaw("Mouse Y") * _mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, -_pitchLimit, _pitchLimit);

            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.position = SinbinderPlayer.Where + Vector3.up * _eyeHeight;

            // Тело поворачивается туда, куда смотрит голова. Курс задаёт
            // камера, а не шаг: иначе шаг поворачивал бы героя, поворот
            // героя — камеру, и он крутился бы на месте.
            var hero = SinbinderPlayer.Instance;
            if (hero != null)
                hero.transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        // ---------- тактический ----------

        /// <summary>
        /// Вид сверху. WASD водит камеру — здесь она и есть руки игрока,
        /// а Греховод стоит там, где стоял.
        /// </summary>
        private void Tactical()
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

            // Отвесной камере «вперёд» — это север карты, а не её взгляд:
            // взгляд смотрит в землю, и его проекция вырождается.
            var forward = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
            var right = Quaternion.Euler(0f, _yaw, 0f) * Vector3.right;

            _targetPosition += (forward * move.z + right * move.x).normalized
                             * (_moveSpeed * Time.deltaTime);

            transform.position = Vector3.Lerp(transform.position, _targetPosition, 0.35f);
            transform.rotation = Quaternion.Euler(_tacticalPitch, _yaw, 0f);
        }

        /// <summary>
        /// Поставить камеру над героем и наклонить вниз. Зовётся при входе
        /// в тактический: из первого лица камера стоит у него в голове,
        /// и без этого вид сверху начался бы изнутри черепа.
        /// </summary>
        private void LookDown()
        {
            _pitch = _tacticalPitch;

            var ground = Following ? SinbinderPlayer.Where : transform.position;
            ground.y = 0f;

            _targetPosition = ground + Vector3.up * _tacticalHeight;
            transform.position = _targetPosition;
            transform.rotation = Quaternion.Euler(_tacticalPitch, _yaw, 0f);
        }

        // ---------- курсор и тело ----------

        /// <summary>
        /// Мышь принадлежит режиму. В первом лице курсора нет и он заперт
        /// в середине экрана — иначе поворот упирался бы в край окна.
        /// В тактическом курсор нужен: им выделяют и отдают приказы.
        /// </summary>
        private void ApplyCursor()
        {
            if (FirstPersonNow)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Release();
            }
        }

        private static void Release()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>
        /// Показать или спрятать тело героя. В первом лице камера стоит
        /// внутри капсулы, и та застила бы пол-экрана изнанкой.
        /// </summary>
        private void ShowHero(bool visible)
        {
            var hero = SinbinderPlayer.Instance;
            if (hero == null) return;

            foreach (var r in hero.GetComponentsInChildren<Renderer>(true))
                r.enabled = visible;
        }

        private void HandleZoom()
        {
            // В первом лице колесо ничего не приближает: там не зум,
            // а шаг. Поле зрения трогать нельзя — оно и есть кадр.
            if (FirstPersonNow) return;

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            _targetZoom -= scroll * _scrollSpeed * Time.deltaTime;
            _targetZoom = Mathf.Clamp(_targetZoom, _minZoom, _maxZoom);

            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, _targetZoom, 0.35f);
        }

        /// <summary>
        /// Принять нынешнее положение камеры за своё. Зовёт тот, кто двигал
        /// камеру мимо этого компонента, — отъезд доли 5, например.
        /// </summary>
        public void Resync()
        {
            _targetPosition = transform.position;
            if (_cam != null) _targetZoom = _cam.fieldOfView;

            _yaw = transform.eulerAngles.y;
            _pitch = transform.eulerAngles.x;
        }
    }
}
