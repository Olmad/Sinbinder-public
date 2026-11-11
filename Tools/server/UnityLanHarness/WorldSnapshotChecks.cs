using System;
using System.Reflection;
using System.Threading.Tasks;
using Sinbinder.Core;
using Sinbinder.Gameplay;
using Sinbinder.Multiplayer;
using UnityEngine;

// Real TCP sockets + actual source-built server/client + real Warrior API
// through the Unity shim. Never confuse with successful Unity Play Mode.
internal static class WorldSnapshotChecks
{
    private static void Need(bool yes, string reason)
    {
        if (!yes) throw new Exception("World snapshot regression: " + reason);
    }

    private static async Task Wait(LanRoomServer room,
        LanRoomClient one, LanRoomClient two,
        Func<bool> condition, string reason)
    {
        for (int i = 0; i < 300; i++)
        {
            room.Pump(0.025f);
            one.Pump(); two.Pump();
            if (condition()) return;
            await Task.Delay(15);
        }
        throw new Exception("World snapshot timeout: " + reason);
    }

    public static async Task Run()
    {
        TestMapFixture.TwoCrypts();
        var probe = new GameObject("snapshot host")
            .AddComponent<ServerWorldProbe>();
        probe.Initialize();

        var own = new GameObject("Lord 1").AddComponent<SinbinderPlayer>();
        own.Team = Team.Player;
        own.TestMover = new UnitMover();
        own.transform.position = new Vector3(3f, 0f, -91f);
        var rival = new GameObject("Lord 2").AddComponent<Warrior>();
        rival.Team = Team.Neutral;
        rival.TestMover = new UnitMover();
        rival.gameObject.AddComponent<RivalLord>().Side = 1;
        rival.transform.position = new Vector3(-4f, 0f, 91f);

        var ally = new GameObject("Player 1 allied Warrior").AddComponent<Warrior>();
        ally.Team = Team.Player;
        ally.transform.position = new Vector3(15f, 0f, -60f);
        var allyHealth = ally.gameObject.AddComponent<Damageable>();
        allyHealth.HP = 18f;
        allyHealth.MaxHP = 30f;
        var neutral = new GameObject("Neutral resident").AddComponent<Warrior>();
        neutral.Team = Team.Neutral;
        neutral.transform.position = new Vector3(1f, 0f, 2f);
        probe.Scan(new Warrior[] { own, rival, ally, neutral });

        long ownId = own.GetComponent<NetworkActorIdentity>().ServerId;
        long rivalId = rival.GetComponent<NetworkActorIdentity>().ServerId;
        long allyId = ally.GetComponent<NetworkActorIdentity>().ServerId;
        long neutralId = neutral.GetComponent<NetworkActorIdentity>().ServerId;
        Need(ownId > 0 && rivalId > 0 && neutralId > 0 && allyId > 0,
            "all real warriors have server IDs");
        var room = new LanRoomServer();
        var c1 = new LanRoomClient();
        var c2 = new LanRoomClient();
        try
        {
            probe.AttachRoom(room);
            Need(room.Start(7809), "start snapshot room");
            c1.Connect("127.0.0.1", 7809);
            c2.Connect("127.0.0.1", 7809);
            await Wait(room, c1, c2, () => c1.PlayerId == 1 &&
                c2.PlayerId == 2, "two TCP connections");
            c1.Ready(); c2.Ready();
            await Wait(room, c1, c2, () => c1.Playing && c2.Playing &&
                c1.GameMatchId == probe.Authority.MatchId &&
                c2.GameMatchId == probe.Authority.MatchId,
                "real match handshake");

            room.PublishPrivateWorldSnapshot();
            await Wait(room, c1, c2, () => c1.WorldFrame > 0 &&
                c2.WorldFrame > 0, "first private snapshot");
            Need(c1.WorldActors.Count == 2 && c1.WorldActors.ContainsKey(ownId) &&
                c1.WorldActors.ContainsKey(allyId) &&
                !c1.WorldActors.ContainsKey(rivalId) &&
                !c1.WorldActors.ContainsKey(neutralId),
                "client 1 gets OWN Lord + ally, not hidden rival or neutral");
            Need(c2.WorldActors.Count == 1 && c2.WorldActors.ContainsKey(rivalId) &&
                !c2.WorldActors.ContainsKey(ownId) &&
                !c2.WorldActors.ContainsKey(allyId),
                "client 2 gets ONLY its own Lord");
            Need(Math.Abs(c1.WorldActors[ownId].Position.z + 91f) < .01f &&
                Math.Abs(c2.WorldActors[rivalId].Position.z - 91f) < .01f,
                "received real positions from authoritative Unity world");
            Need(c1.WorldActors[allyId].HasHealth &&
                Math.Abs(c1.WorldActors[allyId].HP - 18f) < .01f &&
                Math.Abs(c1.WorldActors[allyId].MaxHP - 30f) < .01f &&
                !c2.WorldActors.ContainsKey(allyId),
                "private Damageable health arrives to ally owner, never enemy");

            own.transform.position = new Vector3(8f, 0f, -80f);
            allyHealth.HP = 7f;
            long frame = c1.WorldFrame;
            room.PublishPrivateWorldSnapshot();
            await Wait(room, c1, c2, () => c1.WorldFrame > frame &&
                c2.WorldFrame > frame, "updated positions");
            Need(Math.Abs(c1.WorldActors[ownId].Position.x - 8f) < .01f,
                "own server position changed in client snapshot");
            Need(Math.Abs(c1.WorldActors[allyId].HP - 7f) < .01f &&
                !c2.WorldActors.ContainsKey(allyId),
                "health updates from real Damageable, not client simulation");

            // Explicit per-client, authoritative visibility, NOT the local
            // FogOfWar render mask. Only player 1 can see this neutral.
            probe.CanObserveActorOnServer = (conn, warrior) =>
                conn == 1 && warrior == neutral;
            frame = c1.WorldFrame;
            room.PublishPrivateWorldSnapshot();
            await Wait(room, c1, c2, () => c1.WorldFrame > frame &&
                c2.WorldFrame > frame, "server visibility filter enabled");
            Need(c1.WorldActors.ContainsKey(neutralId) &&
                !c2.WorldActors.ContainsKey(neutralId),
                "one player sees neutral, rival receives no coordinates");

            // A visibility change MUST remove stale positions: clients
            // replace the complete roster rather than accumulating actors.
            probe.CanObserveActorOnServer = null;
            frame = c1.WorldFrame;
            room.PublishPrivateWorldSnapshot();
            await Wait(room, c1, c2, () => c1.WorldFrame > frame &&
                c2.WorldFrame > frame, "visibility revoked");
            Need(!c1.WorldActors.ContainsKey(neutralId),
                "neutral disappears when server revokes visibility");

            ally.IsDead = true;
            probe.Scan(new Warrior[] { own, rival, ally, neutral });
            frame = c1.WorldFrame;
            room.PublishPrivateWorldSnapshot();
            await Wait(room, c1, c2, () => c1.WorldFrame > frame &&
                c2.WorldFrame > frame, "death snapshot");
            Need(!c1.WorldActors.ContainsKey(allyId) &&
                c1.WorldActors.Count == 1,
                "dead owned Warrior removed without ghost entity");

            // A failed server-side visibility projection must revoke the
            // last roster, not leave stale HP/coordinates on either client.
            room.PrivateWorldActors = peerId => {
                if (peerId == 1) throw new InvalidOperationException("visibility unavailable");
                return null;
            };
            frame = c1.WorldFrame;
            room.PublishPrivateWorldSnapshot();
            await Wait(room, c1, c2, () => c1.WorldFrame > frame &&
                c2.WorldFrame > frame, "failed private projection revokes roster");
            Need(c1.WorldActors.Count == 0 && c2.WorldActors.Count == 0,
                "invalid/throw private projections clear stale actors");

            // A subsequent valid frame must recover normal private filtering.
            probe.AttachRoom(room);
            frame = c1.WorldFrame;
            room.PublishPrivateWorldSnapshot();
            await Wait(room, c1, c2, () => c1.WorldFrame > frame &&
                c2.WorldFrame > frame, "private projection recovers");
            Need(c1.WorldActors.Count == 1 && c1.WorldActors.ContainsKey(ownId) &&
                c2.WorldActors.Count == 1 && c2.WorldActors.ContainsKey(rivalId),
                "recovered projection restores only private Lord actors");

            // Never replace a snapshot on receipt of only a partial frame.
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            int generation = (int)typeof(LanRoomClient)
                .GetField("_generation", flags).GetValue(c1);
            var enqueue = typeof(LanRoomClient).GetMethod("Queue", flags);
            Need(enqueue != null, "test harness can enqueue inbound frame");
            long malformedFrame = c1.WorldFrame + 1;
            enqueue.Invoke(c1, new object[] {
                generation,
                LanMessage.Encode(new LanMessage {
                    type = "worldBegin", matchId = c1.GameMatchId,
                    snapshotId = malformedFrame, actorCount = 2
                }).TrimEnd('\n'), null
            });
            enqueue.Invoke(c1, new object[] {
                generation,
                LanMessage.Encode(LanWorldActorState.Packet(
                    new LanWorldActorState(ownId,
                        NetworkActorRole.Lord, new Vector3(99, 0, 99)),
                    c1.GameMatchId, malformedFrame, probe.Tick)).TrimEnd('\n'),
                null
            });
            c1.Pump();
            Need(c1.WorldActors.Count == 1 &&
                Math.Abs(c1.WorldActors[ownId].Position.x - 8f) < .01f,
                "incomplete frame never replaces last valid snapshot");

            Console.WriteLine("PASS: private real-Warrior coordinates and Damageable HP; per-player fog, death, and incomplete frames");
        }
        finally
        {
            c1.Disconnect();
            c2.Disconnect();
            room.Stop();
        }
    }
}
