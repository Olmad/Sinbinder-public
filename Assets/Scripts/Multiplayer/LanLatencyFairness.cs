// Deterministic, server-side latency balancing. Network RTT alone cannot
// guarantee fairness, but a bounded extra queue delay for lower-latency
// peers can reduce systematic advantages without changing game rules.
// Never use values asserted by a client as an authoritative RTT.
using System;
using System.Collections.Generic;

namespace Sinbinder.Multiplayer
{
    public enum PingBalanceMode
    {
        Off = 0,
        Conservative = 1, // at most 100 ms of additional input delay
        Balanced = 2      // at most 150 ms of additional input delay
    }

    /// <summary>
    /// Called only on the authoritative server main thread. Records
    /// recent round-trip measurements from nonce-matched server probes.
    /// Command scheduling remains at 20 ticks/second (50ms per tick).
    /// </summary>
    public sealed class LanLatencyFairness
    {
        public const int TickMilliseconds = 50;
        private const int MaxSamples = 7;
        private const int MinSamples = 3;
        private const double MaxMeasuredRttMilliseconds = 1500.0;

        private sealed class Samples
        {
            public readonly List<double> Rtt = new List<double>();
        }

        private readonly Dictionary<int, Samples> _peers =
            new Dictionary<int, Samples>();
        public PingBalanceMode Mode { get; private set; }

        public LanLatencyFairness(PingBalanceMode mode = PingBalanceMode.Off)
        {
            SetMode(mode);
        }

        public void SetMode(PingBalanceMode mode)
        {
            if (!Enum.IsDefined(typeof(PingBalanceMode), mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            Mode = mode;
        }

        public void OnConnected(int connectionId)
        {
            if (connectionId <= 0) throw new ArgumentOutOfRangeException(nameof(connectionId));
            _peers[connectionId] = new Samples();
        }

        public void OnDisconnected(int connectionId)
        {
            _peers.Remove(connectionId);
        }

        // The caller must have verified the server-issued probe sequence.
        // Delayed, missing and out-of-order probes must NOT be recorded.
        public bool ObserveServerRtt(int connectionId, double milliseconds)
        {
            if (!_peers.TryGetValue(connectionId, out var peer) ||
                double.IsNaN(milliseconds) || double.IsInfinity(milliseconds) ||
                milliseconds <= 0 || milliseconds > MaxMeasuredRttMilliseconds)
                return false;
            peer.Rtt.Add(milliseconds);
            if (peer.Rtt.Count > MaxSamples)
                peer.Rtt.RemoveAt(0);
            return true;
        }

        public int SampleCount(int connectionId)
        {
            return _peers.TryGetValue(connectionId, out var peer)
                ? peer.Rtt.Count : 0;
        }

        public double MedianRttMs(int connectionId)
        {
            if (!_peers.TryGetValue(connectionId, out var peer) ||
                peer.Rtt.Count == 0)
                return -1.0;
            return Median(peer.Rtt);
        }

        public double JitterMs(int connectionId)
        {
            if (!_peers.TryGetValue(connectionId, out var peer) ||
                peer.Rtt.Count < 2)
                return -1.0;
            double med = Median(peer.Rtt);
            var deviations = new List<double>(peer.Rtt.Count);
            foreach (double rtt in peer.Rtt)
                deviations.Add(Math.Abs(rtt - med));
            return Median(deviations);
        }

        public int ExtraInputDelayTicks(int connectionId)
        {
            if (Mode == PingBalanceMode.Off || !_peers.ContainsKey(connectionId) ||
                _peers.Count < 2)
                return 0;
            double own = MedianRttMs(connectionId);
            if (own < 0) return 0;
            double highest = own;
            foreach (var entry in _peers)
            {
                // Do not penalize anybody based on a peer whose RTT has
                // not yet been measured consistently.
                if (entry.Value.Rtt.Count < MinSamples)
                    return 0;
                double rtt = MedianRttMs(entry.Key);
                if (rtt > highest) highest = rtt;
            }
            // Approximate the one-way difference as half the RTT delta.
            // Nonce-validated RTT is not a cheat-proof one-way timestamp.
            double estimatedDifference = Math.Max(0.0, (highest - own) / 2.0);
            int maxTicks = Mode == PingBalanceMode.Conservative ? 2 : 3;
            int extraTicks = (int)Math.Ceiling(estimatedDifference /
                TickMilliseconds - 1e-9);
            return Math.Max(0, Math.Min(maxTicks, extraTicks));
        }

        public int ExtraInputDelayMs(int connectionId)
        {
            return ExtraInputDelayTicks(connectionId) * TickMilliseconds;
        }

        private static double Median(List<double> values)
        {
            var copy = new List<double>(values);
            copy.Sort();
            int middle = copy.Count / 2;
            return (copy.Count % 2 == 1) ? copy[middle] :
                (copy[middle - 1] + copy[middle]) / 2.0;
        }
    }
}
