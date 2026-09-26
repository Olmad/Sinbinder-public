// Assets/Scripts/Gameplay/UnitMover.cs
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
                    _agent.SetDestination(_attackTarget.transform.position);
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

        public void CommandMove(Vector3 destination)
        {
            if (_self != null && _self.IsDead) return;
            if (!_agent.enabled) return;

            _isAttacking = false;
            _attackTarget = null;
            _agent.SetDestination(destination);
        }

        public void CommandAttack(GameObject target)
        {
            if (_self != null && _self.IsDead) return;
            if (!_agent.enabled) return;

            _isAttacking = true;
            _attackTarget = target;
            _agent.SetDestination(target.transform.position);
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