using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Sinbinder.AOS;
using Sinbinder.Gameplay;
using UnityEngine;

internal static class AutoAttackTickChecks
{
    public static void Run(Action<bool, string> check)
    {
        var (tick, mind, presenter, warrior) = ReadyTick();
        mind.RefusesCommand = false;
        Time.deltaTime = 0f;
        Invoke(tick, "Update");
        check(mind.DecisionCount == 0 && mind.Executed.Count == 0,
            "a due AOS tick skips voting and acting while paused");

        Time.deltaTime = 0.01f;
        Invoke(tick, "Update");
        check(mind.DecisionCount == 1 && mind.Executed.Count == 1,
            "resuming the game runs the due AOS tick once");

        (tick, mind, presenter, warrior) = ReadyTick();
        Invoke(tick, "Update");
        check(presenter.Count == 1 && ReferenceEquals(presenter.Context, mind.LastContext) &&
              mind.Executed.Count == 0,
            "the refusal beat presents the current vote before acting");

        Invoke(tick, "OnDisable");
        tick.ResumeCoroutine();
        check(mind.Executed.Count == 0, "disabling the fighter cancels the pending refusal action");

        Invoke(tick, "Update");
        check(mind.DecisionCount == 2 && mind.Executed.Count == 1 && presenter.Count == 1,
            "the reactivated fighter votes again without repeating the same order's refusal cue");

        (tick, mind, presenter, warrior) = ReadyTick();
        Invoke(tick, "Update");
        var previousVote = mind.LastContext;
        warrior.CommandRevision++;
        Time.time = 11.4f;
        tick.ResumeCoroutine();
        check(mind.Executed.Count == 0, "changing order during the beat still cancels the old action");

        Time.time = 12f;
        Invoke(tick, "Update");
        check(presenter.Count == 2 && ReferenceEquals(presenter.Context, mind.LastContext) &&
              !ReferenceEquals(presenter.Context, previousVote) && mind.Executed.Count == 0,
            "the replacement order receives its own vote and refusal cue");
        Time.time = 12.4f;
        tick.ResumeCoroutine();
        check(mind.Executed.Count == 1 && mind.Executed[0] == ActionType.Flee,
            "the current order's action executes after its own refusal beat");
    }

    private static (AutoAttackAOS, AOSWarriorWrapper, RefusalPresenter, Warrior) ReadyTick()
    {
        Time.time = 10f;
        Time.deltaTime = 0.01f;
        var tick = new AutoAttackAOS();
        var mind = new AOSWarriorWrapper();
        var presenter = new RefusalPresenter();
        var warrior = new Warrior();
        tick.Attach(mind);
        tick.Attach(presenter);
        tick.Attach(warrior);
        Invoke(tick, "Awake");
        Time.time = 11f;
        return (tick, mind, presenter, warrior);
    }

    private static void Invoke(AutoAttackAOS tick, string method) =>
        typeof(AutoAttackAOS).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tick, null);
}

// Drive the production behaviour with an explicit Unity clock and coroutine lifecycle.
namespace UnityEngine
{
    internal sealed class RequireComponentAttribute : Attribute { public RequireComponentAttribute(Type type) { } }
    internal sealed class SerializeField : Attribute { }
    internal sealed class HeaderAttribute : Attribute { public HeaderAttribute(string text) { } }
    internal sealed class TooltipAttribute : Attribute { public TooltipAttribute(string text) { } }
    internal enum RuntimeInitializeLoadType { SubsystemRegistration }
    internal sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) { }
    }
    internal sealed class WaitForSeconds { public WaitForSeconds(float seconds) { } }
    internal static class Time { public static float time, deltaTime; }
    internal static class Mathf
    {
        public static float Abs(float value) => Math.Abs(value);
        public static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max);
        public static int Min(int a, int b) => Math.Min(a, b);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
    }
    internal static class Debug { public static void Log(string message) { } }
    public sealed class GameObject { public string name = ""; }

    public class MonoBehaviour
    {
        private readonly Dictionary<Type, object> _components = new();
        private IEnumerator _coroutine;
        public readonly GameObject gameObject = new();
        public int RepeatCount { get; private set; }

        public void Attach<T>(T component) => _components[typeof(T)] = component;
        protected T GetComponent<T>() => _components.TryGetValue(typeof(T), out var value) ? (T)value : default;
        protected void DontDestroyOnLoad(GameObject value) { }
        protected void Destroy(GameObject value) { }
        protected void InvokeRepeating(string method, float delay, float interval) => RepeatCount++;

        protected void StartCoroutine(IEnumerator coroutine)
        {
            _coroutine = coroutine;
            ResumeCoroutine();
        }

        public void ResumeCoroutine()
        {
            if (_coroutine != null && !_coroutine.MoveNext()) _coroutine = null;
        }

        protected void StopAllCoroutines() => _coroutine = null;
    }
}

namespace Sinbinder.Core
{
    internal enum Clarity { Trace }
    internal static class Transparency { public static bool Shows(Clarity level) => false; }

    internal readonly struct Cost : IDisposable
    {
        public static Cost Of(string name) => default;
        public void Dispose() { }
    }
}

namespace Sinbinder.Gameplay
{
    internal sealed class Damageable { public bool IsDead => false; }
    public sealed class Warrior
    {
        public uint CommandRevision { get; set; } = 1u;
        public string Id { get; set; }
        public string DisplayName => Id;
    }
}

namespace Sinbinder.AOS
{
    internal sealed class DecisionContext { }
    internal struct Decision { public bool RefusedCommand; }

    // Supply a vote while AutoAttackAOS controls scheduling and OrderIntegrity checks command identity.
    internal sealed class AOSWarriorWrapper
    {
        public bool RefusesCommand { get; set; } = true;
        public int DecisionCount { get; private set; }
        public DecisionContext LastContext { get; private set; }
        public Decision LastDecisionDetail => new() { RefusedCommand = RefusesCommand };
        public List<ActionType> Executed { get; } = new();

        public ActionType Decide()
        {
            DecisionCount++;
            LastContext = new DecisionContext();
            return RefusesCommand ? ActionType.Flee : ActionType.ObeyCommand;
        }

        public void Execute(ActionType action) => Executed.Add(action);
    }

    internal sealed class RefusalPresenter
    {
        public int Count { get; private set; }
        public DecisionContext Context { get; private set; }

        public void Play(Decision decision, DecisionContext context)
        {
            Count++;
            Context = context;
        }
    }
}
