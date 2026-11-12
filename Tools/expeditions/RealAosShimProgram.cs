// Executes the REAL BehaviourResolver and all thirteen personality modules.
// The external Unity runtime, perks, emotions and effects are headless stubs.
// Do not claim this tests physical Unity PlayMode or persistence of live side effects.
using System;
using System.Collections.Generic;
using System.Linq;
using Sinbinder.AOS;
using Sinbinder.Core;
using Sinbinder.Gameplay;
using Sinbinder.AOS.Modules;

namespace UnityEngine
{
    public sealed class CreateAssetMenuAttribute : Attribute
    {
        public string fileName,menuName;
    }
    public sealed class HeaderAttribute : Attribute
    {
        public HeaderAttribute(string value) {}
    }
    public sealed class TooltipAttribute : Attribute
    {
        public TooltipAttribute(string value) {}
    }
    public sealed class RangeAttribute : Attribute
    {
        public RangeAttribute(float min,float max) {}
    }
    public class ScriptableObject
    {
        public string name;
        public static T CreateInstance<T>() where T:new() => new T();
    }
    public static class Resources
    {
        public static T Load<T>(string name) where T:class => null;
    }
    public static class Mathf
    {
        public static float Abs(float x)=>Math.Abs(x);
        public static float Max(float x,float y)=>Math.Max(x,y);
        public static int Max(int x,int y)=>Math.Max(x,y);
        public static float Min(float x,float y)=>Math.Min(x,y);
        public static float Clamp(float x,float a,float b)=>Math.Max(a,Math.Min(b,x));
        public static float Clamp01(float x)=>Clamp(x,0f,1f);
    }
    public static class Debug
    {
        public static readonly List<string> Messages=new List<string>();
        public static void Log(object msg) => Messages.Add(msg?.ToString()??"");
        public static void LogWarning(object msg) => Messages.Add(msg?.ToString()??"");
    }
}
namespace Sinbinder.Core
{
    public enum SinType { Greed, Pride, Wrath, Envy, Lust, Gluttony, Sloth }
    public enum MoralType { Vicious, Neutral, Pious }
    public sealed class NarrativePerk {}
    public sealed class SoulData
    {
        public const int SpectrumCount=7;
        public SinType Sin;
        public MoralType Moral;
        public float[] Spectra=new float[SpectrumCount];
        public float[] CopySpectra()=>(float[])Spectra.Clone();
    }
    public enum Clarity { Trace }
    public static class Transparency
    {
        public static bool Shows(Clarity clarity)=>false;
    }
}
namespace Sinbinder.Inventory
{
    public sealed class InventoryItem {}
}
namespace Sinbinder.Gameplay
{
    public enum Team { Player, Enemy }
    public interface ISkillSet
    {
        IEnumerable<ActionType> SkillActions {get;}
        bool CanUseSkill(ActionType action);
    }
    public sealed class Warrior
    {
        public string DisplayName;
        public SoulData Soul;
        public float Loyalty;
        public Team Team;
        public T[] GetComponents<T>() => Array.Empty<T>();
    }
    public readonly struct StrategyModifier
    {
        public readonly ActionType Action;
        public readonly float Bonus;
        public StrategyModifier(ActionType action,float bonus)
        {Action=action;Bonus=bonus;}
    }
    public static class SquadOrders
    {
        public static IEnumerable<StrategyModifier> CurrentModifiers()
            => Array.Empty<StrategyModifier>();
    }
}
namespace Sinbinder.Dev
{
    public static class CaptureMode
    {
        public static bool NextRefusal => false;
        public static void RefusalTaken() {}
    }
}
namespace Sinbinder.AOS
{
    public enum MissionID { None }
    public sealed class NPCData {}
    public sealed class MemoryRecord
    {
        public string EventType;
        public string TargetID;
        public float Strength;
        public float Importance;
    }
    public sealed class MemoryProcessor
    {
        // No outside memory persistence in this CPU vote test.
        public static MemoryProcessor Instance => null;
        public List<MemoryRecord> GetMemories(Warrior warrior)
            => new List<MemoryRecord>();
    }
    public sealed class EmotionSystem
    {
        public static EmotionSystem Instance => null;
        public float GetEmotionWeight(Warrior warrior,string module)=>1f;
    }
    public sealed class PerkDatabase {}
    public static class PerkResolver
    {
        public static void ApplyPerks(Dictionary<ActionType,float> scores,
            Warrior warrior,DecisionContext context,PerkDatabase db) {}
    }
    public static class TemptationResolver
    {
        public static void Apply(Dictionary<ActionType,float> scores,
            DecisionContext context,float scale) {}
    }
    public static class AOSStats
    {
        public static void Record(Decision decision,DecisionContext context) {}
    }
    public static class Counterfactual
    {
        public enum Factor { None }
        public static bool Enabled=>false;
        public static Factor Decisive(DecisionContext ctx,
            Func<DecisionContext,bool> obey)=>Factor.None;
        public static bool DecisivePair(DecisionContext ctx,
            Func<DecisionContext,bool> obey,out Factor a,out Factor b)
        { a=Factor.None;b=Factor.None;return false; }
        public static string DecisiveVoice(IEnumerable<string> voices,
            Func<string,bool> obey)=>null;
        public static IEnumerable<string> Voices(SinType? sin)
            => Array.Empty<string>();
    }
}

