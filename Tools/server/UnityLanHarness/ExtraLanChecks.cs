using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Sinbinder.Multiplayer;
using UnityEngine;

internal static class ExtraLanChecks
{
    private static async Task Until(LanRoomServer room, Func<bool> predicate, string label)
    {
        for (int i = 0; i < 500; i++)
        {
            room.Pump(0.01f);
            if (predicate()) return;
            await Task.Delay(10);
        }
        throw new Exception("LAN extra check failed: " + label +
            ", server player count=" + room.PlayerCount);
    }

    public static async Task Run()
    {
        await ConnectionChurn();
        await InboxFloodAndRecovery();
        await SeatReplacementAfterDisconnect();
        await DisconnectedClientAndBadPort();
        Console.WriteLine("PASS: rapid reconnect, inbox overload recovery, invalid endpoint.");
    }

    private static async Task ConnectionChurn()
    {
        var room = new LanRoomServer();
        var client = new LanRoomClient();
        try
        {
            if (!room.Start(7792)) throw new Exception(room.Error);

            for (int i = 0; i < 12; i++)
            {
                client.Connect("127.0.0.1", 7792);
                await Until(room, () =>
                {
                    client.Pump();
                    return client.PlayerId == 1 && room.PlayerCount == 1;
                }, "connect iteration " + i);

                client.Disconnect();
                await Until(room, () => room.PlayerCount == 0,
                    "disconnect iteration " + i);
            }

            // Rapid reconnect attempts without allowing older asynchronous
            // connects to finish must not overwrite the last client's wire.
            for (int i = 0; i < 12; i++)
                client.Connect("127.0.0.1", 7792);

            await Until(room, () =>
            {
                client.Pump();
                return client.PlayerId == 1 && room.PlayerCount == 1;
            }, "last connection survived stale connect attempts");

            client.Ready();
            await Until(room, () =>
            {
                client.Pump();
                return room.PlayerCount == 1 && client.ReadyCount == 1;
            }, "last connection can still send ready");
        }
        finally { client.Disconnect(); room.Stop(); }
    }

    private static async Task InboxFloodAndRecovery()
    {
        var room = new LanRoomServer();
        TcpClient first = null, second = null, replacement = null;
        try
        {
            if (!room.Start(7793)) throw new Exception(room.Error);
            first = new TcpClient();
            second = new TcpClient();
            await first.ConnectAsync("127.0.0.1", 7793);
            await second.ConnectAsync("127.0.0.1", 7793);
            await Until(room, () => room.PlayerCount == 2, "two peers connected");

            // Fill the incoming queue beyond its 256-message bound.
            // Cleanup of a disconnected offender must still run.
            string flood = "{\"type\":\"invalid\"}\n";
            byte[] data = Encoding.UTF8.GetBytes(string.Concat(
                System.Linq.Enumerable.Repeat(flood, 350)));
            try { await first.GetStream().WriteAsync(data); }
            catch (Exception ex) when (ex is System.IO.IOException or SocketException) { }

            await Until(room, () => room.PlayerCount == 1, "flooded peer removed");

            // A fresh player must be able to take the free slot.
            replacement = new TcpClient();
            await replacement.ConnectAsync("127.0.0.1", 7793);
            await Until(room, () => room.PlayerCount == 2,
                "flooded room can accept replacement player");
        }
        finally
        {
            if (first != null) first.Close();
            if (second != null) second.Close();
            if (replacement != null) replacement.Close();
            room.Stop();
        }
    }

    private static async Task SeatReplacementAfterDisconnect()
    {
        var room = new LanRoomServer();
        var first = new LanRoomClient();
        var second = new LanRoomClient();
        var replacement = new LanRoomClient();
        try
        {
            if (!room.Start(7795)) throw new Exception(room.Error);
            first.Connect("127.0.0.1", 7795);
            second.Connect("127.0.0.1", 7795);
            await Until(room, () =>
            {
                first.Pump();
                second.Pump();
                return first.PlayerId != 0 && second.PlayerId != 0;
            }, "initial pair joined");

            first.Ready();
            second.Ready();
            await Until(room, () =>
            {
                first.Pump();
                second.Pump();
                return first.Playing && second.Playing;
            }, "initial pair started");

            int firstId = first.PlayerId;
            int abandonedId = second.PlayerId;
            second.Disconnect();
            await Until(room, () =>
            {
                first.Pump();
                return !first.Playing && room.PlayerCount == 1;
            }, "remaining player sees game stopped");

            replacement.Connect("127.0.0.1", 7795);
            await Until(room, () =>
            {
                first.Pump();
                replacement.Pump();
                return replacement.PlayerId == abandonedId &&
                    room.PlayerCount == 2;
            }, "new client receives vacated player slot");

            first.Ready();
            replacement.Ready();
            await Until(room, () =>
            {
                first.Pump();
                replacement.Pump();
                return first.Playing && replacement.Playing;
            }, "replacement can start a new match");

            if (first.PlayerId != firstId)
                throw new Exception("Persistent player's identity changed");
        }
        finally
        {
            first.Disconnect();
            second.Disconnect();
            replacement.Disconnect();
            room.Stop();
        }
    }

    private static async Task DisconnectedClientAndBadPort()
    {
        var client = new LanRoomClient();
        try
        {
            client.Connect("127.0.0.1", 7794); // nothing listens on this port
            for (int i = 0; i < 300; i++)
            {
                client.Pump();
                if (client.Status.StartsWith("Ошибка соединения:")) return;
                await Task.Delay(10);
            }
            throw new Exception("Rejected TCP connection was not surfaced in client status");
        }
        finally { client.Disconnect(); }
    }
}
