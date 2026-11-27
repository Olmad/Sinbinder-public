using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sinbinder.AOS;
using Sinbinder.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class CombatIntegrationChecks
{
    public static void Run(Action<bool, string> check)
    {
        Victory(check);
        Defeat(check);
        HeroFall(check, lastEnemyFalls: false);
        HeroFall(check, lastEnemyFalls: true);
        Draw(check, enemyFirst: true);
        Draw(check, enemyFirst: false);
        EnemyRetreat(check);
        RetreatAndWipe(check, retreatFirst: true);
        RetreatAndWipe(check, retreatFirst: false);
        Reinforcement(check);
        NonlethalRemoval(check);
        TemporaryCharm(check);
        SceneChange(check);
        PersonalVisit(check);
        TugKnockdown(check);
        Fright(check);
        HitTimestamp(check);
    }

    private static void HitTimestamp(Action<bool, string> check)
    {
        using var battle = new Fixture();
        var victim = battle.Spawn(Team.Player, "clock victim");
        var attacker = battle.Spawn(Team.Enemy, "clock attacker");
        float previous = Time.time;
        try
        {
            check(victim.LastHitAt == -999f, "unhit warrior has no recent wound");
            Time.time = 12.5f;
            victim.TakeDamage(1f, attacker.gameObject);
            check(victim.LastHitAt == 12.5f, "first hit records current clock");
            Time.time = 18f;
            victim.TakeDamage(1f, attacker.gameObject);
            check(victim.LastHitAt == 18f, "second hit replaces the old wound timestamp");
        }
        finally { Time.time = previous; }
    }

    // 26.11: the fear emotion promised by the emotion weights now actually happens.
    private static void Fright(Action<bool, string> check)
    {
        using var battle = new Fixture();
        var feel = new EmotionSystem();
        EmotionSystem.Instance = feel;
        try
        {
            var warrior = battle.Spawn(Team.Player, "wounded");
            var enemy = battle.Spawn(Team.Enemy, "striker");
            float max = warrior.MaxHP;

            warrior.TakeDamage(max * 0.5f, enemy.gameObject);
            check(feel.Triggered.Count == 0, "a hit that leaves half of health does not frighten");

            warrior.TakeDamage(max * 0.25f, enemy.gameObject);
            check(feel.Triggered.Count == 1 && feel.Triggered[0].Type == EmotionType.Fear &&
                  feel.Triggered[0].Who == warrior.Warrior,
                "the hit that drops a warrior below a third of health frightens him once");

            warrior.TakeDamage(max * 0.05f, enemy.gameObject);
            check(feel.Triggered.Count == 1, "further hits below a third do not stack the fright");

            var doomed = battle.Spawn(Team.Player, "doomed");
            Kill(doomed, enemy);
            check(feel.Triggered.All(t => t.Who != doomed.Warrior), "a killing blow is death, not fright");
        }
        finally { EmotionSystem.Instance = null; }
    }

    private static void Victory(Action<bool, string> check)
    {
        using var battle = new Fixture();
        var survivor = battle.Spawn(Team.Player, "survivor");
        var ally = battle.Spawn(Team.Player, "fallen ally");
        var first = battle.Spawn(Team.Neutral, "first enemy");
        var second = battle.Spawn(Team.Enemy, "second enemy");
        var last = battle.Spawn(Team.Enemy, "last enemy");
        var published = new List<BattleResult>();
        battle.Manager.OnBattleResolved += result => published.Add(result);

        int changes = 0;
        battle.Manager.OnUnitsChanged += () => changes++;
        first.Warrior.Team = Team.Enemy;
        battle.Manager.UpdateTeam(first, Team.Enemy);
        Call(first, "Start");
        battle.Manager.OnUnitKilled(first, survivor.gameObject);
        check(changes == 1 && battle.Manager.GetAliveEnemyCount() == 3 && battle.Manager.NeutralCount == 0 && battle.Deaths.Count == 0,
            "a provoked guard whose Start repeats enemy registration changes the live roster only once");

        Kill(first, survivor);
        Kill(first, survivor);
        battle.Manager.OnUnitKilled(first, survivor.gameObject);
        check(changes == 2 && battle.Deaths.SequenceEqual(new[] { first }) && battle.Manager.BodiesOnField.Count == 1,
            "a lethal hit changes the roster and creates a body once despite false and repeated death reports");

        Kill(ally, second);
        Kill(second, survivor);
        battle.EndFrame();
        check(battle.Hub.Battles.Count == 0, "a living combatant on each side keeps combat open");

        Kill(last, survivor);
        check(battle.Hub.Battles.Count == 0 && LootChain.Shares.Count == 0 && published.Count == 0,
            "the last hit defers the result and loot to frame end");
        battle.EndFrame();
        battle.EndFrame();
        check(battle.Hub.Battles.Count == 1 && HasResult(battle.Hub.Battles[0].Result, BattleOutcome.Victory, 3, 1, ally),
            "victory reports all three slain enemies and the actual fallen ally ID once");
        check(battle.Hub.Battles[0].Survivors.SequenceEqual(new[] { survivor }) && published.Count == 1 &&
            HasResult(published[0], BattleOutcome.Victory, 3, 1, ally),
            "AOS and progression receive the same result once, with only the living ally as witness");
        check(LootChain.Shares.Count == 1 && LootChain.Shares[0].Select(body => body.gameObject.name)
            .SequenceEqual(new[] { "Body_first enemy", "Body_second enemy", "Body_last enemy" }),
            "victory shares only the three enemy bodies produced by its encounter");

        var nextEnemy = battle.Spawn(Team.Enemy, "next wave");
        Kill(nextEnemy, survivor);
        battle.EndFrame();
        check(LootChain.Shares.Count == 2 && LootChain.Shares[1].Select(body => body.gameObject.name)
            .SequenceEqual(new[] { "Body_next wave" }),
            "a later wave never shares intact enemy bodies left on the earlier battlefield");
    }

    private static void Defeat(Action<bool, string> check)
    {
        using var battle = new Fixture();
        var ally = battle.Spawn(Team.Player, "ally");
        battle.EndFrame();
        check(battle.Hub.Battles.Count == 0, "registering a single side cannot end a battle");
        var enemy = battle.Spawn(Team.Enemy, "enemy");

        Kill(ally, enemy);
        battle.EndFrame();
        battle.EndFrame();
        check(battle.Hub.Battles.Count == 1 && HasResult(battle.Hub.Battles[0].Result, BattleOutcome.Defeat, 0, 1, ally) &&
            battle.Hub.Battles[0].Survivors.Length == 0 && LootChain.Shares.Count == 0,
            "losing the last ally publishes exactly one defeat and awards no loot");

        var nextAlly = battle.Spawn(Team.Player, "new ally");
        Kill(enemy, nextAlly);
        battle.EndFrame();
        check(battle.Hub.Battles.Count == 2 && HasResult(battle.Hub.Battles[1].Result, BattleOutcome.Victory, 1, 0),
            "the next encounter starts with fresh casualty totals");
    }

    private static void HeroFall(Action<bool, string> check, bool lastEnemyFalls)
    {
        using var battle = new Fixture();
        var hero = battle.Spawn<SinbinderPlayer>(Team.Player, "hero");
        var survivor = battle.Spawn(Team.Player, "ally");
        var enemy = battle.Spawn(Team.Enemy, "enemy");

        Kill(hero, enemy);
        if (lastEnemyFalls) Kill(enemy, survivor);
        battle.EndFrame();
        check(battle.Hub.Battles.Count == 1 &&
            HasResult(battle.Hub.Battles[0].Result, BattleOutcome.Defeat, lastEnemyFalls ? 1 : 0, 1, hero) &&
            battle.Hub.Battles[0].Survivors.SequenceEqual(new[] { survivor }) && LootChain.Shares.Count == 0,
            lastEnemyFalls ? "losing the hero and last foe is a defeat when an ally survives"
                : "the hero's permanent death defeats a solo encounter even with one surviving ally");
    }

    private static void Draw(Action<bool, string> check, bool enemyFirst)
    {
        using var battle = new Fixture();
        var ally = battle.Spawn(Team.Player, "ally");
        var enemy = battle.Spawn(Team.Enemy, "enemy");
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

        string first = enemyFirst ? "enemy first" : "ally first";
        check(battle.Hub.Battles.Count == 0, $"same-frame deaths wait for the frame end ({first})");
        battle.EndFrame();
        battle.EndFrame();
        check(battle.Hub.Battles.Count == 1 && HasResult(battle.Hub.Battles[0].Result, BattleOutcome.Draw, 1, 1, ally) &&
            battle.Hub.Battles[0].Survivors.Length == 0 && LootChain.Shares.Count == 0,
            $"the frame produces one draw with both casualties and no loot ({first})");
    }

    private static void Reinforcement(Action<bool, string> check)
    {
        using var battle = new Fixture();
        var ally = battle.Spawn(Team.Player, "ally");
        var enemy = battle.Spawn(Team.Enemy, "enemy");
        Kill(enemy, ally);
        var reinforcement = battle.Spawn(Team.Enemy, "reinforcement");
        battle.EndFrame();
        check(battle.Hub.Battles.Count == 0, "a reinforcement arriving within the last-hit frame keeps the battle open");
        Kill(reinforcement, ally);
        battle.EndFrame();
        check(battle.Hub.Battles.Count == 1 && HasResult(battle.Hub.Battles[0].Result, BattleOutcome.Victory, 2, 0),
            "the final victory includes deaths from before the reinforcement arrived");
    }

    private static void EnemyRetreat(Action<bool, string> check)
    {
        using var battle = new Fixture();
        var ally = battle.Spawn(Team.Player, "ally");
        var neutral = battle.Spawn(Team.Neutral, "neutral");
        var slain = battle.Spawn(Team.Enemy, "slain enemy");
        var fleeing = battle.Spawn(Team.Enemy, "fleeing enemy");
        Kill(slain, ally);

        int changes = 0;
        battle.Manager.OnUnitsChanged += () => changes++;
        battle.Manager.ReportEnemyRetreat(null);
        battle.Manager.ReportEnemyRetreat(neutral);
        battle.Manager.ReportEnemyRetreat(slain);
        fleeing.gameObject.SetActive(false);
        battle.Manager.ReportEnemyRetreat(fleeing);
        fleeing.gameObject.SetActive(true);
        battle.EndFrame();
        check(changes == 0 && battle.Manager.GetAliveEnemyCount() == 1 && battle.Hub.Battles.Count == 0,
            "an absent, neutral, inactive or dead unit cannot report an enemy retreat");

        battle.Manager.ReportEnemyRetreat(fleeing);
        Kill(fleeing, ally);
        battle.Manager.RegisterEnemyUnit(fleeing);
        check(changes == 1 && !fleeing.IsDead && battle.Manager.GetAliveEnemyCount() == 0 &&
            battle.Deaths.SequenceEqual(new[] { slain }) && battle.Manager.BodiesOnField.Count == 1,
            "a confirmed retreat cannot be reversed by a late hit or registration before Unity destroys the unit");
        battle.Manager.ReportEnemyRetreat(fleeing);
        Call(fleeing, "OnDestroy");
        check(changes == 1 && !fleeing.IsDead && battle.Manager.GetAliveEnemyCount() == 0 && battle.Hub.Battles.Count == 0,
            "the final live enemy's retreat unregisters it once and defers the outcome until frame end");
        battle.EndFrame();
        check(battle.Hub.Battles.Count == 1 && HasResult(battle.Hub.Battles[0].Result, BattleOutcome.Victory, 1, 0) &&
            battle.Deaths.SequenceEqual(new[] { slain }) && LootChain.Shares.Count == 1 &&
            LootChain.Shares[0].Length == 1 && LootChain.Shares[0][0].gameObject.name == "Body_slain enemy",
            "a victory by retreat counts and loots only the opponent that actually died");

        var nextEnemy = battle.Spawn(Team.Enemy, "next fleeing enemy");
        battle.Manager.ReportEnemyRetreat(fleeing);
        battle.EndFrame();
        check(battle.Hub.Battles.Count == 1, "a stale retreat report cannot finish the next wave");
        battle.Manager.ReportEnemyRetreat(nextEnemy);
        var reinforcement = battle.Spawn(Team.Enemy, "reinforcement after retreat");
        battle.EndFrame();
        check(battle.Hub.Battles.Count == 1, "a reinforcement cancels a pending retreat victory in the same frame");
        battle.Manager.ReportEnemyRetreat(reinforcement);
        battle.EndFrame();
        battle.EndFrame();
        check(battle.Hub.Battles.Count == 2 && HasResult(battle.Hub.Battles[1].Result, BattleOutcome.Victory, 0, 0) &&
            battle.Deaths.Count == 1 && battle.Manager.BodiesOnField.Count == 1 &&
            LootChain.Shares.Count == 2 && LootChain.Shares[1].Length == 0,
            "a pure retreat can end a later wave without kills, new corpses or loot");
    }

    private static void RetreatAndWipe(Action<bool, string> check, bool retreatFirst)
    {
        using var battle = new Fixture();
        var ally = battle.Spawn(Team.Player, "ally");
        var enemy = battle.Spawn(Team.Enemy, "enemy");
        if (retreatFirst)
        {
            battle.Manager.ReportEnemyRetreat(enemy);
            Kill(ally, enemy);
        }
        else
        {
            Kill(ally, enemy);
            battle.Manager.ReportEnemyRetreat(enemy);
        }

        battle.EndFrame();
        check(battle.Hub.Battles.Count == 1 && HasResult(battle.Hub.Battles[0].Result, BattleOutcome.Defeat, 0, 1, ally) &&
            battle.Deaths.SequenceEqual(new[] { ally }) && LootChain.Shares.Count == 0,
            retreatFirst ? "the allied wipe after the last enemy flees is a defeat"
                : "an enemy fleeing after the allied wipe does not change the defeat");
    }

    private static void NonlethalRemoval(Action<bool, string> check)
    {
        using var battle = new Fixture();
        var ally = battle.Spawn(Team.Player, "ally");
        var enemy = battle.Spawn(Team.Enemy, "enemy");
        var other = battle.Spawn(Team.Enemy, "other");
        Kill(other, ally);
        battle.EndFrame();

        enemy.gameObject.SetActive(false);
        battle.EndFrame();
        enemy.gameObject.SetActive(true);
        enemy.Warrior.Team = Team.Neutral;
        battle.Manager.UpdateTeam(enemy, Team.Neutral);
        battle.EndFrame();
        enemy.Warrior.Team = Team.Enemy;
        battle.Manager.UpdateTeam(enemy, Team.Enemy);
        Call(enemy, "OnDestroy");
        battle.EndFrame();

        check(battle.Hub.Battles.Count == 0 && battle.Deaths.SequenceEqual(new[] { other }) && LootChain.Shares.Count == 0,
            "deactivation, changing teams and despawning cannot turn a past casualty into a victory");
    }

    private static void SceneChange(Action<bool, string> check)
    {
        using var battle = new Fixture();
        var ally = battle.Spawn(Team.Player, "old ally");
        var enemy = battle.Spawn(Team.Enemy, "old enemy");
        Kill(enemy, ally);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        // The old ally can remain registered until Unity finishes destroying the outgoing scene.
        battle.EndFrame();
        check(battle.Hub.Battles.Count == 0 && LootChain.Shares.Count == 0,
            "a fresh instance of the same scene discards the pending result from the previous encounter");
    }

    private static void TemporaryCharm(Action<bool, string> check)
    {
        using var battle = new Fixture();
        var ally = battle.Spawn(Team.Player, "ally");
        var charmed = battle.Spawn(Team.Enemy, "temporarily charmed enemy");
        var other = battle.Spawn(Team.Enemy, "other enemy");
        charmed.Warrior.Team = Team.Player;

        Kill(other, ally);
        battle.EndFrame();
        check(battle.Hub.Battles.Count == 0 && battle.Manager.GetAliveEnemyCount() == 1,
            "a temporary Charm does not remove a registered enemy or end combat early");

        Kill(charmed, ally);
        battle.EndFrame();
        check(battle.Hub.Battles.Count == 1 && HasResult(battle.Hub.Battles[0].Result, BattleOutcome.Victory, 2, 0),
            "a charmed foe still counts on the registered enemy side if killed before Charm ends");
    }

    private static void PersonalVisit(Action<bool, string> check)
    {
        using var battle = new Fixture();
        var home = battle.Spawn(Team.Player, "ally in the crypt");
        var other = battle.Spawn(Team.Enemy, "unrelated enemy");
        var visit = new PersonalExpedition();
        PersonalExpedition.DialogueVisit = visit;
        var traveller = battle.Spawn(Team.Player, "traveller", w => visit.Travellers.Add(w));
        var guard = battle.Spawn(Team.Enemy, "expedition guard", w => visit.Guards.Add(w));
        var converted = battle.Spawn(Team.Enemy, "converted guard", w => visit.Guards.Add(w));
        converted.Warrior.Team = Team.Player;
        battle.Manager.UpdateTeam(converted, Team.Player);

        Kill(guard, traveller);
        battle.EndFrame();
        check(battle.Hub.Battles.Count == 1 && HasResult(battle.Hub.Battles[0].Result, BattleOutcome.Victory, 1, 0) &&
            battle.Hub.Battles[0].Survivors.SequenceEqual(new[] { traveller, converted }) && !home.IsDead && !other.IsDead,
            "expedition victory counts its actual foes and includes the traveller and converted guard as witnesses");

        var closingVisit = new PersonalExpedition();
        PersonalExpedition.DialogueVisit = closingVisit;
        closingVisit.Travellers.Add(traveller.Warrior);
        var lastGuard = battle.Spawn(Team.Enemy, "last guard before return", w => closingVisit.Guards.Add(w));
        Kill(lastGuard, traveller);
        PersonalExpedition.DialogueVisit = null;
        battle.EndFrame();
        check(battle.Hub.Battles.Count == 1 && LootChain.Shares.Count == 1,
            "exiting a personal map before frame end cannot publish that visit's result in the crypt");

        var nextVisit = new PersonalExpedition();
        PersonalExpedition.DialogueVisit = nextVisit;
        nextVisit.Travellers.Add(traveller.Warrior);
        var nextGuard = battle.Spawn(Team.Enemy, "another guard", w => nextVisit.Guards.Add(w));
        Kill(traveller, nextGuard);
        battle.EndFrame();
        check(battle.Hub.Battles.Count == 2 && HasResult(battle.Hub.Battles[1].Result, BattleOutcome.Defeat, 0, 1, traveller) &&
            battle.Hub.Battles[1].Survivors.Length == 0,
            "returning to a personal map starts fresh and allies left in the crypt cannot prevent defeat");
    }

    private static void TugKnockdown(Action<bool, string> check)
    {
        using var battle = new Fixture();
        Lords.Active = true;
        var lord = battle.Spawn(Team.Player, "lord", w => w.IsLord = true);
        var enemy = battle.Spawn(Team.Enemy, "hunter");

        Kill(lord, enemy);
        battle.EndFrame();
        check(TugMatch.Knockdowns.SequenceEqual(new[] { lord.Warrior }) && battle.Deaths.Count == 0 &&
            battle.Manager.BodiesOnField.Count == 0 && battle.Hub.Battles.Count == 0,
            "a Tug lord's knockdown produces neither a permanent casualty nor combat defeat");

        var ally = battle.Spawn(Team.Player, "ally");
        Kill(enemy, ally);
        battle.EndFrame();
        check(battle.Hub.Battles.Count == 1 && HasResult(battle.Hub.Battles[0].Result, BattleOutcome.Victory, 1, 0),
            "a witnessed Tug hunter wave still earns victory without counting the lord as lost");
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
        private int _nextId;

        public Fixture()
        {
            Hub = new AOSEventHub();
            AOSEventHub.Instance = Hub;
            Manager = new GameObject("Combat").AddComponent<CombatManager>();
            Call(Manager, "Awake");
            Manager.OnAnyDeath += (victim, _) => Deaths.Add(victim);
        }

        public CombatManager Manager { get; }
        public AOSEventHub Hub { get; }
        public List<Damageable> Deaths { get; } = new();

        public Damageable Spawn(Team team, string name, Action<Warrior> prepare = null)
            => Spawn<Warrior>(team, name, prepare);

        public Damageable Spawn<T>(Team team, string name, Action<Warrior> prepare = null) where T : Warrior, new()
        {
            var unit = new GameObject(name);
            var warrior = unit.AddComponent<T>();
            warrior.Team = team;
            warrior.Id = $"fixture-warrior-{++_nextId}";
            prepare?.Invoke(warrior);
            var body = unit.AddComponent<Damageable>();
            Call(body, "Awake");
            Call(body, "Start");
            return body;
        }

        public void EndFrame() => Call(Manager, "LateUpdate");

        public void Dispose()
        {
            // Unity's destroyed-object null semantics are outside this harness.
            typeof(CombatManager).GetProperty(nameof(CombatManager.Instance)).SetValue(null, null);
            AOSEventHub.Instance = null;
            Lords.Active = false;
            LootChain.Shares.Clear();
            PersonalExpedition.DialogueVisit = null;
            TugMatch.Knockdowns.Clear();
        }
    }
}
