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
            public int MessagesInWindow;
        }

        private struct Order
        {
            public long Tick;
            public long Sequence;
            public int PeerId;
            public string Kind;
            public Vector3 Destination;
        }

        private readonly List<Peer> _peers = new List<Peer>();
        private readonly ConcurrentQueue<Action> _inbox = new ConcurrentQueue<Action>();
        private readonly SortedDictionary<long, List<Order>> _orders =
            new SortedDictionary<long, List<Order>>();

        private TcpListener _listener;
        private CancellationTokenSource _cancel;
        private int _pending;
        private bool _active;
        private bool _started;
        private float _carried;
        private long _tick;
        private long _sequence;
        private Vector3 _first = new Vector3(-3, 0.8f, 0);
        private Vector3 _second = new Vector3(3, 0.8f, 0);
        private Vector3 _firstGoal;
        private Vector3 _secondGoal;

        public bool Active { get { return _active; } }
        public int PlayerCount { get { return _peers.Count; } }
        public long Tick { get { return _tick; } }
        public string Error { get; private set; }

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
                _active = true;
                _started = false;
                _carried = 0;
                _tick = 0;
                _sequence = 0;
                Error = null;
                _first = _firstGoal = new Vector3(-3, 0.8f, 0);
                _second = _secondGoal = new Vector3(3, 0.8f, 0);
                _orders.Clear();
                _ = AcceptLoopAsync(_cancel.Token);
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
            _active = false;
            _started = false;
            if (_cancel != null) _cancel.Cancel();
            if (_listener != null) _listener.Stop();
            foreach (var peer in _peers) peer.Wire.Dispose();
            _peers.Clear();
            _orders.Clear();
            while (_inbox.TryDequeue(out _)) { }
            _listener = null;
            // The accept/receive tasks may still observe the cancelled token.
            // Do not dispose its source until after their termination.
        }

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient socket = null;
                try
                {
                    socket = await _listener.AcceptTcpClientAsync();
                }
                catch (Exception e) when (e is SocketException || e is ObjectDisposedException)
                {
                    break;
                }

                if (socket == null) continue;
                var accepted = socket;
                Queue(() => AddPeer(accepted), () => accepted.Close());
            }
        }

        private void Queue(Action action, Action onDrop = null)
        {
            if (Interlocked.Increment(ref _pending) > 256)
            {
                Interlocked.Decrement(ref _pending);
                if (onDrop != null) onDrop();
                return;
            }
            _inbox.Enqueue(action);
        }

        private void AddPeer(TcpClient socket)
        {
            if (!_active || _started || _peers.Count >= 2)
            {
                socket.Close();
                return;
            }

            int id = _peers.Exists(p => p.Id == 1) ? 2 : 1;
            var peer = new Peer { Id = id, Wire = new LanWire(socket), WindowStarted = Time.realtimeSinceStartup };
            _peers.Add(peer);
            Send(peer, new LanMessage { type = "welcome", protocol = 1, playerId = id, unitId = id });
            PublishLobby();
            _ = ReadLoopAsync(peer, _cancel.Token);
        }

        private async Task ReadLoopAsync(Peer peer, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    string line = await peer.Wire.ReadLineAsync(token);
                    if (line == null) break;
                    var copy = line;
                    Queue(() => HandleMessage(peer, copy), () => peer.Wire.Dispose());
                }
            }
            catch (Exception e) when (e is IOException || e is SocketException ||
                                       e is OperationCanceledException ||
                                       e is ObjectDisposedException || e is InvalidDataException)
            {
                // Closing or misbehaving peers do not crash the match.
            }
            finally { Queue(() => RemovePeer(peer)); }
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

            if (!LanMessage.TryDecode(raw, out var packet))
            {
                SendError(peer, "Неверный формат сообщения");
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
                    _carried = 0;
                    _orders.Clear();
                    foreach (var p in _peers)
                        Send(p, new LanMessage { type = "start", tick = 0, playerId = p.Id, unitId = p.Id });
                    PublishState();
                }
                else PublishLobby();
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
            while (_inbox.TryDequeue(out var action))
            {
                Interlocked.Decrement(ref _pending);
                if (_active) action();
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
                Queue(() => RemovePeer(peer));
            }
        }
    }
}
