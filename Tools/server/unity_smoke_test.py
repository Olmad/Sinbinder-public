#!/usr/bin/env python3
"""Black-box smoke test for the Unity LAN host.

Start a built Sinbinder player with:
    Sinbinder.exe -batchmode -nographics -sinbinder-server -port 7777
or create a host using the in-game 'LAN тест' menu.

Then execute:
    python Tools/server/unity_smoke_test.py

Requires only the Python standard library; does not fake success if the
server is not running.
"""
import argparse
import json
import socket
import time


class Peer:
    def __init__(self, address, port):
        self.connection = socket.create_connection((address, port), timeout=5)
        self.connection.settimeout(8)
        self.source = self.connection.makefile("r", encoding="utf-8")
        self.id = 0

    def send(self, **packet):
        self.connection.sendall((json.dumps(packet, separators=(",", ":")) + "\n").encode())

    def receive(self, kind=None, timeout=8):
        # Server sends periodic 'state' packets; skip them when waiting
        # for an exact protocol event.
        stop = time.monotonic() + timeout
        while time.monotonic() < stop:
            line = self.source.readline()
            if not line:
                raise AssertionError("Disconnected while waiting for " + str(kind))
            packet = json.loads(line)
            if kind is None or packet.get("type") == kind:
                return packet
        raise AssertionError("Timeout waiting for " + str(kind))

    def close(self):
        try:
            self.connection.shutdown(socket.SHUT_RDWR)
        except OSError:
            pass
        self.connection.close()


def run(address, port):
    a = Peer(address, port)
    b = Peer(address, port)
    try:
        wa, wb = a.receive("welcome"), b.receive("welcome")
        assert wa["protocol"] == wb["protocol"] == 1
        assert {wa["playerId"], wb["playerId"]} == {1, 2}
        a.id, b.id = wa["playerId"], wb["playerId"]
        print("OK: separate connections and server-side identity")

        a.send(type="ready")
        b.send(type="ready")
        a.receive("start")
        b.receive("start")
        print("OK: lobby ready handshake")

        a.send(type="order", unitId=b.id, kind="Move", x=5, y=0, z=4)
        err = a.receive("error")
        assert "Чужим" in err["message"]
        print("OK: unauthorized orders rejected")

        a.send(type="order", unitId=a.id, kind="Move", x=-1, y=0, z=3)
        accepted = a.receive("accepted")
        executed_a = a.receive("execute")
        executed_b = b.receive("execute")
        assert executed_a == executed_b
        assert accepted["executeTick"] == executed_a["tick"]
        assert executed_a["commanderId"] == a.id
        print("OK: authoritative tick, sequence and identical orders")

        start = a.receive("state")
        print("OK: first snapshot at tick", start["tick"])
        move = False
        prev = start
        for _ in range(40):
            nxt = a.receive("state")
            key = "p1Z" if a.id == 1 else "p2Z"
            if abs(nxt[key] - prev[key]) > 0.02:
                move = True
                break
            prev = nxt
        assert move, "Position never changed after valid order"
        print("OK: server-authoritative movement replication")

        a.send(type="order", unitId=a.id, kind="Move", x=99999, y=0, z=0)
        err = a.receive("error")
        assert "точка" in err["message"]
        print("OK: invalid coordinates rejected")

        b.close()
        stopped = a.receive("stopped")
        assert stopped["reason"]
        print("OK: match stops on disconnect")
        print("PASS: Unity LAN server black-box checks")
    finally:
        a.close()
        b.close()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=7777)
    args = parser.parse_args()
    run(args.host, args.port)


if __name__ == "__main__":
    main()
