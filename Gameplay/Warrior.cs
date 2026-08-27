// Assets/_Project/Scripts/Gameplay/Warrior.cs
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;
using Sinbinder.AOS;

namespace Sinbinder.Gameplay
{
    public enum Team
    {
        Player,
        Enemy
    }

    public class Warrior : MonoBehaviour
    {
        [SerializeField] private string _id;
        [SerializeField] private Core.SoulData _soul;
        [SerializeField] private Core.ShellType _shell;
        [SerializeField] private float _maxHP;
        [SerializeField] private float _hp;
        [SerializeField] private float _attack;
        [SerializeField] private float _defense;
        [SerializeField] private bool _isCommander;
        [SerializeField] private Team _team;

        private Core.VirtueSystem _virtue;
        private float _loyalty = 50f;
        private int _unpaidMissions = 0;
        private Core.RelationshipSystem _relationships;
        private bool _isDead = false;
        private HashSet<string> _spokenWithThisBattle = new();

        public string Id => _id;
        public string DisplayName => _soul.Name;
        public Core.SoulData Soul => _soul;
        public Core.ShellType Shell => _shell;
        public Core.VirtueSystem Virtue => _virtue;
        public float HP => _hp;
        public float MaxHP => _maxHP;
        public float Attack { get => _attack; set => _attack = value; }
        public float Defense { get => _defense; set => _defense = value; }
        public Core.RelationshipSystem Relationships => _relationships;
        public float Loyalty => _loyalty;
        public int UnpaidMissions { get => _unpaidMissions; set => _unpaidMissions = value; }
        public bool IsDead => _isDead;
        public bool IsCommander => _isCommander;
        public Team Team { get => _team; set => _team = value; }
        public ReputationData Reputation = new();
        public int Salary => _soul.Level * 10;

        public void Initialize(Core.SoulData soul, Core.ShellType shell, Core.RelationshipSystem relSystem, bool isCommander = false, Team team = Team.Player)
        {
            _id = System.Guid.NewGuid().ToString();
            _soul = soul;
            _shell = shell;
            _virtue = new Core.VirtueSystem(soul);
            _relationships = relSystem;
            _isCommander = isCommander;
            _team = team;

            _maxHP = 20f + soul.Level * 10f;
            _hp = _maxHP;
            _attack = 3f + soul.Level * 2f;
            _defense = 1f + soul.Level;

            // Применяем пассивные перки, влияющие на скорость передвижения
            if (_soul.HasMemory && _soul.Memory.NarrativePerks != null)
            {
                var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null)
                {
                    if (_soul.Memory.NarrativePerks.Exists(p => p.PerkName == "Часовщик"))
                    {
                        agent.speed *= 1.05f;
                        agent.angularSpeed *= 1.05f;
                    }
                    if (_soul.Memory.NarrativePerks.Exists(p => p.PerkName == "Бегун"))
                    {
                        agent.speed *= 1.10f;
                    }
                }
            }

            Debug.Log($"[SINBINDER] Воин создан: {soul.Name} | {soul.GetSinName()} | {soul.GetMoralName()} | Командир: {_isCommander} | Команда: {_team}");
        }

        public bool HasSpokenWith(string otherId) => _spokenWithThisBattle.Contains(otherId);
        public void MarkSpokenWith(string otherId) => _spokenWithThisBattle.Add(otherId);
        public void ResetBattleDialogue() => _spokenWithThisBattle.Clear();

        public string GetPersonality()
        {
            string desc = $"=== {DisplayName} ===\n";
            desc += $"Оболочка: {_shell}\n";
            desc += $"Грех: {_soul.GetSinName()}\n";
            desc += $"Характер: {_virtue.GetDescription()}\n";
            desc += $"Мораль: {_soul.GetMoralName()}\n";
            desc += $"Роль: {(_isCommander ? "Командир" : "Рядовой")}\n";
            desc += $"Команда: {_team}\n";

            if (_loyalty > 70f) desc += "Верность: Предан вам\n";
            else if (_loyalty < 30f) desc += "Верность: Готов предать\n";
            else desc += "Верность: Нейтральна\n";

            if (_unpaidMissions > 0) desc += $"Не получал плату: {_unpaidMissions} миссий\n";
            if (_soul.HasMemory) desc += $"Память: Дремлет...\n";

            return desc;
        }

        public void TakeDamage(float damage)
        {
            if (_isDead) return;
            float actual = Mathf.Max(1f, damage - _defense);
            _hp -= actual;
            if (_hp <= 0f) { _hp = 0f; _isDead = true; Debug.Log($"[SINBINDER] {DisplayName} пал в бою!"); }
        }

        public void PaySalary(float amount)
        {
            if (amount >= _soul.Level * 10f) { _loyalty = Mathf.Min(100f, _loyalty + 5f); _unpaidMissions = 0; }
            else { _loyalty -= 20f; _unpaidMissions++; }
        }

        public void ChangeLoyalty(float amount) => _loyalty = Mathf.Clamp(_loyalty + amount, 0f, 100f);
    }
}