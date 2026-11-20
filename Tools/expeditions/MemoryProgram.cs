using System;
using System.Collections.Generic;
using Sinbinder.Gameplay;
using Sinbinder.AOS;

// Minimal shape-only AOS types to compile the production ledger and influence
// without pretending that this .NET test runs the whole Unity AOS.
namespace Sinbinder.AOS
{
    public enum EmotionType { Joy, Fear, Anger, Sadness, Disgust, Calm }
    public enum MissionAction
    {
        LetThemPass, TakeGoodsSparePeople, TakeEverything, TakePeople
    }
    public sealed class MemoryRecord
    {
        public string TargetID;
        public string EventType;
        public EmotionType Emotion;
        public float Importance;
        public float DecayRate;
        public MemoryRecord(string target,string eventName,EmotionType emotion,
            float importance,float decayRate=0.01f)
        {
            TargetID=target; EventType=eventName; Emotion=emotion;
            Importance=importance; DecayRate=decayRate;
        }
    }
}
internal static class MemoryChecks
{
    private static int checks;
    private static void Check(bool condition,string message)
    {
        if(!condition) throw new Exception(message);
        checks++;
    }
    private static void Main()
    {
        ExpeditionMemoryLedger.Forget();
        const string site="Соляной обоз";
        Check(ExpeditionMemoryLedger.ForWarrior("Alice",site).Count==0,
            "new game must have no expedition memories");
        ExpeditionMemoryLedger.Remember("j1","Alice",site,
            "ExpeditionSparedCaravan",EmotionType.Joy,1f);
        ExpeditionMemoryLedger.Remember("j1","Alice",site,
            "ExpeditionSparedCaravan",EmotionType.Joy,1f);
        ExpeditionMemoryLedger.Remember("j1","Bob",site,
            "ExpeditionRaidedCaravan",EmotionType.Disgust,1f);
        Check(ExpeditionMemoryLedger.ForWarrior("Alice",site).Count==1,
            "same witnessed event is not remembered twice");
        Check(ExpeditionMemoryLedger.ForWarrior("Bob",site).Count==1,
            "different warrior keeps own memory");
        Check(ExpeditionMemoryLedger.ForWarrior("Alice","Часовня").Count==0,
            "unvisited mission does not import memories from other place");
        var alice=ExpeditionMemoryLedger.ForWarrior("Alice",site);
        Check(alice[0].DecayRate==0f,
            "durable testimony must not pretend to have a lifetime timestamp");
        Check(ExpeditionMemoryVote.Score(alice,site,MissionAction.LetThemPass,0f)>0f,
            "spared caravan encourages repeated mercy");
        Check(ExpeditionMemoryVote.Score(alice,site,MissionAction.TakeEverything,0f)==0f,
            "mercy history does not force a contradictory slaughter");
        Check(ExpeditionMemoryVote.Score(alice,"Часовня",MissionAction.LetThemPass,0f)==0f,
            "mission-site isolation holds during AOS vote");
        var bob=ExpeditionMemoryLedger.ForWarrior("Bob",site);
        Check(ExpeditionMemoryVote.Score(bob,site,MissionAction.TakeEverything,0f)>0f,
            "a different commander receives different AOS memory arguments");

        var before=ExpeditionMemoryLedger.Snapshot();
        before[0].Warrior="HACKED";
        Check(ExpeditionMemoryLedger.ForWarrior("Alice",site).Count==1,
            "editing a save snapshot never changes live ledger");
        ExpeditionMemoryLedger.Restore(ExpeditionMemoryLedger.Snapshot());
        Check(ExpeditionMemoryLedger.ForWarrior("Alice",site).Count==1,
            "save/load preserves authentic memory");
        ExpeditionMemoryLedger.Remember("j2","Alice",site,
            "ExpeditionRepelled",EmotionType.Fear,1f);
        Check(ExpeditionMemoryVote.Score(ExpeditionMemoryLedger.ForWarrior("Alice",site),
            site,MissionAction.TakeEverything,0f)<0f,
            "confirmed defeat discourages violence");
        Check(ExpeditionMemoryVote.Score(ExpeditionMemoryLedger.ForWarrior("Alice",site),
            site,MissionAction.LetThemPass,0f)>
            ExpeditionMemoryVote.Score(alice,site,MissionAction.LetThemPass,0f),
            "observed defeat changes next AOS choice without forcing it");

        for(int i=0;i<100;i++)
            ExpeditionMemoryLedger.Remember("new"+i,"Alice",site,
                "ExpeditionLostCompanion",EmotionType.Sadness,0.9f);
        Check(ExpeditionMemoryLedger.ForWarrior("Alice",site).Count==
            ExpeditionMemoryLedger.MaxPerWarrior,"per-soul ledger is bounded");
        Check(ExpeditionMemoryVote.Score(ExpeditionMemoryLedger.ForWarrior("Alice",site),
            site,MissionAction.LetThemPass,0f)<=12f,
            "memory voice is bounded and cannot replace AOS");
        var saved=ExpeditionMemoryLedger.Snapshot();
        ExpeditionMemoryLedger.Forget();
        Check(ExpeditionMemoryLedger.Snapshot().Count==0,
            "new game clears memories");
        ExpeditionMemoryLedger.Restore(saved);
        Check(ExpeditionMemoryLedger.ForWarrior("Alice",site).Count==16,
            "saved memory cap survives reload");
        ExpeditionMemoryLedger.Restore(null);
        Check(ExpeditionMemoryLedger.Snapshot().Count==0,
            "older saves without history remain valid");
        ExpeditionMemoryLedger.Remember("bad","",site,"ExpeditionRaidedCaravan",
            EmotionType.Joy,1f);
        Check(ExpeditionMemoryLedger.Snapshot().Count==0,
            "invalid identity cannot inject fabricated memory");
        Console.WriteLine($"PASS: {checks} expedition memory persistence and AOS-influence checks");
    }
}
