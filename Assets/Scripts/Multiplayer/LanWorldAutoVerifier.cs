// Optional runtime acceptance test for an ACTUAL Unity player build.
// It is NOT used in solo mode, normal LAN mode or during AOS simulation.
// Enabled only with -mp-verify-client on two separate client processes.
using System;
using UnityEngine;

namespace Sinbinder.Multiplayer
{
    public sealed class LanWorldAutoVerifier
    {
        private readonly LanRoomClient _client;
        private readonly bool _quitOnCompletion;
        private readonly float _begin;
        private float _issuedAt;
        private Vector3 _before;
        private long _lord;
        private long _frame;
        private long _tick;
        private bool _issued;
        private bool _accepted;
        private bool _delivered;
        private string _failed;

        public bool Completed { get; private set; }
        public bool Passed { get; private set; }
        public string Report { get; private set; }
        public int PlayerId { get { return _client.PlayerId; } }

        public LanWorldAutoVerifier(LanRoomClient client, bool quitOnCompletion = true)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _quitOnCompletion = quitOnCompletion;
            _begin = Time.realtimeSinceStartup;
            _client.Received += OnPacket;
            Debug.Log("[Sinbinder REAL Unity Acceptance] START; waiting for two " +
                "LAN clients and private real-Warrior world snapshot");
        }

        private void OnPacket(LanMessage packet)
        {
            if (!_issued || Completed) return;
            if (packet.type == "gameRejected")
                _failed = "Server rejected real Lord command: " + packet.reason;
            else if (packet.type == "gameAccepted")
                _accepted = true;
            else if (packet.type == "gameResolved")
            {
                if (packet.reason != "DeliveredToWarrior")
                    _failed = "Server did not deliver order: " + packet.reason;
                else
                    _delivered = true;
            }
        }

        public void Tick()
        {
            if (Completed) return;
            float now = Time.realtimeSinceStartup;
            if (!string.IsNullOrEmpty(_failed))
            {
                Finish(false, _failed);
                return;
            }
            if (now - _begin > 120f)
            {
                Finish(false, "Timed out waiting for complete two-client real-world test; " +
                    _client.Status + ", match=" + _client.GameMatchId +
                    ", worldFrame=" + _client.WorldFrame);
                return;
            }
            if (!_issued)
            {
                if (!_client.Connected || !_client.Playing ||
                    _client.PlayerId < 1 || _client.PlayerId > 2 ||
                    string.IsNullOrEmpty(_client.GameMatchId) ||
                    _client.OwnLordActorId <= 0 ||
                    _client.WorldFrame <= 0 ||
                    !_client.WorldActors.TryGetValue(_client.OwnLordActorId,
                        out var own))
                    return;

                if (!own.HasHealth)
                {
                    Finish(false, "Own real Lord lacks authoritative Damageable HP");
                    return;
                }

                _before = own.Position;
                _lord = _client.OwnLordActorId;
                _frame = _client.WorldFrame;
                _tick = _client.WorldTick;
                // An incremental move within the real 240m QA map.
                // This tests actual NavMesh/UnitMover, not synthetic capsules.
                var destination = new Vector3(_before.x + 1.2f,
                    _before.y, _before.z + 1.2f);
                if (!_client.OrderGameplay(_lord, NetworkOrderKind.Move, destination))
                {
                    Finish(false, "Could not send real Lord movement command");
                    return;
                }
                _issued = true;
                _issuedAt = now;
                Debug.Log("[Sinbinder REAL Unity Acceptance] ORDER sent; " +
                    "seat=" + _client.PlayerId + ", actor=" + _lord +
                    ", tick=" + _tick + ", frame=" + _frame +
                    ", hp=" + own.HP + "/" + own.MaxHP);
                return;
            }

            if (!_client.Connected || !_client.Playing)
            {
                Finish(false, "Connection closed before order completion");
                return;
            }

            if (now - _issuedAt > 35f)
            {
                Finish(false, "No real Lord movement after 35 seconds; " +
                    "accepted=" + _accepted + ", delivered=" + _delivered +
                    ", worldTick=" + _client.WorldTick +
                    ", worldFrame=" + _client.WorldFrame);
                return;
            }

            if (!_accepted || !_delivered || _client.WorldFrame <= _frame ||
                _client.WorldTick <= _tick ||
                !_client.WorldActors.TryGetValue(_lord, out var current))
                return;

            float dx = current.Position.x - _before.x;
            float dz = current.Position.z - _before.z;
            float displacement = Mathf.Sqrt(dx * dx + dz * dz);
            if (displacement < 0.15f) return;
            if (!current.HasHealth || current.HP < 0 ||
                current.HP > current.MaxHP)
            {
                Finish(false, "Invalid HP in updated real Lord snapshot");
                return;
            }
            Finish(true, "seat=" + _client.PlayerId + ", actor=" + _lord +
                ", match=" + _client.GameMatchId +
                ", receivedTick=" + _client.WorldTick +
                ", movedMeters=" + displacement.ToString("0.00") +
                ", hp=" + current.HP.ToString("0.0") +
                "/" + current.MaxHP.ToString("0.0"));
        }

        private void Finish(bool passed, string report)
        {
            if (Completed) return;
            Completed = true;
            Passed = passed;
            Report = report;
            if (passed)
                Debug.Log("[Sinbinder REAL Unity Acceptance] PASS " + report);
            else
                Debug.LogError("[Sinbinder REAL Unity Acceptance] FAIL " + report);
            if (_quitOnCompletion) Application.Quit(passed ? 0 : 1);
        }

        public void Dispose()
        {
            _client.Received -= OnPacket;
        }
    }
}
