// Additive wire format for real-world orders. The existing "order" packets
// still control the isolated capsule proving ground unchanged.
using System;

namespace Sinbinder.Multiplayer
{
    public static class LanGameOrderCodec
    {
        public static LanMessage Write(NetworkOrderIntent intent)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            return new LanMessage
            {
                type = "gameOrder", protocol = intent.Protocol,
                matchId = intent.MatchId, commandId = intent.CommandId,
                actorId = intent.ActorId, targetId = intent.TargetId,
                kind = intent.Kind.ToString(),
                x = intent.X, y = intent.Y, z = intent.Z
            };
        }

        public static bool TryRead(LanMessage message, out NetworkOrderIntent intent)
        {
            intent = null;
            if (message == null || message.type != "gameOrder" ||
                message.commandId <= 0 || message.actorId <= 0 ||
                string.IsNullOrEmpty(message.matchId) ||
                !Enum.TryParse(message.kind, false, out NetworkOrderKind kind) ||
                !Enum.IsDefined(typeof(NetworkOrderKind), kind))
                return false;

            intent = new NetworkOrderIntent
            {
                Protocol = message.protocol,
                MatchId = message.matchId,
                CommandId = message.commandId,
                ActorId = message.actorId,
                TargetId = message.targetId,
                Kind = kind, X = message.x, Y = message.y, Z = message.z
            };
            return true;
        }

        public static LanMessage Accepted(AuthorizedGameOrder order)
        {
            return new LanMessage
            {
                type = "gameAccepted", matchId = null,
                commandId = order.CommandId, actorId = order.ActorId,
                sequence = order.Sequence, executeTick = order.ExecuteTick,
                kind = order.Kind.ToString()
            };
        }

        public static LanMessage Rejected(long commandId, OrderRejection reason)
        {
            return new LanMessage
            {
                type = "gameRejected", commandId = commandId,
                reason = reason.ToString()
            };
        }

        public static LanMessage Resolved(AuthorizedGameOrder order, bool applied)
        {
            return new LanMessage
            {
                type = "gameResolved", commandId = order.CommandId,
                actorId = order.ActorId, sequence = order.Sequence,
                tick = order.ExecuteTick, reason = applied ? "DeliveredToWarrior" : "Cancelled"
            };
        }
    }
}
