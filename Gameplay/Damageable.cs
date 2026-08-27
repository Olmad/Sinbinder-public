// Assets/_Project/Scripts/Gameplay/Damageable.cs
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

            _hp -= damage;

            if (_hp <= 0f)
            {
                _hp = 0f;
                Die(attacker);
            }
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