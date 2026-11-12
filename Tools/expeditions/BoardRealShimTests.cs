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
        Core.SaveSystem.SaveCount=0;

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
        Console.WriteLine("PASS: "+assertions+
            " combined actual MissionBoard and 13-module real AOS shim assertions");
    }
}
