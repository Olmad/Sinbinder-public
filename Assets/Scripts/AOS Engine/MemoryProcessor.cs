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

        private Dictionary<string, List<MemoryRecord>> _memories = new();

        [SerializeField] private int _maxRecordsPerWarrior = 50;
        [SerializeField] private float _cleanupInterval = 10f;

        void Awake()
        {
            if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
            else Destroy(gameObject);
            InvokeRepeating(nameof(CleanupWeakMemories), _cleanupInterval, _cleanupInterval);
            // Запись прочитали раньше, чем память проснулась, — кладём её сейчас.
            if (Instance == this && _pending != null) { Install(_pending); _pending = null; }
        }

        // ── Сохранение (автор, 23.11: «почему у нас проблемы с сохранением… воспоминаний?») ──
        // До 23.11 память AOS в файл не шла вовсе: отношения считаются из неё
        // (RelationshipSystem), и после загрузки все забывали, кто кого спас и кто предал.
        // Время в записи — возраст, а не Time.time: часы новой сессии начинаются с нуля.

        private static List<SavedMemory> _pending;

        /// <summary>Память тех, кто живёт дольше сцены (owners: отряд, выжившие стражи).</summary>
        public static List<SavedMemory> Snapshot(ICollection<string> owners)
        {
            var list = new List<SavedMemory>();
            var me = Instance;
            if (me == null)
            {
                // Память ещё не проснулась — отдаём прочитанное, тем же отбором по владельцам.
                if (_pending != null)
                    foreach (var s in _pending)
                        if (s != null && (owners == null || owners.Contains(s.Owner))) list.Add(s);
                return list;
            }
            float now = Time.time;
            foreach (var kv in me._memories)
            {
                if (owners != null && !owners.Contains(kv.Key)) continue;
                foreach (var m in kv.Value)
                {
                    if (m == null || Mathf.Abs(m.GetCurrentStrength(now)) <= 0.01f) continue;
                    list.Add(new SavedMemory
                    {
                        Owner = kv.Key, EventType = m.EventType, TargetID = m.TargetID ?? "",
                        Emotion = (int)m.Emotion, Importance = m.Importance, DecayRate = m.DecayRate,
                        Age = Mathf.Max(0f, now - m.CreationTime),
                    });
                }
            }
            return list;
        }

        /// <summary>Загрузка: память заменяется записанной целиком.</summary>
        public static void Restore(List<SavedMemory> saved)
        {
            var copy = saved != null ? new List<SavedMemory>(saved) : new List<SavedMemory>();
            if (Instance == null) { _pending = copy; return; }
            Instance.Install(copy);
        }

        private void Install(List<SavedMemory> saved)
        {
            // Заменяется память тех, кого знает запись: отряд, выжившие стражи и все, чья память
            // в ней лежит. Враги, живые сейчас в сцене (загрузка на месте их тел не трогает), помнят
            // своё дальше — до 23.11 (первая версия) загрузка стирала и их.
            var replace = new HashSet<string>();
            foreach (var m in SquadRoster.Members) if (!string.IsNullOrEmpty(m.WarriorId)) replace.Add(m.WarriorId);
            foreach (var s in Survivors.All) if (s != null && !string.IsNullOrEmpty(s.Id)) replace.Add(s.Id);
            foreach (var s in saved) if (s != null && !string.IsNullOrEmpty(s.Owner)) replace.Add(s.Owner);
            foreach (var id in replace) _memories.Remove(id);
            float now = Time.time;
            foreach (var s in saved)
            {
                if (s == null || string.IsNullOrEmpty(s.Owner) || string.IsNullOrEmpty(s.EventType)) continue;
                if (!_memories.TryGetValue(s.Owner, out var list)) _memories[s.Owner] = list = new List<MemoryRecord>();
                list.Add(new MemoryRecord
                {
                    EventType = s.EventType, TargetID = s.TargetID ?? "", Emotion = (EmotionType)s.Emotion,
                    Importance = Mathf.Clamp01(s.Importance), DecayRate = s.DecayRate,
                    CreationTime = now - Mathf.Max(0f, s.Age),
                });
            }
        }

        /// <summary>Новая игра — никто ничего не помнит.</summary>
        public static void ForgetAll()
        {
            _pending = null;
            Instance?._memories.Clear();
        }

        // Ушедшая память не должна отвечать за живую: «?.» у Instance не видит
        // уничтоженного объекта Unity и звал бы его методы.
        void OnDestroy()
        {
            if (Instance == this) Instance = null;
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