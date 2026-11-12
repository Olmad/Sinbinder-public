// Standalone compile-and-run integration harness. Production
// AutoBattleResolver.cs, BattleRecord.cs and ExpeditionCombatFacts.cs
// compile unchanged; only Unity/decision inputs are minimal fakes.
using System;
using System.Collections.Generic;
using System.Linq;
using Sinbinder.AOS;
using Sinbinder.Gameplay;

namespace UnityEngine
{
    public static class Mathf
    {
        public static int Max(int a,int b) => Math.Max(a,b);
        public static float Max(float a,float b) => Math.Max(a,b);
        public static int RoundToInt(float v) => (int)MathF.Round(v);
    }
}
namespace Sinbinder.Core
{
    public enum Gender { Male, Female }
    public enum SinType { Greed }
    public sealed class SoulData
    {
        public int Level = 1;
        public float Greed;
        public float Get(SinType type) => Greed;
    }
}
namespace Sinbinder.Gameplay
{
    public enum Team { Player, Enemy }
    public sealed class Warrior
    {
        public string Id;
        public string DisplayName;
        public Core.Gender Gender;
        public Team Team;
        public Core.SoulData Soul;
        public float HP, MaxHP, Attack, Loyalty;
        public ActionType PreferredAction = ActionType.Attack;
        public bool ThrowDecision;
        public bool IsDead => HP<=0f;
        public Warrior(string name, Team team, float health=15f,float damage=5f)
        {
            Id=name+"-id";DisplayName=name;Team=team;
            MaxHP=health;HP=health;Attack=damage;
            Soul=new Core.SoulData();
        }
        public void TakeDamage(float damage)
        {
            HP=Math.Max(0f,HP-Math.Max(0f,damage));
        }
        public void Heal(float amount) { HP=Math.Min(MaxHP,HP+amount); }
    }
}
namespace Sinbinder.AOS
{
    public enum ActionType { Attack, SaveAlly, BribeEnemy, Flee, Loot, Idle }
    public enum SquadStrategy { Balanced, Aggressive, Defensive }
    public static class SquadOrders
    {
        public static SquadStrategy Current {get; private set;} = SquadStrategy.Defensive;
        public static void Set(SquadStrategy strategy) => Current=strategy;
    }
    public sealed class DecisionContext
    {
        public Warrior Actor;
    }
    public static class AutoBattleContext
    {
        public static DecisionContext Create(Warrior warrior,List<Warrior> allies,
            List<Warrior> enemies) => new DecisionContext { Actor=warrior };
    }
    public sealed class BehaviourResolver
    {
        public ActionType Decide(Warrior warrior,DecisionContext context)
        {
            if(warrior.ThrowDecision) throw new InvalidOperationException("simulated brain failure");
            return warrior.PreferredAction;
        }
    }
}

static class Program
{
    static int checks;
    static void Check(bool ok,string message)
    {
        if(!ok) throw new Exception("FAIL: "+message);
        checks++;
    }

