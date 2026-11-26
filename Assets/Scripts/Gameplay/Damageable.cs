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
        private bool _deathReported;

        public float HP => _hp;
        public float MaxHP => _maxHP;
        public bool IsDead => _hp <= 0f;
        internal bool HasRetreated { get; private set; }
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

        /// <summary>Ниже этой доли здоровья удар пугает (<see cref="AOS.EmotionType.Fear"/>).</summary>
        public const float FrightShare = 1f / 3f;

        public void TakeDamage(float damage, GameObject attacker)
        {
            ApplyDamage(damage, attacker, allowGuardInterception: true);
        }

        /// <summary>
        /// Стрела долетела (AutoAttack, лучник). Как удар, только без взмаха у стрелявшего:
        /// он выстрелил полсекунды назад, и второй взмах в миг попадания читался бы новым ударом.
        /// </summary>
        public void TakeArrow(float damage, GameObject archer)
        {
            ApplyDamage(damage, archer, allowGuardInterception: true, swing: false);
        }

        // A bodyguard must take the intercepted hit directly. Allowing this
        // hit to enter Bodyguard.Absorb again could bounce between two wounded
        // warriors who are each guarding the other, producing two false saves.
        // The guard's own watch stays intact for the next INDEPENDENT attack.
        internal void TakeInterceptedDamage(float damage, GameObject attacker)
        {
            ApplyDamage(damage, attacker, allowGuardInterception: false);
        }

        private void ApplyDamage(float damage, GameObject attacker, bool allowGuardInterception, bool swing = true)
        {
            // Nonpositive or nonfinite input is not a hit: never heal through damage,
            // mark hostility, consume a bodyguard watch or produce a death event.
            if (damage <= 0f || float.IsNaN(damage) || float.IsInfinity(damage)) return;
            if (IsDead || HasRetreated) return;
            if (Untouchable != null && this == Untouchable) return;

            // Телохранитель принял этот удар на себя (Bodyguard, «Вороньим следом»).
            if (allowGuardInterception && Bodyguard.Absorb(this, attacker, damage)) return;

            // The caravan is genuinely peaceful until the first hostile
            // action. A hit by the player or a player-side warrior commits
            // assault BEFORE the blow is applied, so its neutral guards
            // immediately become real enemies. The method ignores all
            // ordinary battles and personal maps other than Salt Road.
            if(attacker!=null)
            {
                if(_warrior==null) _warrior=GetComponent<Warrior>();
                PersonalExpedition.OnGuardAttacked(_warrior,attacker);
            }

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
            if (swing && attacker != null
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

            // Modifiers can invalidate an otherwise valid hit. Never poison HP or heal via damage.
            if (damage <= 0f || float.IsNaN(damage) || float.IsInfinity(damage)) return;

            _hp -= damage;

            // Испуг — чувство, а не только голос (26.11, полировка AOS). Таблица весов эмоций
            // (EmotionSystem.GetEmotionWeight) обещала: испуганный слушает Страх в полтора раза громче,
            // Гнев — вдвое тише. Но испуга не вызывал никто — радость, печаль и гнев есть, страха нет,
            // и обещание было декоративным (69-PLAN Э1.07). Теперь он приходит тем ударом, что опускает
            // воина ниже трети здоровья, — один раз на падение, гаснет, как другие чувства, за полминуты.
            if (_warrior == null) _warrior = GetComponent<Warrior>();
            float fright = _maxHP * FrightShare;
            if (_warrior != null && _hp > 0f && _hp < fright && _hp + damage >= fright)
                AOS.EmotionSystem.Instance?.TriggerEmotion(_warrior, AOS.EmotionType.Fear, 0.5f);

            // Удар слышно — по телу того, кого ударили (Audio.Sfx).
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
            // Invalid wound fractions must not poison HP or silently heal to full.
            if (float.IsNaN(remaining) || float.IsInfinity(remaining)) return;
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
            // Invalid healing must not poison HP or silently fill the health bar.
            if (float.IsNaN(amount) || float.IsInfinity(amount)) return;
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

        internal bool TryReportDeath()
        {
            if (!IsDead || _deathReported || HasRetreated) return false;
            _deathReported = true;
            return true;
        }

        // A pending hit can land before Unity destroys a hunter that already escaped.
        internal void ConfirmRetreat() => HasRetreated = true;

        /// <summary>Restore wounds without causing a new death/kill event.</summary>
        public void SetHealthFraction(float fraction)
        {
            if(IsDead || float.IsNaN(fraction) || float.IsInfinity(fraction)) return;
            float safe=Mathf.Clamp(fraction,0.01f,1f);
            _hp=Mathf.Max(0.01f,_maxHP*safe);
        }

        public void Initialize(float maxHP, float defense)
        {
            _maxHP = maxHP;
            _hp = maxHP;
            _deathReported = false;
            HasRetreated = false;
        }
    }
}
