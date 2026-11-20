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

    public class EmotionSystem : MonoBehaviour
    {
        public static EmotionSystem Instance { get; private set; }

        private static readonly Dictionary<string, List<Core.SavedEmotion>> _emotions = new();
        private static float _pendingDecayDelay = -1f;

        [SerializeField] private float _decayInterval = 5f;
        private float _nextDecay;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            ScheduleDecay();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        internal static List<Core.SavedEmotion> Capture(string warriorId)
        {
            return !string.IsNullOrEmpty(warriorId) && _emotions.TryGetValue(warriorId, out var active)
                ? new List<Core.SavedEmotion>(active) : null;
        }

        internal static void Restore(string warriorId, List<Core.SavedEmotion> saved)
        {
            if (saved == null || saved.Count == 0) return;

            var active = new List<Core.SavedEmotion>(saved.Count);
            foreach (var emotion in saved)
            {
                if (!System.Enum.IsDefined(typeof(EmotionType), emotion.Type) ||
                    float.IsNaN(emotion.Intensity) || float.IsInfinity(emotion.Intensity) || emotion.Intensity <= 0f)
                    continue;

                active.Add(new Core.SavedEmotion
                {
                    Type = emotion.Type,
                    Intensity = Mathf.Clamp01(emotion.Intensity),
                });
            }
            if (active.Count > 0) _emotions[warriorId] = active;
        }

        internal static float DecayDelay => Instance != null
            ? Mathf.Max(0f, Instance._nextDecay - Time.time) : _pendingDecayDelay;

        internal static void Forget(float decayDelay = -1f)
        {
            _emotions.Clear();
            _pendingDecayDelay = float.IsNaN(decayDelay) || float.IsInfinity(decayDelay) ? -1f : decayDelay;
            if (Instance != null) Instance.ScheduleDecay();
        }

        private void ScheduleDecay()
        {
            if (!Application.isPlaying) return;

            float delay = _pendingDecayDelay >= 0f
                ? Mathf.Min(_pendingDecayDelay, _decayInterval) : _decayInterval;
            _pendingDecayDelay = -1f;
            _nextDecay = Time.time + delay;
            CancelInvoke(nameof(DecayAllEmotions));
            InvokeRepeating(nameof(DecayAllEmotions), delay, _decayInterval);
        }

        /// <summary>
        /// Вызвать эмоцию.
        /// </summary>
        public void TriggerEmotion(Warrior warrior, EmotionType type, float intensity)
        {
            if (!_emotions.TryGetValue(warrior.Id, out var active))
                _emotions[warrior.Id] = active = new List<Core.SavedEmotion>();

            int index = active.FindIndex(e => e.Type == type);
            if (index >= 0)
                active[index] = new Core.SavedEmotion { Type = type, Intensity = Mathf.Min(1f, active[index].Intensity + intensity) };
            else
                active.Add(new Core.SavedEmotion { Type = type, Intensity = intensity });

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
            _nextDecay = Time.time + _decayInterval;
            foreach (var kvp in _emotions)
            {
                var active = kvp.Value;
                for (int i = 0; i < active.Count; i++)
                {
                    var emotion = active[i];
                    emotion.Intensity -= 0.1f;
                    active[i] = emotion;
                }
                kvp.Value.RemoveAll(e => e.Intensity <= 0.01f);
            }
        }
    }
}
