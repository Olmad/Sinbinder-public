// High-level CPU integration against the production MissionBoard,
// real expedition combat and actual save/replay state. Unity scene
// services, the camp spawner and the top-level personality vote are stubbed.
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Sinbinder.Crypt;
using Sinbinder.Gameplay;
using Sinbinder.Inventory;
using Sinbinder.Core;
using SceneObjects=UnityEngine.Object;

static class BoardShimTests
{
    private static int assertions;
    static void Check(bool ok,string reason)
    {
        if(!ok)throw new Exception("BOARD SHIM FAIL: "+reason);
        assertions++;
    }

    static SquadRoster.Member Make(string name)
    {
        return new SquadRoster.Member
        {
            Name=name,WarriorId=name+"-id",IsAway=false,Loyalty=100f,
            HealthFraction=1f,Leadership=100f,
            Sin=SinType.Wrath,Moral=MoralType.Vicious,
            Shell=ShellType.Skeleton
        };
    }
    static void Reset()
    {
        MissionBoard.Forget();
        ExpeditionReplayArchive.Forget();
        SquadRoster.Members.Clear();
        Sinbinder.Core.SaveSystem.Arriving=false;
        Sinbinder.Core.SaveSystem.SaveCount=0;
        Sinbinder.Core.SaveSystem.LastSave=null;
        PersonalExpedition.Active=false;
        PlayerInventory.Instance=new PlayerInventory();
        SceneObjects.SceneWarriors.Clear();
        SceneObjects.CampSpawners.Clear();
        SceneObjects.CampSpawners.Add(new PrologueCampSpawner());
    }
    static Mission Find(string name)
    {
        foreach(var mission in MissionCatalog.All())
            if(mission.Name==name)return mission;
        throw new Exception("Missing test mission "+name);
    }
    static List<SavedAutoExpedition> JsonRoundtrip(List<SavedAutoExpedition> journeys)
    {
        var options=new JsonSerializerOptions{IncludeFields=true};
        var json=JsonSerializer.Serialize(journeys,options);
        return JsonSerializer.Deserialize<List<SavedAutoExpedition>>(json,options);
    }

    static Warrior PlaceWarrior(SquadRoster.Member member, bool active)
    {
        var warrior=new Warrior(member.Name,Team.Player) {Id=member.WarriorId};
        warrior.gameObject.SetActive(active);
        SceneObjects.SceneWarriors.Add(warrior);
        return warrior;
    }

