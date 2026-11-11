using System.Collections.Generic;
namespace Sinbinder.Gameplay
{
    /// <summary>Bounded history of authentic auto-expedition event traces.</summary>
    public static class ExpeditionReplayArchive
    {
        public const int Capacity = 8;
        private static readonly List<ExpeditionReplayData> Entries = new List<ExpeditionReplayData>();
        public static IReadOnlyList<ExpeditionReplayData> History => Entries;
        public static void Add(ExpeditionReplayData replay)
        {
            if(replay==null || replay.Frames==null || !replay.HasCompleteRoute) return;
            Entries.Add(replay);
            if(Entries.Count>Capacity) Entries.RemoveAt(0);
        }
        public static List<ExpeditionReplayData> Snapshot()
            => new List<ExpeditionReplayData>(Entries);
        public static void Restore(List<ExpeditionReplayData> previous)
        {
            Entries.Clear();
            if(previous==null) return;
            foreach(var replay in previous) Add(replay);
        }
        public static void Forget() => Entries.Clear();
    }
}
