using System;
using System.Threading.Tasks;
using Sinbinder.Core;
using Sinbinder.Gameplay;
using Sinbinder.Multiplayer;
using UnityEngine;

// Exercise the real Unity LAN transport and world registry in one .NET test:
// TCP client -> authenticated server peer -> MatchAuthority ->
// actual Warrior API via AOS bridge -> private acknowledgement to issuer.
// No claim is made about full Unity gameplay or world replication.
internal static class GameOrderWireChecks
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("Game wire regression: " + message);
    }

    private static async Task Until(LanRoomServer room,
        LanRoomClient one, LanRoomClient two, Func<bool> condition, string message)
    {
        for (int i = 0; i < 380; i++)
        {
            room.Pump(0.025f);
            one.Pump();
            two.Pump();
            if (condition()) return;
            await Task.Delay(15);
        }
        throw new Exception("LAN game-wire timed out: " + message +
            " [A " + one.Status + "; B " + two.Status + "]");
    }

    public static async Task Run()
    {
        var encoded = LanGameOrderCodec.Write(new NetworkOrderIntent {
            MatchId = "game-1", CommandId = 2, ActorId = 9,
            Kind = NetworkOrderKind.Move, X = -12f, Y = 0f, Z = 8f
        });
        Require(LanMessage.TryDecode(LanMessage.Encode(encoded), out var decoded)
            && LanGameOrderCodec.TryRead(decoded, out var intent)
            && intent.ActorId == 9 && intent.CommandId == 2 &&
            intent.Kind == NetworkOrderKind.Move,
            "real-world order survives JSON-line roundtrip");
        decoded.kind = "Admin";
        Require(!LanGameOrderCodec.TryRead(decoded, out _),
            "invalid command kind must never be accepted");

        TestMapFixture.TwoCrypts();
        var probe = new GameObject("Live Warrior network host")
            .AddComponent<ServerWorldProbe>();
        probe.Initialize();
        var lordA = new GameObject("Lord one").AddComponent<SinbinderPlayer>();
        lordA.Team = Team.Player;
        lordA.TestMover = new UnitMover();
        var lordB = new GameObject("Lord two").AddComponent<Warrior>();
        lordB.Team = Team.Neutral;
        lordB.TestMover = new UnitMover();
        lordB.gameObject.AddComponent<RivalLord>().Side = 1;
        var neutral = new GameObject("Resident").AddComponent<Warrior>();
        neutral.Team = Team.Neutral;
        probe.Scan(new Warrior[] { lordA, lordB, neutral });
        long neutralId = neutral.GetComponent<NetworkActorIdentity>().ServerId;
        long lordAId = lordA.GetComponent<NetworkActorIdentity>().ServerId;
        long lordBId = lordB.GetComponent<NetworkActorIdentity>().ServerId;
        Require(neutralId > 0 && lordAId > 0 && lordBId > 0,
            "registered actual Warrior instances must have server IDs");

        var room = new LanRoomServer();
        var one = new LanRoomClient();
        var two = new LanRoomClient();
        var replacement = new LanRoomClient();
        int acceptedA = 0, acceptedB = 0, rejectedA = 0;
        int resolvedA = 0, resolvedB = 0, cancelled = 0;
        string rejection = null;
        probe.OrderResolved += (order, applied) =>
        {
            if (!applied) cancelled++;
        };
        one.Received += packet =>
        {
            if (packet.type == "gameAccepted") acceptedA++;
            if (packet.type == "gameRejected") { rejectedA++; rejection = packet.reason; }
            if (packet.type == "gameResolved") resolvedA++;
        };
        two.Received += packet =>
        {
            if (packet.type == "gameAccepted") acceptedB++;
            if (packet.type == "gameResolved") resolvedB++;
        };

        try
        {
            probe.AttachRoom(room);
            if (!room.Start(7806)) throw new Exception(room.Error);
            one.Connect("127.0.0.1", 7806);
            two.Connect("127.0.0.1", 7806);
            await Until(room, one, two, () =>
                one.PlayerId == 1 && two.PlayerId == 2,
                "authenticated slots 1 and 2 connected");
            one.Ready();
            two.Ready();
            await Until(room, one, two, () =>
                one.Playing && two.Playing &&
                one.GameMatchId == probe.Authority.MatchId &&
                two.GameMatchId == probe.Authority.MatchId &&
                one.OwnLordActorId == lordAId && two.OwnLordActorId == lordBId,
                "both receive server match ID and their OWN Lord actor ID");

            Require(one.OrderGameplay(lordBId, NetworkOrderKind.Move,
                new Vector3(3f, 0f, 4f)), "first peer sent forged Lord order");
            await Until(room, one, two, () => rejectedA == 1,
                "server rejects command on enemy Lord");
            Require(rejection == OrderRejection.NotAllowed.ToString() &&
                lordB.TestMover.Destination.x == 0f,
                "one TCP peer cannot order rival Lord");

            Require(one.OrderGameplay(neutralId, NetworkOrderKind.Move,
                new Vector3(4f, 0f, 8f)), "first peer speaks to neutral");
            await Until(room, one, two, () => acceptedA == 1,
                "first verified neutral command accepted");
            Require(two.OrderGameplay(neutralId, NetworkOrderKind.Patrol,
                new Vector3(-7f, 0f, -4f)), "second peer speaks to same neutral");
            await Until(room, one, two, () => acceptedB == 1,
                "second verified neutral command accepted");
            Require(neutral.LastCommand == CommandKind.None,
                "TCP acknowledgements must not execute AOS early");

            // The real authoritative WORLD tick is separate from TCP pump;
            // this validates the boundary, not the full Unity AOS simulation.
            probe.Advance(0.20f);
            await Until(room, one, two, () =>
                resolvedA == 1 && resolvedB == 1,
                "both receive individual delivery results");
            Require(neutral.LastLord == "Греховод стороны 2" &&
                neutral.LastCommand == CommandKind.Patrol,
                "server executes both real-Warrior proposals in sequence");
            Require(resolvedA == 1 && resolvedB == 1,
                "execution results do not broadcast rival orders");

            // Closing a transport session must invalidate an order which
            // was already accepted but has not yet reached ExecuteTick.
            Require(one.OrderGameplay(neutralId, NetworkOrderKind.Move,
                new Vector3(3f, 0f, 6f)), "queue before disconnect");
            await Until(room, one, two, () => acceptedA == 2,
                "pending command was accepted");
            one.Disconnect();
            await Until(room, one, two, () => room.PlayerCount == 1,
                "client disconnect reaches server");
            probe.Advance(0.20f);
            Require(cancelled == 1 && neutral.LastCommand == CommandKind.Patrol,
                "disconnected Lord cannot execute pending game order");

            // A fresh client process starts its counter at zero, but the
            // SERVER must preserve the last accepted command for this seat.
            // The reconnect handshake transfers that cursor without replay.
            int replacementAccepted = 0;
            replacement.Received += packet =>
            {
                if (packet.type == "gameAccepted") replacementAccepted++;
            };
            replacement.Connect("127.0.0.1", 7806);
            await Until(room, replacement, two,
                () => replacement.PlayerId == 1 && room.PlayerCount == 2,
                "reused vacated Lord seat");
            two.Ready();
            replacement.Ready();
            await Until(room, replacement, two,
                () => replacement.Playing && two.Playing &&
                      replacement.GameMatchId == probe.Authority.MatchId &&
                      replacement.OwnLordActorId == lordAId,
                "reconnected Lord receives match and actor identity");
            Require(replacement.OrderGameplay(neutralId, NetworkOrderKind.Move,
                new Vector3(6f, 0f, 3f)), "new client can issue gameplay command");
            await Until(room, replacement, two,
                () => replacementAccepted == 1,
                "reconnected client resumed its server-side replay cursor");
            probe.Advance(0.20f);
            Require(neutral.LastLord == "Греховод стороны 1" &&
                neutral.LastCommand == CommandKind.Move,
                "reconnected client commands reach same real Warrior");
            Console.WriteLine(
                "PASS: two TCP clients -> real Warrior/AOS, private confirmations, disconnect cancellation and fresh-client reconnect");
        }
        finally
        {
            one.Disconnect();
            two.Disconnect();
            replacement.Disconnect();
            room.Stop();
        }
    }
}
