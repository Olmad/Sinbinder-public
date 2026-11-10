// Lives on REAL Unity game objects which have been explicitly registered
// by an authoritative server. Not automatically attached to solo scenes.
using System;
using UnityEngine;

namespace Sinbinder.Multiplayer
{
    [DisallowMultipleComponent]
    public sealed class NetworkActorIdentity : MonoBehaviour
    {
        [SerializeField] private long _serverId;
        [SerializeField] private string _matchId;

        public long ServerId { get { return _serverId; } }
        public string MatchId { get { return _matchId; } }
        public bool Assigned { get { return _serverId > 0 && !string.IsNullOrEmpty(_matchId); } }

        // Only server-side registration calls this. A client's Warrior.Id
        // (locally generated GUID) must NEVER be used as a network identity.
        public void BindServerIdentity(long serverId, string matchId)
        {
            if (serverId <= 0 || string.IsNullOrEmpty(matchId))
                throw new ArgumentException("Invalid authoritative identity");
            if (Assigned && (_serverId != serverId ||
                             !string.Equals(_matchId, matchId, StringComparison.Ordinal)))
                throw new InvalidOperationException("Network identity cannot be reassigned");
            _serverId = serverId;
            _matchId = matchId;
        }

        public void ClearServerIdentity()
        {
            _serverId = 0;
            _matchId = null;
        }
    }
}
