// Перевод: текст через Loc
using System;
using UnityEngine;

namespace Sinbinder.AOS
{
    /// <summary>Воспоминание в файле: чьё оно и сколько ему секунд (не Time.time — часы сессии).</summary>
    [Serializable]
    public class SavedMemory
    {
        public string Owner;
        public string EventType;
        public string TargetID;
        public int Emotion;
        public float Importance;
        public float DecayRate;
        public float Age;

        internal bool IsValidForTransfer()
            => !string.IsNullOrEmpty(Owner) && !string.IsNullOrEmpty(EventType) &&
               Enum.IsDefined(typeof(EmotionType), Emotion) &&
               !float.IsNaN(Importance) && !float.IsInfinity(Importance) && Importance >= 0f && Importance <= 1f &&
               !float.IsNaN(DecayRate) && !float.IsInfinity(DecayRate) && DecayRate >= 0f &&
               !float.IsNaN(Age) && !float.IsInfinity(Age) && Age >= 0f;

        internal SavedMemory CopyAtAge(float extraAge, string newOwner = null)
            => new SavedMemory
            {
                Owner = newOwner ?? Owner, EventType = EventType,
                TargetID = newOwner != null && TargetID == Owner ? newOwner : TargetID ?? "",
                Emotion = Emotion, Importance = Importance, DecayRate = DecayRate,
                Age = Age + Math.Max(0f, extraAge),
            };

        internal MemoryRecord ToLive(float now)
            => new MemoryRecord
            {
                EventType = EventType, TargetID = TargetID ?? "", Emotion = (EmotionType)Emotion,
                Importance = Importance, DecayRate = DecayRate, CreationTime = now - Age,
            };
    }

    [Serializable]
    public class MemoryRecord
    {
        public string EventType;
        public string TargetID;
        public EmotionType Emotion;      // Теперь enum
        public float Importance;         // 0..1
        public float DecayRate;          // скорость забывания
        public float CreationTime;       // Time.time в момент создания

        // Для обратной совместимости — вычисляемое свойство
        public float Strength => GetCurrentStrength(Time.time);

        // Пустой конструктор для сериализации
        public MemoryRecord() { }

        // Основной конструктор
        public MemoryRecord(string targetID, string eventType, EmotionType emotion, float importance, float decayRate = 0.01f)
        {
            TargetID = targetID;
            EventType = eventType;
            Emotion = emotion;
            Importance = Mathf.Clamp01(importance);
            DecayRate = decayRate;
            CreationTime = Time.time;
        }

        /// <summary>
        /// Текущая сила воспоминания с учётом затухания.
        /// Формула: Strength = Importance * |вес эмоции| * Recency * знак веса
        /// Recency = 1 / (1 + Age * DecayRate)
        /// </summary>
        public float GetCurrentStrength(float currentTime)
        {
            float age = currentTime - CreationTime;
            float recency = 1f / (1f + age * DecayRate);
            float emotionWeight = GetEmotionWeight(Emotion);
            return Importance * Mathf.Abs(emotionWeight) * recency * Mathf.Sign(emotionWeight);
        }

        // Веса эмоций (возьми свои или эти, основанные на твоих описаниях)
        public static float GetEmotionWeight(EmotionType emotion)
        {
            return emotion switch
            {
                EmotionType.Joy      =>  1.0f,
                EmotionType.Hope     =>  0.8f,
                EmotionType.Calm     =>  0.3f,
                EmotionType.Surprise =>  0.5f,
                EmotionType.Fear     => -0.8f,
                EmotionType.Anger    => -1.3f,
                EmotionType.Sadness  => -0.5f,
                EmotionType.Disgust  => -0.7f,
                _                    =>  0.2f
            };
        }
    }
}