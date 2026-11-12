// Tests the real MissionBoard AND real thirteen-module BehaviourResolver
// in one game-state loop, with Unity services shimmed out.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Sinbinder.AOS;
using Sinbinder.Core;
using Sinbinder.Gameplay;
using Sinbinder.Crypt;
using Sinbinder.Inventory;

static class RealBoardShimTests
{
    static int assertions;
    static void Check(bool ok,string message)
    {
        if(!ok)throw new Exception("REAL BOARD AOS SHIM FAIL: "+message);
        assertions++;
    }
    static SquadRoster.Member Member(string name,SinType sin,float intensity)
    {
        return new SquadRoster.Member
        {
            Name=name,Sin=sin,Intensity=intensity,
            Moral=MoralType.Vicious,
            Shell=ShellType.Skeleton,
            Loyalty=60f,Leadership=100f,HealthFraction=1f
        };
    }
    static List<SavedAutoExpedition> CopyDisk()
    {
        var opt=new JsonSerializerOptions{IncludeFields=true};
        return JsonSerializer.Deserialize<List<SavedAutoExpedition>>(
            JsonSerializer.Serialize(MissionBoard.SnapshotJourneys(),opt),opt);
    }
    static Mission Mission(string name)
        => MissionCatalog.All().First(m=>m.Name==name);

