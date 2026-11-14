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
        public readonly float HP;
        public readonly float MaxHP;
        public bool HasHealth { get { return MaxHP > 0f; } }

        public LanWorldActorState(long id, NetworkActorRole role, Vector3 position,
            float hp = -1f, float maxHP = 0f)
        {
            Id = id;
            Role = role;
            Position = position;
            HP = hp;
            MaxHP = maxHP;
        }

        public bool ValidHealth()
        {
            return (HP == -1f && MaxHP == 0f) ||
                (Valid(HP) && Valid(MaxHP) &&
                 HP >= 0f && MaxHP > 0f && HP <= MaxHP);
        }

        public static LanMessage Packet(LanWorldActorState actor,
            string matchId, long frameId, long tick)
        {
            return new LanMessage {
                type = "worldActor", matchId = matchId,
                snapshotId = frameId, tick = tick,
                actorId = actor.Id, actorRole = (int)actor.Role,
                x = actor.Position.x, y = actor.Position.y,
                z = actor.Position.z,
                hp = actor.HP, maxHp = actor.MaxHP
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
                new Vector3(packet.x, packet.y, packet.z),
                packet.hp, packet.maxHp);
            return actor.ValidHealth();
        }

        public static bool Valid(float coordinate)
        {
            return !float.IsNaN(coordinate) && !float.IsInfinity(coordinate) &&
                   Math.Abs(coordinate) <= 10000f;
        }
    }
}
