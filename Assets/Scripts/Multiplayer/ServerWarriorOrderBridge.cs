// Explicit, opt-in bridge between the future authoritative network match
// and the REAL Sinbinder warrior/AOS implementation. Not auto-installed in
// campaign scenes. This class must run on the Unity SERVER main thread.
using System;
using System.Collections.Generic;
using Sinbinder.Core;
using Sinbinder.Gameplay;
using UnityEngine;

namespace Sinbinder.Multiplayer
{
    /// <summary>
    /// Not a second AI. A server-validated order is handed to the existing
    /// Warrior.IssueCommand / IssuePatrol, so AOS still votes to obey or refuse.
    /// The caller must supply server-side visibility and hearing checks.
    /// </summary>
    public sealed class ServerWarriorOrderBridge
    {
        private readonly MatchAuthority _authority;
        private readonly Dictionary<long, Warrior> _warriors =
            new Dictionary<long, Warrior>();
        private readonly Dictionary<long, NetworkActorRole> _roles =
            new Dictionary<long, NetworkActorRole>();
        // Acknowledgement and execution are distinct. Previously the bridge
        // executed immediately even while reporting ExecuteTick = now + 3.
        private sealed class Scheduled
        {
            public AuthorizedGameOrder Order;
            public float Muffle;
            public Func<Warrior, Warrior, bool> Visible;
        }

        private readonly SortedDictionary<long, List<Scheduled>> _scheduled =
            new SortedDictionary<long, List<Scheduled>>();
        private const int MaxPendingOrders = 512;
        private int _pending;

        public int PendingOrders { get { return _pending; } }

        /// <summary>
        /// Emitted on authoritative simulation tick, never on receipt.
        /// If successful=false, world state changed after command acceptance
        /// (warrior died/unregistered, target vanished or became hidden).
        /// </summary>
        public event Action<AuthorizedGameOrder, bool> OrderResolved;

        public ServerWarriorOrderBridge(MatchAuthority authority)
        {
            _authority = authority ?? throw new ArgumentNullException(nameof(authority));
        }

        public long Register(Warrior warrior, NetworkActorRole role, int ownerConnection = 0)
        {
            if (warrior == null || warrior.IsDead)
                throw new ArgumentException("Warrior must be alive and initialized");
            // Neutral humans can listen to BOTH Lords; an AI Lord may itself
            // have Team.Neutral, so only validate the explicitly neutral role.
            if (role == NetworkActorRole.NeutralWarrior && warrior.Team != Team.Neutral)
                throw new ArgumentException("Neutral role requires a neutral Warrior");
            foreach (var entry in _warriors)
                if (entry.Value == warrior)
                    throw new InvalidOperationException("Warrior already has a network ID");

            var identity = warrior.GetComponent<NetworkActorIdentity>();
            if (identity != null && identity.Assigned)
                throw new InvalidOperationException("Warrior is still bound to another match");

            long id = _authority.RegisterActor(role, ownerConnection);
            try
            {
                if (identity == null)
                    identity = warrior.gameObject.AddComponent<NetworkActorIdentity>();
                identity.BindServerIdentity(id, _authority.MatchId);
                _warriors.Add(id, warrior);
                _roles.Add(id, role);
                return id;
            }
            catch
            {
                // A failed Unity component initialization must not leak an
                // authoritative actor ID into the match registry.
                _authority.RemoveActor(id);
                throw;
            }
        }

        public void Unregister(long id)
        {
            if (_warriors.TryGetValue(id, out var warrior) && warrior != null)
            {
                var identity = warrior.GetComponent<NetworkActorIdentity>();
                if (identity != null && identity.ServerId == id &&
                    identity.MatchId == _authority.MatchId)
                    identity.ClearServerIdentity();
            }
            _warriors.Remove(id);
            _roles.Remove(id);
            _authority.RemoveActor(id);
        }

        public bool TryGetWarrior(long id, out Warrior warrior)
        {
            if (_warriors.TryGetValue(id, out warrior) && warrior != null)
                return true;
            warrior = null;
            return false;
        }

