using UnityEngine;
using Sinbinder.Gameplay;
using Sinbinder.Core;

namespace Sinbinder.AOS
{
    public class MissionResolver : MonoBehaviour
    {
        public static MissionResolver Instance { get; private set; }

        void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public MissionOutcome Resolve(Warrior commander, Quest quest)
        {
            var context = quest.CreateContext();
            var resolver = new BehaviourResolver();
            var availableActions = quest.GetAvailableCommanderActions();
            var action = resolver.DecideMission(commander, context, availableActions);
            return quest.ApplyOutcome(action, commander);
        }
    }
}