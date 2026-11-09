using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sinbinder.Server;

// Isolated networking prototype. It does NOT simulate Unity combat or AOS.
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        string bind = "127.0.0.1";
        int port = 7777;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--bind" && i + 1 < args.Length) bind = args[++i];
            else if (args[i] == "--port" && i + 1 < args.Length &&
                     int.TryParse(args[++i], out int parsed)) port = parsed;
            else
            {
                Console.Error.WriteLine("Usage: dotnet run -- [--bind 127.0.0.1] [--port 7777]");
                return 2;
            }
        }

        if (!IPAddress.TryParse(bind, out var ip) || port is < 1 or > 65535)
        {
            Console.Error.WriteLine("Invalid address or port.");
            return 2;
        }

        using var cancel = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancel.Cancel(); };
        var listener = new TcpListener(ip, port);
        var room = new Room();
        listener.Start();
        using var stopListener = cancel.Token.Register(() => listener.Stop());
        Console.WriteLine($"Sinbinder prototype server listening on {ip}:{port} (TCP)");
        if (!IPAddress.IsLoopback(ip))
            Console.WriteLine("WARNING: no encryption or authentication. Use only a trusted LAN/VPN.");

        var ticks = room.RunTicksAsync(cancel.Token);
        try
        {
            while (!cancel.IsCancellationRequested)
            {
                TcpClient socket;
                try { socket = await listener.AcceptTcpClientAsync(cancel.Token); }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) when (cancel.IsCancellationRequested) { break; }
                catch (SocketException) when (cancel.IsCancellationRequested) { break; }

                socket.NoDelay = true;
                _ = room.HandleConnectionAsync(socket, cancel.Token);
            }
        }
        finally
        {
            cancel.Cancel();
            listener.Stop();
            await ticks;
        }

        return 0;
    }
}

internal sealed class ClientPacket
{
    public string? Type { get; set; }
    public int UnitId { get; set; }
    public string? Kind { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

internal sealed class Peer : IDisposable
{
    private const int MaxLineBytes = 4096;
    private readonly NetworkStream _stream;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly byte[] _one = new byte[1];
    public int Id { get; }
    public bool Ready { get; set; }
    public TcpClient Socket { get; }

    public Peer(int id, TcpClient socket)
    {
        Id = id;
        Socket = socket;
        _stream = socket.GetStream();
    }

    // Read a bounded UTF-8 message. Never allocate from an untrusted length.
    public async Task<string?> ReadLineAsync(CancellationToken token)
    {
        using var buffer = new MemoryStream();
        while (true)
        {
            int count = await _stream.ReadAsync(_one, token);
            if (count == 0) return null;
            if (_one[0] == (byte)'\n') break;
            if (buffer.Length >= MaxLineBytes)
                throw new InvalidDataException("Message exceeds 4096 bytes.");
            buffer.WriteByte(_one[0]);
        }
        return Encoding.UTF8.GetString(buffer.ToArray()).TrimEnd('\r');
    }

    public async Task SendAsync(object message, CancellationToken token)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, Room.JsonOptions) + "\n");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        await _sendLock.WaitAsync(timeout.Token);
        try { await _stream.WriteAsync(bytes, timeout.Token); }
        finally { _sendLock.Release(); }
    }

    public void Dispose()
    {
        Socket.Dispose();
        _sendLock.Dispose();
    }
}

internal readonly record struct ScheduledOrder(
    long Tick, long Sequence, int CommanderId, int UnitId,
    string Kind, double X, double Y, double Z);

