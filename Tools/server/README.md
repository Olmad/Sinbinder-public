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

Supported order kinds for protocol proof only: `Move`, `Hold`. The server
**does not move a unit** or simulate AOS, battles, crypts, fog, or economy. An
`execute` event is an authorized input event, **not a world-state snapshot**.

## Boundaries / follow-up

- Unity project remains untouched; no new packages or GameObjects.
- Network event ordering and IDs are assigned by the server.
- Deterministic lockstep is **not** assumed: Unity NavMeshAgent and position-
  dependent AOS decisions can diverge between computers.
- Next integration needs a client transport, server-side match simulation,
  connection-to-Lord identity mapping, ownership validation using game data,
  and server-authoritative visibility/state replication. Decide whether
  to replace TCP with Unity Transport before shipping.
- Validate with `dotnet build Tools/server/Sinbinder.Server.csproj` and two
  simultaneous clients. No Unity or .NET execution occurred during creation.