    public static void Run()
    {
        MissionBoard.Forget();
        ExpeditionReplayArchive.Forget();
        SquadRoster.Members.Clear();
        PlayerInventory.Instance=new PlayerInventory();
        PersonalExpedition.Active=false;
        Sinbinder.Core.SaveSystem.SaveCount=0;

        // These are the actual strategy modifiers read by the same
        // production AOS resolver, not a hand-written sample bonus list.
        Check(Sinbinder.Gameplay.SquadOrders.FromSin(SinType.Wrath)
                ==SquadStrategy.Aggressive,
            "real Wrath commander selects Aggressive strategy");
        Check(Sinbinder.Gameplay.SquadOrders.FromSin(SinType.Greed)
                ==SquadStrategy.LootFocused,
            "real Greed commander selects LootFocused strategy");
        Sinbinder.Gameplay.SquadOrders.Set(SquadStrategy.Aggressive);
        Check(Sinbinder.Gameplay.SquadOrders.CurrentModifiers().Any(m=>
                m.Action==ActionType.Attack && m.Bonus>0f),
            "real aggressive strategy raises AOS attack score");
        Sinbinder.Gameplay.SquadOrders.Reset();
        var resolver=new BehaviourResolver();
        var warrior=new Warrior("Гневный",Team.Player);
        warrior.Soul.Sin=SinType.Wrath;
        warrior.Soul.Spectra[(int)SinType.Wrath]=90f;
        warrior.Soul.Spectra[(int)SinType.Greed]=60f;
        var context=new DecisionContext
        {
            CurrentHP=15f,MaxHP=15f,NearbyEnemies=2,EnemiesInSight=2,
            NearbyLoot=1,DangerLevel=0.5f,
            RecentMemories=new List<MemoryRecord>()
        };
        var ballots=resolver.Ballot(warrior,context);
        Check(ballots.Select(v=>v.Module).Distinct().Count()==13,
            "actual 13 personality modules vote on real battle context");
        Check(ballots.Any(v=>v.Module=="Wrath" && v.Action==ActionType.Attack &&
            v.Voice>0f),"actual Wrath modifies attack score");
        var decision=resolver.DecideDetailed(warrior,context);
        Check(Enum.IsDefined(typeof(ActionType),decision.Action),
            "actual mission warrior receives real AOS decision");

        SquadRoster.Members.AddRange(new[]{
            Member("Греховод1",SinType.Wrath,85f),
            Member("Помощник1",SinType.Greed,80f)
        });
        var board=new MissionBoard();
        string dispatched=board.Send(Mission("Придорожная часовня"),"Греховод1",
            new[]{"Помощник1"},null);
        Check(dispatched.Contains("отправлен") && SquadRoster.Away.Count()==2,
            "real MissionBoard sends a two-member squad");
        board.AdvanceJourneys(ExpeditionJourneyTiming.OutboundSeconds,false);
        var trip=MissionBoard.Journeys.Single();
        Check(trip.Phase==3 && trip.BattleCheckpoint!=null &&
              trip.BattleCheckpoint.CompletedRounds==0,
            "real AOS encounter begins without precalculated rounds");
        board.AdvanceJourneys(ExpeditionJourneyTiming.BattleRoundSeconds,false);
        trip=MissionBoard.Journeys.Single();
        Check(trip.EncounterTrace!=null && trip.EncounterTrace.Frames.Count>0,
            "genuine module-led AOS choices produce logged real actions");
        Check(trip.BattleCheckpoint==null ||
              trip.BattleCheckpoint.CompletedRounds==1,
            "only first real AOS round can occur in first interval");

        var saved=CopyDisk();
        int alreadyRecorded=saved[0].EncounterTrace.Frames.Count;
        MissionBoard.RestoreJourneys(saved);
        Check(MissionBoard.Journeys.Single().EncounterTrace.Frames.Count==alreadyRecorded,
            "serializing one round preserves exact real AOS log");
        int time=0;
        while(MissionBoard.Journeys.Single().Phase==3)
        {
            if(++time>25)throw new Exception("Real AOS fight exceeded maximum rounds");
            board.AdvanceJourneys(ExpeditionJourneyTiming.BattleRoundSeconds,false);
            Check(!MissionBoard.Journeys.Single().CombatBlocked,
                "real AOS round checkpoint must remain valid across reload");
        }
        trip=MissionBoard.Journeys.Single();
        Check(trip.Phase==2 && trip.EncounterResolved &&
              trip.BattleCheckpoint==null,
            "real thirteen-module AOS fight ends in a durable world result");
        Check(trip.EncounterTrace.Frames.All(f=>f.HasOutcome &&
              f.Round>0 && f.Round<=AutoBattleResolver.MaxTurns),
            "stored AOS replay contains actual decisions and legal turns");
        Check(trip.EncounterTrace.Frames.Count>=alreadyRecorded,
            "restored fight never removes earlier recorded real actions");

        // Real AOS cannot settle remotely, and cannot award a second
        // time even if the game clock continues after returning.
        board.AdvanceJourneys(ExpeditionJourneyTiming.ReturnSeconds,false);
        Check(ExpeditionReplayArchive.History.Count==0,
            "no physical return or loot from real AOS in unrelated scene");
        var returning=CopyDisk();
        MissionBoard.RestoreJourneys(returning);
        board.AdvanceJourneys(0.1f,true);
        Check(MissionBoard.Journeys.Count==0 &&
              ExpeditionReplayArchive.History.Count==1,
            "real AOS return commits once from saved outcome");
        int money=PlayerInventory.Instance.Gold;
        board.AdvanceJourneys(999f,true);
        Check(PlayerInventory.Instance.Gold==money &&
              ExpeditionReplayArchive.History.Count==1,
            "no double reward or replay after real AOS conclusion");

        // Junction decisions now run the unmodified real mission vote,
        // including morality/loyalty/memory. The player may advise, not
        // guarantee the commander's final choice.
        MissionBoard.Forget();
        SquadRoster.Members.Clear();
        SquadRoster.Members.Add(Member("Командир",SinType.Greed,90f));
        // The real expedition ledger, rather than a fake history provider,
        // must contribute a bounded Memory vote at precisely this site.
        ExpeditionMemoryLedger.Remember("past1","Командир","Соляной обоз",
            "ExpeditionSparedCaravan",EmotionType.Calm,1f);
        ExpeditionMemoryLedger.Remember("past1","Командир","Соляной обоз",
            "ExpeditionSparedCaravan",EmotionType.Calm,1f);
        Check(ExpeditionMemoryLedger.ForWarrior("Командир","Соляной обоз").Count==1,
            "real memory ledger deduplicates previously observed decisions");
        Check(ExpeditionMemoryLedger.ForWarrior("Командир","Часовня").Count==0,
            "real memory does not leak experience across different mission sites");
        var past=ExpeditionMemoryLedger.Snapshot();
        ExpeditionMemoryLedger.Restore(past);
        Check(ExpeditionMemoryLedger.ForWarrior("Командир","Соляной обоз").Count==1,
            "saved and restored real expedition memories influence AOS once");
        var memorySoul=new Soul
        {
            Spectra=new float[7]
        };
        var memoryContext=new MissionContext
        {
            ExpeditionSite="Соляной обоз",
            RecentMemories=ExpeditionMemoryLedger.ForWarrior("Командир","Соляной обоз")
        };
        Check(new Sinbinder.AOS.Modules.MemoryModule().EvaluateMission(
                memorySoul,memoryContext,MissionAction.LetThemPass)>0f,
            "real thirteen-module voting receives site-scoped expedition memory");
        board.Send(Mission("Соляной обоз"),"Командир",
            new string[0],null);
        board.AdvanceJourneys(ExpeditionJourneyTiming.OutboundSeconds,false);
        Check(MissionBoard.Journeys.Single().Phase==1,
            "real caravan junction offers advice before the genuine vote");
        board.AdvanceJourneys(ExpeditionJourneyTiming.AdviceSeconds,false);
        Check(MissionBoard.Journeys.Single().Phase==2 ||
              MissionBoard.Journeys.Single().Phase==3,
            "real commander autonomously chose and advanced rather than hanging");
        Check(MissionBoard.Journeys.Single().EncounterResolved ||
              MissionBoard.Journeys.Single().BattleCheckpoint!=null,
            "actual mission vote either avoided battle or started real combat");
        // Two simultaneous battles with REAL strategies and thirteen
        // modules must not leak a commander's temporary strategy into the
        // second crew, nor duplicate homecoming rewards after JSON reload.
        MissionBoard.Forget();
        ExpeditionReplayArchive.Forget();
        SquadRoster.Members.Clear();
        PlayerInventory.Instance=new PlayerInventory();
        SquadRoster.Members.AddRange(new[]{
            Member("Командир А",SinType.Wrath,95f),
            Member("Воин А",SinType.Wrath,70f),
            Member("Командир Б",SinType.Greed,95f),
            Member("Воин Б",SinType.Greed,70f)
        });
        board.Send(Mission("Придорожная часовня"),
            "Командир А",new[]{"Воин А"},null);
        board.Send(Mission("Затопленная каменоломня"),
            "Командир Б",new[]{"Воин Б"},null);
        Check(MissionBoard.Journeys.Count==2 &&
              MissionBoard.Journeys[0].JourneyId!=MissionBoard.Journeys[1].JourneyId,
            "two real-AOS expeditions have independent journey identities");
        board.AdvanceJourneys(ExpeditionJourneyTiming.OutboundSeconds,false);
        Check(MissionBoard.Journeys.All(j=>j.Phase==3 &&
              j.BattleCheckpoint.CompletedRounds==0),
            "parallel real AOS battles start independently with no precomputed turns");
        board.AdvanceJourneys(ExpeditionJourneyTiming.BattleRoundSeconds,false);
        Check(Sinbinder.Gameplay.SquadOrders.Current==SquadStrategy.Balanced,
            "global tactical strategy restored after interleaved real AOS rounds");
        var bothSaved=CopyDisk();
        Check(bothSaved.All(j=>j.EncounterTrace.Frames.Count>0 &&
              j.BattleCheckpoint.CompletedRounds==1),
            "each real AOS squad has its own first-round battle history");
        MissionBoard.RestoreJourneys(bothSaved);
        int parallelSteps=0;
        while(MissionBoard.Journeys.Any(j=>j.Phase==3))
        {
            if(++parallelSteps>25) throw new Exception("Parallel real AOS stalled");
            board.AdvanceJourneys(ExpeditionJourneyTiming.BattleRoundSeconds,false);
        }
        Check(MissionBoard.Journeys.All(j=>j.Phase==2 && j.EncounterResolved),
            "both real module-led AOS encounters complete after round restore");
        board.AdvanceJourneys(ExpeditionJourneyTiming.ReturnSeconds,false);
        Check(ExpeditionReplayArchive.History.Count==0,
            "parallel squads do not receive loot or replay while player is away");
        board.AdvanceJourneys(0.1f,true);
        Check(MissionBoard.Journeys.Count==0 &&
              ExpeditionReplayArchive.History.Count==2,
            "two actual AOS encounters return and archive exactly once");
        int finalPayout=PlayerInventory.Instance.Gold;
        board.AdvanceJourneys(999f,true);
        Check(PlayerInventory.Instance.Gold==finalPayout &&
              ExpeditionReplayArchive.History.Count==2,
            "subsequent game ticks cannot duplicate parallel real-AOS rewards");

        Console.WriteLine("PASS: "+assertions+
            " combined actual MissionBoard and 13-module real AOS shim assertions");
    }
}
