// Pure, bounded AOS Memory-module vote from verified expedition history.
// One influence among the normal 13 AOS personality modules, never a forced action.
using System;
using System.Collections.Generic;
using Sinbinder.AOS;

namespace Sinbinder.Gameplay
{
    public static class ExpeditionMemoryVote
    {
        public static float Score(IReadOnlyList<MemoryRecord> memories,
            string mission, MissionAction action, float wrath)
        {
            if(memories==null || string.IsNullOrEmpty(mission)) return 0f;
            float score=0f;
            foreach(var memory in memories)
            {
                if(memory==null || memory.TargetID!=mission) continue;
                float importance=Math.Max(0f,Math.Min(1f,memory.Importance));
                switch(memory.EventType)
                {
                    case "ExpeditionSparedCaravan":
                        if(action==MissionAction.LetThemPass) score+=7f*importance;
                        break;
                    case "ExpeditionTookGoods":
                        if(action==MissionAction.TakeGoodsSparePeople) score+=7f*importance;
                        break;
                    case "ExpeditionRaidedCaravan":
                        if(action==MissionAction.TakeEverything) score+=7f*importance;
                        break;
                    case "ExpeditionCaptureAttempt":
                        if(action==MissionAction.TakePeople) score+=4f*importance;
                        break;
                    case "ExpeditionRepelled":
                    case "ExpeditionLostCompanion":
                        float caution=Math.Max(0.5f,Math.Min(1.5f,1f-wrath/200f));
                        if(action==MissionAction.LetThemPass)
                            score+=4f*importance*caution;
                        if(action==MissionAction.TakeEverything ||
                           action==MissionAction.TakePeople)
                            score-=5f*importance*caution;
                        break;
                }
            }
            return Math.Max(-12f,Math.Min(12f,score));
        }
    }
}
