// Assets/Scripts/Gameplay/LustSkills.cs
using System.Collections;
using UnityEngine;

namespace Sinbinder.Gameplay
{
    [RequireComponent(typeof(AOS.AOSWarriorWrapper))]
    public class LustSkills : MonoBehaviour, AOS.ISkillSet
    {
        public float CharmDuration = 6f;
        public float CharmCooldown = 18f;
        public float KissOfDeathDamage = 25f;
        public float KissOfDeathHeal = 15f;
        public float KissOfDeathCooldown = 12f;
        public float SeduceDuration = 8f;
        public float SeduceCooldown = 22f;
        public float FatalPassionDamage = 50f;
        public float FatalPassionSelfDamage = 15f;
        public float FatalPassionCooldown = 30f;

        private float _charmTimer, _kissTimer, _seduceTimer, _fatalTimer;
        private Warrior _warrior;

        void Awake() { _warrior = GetComponent<Warrior>(); }

        void Update()
        {
            _charmTimer -= Time.deltaTime;
            _kissTimer -= Time.deltaTime;
            _seduceTimer -= Time.deltaTime;
            _fatalTimer -= Time.deltaTime;
        }
        private static readonly AOS.ActionType[] _actions = { AOS.ActionType.Charm, AOS.ActionType.KissOfDeath, AOS.ActionType.Seduce, AOS.ActionType.FatalPassion };
        public System.Collections.Generic.IReadOnlyList<AOS.ActionType> SkillActions => _actions;


        public bool CanUseSkill(AOS.ActionType action)
        {
            return action switch
            {
                AOS.ActionType.Charm => _charmTimer <= 0f,
                AOS.ActionType.KissOfDeath => _kissTimer <= 0f,
                AOS.ActionType.Seduce => _seduceTimer <= 0f,
                AOS.ActionType.FatalPassion => _fatalTimer <= 0f,
                _ => false
            };
        }

        public void ExecuteSkill(AOS.ActionType action)
        {
            switch (action)
            {
                case AOS.ActionType.Charm: StartCoroutine(CharmRoutine()); break;
                case AOS.ActionType.KissOfDeath: StartCoroutine(KissRoutine()); break;
                case AOS.ActionType.Seduce: StartCoroutine(SeduceRoutine()); break;
                case AOS.ActionType.FatalPassion: StartCoroutine(FatalRoutine()); break;
            }
        }

        IEnumerator CharmRoutine()
        {
            _charmTimer = CharmCooldown;
            var enemy = FindClosestEnemy();
            if (enemy != null)
            {
                enemy.Team = Team.Player;
                Debug.Log($"[LUST] {_warrior.DisplayName} очаровывает {enemy.DisplayName}!");
                yield return new WaitForSeconds(CharmDuration);
                enemy.Team = Team.Enemy;
                Debug.Log($"[LUST] Очарование спадает с {enemy.DisplayName}.");
            }
        }

        IEnumerator KissRoutine()
        {
            _kissTimer = KissOfDeathCooldown;
            var enemy = FindClosestEnemy();
            if (enemy != null)
            {
                enemy.TakeDamage(KissOfDeathDamage);
                _warrior.Heal(KissOfDeathHeal);
                Debug.Log($"[LUST] {_warrior.DisplayName} использует Поцелуй Смерти на {enemy.DisplayName}!");
            }
            yield return null;
        }

        IEnumerator SeduceRoutine()
        {
            _seduceTimer = SeduceCooldown;
            var enemy = FindClosestEnemy();
            if (enemy != null)
            {
                enemy.Team = Team.Player;
                Debug.Log($"[LUST] {_warrior.DisplayName} соблазняет {enemy.DisplayName} на {SeduceDuration} сек.");
                yield return new WaitForSeconds(SeduceDuration);
                enemy.Team = Team.Enemy;
            }
        }

        IEnumerator FatalRoutine()
        {
            _fatalTimer = FatalPassionCooldown;
            var enemy = FindClosestEnemy();
            if (enemy != null)
            {
                enemy.TakeDamage(FatalPassionDamage);
                _warrior.TakeDamage(FatalPassionSelfDamage);
                Debug.Log($"[LUST] {_warrior.DisplayName} использует Роковую Страсть!");
            }
            yield return null;
        }

        private Warrior FindClosestEnemy()
        {
            if (CombatManager.Instance == null) return null;
            var target = CombatManager.Instance.GetClosestEnemy(transform.position, 20f, gameObject);
            return target?.Warrior;
        }
    }
}