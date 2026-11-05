// Assets/Scripts/Gameplay/Damageable.cs
// Перевод: текст через Loc
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
                else if (_warrior != null && _warrior.Team == Team.Neutral)
                    CombatManager.Instance.RegisterNeutralUnit(this);
                else
                    CombatManager.Instance.RegisterPlayerUnit(this);
            }
        }

        void OnDestroy()
        {
            if (CombatManager.Instance != null)
                CombatManager.Instance.UnregisterUnit(this);
        }

        /// <summary>
        /// Не берёт урона — только съёмка трейлера (Editor/TrailerShoot): посреди набега Греховод нужен живым
        /// до последнего кадра. В игре не ставится никем.
        /// </summary>
        public static Damageable Untouchable;

        public void TakeDamage(float damage, GameObject attacker)
        {
            if (IsDead) return;
            if (Untouchable != null && this == Untouchable) return;

            // Телохранитель принял этот удар на себя (Bodyguard, «Вороньим следом»).
            if (Bodyguard.Absorb(this, attacker, damage)) return;

            // Кто ударил, тот в бою участвовал. Без этой отметки «Тень»
            // не отличить от остальных (AOSWarriorWrapper.Struck).
            if (attacker != null
                && attacker.TryGetComponent<AOS.AOSWarriorWrapper>(out var striker))
                striker.MarkStruck();

            // Кто ударил — тот враг ударенному, а Греховод, который его послал, теряет его верность
            // («Перетягивание», Hostility; в демо ничьих нет — ничего не меняется).
            if (_warrior == null) _warrior = GetComponent<Warrior>();
            if (attacker != null) Hostility.Struck(_warrior, attacker.GetComponent<Warrior>());

            // И взмах у того, кто ударил: удар идёт тремя путями (AutoAttack,
            // Engagement, умения), а ловить его надёжно — здесь, где он
            // доходит. Автор, 21 октября: «оружие достают, а махать им так
            // и не научились».
            if (attacker != null
                && attacker.TryGetComponent<WarriorAnimation>(out var arm))
                arm.Struck();

            damage = ApplyPosition(damage, attacker);

            // Дары титулованных и легенды («Перетягивание», Gift): кто бьёт и кто рядом с ударенным.
            if (_warrior == null) _warrior = GetComponent<Warrior>();
            damage = Gift.Modify(damage, attacker, _warrior);

            // Защита тела: от оболочки и от вещей в руках (CombatMath).
            // Воина ищем и здесь: самопроверка идёт без Awake, а в игре
            // тело бывает собрано раньше души.
            if (CombatMath.Enabled)
            {
                if (_warrior == null) _warrior = GetComponent<Warrior>();
                if (_warrior != null) damage = CombatMath.Absorb(damage, _warrior.Defense);
            }

            // Блок — только у Греховода, только в ручном режиме первого лица.
            if (_warrior is SinbinderPlayer &&
                TryGetComponent<FirstPersonCombat>(out var manualCombat))
                damage = manualCombat.ReduceIncoming(damage, attacker);

            // In the optional dream only: Death preserves his own pawn.
            // Never protects the newly risen zombie or other characters.
            if (_warrior is SinbinderPlayer && DreamTutorialDirector.ProtectingHero)
                damage = Mathf.Min(damage, Mathf.Max(0f, _hp - 1f));

            _hp -= damage;

            // Удар слышно — по телу того, кого ударили (Audio.Sfx).
            if (_warrior == null) _warrior = GetComponent<Warrior>();
            Audio.Sfx.Hit(_warrior, transform.position);
            // И видно — вспышка и пыль по телу ударенного (41-SHOWCASE п. 7).
            HitBurst.At(_warrior, transform.position);

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
            {
                damage *= Facing.DamageMultiplier(transform, attacker.transform.position);

                // Первый удар сзади своим — назвать: глагол сработал (Lesson, урок спины).
                if (Facing.IsFromBehind(transform, attacker.transform.position))
                    Lesson.BackStruck(attacker);
            }

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