// Assets/Scripts/Gameplay/UnitMover.cs
// Перевод: текст через Loc
using UnityEngine;
using UnityEngine.AI;

namespace Sinbinder.Gameplay
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class UnitMover : MonoBehaviour
    {
        private NavMeshAgent _agent;
        private Damageable _self;
        private GameObject _attackTarget;
        private bool _isAttacking;

        public bool IsMoving => _agent.velocity.magnitude > 0.1f;
        public bool IsAttacking => _isAttacking;

        /// <summary>
        /// Куда смотреть, стоя. Просьба живёт полторы секунды: её обновляет
        /// каждое решение, пока воин живёт лагерем (раз в секунду), и она
        /// гаснет сама, как только он занят другим.
        /// </summary>
        private Vector3 _face;
        private float _faceUntil = -1f;

        /// <summary>Градусов в секунду: поворот головой всего тела, неспешный.</summary>
        private const float FaceTurn = 200f;

        /// <summary>
        /// Стоя — повернуться к точке: к огню, к столу, к собеседнику
        /// (<see cref="CampLife.Facing"/>). До 26 сентября воин в лагере
        /// дошёл — и стоял лицом туда, откуда пришёл.
        /// </summary>
        public void Face(Vector3 point)
        {
            _face = point;
            _faceUntil = Time.time + 1.5f;
        }

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _self = GetComponent<Damageable>();

            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.constraints = RigidbodyConstraints.FreezeAll;
            }
        }

        void Update()
        {
            if (_self != null && _self.IsDead)
            {
                if (_agent.enabled)
                {
                    _agent.ResetPath();
                    _agent.enabled = false;
                }
                return;
            }

            if (_isAttacking && _attackTarget != null)
            {
                float dist = Vector3.Distance(transform.position, _attackTarget.transform.position);
                float attackRange = 2f;

                if (dist > attackRange)
                {
                    // Путь — заново, только если цель ушла или пора: прокладывали каждый
                    // кадр, и на поле в 400 это было 400 путей за кадр (замер 27.10).
                    var at = _attackTarget.transform.position;
                    if (Time.time >= _repathAt || (at - _pathTo).sqrMagnitude > RepathShift * RepathShift)
                        Go(at);
                }
                else
                {
                    _agent.ResetPath();
                }
            }

            // Стоит — повернуться туда, куда просили. Пока идёт, поворачивает
            // сам навмеш, по ходу; спорить с ним нельзя.
            if (Time.time < _faceUntil && !_isAttacking && _agent.enabled
                && _agent.velocity.sqrMagnitude < 0.01f && !_agent.pathPending
                && (!_agent.hasPath || _agent.remainingDistance <= _agent.stoppingDistance + 0.3f))
            {
                var to = _face - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.04f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation,
                        Quaternion.LookRotation(to), FaceTurn * Time.deltaTime);
            }
        }

        /// <summary>
        /// Дошёл, докуда смог: пути нет или он кончился, и агент стоит.
        /// Точка бывает недостижима (в предмете, за толпой) — тогда «в полутора
        /// метрах от неё» не наступает никогда, а приказ висит вечно.
        /// </summary>
        public bool Arrived => _agent.enabled && !_agent.pathPending
            && (!_agent.hasPath || _agent.remainingDistance <= _agent.stoppingDistance + 0.3f)
            && _agent.velocity.sqrMagnitude < 0.01f;

        public void CommandMove(Vector3 destination)
        {
            if (_self != null && _self.IsDead) return;
            if (!_agent.enabled) return;

            // Урок стоп-кадром: агент стоит вместе с миром, и Греховода
            // ведёт урок — по реальному времени и внутри кольца.
            if (Lesson.Holding && GetComponent<SinbinderPlayer>() != null)
            {
                Lesson.WalkTo(destination);
                return;
            }

            // Тот же приказ раз в секунду (решение повторяется) — не прокладывать путь заново.
            // Сверяем с тем, куда агент правда идёт: путь мог задать и кто-то мимо нас.
            var going = _agent.destination - destination;
            going.y = 0f;
            bool same = !_isAttacking && _agent.hasPath && going.sqrMagnitude < 0.25f;
            _isAttacking = false;
            _attackTarget = null;
            if (!same) Go(destination);
        }

        private Vector3 _pathTo = new(float.MaxValue, 0f, 0f);
        private float _repathAt;

        /// <summary>Насколько цель удара должна сдвинуться, чтобы проложить путь заново, м.</summary>
        private const float RepathShift = 0.75f;

        private void Go(Vector3 to)
        {
            _pathTo = to;
            _repathAt = Time.time + 0.25f;
            _agent.SetDestination(to);
        }

        public void CommandAttack(GameObject target)
        {
            if (_self != null && _self.IsDead) return;
            if (!_agent.enabled) return;

            // Бить замерших на уроке — не урок, а даровой удар.
            if (Lesson.Holding && GetComponent<SinbinderPlayer>() != null) return;

            // Та же цель (решение «бить» повторяется раз в секунду) — путь уже проложен.
            bool same = _isAttacking && _attackTarget == target && _agent.hasPath;
            _isAttacking = true;
            _attackTarget = target;
            if (!same) Go(target.transform.position);
        }

        public void Stop()
        {
            if (!_agent.enabled) return;

            _isAttacking = false;
            _attackTarget = null;
            _agent.ResetPath();
        }
    }
}