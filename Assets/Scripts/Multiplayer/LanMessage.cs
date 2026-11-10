using System;
using UnityEngine;

namespace Sinbinder.Multiplayer
{
    // Transport-neutral JSON-line envelope. The names deliberately match the
    // experimental .NET server in Tools/server. No Unity object references.
    [Serializable]
    public sealed class LanMessage
    {
        public string type;
        public string message;
        public string reason;
        public string kind;
        public int protocol;
        public int playerId;
        public int unitId;
        public int commanderId;
        // Additive, opt-in protocol for REAL-Warrior server commands.
        // Old capsule messages and the .NET 8 proof server remain valid.
        public string matchId;
        public long commandId;
        public long actorId;
        public long targetId;
        public int players;
        public int ready;
        public long tick;
        public long executeTick;
        public long sequence;
        public float x;
        public float y;
        public float z;
        public float p1X;
        public float p1Z;
        public float p2X;
        public float p2Z;

        public static string Encode(LanMessage data)
        {
            return JsonUtility.ToJson(data) + "\n";
        }

        public static bool TryDecode(string raw, out LanMessage data)
        {
            data = null;
            if (string.IsNullOrEmpty(raw) || raw.Length > 4096) return false;
            try
            {
                data = JsonUtility.FromJson<LanMessage>(raw);
                return data != null && !string.IsNullOrEmpty(data.type);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
