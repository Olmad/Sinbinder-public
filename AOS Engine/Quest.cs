using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.AOS
{
    public abstract class Quest : ScriptableObject
    {
        public string questName;
        [TextArea] public string description;
        public List<QuestOption> playerOptions;
        public List<CommanderOutcome> commanderOutcomes;
        public bool isCompleted;

        /// <summary>
        /// Возвращает список действий, доступных командиру в этой миссии.
        /// Может быть переопределён для динамического изменения списка.
        /// </summary>
        public virtual List<MissionAction> GetAvailableCommanderActions()
        {
            return new List<MissionAction>();
        }

        /// <summary>
        /// Создаёт контекст миссии для AOS.
        /// </summary>
        protected abstract MissionContext CreateContext();

        /// <summary>
        /// Применяет выбранное действие и возвращает исход миссии.
        /// </summary>
        protected abstract MissionOutcome ApplyOutcome(MissionAction action, Warrior commander);

        /// <summary>
        /// Разрешить квест действием игрока (выбор варианта).
        /// </summary>
        public abstract void ResolvePlayerChoice(QuestOption chosenOption);

        /// <summary>
        /// Разрешить квест через командира (AOS).
        /// </summary>
        public MissionOutcome ResolveCommander(Warrior commander)
        {
            var context = CreateContext();
            var resolver = new BehaviourResolver();
            var availableActions = GetAvailableCommanderActions();
            var action = resolver.DecideMission(commander, context, availableActions);
            return ApplyOutcome(action, commander);
        }
    }

    [System.Serializable]
    public class QuestOption
    {
        public string text;
        public MissionOutcome outcome;
        public int respectChange;
        public int fearChange;
        public int goldChange;
        public int moralityChange;
    }

    [System.Serializable]
    public class CommanderOutcome
    {
        public SinType sin;
        public MoralType moral;
        public MissionAction action;
    }
}