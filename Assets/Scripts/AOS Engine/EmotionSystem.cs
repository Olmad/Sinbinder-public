// Assets/Scripts/AOS Engine/EmotionSystem.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.AOS
{
    public enum EmotionType
    {
        Calm, Joy, Fear, Anger, Sadness, Disgust, Surprise, Hope
    }

    [System.Serializable]
    public struct SavedEmotion
    {
        public string Owner;
        public EmotionType Type;
        public float Intensity;
    }

    public class EmotionSystem : MonoBehaviour
    {
        public static EmotionSystem Instance { get; private set; }

        private readonly Dictionary<string, List<ActiveEmotion>> _emotions = new();
        private static List<SavedEmotion> _pending;

        [SerializeField] private float _decayInterval = 5f;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InvokeRepeating(nameof(DecayAllEmotions), _decayInterval, _decayInterval);
            if (_pending != null)
            {
                Install(_pending);
                _pending = null;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        internal static List<SavedEmotion> Snapshot(ICollection<string> owners)
        {
            var copy = new List<SavedEmotion>();
            var system = Instance;
            if (system == null)
            {
                if (_pending != null)
                    foreach (var emotion in _pending)
                        if (owners.Contains(emotion.Owner)) copy.Add(emotion);
                return copy;
            }

            foreach (var owner in owners)
                if (system._emotions.TryGetValue(owner, out var active))
                    foreach (var emotion in active)
                        copy.Add(new SavedEmotion
                        {
                            Owner = owner, Type = emotion.Type, Intensity = emotion.Intensity,
                        });
            return copy;
        }

        internal static void Restore(IEnumerable<SavedEmotion> saved, ISet<string> replace)
        {
            var copy = new List<SavedEmotion>();
            var claimed = new HashSet<(string, EmotionType)>();
            if (saved != null)
                foreach (var emotion in saved)
                {
                    if (string.IsNullOrEmpty(emotion.Owner) || !replace.Contains(emotion.Owner) ||
                        !System.Enum.IsDefined(typeof(EmotionType), emotion.Type) ||
                        float.IsNaN(emotion.Intensity) || float.IsInfinity(emotion.Intensity) ||
                        emotion.Intensity <= 0f || !claimed.Add((emotion.Owner, emotion.Type)))
                        continue;

                    copy.Add(new SavedEmotion
                    {
                        Owner = emotion.Owner, Type = emotion.Type, Intensity = Mathf.Clamp01(emotion.Intensity),
                    });
                }

            var system = Instance;
            if (system == null) { _pending = copy; return; }
            foreach (var owner in replace) system._emotions.Remove(owner);
            system.Install(copy);
        }

        private void Install(List<SavedEmotion> saved)
        {
            foreach (var emotion in saved)
            {
                if (!_emotions.TryGetValue(emotion.Owner, out var active))
                    _emotions[emotion.Owner] = active = new List<ActiveEmotion>();
                active.Add(new ActiveEmotion { Type = emotion.Type, Intensity = emotion.Intensity });
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ForgetAll()
        {
            _pending = null;
            if (Instance != null) Instance._emotions.Clear();
        }

        /// <summary>
        /// Вызвать эмоцию.
        /// </summary>
        public void TriggerEmotion(Warrior warrior, EmotionType type, float intensity)
        {
            if (!_emotions.ContainsKey(warrior.Id))
                _emotions[warrior.Id] = new List<ActiveEmotion>();

            var existing = _emotions[warrior.Id].FirstOrDefault(e => e.Type == type);
            if (existing != null)
                existing.Intensity = Mathf.Min(1f, existing.Intensity + intensity);
            else
                _emotions[warrior.Id].Add(new ActiveEmotion { Type = type, Intensity = intensity });

            // Только разработчику: запись в журнал Unity дорога (стек вызовов на каждую),
            // а чувств на гибель — по два на каждого свидетеля. На поле в 400 одна гибель
            // стоила 20–40 мс кадра (замер 27.10, TugBench).
            if (Core.Transparency.Shows(Core.Clarity.Trace))
                Debug.Log($"[EMOTION] {warrior.DisplayName}: {type} +{intensity}");
        }

        /// <summary>
        /// Получить доминирующую эмоцию.
        /// </summary>
        public EmotionType GetDominantEmotion(Warrior warrior)
        {
            if (!_emotions.ContainsKey(warrior.Id) || _emotions[warrior.Id].Count == 0)
                return EmotionType.Calm;

            return _emotions[warrior.Id]
                .OrderByDescending(e => e.Intensity)
                .First().Type;
        }

        /// <summary>
        /// Получить модификатор веса модуля в зависимости от эмоций.
        /// </summary>
        public float GetEmotionWeight(Warrior warrior, string moduleID)
        {
            var dominant = GetDominantEmotion(warrior);
            return dominant switch
            {
                EmotionType.Fear => moduleID == "Fear" ? 1.5f : moduleID == "Wrath" ? 0.5f : 1.0f,
                EmotionType.Anger => moduleID == "Wrath" ? 1.5f : moduleID == "Virtue" ? 0.5f : 1.0f,
                EmotionType.Joy => moduleID == "Loyalty" ? 1.3f : 1.0f,
                EmotionType.Sadness => moduleID == "Fear" ? 1.3f : 0.9f,
                _ => 1.0f
            };
        }

        private void DecayAllEmotions()
        {
            foreach (var kvp in _emotions)
            {
                foreach (var e in kvp.Value)
                    e.Intensity -= 0.1f;
                kvp.Value.RemoveAll(e => e.Intensity <= 0.01f);
            }
        }

        private class ActiveEmotion
        {
            public EmotionType Type;
            public float Intensity;
        }
    }
}
