using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Sinbinder.Multiplayer
{
    /// <summary>
    /// Minimal authoritative LAN match for two capsule pawns. Never drives the
    /// Sinbinder story, combat, or AOS. Runs in the Unity player (including
    /// -batchmode -nographics), so no .NET 8 sidecar is required.
    /// </summary>
    public sealed class LanRoomServer
    {
        private sealed class Peer
        {
            public int Id;
            public LanWire Wire;
            public bool Ready;
            public float WindowStarted;
            public float LastPacketAt;
            public int MessagesInWindow;
            public int RemovalQueued;
            public long LastLordActorId = -1;
        }

        private struct Disconnected
        {
            public Peer Peer;
            public int Epoch;
        }

        private struct Order
        {
            public long Tick;
            public long Sequence;
            public int PeerId;
            public string Kind;
            public Vector3 Destination;
        }

        // Cleanup remains available even when an accepted socket is dropped
        // before Pump() has registered it as a Peer.
        private struct PendingAction
        {
            public Action Perform;
            public Action OnDrop;
            public int Epoch;
        }

        private readonly object _inboxGate = new object();
        private const int MaxMessagesPerPump = 128;
        private readonly List<Peer> _peers = new List<Peer>();
        private readonly ConcurrentQueue<PendingAction> _inbox =
            new ConcurrentQueue<PendingAction>();
        private readonly ConcurrentQueue<Disconnected> _disconnects = new ConcurrentQueue<Disconnected>();
        private readonly SortedDictionary<long, List<Order>> _orders =
            new SortedDictionary<long, List<Order>>();

        private TcpListener _listener;
        private CancellationTokenSource _cancel;
        private int _pending;
        private int _epoch;
        private bool _active;
        private bool _started;
        private float _carried;
        private long _tick;
        private long _sequence;
        private long _snapshotSequence;
        public const int MaxWorldActorsPerSnapshot = 24;
        private Vector3 _first = new Vector3(-3, 0.8f, 0);
        private Vector3 _second = new Vector3(3, 0.8f, 0);
        private Vector3 _firstGoal;
        private Vector3 _secondGoal;

        public bool Active { get { return _active; } }
        public int PlayerCount { get { return _peers.Count; } }
        public long Tick { get { return _tick; } }
        public string Error { get; private set; }

        // Explicitly installed only by the opt-in Multiplayer_Test host.
        // Callbacks run on Unity's main thread from Pump(), not a TCP task.
        public string GameMatchId { get; set; }
        public Func<int, long> ResolveLordActorId { get; set; }
        public Func<int, long> ResolveLastCommandId { get; set; }
        // Server-side projection must be private to each authenticated
        // connection. Never supply the raw world roster directly here.
        public Func<int, IReadOnlyList<LanWorldActorState>> PrivateWorldActors { get; set; }
        public Func<long> ResolveWorldTick { get; set; }
        public Func<int, NetworkOrderIntent, LanMessage> RouteGameOrder { get; set; }
        public Action<int, bool> PeerConnectionChanged { get; set; }

        public bool Start(int port)
        {
            if (_active) return true;
            if (port < 1 || port > 65535)
            {
                Error = "Порт должен быть от 1 до 65535";
                return false;
            }
            try
            {
                _cancel = new CancellationTokenSource();
                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Start(2);
                lock (_inboxGate)
                {
                    // Match each accepted socket with an epoch and make
                    // opening the queue atomic with closing the previous one.
                    _epoch++;
                    _active = true;
                }
                _started = false;
                _carried = 0;
                _tick = 0;
                _sequence = 0;
                _snapshotSequence = 0;
                Error = null;
                _first = _firstGoal = new Vector3(-3, 0.8f, 0);
                _second = _secondGoal = new Vector3(3, 0.8f, 0);
                _orders.Clear();
                _ = AcceptLoopAsync(_listener, _cancel.Token, _epoch);
                return true;
            }
            catch (Exception e) when (e is SocketException || e is ArgumentException)
            {
                Error = "Не удалось открыть LAN-сервер: " + e.Message;
                _active = false;
                if (_listener != null) _listener.Stop();
                if (_cancel != null) { _cancel.Cancel(); _cancel.Dispose(); }
                _listener = null;
                _cancel = null;
                return false;
            }
        }

        public void Stop()
        {
            if (!_active) return;
            // A receive/accept callback can attempt to Queue() at precisely
            // the same time as Stop(). Serialize invalidation with enqueue:
            // nothing can be left behind after the final queue drain.
            lock (_inboxGate)
            {
                _active = false;
                _epoch++;
                while (_inbox.TryDequeue(out var dropped))
                {
                    Interlocked.Decrement(ref _pending);
                    dropped.OnDrop?.Invoke();
                }
            }
            _started = false;
            if (_cancel != null) _cancel.Cancel();
            if (_listener != null) _listener.Stop();
            foreach (var peer in _peers)
            {
                PeerConnectionChanged?.Invoke(peer.Id, false);
                peer.Wire.Dispose();
            }
            _peers.Clear();
            _orders.Clear();
            while (_disconnects.TryDequeue(out _)) { }
            _listener = null;
            // The accept/receive tasks may still observe the cancelled token.
            // Do not dispose its source until after their termination.
        }

        private async Task AcceptLoopAsync(TcpListener listener, CancellationToken token, int epoch)
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient socket = null;
                try
                {
                    socket = await listener.AcceptTcpClientAsync();
                }
                catch (Exception e) when (e is SocketException || e is ObjectDisposedException)
                {
                    break;
                }

                if (socket == null) continue;
                var accepted = socket;
                Queue(() => AddPeer(accepted, epoch), epoch, () => accepted.Close());
            }
        }

        private void Queue(Action action, int epoch, Action onDrop = null)
        {
            bool accepted;
            lock (_inboxGate)
            {
                accepted = _active && epoch == _epoch &&
                    Volatile.Read(ref _pending) < 256;
                if (accepted)
                {
                    Interlocked.Increment(ref _pending);
                    _inbox.Enqueue(new PendingAction {
                        Perform = action, OnDrop = onDrop, Epoch = epoch
                    });
                }
            }
            // Dispose rejected sockets outside the lock. A Stop() cannot
            // miss this callback, even if it races with the enqueue attempt.
            if (!accepted) onDrop?.Invoke();
        }

        private void AddPeer(TcpClient socket, int epoch)
        {
            if (!_active || _started || _peers.Count >= 2)
            {
                socket.Close();
                return;
            }

            int id = _peers.Exists(p => p.Id == 1) ? 2 : 1;
            var peer = new Peer { Id = id, Wire = new LanWire(socket),
                WindowStarted = Time.realtimeSinceStartup, LastPacketAt = Time.realtimeSinceStartup };
            _peers.Add(peer);
            PeerConnectionChanged?.Invoke(id, true);
            Send(peer, new LanMessage { type = "welcome", protocol = 1, playerId = id, unitId = id });
            PublishLobby();
            _ = ReadLoopAsync(peer, _cancel.Token, epoch);
        }

        private async Task ReadLoopAsync(Peer peer, CancellationToken token, int epoch)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    string line = await peer.Wire.ReadLineAsync(token);
                    if (line == null) break;
                    var copy = line;
                    Queue(() => HandleMessage(peer, copy), epoch, () => peer.Wire.Dispose());
                }
            }
            catch (Exception e) when (e is IOException || e is SocketException ||
                                       e is OperationCanceledException ||
                                       e is ObjectDisposedException || e is InvalidDataException)
            {
                // Closing or misbehaving peers do not crash the match.
            }
            finally { EnqueueDisconnect(peer, epoch); }
        }

        private void EnqueueDisconnect(Peer peer, int epoch)
        {
            // Cleanup must NEVER be dropped when the bounded incoming queue
            // overflows. Coalesce repeated send failures to avoid an
            // unbounded disconnect queue when a peer stops reading.
            if (Interlocked.Exchange(ref peer.RemovalQueued, 1) != 0) return;
            _disconnects.Enqueue(new Disconnected { Peer = peer, Epoch = epoch });
        }

        private void HandleMessage(Peer peer, string raw)
        {
            if (!_active || !_peers.Contains(peer)) return;
            float now = Time.realtimeSinceStartup;
            if (now - peer.WindowStarted >= 1f)
            {
                peer.WindowStarted = now;
                peer.MessagesInWindow = 0;
            }
            if (++peer.MessagesInWindow > 25)
            {
                RemovePeer(peer);
                return;
            }
            peer.LastPacketAt = now;

            if (!LanMessage.TryDecode(raw, out var packet))
            {
                SendError(peer, "Неверный формат сообщения");
                return;
            }

            if (packet.type == "ping")
            {
                Send(peer, new LanMessage { type = "pong" });
                return;
            }

            if (packet.type == "ready")
            {
                if (_started) return;
                peer.Ready = true;
                if (_peers.Count == 2 && _peers.All(p => p.Ready))
                {
                    _started = true;
                    _tick = _sequence = 0;
                    _snapshotSequence = 0;
                    _carried = 0;
                    _orders.Clear();
                    // A room can be reused after one player disconnects.
                    // Never inherit the previous match's position or goal.
                    _first = _firstGoal = new Vector3(-3, 0.8f, 0);
                    _second = _secondGoal = new Vector3(3, 0.8f, 0);
                    foreach (var p in _peers)
                    {
                        Send(p, new LanMessage { type = "start", tick = 0, playerId = p.Id, unitId = p.Id });
                        SendGameIdentity(p);
                    }
                    PublishState();
                }
                else PublishLobby();
                return;
            }

            if (packet.type == "gameOrder")
            {
                // Keep real-gameplay packets completely separate from
                // synthetic capsule orders. The TCP socket's peer.Id is
                // authoritative; never accept a commanderId from the packet.
                if (!_started || RouteGameOrder == null ||
                    string.IsNullOrEmpty(GameMatchId))
                {
                    Send(peer, LanGameOrderCodec.Rejected(packet.commandId,
                        OrderRejection.NotAllowed));
                    return;
                }
                if (!LanGameOrderCodec.TryRead(packet, out var intent))
                {
                    Send(peer, LanGameOrderCodec.Rejected(packet.commandId,
                        OrderRejection.UnsupportedOrder));
                    return;
                }
                try
                {
                    var response = RouteGameOrder(peer.Id, intent);
                    Send(peer, response ?? LanGameOrderCodec.Rejected(
                        packet.commandId, OrderRejection.NotAllowed));
                }
                catch (Exception e)
                {
                    Debug.LogError("[Sinbinder LAN] Server game-order callback: " + e);
                    Send(peer, LanGameOrderCodec.Rejected(packet.commandId,
                        OrderRejection.NotAllowed));
                }
                return;
            }

            if (packet.type != "order")
            {
                SendError(peer, "Неизвестный тип запроса");
                return;
            }

            if (!_started)
            {
                SendError(peer, "Матч ещё не начат");
                return;
            }
            if (packet.unitId != peer.Id)
            {
                SendError(peer, "Чужим юнитом командовать нельзя");
                return;
            }
            if (packet.kind != "Move" && packet.kind != "Hold")
            {
                SendError(peer, "Неизвестный приказ");
                return;
            }
            if (float.IsNaN(packet.x) || float.IsInfinity(packet.x) ||
                float.IsNaN(packet.y) || float.IsInfinity(packet.y) ||
                float.IsNaN(packet.z) || float.IsInfinity(packet.z) ||
                Mathf.Abs(packet.x) > 8f || Mathf.Abs(packet.z) > 8f ||
                Mathf.Abs(packet.y) > 0.1f)
            {
                SendError(peer, "Недопустимая точка назначения");
                return;
            }
            var order = new Order
            {
                Tick = _tick + 3,
                Sequence = ++_sequence,
                PeerId = peer.Id,
                Kind = packet.kind,
                Destination = new Vector3(packet.x, 0.8f, packet.z)
            };
            if (!_orders.TryGetValue(order.Tick, out var scheduled))
            {
                scheduled = new List<Order>();
                _orders.Add(order.Tick, scheduled);
            }
            if (scheduled.Count >= 64)
            {
                SendError(peer, "Очередь переполнена");
                return;
            }
            scheduled.Add(order);
            Send(peer, new LanMessage { type = "accepted", executeTick = order.Tick, sequence = order.Sequence });
        }

        private void RemovePeer(Peer peer)
        {
            if (!_peers.Remove(peer)) return;
            PeerConnectionChanged?.Invoke(peer.Id, false);
            peer.Wire.Dispose();
            if (_started)
            {
                _started = false;
                _orders.Clear();
                foreach (var other in _peers) other.Ready = false;
                Broadcast(new LanMessage { type = "stopped", reason = "Другой игрок отключился" });
            }
            PublishLobby();
        }

        public void Pump(float unscaledDelta)
        {
            while (_disconnects.TryDequeue(out var removed))
                if (_active && removed.Epoch == _epoch) RemovePeer(removed.Peer);
            int processed = 0;
            // Never spend an unbounded amount of Unity frame time handling
            // network packets if peers send a burst of messages.
            while (processed < MaxMessagesPerPump && _inbox.TryDequeue(out var queued))
            {
                processed++;
                Interlocked.Decrement(ref _pending);
                if (_active && queued.Epoch == _epoch)
                    queued.Perform();
                else
                    queued.OnDrop?.Invoke();
            }
            while (_disconnects.TryDequeue(out var removed))
                if (_active && removed.Epoch == _epoch) RemovePeer(removed.Peer);

            // A silent TCP peer can remain 'connected' after its cable/Wi-Fi
            // disappears. Reap it and free its lobby slot independently
            // of an OS-level FIN/RST arriving.
            if (_active)
            {
                float now = Time.realtimeSinceStartup;
                for (int i = _peers.Count - 1; i >= 0; i--)
                    if (now - _peers[i].LastPacketAt > 20f)
                        RemovePeer(_peers[i]);
            }
            if (!_active || !_started) return;

            _carried += Mathf.Clamp(unscaledDelta, 0f, 0.25f);
            int steps = 0;
            while (_carried >= 0.05f && steps++ < 5)
            {
                _carried -= 0.05f;
                _tick++;
                if (_orders.TryGetValue(_tick, out var due))
                {
                    foreach (var o in due.OrderBy(o => o.Sequence))
                    {
                        if (o.PeerId == 1)
                            _firstGoal = o.Kind == "Hold" ? _first : o.Destination;
                        else
                            _secondGoal = o.Kind == "Hold" ? _second : o.Destination;

                        Broadcast(new LanMessage {
                            type = "execute", tick = _tick, sequence = o.Sequence,
                            commanderId = o.PeerId, unitId = o.PeerId, kind = o.Kind,
                            x = o.Destination.x, y = 0, z = o.Destination.z
                        });
                    }
                    _orders.Remove(_tick);
                }
                _first = Vector3.MoveTowards(_first, _firstGoal, 2.8f * 0.05f);
                _second = Vector3.MoveTowards(_second, _secondGoal, 2.8f * 0.05f);
                if (_tick % 2 == 0) PublishState();
            }
            if (steps > 5) _carried = 0f; // No unbounded catch-up after a frame stall.
        }

        /// <summary>
        /// Sends a FULL, separately filtered roster for each peer.
        /// The client swaps state only after worldEnd so previously
        /// visible or dead actors disappear rather than persisting.
        /// A bounded count and per-actor validation keep frames small.
        /// Called by the opt-in real-world Unity host on main thread.
        /// </summary>
        public void PublishPrivateWorldSnapshot()
        {
            if (!_active || !_started || string.IsNullOrEmpty(GameMatchId) ||
                PrivateWorldActors == null)
                return;
            long frame = ++_snapshotSequence;
            long worldTick = ResolveWorldTick != null ? ResolveWorldTick() : _tick;
            foreach (var peer in _peers)
            {
                IReadOnlyList<LanWorldActorState> actors;
                try { actors = PrivateWorldActors(peer.Id); }
                catch (Exception e)
                {
                    Debug.LogError("[Sinbinder LAN] private world filter failed: " + e);
                    continue; // fail closed; never send an unfiltered roster
                }
                if (actors == null || actors.Count > MaxWorldActorsPerSnapshot)
                    continue;
                bool valid = true;
                var unique = new HashSet<long>();
                for (int i = 0; i < actors.Count; i++)
                {
                    var a = actors[i];
                    if (a.Id <= 0 || !unique.Add(a.Id) ||
                        !LanWorldActorState.Valid(a.Position.x) ||
                        !LanWorldActorState.Valid(a.Position.y) ||
                        !LanWorldActorState.Valid(a.Position.z) ||
                        !a.ValidHealth())
                    {
                        valid = false;
                        break;
                    }
                }
                if (!valid) continue;
                Send(peer, new LanMessage {
                    type = "worldBegin", matchId = GameMatchId,
                    snapshotId = frame, actorCount = actors.Count,
                    tick = worldTick
                });
                foreach (var actor in actors)
                    Send(peer, LanWorldActorState.Packet(actor, GameMatchId, frame, worldTick));
                Send(peer, new LanMessage {
                    type = "worldEnd", matchId = GameMatchId,
                    snapshotId = frame, actorCount = actors.Count,
                    tick = worldTick
                });
            }
        }

        // Only send each peer their own Lord's ID. Neutral actor visibility
        // requires a separate server-side fog-of-war filtered roster.
        private void SendGameIdentity(Peer peer)
        {
            if (string.IsNullOrEmpty(GameMatchId) || RouteGameOrder == null) return;
            long actorId = ResolveLordActorId != null
                ? ResolveLordActorId(peer.Id) : 0L;
            peer.LastLordActorId = actorId;
            Send(peer, new LanMessage {
                type = "gameMatch", protocol = MatchAuthority.Protocol,
                matchId = GameMatchId, playerId = peer.Id, actorId = actorId,
                lastCommandId = ResolveLastCommandId != null
                    ? ResolveLastCommandId(peer.Id) : 0L
            });
        }

        public void RefreshGameIdentity()
        {
            if (!_active || !_started || string.IsNullOrEmpty(GameMatchId))
                return;
            foreach (var peer in _peers)
            {
                long current = ResolveLordActorId != null
                    ? ResolveLordActorId(peer.Id) : 0L;
                if (peer.LastLordActorId != current) SendGameIdentity(peer);
            }
        }

        // Only the issuer receives a command resolution. Broadcasting actor
        // identifiers would leak fog-of-war information to the rival.
        public void NotifyGameOrderResolved(AuthorizedGameOrder order, bool applied)
        {
            if (!_active || !_started || string.IsNullOrEmpty(GameMatchId)) return;
            var peer = _peers.Find(p => p.Id == order.ConnectionId);
            if (peer != null) Send(peer, LanGameOrderCodec.Resolved(order, applied));
        }

        private void PublishState()
        {
            Broadcast(new LanMessage
            {
                type = "state", tick = _tick,
                p1X = _first.x, p1Z = _first.z,
                p2X = _second.x, p2Z = _second.z
            });
        }

        private void PublishLobby()
        {
            Broadcast(new LanMessage
            {
                type = "lobby", players = _peers.Count,
                ready = _peers.Count(p => p.Ready)
            });
        }

        private void SendError(Peer peer, string message)
        {
            Send(peer, new LanMessage { type = "error", message = message });
        }

        private void Send(Peer peer, LanMessage packet)
        {
            if (!_active) return;
            string line = LanMessage.Encode(packet);
            _ = SendSafeAsync(peer, line, _cancel.Token);
        }

        private void Broadcast(LanMessage packet)
        {
            if (!_active) return;
            string line = LanMessage.Encode(packet);
            foreach (var peer in _peers) _ = SendSafeAsync(peer, line, _cancel.Token);
        }

        private async Task SendSafeAsync(Peer peer, string line, CancellationToken token)
        {
            try { await peer.Wire.SendLineAsync(line, token); }
            catch (Exception e) when (e is IOException || e is SocketException ||
                                       e is OperationCanceledException || e is ObjectDisposedException)
            {
                EnqueueDisconnect(peer, _epoch);
            }
        }
    }
}
