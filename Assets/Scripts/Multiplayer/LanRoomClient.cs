using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Sinbinder.Multiplayer
{
    /// <summary>Unity main-thread facade around TCP. Call Pump from Update.</summary>
    public sealed class LanRoomClient
    {
        private struct Incoming
        {
            public int Generation;
            public string Data;
            public string Status;
        }

        private readonly ConcurrentQueue<Incoming> _incoming = new ConcurrentQueue<Incoming>();
        private readonly object _connectionGate = new object();
        private TcpClient _socket;
        private LanWire _wire;
        private CancellationTokenSource _cancel;
        private int _generation;
        private int _queued;
        private float _lastInboundAt;
        private float _lastPingAt;

        public bool Connected { get; private set; }
        public bool Playing { get; private set; }
        public int PlayerId { get; private set; }
        public int Players { get; private set; }
        public int ReadyCount { get; private set; }
        public long Tick { get; private set; }
        public Vector3 First { get; private set; }
        public Vector3 Second { get; private set; }
        public string Status { get; private set; } = "Не подключено";
        public event Action<LanMessage> Received;

        public void Connect(string host, int port)
        {
            Disconnect();
            if (string.IsNullOrWhiteSpace(host) || port < 1 || port > 65535)
            {
                Status = "Проверь адрес и порт";
                return;
            }
            int generation = _generation;
            var source = new CancellationTokenSource();
            var socket = new TcpClient();
            lock (_connectionGate)
            {
                _cancel = source;
                _socket = socket;
            }
            First = new Vector3(-3, 0.8f, 0);
            Second = new Vector3(3, 0.8f, 0);
            Status = "Подключение к " + host + ":" + port;
            _ = ConnectAndReadAsync(socket, host.Trim(), port, generation, source.Token);
        }

        private async Task ConnectAndReadAsync(TcpClient socket, string host, int port, int generation, CancellationToken token)
        {
            try
            {
                Task connecting = socket.ConnectAsync(host, port);
                Task timeout = Task.Delay(5000, token);
                if (await Task.WhenAny(connecting, timeout) != connecting)
                {
                    if (token.IsCancellationRequested) return;
                    throw new TimeoutException("Сервер не ответил за 5 секунд");
                }
                await connecting;
                if (token.IsCancellationRequested) return;

                var wire = new LanWire(socket);
                lock (_connectionGate)
                {
                    // A previous asynchronous Connect must never replace a
                    // newer connection's wire after a rapid reconnect.
                    if (generation != _generation || token.IsCancellationRequested)
                    {
                        wire.Dispose();
                        return;
                    }
                    _wire = wire;
                }
                Queue(generation, null, "CONNECTED");
                while (!token.IsCancellationRequested)
                {
                    string line = await wire.ReadLineAsync(token);
                    if (line == null) break;
                    Queue(generation, line, null);
                }
                Queue(generation, null, "CLOSED");
            }
            catch (Exception e) when (e is SocketException || e is TimeoutException ||
                                       e is OperationCanceledException || e is ObjectDisposedException ||
                                       e is System.IO.IOException || e is System.IO.InvalidDataException || e is ArgumentException)
            {
                Queue(generation, null, "ERROR:" + e.Message);
            }
            finally
            {
                socket.Close();
                lock (_connectionGate)
                {
                    if (generation == _generation)
                    {
                        _socket = null;
                        _wire = null;
                    }
                }
            }
        }

        private void Queue(int generation, string data, string status)
        {
            if (generation != Volatile.Read(ref _generation)) return;
            if (Interlocked.Increment(ref _queued) > 256)
            {
                Interlocked.Decrement(ref _queued);
                lock (_connectionGate)
                {
                    if (generation == _generation && _socket != null)
                        _socket.Close();
                }
                return;
            }
            _incoming.Enqueue(new Incoming { Generation = generation, Data = data, Status = status });
        }

        public void Pump()
        {
            while (_incoming.TryDequeue(out var next))
            {
                Interlocked.Decrement(ref _queued);
                if (next.Generation != _generation) continue;
                if (next.Status == "CONNECTED")
                {
                    Connected = true;
                    _lastInboundAt = Time.realtimeSinceStartup;
                    _lastPingAt = _lastInboundAt;
                    Status = "Подключено. Ожидаем игроков";
                    continue;
                }
                if (next.Status != null)
                {
                    // A terminal event invalidates every queued packet from
                    // this TCP generation. Otherwise a buffered "start" can
                    // resurrect Playing after CLOSED/ERROR.
                    string failure = next.Status == "CLOSED"
                        ? "Сервер закрыл соединение"
                        : "Ошибка соединения: " + next.Status.Substring("ERROR:".Length);
                    Disconnect();
                    Status = failure;
                    return;
                }
                if (!LanMessage.TryDecode(next.Data, out var packet))
                {
                    Status = "Некорректное сообщение сервера";
                    continue;
                }
                _lastInboundAt = Time.realtimeSinceStartup;
                switch (packet.type)
                {
                    case "pong":
                        // Acknowledges that the server's read and send loops
                        // still work even while the match is idle.
                        break;
                    case "welcome":
                        if (packet.protocol != 1) { Status = "Версия сетевого протокола не подходит"; Disconnect(); break; }
                        PlayerId = packet.playerId;
                        Status = "Подключено, игрок " + PlayerId;
                        break;
                    case "lobby":
                        Players = packet.players;
                        ReadyCount = packet.ready;
                        break;
                    case "start":
                        Playing = true;
                        Players = 2;
                        ReadyCount = 2;
                        Tick = 0;
                        // Reset the presentation immediately. The state
                        // snapshot for the new round follows this event.
                        First = new Vector3(-3, 0.8f, 0);
                        Second = new Vector3(3, 0.8f, 0);
                        Status = "Матч запущен. ПКМ по полю: двигаться";
                        break;
                    case "state":
                        Tick = packet.tick;
                        First = new Vector3(packet.p1X, 0.8f, packet.p1Z);
                        Second = new Vector3(packet.p2X, 0.8f, packet.p2Z);
                        break;
                    case "stopped":
                        Playing = false;
                        Status = string.IsNullOrEmpty(packet.reason) ? "Матч остановлен" : packet.reason;
                        break;
                    case "error":
                        Status = string.IsNullOrEmpty(packet.message) ? "Сервер отклонил запрос" : packet.message;
                        break;
                }
                if (packet.type != "welcome" || packet.protocol == 1)
                    Received?.Invoke(packet);
            }

            if (!Connected) return;
            float now = Time.realtimeSinceStartup;
            if (now - _lastInboundAt > 12f)
            {
                Disconnect();
                Status = "Ошибка соединения: сервер не отвечает";
                return;
            }
            if (now - _lastPingAt >= 2f)
            {
                _lastPingAt = now;
                Send(new LanMessage { type = "ping" });
            }
        }

        public void Ready()
        {
            Send(new LanMessage { type = "ready" });
        }

        public void Move(Vector3 destination)
        {
            if (!Playing || PlayerId == 0) return;
            Send(new LanMessage
            {
                type = "order", unitId = PlayerId, kind = "Move",
                x = Mathf.Clamp(destination.x, -8, 8),
                y = 0,
                z = Mathf.Clamp(destination.z, -8, 8)
            });
        }

        public void Hold()
        {
            if (!Playing || PlayerId == 0) return;
            Send(new LanMessage { type = "order", unitId = PlayerId, kind = "Hold" });
        }

        private void Send(LanMessage packet)
        {
            if (!Connected || _wire == null || _cancel == null) return;
            string wireText = LanMessage.Encode(packet);
            _ = SendSafeAsync(_wire, wireText, _cancel.Token, _generation);
        }

        private async Task SendSafeAsync(LanWire wire, string text, CancellationToken token, int generation)
        {
            try { await wire.SendLineAsync(text, token); }
            catch (Exception e) when (e is System.IO.IOException || e is SocketException ||
                                       e is OperationCanceledException || e is ObjectDisposedException)
            {
                Queue(generation, null, "ERROR:" + e.Message);
            }
        }

        public void Disconnect()
        {
            CancellationTokenSource source;
            LanWire wire;
            TcpClient socket;
            lock (_connectionGate)
            {
                _generation++;
                source = _cancel;
                wire = _wire;
                socket = _socket;
                _cancel = null;
                _wire = null;
                _socket = null;
            }
            if (source != null) source.Cancel();
            if (wire != null) wire.Dispose();
            if (socket != null) socket.Close();
            Connected = false;
            Playing = false;
            PlayerId = 0;
            Players = 0;
            ReadyCount = 0;
            Tick = 0;
            Status = "Не подключено";
        }
    }
}