    static void Main()
    {
        var one=new Warrior("Марга",Team.Player,15f,5f);
        var foe=new Warrior("Чужой 1",Team.Enemy,15f,1f);
        var squad=new List<Warrior>{one};
        var foes=new List<Warrior>{foe};
        var history=new BattleRecord();
        var session=new AutoBattleResolver.Session(one,squad,foes,
            SquadStrategy.Aggressive,history);
        Check(!session.IsComplete && session.Result==null,
            "encounter is not silently resolved during construction");
        Check(session.CompletedRounds==0 && history.Events.Count==0,
            "no battle events are fabricated before first step");
        Check(session.StepRound(),"first genuine resolver round executed");
        Check(session.CompletedRounds==1 && !session.IsComplete,
            "after one round later rounds are still pending");
        Check(one.HP==14f && foe.HP==10f,
            "real damage applied only for the first round");
        Check(history.Events.Count==2 && history.Events.All(e=>e.Round==1),
            "only first round's executed actors are recorded");
        Check(SquadOrders.Current==SquadStrategy.Defensive,
            "temporary AOS orders restored after each round");

        Check(session.StepRound(),"second round actually progressed");
        Check(session.CompletedRounds==2 && history.Events.Count==4,
            "second-round actors were not retrospectively simulated");
        Check(!session.IsComplete,"encounter still live before decisive blow");
        Check(session.StepRound(),"third round completes encounter");
        Check(session.IsComplete && session.CompletedRounds==3,
            "real battle ends when last enemy falls");
        Check(session.Result.TotalTurns==3 && history.TotalTurns==3 &&
              history.Winner=="Squad","genuine final winner and turn count");
        Check(foe.HP==0f && session.Result.EnemiesDefeated==1,
            "final enemy actually fell in combat");
        Check(!session.StepRound() && history.Events.Count==5,
            "finished encounter cannot repeat actions or duplicate record");

        var one2=new Warrior("Марга",Team.Player,15f,5f);
        var foe2=new Warrior("Чужой 1",Team.Enemy,15f,1f);
        var record2=new BattleRecord();
        var instant=AutoBattleResolver.Resolve(one2,new List<Warrior>{one2},
            new List<Warrior>{foe2},SquadStrategy.Aggressive,record2);
        Check(instant.TotalTurns==session.Result.TotalTurns &&
              instant.EnemiesDefeated==session.Result.EnemiesDefeated &&
              record2.Events.Count==history.Events.Count &&
              record2.Winner==history.Winner,
            "legacy Resolve and manual round-stepping share exact results");
        Check(record2.Events.Select(e=>e.Action)
                  .SequenceEqual(history.Events.Select(e=>e.Action)),
            "legacy and staged executor have identical decisions");

        var a=new Warrior("Отряд А",Team.Player,20f,2f);
        var b=new Warrior("Чужой А",Team.Enemy,20f,2f);
        var c=new Warrior("Отряд Б",Team.Player,20f,3f);
        var d=new Warrior("Чужой Б",Team.Enemy,20f,3f);
        var sa=new AutoBattleResolver.Session(a,new List<Warrior>{a},
            new List<Warrior>{b},SquadStrategy.Aggressive);
        var sb=new AutoBattleResolver.Session(c,new List<Warrior>{c},
            new List<Warrior>{d},SquadStrategy.Balanced);
        sa.StepRound(); sb.StepRound(); sa.StepRound();
        Check(sa.CompletedRounds==2 && sb.CompletedRounds==1,
            "two concurrent battles progress independently");
        Check(SquadOrders.Current==SquadStrategy.Defensive,
            "interleaved AOS sessions cannot leak temporary order policy");
        Check(sa.Record.Events.Count==4 && sb.Record.Events.Count==2,
            "parallel battle records do not mix");

        var fleeing=new Warrior("Беглец",Team.Player,20f,3f)
            {PreferredAction=ActionType.Flee};
        var blocker=new Warrior("Охранник",Team.Enemy,20f,4f);
        var withdrew=new AutoBattleResolver.Session(fleeing,
            new List<Warrior>{fleeing},new List<Warrior>{blocker},
            SquadStrategy.Defensive);
        Check(withdrew.StepRound() && withdrew.IsComplete,
            "real fleeing removes last combatant without 20 phantom turns");
        Check(withdrew.Record.Events.Count==1 &&
              withdrew.Record.Events[0].Resolved &&
              withdrew.Record.Winner=="Enemy",
            "confirmed retreat is not narrated as a win");

        var runaway=new Warrior("Неисправный",Team.Player,10f,1f)
            {ThrowDecision=true};
        var testEnemy=new Warrior("Чужой",Team.Enemy,10f,1f);
        var broken=new AutoBattleResolver.Session(runaway,
            new List<Warrior>{runaway},new List<Warrior>{testEnemy},
            SquadStrategy.Aggressive);
        bool caught=false;
        try {broken.StepRound();}catch(InvalidOperationException){caught=true;}
        Check(caught && SquadOrders.Current==SquadStrategy.Defensive,
            "AOS order restored even when decision engine throws");
        Check(broken.CompletedRounds==0,
            "incomplete failed round is not marked as committed");

        Check(broken.IsFaulted,"failed real AOS step is marked unsafe to repeat");
        caught=false;
        try {broken.StepRound();}catch(InvalidOperationException){caught=true;}
        Check(caught,"partially failed round cannot be run a second time");

        var snapshot=new ExpeditionBattleCheckpoint();
        snapshot.Actors.Add(new ExpeditionBattleActor
        {
            Name="Марга",Id="stable-player-1",OriginalParty=true,Team=0,
            HealthFraction=1f,SquadIndex=0,EnemyIndex=-1
        });
        snapshot.Actors.Add(new ExpeditionBattleActor
        {
            Name="Чужой 1",Id="stable-enemy-1",OriginalParty=false,Team=1,
            HealthFraction=1f,SquadIndex=-1,EnemyIndex=0
        });
        Check(ExpeditionBattleCheckpoint.IsValid(snapshot,1,1,20),
            "fresh combat has a valid and empty pre-first-round snapshot");
        snapshot.CompletedRounds=1;
        snapshot.Actors[0].HealthFraction=0.75f;
        snapshot.Record.Events.Add(new BattleEvent
        {
            ActorId="stable-player-1",ActorName="Марга",Round=1,
            Action=ActionType.Attack,Resolved=true,DamageDealt=3f
        });
        var copy=ExpeditionBattleCheckpoint.Copy(snapshot);
        Check(ExpeditionBattleCheckpoint.IsValid(copy,1,1,20),
            "snapshot with real AOS first round validates");
        snapshot.Actors[0].HealthFraction=1f;
        snapshot.Record.Events[0].DamageDealt=99f;
        Check(copy.Actors[0].HealthFraction==0.75f &&
              copy.Record.Events[0].DamageDealt==3f,
            "saving a round makes deep copy of actor HP and actions");
        copy.Actors[1].Id="stable-player-1";
        Check(!ExpeditionBattleCheckpoint.IsValid(copy,1,1,20),
            "a corrupted save cannot put two actors at the same ID");
        copy.Actors[1].Id="stable-enemy-1";
        copy.Actors[1].SquadIndex=1;
        Check(!ExpeditionBattleCheckpoint.IsValid(copy,1,1,20),
            "one actor cannot act on both teams in a saved round");
        copy.Actors[1].SquadIndex=-1;
        copy.Actors[1].EnemyIndex=2;
        Check(!ExpeditionBattleCheckpoint.IsValid(copy,1,1,20),
            "hole in saved squad order cannot invent actors");
        copy.Actors[1].EnemyIndex=0;
        copy.Actors[1].Dead=true;
        Check(!ExpeditionBattleCheckpoint.IsValid(copy,1,1,20),
            "dead actors cannot retain living HP after restore");
        copy.Actors[1].HealthFraction=0f;
        Check(ExpeditionBattleCheckpoint.IsValid(copy,1,1,20),
            "legitimately dead original foe can be restored");
        copy.Record.Events.Add(new BattleEvent{ Round=2 });
        Check(!ExpeditionBattleCheckpoint.IsValid(copy,1,1,20),
            "future events cannot appear before their AOS round");
        copy.Record.Events.RemoveAt(1);
        Check(!ExpeditionBattleCheckpoint.IsValid(copy,1,2,20),
            "checkpoint cannot silently omit an original enemy");
        Check(!ExpeditionBattleCheckpoint.IsValid(null,1,1,20),
            "missing battle checkpoint is invalid, never an excuse to reroll");

        Console.WriteLine("PASS: "+checks+" real AOS session, one-round progression, and legacy equivalence checks");
    }
}
