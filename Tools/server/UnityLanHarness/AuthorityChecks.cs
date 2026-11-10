using System;
using Sinbinder.Multiplayer;
using Sinbinder.Gameplay;
using UnityEngine;

internal static class AuthorityChecks
{
    private static void Require(bool okay, string description)
    {
        if (!okay) throw new Exception("AUTHORITY TEST FAILED: " + description);
    }

    private static NetworkOrderIntent Intent(string match, long id,
        long actor, NetworkOrderKind kind,
        float x = 0, float z = 0, long target = 0)
    {
        return new NetworkOrderIntent {
            Protocol = MatchAuthority.Protocol, MatchId = match,
            CommandId = id, ActorId = actor, Kind = kind,
            X = x, Y = 0, Z = z, TargetId = target
        };
    }

    private static void Reject(MatchAuthority authority, int conn,
        NetworkOrderIntent order, OrderRejection wanted,
        Func<long, long, bool> visible = null)
    {
        bool accepted = authority.TryApprove(conn, order, 7, visible,
            out _, out OrderRejection actual);
        Require(!accepted && actual == wanted,
            "expected " + wanted + ", got " + actual);
    }

    public static void Run()
    {
        TestIdentityAndReplay();
        TestUnityWarriorOrderBridge();
        Console.WriteLine("PASS: server-owned IDs, dual-Lord neutral AOS proposals, non-spoofable commands and real-Warrior bridge");
    }

