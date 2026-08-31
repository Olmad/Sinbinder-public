// Assets/Gameplay/PatienceSkills.cs
using System.Collections;
using UnityEngine;

namespace Sinbinder.Gameplay
{
    [RequireComponent(typeof(AOS.AOSWarriorWrapper))]
    public class PatienceSkills : MonoBehaviour, AOS.ISkillSet
    {
        public float IronStanceDefBonus = 40f;
        public float IronStanceDuration = 8f;
        public float IronStanceCooldown = 15f;

        public float CounterAttackBonus = 1.2f;
        public float CounterAttackCooldown = 10f;

        public float SecondWindHealPercent = 0.3f;
        public float SecondWindCooldown = 25f;

        public float UnshakableDuration = 12f;
        public float UnshakableCooldown = 40f;

        private float _ironTimer, _counterTimer, _secondWindTimer, _unshakableTimer;
        private Warrior _warrior;
        private AOS.AOSWarriorWrapper _wrapper;

        void Awake()
        {
            _warrior = GetComponent<Warrior>();
            _wrapper = GetComponent<AOS.AOSWarriorWrapper>();
        }

        void Update()
        {
            _ironTimer -= Time.deltaTime;
            _counterTimer -= Time.deltaTime;
            _secondWindTimer -= Time.deltaTime;
            _unshakableTimer -= Time.deltaTime;
        }
        private static readonly AOS.ActionType[] _actions = { AOS.ActionType.IronStance, AOS.ActionType.CounterAttack, AOS.ActionType.SecondWind, AOS.ActionType.Unshakable };
        public System.Collections.Generic.IReadOnlyList<AOS.ActionType> SkillActions => _actions;


        public bool CanUseSkill(AOS.ActionType action)
        {
            return action switch
            {
                AOS.ActionType.IronStance => _ironTimer <= 0f,
                AOS.ActionType.CounterAttack => _counterTimer <= 0f,
                AOS.ActionType.SecondWind => _secondWindTimer <= 0f,
                AOS.ActionType.Unshakable => _unshakableTimer <= 0f,
                _ => false
            };
        }

        public void ExecuteSkill(AOS.ActionType action)
        {
            switch (action)
            {
                case AOS.ActionType.IronStance: StartCoroutine(IronStanceRoutine()); break;
                case AOS.ActionType.CounterAttack: StartCoroutine(CounterAttackRoutine()); break;
                case AOS.ActionType.SecondWind: StartCoroutine(SecondWindRoutine()); break;
                case AOS.ActionType.Unshakable: StartCoroutine(UnshakableRoutine()); break;
            }
        }

        IEnumerator IronStanceRoutine()
        {
            _ironTimer = IronStanceCooldown;
            var originalDef = _warrior.Defense;
            _warrior.Defense += IronStanceDefBonus;
            Debug.Log($"[PATIENCE] {_warrior.DisplayName} входит в Железную Стойку. Защита: {_warrior.Defense}");
            yield return new WaitForSeconds(IronStanceDuration);
            _warrior.Defense = originalDef;
            Debug.Log($"[PATIENCE] {_warrior.DisplayName} выходит из Железной Стойки.");
        }

        IEnumerator CounterAttackRoutine()
        {
            _counterTimer = CounterAttackCooldown;
            Debug.Log($"[PATIENCE] {_warrior.DisplayName} готов к Контратаке.");
            // Логика контратаки реализуется в Damageable.OnTakeDamage
            yield return null;
        }

        IEnumerator SecondWindRoutine()
        {
            _secondWindTimer = SecondWindCooldown;
            float healAmount = _warrior.MaxHP * SecondWindHealPercent;
            _warrior.Heal(healAmount);
            Debug.Log($"[PATIENCE] {_warrior.DisplayName} использует Второе Дыхание. Восстановлено HP: {healAmount}");
            yield return null;
        }

        IEnumerator UnshakableRoutine()
        {
            _unshakableTimer = UnshakableCooldown;
            Debug.Log($"[PATIENCE] {_warrior.DisplayName} становится Непоколебимым на {UnshakableDuration} сек.");
            yield return new WaitForSeconds(UnshakableDuration);
            Debug.Log($"[PATIENCE] {_warrior.DisplayName} теряет Непоколебимость.");
        }
    }
}