// Assets/Scripts/AOS Engine/MemoryProcessor.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.AOS
{
    public class MemoryProcessor : MonoBehaviour
    {
        public static MemoryProcessor Instance { get; private set; }

        // Загрузка может восстановить мир до появления компонента в сцене.
        private static readonly Dictionary<string, List<MemoryRecord>> _memories = new();

        [SerializeField] private int _maxRecordsPerWarrior = 50;
        [SerializeField] private float _cleanupInterval = 10f;

        void Awake()
        {
            if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
            else Destroy(gameObject);
            InvokeRepeating(nameof(CleanupWeakMemories), _cleanupInterval, _cleanupInterval);
        }

        // Ушедшая память не должна отвечать за живую: «?.» у Instance не видит
        // уничтоженного объекта Unity и звал бы его методы.
        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        internal static List<Core.SavedMemory> Capture(string warriorId, float now)
        {
            if (string.IsNullOrEmpty(warriorId) || !_memories.TryGetValue(warriorId, out var records))
                return null;

            var copy = new List<Core.SavedMemory>(records.Count);
            foreach (var record in records)
            {
                copy.Add(new Core.SavedMemory
                {
                    EventType = record.EventType,
                    TargetId = record.TargetID,
                    Emotion = record.Emotion,
                    Importance = record.Importance,
                    DecayRate = record.DecayRate,
                    Age = Mathf.Max(0f, now - record.CreationTime),
                });
            }
            return copy;
        }

        internal static void Restore(string warriorId, List<Core.SavedMemory> saved,
            IReadOnlyDictionary<string, string> targetIds, float now)
        {
            if (saved == null || saved.Count == 0) return;

            var records = new List<MemoryRecord>(saved.Count);
            foreach (var record in saved)
            {
                if (string.IsNullOrEmpty(record.EventType) ||
                    !System.Enum.IsDefined(typeof(EmotionType), record.Emotion) ||
                    float.IsNaN(record.Importance) || float.IsInfinity(record.Importance) || record.Importance < 0f ||
                    float.IsNaN(record.DecayRate) || float.IsInfinity(record.DecayRate) || record.DecayRate < 0f ||
                    float.IsNaN(record.Age) || float.IsInfinity(record.Age) || record.Age < 0f)
                    continue;

                string targetId = record.TargetId;
                if (targetId != null && targetIds.TryGetValue(targetId, out var liveId))
                    targetId = liveId;

                records.Add(new MemoryRecord
                {
                    EventType = record.EventType,
                    TargetID = targetId,
                    Emotion = record.Emotion,
                    Importance = record.Importance,
                    DecayRate = record.DecayRate,
                    CreationTime = now - record.Age,
                });
            }
            if (records.Count > 0) _memories[warriorId] = records;
        }

        internal static void Forget() => _memories.Clear();

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
                    .OrderByDescending(m => Mathf.Abs(m.GetCurrentStrength(Time.time)))
                    .Take(_maxRecordsPerWarrior)
                    .ToList();
            }
        }

        /// <summary>
        /// Живые воспоминания, сильные — первыми. Сила со знаком (гнев, страх,
        /// печаль — минус), поэтому живость — по модулю. До 06.11 здесь стояло
        /// «сила > 0,05», и вся тёмная память пропадала на входе: убитый не помнил
        /// убийц (голос Памяти во сне молчал при них на каждом прогоне), обида
        /// и отнятое не трогали отношений. Чистка памяти и модуль Памяти и раньше
        /// брали модуль — отсекала только выборка.
        /// </summary>
        public List<MemoryRecord> GetMemories(Warrior warrior)
        {
            if (!_memories.ContainsKey(warrior.Id)) return new List<MemoryRecord>();
            return Vivid(_memories[warrior.Id]);
        }

        private static List<MemoryRecord> Vivid(List<MemoryRecord> all)
        {
            float currentTime = Time.time;
            return all
                .Where(m => Mathf.Abs(m.GetCurrentStrength(currentTime)) > 0.05f)
                .OrderByDescending(m => Mathf.Abs(m.GetCurrentStrength(currentTime)))
                .ToList();
        }

        public List<MemoryRecord> GetMemoriesById(string warriorId)
        {
            if (!_memories.ContainsKey(warriorId)) return new List<MemoryRecord>();
            return Vivid(_memories[warriorId]);
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
                    .OrderByDescending(m => Mathf.Abs(m.GetCurrentStrength(Time.time)))
                    .Take(_maxRecordsPerWarrior)
                    .ToList();
            }
        }

        internal void RecordBattle(string warriorId, BattleResult result)
        {
            switch (result.Outcome)
            {
                case BattleOutcome.Victory:
                    CreateMemoryById(warriorId, "WonBattle", "", EmotionType.Joy, 0.6f + result.EnemiesKilled * 0.1f);
                    break;
                case BattleOutcome.Defeat:
                    CreateMemoryById(warriorId, "LostBattle", "", EmotionType.Anger, 0.7f + result.AlliesLost * 0.2f);
                    return;
            }

            if (result.AlliesLost > 0)
                CreateMemoryById(warriorId, "AlliesLost", "", EmotionType.Sadness, result.AlliesLost * 0.3f);
        }

        public void RecordInteraction(Warrior subject, Warrior target, string eventType)
        {
            EmotionType emotion = eventType switch
            {
                "AllySavedMe" => EmotionType.Joy,
                "AllyBetrayedMe" => EmotionType.Anger,
                "AllyKilledEnemy" => EmotionType.Hope,
                // Обмен вещами с Греховодом (SquadGear): дать — радость,
                // отнять — злость, и отношения к нему считаются и из этого.
                "SinbinderGaveMe" => EmotionType.Joy,
                "SinbinderTookFromMe" => EmotionType.Anger,
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
