// Server-side identity and order authorization for the future REAL Sinbinder
// match. Pure C#: may be used by either a Unity host or a dedicated server.
// Does NOT execute AOS, damage, NavMesh, or gameplay locally on clients.
using System;
using System.Collections.Generic;

namespace Sinbinder.Multiplayer
{
    public enum NetworkActorRole
    {
        Lord = 1,
        NeutralWarrior = 2,
        AffiliatedWarrior = 3
    }

    // Do not assign numeric values from Gameplay.CommandKind: those values
    // are serialized into existing Unity scenes and must not be modified.
    public enum NetworkOrderKind
    {
        Move = 1,
        Attack = 2,
        Hold = 3,
        Defend = 4,
        FallBack = 5,
        Patrol = 6,
        AttackMove = 7
    }

    public enum OrderRejection
    {
        None,
        WrongProtocol,
        WrongMatch,
        UnknownPlayer,
        UnknownActor,
        NotAllowed,
        StaleOrDuplicate,
        InvalidPoint,
        TargetRequired,
        UnknownTarget,
        TargetNotVisible,
        UnsupportedOrder
    }

    [Serializable]
    public sealed class NetworkOrderIntent
    {
        public int Protocol = MatchAuthority.Protocol;
        public string MatchId;
        public long CommandId;
        public long ActorId;
        public long TargetId;
        public NetworkOrderKind Kind;
        public float X;
        public float Y;
        public float Z;
    }

    // Approved order carries the server-verified Lord name. It does not
    // carry a player-controlled "Lord" or "Team" property.
    public readonly struct AuthorizedGameOrder
    {
        public readonly long Sequence;
        public readonly long ExecuteTick;
        public readonly long CommandId;
        public readonly int ConnectionId;
        public readonly string Lord;
        public readonly long ActorId;
        public readonly long TargetId;
        public readonly NetworkOrderKind Kind;
        public readonly float X, Y, Z;

        internal AuthorizedGameOrder(long sequence, long tick, long commandId,
            int connectionId, string lord, NetworkOrderIntent input)
        {
            Sequence = sequence;
            ExecuteTick = tick;
            CommandId = commandId;
            ConnectionId = connectionId;
            Lord = lord;
            ActorId = input.ActorId;
            TargetId = input.TargetId;
            Kind = input.Kind;
            X = input.X;
            Y = input.Y;
            Z = input.Z;
        }
    }

    /// <summary>
    /// Registry/authorization only. The authoritative Unity simulation
    /// subsequently sends the approved order to Warrior.IssueCommand,
    /// where the 13 AOS modules choose whether to obey it.
    ///
    /// CRITICAL: neutral warriors may hear orders from BOTH Lords; they
    /// are not exclusively owned like the test capsules.
    /// </summary>
    public sealed class MatchAuthority
    {
        public const int Protocol = 1;
        private sealed class Player
        {
            public readonly string Lord;
            public readonly int Side;
            public bool Connected = true;
            public long LastCommandId;
            public Player(string lord, int side) { Lord = lord; Side = side; }
        }

        private sealed class Actor
        {
            public readonly NetworkActorRole Role;
            public readonly int OwnerConnection;
            public Actor(NetworkActorRole role, int owner)
            {
                Role = role;
                OwnerConnection = owner;
            }
        }

        private readonly Dictionary<int, Player> _players = new Dictionary<int, Player>();
        private readonly Dictionary<long, Actor> _actors = new Dictionary<long, Actor>();
        private long _nextActorId;
        private long _nextSequence;
        private readonly float _maxCoordinate;
        private readonly int _maxPlayers;
        private readonly long _delay;

        public string MatchId { get; private set; }
        public int ActorCount { get { return _actors.Count; } }
        public int PlayerCount { get { return _players.Count; } }
        public long LastSequence { get { return _nextSequence; } }

        public MatchAuthority(string matchId, float maxCoordinate = 10000f,
            int maxPlayers = 4, long commandDelayTicks = 3)
        {
            if (string.IsNullOrEmpty(matchId)) throw new ArgumentException("MatchId required");
            if (float.IsNaN(maxCoordinate) || float.IsInfinity(maxCoordinate) ||
                maxCoordinate <= 0) throw new ArgumentOutOfRangeException(nameof(maxCoordinate));
            if (maxPlayers < 2 || maxPlayers > 4) throw new ArgumentOutOfRangeException(nameof(maxPlayers));
            if (commandDelayTicks < 0) throw new ArgumentOutOfRangeException(nameof(commandDelayTicks));
            MatchId = matchId;
            _maxCoordinate = maxCoordinate;
            _maxPlayers = maxPlayers;
            _delay = commandDelayTicks;
        }

        /// <summary>Called by authenticated transport, NEVER by a client packet.</summary>
        public void RegisterPlayer(int connectionId, int side, string lord)
        {
            if (connectionId <= 0 || side < 0 || side >= _maxPlayers ||
                string.IsNullOrEmpty(lord))
                throw new ArgumentException("Invalid server player assignment");
            if (_players.ContainsKey(connectionId))
                throw new InvalidOperationException("Connection is already registered");
            if (_players.Count >= _maxPlayers)
                throw new InvalidOperationException("Match is full");
            foreach (var player in _players.Values)
                if (player.Lord == lord || player.Side == side)
                    throw new InvalidOperationException("Lord and side must be unique");
            _players.Add(connectionId, new Player(lord, side));
        }

