using System.Collections.Generic;
using UnityEngine;
using Sinbinder.AOS;

namespace Sinbinder.Core
{
    public class RelationshipSystem
    {
        private MemoryProcessor _memoryProcessor;

        public RelationshipSystem(MemoryProcessor memoryProcessor)
        {
            _memoryProcessor = memoryProcessor;
        }

        public float GetRelationship(string warriorId, string targetId)
        {
            if (_memoryProcessor == null) return 0f;
            float totalScore = 0f;
            var memories = _memoryProcessor.GetMemoriesById(warriorId);
            float currentTime = Time.time;
            foreach (var memory in memories)
            {
                if (memory.TargetID != targetId) continue;
                totalScore += memory.GetCurrentStrength(currentTime);
            }
            return Mathf.Clamp(totalScore, -100f, 100f);
        }

        [System.Obsolete("Use real events instead of direct manipulation.")]
        public void Change(string warriorId, string targetId, float amount)
        {
            if (_memoryProcessor == null) return;
            EmotionType emotion = amount > 0 ? EmotionType.Hope : EmotionType.Anger;
            float importance = Mathf.Abs(amount) / 50f;
            _memoryProcessor.CreateMemoryById(warriorId, "LegacyChange", targetId, emotion, importance);
        }
    }
}