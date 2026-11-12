// Expanded Unity API shim for compiling the REAL MissionBoard and all
// production AOS/exploration/replay/loot policy it invokes.
// External engine scene/AI/physics APIs are intentionally stubbed. This is
// an integration test for the CPU/game-state code, NOT Unity PlayMode.
using System;
using System.Collections.Generic;
using System.Linq;
using Sinbinder.AOS;
using Sinbinder.Gameplay;
using Sinbinder.Crypt;

namespace UnityEngine
{
    public static partial class Mathf
    {
        public static float Clamp(float v,float a,float b) => Math.Max(a,Math.Min(b,v));
    }
    public class MonoBehaviour
    {
        public GameObject gameObject {get;}=new GameObject("Shim MissionBoard");
        protected static void Destroy(GameObject obj) { Object.Destroy(obj); }
        protected static void DestroyImmediate(GameObject obj) { Object.DestroyImmediate(obj); }
    }
    public enum FindObjectsSortMode { InstanceID }
    public enum FindObjectsInactive { Include }
    public static class Time { public static float deltaTime=0.1f; }
    public static class Debug
    {
        public static readonly List<string> Warnings=new List<string>();
        public static void LogWarning(object obj) => Warnings.Add(obj?.ToString() ?? "");
    }
    public static partial class Object
    {
        public static T FindFirstObjectByType<T>() where T:class => null;
        public static T[] FindObjectsByType<T>(FindObjectsSortMode sort) where T:class
            => Array.Empty<T>();
        public static T[] FindObjectsByType<T>(FindObjectsInactive inactive,
            FindObjectsSortMode sort) where T:class => Array.Empty<T>();
    }
}
namespace Sinbinder.Core
{
    public static class Loc
    {
        public static string T(string s) => s;
        public static string N(string s) => s;
        public static string Name(string s) => s;
        public static string F(string template, params object[] args)
            => string.Format(template,args);
    }
    public sealed class Record
    {
        public string Title;
    }
    public static class Records
    {
        public static Record FromMission(string name) => null;
        public static string MetalWord(Record r) => "пластинка";
    }
    public static class SaveSystem
    {
        public static int SaveCount;
        public static List<SavedAutoExpedition> LastSave;
        public static void AutoSave()
        {
            SaveCount++;
            LastSave=MissionBoard.SnapshotJourneys();
        }
    }
}
namespace Sinbinder.AOS
{
#if !REAL_AOS
    public enum MissionAction
    {
        TakeGoodsSparePeople,TakeEverything,LetThemPass,TakePeople
    }
#endif
    public enum EmotionType { Fear,Joy,Disgust,Calm,Sadness }
    public sealed class MemoryRecord
    {
        public string EventType;
        public string TargetID;
        public float Strength;
        public float Importance;
    }
#if !REAL_AOS
    public sealed class MissionContext
    {
        public bool HasInnocentVictims, HasTreasure, HasGuiltyParty;
        public string ExpeditionSite;
        public List<MemoryRecord> RecentMemories;
        public List<Sinbinder.Inventory.InventoryItem> CarriedItems;
        public bool HasSuggestion;
        public MissionAction SuggestedAction;
    }
    public sealed partial class BehaviourResolver
    {
        public MissionAction DecideMission(Warrior commander,
            MissionContext context,List<MissionAction> options)
        {
            // Only the vote is stubbed. The real MissionBoard validates,
            // stores, and applies the decided action through production
            // MissionCatalog and JunctionCatalog.
            var answer=context.HasSuggestion
                ? context.SuggestedAction : MissionAction.TakeEverything;
            return options.Contains(answer) ? answer : options[0];
        }
    }
#endif
#if REAL_AOS
    public enum MissionID { None }
    public sealed class NPCData {}
#endif
}
namespace Sinbinder.Inventory
{
    public enum ItemType { Shell }
    public sealed partial class InventoryItem
    {
        public string Name;
        public ItemType Type;
        public InventoryItem(string name,string description,ItemType type)
        {Name=name;Type=type;}
    }
    public sealed class PlayerInventory
    {
        public static PlayerInventory Instance;
        public int Gold;
        public int ShellCount;
        public void AddGold(int value) { Gold+=value; }
        public bool AddItem(InventoryItem item)
        {
            if(item.Type==ItemType.Shell) ShellCount++;
            return true;
        }
    }
}
namespace Sinbinder.Gameplay
{
    public partial class Warrior
    {
        public UnityEngine.GameObject gameObject {get;}=
            new UnityEngine.GameObject("Shim Warrior");
    }
    public sealed class SinbinderPlayer : Warrior {}
    public static class PersonalExpedition
    {
        public static bool Active;
    }
    public static class Leadership
    {
        public static int SquadSize(float skill) => 6;
        public static string Count(int count) => count.ToString();
        public static string Collective(int count) => count.ToString();
    }
    public static class Homecoming
    {
        public static string Story(Core.SinType sin, Core.Gender gender) => "Вернулся.";
    }
    public static class Aftermath
    {
        public static float LoyaltyShift(Core.MoralType moral, MissionAction a) => 0f;
        public static string Judged(Core.MoralType moral,MissionAction a,Core.Gender g)
            => "оценил";
    }
    public static class ExpeditionMemoryLedger
    {
        public static List<MemoryRecord> ForWarrior(string name,string mission)
            => new List<MemoryRecord>();
        public static void Remember(string journeyId,string name,string mission,
            string eventType,EmotionType emotion,float importance) {}
        public static void Forget() {}
    }
    public static partial class SquadRoster
    {
        public static readonly List<Member> Members=new List<Member>();
        public static IEnumerable<Member> Away => Members.Where(m=>m.IsAway);
        public static bool TryGet(string name,out Member m)
        {
            m=Members.FirstOrDefault(x=>x.Name==name);
            return m!=null;
        }
        public static bool CanGo(Member m) => m!=null && !m.IsAway &&
            string.IsNullOrEmpty(m.Unavailable);
        public static ICollection<string> Suggest(string name,int count,bool keepExperienced)
            => Members.Where(m=>m.Name!=name && CanGo(m)).Take(Math.Max(0,count-1))
               .Select(m=>m.Name).ToList();
        public static void SendAway(string commander,ICollection<string> others)
        {
            var names=new HashSet<string>(others ?? Array.Empty<string>());
            names.Add(commander);
            foreach(var member in Members)
                if(names.Contains(member.Name)) member.IsAway=true;
        }
        public static void ComeBack(List<string> all,List<string> survivors)
        {
            var alive=new HashSet<string>(survivors);
            Members.RemoveAll(x=>all.Contains(x.Name) && !alive.Contains(x.Name));
            foreach(var member in Members)
                if(all.Contains(member.Name)) member.IsAway=false;
        }
        public static void ApplyExpeditionHealth(string name,float fraction)
        {
            if(TryGet(name,out var m)) m.HealthFraction=fraction;
        }
        public static void CreditPocket(string name,int amount)
        {
            if(TryGet(name,out var m)) m.Pocket+=amount;
        }
        public static void ShiftLoyalty(string name,float amount)
        {
            if(TryGet(name,out var m)) m.Loyalty+=amount;
        }
        public sealed partial class Member
        {
            public bool IsAway;
            public string Unavailable;
            public float Leadership=100f;
            public string ShownName => Name;
        }
    }
}
namespace Sinbinder.Crypt
{
    public static class ExpeditionWorldClock
    {
        public static bool IsRunning;
    }
    public static class CryptUpgrades
    {
        public static void Forget() {}
    }
    public static class Retelling
    {
        public static string Describe(BattleEvent ev)
            => ev.ActorName+" "+ev.Action+" "+ev.DamageDealt.ToString("0.0");
        public static string Tell(BattleRecord r)
            => r?.Events==null ? "" : "Ходов: "+r.TotalTurns;
    }
}
namespace Sinbinder.UI
{
    public sealed class BattleLogUI
    {
        public void Write(string msg) {}
    }
}
