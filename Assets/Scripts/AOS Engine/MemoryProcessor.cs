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

        // Body GUIDs are registered at initialization, even before combat
        // registration. Death callbacks may address a just-unregistered body.
        private static readonly Dictionary<string, string> _bodyPeople = new();
        internal static void RegisterBodyIdentity(string bodyId, string personId)
        {
            if (string.IsNullOrEmpty(bodyId) || string.IsNullOrEmpty(personId)) return;
            if (!_bodyPeople.TryGetValue(bodyId, out var known)) _bodyPeople.Add(bodyId, personId);
            else if (known != personId) _bodyPeople[bodyId] = null; // ambiguous: never guess
        }

        private static string PersonOf(string bodyId)
            => !string.IsNullOrEmpty(bodyId) && _bodyPeople.TryGetValue(bodyId, out var person) ? person : null;

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
                        TargetPersonId = m.TargetPersonId,
                        Emotion = (int)m.Emotion, Importance = m.Importance, DecayRate = m.DecayRate,
                        Age = Mathf.Max(0f, now - m.CreationTime),
                    });
                }
            }
            return list;
        }

        // Fresh body only: consume the unbound payload once. No global ledger
        // restore here, and no name-based target remapping.
        internal void InstallRaisedMemories(Warrior warrior)
        {
            if (warrior == null || warrior.Soul == null || string.IsNullOrEmpty(warrior.Id)) return;
            var saved = warrior.Soul.PastMemoriesAt(Time.time, warrior.Id);
            if (saved.Count == 0) return;
            if (_memories.ContainsKey(warrior.Id))
            {
                Debug.LogWarning("[AOS] Cannot install raised memories over an existing body ledger.");
                return;
            }
            _memories.Add(warrior.Id, saved.Select(record => record.ToLive(Time.time)).ToList());
            warrior.Soul.ClearPastMemories();
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
                    EventType = s.EventType, TargetID = s.TargetID ?? "", TargetPersonId = s.TargetPersonId, Emotion = (EmotionType)s.Emotion,
                    Importance = Mathf.Clamp01(s.Importance), DecayRate = s.DecayRate,
                    CreationTime = now - Mathf.Max(0f, s.Age),
                });
            }
        }

        /// <summary>Новая игра — никто ничего не помнит.</summary>
        public static void ForgetAll()
        {
            _pending = null;
            _bodyPeople.Clear();
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
            if (warrior == null) return;
            CreateMemoryById(warrior.Id, eventType, targetId, emotion, importance);
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
            var vivid = all
                .Where(m => Mathf.Abs(m.GetCurrentStrength(currentTime)) > 0.05f)
                .OrderByDescending(m => Mathf.Abs(m.GetCurrentStrength(currentTime)))
                .ToList();
            if (!vivid.Any(m => !string.IsNullOrEmpty(m.TargetPersonId)) || CombatManager.Instance == null)
                return vivid;

            var bodies = CombatManager.Instance.GetAllWarriors()
                .Where(w => w != null && !w.IsDead && w.gameObject.activeInHierarchy && w.Soul != null)
                .Select(w => new KeyValuePair<string, string>(w.Soul.PersonId, w.Id));
            var current = MemoryRecord.UniqueLivingBodies(bodies);
            return vivid.Select(m => m.ProjectTarget(current)).ToList();
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

            string personId = PersonOf(targetId);
            var existing = _memories[warriorId]
                .FirstOrDefault(m => m.EventType == eventType && m.MatchesTarget(targetId, personId));

            if (existing != null)
            {
                existing.Importance = Mathf.Max(existing.Importance, importance);
                existing.CreationTime = Time.time;
                if (string.IsNullOrEmpty(existing.TargetPersonId)) existing.TargetPersonId = personId;
            }
            else
            {
                _memories[warriorId].Add(new MemoryRecord(targetId, eventType, emotion, importance)
                    { TargetPersonId = personId });
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
            if (string.IsNullOrEmpty(warriorId)) return;

            switch (result.Outcome)
            {
                case BattleOutcome.Victory:
                    CreateMemoryById(warriorId, "WonBattle", "", EmotionType.Joy,
                        Mathf.Clamp01(0.6f + result.EnemiesKilled * 0.1f));
                    break;
                case BattleOutcome.Defeat:
                    CreateMemoryById(warriorId, "LostBattle", "", EmotionType.Anger,
                        Mathf.Clamp01(0.7f + result.AlliesLost * 0.2f));
                    return;
                case BattleOutcome.Draw:
                    CreateMemoryById(warriorId, "DrewBattle", "", EmotionType.Sadness, 0.6f);
                    break;
            }

            if (result.AlliesLost > 0)
                CreateMemoryById(warriorId, "AlliesLost", "", EmotionType.Sadness,
                    Mathf.Clamp01(result.AlliesLost * 0.3f));
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
