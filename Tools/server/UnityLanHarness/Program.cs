using System;
using System.Threading.Tasks;
using Sinbinder.Multiplayer;
using UnityEngine;

internal static class Program
{
    private static async Task PumpUntil(
        LanRoomServer room, LanRoomClient one, LanRoomClient two,
        Func<bool> condition, string reason)
    {
        for (int i = 0; i < 400; i++)
        {
            room.Pump(0.025f);
            one.Pump();
            two.Pump();
            if (condition()) return;
            await Task.Delay(25);
        }
        throw new Exception("Unity LAN harness timed out: " + reason +
            " | A=" + one.Status + " B=" + two.Status);
    }

    public static async Task<int> Main()
    {
        if (LanMessage.TryDecode("{not json}", out _))
            throw new Exception("Malformed packets must be rejected");
        if (LanMessage.TryDecode(new string('A', 5000), out _))
            throw new Exception("Oversized packets must be rejected");
        var original = new LanMessage
        {
            type = "state", tick = 42, p1X = 1.5f, p1Z = 2f,
            p2X = -1.5f, p2Z = -2f
        };
        if (!LanMessage.TryDecode(LanMessage.Encode(original), out var cloned) ||
            cloned.tick != 42 || Math.Abs(cloned.p1X - 1.5f) > 0.001f)
            throw new Exception("JSON message roundtrip failed");

        var room = new LanRoomServer();
        var one = new LanRoomClient();
        var two = new LanRoomClient();
        try
        {
            if (!room.Start(7788)) throw new Exception(room.Error);
            one.Connect("127.0.0.1", 7788);
            two.Connect("127.0.0.1", 7788);

            await PumpUntil(room, one, two,
                () => one.PlayerId > 0 && two.PlayerId > 0,
                "two welcome packets");

            if (one.PlayerId == two.PlayerId)
                throw new Exception("Server assigned duplicate owner ID");

            one.Ready();
            two.Ready();
            await PumpUntil(room, one, two,
                () => one.Playing && two.Playing,
                "ready/start exchange");

            one.Move(new Vector3(-1, 0, 3));
            two.Move(new Vector3(1, 0, -3));
            await PumpUntil(room, one, two,
                () => one.First.z > 0.5f && one.Second.z < -0.5f &&
                      two.First.z > 0.5f && two.Second.z < -0.5f,
                "both server-driven pawns moved");

            if (Math.Abs(one.First.z - two.First.z) > 0.2f ||
                Math.Abs(one.Second.z - two.Second.z) > 0.2f)
                throw new Exception("Clients diverged on replicated positions");
            Console.WriteLine("PASS: Unity LAN source compiled and ran; 2 clients/ready/movement/snapshots.");

            two.Disconnect();
            await PumpUntil(room, one, two, () => !one.Playing,
                "server stop notification");
            Console.WriteLine("PASS: disconnect stops match.");
            await ExtraLanChecks.Run();
            MapVisualChecks.Run();
            AuthorityChecks.Run();
            WorldProbeChecks.Run();
            await GameOrderWireChecks.Run();
            await WorldSnapshotChecks.Run();
            return 0;
        }
        finally
        {
            one.Disconnect();
            two.Disconnect();
            room.Stop();
        }
    }
}
