// Assets/_Project/Scripts/Gameplay/UnitMover.cs
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