    static void ColdHomecoming(MissionBoard board, Mission mission)
    {
        Reset();
        var missing=Make("Раненый командир");
        missing.Sin=SinType.Greed;
        missing.Loyalty=50f;
        var inactive=Make("Боец в резерве");
        var active=Make("Боец на ногах");
        var fallen=Make("Павший боец");
        inactive.UnpaidMissions=2;
        var members=new[]{missing,inactive,active,fallen};
        foreach(var member in members) member.IsAway=true;
        SquadRoster.Members.AddRange(members);

        var reserve=PlaceWarrior(inactive,false);
        var standing=PlaceWarrior(active,true);
        var casualty=PlaceWarrior(fallen,false);
        var namesake=PlaceWarrior(missing,true);
        namesake.Id="another-warrior-id";
        var wrongScene=PlaceWarrior(missing,true);
        wrongScene.gameObject.scene=1;
        var cryptSpawner=SceneObjects.CampSpawners.Single();
        SceneObjects.CampSpawners.Clear();

        const int droppedCoins=13;
        MissionBoard.RestoreJourneys(JsonRoundtrip(new List<SavedAutoExpedition>
        {
            new SavedAutoExpedition
            {
                MissionName=mission.Name,
                Commander=missing.Name,
                Party=members.Select(member=>member.Name).ToList(),
                Phase=2,
                EncounterResolved=true,
                EncounterFought=true,
                BattleWon=true,
                Survivors=new List<string>{missing.Name,inactive.Name,active.Name},
                SurvivorHealth=new List<ExpeditionSurvivorHealth>
                {
                    new ExpeditionSurvivorHealth {Name=missing.Name,Fraction=0.4f},
                    new ExpeditionSurvivorHealth {Name=inactive.Name,Fraction=0.6f}
                },
                EncounterTrace=new ExpeditionReplayData(),
                TakenCoins=droppedCoins
            }
        }));
        Check(MissionBoard.Journeys.Count==1 && SquadRoster.Away.Count()==members.Length,
            "cold load restores returning party with absent and present scene bodies");
        board.AdvanceJourneys(0.1f,true);
        Check(MissionBoard.Journeys.Count==1 && SquadRoster.Away.Count()==members.Length &&
              !reserve.gameObject.activeSelf && !casualty.gameObject.WasDestroyed &&
              PlayerInventory.Instance.Gold==0 && ExpeditionReplayArchive.History.Count==0,
            "homecoming waits without scene or payout changes when camp spawner is unavailable");

        var otherSpawner=new PrologueCampSpawner();
        otherSpawner.gameObject.scene=1;
        SceneObjects.CampSpawners.Add(otherSpawner);
        board.AdvanceJourneys(0.1f,true);
        Check(MissionBoard.Journeys.Count==1 && PlayerInventory.Instance.Gold==0,
            "a spawner in another loaded scene cannot complete crypt homecoming");

        SceneObjects.CampSpawners.Add(cryptSpawner);
        board.AdvanceJourneys(0.1f,true);
        var present=MissionBoard.Squad().Where(w=>w.gameObject.scene==0).ToArray();
        var restored=present.SingleOrDefault(w=>w.Id==missing.WarriorId);
        Check(restored!=null && Math.Abs(restored.HP/restored.MaxHP-0.4f)<0.001f,
            "return materializes the original missing survivor with saved wounds");
        Check(present.Length==4 && present.Contains(reserve) && present.Contains(standing) &&
              reserve.UnpaidMissions==inactive.UnpaidMissions &&
              Math.Abs(reserve.HP/reserve.MaxHP-0.6f)<0.001f,
            "cold return reuses both present bodies and restores the dormant body state");
        Check(namesake.HP==namesake.MaxHP && namesake.gameObject.activeSelf &&
              !namesake.gameObject.WasDestroyed &&
              wrongScene.HP==wrongScene.MaxHP && wrongScene.gameObject.activeSelf,
            "same-name and wrong-scene actors stay untouched during homecoming");
        Check(casualty.gameObject.WasDestroyed && !SquadRoster.TryGet(fallen.Name,out _) &&
              !SquadRoster.Away.Any(),"homecoming removes the casualty and clears survivor reservations");
        int paid=PlayerInventory.Instance.Gold;
        int kept=restored.PocketGold;
        Check(kept>0 && namesake.PocketGold==0 && wrongScene.PocketGold==0 &&
              kept+paid==droppedCoins+MissionCatalog.Coin(mission.Prize),
            "the restored survivor receives the personal share in the returning scene");
        board.AdvanceJourneys(1f,true);
        Check(MissionBoard.Journeys.Count==0 && PlayerInventory.Instance.Gold==paid &&
              restored.PocketGold==kept &&
              ExpeditionReplayArchive.History.Count==1 &&
              MissionBoard.Squad().Count(w=>w.gameObject.scene==0)==4,
            "retried cold homecoming delivers bodies, loot and replay only once");
    }

