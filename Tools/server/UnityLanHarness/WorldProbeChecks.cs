using System;
using System.Reflection;
using Sinbinder.Core;
using Sinbinder.Gameplay;
using Sinbinder.Multiplayer;
using UnityEngine;

internal static class WorldProbeChecks
{
    private static void Require(bool condition, string why)
    {
        if (!condition) throw new Exception("World probe regression: " + why);
    }

    private static NetworkOrderIntent Intent(MatchAuthority authority,
        long actorId, long number, float x)
    {
        return new NetworkOrderIntent
        {
            Protocol = MatchAuthority.Protocol,
            MatchId = authority.MatchId,
            ActorId = actorId,
            CommandId = number,
            Kind = NetworkOrderKind.Move,
            X = x, Y = 0f, Z = 5f
        };
    }

    public static void Run()
    {
        TestMapFixture.TwoCrypts();
        var go = new GameObject("Network world probe test");
        var probe = go.AddComponent<ServerWorldProbe>();
        probe.Initialize();

        var hero = new GameObject("Player Lord").AddComponent<SinbinderPlayer>();
        hero.Team = Team.Player;
        hero.TestMover = new UnitMover();

        var rival = new GameObject("Other Lord").AddComponent<Warrior>();
        rival.Team = Team.Neutral; // Actual RivalLord is also Team.Neutral!
        rival.TestMover = new UnitMover();
        var rivalAI = rival.gameObject.AddComponent<RivalLord>();
        rivalAI.Side = 1;

        var neutral = new GameObject("Neutral Resident").AddComponent<Warrior>();
        neutral.Team = Team.Neutral;

        probe.Scan(new Warrior[] { hero, rival, neutral });
        Require(probe.RegisteredCount == 3, "register actual hero, rival and neutral");
        Require(!rivalAI.NetworkControlled,
            "local probe without a network room MUST preserve solo rival AI");
        long heroId = hero.GetComponent<NetworkActorIdentity>().ServerId;
        long rivalId = rival.GetComponent<NetworkActorIdentity>().ServerId;
        long neutralId = neutral.GetComponent<NetworkActorIdentity>().ServerId;
        Require(heroId != rivalId && rivalId != neutralId, "unique runtime entity IDs");

        int applied = 0, denied = 0;
        probe.OrderResolved += (_, success) =>
        {
            if (success) applied++;
            else denied++;
        };
        Require(probe.TryIssueFromConnection(1,
                Intent(probe.Authority, neutralId, 1, 7f), null,
                out var accepted, out var reason),
            "server world accepts neutral proposal from first Lord: " + reason);
        Require(accepted.Lord == "Греховод стороны 1" &&
            accepted.ExecuteTick == 3,
            "registered Lord controls voice and tick from server, not client");
        probe.Advance(0.10f);
        Require(neutral.LastCommand == CommandKind.None &&
            probe.Orders.PendingOrders == 1, "no early AOS execution");
        probe.Advance(0.05f);
        Require(neutral.LastCommand == CommandKind.Move &&
            neutral.LastLord == "Греховод стороны 1" && applied == 1,
            "real gameplay Warrior gets AOS proposal on authoritative tick");

        Require(probe.TryIssueFromConnection(2,
                Intent(probe.Authority, neutralId, 1, -4f), null,
                out accepted, out reason),
            "other Lord is permitted to propose neutral order");
        probe.Advance(0.15f);
        Require(neutral.LastLord == "Греховод стороны 2" && applied == 2,
            "same live neutral Warrior hears both server-side Lords");

        Require(!probe.TryIssueFromConnection(1,
                Intent(probe.Authority, rivalId, 2, 2f), null,
                out _, out reason) &&
            reason == OrderRejection.NotAllowed,
            "one Lord cannot drive the other Lord's body");

        // Simulate a death, re-spawn and cleanup by scanning live Warriors.
        hero.IsDead = true;
        neutral.IsDead = true;
        probe.Scan(new Warrior[] { hero, rival, neutral });
        Require(probe.RegisteredCount == 1 &&
            !hero.GetComponent<NetworkActorIdentity>().Assigned &&
            !neutral.GetComponent<NetworkActorIdentity>().Assigned,
            "dead actors are evicted and IDs cleared");

        var replacement = new GameObject("Resurrected Resident").AddComponent<Warrior>();
        replacement.Team = Team.Neutral;
        probe.Scan(new Warrior[] { rival, replacement });
        Require(probe.RegisteredCount == 2 &&
            replacement.GetComponent<NetworkActorIdentity>().ServerId > neutralId,
            "replacement warrior gets new server ID rather than reusing a corpse's");
        Require(denied == 0, "all scheduled world commands executed");
        var method = typeof(ServerWorldProbe).GetMethod("OnDestroy",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Require(method != null, "probe cleanup hook exists");
        method.Invoke(probe, null);
        Require(!replacement.GetComponent<NetworkActorIdentity>().Assigned &&
            !rival.GetComponent<NetworkActorIdentity>().Assigned,
            "probe teardown clears real-world actor IDs");
        Console.WriteLine("PASS: real-world Warrior discovery, dual Lords, AOS tick, death and identity cleanup");
    }
}
