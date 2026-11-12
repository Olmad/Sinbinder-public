using System.Collections.Generic;
namespace Sinbinder.Gameplay
{
    /// <summary>Bounded history of authentic auto-expedition event traces.</summary>
    public static class ExpeditionReplayArchive
    {
        public const int Capacity = 8;
        private static readonly List<ExpeditionReplayData> Entries = new List<ExpeditionReplayData>();
        public static IReadOnlyList<ExpeditionReplayData> History => Entries;

        // Snapshots must not share mutable frame/squad collections with a live
        // replay viewer or with an older save. Otherwise changing a displayed
        // replay can silently rewrite the data that gets saved next time.
        private static ExpeditionReplayData Copy(ExpeditionReplayData source)
        {
            if (source == null || source.Frames == null || !source.HasCompleteRoute)
                return null;
            var copy = new ExpeditionReplayData
            {
                MissionName = source.MissionName ?? "",
                Leader = source.Leader ?? "",
                Squad = source.Squad == null
                    ? new List<string>() : new List<string>(source.Squad),
                Frames = new List<ExpeditionReplayFrame>(source.Frames.Count)
            };
            foreach (var frame in source.Frames)
            {
                if (frame == null) return null; // reject corrupted saves
                copy.Frames.Add(new ExpeditionReplayFrame
                {
                    Kind = frame.Kind,
                    Actor = frame.Actor ?? "",
                    Target = frame.Target ?? "",
                    Description = frame.Description ?? "",
                    Damage = frame.Damage,
                    Healing = frame.Healing,
                    Sequence = frame.Sequence,
                    Round = frame.Round
                });
            }
            return copy;
        }

        public static void Add(ExpeditionReplayData replay)
        {
            var stable = Copy(replay);
            if (stable == null) return;
            Entries.Add(stable);
            if (Entries.Count > Capacity) Entries.RemoveAt(0);
        }

        public static List<ExpeditionReplayData> Snapshot()
        {
            var snapshot = new List<ExpeditionReplayData>(Entries.Count);
            foreach (var entry in Entries)
            {
                var stable = Copy(entry);
                if (stable != null) snapshot.Add(stable);
            }
            return snapshot;
        }

        public static void Restore(List<ExpeditionReplayData> previous)
        {
            Entries.Clear();
            if (previous == null) return;
            foreach (var replay in previous) Add(replay);
        }
        public static void Forget() => Entries.Clear();
    }
}
