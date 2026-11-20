// Stable event history of expedition decisions and their observed outcomes.
// Does not invent physical objects or memories for dead/unseen participants.
using System;
using System.Collections.Generic;
using Sinbinder.AOS;

namespace Sinbinder.Gameplay
{
    [Serializable]
    public sealed class ExpeditionMemoryEntry
    {
        public string JourneyId;
        public string Warrior;
        public string Mission;
        public string EventType;
        public EmotionType Emotion;
        public float Importance;
    }

    /// <summary>
    /// A soul's expedition testimony survives destruction of transient
    /// simulation GameObjects and save/load. Name is the roster's existing
    /// stable key until souls gain persistent IDs throughout the game.
    /// Entries are not synthesized from failed requests or unobserved scenes.
    /// </summary>
    public static class ExpeditionMemoryLedger
    {
        public const int MaxPerWarrior = 16;
        private static readonly List<ExpeditionMemoryEntry> Entries =
            new List<ExpeditionMemoryEntry>();

        private static ExpeditionMemoryEntry Copy(ExpeditionMemoryEntry e) =>
            new ExpeditionMemoryEntry
            {
                JourneyId=e.JourneyId, Warrior=e.Warrior, Mission=e.Mission,
                EventType=e.EventType, Emotion=e.Emotion, Importance=e.Importance
            };

        public static void Remember(string journeyId, string warrior,
            string mission, string eventType, EmotionType emotion, float importance)
        {
            if (string.IsNullOrWhiteSpace(journeyId) ||
                string.IsNullOrWhiteSpace(warrior) ||
                string.IsNullOrWhiteSpace(mission) ||
                string.IsNullOrWhiteSpace(eventType) ||
                float.IsNaN(importance) || float.IsInfinity(importance) ||
                importance <= 0f) return;

            foreach(var e in Entries)
                if(e.JourneyId==journeyId && e.Warrior==warrior &&
                   e.EventType==eventType) return;

            Entries.Add(new ExpeditionMemoryEntry
            {
                JourneyId=journeyId, Warrior=warrior, Mission=mission,
                EventType=eventType, Emotion=emotion,
                Importance=Math.Min(1f, importance)
            });

            int seen=0;
            for(int i=Entries.Count-1;i>=0;i--)
            {
                if(Entries[i].Warrior!=warrior) continue;
                if(++seen>MaxPerWarrior) Entries.RemoveAt(i);
            }
        }

        public static List<ExpeditionMemoryEntry> Snapshot()
        {
            var result=new List<ExpeditionMemoryEntry>(Entries.Count);
            foreach(var e in Entries) result.Add(Copy(e));
            return result;
        }

        public static void Restore(List<ExpeditionMemoryEntry> saved)
        {
            Entries.Clear();
            if(saved==null) return;
            foreach(var e in saved)
                if(e!=null)
                    Remember(e.JourneyId,e.Warrior,e.Mission,
                        e.EventType,e.Emotion,e.Importance);
        }

        /// <summary>
        /// AOS receives the same MemoryRecord shape as ordinary memories.
        /// Expedition memory is durable testimony, without a fabricated
        /// creation time: decay is zero until a gameplay calendar exists.
        /// </summary>
        public static List<MemoryRecord> ForWarrior(string warrior, string mission)
        {
            var result=new List<MemoryRecord>();
            if(string.IsNullOrWhiteSpace(warrior) ||
               string.IsNullOrWhiteSpace(mission)) return result;
            foreach(var e in Entries)
                if(e.Warrior==warrior && e.Mission==mission)
                    result.Add(new MemoryRecord(e.Mission,e.EventType,
                        e.Emotion,e.Importance,0f));
            return result;
        }

        public static void Forget() => Entries.Clear();
    }
}
