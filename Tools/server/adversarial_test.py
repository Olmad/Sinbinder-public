#!/usr/bin/env python3
"""Adversarial end-to-end checks for the optional .NET 8 TCP server.

Run while the server listens on 127.0.0.1:7777.
"""
import json
import socket
import time


class Peer:
    def __init__(self):
        self.socket = socket.create_connection(("127.0.0.1", 7777), timeout=4)
        self.socket.settimeout(4)
        self.reader = self.socket.makefile("r", encoding="utf-8")

    def until(self, kind, limit=100):
        for _ in range(limit):
            line = self.reader.readline()
            if not line:
                raise AssertionError(f"Unexpected close waiting for {kind}")
            item = json.loads(line)
            if item.get("type") == kind:
                return item
        raise AssertionError(f"Message {kind} not received")

    def raw(self, payload):
        self.socket.sendall(payload)

    def close(self):
        self.reader.close()
        self.socket.close()


def main():
    a, b = Peer(), Peer()
    c = None
    replacement = None
    try:
        first, second = a.until("welcome"), b.until("welcome")
        assert {first["playerId"], second["playerId"]} == {1, 2}

        c = Peer()
        rejected = c.until("error")
        assert "full" in rejected["message"].lower()
        c.close()
        c = None
        print("PASS: third simultaneous client rejected")

        # A flood used to be accepted without a per-peer limit.
        a.raw(b'{"type":"unrecognized"}\n' * 75)
        last_error = a.until("error")
        assert last_error["type"] == "error"

        # Allow the server to drain/close the offending connection.
        time.sleep(0.5)
        replacement = Peer()
        welcome = replacement.until("welcome")
        assert welcome["playerId"] == first["playerId"], welcome
        print("PASS: request flood does not lock out the room")
    finally:
        for peer in [a, b, c, replacement]:
            if peer is None:
                continue
            try:
                peer.close()
            except (OSError, TimeoutError):
                pass


if __name__ == "__main__":
    main()
