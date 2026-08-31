// Assets/AOS Engine/EmotionSystem.cs
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

        private Dictionary<string, List<ActiveEmotion>> _emotions = new();

        [SerializeField] private float _decayInterval = 5f;

        void Awake()
        {
            if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
            else Destroy(gameObject);
            InvokeRepeating(nameof(DecayAllEmotions), _decayInterval, _decayInterval);
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