using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sinbinder.AOS;
using Sinbinder.Gameplay;
using Sinbinder.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class CombatIntegrationChecks
{
    public static void Run(Action<bool, string> check)
    {
        Victory(check);
        Draw(check, enemyFirst: true);
        Draw(check, enemyFirst: false);
        DefeatAndNextWave(check);
        Reinforcement(check);
        Teardown(check);
        Knockdown(check);
    }

    private static void Victory(Action<bool, string> check)
    {
        using var battle = new Fixture();
        var survivor = battle.Spawn(Team.Player, "survivor");
        var ally = battle.Spawn(Team.Player, "fallen ally");
        var first = battle.Spawn(Team.Enemy, "first enemy");
        var second = battle.Spawn(Team.Enemy, "second enemy");
        var last = battle.Spawn(Team.Enemy, "last enemy");
        // Prologue Start may run after all of the scene's units registered.
        battle.StartPrologue("next chapter");

        int changes = 0;
        battle.Manager.OnUnitsChanged += () => changes++;
        battle.Manager.RegisterEnemyUnit(first);
        battle.Manager.OnUnitKilled(first, survivor.gameObject);
        check(changes == 0 && battle.Manager.GetAliveEnemyCount() == 3 && battle.Deaths.Count == 0,
            "duplicate registration and a false death report preserve the live roster");

        Kill(first, survivor);
        Kill(first, survivor);
        battle.Manager.OnUnitKilled(first, survivor.gameObject);
        check(changes == 1 && battle.Deaths.SequenceEqual(new[] { first }) &&
            battle.Hub.Deaths.SequenceEqual(new[] { first }) && battle.Manager.BodiesOnField.Count == 1,
            "the lethal hit removes its unit and broadcasts exactly one death and body");

        Kill(ally, second);
        Kill(second, survivor);
        battle.EndFrame();
        check(battle.Hub.Battles.Count == 0 && battle.Log.Lines.Count == 0,
            "a living member on each side keeps the battle and prologue open");

        Kill(last, survivor);
        check(battle.Hub.Battles.Count == 0 && battle.Log.Lines.Count == 0,
            "the final lethal hit defers battle completion and progression to frame end");
        battle.EndFrame();
        battle.EndFrame();

        check(battle.Hub.Battles.Count == 1 && HasResult(battle.Hub.Battles[0], BattleOutcome.Victory, 3, 1, ally),
            "combat publishes victory once with three enemy deaths and one allied death");
        check(LootChain.Shares == 1 && SceneManager.Loads.SequenceEqual(new[] { "next chapter" }),
            "victory shares loot once and advances the prologue once");
    }

    private static void Draw(Action<bool, string> check, bool enemyFirst)
    {
        using var battle = new Fixture();
        battle.StartPrologue("next chapter");
        var first = enemyFirst ? battle.Spawn(Team.Enemy, "enemy") : battle.Spawn(Team.Player, "ally");
        battle.EndFrame();
        string order = enemyFirst ? "enemy first" : "ally first";
        check(battle.Hub.Battles.Count == 0 && battle.Log.Lines.Count == 0,
            $"a single registered side does not end the prologue ({order})");
        var ally = enemyFirst ? battle.Spawn(Team.Player, "ally") : first;
        var enemy = enemyFirst ? first : battle.Spawn(Team.Enemy, "enemy");

        if (enemyFirst)
        {
            Kill(enemy, ally);
            Kill(ally, enemy);
        }
        else
        {
            Kill(ally, enemy);
            Kill(enemy, ally);
        }

        check(battle.Hub.Battles.Count == 0 && battle.Log.Lines.Count == 0,
            $"same-frame deaths defer all terminal effects ({order})");
        battle.EndFrame(prologueFirst: enemyFirst);
        battle.EndFrame();
        check(battle.Hub.Battles.Count == 1 && HasResult(battle.Hub.Battles[0], BattleOutcome.Draw, 1, 1, ally),
            $"both deaths produce one draw with both casualties ({order})");
        check(battle.End.Wiped.SequenceEqual(new[] { true }) && LootChain.Shares == 0 && SceneManager.Loads.Count == 0,
            $"the prologue ends as a wipe and shares no victory loot after a draw ({order})");
    }

    private static void DefeatAndNextWave(Action<bool, string> check)
    {
        using var battle = new Fixture();
        battle.StartPrologue();
        var ally = battle.Spawn(Team.Player, "first ally");
        var enemy = battle.Spawn(Team.Enemy, "enemy");
        Kill(ally, enemy);
        battle.EndFrame();
        check(battle.Hub.Battles.Count == 1 && HasResult(battle.Hub.Battles[0], BattleOutcome.Defeat, 0, 1, ally) &&
            battle.End.Wiped.SequenceEqual(new[] { true }) && LootChain.Shares == 0,
            "losing the last ally publishes defeat with that warrior's identity and wipes the prologue");

        var nextAlly = battle.Spawn(Team.Player, "new ally");
        Kill(enemy, nextAlly);
        battle.EndFrame();
        check(battle.Hub.Battles.Count == 2 && HasResult(battle.Hub.Battles[1], BattleOutcome.Victory, 1, 0),
            "a later battle uses fresh casualty totals and awards exactly one new outcome");
    }

    private static void Reinforcement(Action<bool, string> check)
    {
        using var battle = new Fixture();
        battle.StartPrologue();
        var ally = battle.Spawn(Team.Player, "ally");
        var enemy = battle.Spawn(Team.Enemy, "first enemy");
        Kill(enemy, ally);
        var reinforcement = battle.Spawn(Team.Enemy, "reinforcement");
        battle.EndFrame();
        check(battle.Hub.Battles.Count == 0 && battle.Log.Lines.Count == 0,
            "enemy reinforcement in the same frame cancels pending victory and progression");

        Kill(reinforcement, ally);
        battle.EndFrame();
        check(battle.Hub.Battles.Count == 1 && HasResult(battle.Hub.Battles[0], BattleOutcome.Victory, 2, 0),
            "the resumed battle keeps the casualties from before reinforcement");
    }

    private static void Teardown(Action<bool, string> check)
    {
        using var battle = new Fixture();
        var ally = battle.Spawn(Team.Player, "ally");
        var enemy = battle.Spawn(Team.Enemy, "enemy");
        Call(enemy, "OnDestroy");
        battle.EndFrame();
        check(battle.Hub.Battles.Count == 0 && battle.Hub.Deaths.Count == 0,
            "destroying the last live enemy unregisters it without an artificial victory");

        var last = battle.Spawn(Team.Enemy, "last enemy");
        Kill(last, ally);
        SceneManager.Unload();
        battle.EndFrame();
        check(battle.Hub.Battles.Count == 0 && LootChain.Shares == 0,
            "the real scene-unload callback discards an unresolved result");
    }

    private static void Knockdown(Action<bool, string> check)
    {
        using var battle = new Fixture();
        Lords.Active = true;
        var ally = battle.Spawn(Team.Player, "ally");
        var lord = battle.Spawn(Team.Player, "lord");
        lord.Warrior.IsLord = true;
        var enemy = battle.Spawn(Team.Enemy, "enemy");

        Kill(lord, enemy);
        check(TugMatch.Knockdowns.SequenceEqual(new[] { lord.Warrior }) && battle.Deaths.Count == 0 &&
            battle.Manager.BodiesOnField.Count == 0,
            "a lord knockdown schedules return and skips permanent death and loot");
        Kill(enemy, ally);
        battle.EndFrame();
        check(battle.Hub.Battles.Count == 1 && HasResult(battle.Hub.Battles[0], BattleOutcome.Victory, 1, 0),
            "the later Tug victory does not count a temporary lord knockdown as an allied casualty");
    }

    private static bool HasResult(BattleResult result, BattleOutcome outcome, int enemies, int allies, params Damageable[] fallen) =>
        result.Outcome == outcome && result.EnemiesKilled == enemies && result.AlliesLost == allies &&
        result.FallenAllies.SequenceEqual(fallen.Select(unit => unit.Warrior.Id));

    private static void Kill(Damageable target, Damageable attacker) =>
        target.TakeDamage(2f * target.MaxHP, attacker.gameObject);

    private static void Call(MonoBehaviour component, string message)
    {
        var callback = component.GetType().GetMethod(message, BindingFlags.Instance | BindingFlags.NonPublic);
        if (callback == null) throw new InvalidOperationException($"Missing Unity callback {component.GetType().Name}.{message}");
        callback.Invoke(component, null);
    }

    private sealed class Fixture : IDisposable
    {
        private PrologueDirector _prologue;

        public Fixture()
        {
            Hub = new AOSEventHub();
            AOSEventHub.Instance = Hub;
            Manager = new GameObject("Combat").AddComponent<CombatManager>();
            Call(Manager, "Awake");
            Manager.OnAnyDeath += (victim, _) => Deaths.Add(victim);
            Log = new GameObject("Battle log").AddComponent<BattleLogUI>();
            End = new GameObject("Prologue end").AddComponent<DemoEndUI>();
        }

        public CombatManager Manager { get; }
        public AOSEventHub Hub { get; }
        public BattleLogUI Log { get; }
        public DemoEndUI End { get; }
        public List<Damageable> Deaths { get; } = new();

        public void StartPrologue(string nextScene = null)
        {
            _prologue = new GameObject("Prologue").AddComponent<PrologueDirector>();
            if (nextScene != null)
                typeof(PrologueDirector).GetField("_nextScene", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(_prologue, nextScene);
            Call(_prologue, "Awake");
            Call(_prologue, "Start");
        }

        public Damageable Spawn(Team team, string name)
        {
            var unit = new GameObject(name);
            unit.AddComponent<Warrior>().Team = team;
            var body = unit.AddComponent<Damageable>();
            Call(body, "Awake");
            Call(body, "Start");
            return body;
        }

        public void EndFrame(bool prologueFirst = false)
        {
            if (prologueFirst && _prologue != null) Call(_prologue, "LateUpdate");
            Call(Manager, "LateUpdate");
            if (!prologueFirst && _prologue != null) Call(_prologue, "LateUpdate");
            _prologue?.ContinueCoroutines();
        }

        public void Dispose()
        {
            if (_prologue != null) Call(_prologue, "OnDestroy");
            Call(Manager, "OnDestroy");
            GameObject.ClearScene();
            SceneManager.Loads.Clear();
            AOSEventHub.Instance = null;
            Lords.Active = false;
            LootChain.Shares = 0;
            TugMatch.Knockdowns.Clear();
        }
    }
}
