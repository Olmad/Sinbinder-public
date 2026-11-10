// First authoritative, visibility-filtered REAL gameplay state projection.
// Never send all warriors blindly: hidden opponents must not appear in TCP.
using System;
using UnityEngine;

namespace Sinbinder.Multiplayer
{
    public readonly struct LanWorldActorState
    {
        public readonly long Id;
        public readonly NetworkActorRole Role;
        public readonly Vector3 Position;

        public LanWorldActorState(long id, NetworkActorRole role, Vector3 position)
        {
            Id = id;
            Role = role;
            Position = position;
        }

        public static LanMessage Packet(LanWorldActorState actor,
            string matchId, long frameId, long tick)
        {
            return new LanMessage {
                type = "worldActor", matchId = matchId,
                snapshotId = frameId, tick = tick,
                actorId = actor.Id, actorRole = (int)actor.Role,
                x = actor.Position.x, y = actor.Position.y,
                z = actor.Position.z
            };
        }

        public static bool TryRead(LanMessage packet, string matchId,
            long frameId, out LanWorldActorState actor)
        {
            actor = default;
            if (packet == null || packet.type != "worldActor" ||
                !string.Equals(packet.matchId, matchId, StringComparison.Ordinal) ||
                packet.snapshotId != frameId || packet.actorId <= 0 ||
                packet.actorRole < (int)NetworkActorRole.Lord ||
                packet.actorRole > (int)NetworkActorRole.AffiliatedWarrior ||
                !Valid(packet.x) || !Valid(packet.y) || !Valid(packet.z))
                return false;
            actor = new LanWorldActorState(packet.actorId,
                (NetworkActorRole)packet.actorRole,
                new Vector3(packet.x, packet.y, packet.z));
            return true;
        }

        public static bool Valid(float coordinate)
        {
            return !float.IsNaN(coordinate) && !float.IsInfinity(coordinate) &&
                   Math.Abs(coordinate) <= 10000f;
        }
    }
}
