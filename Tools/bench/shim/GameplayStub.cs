// Заглушка воина: члены, которых касается логика решений и авто-бой.
// Настоящий Warrior — MonoBehaviour с боем, навигацией и сценой;
// для подсчёта голосов и для автономной вылазки ничего этого не нужно.
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    public enum Team { Player, Enemy }

    public class Warrior
    {
        public string Id = System.Guid.NewGuid().ToString();
        public SoulData Soul;
        public string DisplayName => Soul != null ? Soul.Name : "";
        public float Loyalty = 50f;
        public int UnpaidMissions;
        public float HP = 30f, MaxHP = 30f;
        public float Attack = 6f;
        public bool IsDead => HP <= 0f;
        public bool IsCommander;
        public Team Team = Team.Player;
        public RelationshipSystem Relationships;

        public void TakeDamage(float d) { HP = UnityEngine.Mathf.Max(0f, HP - d); }
        public void Heal(float a) { HP = UnityEngine.Mathf.Min(MaxHP, HP + a); }
        public void SetCommander(bool v) => IsCommander = v;
    }

    /// <summary>Отношения в заглушке нейтральны: память здесь не ведётся.</summary>
    public class RelationshipSystem
    {
        public float GetRelationship(string a, string b) => 50f;
    }
}

namespace Sinbinder.AOS
{
    /// <summary>Хранилище памяти в стенде отсутствует.</summary>
    public class MemoryProcessor
    {
        public static MemoryProcessor Instance => null;
        public System.Collections.Generic.List<MemoryRecord> GetMemories(Sinbinder.Gameplay.Warrior w) => null;
    }
}