        /// <param name="authenticatedConnectionId">
        /// Obtained from the server socket/transport session, never from JSON.
        /// </param>
        /// <param name="visibleOnServer">
        /// Authoritative LOS/fog check. Required for targeted attacks.
        /// </param>
        /// <param name="muffleOnServer">
        /// Server's voice attenuation for this Warrior and Lord. Required
        /// for warriors who hear orders; zero/absent cannot be trusted from
        /// the client. Pass null only when controlling a Lord's own movement.
        /// </param>
        public bool TryIssue(
            int authenticatedConnectionId,
            NetworkOrderIntent intent,
            long serverTick,
            Func<Warrior, Warrior, bool> visibleOnServer,
            Func<Warrior, string, float> muffleOnServer,
            out AuthorizedGameOrder approved,
            out OrderRejection reason)
        {
            approved = default;
            reason = OrderRejection.None;

            // Do not consume a CommandId on preflight failure.
            if (intent == null || !_warriors.TryGetValue(intent.ActorId, out var warrior) ||
                warrior == null || warrior.IsDead)
            {
                reason = OrderRejection.UnknownActor;
                return false;
            }

            Warrior target = null;
            if (intent.Kind == NetworkOrderKind.Attack)
            {
                if (!_warriors.TryGetValue(intent.TargetId, out target) ||
                    target == null || target.IsDead || target == warrior)
                {
                    reason = OrderRejection.UnknownTarget;
                    return false;
                }
                if (visibleOnServer == null || !visibleOnServer(warrior, target))
                {
                    reason = OrderRejection.TargetNotVisible;
                    return false;
                }
            }

            var role = _roles[intent.ActorId];
            UnitMover mover = null;
            float muffle = 0f;
            if (role == NetworkActorRole.Lord)
            {
                // The controlled Lord is a direct avatar, not an AOS pawn.
                if (intent.Kind != NetworkOrderKind.Move &&
                    intent.Kind != NetworkOrderKind.Attack)
                {
                    reason = OrderRejection.NotAllowed;
                    return false;
                }
                mover = warrior.GetComponent<UnitMover>();
                if (mover == null)
                {
                    reason = OrderRejection.NotAllowed;
                    return false;
                }
            }
            else
            {
                if (muffleOnServer == null)
                {
                    reason = OrderRejection.NotAllowed;
                    return false;
                }
                if (!_authority.TryGetLord(authenticatedConnectionId, out var lord))
                {
                    reason = OrderRejection.UnknownPlayer;
                    return false;
                }
                muffle = muffleOnServer(warrior, lord);
                if (float.IsNaN(muffle) || float.IsInfinity(muffle) ||
                    muffle < 0f || muffle > 1f || !Voice.Heard(muffle))
                {
                    reason = OrderRejection.NotAllowed;
                    return false;
                }
            }

            Func<long, long, bool> visible = null;
            if (intent.Kind == NetworkOrderKind.Attack)
                visible = (actorId, targetId) =>
                    actorId == intent.ActorId && targetId == intent.TargetId &&
                    target != null && !target.IsDead &&
                    visibleOnServer != null && visibleOnServer(warrior, target);

            if (_pending >= MaxPendingOrders)
            {
                reason = OrderRejection.NotAllowed;
                return false;
            }

            if (!_authority.TryApprove(authenticatedConnectionId, intent,
                    serverTick, visible, out approved, out reason))
                return false;

            if (!_scheduled.TryGetValue(approved.ExecuteTick, out var bucket))
            {
                bucket = new List<Scheduled>();
                _scheduled.Add(approved.ExecuteTick, bucket);
            }
            bucket.Add(new Scheduled {
                Order = approved, Muffle = muffle, Visible = visibleOnServer
            });
            _pending++;
            // The AOS proposal is issued only by ProcessDue on ExecuteTick.
            return true;
        }

        /// <summary>
        /// Advance using the AUTHORITATIVE Unity server tick. A bounded
        /// number of orders is accepted, so this remains predictable.
        /// The caller must invoke this on the Unity simulation thread.
        /// </summary>
        public void ProcessDue(long serverTick)
        {
            if (serverTick < 0) throw new ArgumentOutOfRangeException(nameof(serverTick));
            while (_scheduled.Count > 0)
            {
                long next = 0;
                foreach (var item in _scheduled)
                {
                    next = item.Key;
                    break;
                }
                if (next > serverTick) break;
                var bucket = _scheduled[next];
                _scheduled.Remove(next);
                // All orders in a tick have sequential global server IDs.
                // Preserve the server's acceptance order.
                foreach (var pending in bucket)
                {
                    _pending--;
                    bool applied = Apply(pending);
                    OrderResolved?.Invoke(pending.Order, applied);
                }
            }
        }

        /// <summary>Cancel queued game actions on server match teardown.</summary>
        public void CancelPending()
        {
            foreach (var bucket in _scheduled.Values)
                foreach (var pending in bucket)
                    OrderResolved?.Invoke(pending.Order, false);
            _scheduled.Clear();
            _pending = 0;
        }

        private bool Apply(Scheduled item)
        {
            var approved = item.Order;
            if (!_warriors.TryGetValue(approved.ActorId, out var warrior) ||
                warrior == null || warrior.IsDead)
                return false;
            Warrior target = null;
            if (approved.Kind == NetworkOrderKind.Attack)
            {
                if (!_warriors.TryGetValue(approved.TargetId, out target) ||
                    target == null || target.IsDead || target == warrior ||
                    item.Visible == null || !item.Visible(warrior, target))
                    return false;
            }

            Vector3 point = target != null ? target.transform.position
                : new Vector3(approved.X, approved.Y, approved.Z);
            if (_roles[approved.ActorId] == NetworkActorRole.Lord)
            {
                var mover = warrior.GetComponent<UnitMover>();
                if (mover == null) return false;
                if (approved.Kind == NetworkOrderKind.Attack)
                    mover.CommandAttack(target.gameObject);
                else
                    mover.CommandMove(point);
                return true;
            }

            if (approved.Kind == NetworkOrderKind.Patrol)
                warrior.IssuePatrol(point, item.Muffle, approved.Lord);
            else
                warrior.IssueCommand(Kind(approved.Kind), point,
                    target != null ? target.gameObject : null,
                    item.Muffle, approved.Lord);
            // AOS votes independently after the request reaches Warrior.
            return true;
        }

        private static CommandKind Kind(NetworkOrderKind kind)
        {
            switch (kind)
            {
                case NetworkOrderKind.Move: return CommandKind.Move;
                case NetworkOrderKind.Attack: return CommandKind.Attack;
                case NetworkOrderKind.Hold: return CommandKind.Hold;
                case NetworkOrderKind.Defend: return CommandKind.Defend;
                case NetworkOrderKind.FallBack: return CommandKind.FallBack;
                case NetworkOrderKind.AttackMove: return CommandKind.AttackMove;
                case NetworkOrderKind.Patrol: return CommandKind.Patrol;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }
}
