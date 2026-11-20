using System;
using System.Threading.Tasks;
using Sinbinder.Multiplayer;
using UnityEngine;

internal static class LatencyFairnessChecks
{
    private static void Check(bool yes, string why)
    {
        if (!yes) throw new Exception("Latency fairness regression: " + why);
    }

    public static async Task Run()
    {
        var balance = new LanLatencyFairness(PingBalanceMode.Conservative);
        balance.OnConnected(1);
        balance.OnConnected(2);
        // No untrusted or insufficient RTT samples may penalize a player.
        Check(balance.ExtraInputDelayTicks(1) == 0,
            "balance must be off until BOTH players have enough server RTT samples");
        foreach (double rtt in new [] { 20.0, 21.0, 19.0 })
            Check(balance.ObserveServerRtt(1, rtt), "low RTT samples valid");
        foreach (double rtt in new [] { 200.0, 210.0 })
            Check(balance.ObserveServerRtt(2, rtt), "high RTT samples valid");
        Check(balance.ExtraInputDelayTicks(1) == 0,
            "unknown opponent RTT must not introduce artificial lag");
        Check(balance.ObserveServerRtt(2, 190.0), "third server RTT sample");
        Check(balance.ExtraInputDelayTicks(1) == 2 &&
              balance.ExtraInputDelayTicks(2) == 0,
            "slower peer has no added delay; 20ms peer is capped at 100ms");
        Check(Math.Abs(balance.MedianRttMs(1) - 20.0) < .1,
            "median RTT ignores small variations");
        Check(balance.JitterMs(1) <= 1.1, "jitter calculated robustly");
        Check(!balance.ObserveServerRtt(1, double.NaN) &&
              !balance.ObserveServerRtt(1, double.PositiveInfinity) &&
              !balance.ObserveServerRtt(1, -5.0) &&
              !balance.ObserveServerRtt(1, 20000.0),
            "invalid fake RTT never recorded");
        balance.SetMode(PingBalanceMode.Off);
        Check(balance.ExtraInputDelayTicks(1) == 0,
            "Off mode must never delay gameplay");
        balance.SetMode(PingBalanceMode.Balanced);
        foreach (double rtt in new [] { 600.0, 590.0, 610.0 })
            Check(balance.ObserveServerRtt(2, rtt), "high RTT samples valid");
        // Median of 200,210,190,600,590,610 = 400; ~190ms extra
        // one-way difference. Balanced clamps to 150ms.
        Check(balance.ExtraInputDelayTicks(1) == 3,
            "Balanced caps low-ping penalty at 150 ms");
        balance.OnDisconnected(2);
        Check(balance.ExtraInputDelayTicks(1) == 0,
            "disconnected peer cannot continue to delay remaining player");

        var match = new MatchAuthority("latency-test");
        match.RegisterPlayer(1, 0, "Lord A");
        var lord = match.RegisterActor(NetworkActorRole.Lord, 1);
        var input = new NetworkOrderIntent {
            MatchId = "latency-test", Protocol = MatchAuthority.Protocol,
            CommandId = 1, ActorId = lord, Kind = NetworkOrderKind.Move,
            X = 0f, Y = 0f, Z = 2f
        };
        Check(match.TryApprove(1, input, 10, null,
            out var approved, out var reason, 2) &&
            approved.ExecuteTick == 15,
            "approved real order uses base 3 ticks + server-owned 2-tick balance");
        input.CommandId = 2;
        Check(!match.TryApprove(1, input, 10, null,
            out _, out reason, 999) &&
            reason == OrderRejection.NotAllowed,
            "server refuses forged excessive input delay");
        Check(match.TryApprove(1, input, 10, null,
            out var normal, out _, 0) && normal.ExecuteTick == 13,
            "default input path stays backward-compatible");

        // Real client/server sockets: server sends nonce probe; client
        // replies without self-reporting RTT, displays server telemetry.
        var room = new LanRoomServer();
        var a = new LanRoomClient();
        var b = new LanRoomClient();
        try
        {
            room.PingBalanceSetting = PingBalanceMode.Balanced;
            Check(room.Start(7816), "room listening");
            a.Connect("127.0.0.1", 7816);
            b.Connect("127.0.0.1", 7816);
            bool gotBoth = false;
            for (int i = 0; i < 300; i++)
            {
                // The CI shim's monotonic virtual time avoids a slow test.
                Time.Advance(0.018f);
                room.Pump(.018f);
                a.Pump();
                b.Pump();
                if (a.ServerMeasuredPingMs > 0f &&
                    b.ServerMeasuredPingMs > 0f &&
                    a.PingBalanceSetting == "Balanced" &&
                    b.PingBalanceSetting == "Balanced")
                {
                    gotBoth = true;
                    break;
                }
                await Task.Delay(10);
            }
            Check(gotBoth,
                "both actual TCP clients receive RTT sampled by server challenge");
            Check(a.FairInputDelayMs <= 150 && b.FairInputDelayMs <= 150,
                "no arbitrary large ping equalization on either client");
            Console.WriteLine("PASS: ping challenge RTT, jitter and capped per-player fairness using actual TCP clients");
        }
        finally
        {
            a.Disconnect();
            b.Disconnect();
            room.Stop();
        }
    }
}