    private static void TestIdentityAndReplay()
    {
        var match = new MatchAuthority("authority-qa", 120f, 2);
        match.RegisterPlayer(101, 0, "Греховод Первый");
        match.RegisterPlayer(202, 1, "Греховод Второй");

        long firstLord = match.RegisterActor(NetworkActorRole.Lord, 101);
        long secondLord = match.RegisterActor(NetworkActorRole.Lord, 202);
        long neutral = match.RegisterActor(NetworkActorRole.NeutralWarrior);
        long ally = match.RegisterActor(NetworkActorRole.AffiliatedWarrior, 101);
        Require(firstLord != secondLord && secondLord != neutral &&
            neutral != ally, "server-generated actor IDs must be unique");

        var first = Intent("authority-qa", 1, firstLord,
            NetworkOrderKind.Move, 6, 9);
        Require(match.TryApprove(101, first, 10, null, out var executed,
            out var rejection) && rejection == OrderRejection.None,
            "valid first-Lord movement");
        Require(executed.ExecuteTick == 13 &&
            executed.Lord == "Греховод Первый" &&
            executed.ConnectionId == 101 && executed.ActorId == firstLord,
            "approved order must contain server Lord, connection and tick");
        long sequence = match.LastSequence;

        // CLIENT CANNOT CHOOSE WHO THEY ARE. In this API the connection ID
        // comes from the authenticated transport, not from input fields.
        Reject(match, 101, Intent("authority-qa", 2, secondLord,
            NetworkOrderKind.Move), OrderRejection.NotAllowed);
        Reject(match, 202, Intent("authority-qa", 1, ally,
            NetworkOrderKind.Move), OrderRejection.NotAllowed);
        Reject(match, 101, first, OrderRejection.StaleOrDuplicate);
        Reject(match, 101, Intent("other-game", 9, firstLord,
            NetworkOrderKind.Move), OrderRejection.WrongMatch);
        Reject(match, 303, Intent("authority-qa", 1, neutral,
            NetworkOrderKind.Move), OrderRejection.UnknownPlayer);

        var wrongProtocol = Intent("authority-qa", 2, firstLord,
            NetworkOrderKind.Move);
        wrongProtocol.Protocol = 999;
        Reject(match, 101, wrongProtocol, OrderRejection.WrongProtocol);

        var invalid = Intent("authority-qa", 2, firstLord,
            NetworkOrderKind.Move, float.NaN);
        Reject(match, 101, invalid, OrderRejection.InvalidPoint);
        invalid.X = float.PositiveInfinity;
        Reject(match, 101, invalid, OrderRejection.InvalidPoint);
        invalid.X = 121f;
        Reject(match, 101, invalid, OrderRejection.InvalidPoint);
        invalid.X = 1f;
        invalid.Kind = (NetworkOrderKind)999;
        Reject(match, 101, invalid, OrderRejection.UnsupportedOrder);

        // Neutrality is NOT ownership: either Lord can propose orders,
        // and the 13 AOS modules later decide whether the warrior obeys.
        Require(match.TryApprove(101, Intent("authority-qa", 2, neutral,
            NetworkOrderKind.Move, 7, 8), 11, null,
            out var one, out _), "first Lord can speak to neutral");
        Require(match.TryApprove(202, Intent("authority-qa", 1, neutral,
            NetworkOrderKind.FallBack, 4, 3), 11, null,
            out var two, out _), "second Lord can speak to same neutral");
        Require(one.Lord != two.Lord && one.Sequence < two.Sequence,
            "distinct speakers and deterministic server order");

        Reject(match, 101, Intent("authority-qa", 3, neutral,
            NetworkOrderKind.Attack), OrderRejection.TargetRequired);
        Reject(match, 101, Intent("authority-qa", 3, neutral,
            NetworkOrderKind.Attack, target: 9_999),
            OrderRejection.UnknownTarget);
        Reject(match, 101, Intent("authority-qa", 3, neutral,
            NetworkOrderKind.Attack, target: secondLord),
            OrderRejection.TargetNotVisible);
        Reject(match, 101, Intent("authority-qa", 3, neutral,
            NetworkOrderKind.Attack, target: secondLord),
            OrderRejection.TargetNotVisible,
            (from, to) => false);
        Require(match.TryApprove(101, Intent("authority-qa", 3, neutral,
            NetworkOrderKind.Attack, target: secondLord), 15,
            (from, to) => to == secondLord,
            out var hit, out _), "server visibility permits attack");
        Require(hit.TargetId == secondLord && hit.ExecuteTick == 18,
            "attack snapshot was recorded by server");

        Reject(match, 101, Intent("authority-qa", 4, neutral,
            NetworkOrderKind.Move, target: secondLord), OrderRejection.NotAllowed);
        Require(match.LastSequence == sequence + 3,
            "denied packets must not consume global server sequence");
        match.RemoveActor(secondLord);
        Reject(match, 101, Intent("authority-qa", 4, neutral,
            NetworkOrderKind.Attack, target: secondLord),
            OrderRejection.UnknownTarget);

        match.SetConnected(202, false);
        Reject(match, 202, Intent("authority-qa", 2, neutral,
            NetworkOrderKind.Move), OrderRejection.UnknownPlayer);
        match.SetConnected(202, true);
        Require(match.TryApprove(202, Intent("authority-qa", 2, neutral,
            NetworkOrderKind.Hold), 16, null, out _, out _),
            "authorized reconnect may resume with newer sequence");

        var nextMatch = new MatchAuthority("authority-qa-next", 120, 2);
        nextMatch.RegisterPlayer(101, 0, "Греховод Первый");
        long newActor = nextMatch.RegisterActor(NetworkActorRole.Lord, 101);
        Reject(nextMatch, 101, Intent("authority-qa", 99, newActor,
            NetworkOrderKind.Move), OrderRejection.WrongMatch);
    }

