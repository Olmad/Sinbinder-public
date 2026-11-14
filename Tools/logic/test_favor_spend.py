#!/usr/bin/env python3
"""L0 source-contract regression for Favor.Spend. Does not execute Unity C#."""
from pathlib import Path
import re

source = (Path(__file__).resolve().parents[2] / "Assets/Scripts/Gameplay/Favor.cs").read_text(encoding="utf-8")
match = re.search(r"public static bool Spend\(string lord, int coins\)\s*\{(?P<body>.*?)\n\s*\}", source, re.S)
assert match, "Production Spend method missing"
body = match.group("body")
guard = "if (string.IsNullOrEmpty(lord) || coins <= 0 || PurseOf(lord) < coins) return false;"
mutation = "Purses[lord] = PurseOf(lord) - coins;"
assert guard in body, "Nonpositive amount must be rejected"
assert mutation in body and body.index(guard) < body.index(mutation), "Guard must precede mutation"
assert "return true;" in body[body.index(mutation):], "Success only after debit"

# Truth table for the exact production guard. This is an L0 contract model,
# not an executed C# unit test.
def modeled_spend(lord, purse, coins):
    if not lord or coins <= 0 or purse < coins:
        return False, purse
    return True, purse - coins

for lord, purse, coins, expected in [
    ("player", 20, 5, (True, 15)),
    ("player", 20, -5, (False, 20)),
    ("player", 20, 0, (False, 20)),
    ("player", 20, 21, (False, 20)),
    ("", 20, 5, (False, 20)),
]:
    assert modeled_spend(lord, purse, coins) == expected
print("PASS L0 Favor.Spend source guard + positive/negative contract cases")
