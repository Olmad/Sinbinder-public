// Assets/Scripts/Gameplay/AutoAttack.cs
using UnityEngine;

namespace Sinbinder.Gameplay
{
    [RequireComponent(typeof(Damageable))]
    public class AutoAttack : MonoBehaviour
    {
        [SerializeField] private float _attackDamage = 5f;
        [SerializeField] private float _attackRange = 2f;
        [SerializeField] private float _attackCooldown = 1f;

        private Damageable _self;
        private Damageable _currentTarget;
        private float _cooldownTimer;
        private bool _isPlayerControlled;

        public float AttackDamage => _attackDamage;
        public float AttackRange => _attackRange;
        public Damageable CurrentTarget => _currentTarget;

        void Awake() { _self = GetComponent<Damageable>(); }

        void Update()
        {
            if (_self.IsDead) return;
            _cooldownTimer -= Time.deltaTime;

            if (_currentTarget != null)
            {
                if (_currentTarget.IsDead) { _currentTarget = null; return; }
                float dist = Vector3.Distance(transform.position, _currentTarget.transform.position);
                if (dist <= _attackRange && _cooldownTimer <= 0f)
                    Attack(_currentTarget);
            }
            else if (!_isPlayerControlled)
            {
                FindClosestEnemy();
            }
        }

        public void SetTarget(Damageable target) { _currentTarget = target; _isPlayerControlled = true; }
        public void ClearTarget() { _currentTarget = null; _isPlayerControlled = false; }

        private void Attack(Damageable target)
        {
            _cooldownTimer = _attackCooldown;

            // Удар стоит сил, а выдохшийся бьёт вполсилы.
            float damage = _attackDamage;
            var fatigue = GetComponent<Fatigue>();
            if (fatigue != null)
            {
                damage *= fatigue.Effectiveness;
                fatigue.SpendForAttack();
            }

            target.TakeDamage(damage, gameObject);
        }

        private void FindClosestEnemy()
        {
            if (CombatManager.Instance == null) return;
            var enemy = CombatManager.Instance.GetClosestEnemy(transform.position, _attackRange * 2f, gameObject);
            if (enemy != null && !enemy.IsDead)
                _currentTarget = enemy;
        }

        public void Initialize(float damage, float range, float cooldown)
        {
            _attackDamage = damage;
            _attackRange = range;
            _attackCooldown = cooldown;
        }

        public void ForceAttack(Damageable target)
        {
            if (target != null && !target.IsDead)
            {
                _currentTarget = target;
                _isPlayerControlled = true;
                _cooldownTimer = 0f;
                Attack(target);
            }
        }
    }
}