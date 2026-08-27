// Assets/_Project/Scripts/AOS/MemoryProcessor.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.AOS
{
    public class MemoryProcessor : MonoBehaviour
    {
        public static MemoryProcessor Instance { get; private set; }

        private Dictionary<string, List<MemoryRecord>> _memories = new();

        [SerializeField] private int _maxRecordsPerWarrior = 50;
        [SerializeField] private float _cleanupInterval = 10f;

        void Awake()
        {
            if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
            else Destroy(gameObject);
            InvokeRepeating(nameof(CleanupWeakMemories), _cleanupInterval, _cleanupInterval);
        }

        public void CreateMemory(Warrior warrior, string eventType, string targetId, EmotionType emotion, float importance)
        {
            if (!_memories.ContainsKey(warrior.Id))
                _memories[warrior.Id] = new List<MemoryRecord>();

            var existing = _memories[warrior.Id]
                .FirstOrDefault(m => m.EventType == eventType && m.TargetID == targetId);

            if (existing != null)
            {
                existing.Importance = Mathf.Max(existing.Importance, importance);
                existing.CreationTime = Time.time;
            }
            else
            {
                _memories[warrior.Id].Add(new MemoryRecord(targetId, eventType, emotion, importance));
            }

            if (_memories[warrior.Id].Count > _maxRecordsPerWarrior)
            {
                _memories[warrior.Id] = _memories[warrior.Id]
                    .OrderByDescending(m => m.GetCurrentStrength(Time.time))
                    .Take(_maxRecordsPerWarrior)
                    .ToList();
            }
        }

        public List<MemoryRecord> GetMemories(Warrior warrior)
        {
            if (!_memories.ContainsKey(warrior.Id)) return new List<MemoryRecord>();
            float currentTime = Time.time;
            return _memories[warrior.Id]
                .Where(m => m.GetCurrentStrength(currentTime) > 0.05f)
                .OrderByDescending(m => m.GetCurrentStrength(currentTime))
                .ToList();
        }

        public List<MemoryRecord> GetMemoriesById(string warriorId)
        {
            if (!_memories.ContainsKey(warriorId)) return new List<MemoryRecord>();
            float currentTime = Time.time;
            return _memories[warriorId]
                .Where(m => m.GetCurrentStrength(currentTime) > 0.05f)
                .OrderByDescending(m => m.GetCurrentStrength(currentTime))
                .ToList();
        }

        public void CreateMemoryById(string warriorId, string eventType, string targetId, EmotionType emotion, float importance)
        {
            if (!_memories.ContainsKey(warriorId))
                _memories[warriorId] = new List<MemoryRecord>();

            var existing = _memories[warriorId]
                .FirstOrDefault(m => m.EventType == eventType && m.TargetID == targetId);

            if (existing != null)
            {
                existing.Importance = Mathf.Max(existing.Importance, importance);
                existing.CreationTime = Time.time;
            }
            else
            {
                _memories[warriorId].Add(new MemoryRecord(targetId, eventType, emotion, importance));
            }

            if (_memories[warriorId].Count > _maxRecordsPerWarrior)
            {
                _memories[warriorId] = _memories[warriorId]
                    .OrderByDescending(m => m.GetCurrentStrength(Time.time))
                    .Take(_maxRecordsPerWarrior)
                    .ToList();
            }
        }

        public void RecordBattle(Warrior warrior, bool won, int enemiesKilled, int alliesLost)
        {
            if (won)
            {
                CreateMemory(warrior, "WonBattle", "", EmotionType.Joy, 0.6f + enemiesKilled * 0.1f);
                if (alliesLost > 0)
                    CreateMemory(warrior, "AlliesLost", "", EmotionType.Sadness, alliesLost * 0.3f);
            }
            else
            {
                CreateMemory(warrior, "LostBattle", "", EmotionType.Anger, 0.7f + alliesLost * 0.2f);
            }
        }

        public void RecordInteraction(Warrior subject, Warrior target, string eventType)
        {
            EmotionType emotion = eventType switch
            {
                "AllySavedMe" => EmotionType.Joy,
                "AllyBetrayedMe" => EmotionType.Anger,
                "AllyKilledEnemy" => EmotionType.Hope,
                _ => EmotionType.Calm
            };
            CreateMemory(subject, eventType, target.Id, emotion, 0.8f);
        }

        private void CleanupWeakMemories()
        {
            float currentTime = Time.time;
            foreach (var kvp in _memories)
            {
                kvp.Value.RemoveAll(m => Mathf.Abs(m.GetCurrentStrength(currentTime)) <= 0.01f);
            }
        }

        public bool HasCoreMemory(Warrior warrior, DeedType deedType)
        {
            if (!_memories.ContainsKey(warrior.Id)) return false;
            return _memories[warrior.Id].Any(m => m.EventType == deedType.ToString() && m.Importance > 0.8f);
        }
    }
}