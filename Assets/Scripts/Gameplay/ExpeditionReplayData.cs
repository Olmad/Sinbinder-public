// Lossless chronological expedition trace for automatic missions.
// Data is intentionally independent from the small three-line retelling.
using System;
using System.Collections.Generic;

namespace Sinbinder.Gameplay
{
    public enum ExpeditionReplayKind
    {
        Departure,
        March,
        CommandDecision,
        CombatAction,
        Loot,
        Return
    }

    [Serializable]
    public sealed class ExpeditionReplayFrame
    {
        public ExpeditionReplayKind Kind;
        public string Actor;
        public string Target;
        public string Description;
        public float Damage;
        public float Healing;
        public int Sequence;
        public int Round; // 0 outside combat or from old save
        // Additional factual AOS outcome fields. Old saves have
        // HasOutcome=false, so absent booleans are never treated as failure.
        public bool HasOutcome;
        public bool Resolved;
        public bool TargetKilled;
        public string TargetName;
        public float TargetHealthAfter;
    }

    [Serializable]
    public sealed class ExpeditionReplayData
    {
        public string MissionName;
        public string Leader;
        public List<string> Squad = new List<string>();
        public List<ExpeditionReplayFrame> Frames =
            new List<ExpeditionReplayFrame>();

        public ExpeditionReplayData() { } // Unity JsonUtility deserialization

        public ExpeditionReplayData(string missionName, string leader,
                                    IEnumerable<string> squad)
        {
            MissionName=missionName ?? "";
            Leader=leader ?? "";
            if(squad != null)
                foreach(var name in squad)
                    if(!string.IsNullOrWhiteSpace(name) && !Squad.Contains(name))
                        Squad.Add(name);
        }

        public void Append(ExpeditionReplayKind kind, string actor,
                           string description, string target = "",
                           float damage = 0f, float healing = 0f, int round = 0,
                           string targetName = "", bool hasOutcome = false,
                           bool resolved = false, bool targetKilled = false,
                           float targetHealthAfter = 0f)
        {
            Frames.Add(new ExpeditionReplayFrame
            {
                Kind=kind,Actor=actor ?? "",Target=target ?? "",
                Description=description ?? "",Damage=damage,Healing=healing,
                Sequence=Frames.Count, Round=round,
                TargetName=targetName ?? "",HasOutcome=hasOutcome,
                Resolved=resolved,TargetKilled=targetKilled,
                TargetHealthAfter=targetHealthAfter
            });
        }

        // Unlike Retelling, no duplicate filter or three-entry cap.
        public IEnumerable<ExpeditionReplayFrame> AllEvents()
        {
            foreach(var frame in Frames) yield return frame;
        }

        public bool HasCompleteRoute =>
            Frames != null && Frames.Count >= 3 &&
            Frames[0].Kind == ExpeditionReplayKind.Departure &&
            Frames[Frames.Count - 1].Kind == ExpeditionReplayKind.Return;
    }
}
