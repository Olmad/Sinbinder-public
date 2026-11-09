# Sinbinder: isolated networking prototype

This is **not** a playable multiplayer game. It is a separate, small .NET 8 TCP server
that does not depend on Unity and does not modify existing gameplay or deferred
`.cs.later` classes.

## Run

Install .NET 8 SDK, then:

```sh
dotnet run --project Tools/server/Sinbinder.Server.csproj -- --bind 127.0.0.1 --port 7777
```

The protocol is **UTF-8, one JSON object per line, TCP**, version 1.
Maximum inbound line: 4096 bytes. One server room has at most two players.
There is **no authentication or encryption**, so do not expose this publicly.
Loopback is the default; use a trusted VPN/LAN only for remote testing.

Example with two terminal connections (e.g. `nc 127.0.0.1 7777`):
1. Each connection receives `{"type":"welcome","protocol":1,"playerId":1,"unitId":1}`
   (the second has ID 2).
2. Each sends `{"type":"ready"}` followed by newline.
3. Both receive `{"type":"start","tick":0,...}`.
4. Player 1 sends `{"type":"order","unitId":1,"kind":"Move","x":2,"y":0,"z":3}`.
5. Sender receives `accepted`; both receive `execute` with an identical
   server-assigned tick and sequence.
6. Attempting to order the other player's unit receives `error`.
7. A disconnect stops the match and clears queued orders; the remaining player
   can wait for another player to connect and ready.

Supported test-pawn orders: `Move`, `Hold`. This server **does simulate
movement of two simple test pawns** at 20 ticks/second and broadcasts a
`state` snapshot (coordinates `p1X`, `p1Z`, `p2X`, `p2Z`) 10 times per
second. It does **not** simulate the real Sinbinder warriors, AOS, battles,
crypts, fog or economy.

This .NET 8 executable is **optional**. The Unity build contains an embedded
LAN host that speaks the same protocol and needs no external .NET installation.
See `Assets/Scripts/Multiplayer/README.md`.

## Boundaries / follow-up

- The existing Unity gameplay remains untouched. An isolated Unity LAN
  test arena is auto-created at runtime; no network packages required.
- Network event ordering and IDs are assigned by the server.
- Deterministic lockstep is **not** assumed: Unity NavMeshAgent and position-
  dependent AOS decisions can diverge between computers.
- Next integration needs real AOS match simulation, connection-to-Lord
  mapping, ownership checks on actual Warrior objects, world-state and
  fog-of-war replication. Choose a release-grade transport before shipping.
- Validate with `dotnet build Tools/server/Sinbinder.Server.csproj`,
  `python Tools/server/smoke_test.py` and
  `python Tools/server/unity_smoke_test.py` while a room is running.
  None of these runtime validations have been executed here.
