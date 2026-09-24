// Assets/Scripts/Gameplay/Damageable.cs
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

            // Запас тела — от оболочки, и только от неё. Душу ставят
            // раньше тела (Initialize, потом WarriorRig.Attach) везде,
            // где собирают воинов, поэтому оболочка здесь уже известна,
            // а полоска над головой, которую строят следом, увидит
            // верный максимум. До 24 сентября у всех было 30 по умолчанию,
            // и Голем держал удар ровно как Призрак.
            //
            // Initialize ниже по-прежнему может задать запас явно.
            if (_warrior != null && _warrior.ShellHP > 0f)
            {
                _maxHP = _warrior.ShellHP;
                _hp = _maxHP;
            }
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

            // Кто ударил, тот в бою участвовал. Без этой отметки «Тень»
            // не отличить от остальных (AOSWarriorWrapper.Struck).
            if (attacker != null
                && attacker.TryGetComponent<AOS.AOSWarriorWrapper>(out var striker))
                striker.MarkStruck();

            damage = ApplyPosition(damage, attacker);

            // Защита тела: от оболочки и от вещей в руках (CombatMath).
            // Воина ищем и здесь: самопроверка идёт без Awake, а в игре
            // тело бывает собрано раньше души.
            if (CombatMath.Enabled)
            {
                if (_warrior == null) _warrior = GetComponent<Warrior>();
                if (_warrior != null) damage = CombatMath.Absorb(damage, _warrior.Defense);
            }

            _hp -= damage;

            if (_hp <= 0f)
            {
                _hp = 0f;
                Die(attacker);
            }
        }

        /// <summary>
        /// Выйти в бой уже раненым: остаётся такая доля запаса. Нужно первой
        /// волне охотников — они приходят, перебив отряды в поле, и бой с ними
        /// лёгок ранами, а не слабостью людей (мысль автора, 24 сентября).
        /// Ниже единицы здоровья не опускает: ранить — не значит убить.
        /// </summary>
        public void Wound(float remaining)
        {
            if (IsDead) return;
            _hp = Mathf.Clamp(_maxHP * remaining, 1f, _maxHP);
        }

        /// <summary>
        /// Лечение. Зовёт <see cref="Warrior.Heal"/>: умения лечили полосу
        /// души, которую бой не видел, и исцеление пропадало даром.
        /// Мёртвого не лечит — смерть терминальна.
        /// </summary>
        public void Heal(float amount)
        {
            if (IsDead) return;
            _hp = Mathf.Min(_maxHP, _hp + Mathf.Abs(amount));
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