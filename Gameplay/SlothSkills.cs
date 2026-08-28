// Assets/_Project/Scripts/Gameplay/SlothSkills.cs
using System.Collections;
using UnityEngine;
using Sinbinder.AOS;

namespace Sinbinder.Gameplay
{
    public class SlothSkills : MonoBehaviour, AOS.ISkillSet
    {
        public float YawnStunDuration = 4f;
        public float YawnCooldown = 18f;
        public float LazyHealPercent = 0.15f;
        public float LazyHealCooldown = 12f;
        public float AuraOfApathyRadius = 6f;
        public float AuraOfApathyDuration = 8f;
        public float AuraOfApathyCooldown = 22f;
        public float EternalSleepRadius = 7f;
        public float EternalSleepDuration = 5f;
        public float EternalSleepCooldown = 30f;

        private float _yawnTimer, _lazyTimer, _auraTimer, _sleepTimer;
        private Warrior _warrior;

        void Awake() { _warrior = GetComponent<Warrior>(); }
        void Update()
        {
            _yawnTimer -= Time.deltaTime;
            _lazyTimer -= Time.deltaTime;
            _auraTimer -= Time.deltaTime;
            _sleepTimer -= Time.deltaTime;
        }
        private static readonly AOS.ActionType[] _actions = { AOS.ActionType.Yawn, AOS.ActionType.LazyHeal, AOS.ActionType.AuraOfApathy, AOS.ActionType.EternalSleep };
        public System.Collections.Generic.IReadOnlyList<AOS.ActionType> SkillActions => _actions;


        public bool CanUseSkill(ActionType action)
        {
            return action switch
            {
                ActionType.Yawn => _yawnTimer <= 0f,
                ActionType.LazyHeal => _lazyTimer <= 0f,
                ActionType.AuraOfApathy => _auraTimer <= 0f,
                ActionType.EternalSleep => _sleepTimer <= 0f,
                _ => false
            };
        }

        public void ExecuteSkill(ActionType action)
        {
            switch (action)
            {
                case ActionType.Yawn: StartCoroutine(YawnRoutine()); break;
                case ActionType.LazyHeal: StartCoroutine(LazyHealRoutine()); break;
                case ActionType.AuraOfApathy: StartCoroutine(AuraRoutine()); break;
                case ActionType.EternalSleep: StartCoroutine(SleepRoutine()); break;
            }
        }

        IEnumerator YawnRoutine()
        {
            _yawnTimer = YawnCooldown;
            Debug.Log($"[SLOTH] {_warrior.DisplayName} зевает, усыпляя врага.");
            yield return null;
        }

        IEnumerator LazyHealRoutine()
        {
            _lazyTimer = LazyHealCooldown;
            _warrior.Heal(_warrior.MaxHP * LazyHealPercent);
            Debug.Log($"[SLOTH] {_warrior.DisplayName} лениво восстанавливает HP.");
            yield return null;
        }

        IEnumerator AuraRoutine()
        {
            _auraTimer = AuraOfApathyCooldown;
            Debug.Log($"[SLOTH] {_warrior.DisplayName} активирует Ауру Апатии.");
            yield return null;
        }

        IEnumerator SleepRoutine()
        {
            _sleepTimer = EternalSleepCooldown;
            Debug.Log($"[SLOTH] {_warrior.DisplayName} использует Вечный Сон.");
            yield return null;
        }
    }
}