using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
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
        await DropPendingAcceptOnStop();
        await StopEnqueueRaceAndFrameBudget();
        await DiscardPacketsAfterTerminalEvent();
        await InboxFloodAndRecovery();
        await SeatReplacementAfterDisconnect();
        await DisconnectedClientAndBadPort();
        await HeartbeatAndSilentPeerExpiry();
        await NonRespondingServerTimeout();
        await ServerRestartSamePort();
        TestArenaLifecycle();
        Console.WriteLine("PASS: reconnect, flood recovery, heartbeat, silent-peer expiry, dead-server detection and arena lifecycle.");
    }

    private static async Task StopEnqueueRaceAndFrameBudget()
    {
        var room = new LanRoomServer();
        try
        {
            if (!room.Start(7787)) throw new Exception(room.Error);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(LanRoomServer);
            var enqueue = type.GetMethod("Queue", flags);
            var epochField = type.GetField("_epoch", flags);
            var pendingField = type.GetField("_pending", flags);
            if (enqueue == null || epochField == null || pendingField == null)
                throw new Exception("LAN queue contract missing");
            int epoch = (int)epochField.GetValue(room);
            int executed = 0;
            int discarded = 0;
            Action onExecute = () => System.Threading.Interlocked.Increment(ref executed);
            Action onDrop = () => System.Threading.Interlocked.Increment(ref discarded);

            // Saturate the bounded inbox before a single frame and verify
            // that Pump does not consume its entire queue in one update.
            for (int i = 0; i < 256; i++)
                enqueue.Invoke(room, new object[] { onExecute, epoch, onDrop });
            room.Pump(0f);
            if (executed != 128)
                throw new Exception("Network work was not capped at 128 packets/frame: " + executed);
            room.Pump(0f);
            if (executed != 256)
                throw new Exception("Network work was lost across capped frames");

            // Simulate pending accepts arriving concurrently with Stop().
            // After Stop every accepted callback must execute its OnDrop
            // exactly once, whether it was queued before or after invalidation.
            const int attempts = 1500;
            var began = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var writer = Task.Run(async () =>
            {
                for (int i = 0; i < attempts; i++)
                {
                    enqueue.Invoke(room, new object[] { onExecute, epoch, onDrop });
                    if (i == 0) began.TrySetResult(true);
                    if (i % 23 == 0) await Task.Yield();
                }
            });
            await began.Task;
            room.Stop();
            await writer;
            if (executed != 256 || discarded != attempts)
                throw new Exception(
                    "Stop lost queued cleanup callbacks. Executed=" + executed +
                    " Dropped=" + discarded + " Expected drops=" + attempts);
            int pending = (int)pendingField.GetValue(room);
            if (pending != 0)
                throw new Exception("Pending packet count after Stop: " + pending);
            Console.WriteLine("PASS: bounded network frame budget and atomic Stop/Queue cleanup");
        }
        finally { room.Stop(); }
    }

    private static async Task DropPendingAcceptOnStop()
    {
        var room = new LanRoomServer();
        TcpClient socket = null;
        try
        {
            if (!room.Start(7791)) throw new Exception(room.Error);
            socket = new TcpClient();
            await socket.ConnectAsync("127.0.0.1", 7791);

            // Wait for the listener to accept the connection, but DO NOT
            // pump the room: this reproduces the previously lost onDrop
            // cleanup path for accepted, unregistered sockets.
            var field = typeof(LanRoomServer).GetField("_inbox",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new Exception("LAN inbox not found");
            bool pending = false;
            for (int i = 0; i < 300; i++)
            {
                var inbox = field.GetValue(room);
                int count = (int)inbox.GetType().GetProperty("Count").GetValue(inbox);
                if (count > 0) { pending = true; break; }
                await Task.Delay(10);
            }
            if (!pending) throw new Exception("Listener did not queue an unregistered TCP peer");

            room.Stop();
            var response = new byte[1];
            using (var deadline = new System.Threading.CancellationTokenSource(
                       TimeSpan.FromSeconds(3)))
            {
                int length = await socket.GetStream().ReadAsync(
                    response, 0, response.Length, deadline.Token);
                if (length != 0)
                    throw new Exception("Unregistered peer remained alive after Stop");
            }
            Console.WriteLine("PASS: Stop releases TCP connections still waiting in the inbox");
        }
        finally
        {
            if (socket != null) socket.Close();
            room.Stop();
        }
    }

    private static async Task DiscardPacketsAfterTerminalEvent()
    {
        await VerifyTerminal("CLOSED");
        await VerifyTerminal("ERROR:Simulated disconnection");
        Console.WriteLine("PASS: terminal socket events invalidate stale queued start/state packets");
    }

    private static async Task VerifyTerminal(string terminal)
    {
        var listener = new TcpListener(IPAddress.Loopback, 7789);
        var client = new LanRoomClient();
        TcpClient remote = null;
        try
        {
            listener.Start();
            var accepted = listener.AcceptTcpClientAsync();
            client.Connect("127.0.0.1", 7789);
            remote = await accepted;
            for (int i = 0; i < 400 && !client.Connected; i++)
            {
                client.Pump();
                await Task.Delay(10);
            }
            if (!client.Connected) throw new Exception("Mock transport did not connect");

            int count = 0;
            client.Received += message => { if (message.type == "start") count++; };
            var type = typeof(LanRoomClient);
            var fields = BindingFlags.Instance | BindingFlags.NonPublic;
            var generation = (int)type.GetField("_generation", fields).GetValue(client);
            var enqueue = type.GetMethod("Queue", fields);
            if (enqueue == null) throw new Exception("LAN receiver queue method missing");

            // A race can put CLOSED ahead of already received packets on the
            // main-thread queue. Nothing after CLOSED is allowed to restart
            // the old match.
            enqueue.Invoke(client, new object[] { generation, null, terminal });
            enqueue.Invoke(client, new object[] { generation,
                @"{""type"":""start"",""playerId"":1,""unitId"":1}", null });
            enqueue.Invoke(client, new object[] { generation,
                @"{""type"":""state"",""tick"":100,""p1X"":7}", null });
            client.Pump();

            if (client.Connected || client.Playing || client.PlayerId != 0 ||
                client.Tick != 0 || count != 0)
                throw new Exception("Stale packets revived a closed TCP generation: " +
                    terminal);
            if (terminal == "CLOSED" && !client.Status.Contains("закрыл"))
                throw new Exception("Client lost its disconnect status");
            if (terminal.StartsWith("ERROR") &&
                !client.Status.Contains("Simulated disconnection"))
                throw new Exception("Client lost its socket error status");
        }
        finally
        {
            client.Disconnect();
            if (remote != null) remote.Close();
            listener.Stop();
        }
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

            // Rapid reconnection can legitimately race with server-side
            // removal of closed sockets. Do not expect the LAST attempt to
            // take a slot before stale sessions have been drained. Verify
            // that the server recovers and that the client can retry.
            for (int i = 0; i < 12; i++)
                client.Connect("127.0.0.1", 7792);
            client.Disconnect();
            await Until(room, () => room.PlayerCount == 0,
                "rapid reconnects left no ghost sessions");

            client.Connect("127.0.0.1", 7792);
            await Until(room, () =>
            {
                client.Pump();
                return client.PlayerId == 1 && room.PlayerCount == 1;
            }, "new connection succeeds after clearing stale peers");

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

            // Make the first match visibly different from a fresh spawn.
            // Otherwise a broken restart can pass even if old pawn
            // positions and movement goals are still retained.
            first.Move(new Vector3(-1f, 0f, 3f));
            await Until(room, () =>
            {
                first.Pump();
                second.Pump();
                return Math.Abs(first.PlayerId == 1 ?
                    first.First.z : first.Second.z) > 0.5f;
            }, "first match pawn moved away from its starting position");

            bool freshSnapshot = false;
            first.Received += packet =>
            {
                if (packet.type == "start") freshSnapshot = false;
                if (packet.type == "state" && packet.tick == 0 && first.Playing)
                    freshSnapshot = true;
            };

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

            await Until(room, () =>
            {
                first.Pump();
                replacement.Pump();
                return freshSnapshot;
            }, "new round publishes a fresh authoritative snapshot");

            if (Math.Abs(first.First.z) > 0.01f ||
                Math.Abs(first.Second.z) > 0.01f ||
                Math.Abs(replacement.First.z) > 0.01f ||
                Math.Abs(replacement.Second.z) > 0.01f)
                throw new Exception("New match inherited previous round movement");
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

    private static async Task HeartbeatAndSilentPeerExpiry()
    {
        var room = new LanRoomServer();
        var client = new LanRoomClient();
        TcpClient silent = null;
        try
        {
            if (!room.Start(7797)) throw new Exception(room.Error);
            client.Connect("127.0.0.1", 7797);
            await Until(room, () =>
            {
                client.Pump();
                return client.PlayerId == 1;
            }, "heartbeat client connected");

            bool sawPong = false;
            client.Received += packet => { if (packet.type == "pong") sawPong = true; };
            Time.Advance(3f);
            client.Pump(); // queues ping on next heartbeat
            await Until(room, () =>
            {
                client.Pump();
                return sawPong;
            }, "server acknowledged heartbeat");

            silent = new TcpClient();
            await silent.ConnectAsync("127.0.0.1", 7797);
            await Until(room, () => room.PlayerCount == 2, "silent raw peer admitted");
            Time.Advance(21f);
            // The LAN test client has not pumped since the clock jump, so
            // BOTH peers will be reaped if we advance too far. Instead the
            // important invariant is that nobody occupies a ghost seat.
            room.Pump(0.01f);
            if (room.PlayerCount != 0)
                throw new Exception("Silent peers were not reaped after 20 seconds");
        }
        finally
        {
            if (silent != null) silent.Close();
            client.Disconnect();
            room.Stop();
        }
    }

    private static async Task NonRespondingServerTimeout()
    {
        var listener = new TcpListener(IPAddress.Loopback, 7798);
        var client = new LanRoomClient();
        TcpClient remote = null;
        try
        {
            listener.Start();
            Task<TcpClient> accepted = listener.AcceptTcpClientAsync();
            client.Connect("127.0.0.1", 7798);
            remote = await accepted;
            for (int i = 0; i < 400 && !client.Connected; i++)
            {
                client.Pump();
                await Task.Delay(10);
            }
            if (!client.Connected)
                throw new Exception("Could not establish mock silent connection");
            Time.Advance(13f);
            client.Pump();
            if (client.Connected || !client.Status.Contains("сервер не отвечает"))
                throw new Exception("Client did not report a dead server after 12 seconds");
        }
        finally
        {
            client.Disconnect();
            if (remote != null) remote.Close();
            listener.Stop();
        }
    }

    private static async Task ServerRestartSamePort()
    {
        var room = new LanRoomServer();
        TcpClient socket = null;
        try
        {
            // Rapid Start/Stop used to race with callbacks from the
            // previous listener epoch and corrupt the inbox bound counter.
            for (int i = 0; i < 12; i++)
            {
                if (!room.Start(7799))
                    throw new Exception("Restart iteration " + i + ": " + room.Error);
                socket = new TcpClient();
                await socket.ConnectAsync("127.0.0.1", 7799);
                await Until(room, () => room.PlayerCount == 1,
                    "listener restarted on same port, iteration " + i);
                room.Stop();
                socket.Close();
                socket = null;
            }
            if (!room.Start(7799))
                throw new Exception("Final restart failed: " + room.Error);
            socket = new TcpClient();
            await socket.ConnectAsync("127.0.0.1", 7799);
            await Until(room, () => room.PlayerCount == 1,
                "fresh client accepted after 12 restarts");
        }
        finally
        {
            if (socket != null) socket.Close();
            room.Stop();
        }
    }

    private static void TestArenaLifecycle()
    {
        var selection = new UnityEngine.GameObject("persistent selection")
            .AddComponent<Sinbinder.Gameplay.SelectionManager>();
        Sinbinder.Gameplay.SelectionManager.Instance = selection;

        var bootstrap = new UnityEngine.GameObject("LAN test controller")
            .AddComponent<SinbinderLanBootstrap>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(SinbinderLanBootstrap);
        void Invoke(string name)
        {
            var method = type.GetMethod(name, flags);
            if (method == null) throw new Exception("Missing LAN lifecycle method: " + name);
            method.Invoke(bootstrap, Array.Empty<object>());
        }

        Invoke("Awake");
        Invoke("EnterArena");
        if (selection.enabled)
            throw new Exception("Persistent selection input remained enabled during LAN test");
        Invoke("Leave");
        if (!selection.enabled)
            throw new Exception("Persistent selection input was not restored after LAN test");
        Invoke("OnDestroy");
        Sinbinder.Gameplay.SelectionManager.Instance = null;
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
