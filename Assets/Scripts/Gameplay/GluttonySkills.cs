// Assets/Scripts/Gameplay/GluttonySkills.cs
using System.Collections;
using UnityEngine;

namespace Sinbinder.Gameplay
{
    [RequireComponent(typeof(AOS.AOSWarriorWrapper))]
    public class GluttonySkills : MonoBehaviour, AOS.ISkillSet
    {
        public float DevourHealPercent = 0.2f;
        public float BellyArmorBonus = 15f;
        public float VomitDamage = 20f;
        public float VomitSlowDuration = 3f;
        public float VomitCooldown = 18f;
        public float InsatiableHungerDuration = 10f;
        public float InsatiableHungerSpeedBonus = 0.3f;
        public float InsatiableHungerCooldown = 25f;

        private float _vomitTimer, _hungerTimer;
        private Warrior _warrior;

        void Awake() { _warrior = GetComponent<Warrior>(); }
        void Update()
        {
            _vomitTimer -= Time.deltaTime;
            _hungerTimer -= Time.deltaTime;
        }
        // Список умений живёт в AOS.SkillCatalog, а не здесь: им
        // пользуются и проводка, и стенд, а две копии одного списка
        // рано или поздно разойдутся.
        public System.Collections.Generic.IReadOnlyList<AOS.ActionType> SkillActions
            => AOS.SkillCatalog.For(Sinbinder.Core.SinType.Gluttony, 1f);


        public bool CanUseSkill(AOS.ActionType action)
        {
            return action switch
            {
                // Пожирание предлагалось всегда, даже когда есть нечего:
                // умение выигрывало голосование и молча ничего не делало.
                // Бюллетень спрашивает именно здесь — значит и проверять
                // надо здесь, а не оправдываться пустой корутиной.
                AOS.ActionType.Devour => CorpseWithinReach(),
                AOS.ActionType.Vomit => _vomitTimer <= 0f,
                AOS.ActionType.InsatiableHunger => _hungerTimer <= 0f,
                _ => false
            };
        }

        public void ExecuteSkill(AOS.ActionType action)
        {
            switch (action)
            {
                case AOS.ActionType.Devour: StartCoroutine(DevourRoutine()); break;
                case AOS.ActionType.Vomit: StartCoroutine(VomitRoutine()); break;
                case AOS.ActionType.InsatiableHunger: StartCoroutine(HungerRoutine()); break;
            }
        }

        private bool CorpseWithinReach()
        {
            foreach (var body in FindObjectsByType<HarvestableBody>(FindObjectsSortMode.InstanceID))
            {
                if (body == null || body.IsCollected) continue;
                if (Vector3.Distance(transform.position, body.transform.position) < DevourReach)
                    return true;
            }
            return false;
        }

        private const float DevourReach = 2f;

        IEnumerator DevourRoutine()
        {
            var corpses = FindObjectsByType<HarvestableBody>(FindObjectsSortMode.InstanceID);
            foreach (var body in corpses)
            {
                if (!body.IsCollected && Vector3.Distance(transform.position, body.transform.position) < DevourReach)
                {
                    _warrior.Heal(_warrior.MaxHP * DevourHealPercent);
                    body.MarkCollected();
                    Debug.Log($"[GLUTTONY] {_warrior.DisplayName} пожирает труп.");
                    break;
                }
            }
            yield return null;
        }

        IEnumerator VomitRoutine()
        {
            _vomitTimer = VomitCooldown;
            if (CombatManager.Instance == null) yield break;
            var enemies = CombatManager.Instance.GetEnemies(gameObject);
            foreach (var e in enemies)
            {
                if (e == null || e.IsDead) continue;
                if (Vector3.Distance(transform.position, e.transform.position) < 3f)
                {
                    e.TakeDamage(VomitDamage, gameObject);
                    var mover = e.GetComponent<UnitMover>();
                    if (mover != null) StartCoroutine(SlowEnemy(mover, VomitSlowDuration));
                }
            }
            yield return null;
        }

        IEnumerator HungerRoutine()
        {
            _hungerTimer = InsatiableHungerCooldown;
            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            float origSpeed = agent != null ? agent.speed : 0f;
            if (agent != null) agent.speed *= (1 + InsatiableHungerSpeedBonus);
            yield return new WaitForSeconds(InsatiableHungerDuration);
            if (agent != null) agent.speed = origSpeed;
        }

        IEnumerator SlowEnemy(UnitMover mover, float duration)
        {
            var agent = mover.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent == null) yield break;
            float origSpeed = agent.speed;
            agent.speed *= 0.5f;
            yield return new WaitForSeconds(duration);
            agent.speed = origSpeed;
        }
    }
}