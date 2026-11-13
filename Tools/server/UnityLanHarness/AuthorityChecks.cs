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
        TestListenerFailureIsolation();
        Console.WriteLine("PASS: server-owned IDs, dual-Lord neutral AOS proposals, non-spoofable commands and real-Warrior bridge");
    }

    private static void TestListenerFailureIsolation()
    {
        var authority = new MatchAuthority("listener-isolation", 120f, 2);
        authority.RegisterPlayer(1, 0, "Mòrt");
        var bridge = new ServerWarriorOrderBridge(authority);
        var lord = new GameObject("Listener Lord").AddComponent<Warrior>();
        lord.Team = Team.Player;
        lord.TestMover = new UnitMover();
        long actor = bridge.Register(lord, NetworkActorRole.Lord, 1);
        int failedListenerCalls = 0, healthyCalls = 0;
        bridge.OrderResolved += (order, applied) => {
            failedListenerCalls++;
            throw new InvalidOperationException("test listener failure");
        };
        bridge.OrderResolved += (order, applied) => {
            healthyCalls++;
            Require(applied, "scheduled valid Lord movement must apply");
        };
        Require(bridge.TryIssue(1, Intent("listener-isolation", 1, actor,
            NetworkOrderKind.Move, 3f, 4f), 5, null, null,
            out var first, out _), "first same-tick order accepted");
        Require(bridge.TryIssue(1, Intent("listener-isolation", 2, actor,
            NetworkOrderKind.Move, 8f, 9f), 5, null, null,
            out var second, out _), "second same-tick order accepted");
        Require(first.ExecuteTick == second.ExecuteTick &&
            bridge.PendingOrders == 2, "two orders share scheduled bucket");
        bridge.ProcessDue(first.ExecuteTick);
        Require(failedListenerCalls == 2 && healthyCalls == 2 &&
            bridge.PendingOrders == 0 &&
            Math.Abs(lord.TestMover.Destination.x - 8f) < 0.01f,
            "listener exception cannot drop later order or healthy subscriber");

        // Negative teardown: exceptions must not strand pending orders.
        int cancelled = 0;
        // A Lord is enough: no AOS or fog callbacks needed.
        var teardownAuthority = new MatchAuthority("listener-teardown-2", 120f, 2);
        teardownAuthority.RegisterPlayer(1, 0, "Mòrt");
        var teardown = new ServerWarriorOrderBridge(teardownAuthority);
        var secondLord = new GameObject("Teardown Lord").AddComponent<Warrior>();
        secondLord.Team = Team.Player;
        secondLord.TestMover = new UnitMover();
        long teardownActor = teardown.Register(secondLord, NetworkActorRole.Lord, 1);
        teardown.OrderResolved += (order, applied) =>
            { throw new InvalidOperationException("teardown listener failure"); };
        teardown.OrderResolved += (order, applied) => {
            Require(!applied, "cancelled orders must report false");
            cancelled++;
        };
        for (long id = 1; id <= 2; id++)
            Require(teardown.TryIssue(1, Intent("listener-teardown-2", id,
                teardownActor, NetworkOrderKind.Move, id, 0f), 5, null, null,
                out _, out _), "teardown order accepted");
        teardown.CancelPending();
        Require(cancelled == 2 && teardown.PendingOrders == 0,
            "teardown notifies every order and clears pending despite listener exceptions");
        teardown.CancelPending();
        Require(cancelled == 2, "repeat teardown must not re-notify old orders");

        // Negative regression: teardown from a resolved listener must stop
        // the remaining orders of the already-dequeued execution bucket.
        var reentrantAuthority = new MatchAuthority("reentrant-cancel", 120f, 2);
        reentrantAuthority.RegisterPlayer(1, 0, "Mòrt");
        var reentrant = new ServerWarriorOrderBridge(reentrantAuthority);
        var reentrantLord = new GameObject("Reentrant Lord").AddComponent<Warrior>();
        reentrantLord.Team = Team.Player;
        reentrantLord.TestMover = new UnitMover();
        long reentrantActor = reentrant.Register(reentrantLord, NetworkActorRole.Lord, 1);
        int completed = 0, stopped = 0;
        reentrant.OrderResolved += (order, applied) =>
        {
            if (applied)
            {
                completed++;
                reentrant.CancelPending();
            }
            else
            {
                stopped++;
                // Nested teardown while notifying a cancelled active order.
                reentrant.CancelPending();
            }
        };
        Require(reentrant.TryIssue(1, Intent("reentrant-cancel", 1, reentrantActor,
            NetworkOrderKind.Move, 3f, 0f), 5, null, null,
            out var reentrantFirst, out _), "first reentrant order accepted");
        Require(reentrant.TryIssue(1, Intent("reentrant-cancel", 2, reentrantActor,
            NetworkOrderKind.Move, 9f, 0f), 5, null, null,
            out var reentrantSecond, out _), "second reentrant order accepted");
        Require(reentrantFirst.ExecuteTick == reentrantSecond.ExecuteTick,
            "reentrant orders share tick");
        reentrant.ProcessDue(reentrantFirst.ExecuteTick);
        Require(completed == 1 && stopped == 1 && reentrant.PendingOrders == 0 &&
            Math.Abs(reentrantLord.TestMover.Destination.x - 3f) < 0.01f,
            "reentrant teardown must cancel second order without negative pending count");
        reentrant.CancelPending();
        Require(completed == 1 && stopped == 1,
            "repeat reentrant teardown cannot duplicate notifications");
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
        int appliedOrders = 0, cancelledOrders = 0;
        bridge.OrderResolved += (order, applied) =>
        {
            if (applied) appliedOrders++;
            else cancelledOrders++;
        };
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
        var identity = neutral.GetComponent<NetworkActorIdentity>();
        Require(identity != null && identity.Assigned &&
            identity.MatchId == "aos-bridge" && identity.ServerId == id3,
            "server ID must be bound directly to actual Unity Warrior object");
        bool rejectedRebind = false;
        try { identity.BindServerIdentity(id3 + 999, "aos-bridge"); }
        catch (InvalidOperationException) { rejectedRebind = true; }
        Require(rejectedRebind, "client-side identity reassignment is forbidden");

        // Adversarial privacy regression: an unauthorized Lord must not
        // distinguish a real hidden target from an absent target by response.
        int visibilityProbes = 0;
        Func<Warrior, Warrior, bool> countVisibility = (a, b) => {
            visibilityProbes++;
            return false;
        };
        Require(!bridge.TryIssue(1, Intent("aos-bridge", 1, id2,
            NetworkOrderKind.Attack, target: id3), 1, countVisibility, null,
            out _, out var privacyReason) && privacyReason == OrderRejection.NotAllowed,
            "foreign Lord must not learn that attack target exists");
        Require(!bridge.TryIssue(1, Intent("aos-bridge", 1, id2,
            NetworkOrderKind.Attack, target: 999999), 1, countVisibility, null,
            out _, out privacyReason) && privacyReason == OrderRejection.NotAllowed,
            "foreign Lord must get same denial for nonexistent target");
        Require(visibilityProbes == 0 && bridge.PendingOrders == 0 &&
            authority.LastAcceptedCommandId(1) == 0,
            "unauthorized attacks cannot query visibility or consume commands");

        // Invalid protocol/match/replay must be rejected before target probes,
        // regardless of whether the target exists or is hidden.
        foreach (int variant in new[] { 0, 1, 2 })
        {
            foreach (long targetId in new[] { id2, 999999L })
            {
                var invalidEnvelope = Intent("aos-bridge", variant == 2 ? 0 : 1,
                    id1, NetworkOrderKind.Attack, target: targetId);
                OrderRejection expected;
                if (variant == 0)
                {
                    invalidEnvelope.Protocol = MatchAuthority.Protocol + 1;
                    expected = OrderRejection.WrongProtocol;
                }
                else if (variant == 1)
                {
                    invalidEnvelope.MatchId = "other-match";
                    expected = OrderRejection.WrongMatch;
                }
                else expected = OrderRejection.StaleOrDuplicate;
                Require(!bridge.TryIssue(1, invalidEnvelope, 1,
                    countVisibility, null, out _, out var denied) && denied == expected,
                    "invalid envelope must reject before target lookup: " + variant);
            }
        }
        Require(visibilityProbes == 0 && bridge.PendingOrders == 0 &&
            authority.LastAcceptedCommandId(1) == 0,
            "invalid envelopes cannot probe fog or consume replay cursor");

        // Invalid order shapes must fail before target existence/visibility.
        // Both a real hidden target and an absent target give the same denial.
        foreach (long targetId in new[] { id2, 999999L })
        {
            var unsupported = Intent("aos-bridge", 1, id1,
                (NetworkOrderKind)999, target: targetId);
            Require(!bridge.TryIssue(1, unsupported, 1, countVisibility, null,
                out _, out var malformedReason) &&
                malformedReason == OrderRejection.UnsupportedOrder,
                "unsupported kind must not probe hidden target");

            var nonFinite = Intent("aos-bridge", 1, id1,
                NetworkOrderKind.Attack, float.NaN, target: targetId);
            Require(!bridge.TryIssue(1, nonFinite, 1, countVisibility, null,
                out _, out malformedReason) &&
                malformedReason == OrderRejection.InvalidPoint,
                "nonfinite attack must not probe hidden target");

            var outOfBounds = Intent("aos-bridge", 1, id1,
                NetworkOrderKind.Attack, 121f, target: targetId);
            Require(!bridge.TryIssue(1, outOfBounds, 1, countVisibility, null,
                out _, out malformedReason) &&
                malformedReason == OrderRejection.InvalidPoint,
                "out-of-bounds attack must not probe hidden target");
        }
        Require(visibilityProbes == 0 && bridge.PendingOrders == 0 &&
            authority.LastAcceptedCommandId(1) == 0,
            "malformed orders cannot query fog or consume replay cursor");

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
        Require(neutral.LastCommand == CommandKind.None &&
            bridge.PendingOrders == 1,
            "approved AOS orders must NOT execute on receipt");
        bridge.ProcessDue(4);
        Require(neutral.LastCommand == CommandKind.None,
            "AOS command must not execute before authoritative server tick");
        bridge.ProcessDue(5);
        Require(first.Lord == "Mòrt" && neutral.LastLord == "Mòrt" &&
            neutral.LastCommand == CommandKind.Move &&
            Math.Abs(neutral.LastMuffle - 0.2f) < .01,
            "server tick executes existing Warrior.IssueCommand");

        Require(bridge.TryIssue(2, Intent("aos-bridge", 1, id3,
            NetworkOrderKind.Patrol, 3, 5), 3, null,
            (w,l) => 0.3f, out var second, out reason),
            "other Lord can propose patrol to same neutral");
        Require(second.Lord == "Vida" && neutral.LastLord == "Mòrt",
            "rival Lord order stays queued until its tick");
        bridge.ProcessDue(6);
        Require(neutral.LastLord == "Vida" &&
            neutral.LastCommand == CommandKind.Patrol,
            "existing Warrior.IssuePatrol executes on tick and keeps rival Lord");

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
        Require(neutral.LastTarget == null,
            "accepted attack must not be applied before its server tick");
        bridge.ProcessDue(7);
        Require(neutral.LastTarget == lord2.gameObject &&
            Math.Abs(neutral.LastPoint.x - 12f) < 0.01f &&
            Math.Abs(neutral.LastPoint.z + 3f) < 0.01f,
            "tick-executed attack derives real SERVER position, not client coordinates");

        Require(bridge.TryIssue(2, Intent("aos-bridge", 2, id2,
            NetworkOrderKind.Move, 5, 7), 4, null, null,
            out _, out reason) &&
            Math.Abs(lord2.TestMover.Destination.x) < .01,
            "direct Lord movement is queued without AOS");
        bridge.ProcessDue(7);
        Require(Math.Abs(lord2.TestMover.Destination.x - 5f) < .01,
            "direct Lord moves on authoritative server tick");
        Require(appliedOrders == 4 && cancelledOrders == 0,
            "all four accepted orders execute exactly once");
        Require(bridge.TryIssue(1, Intent("aos-bridge", 3, id3,
            NetworkOrderKind.Move, 2f, 4f), 8, null,
            (w,l) => 0f, out _, out reason),
            "pending order accepted before target is unregistered");
        bridge.Unregister(id3);
        bridge.ProcessDue(11);
        Require(cancelledOrders == 1 && appliedOrders == 4,
            "unregistered actor's pending order must be cancelled");

        // A command accepted while connected must not fire after that
        // player loses their network session before ExecuteTick.
        Require(bridge.TryIssue(2, Intent("aos-bridge", 3, id2,
            NetworkOrderKind.Move, 17f, 4f), 15, null, null,
            out _, out reason), "connected Lord can queue movement");
        authority.SetConnected(2, false);
        bridge.ProcessDue(18);
        Require(cancelledOrders == 2 && appliedOrders == 4 &&
            Math.Abs(lord2.TestMover.Destination.x - 5f) < .01,
            "disconnect cancels queued Lord movement");
        authority.SetConnected(2, true);
        Require(bridge.PendingOrders == 0,
            "server queue drained at scheduled tick");
        // Negative regression: disconnect AND reconnect before ExecuteTick.
        // The new socket session must not resurrect the old accepted intent.
        Require(bridge.TryIssue(2, Intent("aos-bridge", 4, id2,
            NetworkOrderKind.Move, 31f, 4f), 20, null, null,
            out var stale, out reason), "old session can queue an order");
        authority.SetConnected(2, false);
        authority.SetConnected(2, true);
        Require(!authority.IsCurrentSession(2, stale.SessionEpoch),
            "reconnect invalidates previous session epoch");
        bridge.ProcessDue(23);
        Require(cancelledOrders == 3 && appliedOrders == 4 &&
            Math.Abs(lord2.TestMover.Destination.x - 5f) < .01,
            "reconnected Lord must not execute old session order");

        // Positive regression: a newly accepted order from the resumed
        // session executes normally; replay cursor is not reset.
        Require(bridge.TryIssue(2, Intent("aos-bridge", 5, id2,
            NetworkOrderKind.Move, 23f, 4f), 24, null, null,
            out var resumed, out reason) &&
            resumed.SessionEpoch != stale.SessionEpoch,
            "resumed session accepts only a fresh command ID");
        bridge.ProcessDue(27);
        Require(appliedOrders == 5 && cancelledOrders == 3 &&
            Math.Abs(lord2.TestMover.Destination.x - 23f) < .01,
            "fresh resumed-session movement executes exactly once");
        Require(!bridge.TryIssue(2, Intent("aos-bridge", 4, id2,
            NetworkOrderKind.Move), 28, null, null,
            out _, out reason) && reason == OrderRejection.StaleOrDuplicate,
            "reconnect does not reset anti-replay cursor");
        Require(!identity.Assigned, "unregister clears old match identity");
        Require(!bridge.TryIssue(1, Intent("aos-bridge", 4, id3,
            NetworkOrderKind.Move), 12, null, (w,l) => 0f,
            out _, out reason) && reason == OrderRejection.UnknownActor,
            "unregistered/dead actors cannot accept orders");
    }
}
