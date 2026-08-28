// Assets/_Project/Scripts/Gameplay/DiligenceSkills.cs
using System.Collections;
using UnityEngine;
using Sinbinder.AOS;

namespace Sinbinder.Gameplay
{
    public class DiligenceSkills : MonoBehaviour, AOS.ISkillSet
    {
        public float WorkSurgeDuration = 10f;
        public float WorkSurgeSpeedBonus = 0.3f;
        public float WorkSurgeCooldown = 15f;
        public float WorkInspirationDuration = 8f;
        public float WorkInspirationBonus = 0.15f;
        public float WorkInspirationCooldown = 20f;
        public float TirelessCooldownReduction = 0.5f; // Снижает кулдаун одного навыка вдвое

        private float _surgeTimer, _inspirationTimer, _tirelessTimer;
        private Warrior _warrior;

        void Awake() { _warrior = GetComponent<Warrior>(); }
        void Update()
        {
            _surgeTimer -= Time.deltaTime;
            _inspirationTimer -= Time.deltaTime;
            _tirelessTimer -= Time.deltaTime;
        }
        private static readonly AOS.ActionType[] _actions = { AOS.ActionType.WorkSurge, AOS.ActionType.WorkInspiration, AOS.ActionType.Tireless };
        public System.Collections.Generic.IReadOnlyList<AOS.ActionType> SkillActions => _actions;


        public bool CanUseSkill(ActionType action)
        {
            return action switch
            {
                ActionType.WorkSurge => _surgeTimer <= 0f,
                ActionType.WorkInspiration => _inspirationTimer <= 0f,
                ActionType.Tireless => _tirelessTimer <= 0f,
                _ => false
            };
        }

        public void ExecuteSkill(ActionType action)
        {
            switch (action)
            {
                case ActionType.WorkSurge: StartCoroutine(WorkSurgeRoutine()); break;
                case ActionType.WorkInspiration: StartCoroutine(WorkInspirationRoutine()); break;
                case ActionType.Tireless: StartCoroutine(TirelessRoutine()); break;
            }
        }

        IEnumerator WorkSurgeRoutine()
        {
            _surgeTimer = WorkSurgeCooldown;
            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) agent.speed *= (1 + WorkSurgeSpeedBonus);
            Debug.Log($"[DILIGENCE] {_warrior.DisplayName} активирует Трудовой Порыв.");
            yield return new WaitForSeconds(WorkSurgeDuration);
            if (agent != null) agent.speed /= (1 + WorkSurgeSpeedBonus);
        }

        IEnumerator WorkInspirationRoutine()
        {
            _inspirationTimer = WorkInspirationCooldown;
            Debug.Log($"[DILIGENCE] {_warrior.DisplayName} вдохновляет союзников на труд.");
            yield return null;
        }

        IEnumerator TirelessRoutine()
        {
            _tirelessTimer = 60f;
            Debug.Log($"[DILIGENCE] {_warrior.DisplayName} игнорирует кулдаун одного навыка.");
            yield return null;
        }
    }
}