    private static void TestUnityWarriorOrderBridge()
    {
        var authority = new MatchAuthority("aos-bridge", 120, 2);
        authority.RegisterPlayer(1, 0, "Mòrt");
        authority.RegisterPlayer(2, 1, "Vida");
        var bridge = new ServerWarriorOrderBridge(authority);
        var lord1 = new GameObject("Lord A").AddComponent<Warrior>();
        lord1.Team = Team.Player;
        lord1.TestMover = new UnitMover();
        var lord2 = new GameObject("Lord B").AddComponent<Warrior>();
        lord2.Team = Team.Enemy;
        lord2.TestMover = new UnitMover();
        var neutral = new GameObject("Neutral").AddComponent<Warrior>();
        neutral.Team = Team.Neutral;

        long id1 = bridge.Register(lord1, NetworkActorRole.Lord, 1);
        long id2 = bridge.Register(lord2, NetworkActorRole.Lord, 2);
        long id3 = bridge.Register(neutral, NetworkActorRole.NeutralWarrior);

        Require(!bridge.TryIssue(1, Intent("aos-bridge", 1, id2,
            NetworkOrderKind.Move), 1, null, null,
            out _, out var reason) && reason == OrderRejection.NotAllowed,
            "cannot control enemy Lord directly");

        Require(!bridge.TryIssue(1, Intent("aos-bridge", 1, id3,
            NetworkOrderKind.Move), 1, null, null,
            out _, out reason) && reason == OrderRejection.NotAllowed,
            "voice authority required for a neutral warrior");

        Require(!bridge.TryIssue(1, Intent("aos-bridge", 1, id3,
            NetworkOrderKind.Move), 1, null, (w,l) => 1f,
            out _, out reason) && reason == OrderRejection.NotAllowed,
            "warrior cannot hear too-distant orders");

        Require(bridge.TryIssue(1, Intent("aos-bridge", 1, id3,
            NetworkOrderKind.Move, 4, 9), 2, null,
            (w,l) => 0.2f, out var first, out reason),
            "neutral receives an AOS proposal from first Lord");
        Require(first.Lord == "Mòrt" && neutral.LastLord == "Mòrt" &&
            neutral.LastCommand == CommandKind.Move &&
            Math.Abs(neutral.LastMuffle - 0.2f) < .01,
            "existing Warrior.IssueCommand gets verified Lord + hearing");

        Require(bridge.TryIssue(2, Intent("aos-bridge", 1, id3,
            NetworkOrderKind.Patrol, 3, 5), 3, null,
            (w,l) => 0.3f, out var second, out reason),
            "other Lord can propose patrol to same neutral");
        Require(second.Lord == "Vida" && neutral.LastLord == "Vida" &&
            neutral.LastCommand == CommandKind.Patrol,
            "existing Warrior.IssuePatrol preserves rival Lord identity");

        Require(!bridge.TryIssue(1, Intent("aos-bridge", 2, id3,
            NetworkOrderKind.Attack, target: id2), 4, (a,b) => false,
            (w,l) => 0f, out _, out reason) &&
            reason == OrderRejection.TargetNotVisible,
            "hidden enemy cannot be targeted");
        lord2.transform.position = new Vector3(12f, 0f, -3f);
        Require(bridge.TryIssue(1, Intent("aos-bridge", 2, id3,
            NetworkOrderKind.Attack, 99f, 99f, id2), 4,
            (a,b) => true, (w,l) => 0f, out _, out reason),
            "visible enemy can be target of AOS attack");
        Require(neutral.LastTarget == lord2.gameObject &&
            Math.Abs(neutral.LastPoint.x - 12f) < 0.01f &&
            Math.Abs(neutral.LastPoint.z + 3f) < 0.01f,
            "attack derives real position from SERVER, not packet coordinates");

        Require(bridge.TryIssue(2, Intent("aos-bridge", 2, id2,
            NetworkOrderKind.Move, 5, 7), 4, null, null,
            out _, out reason) &&
            Math.Abs(lord2.TestMover.Destination.x - 5f) < .01,
            "direct Lord movement uses UnitMover without AOS voting");
        bridge.Unregister(id3);
        Require(!bridge.TryIssue(1, Intent("aos-bridge", 3, id3,
            NetworkOrderKind.Move), 5, null, (w,l) => 0f,
            out _, out reason) && reason == OrderRejection.UnknownActor,
            "unregistered/dead actors cannot accept orders");
    }
}
