#!/usr/bin/env python3
"""Run after starting the prototype server on 127.0.0.1:7777.

Only uses the Python standard library. Tests handshake, ready/start,
authorized command broadcast and rejection of a forged unit ID.
"""
import json
import socket
import sys
import time

HOST = "127.0.0.1"
PORT = 7777


class Client:
    def __init__(self):
        self.sock = socket.create_connection((HOST, PORT), timeout=5)
        self.sock.settimeout(5)
        self.file = self.sock.makefile("r", encoding="utf-8")

    def send(self, **message):
        self.sock.sendall((json.dumps(message) + "\n").encode("utf-8"))

    def until(self, expected):
        deadline = time.monotonic() + 5
        while time.monotonic() < deadline:
            line = self.file.readline()
            if not line:
                raise AssertionError("Unexpected disconnect")
            item = json.loads(line)
            if item.get("type") == expected:
                return item
        raise AssertionError("Did not receive " + expected)

    def close(self):
        self.sock.close()


def main():
    first = Client()
    second = Client()
    try:
        a = first.until("welcome")
        b = second.until("welcome")
        assert {a["playerId"], b["playerId"]} == {1, 2}

        first.send(type="ready")
        second.send(type="ready")
        first.until("start")
        second.until("start")

        first.send(type="order", unitId=b["unitId"], kind="Move", x=1, y=0, z=1)
        assert "not owned" in first.until("error")["message"].lower()

        first.send(type="order", unitId=a["unitId"], kind="Move", x=1, y=0, z=1)
        accepted = first.until("accepted")
        event_a = first.until("execute")
        event_b = second.until("execute")
        assert event_a == event_b
        assert accepted["executeTick"] == event_a["tick"]
        assert event_a["commanderId"] == a["playerId"]
        assert event_a["unitId"] == a["unitId"]
        print("PASS: handshake, lobby, ready, ownership, broadcast, tick")
        return 0
    finally:
        first.close()
        second.close()


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as exc:
        print("FAIL:", exc, file=sys.stderr)
        sys.exit(1)