    static void RecallWithAndWithoutBody(MissionBoard board, Mission mission)
    {
        Reset();
        var recalled=Make("Отозванный");
        recalled.IsAway=true;
        recalled.HealthFraction=0.75f;
        SquadRoster.Members.Add(recalled);
        var recall=new List<SavedAutoExpedition>
        {
            new SavedAutoExpedition
            {
                MissionName=mission.Name,
                Commander=recalled.Name,
                Party=new List<string>{recalled.Name},
                Phase=2,
                Recalled=true
            }
        };
        MissionBoard.RestoreJourneys(JsonRoundtrip(recall));
        board.AdvanceJourneys(0.1f,true);
        var warrior=MissionBoard.Squad().SingleOrDefault();
        Check(warrior!=null && warrior.Id==recalled.WarriorId &&
              Math.Abs(warrior.HP/warrior.MaxHP-0.75f)<0.001f && !recalled.IsAway,
            "a recalled expedition restores its absent warrior from roster on cold load");
        Check(MissionBoard.Journeys.Count==0 && PlayerInventory.Instance.Gold==0 &&
              ExpeditionReplayArchive.History.Count==1,
            "cold recall preserves single return reporting without loot");

        SceneObjects.CampSpawners.Clear();
        warrior.gameObject.SetActive(false);
        recalled.IsAway=true;
        MissionBoard.RestoreJourneys(JsonRoundtrip(recall));
        board.AdvanceJourneys(0.1f,true);
        Check(MissionBoard.Journeys.Count==0 && MissionBoard.Squad().SingleOrDefault()==warrior &&
              !recalled.IsAway && ExpeditionReplayArchive.History.Count==2,
            "an existing dormant recall body returns in place without a camp spawner");
    }