        /// <summary>Disconnect preserves identity for a server-authorized reconnect.</summary>
        public void SetConnected(int connectionId, bool connected)
        {
            if (_players.TryGetValue(connectionId, out var player))
                player.Connected = connected;
        }

        /// <summary>Only server code allocates IDs, never Warrior.Id / client IDs.</summary>
        public long RegisterActor(NetworkActorRole role, int ownerConnection = 0)
        {
            if (role != NetworkActorRole.Lord && role != NetworkActorRole.NeutralWarrior &&
                role != NetworkActorRole.AffiliatedWarrior)
                throw new ArgumentException("Unknown role");
            if (role == NetworkActorRole.NeutralWarrior && ownerConnection != 0)
                throw new ArgumentException("Neutral warrior cannot have exclusive owner");
            if (role != NetworkActorRole.NeutralWarrior && !_players.ContainsKey(ownerConnection))
                throw new ArgumentException("Owned actor needs registered player");
            if (_nextActorId == long.MaxValue) throw new InvalidOperationException("Actor IDs exhausted");
            long id = ++_nextActorId;
            _actors.Add(id, new Actor(role, ownerConnection));
            return id;
        }

        public void RemoveActor(long actorId)
        {
            _actors.Remove(actorId);
        }

        /// <summary>Server-only replay cursor for a reconnected seat.</summary>
        public long LastAcceptedCommandId(int connectionId)
        {
            return _players.TryGetValue(connectionId, out var player)
                ? player.LastCommandId : 0L;
        }

        public bool TryGetLord(int connectionId, out string lord)
        {
            if (_players.TryGetValue(connectionId, out var player) && player.Connected)
            {
                lord = player.Lord;
                return true;
            }
            lord = null;
            return false;
        }

        // The connection ID is passed by the server's authenticated socket
        // context. It must not be decoded from an untrusted order payload.
        //
        // targetVisible is a server-side visibility check. For Attack, absence
        // of this check FAILS CLOSED. Clients cannot assert visibility.
        public bool TryApprove(int authenticatedConnectionId, NetworkOrderIntent intent,
            long currentTick, Func<long, long, bool> targetVisible,
            out AuthorizedGameOrder approved, out OrderRejection reason,
            int extraDelayTicks = 0)
        {
            approved = default;
            reason = OrderRejection.None;
            if (intent == null || intent.Protocol != Protocol)
            {
                reason = OrderRejection.WrongProtocol;
                return false;
            }
            if (!string.Equals(intent.MatchId, MatchId, StringComparison.Ordinal))
            {
                reason = OrderRejection.WrongMatch;
                return false;
            }
            if (!_players.TryGetValue(authenticatedConnectionId, out var player) ||
                !player.Connected)
            {
                reason = OrderRejection.UnknownPlayer;
                return false;
            }
            if (intent.CommandId <= player.LastCommandId || intent.CommandId <= 0)
            {
                reason = OrderRejection.StaleOrDuplicate;
                return false;
            }
            if (!_actors.TryGetValue(intent.ActorId, out var actor))
            {
                reason = OrderRejection.UnknownActor;
                return false;
            }
            if (actor.Role != NetworkActorRole.NeutralWarrior &&
                actor.OwnerConnection != authenticatedConnectionId)
            {
                reason = OrderRejection.NotAllowed;
                return false;
            }
            if (!Enum.IsDefined(typeof(NetworkOrderKind), intent.Kind))
            {
                reason = OrderRejection.UnsupportedOrder;
                return false;
            }
            if (!FiniteWithin(intent.X) || !FiniteWithin(intent.Y) ||
                !FiniteWithin(intent.Z))
            {
                reason = OrderRejection.InvalidPoint;
                return false;
            }
            if (intent.Kind == NetworkOrderKind.Attack)
            {
                if (intent.TargetId <= 0 || intent.TargetId == intent.ActorId)
                {
                    reason = OrderRejection.TargetRequired;
                    return false;
                }
                if (!_actors.ContainsKey(intent.TargetId))
                {
                    reason = OrderRejection.UnknownTarget;
                    return false;
                }
                if (targetVisible == null ||
                    !targetVisible(intent.ActorId, intent.TargetId))
                {
                    reason = OrderRejection.TargetNotVisible;
                    return false;
                }
            }
            else if (intent.TargetId != 0)
            {
                // Never allow a forged target for an unrelated command.
                reason = OrderRejection.NotAllowed;
                return false;
            }
            if (extraDelayTicks < 0 || extraDelayTicks > 3 ||
                currentTick < 0 ||
                currentTick > long.MaxValue - _delay - extraDelayTicks ||
                _nextSequence == long.MaxValue)
            {
                reason = OrderRejection.NotAllowed;
                return false;
            }

            player.LastCommandId = intent.CommandId;
            approved = new AuthorizedGameOrder(++_nextSequence,
                currentTick + _delay + extraDelayTicks, intent.CommandId,
                authenticatedConnectionId, player.Lord, intent);
            return true;
        }

        private bool FiniteWithin(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) &&
                Math.Abs(value) <= _maxCoordinate;
        }
    }
}
