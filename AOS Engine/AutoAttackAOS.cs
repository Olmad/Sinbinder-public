// Assets/_Project/Scripts/AOS/AutoAttackAOS.cs (полностью)
using UnityEngine;

namespace Sinbinder.AOS
{
    [RequireComponent(typeof(AOSWarriorWrapper))]
    public class AutoAttackAOS : MonoBehaviour
    {
        [SerializeField] private float _decisionInterval = 1f;

        private AOSWarriorWrapper _wrapper;
        private float _lastDecisionTime;

        void Awake()
        {
            _wrapper = GetComponent<AOSWarriorWrapper>();
        }

        void Update()
        {
            if (Time.time - _lastDecisionTime < _decisionInterval) return;
            _lastDecisionTime = Time.time;

            var action = _wrapper.Decide();
            _wrapper.Execute(action);
        }
    }
}