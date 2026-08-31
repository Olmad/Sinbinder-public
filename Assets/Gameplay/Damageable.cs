// Assets/Gameplay/Damageable.cs
using UnityEngine;

namespace Sinbinder.Gameplay
{
    public class Damageable : MonoBehaviour
    {
        [SerializeField] private float _maxHP = 30f;
        [SerializeField] private float _hp = 30f;

        private Warrior _warrior;

        public float HP => _hp;
        public float MaxHP => _maxHP;
        public bool IsDead => _hp <= 0f;
        public Warrior Warrior => _warrior;

        void Awake()
        {
            _warrior = GetComponent<Warrior>();
        }

        void Start()
        {
            if (CombatManager.Instance != null)
            {
                if (_warrior != null && _warrior.Team == Team.Enemy)
                    CombatManager.Instance.RegisterEnemyUnit(this);
                else
                    CombatManager.Instance.RegisterPlayerUnit(this);
            }
        }

        void OnDestroy()
        {
            if (CombatManager.Instance != null)
                CombatManager.Instance.UnregisterUnit(this);
        }

        public void TakeDamage(float damage, GameObject attacker)
        {
            if (IsDead) return;

            damage = ApplyPosition(damage, attacker);
            _hp -= damage;

            if (_hp <= 0f)
            {
                _hp = 0f;
                Die(attacker);
            }
        }

        /// <summary>
        /// Положение решает не меньше, чем оружие: удар в спину бьёт
        /// сильнее, окружённый защищается хуже. Это пол игры — механики,
        /// которые работают, даже если снять с воинов личности.
        /// </summary>
        private float ApplyPosition(float damage, GameObject attacker)
        {
            if (attacker != null)
                damage *= Facing.DamageMultiplier(transform, attacker.transform.position);

            var engagement = GetComponent<Engagement>();
            if (engagement != null)
                damage *= engagement.IncomingMultiplier;

            return damage;
        }

        private void Die(GameObject killer)
        {
            if (CombatManager.Instance != null)
                CombatManager.Instance.OnUnitKilled(this, killer);
        }

        public void Initialize(float maxHP, float defense)
        {
            _maxHP = maxHP;
            _hp = maxHP;
        }
    }
}