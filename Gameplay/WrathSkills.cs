// Assets/_Project/Scripts/Gameplay/WrathSkills.cs
using System.Collections;
using UnityEngine;

namespace Sinbinder.Gameplay
{
    [RequireComponent(typeof(AOS.AOSWarriorWrapper))]
    public class WrathSkills : MonoBehaviour, AOS.ISkillSet
    {
        public float BerserkDuration = 8f;
        public float BerserkAttackBonus = 0.5f;
        public float BerserkDefensePenalty = 0.3f;
        public float BerserkCooldown = 20f;

        public float PowerStrikeDamageMultiplier = 2f;
        public float PowerStrikeCooldown = 15f;

        private float _berserkTimer, _powerStrikeTimer;
        private Warrior _warrior;
        private AOS.AOSWarriorWrapper _wrapper;

        void Awake()
        {
            _warrior = GetComponent<Warrior>();
            _wrapper = GetComponent<AOS.AOSWarriorWrapper>();
        }

        void Update()
        {
            _berserkTimer -= Time.deltaTime;
            _powerStrikeTimer -= Time.deltaTime;
        }
        private static readonly AOS.ActionType[] _actions = { AOS.ActionType.Berserk, AOS.ActionType.PowerStrike };
        public System.Collections.Generic.IReadOnlyList<AOS.ActionType> SkillActions => _actions;


        public bool CanUseSkill(AOS.ActionType action)
        {
            return action switch
            {
                AOS.ActionType.Berserk => _berserkTimer <= 0f,
                AOS.ActionType.PowerStrike => _powerStrikeTimer <= 0f,
                _ => false
            };
        }

        public void ExecuteSkill(AOS.ActionType action)
        {
            switch (action)
            {
                case AOS.ActionType.Berserk: StartCoroutine(BerserkRoutine()); break;
                case AOS.ActionType.PowerStrike: StartCoroutine(PowerStrikeRoutine()); break;
            }
        }

        IEnumerator BerserkRoutine()
        {
            _berserkTimer = BerserkCooldown;
            var origAtk = _warrior.Attack;
            var origDef = _warrior.Defense;
            _warrior.Attack *= (1 + BerserkAttackBonus);
            _warrior.Defense *= (1 - BerserkDefensePenalty);
            Debug.Log($"[WRATH] {_warrior.DisplayName} впадает в Берсерк! Атака: {_warrior.Attack}, Защита: {_warrior.Defense}");
            yield return new WaitForSeconds(BerserkDuration);
            _warrior.Attack = origAtk;
            _warrior.Defense = origDef;
            Debug.Log($"[WRATH] {_warrior.DisplayName} выходит из Берсерка.");
        }

        IEnumerator PowerStrikeRoutine()
        {
            _powerStrikeTimer = PowerStrikeCooldown;
            var target = GetComponent<AOSWarriorWrapper>()?.FindBestTarget();
            if (target != null)
            {
                float dmg = _warrior.Attack * PowerStrikeDamageMultiplier;
                target.TakeDamage(dmg);
                Debug.Log($"[WRATH] {_warrior.DisplayName} наносит Мощный Удар на {dmg} урона!");
            }
            yield return null;
        }
    }
}