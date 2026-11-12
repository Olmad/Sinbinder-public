// Serializable, round-boundary checkpoint for a real auto-expedition AOS battle.
// A snapshot stores facts observed by the engine, never a predicted final result.
using System;
using System.Collections.Generic;
using Sinbinder.AOS;

namespace Sinbinder.Gameplay
{
    [Serializable]
    public sealed class ExpeditionBattleActor
    {
        public string Name;
        public string Id;
        // Actor came from the original outgoing player party, not merely
        // from the current team (which can change after an AOS bribe).
        public bool OriginalParty;
        public int Team;
        public float HealthFraction;
        public bool Dead;
        // -1 means fled/removed. Keep indices for stable target order.
        public int SquadIndex = -1;
        public int EnemyIndex = -1;
    }

    [Serializable]
    public sealed class ExpeditionBattleCheckpoint
    {
        public int CompletedRounds;
        public List<ExpeditionBattleActor> Actors =
            new List<ExpeditionBattleActor>();
        public BattleRecord Record = new BattleRecord();

        /// <summary>Deep-copy the completed round, including every AOS event.</summary>
        public static ExpeditionBattleCheckpoint Copy(ExpeditionBattleCheckpoint source)
        {
            if(source==null) return null;
            var result=new ExpeditionBattleCheckpoint
            {
                CompletedRounds=source.CompletedRounds,
                Actors=new List<ExpeditionBattleActor>(),
                Record=new BattleRecord()
            };
            if(source.Actors!=null)
                foreach(var a in source.Actors)
                    result.Actors.Add(a==null ? null : new ExpeditionBattleActor
                    {
                        Name=a.Name,Id=a.Id,OriginalParty=a.OriginalParty,
                        Team=a.Team,HealthFraction=a.HealthFraction,
                        Dead=a.Dead,SquadIndex=a.SquadIndex,EnemyIndex=a.EnemyIndex
                    });
            if(source.Record!=null)
            {
                result.Record.Winner=source.Record.Winner;
                result.Record.TotalTurns=source.Record.TotalTurns;
                if(source.Record.Events!=null)
                    foreach(var ev in source.Record.Events)
                        result.Record.Events.Add(ev==null ? null : new BattleEvent
                        {
                            ActorId=ev.ActorId,ActorName=ev.ActorName,
                            ActorGender=ev.ActorGender,Action=ev.Action,
                            TargetId=ev.TargetId,TargetName=ev.TargetName,
                            DamageDealt=ev.DamageDealt,HealDealt=ev.HealDealt,
                            Resolved=ev.Resolved,TargetKilled=ev.TargetKilled,
                            TargetHealthAfter=ev.TargetHealthAfter,Round=ev.Round
                        });
            }
            return result;
        }

        public static bool IsValid(ExpeditionBattleCheckpoint state,
                                  int originalPartyCount, int initialEnemyCount,
                                  int maxRounds)
        {
            if(state==null || state.Actors==null || state.Record==null ||
               state.Record.Events==null ||
               originalPartyCount<=0 || initialEnemyCount<=0 ||
               state.CompletedRounds<0 || state.CompletedRounds>maxRounds ||
               state.Actors.Count!=originalPartyCount+initialEnemyCount)
                return false;

            var ids=new HashSet<string>();
            var squadSlots=new HashSet<int>();
            var enemySlots=new HashSet<int>();
            int countedParty=0;
            foreach(var actor in state.Actors)
            {
                if(actor==null || string.IsNullOrEmpty(actor.Id) ||
                   string.IsNullOrEmpty(actor.Name) || !ids.Add(actor.Id) ||
                   float.IsNaN(actor.HealthFraction) ||
                   float.IsInfinity(actor.HealthFraction) ||
                   actor.HealthFraction<0f || actor.HealthFraction>1f ||
                   (!actor.Dead && actor.HealthFraction<=0f) ||
                   (actor.Dead && actor.HealthFraction!=0f) ||
                   actor.Team<0 || actor.Team>2 ||
                   actor.SquadIndex< -1 || actor.EnemyIndex< -1 ||
                   (actor.SquadIndex>=0 && actor.EnemyIndex>=0))
                    return false;
                if(actor.OriginalParty) countedParty++;
                if(actor.SquadIndex>=0 && !squadSlots.Add(actor.SquadIndex))
                    return false;
                if(actor.EnemyIndex>=0 && !enemySlots.Add(actor.EnemyIndex))
                    return false;
            }
            if(countedParty!=originalPartyCount) return false;
            // Ordering should not have holes; corrupted indices can never
            // materialise a participant twice or teleport them to a side.
            for(int i=0;i<squadSlots.Count;i++)
                if(!squadSlots.Contains(i)) return false;
            for(int i=0;i<enemySlots.Count;i++)
                if(!enemySlots.Contains(i)) return false;

            int lastRound=0;
            foreach(var ev in state.Record.Events)
            {
                if(ev==null || ev.Round<1 ||
                   ev.Round>state.CompletedRounds ||
                   ev.Round<lastRound) return false;
                lastRound=ev.Round;
            }
            return true;
        }
    }
}