    public static void Run()
    {
        Reset();
        SquadRoster.Members.AddRange(new [] { Make("Командир"),Make("Боец1"),Make("Боец2") });
        var mission=Find("Придорожная часовня");
        var board=new MissionBoard();
        var sent=board.Send(mission,"Командир",new[]{"Боец1","Боец2"},null);
        Check(sent.Contains("отправлен"),"real MissionBoard.Send marks mission departure");
        Check(MissionBoard.Journeys.Count==1 && SquadRoster.Away.Count()==3,
            "exactly one journey dispatched with three real reserved warriors");
        Check(Sinbinder.Core.SaveSystem.SaveCount>0,
            "outbound stage requests an autosave");

        board.AdvanceJourneys(15f,false);
        var trip=MissionBoard.Journeys[0];
        Check(trip.Phase==3 && trip.BattleCheckpoint!=null &&
              trip.BattleCheckpoint.CompletedRounds==0,
            "arrival starts REAL phase-3 AOS battle without precomputing it");
        Check(trip.EncounterTrace.Frames.Count==0,
            "no invented future battle events at the destination");

        board.AdvanceJourneys(ExpeditionJourneyTiming.BattleRoundSeconds,false);
        trip=MissionBoard.Journeys[0];
        Check(trip.Phase==3 && trip.BattleCheckpoint.CompletedRounds==1,
            "next game-time interval executes ONE genuine AOS round");
        Check(trip.EncounterTrace.Frames.Count>0 &&
              trip.EncounterTrace.Frames.All(f=>f.Round==1),
            "live trace contains only genuinely completed first-round events");
        Check(PlayerInventory.Instance.Gold==0 &&
              ExpeditionReplayArchive.History.Count==0,
            "no loot or invented complete replay before encounter ends");

        var before=JsonRoundtrip(MissionBoard.SnapshotJourneys());
        int oldEvents=before[0].EncounterTrace.Frames.Count;
        string firstGuid=before[0].BattleCheckpoint.Actors[0].Id;
        MissionBoard.RestoreJourneys(before);
        Check(MissionBoard.Journeys.Count==1 &&
              MissionBoard.Journeys[0].BattleCheckpoint.CompletedRounds==1,
            "serial JSON-style load restores one completed battle round");
        Check(MissionBoard.Journeys[0].BattleCheckpoint.Actors[0].Id==firstGuid,
            "load retains original actors instead of creating replacement identities");
        board.AdvanceJourneys(ExpeditionJourneyTiming.BattleRoundSeconds,false);
        trip=MissionBoard.Journeys[0];
        Check(trip.BattleCheckpoint==null || trip.BattleCheckpoint.CompletedRounds==2,
            "first post-load action is round two, never round one again");
        if(trip.BattleCheckpoint!=null)
        {
            Check(trip.EncounterTrace.Frames.Count>oldEvents &&
                trip.EncounterTrace.Frames.Skip(oldEvents).All(e=>e.Round==2),
                "no old events duplicated on second round");
        }
        int safety=0;
        while(MissionBoard.Journeys.Count>0 && MissionBoard.Journeys[0].Phase==3)
        {
            if(++safety>30) throw new Exception("AOS fight stuck");
            board.AdvanceJourneys(ExpeditionJourneyTiming.BattleRoundSeconds,false);
        }
        trip=MissionBoard.Journeys[0];
        Check(trip.Phase==2 && trip.EncounterResolved &&
              trip.BattleCheckpoint==null,
            "final AOS round records a durable result and only then heads home");
        Check(trip.EncounterTrace.Frames.All(f=>f.HasOutcome && f.Round>0),
            "all stored combat events are real executed AOS events");

        board.AdvanceJourneys(ExpeditionJourneyTiming.ReturnSeconds,false);
        Check(MissionBoard.Journeys.Count==1 && PlayerInventory.Instance.Gold==0,
            "remote scene cannot settle the battle or grant loot");
        Check(ExpeditionReplayArchive.History.Count==0,
            "replay archive is not prematurely completed outside crypt");
        var savedReturn=JsonRoundtrip(MissionBoard.SnapshotJourneys());
        MissionBoard.RestoreJourneys(savedReturn);
        board.AdvanceJourneys(0.1f,true);
        Check(MissionBoard.Journeys.Count==0,
            "return in real crypt removes only completed journey");
        Check(ExpeditionReplayArchive.History.Count==1,
            "one completed return adds exactly one full archival replay");
        var returnedCoins=PlayerInventory.Instance.Gold;
        board.AdvanceJourneys(99f,true);
        Check(PlayerInventory.Instance.Gold==returnedCoins &&
              ExpeditionReplayArchive.History.Count==1,
            "subsequent time cannot duplicate expedition loot or replay");
        Check(!SquadRoster.Away.Any(),
            "the roster no longer holds original surviving warriors in flight");

        // Tampered save at a committed battle boundary may never reroll
        // the first AOS round or manufacture a successful payout.
        Reset();
        SquadRoster.Members.AddRange(new [] {Make("Командир"),Make("Боец1"),Make("Боец2")});
        board.Send(mission,"Командир",new[]{"Боец1","Боец2"},null);
        board.AdvanceJourneys(15f,false);
        board.AdvanceJourneys(ExpeditionJourneyTiming.BattleRoundSeconds,false);
        var corrupted=JsonRoundtrip(MissionBoard.SnapshotJourneys());
        corrupted[0].BattleCheckpoint.Actors[0].Id=
            corrupted[0].BattleCheckpoint.Actors[1].Id;
        MissionBoard.RestoreJourneys(corrupted);
        Check(MissionBoard.Journeys.Count==1 && MissionBoard.Journeys[0].CombatBlocked,
            "invalid checkpoint is preserved in blocked state, not discarded");
        int oldRound=MissionBoard.Journeys[0].BattleCheckpoint.CompletedRounds;
        board.AdvanceJourneys(100f,true);
        Check(MissionBoard.Journeys[0].BattleCheckpoint.CompletedRounds==oldRound &&
              PlayerInventory.Instance.Gold==0,
            "invalid state cannot be re-executed or awarded rewards");

        // A real personal map must not be destroyed by an automatic
        // expedition autosave or remote homecoming.
        Reset();
        SquadRoster.Members.Add(Make("Командир"));
        board.Send(Find("Соляной обоз"),"Командир",new string[0],
            Sinbinder.AOS.MissionAction.LetThemPass);
        int autosaves=Sinbinder.Core.SaveSystem.SaveCount;
        PersonalExpedition.Active=true;
        board.AdvanceJourneys(15f,true);
        Check(Sinbinder.Core.SaveSystem.SaveCount==autosaves,
            "personal expedition never forced closed by offscreen autosave");
        PersonalExpedition.Active=false;
        Check(MissionBoard.Journeys.Count==1,"personal map does not delete remote trip");

        // Two separate teams must be able to fight simultaneously, each
        // keeping its own round checkpoint and immutable stream of events.
        Reset();
        SquadRoster.Members.AddRange(new [] {
            Make("A0"),Make("A1"),Make("A2"),
            Make("B0"),Make("B1"),Make("B2")
        });
        board.Send(mission,"A0",new[]{"A1","A2"},null);
        board.Send(Find("Затопленная каменоломня"),
            "B0",new[]{"B1","B2"},null);
        Check(MissionBoard.Journeys.Count==2 &&
              MissionBoard.Journeys[0].JourneyId!=MissionBoard.Journeys[1].JourneyId,
            "parallel trips each have unique durable journey IDs");
        board.AdvanceJourneys(15f,false);
        Check(MissionBoard.Journeys.All(j=>j.Phase==3 &&
              j.BattleCheckpoint.CompletedRounds==0),
            "both teams reach independent genuine AOS combat states");
        board.AdvanceJourneys(ExpeditionJourneyTiming.BattleRoundSeconds,false);
        var independent=JsonRoundtrip(MissionBoard.SnapshotJourneys());
        Check(independent.All(j=>j.BattleCheckpoint.CompletedRounds==1 &&
              j.EncounterTrace.Frames.Count>0),
            "each encounter saves its own first AOS round");
        var savedIds=independent.ToDictionary(j=>j.JourneyId,
            j=>j.BattleCheckpoint.Actors[0].Id);
        MissionBoard.RestoreJourneys(independent);
        Check(MissionBoard.Journeys.Count==2 &&
              MissionBoard.Journeys.All(j=>
                  savedIds[j.JourneyId]==j.BattleCheckpoint.Actors[0].Id),
            "parallel JSON restore keeps stable warrior identities per squad");
        int rounds=0;
        while(MissionBoard.Journeys.Any(j=>j.Phase==3))
        {
            if(++rounds>25)throw new Exception("Parallel AOS encounters stuck");
            board.AdvanceJourneys(ExpeditionJourneyTiming.BattleRoundSeconds,false);
        }
        Check(MissionBoard.Journeys.Count==2 &&
              MissionBoard.Journeys.All(j=>j.Phase==2 && j.EncounterResolved),
            "two independent AOS outcomes completed without losing squads");
        var journeyIds=MissionBoard.Journeys.Select(j=>j.JourneyId).ToList();
        board.AdvanceJourneys(ExpeditionJourneyTiming.ReturnSeconds,false);
        Check(MissionBoard.Journeys.Count==2 &&
              ExpeditionReplayArchive.History.Count==0,
            "neither offscreen squad can settle or create archived loot");
        board.AdvanceJourneys(0.1f,true);
        Check(MissionBoard.Journeys.Count==0 &&
              ExpeditionReplayArchive.History.Count==2,
            "both real returns settled exactly once in crypt");
        Check(journeyIds.Count==2 && !SquadRoster.Away.Any(),
            "parallel parties are not stranded in roster after return");
        int paid=PlayerInventory.Instance.Gold;
        board.AdvanceJourneys(50f,true);
        Check(ExpeditionReplayArchive.History.Count==2 &&
              PlayerInventory.Instance.Gold==paid,
            "two concurrent expeditions never duplicate loot on later frames");

        ColdHomecoming(board,mission);
        RecallWithAndWithoutBody(board,mission);

        Console.WriteLine("PASS: "+assertions+
            " real MissionBoard Unity API shim scenarios, JSON roundtrips and single payout");
    }
}
