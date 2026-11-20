using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Sinbinder.AOS;
using UnityEngine;

internal static class AutoAttackTickChecks
{
    public static void Run(Action<bool, string> check)
    {
        var (tick, mind, presenter) = StartRefusal();
        var refused = mind.LastContext;
        check(presenter.Context == refused && mind.Executed.Count == 0,
            "real AOS tick presents its recorded refusal before starting the action");

        mind.Persuaded = true;
        Time.time = 11.4f;
        tick.ResumeCoroutine();
        check(mind.Executed.Count == 0,
            "a whisper accepted during the beat suppresses the old refusal action");

        Time.deltaTime = 0f;
        Invoke(tick, "Update");
        check(mind.LastContext == refused, "a stale vote does not force a new decision while paused");

        Time.deltaTime = 0.01f;
        Time.time = 11.41f;
        Invoke(tick, "Update");
        check(mind.Executed.Count == 1 && mind.Executed[0] != refused && mind.Executed[0].Persuaded,
            "the next active AOS frame executes the new recorded whisper decision");

        (tick, mind, _) = StartRefusal();
        mind.Decide();
        var newerVote = mind.LastContext;
        Time.time = 11.4f;
        tick.ResumeCoroutine();
        check(mind.Executed.Count == 0, "a newer vote for the same order invalidates the queued context");

        Time.time = 11.41f;
        Invoke(tick, "Update");
        check(mind.Executed.Count == 1 && mind.Executed[0] != newerVote,
            "the replacement context is evaluated again before dispatch");

        (tick, mind, _) = StartRefusal();
        mind.Revision++;
        Time.time = 11.4f;
        tick.ResumeCoroutine();
        check(mind.Executed.Count == 0, "replacing an order during the beat prevents the old action");

        mind.Persuaded = true;
        Time.time = 11.41f;
        Invoke(tick, "Update");
        check(mind.Executed.Count == 1 && mind.Executed[0].Revision == mind.Revision,
            "the replacement order executes only after its own vote");

        (tick, mind, _) = StartRefusal();
        Invoke(tick, "OnDisable");
        tick.ResumeCoroutine();
        check(mind.Executed.Count == 0, "disabling the fighter cancels the pending refusal action");
        Time.time = 11.1f;
        Invoke(tick, "Update");
        check(mind.Executed.Count == 1, "the reactivated fighter can decide again");
    }

    private static (AutoAttackAOS Tick, AOSWarriorWrapper Mind, RefusalPresenter Presenter) StartRefusal()
    {
        Time.time = 10f;
        Time.deltaTime = 0.01f;
        var tick = new AutoAttackAOS();
        var mind = new AOSWarriorWrapper();
        var presenter = new RefusalPresenter();
        tick.Attach(mind);
        tick.Attach(presenter);
        Invoke(tick, "Awake");
        Time.time = 11f;
        Invoke(tick, "Update");
        return (tick, mind, presenter);
    }

    private static void Invoke(AutoAttackAOS tick, string method) =>
        typeof(AutoAttackAOS).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tick, null);
}

// The clock and explicit coroutine resumption stand in for Unity's lifecycle.
namespace UnityEngine
{
    internal sealed class RequireComponentAttribute : Attribute { public RequireComponentAttribute(Type type) { } }
    internal sealed class SerializeField : Attribute { }
    internal sealed class HeaderAttribute : Attribute { public HeaderAttribute(string text) { } }
    internal sealed class TooltipAttribute : Attribute { public TooltipAttribute(string text) { } }
    internal sealed class WaitForSeconds { public WaitForSeconds(float seconds) { } }
    internal static class Time { public static float time, deltaTime; }
    public sealed class GameObject { public string name = ""; }

    public class MonoBehaviour
    {
        private readonly Dictionary<Type, object> _components = new();
        private IEnumerator _coroutine;
        public readonly GameObject gameObject = new();

        public void Attach<T>(T component) => _components[typeof(T)] = component;
        protected T GetComponent<T>() => _components.TryGetValue(typeof(T), out var value) ? (T)value : default;
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
    internal readonly struct Cost : IDisposable
    {
        public static Cost Of(string name) => default;
        public void Dispose() { }
    }
}

namespace Sinbinder.Gameplay
{
    internal sealed class Damageable { public bool IsDead => false; }
}

namespace Sinbinder.AOS
{
    internal sealed class DecisionContext
    {
        public uint Revision;
        public bool Persuaded;
    }
    internal struct Decision { public bool RefusedCommand; }

    // This double supplies ballots, while AutoAttackAOS controls timing and
    // context delivery and OrderIntegrity supplies the production guard.
    internal sealed class AOSWarriorWrapper
    {
        public bool Persuaded { get; set; }
        public uint Revision { get; set; } = 1u;
        public uint LastDecisionCommandRevision => LastContext.Revision;
        public bool HasCurrentCommandDecision => LastDecisionCommandRevision == Revision;
        public DecisionContext LastContext { get; private set; }
        public Decision LastDecisionDetail => new() { RefusedCommand = !LastContext.Persuaded };
        public List<DecisionContext> Executed { get; } = new();

        public void Decide() => LastContext = new DecisionContext { Revision = Revision, Persuaded = Persuaded };

        public bool TryExecuteDecision(DecisionContext context)
        {
            if (context != LastContext ||
                !OrderIntegrity.MayExecuteDecision(context.Revision, Revision, context.Persuaded, Persuaded))
                return false;
            Executed.Add(context);
            return true;
        }
    }

    internal sealed class RefusalPresenter
    {
        public DecisionContext Context { get; private set; }
        public void Play(Decision decision, DecisionContext context) => Context = context;
    }
}
