// Assets/Scripts/Gameplay/AutoAttack.cs
// Перевод: текст через Loc
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

        /// <summary>
        /// Сила удара. С 24 сентября — удар воина (оболочка плюс вещи
        /// в руках, <see cref="Warrior.Attack"/>), пока включён
        /// <see cref="CombatMath"/>; выключен — прежнее число компонента.
        /// </summary>
        public float AttackDamage
        {
            get
            {
                if (!CombatMath.Enabled) return _attackDamage;

                // Лениво: удар могли повесить раньше, чем душу.
                if (_warrior == null) _warrior = GetComponent<Warrior>();
                return _warrior != null ? _warrior.Attack : _attackDamage;
            }
        }

        private Warrior _warrior;
        public float AttackRange => _attackRange;
        public Damageable CurrentTarget => _currentTarget;

        void Awake()
        {
            _self = GetComponent<Damageable>();
            _warrior = GetComponent<Warrior>();
        }

        void Update()
        {
            if (_self.IsDead) return;

            // Мир стоит — пауза или урок, — и руки стоят с ним. Иначе
            // готовый к удару бил бы в замершем мире один раз, а удар
            // на уроке жатвы мог бы положить ещё одного.
            if (Time.deltaTime <= 0f) return;

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


        /// <summary>
        /// Через сколько после начала взмаха клинок доходит до цели, секунд.
        /// До 22 октября урон приходил в миг решения, и взмах начинался вместе
        /// с ним: враг вздрагивал раньше, чем его задевали (отзыв автора 21.10,
        /// п. 8: «атаки выглядят странно»).
        /// </summary>
        private const float HitDelay = 0.3f;

        /// <summary>Удар по приказу — не чаще раза в столько секунд.</summary>
        private const float ForcedPace = 0.55f;

        /// <summary>
        /// Когда начат последний взмах — по часам игры. Темп удара по приказу
        /// считается по нему, а не по <c>_cooldownTimer</c>: у воинов отряда
        /// этот компонент выключен (AOSSceneSetup — бьёт AutoAttackAOS), его
        /// Update не идёт, и таймер, взведённый первым ударом, стоял вечно —
        /// каждый бил по приказу один раз за бой (23 октября: убежавших раненых
        /// охотников первой волны никто не мог добить).
        /// </summary>
        private float _lastSwing = float.NegativeInfinity;

        private void Attack(Damageable target, bool credit = false)
        {
            _cooldownTimer = _attackCooldown;
            _lastSwing = Time.time;

            // Удар стоит сил, а выдохшийся бьёт вполсилы.
            float damage = AttackDamage;
            var fatigue = GetComponent<Fatigue>();
            if (fatigue != null)
            {
                damage *= fatigue.Effectiveness;
                fatigue.SpendForAttack();
            }

            // Лицом к цели, если она сильно сбоку: бил туда, куда смотрел, —
            // бывало, и боком. Чуть вбок — не трогаем: в свалке спина врага
            // открыта соседу (Facing, удар сзади ×1,5), и выравнивать всех
            // лицом к лицу значило бы срезать урон боя.
            var to = target.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 1e-4f && Vector3.Angle(transform.forward, to) > 45f)
                transform.rotation = Quaternion.LookRotation(to);

            // Взмах — сейчас, урон — когда клинок дойдёт.
            if (TryGetComponent<WarriorAnimation>(out var arm)) arm.Struck();
            StartCoroutine(Land(target, damage, credit));
        }

        private System.Collections.IEnumerator Land(Damageable target, float damage, bool credit)
        {
            yield return new WaitForSeconds(HitDelay);
            if (_self.IsDead || target == null || target.IsDead) yield break;
            // Отскочил за время взмаха — промах.
            if (Vector3.Distance(transform.position, target.transform.position) > _attackRange + 0.6f) yield break;

            target.TakeDamage(damage, gameObject);

            // Убийство по приказу — деяние (AOSWarriorWrapper, как и до задержки).
            if (credit && target.IsDead && TryGetComponent<AOS.AOSWarriorWrapper>(out var mind))
                mind.OnKilledEnemy(target.Warrior);
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
                // Решение приходит каждый такт, и до 22 октября каждый такт
                // начинал удар заново, не докончив прежний (п. 8 отзыва 21.10).
                // Темп боя по приказу при этом был около двух ударов в секунду —
                // на нём держится бой демо. Его и оставляем, но взмах доигрывается.
                if (Time.time - _lastSwing >= ForcedPace) Attack(target, credit: true);
            }
        }
    }
}