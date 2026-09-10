// Assets/Scripts/Gameplay/PlayerWalk.cs
using UnityEngine;
using UnityEngine.AI;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Ходьба Греховода с клавиш.
    ///
    /// W, A, S, D двигают <b>его</b>, а не камеру. До этого те же четыре
    /// клавиши двигали вид, и подсказка «как ходить» говорила правду
    /// только формально: что-то действительно ехало, но игрок при этом
    /// оставался ничем. Камера теперь следует за ним
    /// (<see cref="RTS_Camera"/>), а не заменяет его.
    ///
    /// Движение идёт через <see cref="NavMeshAgent.Move"/>, а не через
    /// <c>transform.position</c>: агент уже есть (его вешает
    /// <see cref="WarriorRig"/>), он держит героя на навмеше и не пускает
    /// сквозь палатки. Прямая правка позиции всё это обошла бы, и
    /// Греховод гулял бы по холму насквозь.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class PlayerWalk : MonoBehaviour
    {
        [SerializeField] private float _speed = 4.2f;

        /// <summary>
        /// Порог, ниже которого нажатие считается отпущенным. Нужен
        /// не для клавиш (они дискретны), а для того, чтобы не сбрасывать
        /// приказ идти каждый кадр, когда игрок ничего не жмёт.
        /// </summary>
        private const float Deadzone = 0.01f;

        private NavMeshAgent _agent;
        private Transform _eye;

        /// <summary>Шёл ли он с клавиш в прошлом кадре.</summary>
        public bool Walking { get; private set; }

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
        }

        void Start()
        {
            var cam = Camera.main;
            if (cam != null) _eye = cam.transform;

            if (_eye == null)
                Debug.LogWarning("[ГРЕХОВОД] Камеры в сцене нет: ходьба "
                               + "с клавиш пойдёт по осям мира, а не по взгляду.");
        }

        void Update()
        {
            // В разговоре камера отобрана у игрока, и ходить в это время
            // значит уехать из собственной сцены.
            if (Dialogue.DialogueCameraController.Instance != null &&
                Dialogue.DialogueCameraController.Instance.InDialogue)
            {
                Walking = false;
                return;
            }

            // WASD принадлежат режиму камеры. В тактическом ими водят
            // камеру — это руки игрока там, — и ноги обязаны молчать,
            // иначе одно нажатие делает два дела разом.
            if (_eye != null)
            {
                var view = _eye.GetComponent<RTS_Camera>();
                if (view != null && !view.FirstPersonNow) { Walking = false; return; }
            }

            Vector3 wish = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) wish.z += 1f;
            if (Input.GetKey(KeyCode.S)) wish.z -= 1f;
            if (Input.GetKey(KeyCode.A)) wish.x -= 1f;
            if (Input.GetKey(KeyCode.D)) wish.x += 1f;

            if (wish.sqrMagnitude < Deadzone)
            {
                Walking = false;
                return;
            }

            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;

            if (_eye != null)
            {
                forward = _eye.forward; forward.y = 0f; forward.Normalize();
                right = _eye.right; right.y = 0f; right.Normalize();
            }

            Vector3 step = (forward * wish.z + right * wish.x).normalized;

            // Приказ идти, отданный мышью, и ходьба с клавиш — два хозяина
            // одного агента. Клавиши главнее: игрок жмёт их прямо сейчас.
            if (_agent.hasPath) _agent.ResetPath();

            if (_agent.isOnNavMesh)
                _agent.Move(step * (_speed * Time.deltaTime));
            else
                transform.position += step * (_speed * Time.deltaTime);

            // Поворот тела не трогаем: в первом лице курс задаёт голова
            // (RTS_Camera), и шаг вбок не должен разворачивать героя туда,
            // куда он не смотрит. Раньше это было разумно — камера висела
            // за спиной сама по себе; теперь развернуло бы и взгляд.
            Walking = true;
        }
    }
}