static class RealAosShimProgram
{
    static int assertions;
    static void Check(bool ok,string message)
    {
        if(!ok)throw new Exception("REAL AOS SHIM FAIL: "+message);
        assertions++;
    }
    static Warrior Warrior(string name,float greed,float wrath,MoralType morality)
    {
        var soul=new SoulData{Sin=SinType.Greed,Moral=morality};
        soul.Spectra[(int)SinType.Greed]=greed;
        soul.Spectra[(int)SinType.Wrath]=wrath;
        return new Warrior{DisplayName=name,Soul=soul,Loyalty=65f,Team=Team.Player};
    }

    static void Main()
    {
        var resolver=new BehaviourResolver();
        var actor=Warrior("Командир",85f,70f,MoralType.Vicious);
        var context=new DecisionContext
        {
            NearbyEnemies=2,EnemiesInSight=2,NearbyLoot=1,
            CurrentHP=75f,MaxHP=100f,DangerLevel=0.35f,
            RecentMemories=new List<MemoryRecord>()
        };
        var ballot=resolver.Ballot(actor,context);
        var moduleNames=ballot.Select(v=>v.Module).Distinct().ToList();
        Check(moduleNames.Count==13,
            "all thirteen actual AOS personality modules evaluate combat options");
        Check(new[]{"Greed","Pride","Wrath","Envy","Lust","Gluttony",
            "Sloth","Patience","Fear","Loyalty","Morality","Memory","Virtue"}
            .All(m=>moduleNames.Contains(m)),
            "all thirteen expected production modules are represented in votes");
        Check(ballot.Any(v=>v.Module=="Greed" && v.Action==ActionType.Loot && v.Voice>0),
            "real Greed module actually raises loot score");
        Check(ballot.Any(v=>v.Module=="Wrath" && v.Action==ActionType.Attack && v.Voice>0),
            "real Wrath module actually raises attack score");
        var decision=resolver.DecideDetailed(actor,context);
        Check(Enum.IsDefined(typeof(ActionType),decision.Action),
            "real AOS returns an existing action rather than invalid stub output");

        var caravanOptions=new List<MissionAction>
        {
            MissionAction.TakeGoodsSparePeople,
            MissionAction.TakeEverything,
            MissionAction.LetThemPass,
            MissionAction.TakePeople
        };
        var caravan=new MissionContext
        {
            HasTreasure=true,HasInnocentVictims=true,HasGuiltyParty=false,
            ExpeditionSite="Соляной обоз",
            RecentMemories=new List<MemoryRecord>()
        };
        var greedy=resolver.DecideMission(actor,caravan,caravanOptions);
        Check(caravanOptions.Contains(greedy),
            "real thirteen-module mission vote returns only an offered action");
        var restrained=Warrior("Милосердный",-90f,-80f,MoralType.Pious);
        var kinder=resolver.DecideMission(restrained,caravan,caravanOptions);
        Check(caravanOptions.Contains(kinder),
            "real moral personality can decide independently of the same options");

        // AOS Memory in missions must be a weighted voice, not a script.
        caravan.RecentMemories.Add(new MemoryRecord
        {
            TargetID="Соляной обоз",
            EventType="ExpeditionSparedCaravan",
            Importance=1f,
            Strength=1f
        });
        var memoryModule=new MemoryModule();
        var memorySoul=Soul.FromWarrior(actor);
        float memoryVote=memoryModule.EvaluateMission(memorySoul,caravan,MissionAction.LetThemPass);
        Check(memoryVote!=0f,
            "production Memory module reads persistent expedition experience");
        Check(memoryModule.EvaluateMission(memorySoul,
            new MissionContext{ExpeditionSite="Другая миссия",
                RecentMemories=caravan.RecentMemories},
            MissionAction.LetThemPass)==0f,
            "expedition memory cannot contaminate another location");

        var contextNoThreat=new DecisionContext
        {
            NearbyEnemies=0,EnemiesInSight=0,NearbyLoot=0,
            CurrentHP=100f,MaxHP=100f,
            RecentMemories=new List<MemoryRecord>()
        };
        var idleVotes=resolver.Ballot(actor,contextNoThreat);
        Check(idleVotes.All(v=>v.Action!=ActionType.Attack &&
            v.Action!=ActionType.Flee && v.Action!=ActionType.Loot),
            "real resolver never proposes unachievable actions without enemies or loot");
        Console.WriteLine("PASS: "+assertions+
            " real BehaviourResolver and all 13 AOS module shim checks");
    }
}
