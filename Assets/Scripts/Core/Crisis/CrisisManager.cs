// Assets/Scripts/Core/Crisis/CrisisManager.cs
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.Core
{
    public class CrisisManager : MonoBehaviour
    {
        public static CrisisManager Instance { get; private set; }

        [SerializeField] private List<CrisisData> _allCrises = new();
        private Dictionary<string, CrisisData> _crisisDict = new();

        public System.Action<CrisisData, Warrior, Warrior> OnCrisisTriggered;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                BuildDictionary();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void BuildDictionary()
        {
            _crisisDict.Clear();
            foreach (var crisis in _allCrises)
            {
                if (!string.IsNullOrEmpty(crisis.Id))
                    _crisisDict[crisis.Id] = crisis;
            }
        }

        public CrisisData CheckCrisis(Warrior warrior, Warrior target, string situation)
        {
            // 1. Сначала ищем по точному ключу
            string key = $"{situation}_{warrior.Soul.Sin}";
            if (_crisisDict.TryGetValue(key, out var crisis))
                return crisis;

            // 2. Затем перебираем все кризисы и проверяем условия
            foreach (var c in _allCrises)
            {
                if (c.TriggerCondition != situation) continue;
                if (c.WarriorSin == warrior.Soul.Sin)
                    return c;
            }
            return null;
        }

        public void ApplyChoice(CrisisData crisis, int choiceIndex, Warrior warrior, Warrior target)
        {
            if (choiceIndex < 0 || choiceIndex >= crisis.Choices.Count) return;

            var choice = crisis.Choices[choiceIndex];
            warrior.Virtue.Change(choice.VirtueChange);
            warrior.ChangeLoyalty(choice.LoyaltyChange);

            if (target != null && choice.RelationshipChange != 0)
                warrior.Relationships.Change(warrior.Id, target.Id, choice.RelationshipChange);
        }
    }
}