internal sealed class Room
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
    };

    private readonly object _gate = new();
    private readonly List<Peer> _peers = new();
    private readonly SortedDictionary<long, List<ScheduledOrder>> _orders = new();
    private bool _started;
    private long _tick;
    private long _sequence;
    private const int TickRate = 20;
    private const int CommandDelay = 3;
    private double _p1X = -3, _p1Z, _p2X = 3, _p2Z;
    private double _p1GoalX = -3, _p1GoalZ, _p2GoalX = 3, _p2GoalZ;

    public async Task HandleConnectionAsync(TcpClient socket, CancellationToken token)
    {
        Peer? peer = null;
        lock (_gate)
        {
            if (!_started && _peers.Count < 2)
            {
                int id = _peers.Any(p => p.Id == 1) ? 2 : 1;
                peer = new Peer(id, socket);
                _peers.Add(peer);
            }
        }

        if (peer == null)
        {
            using (socket)
            {
                try
                {
                    var bytes = Encoding.UTF8.GetBytes("{\"type\":\"error\",\"message\":\"Room full or match already started\"}\n");
                    await socket.GetStream().WriteAsync(bytes, token);
                }
                catch (IOException) { }
                catch (SocketException) { }
            }
            return;
        }

        try
        {
            await peer.SendAsync(new { type = "welcome", protocol = 1, playerId = peer.Id, unitId = peer.Id }, token);
            await PublishLobbyAsync(token);
            while (!token.IsCancellationRequested)
            {
                var line = await peer.ReadLineAsync(token);
                if (line == null) break;
                ClientPacket? packet;
                try { packet = JsonSerializer.Deserialize<ClientPacket>(line, JsonOptions); }
                catch (JsonException)
                {
                    await ErrorAsync(peer, "Malformed JSON", token);
                    continue;
                }

                if (packet?.Type == "ready")
                    await MarkReadyAsync(peer, token);
                else if (packet?.Type == "order")
                    await AcceptOrderAsync(peer, packet, token);
                else
                    await ErrorAsync(peer, "Unknown request type", token);
            }
        }
        catch (Exception ex) when (ex is IOException or SocketException or
                                   OperationCanceledException or InvalidDataException or
                                   ObjectDisposedException)
        {
            // A bad connection must not crash the server or the other player.
        }
        finally
        {
            bool stopped;
            lock (_gate)
            {
                _peers.Remove(peer);
                stopped = _started;
                if (stopped)
                {
                    _started = false;
                    _orders.Clear();
                    foreach (var other in _peers) other.Ready = false;
                }
            }
            peer.Dispose();
            if (stopped) await BroadcastSafeAsync(new { type = "stopped", reason = "Player disconnected" }, token);
            await PublishLobbyAsync(token);
        }
    }

    private async Task MarkReadyAsync(Peer peer, CancellationToken token)
    {
        bool start;
        Peer[] players;
        lock (_gate)
        {
            if (_started) { start = false; players = Array.Empty<Peer>(); }
            else
            {
                peer.Ready = true;
                start = _peers.Count == 2 && _peers.All(p => p.Ready);
                if (start)
                {
                    _started = true;
                    _tick = 0;
                    _sequence = 0;
                    _orders.Clear();
                    _p1X = _p1GoalX = -3;
                    _p1Z = _p1GoalZ = 0;
                    _p2X = _p2GoalX = 3;
                    _p2Z = _p2GoalZ = 0;
                }
                players = start ? _peers.ToArray() : Array.Empty<Peer>();
            }
        }
        if (start)
        {
            foreach (var player in players)
                await SendSafeAsync(player, new { type = "start", tick = 0, playerId = player.Id, unitId = player.Id }, token);
            await PublishStateAsync(token);
        }
        else
            await PublishLobbyAsync(token);
    }

    private async Task AcceptOrderAsync(Peer peer, ClientPacket packet, CancellationToken token)
    {
        ScheduledOrder scheduled = default;
        string? error = null;
        lock (_gate)
        {
            if (!_started) error = "Match has not started";
            else if (packet.UnitId != peer.Id) error = "Чужим юнитом командовать нельзя";
            else if (packet.Kind is not ("Move" or "Hold")) error = "Неизвестный приказ";
            else if (!double.IsFinite(packet.X) || !double.IsFinite(packet.Y) || !double.IsFinite(packet.Z) ||
                     Math.Abs(packet.X) > 8 || Math.Abs(packet.Z) > 8 || Math.Abs(packet.Y) > 0.1)
                error = "Недопустимая точка назначения";
            else
            {
                long due = _tick + CommandDelay;
                scheduled = new ScheduledOrder(due, ++_sequence, peer.Id, packet.UnitId,
                    packet.Kind!, packet.X, packet.Y, packet.Z);
                if (!_orders.TryGetValue(due, out var list))
                    _orders.Add(due, list = new List<ScheduledOrder>());
                if (list.Count >= 64) error = "Очередь переполнена";
                else list.Add(scheduled);
            }
        }
        if (error != null) await ErrorAsync(peer, error, token);
        else await SendSafeAsync(peer, new { type = "accepted", executeTick = scheduled.Tick, sequence = scheduled.Sequence }, token);
    }

    public async Task RunTicksAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1.0 / TickRate));
        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                List<ScheduledOrder>? due = null;
                bool publish;
                lock (_gate)
                {
                    if (!_started) continue;
                    ++_tick;
                    if (_orders.Remove(_tick, out var batch)) due = batch;
                    if (due != null)
                    {
                        foreach (var order in due.OrderBy(o => o.Sequence))
                        {
                            if (order.CommanderId == 1)
                            {
                                _p1GoalX = order.Kind == "Hold" ? _p1X : order.X;
                                _p1GoalZ = order.Kind == "Hold" ? _p1Z : order.Z;
                            }
                            else
                            {
                                _p2GoalX = order.Kind == "Hold" ? _p2X : order.X;
                                _p2GoalZ = order.Kind == "Hold" ? _p2Z : order.Z;
                            }
                        }
                    }
                    (_p1X, _p1Z) = MoveTowards(_p1X, _p1Z, _p1GoalX, _p1GoalZ);
                    (_p2X, _p2Z) = MoveTowards(_p2X, _p2Z, _p2GoalX, _p2GoalZ);
                    publish = _tick % 2 == 0;
                }

                if (due != null)
                    foreach (var order in due.OrderBy(o => o.Sequence))
                        await BroadcastSafeAsync(new
                        {
                            type = "execute", tick = order.Tick, sequence = order.Sequence,
                            commanderId = order.CommanderId, unitId = order.UnitId,
                            kind = order.Kind, x = order.X, y = order.Y, z = order.Z
                        }, token);
                if (publish) await PublishStateAsync(token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    // Same speed and initial positions as the built-in Unity LAN server.
    private static (double x, double z) MoveTowards(
        double x, double z, double goalX, double goalZ)
    {
        double dx = goalX - x, dz = goalZ - z;
        double distance = Math.Sqrt(dx * dx + dz * dz);
        const double step = 2.8 / TickRate;
        if (distance <= step || distance == 0) return (goalX, goalZ);
        return (x + step * dx / distance, z + step * dz / distance);
    }

    private async Task PublishStateAsync(CancellationToken token)
    {
        object state;
        lock (_gate)
        {
            if (!_started) return;
            state = new
            {
                type = "state", tick = _tick,
                p1X = _p1X, p1Z = _p1Z,
                p2X = _p2X, p2Z = _p2Z
            };
        }
        await BroadcastSafeAsync(state, token);
    }

    private async Task PublishLobbyAsync(CancellationToken token)
    {
        int players, ready;
        lock (_gate)
        {
            players = _peers.Count;
            ready = _peers.Count(p => p.Ready);
        }
        await BroadcastSafeAsync(new { type = "lobby", players, ready }, token);
    }

    private static Task ErrorAsync(Peer peer, string reason, CancellationToken token)
        => SendSafeAsync(peer, new { type = "error", message = reason }, token);

    private static async Task SendSafeAsync(Peer peer, object message, CancellationToken token)
    {
        try { await peer.SendAsync(message, token); }
        catch (Exception ex) when (ex is IOException or SocketException or
                                   OperationCanceledException or ObjectDisposedException) { }
    }

    private async Task BroadcastSafeAsync(object message, CancellationToken token)
    {
        Peer[] peers;
        lock (_gate) peers = _peers.ToArray();
        await Task.WhenAll(peers.Select(p => SendSafeAsync(p, message, token)));
    }
}
