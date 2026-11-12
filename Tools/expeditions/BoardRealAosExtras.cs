// Headless stand-ins for Unity services not owned by the production AOS.
// Production BehaviourResolver and all 13 personality modules are compiled
// without modification. External perks, emotions and effects remain stubs.
using System;
using System.Collections.Generic;
using Sinbinder.AOS;
using Sinbinder.Gameplay;
using Sinbinder.Core;

namespace UnityEngine
{
    public sealed class CreateAssetMenuAttribute : Attribute
    {
        public string fileName,menuName;
    }
    public sealed class HeaderAttribute : Attribute
    {
        public HeaderAttribute(string name) {}
    }
    public sealed class TooltipAttribute : Attribute
    {
        public TooltipAttribute(string name) {}
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
        public static T Load<T>(string key) where T:class => null;
    }
    public static partial class Mathf
    {
        public static float Abs(float value)=>Math.Abs(value);
        public static float Min(float x,float y)=>Math.Min(x,y);
        public static float Clamp01(float x)=>Clamp(x,0f,1f);
    }
    public static partial class Debug
    {
        public static void Log(object value) {}
    }
}
namespace Sinbinder.Core
{
    public sealed class NarrativePerk {}
    public enum Clarity { Trace }
    public static class Transparency
    {
        public static bool Shows(Clarity clarity)=>false;
    }
}
namespace Sinbinder.Gameplay
{
    public interface ISkillSet
    {
        IEnumerable<ActionType> SkillActions {get;}
        bool CanUseSkill(ActionType action);
    }
    public readonly struct StrategyModifier
    {
        public readonly ActionType Action;
        public readonly float Bonus;
        public StrategyModifier(ActionType action,float bonus)
        { Action=action;Bonus=bonus; }
    }
    public static class SquadOrders
    {
        public static IEnumerable<StrategyModifier> CurrentModifiers()
            => Array.Empty<StrategyModifier>();
        public static Sinbinder.AOS.SquadStrategy FromSin(SinType sin)
            => Sinbinder.AOS.SquadStrategy.Balanced;
    }
}
namespace Sinbinder.Dev
{
    public static class CaptureMode
    {
        public static bool NextRefusal=>false;
        public static void RefusalTaken() {}
    }
}
namespace Sinbinder.AOS
{
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
        public static void Record(Decision result,DecisionContext context) {}
    }
    public static class Counterfactual
    {
        public enum Factor { None }
        public static bool Enabled=>false;
        public static Factor Decisive(DecisionContext context,
            Func<DecisionContext,bool> predicate)=>Factor.None;
        public static bool DecisivePair(DecisionContext context,
            Func<DecisionContext,bool> predicate,out Factor first,out Factor second)
        { first=Factor.None;second=Factor.None;return false; }
        public static IEnumerable<string> Voices(SinType? sin)
            => Array.Empty<string>();
        public static string DecisiveVoice(IEnumerable<string> voices,
            Func<string,bool> predicate)=>null;
    }
}
