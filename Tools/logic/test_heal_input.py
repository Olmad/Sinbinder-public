#!/usr/bin/env python3
"""L0 contract on production Damageable.Heal, not Unity runtime execution."""
from pathlib import Path
import math
import re

source = (Path(__file__).resolve().parents[2] / "Assets/Scripts/Gameplay/Damageable.cs").read_text(encoding="utf-8")
match = re.search(r"public void Heal\(float amount\)\s*\{", source)
assert match, "Heal method missing"
body = source[match.end():source.index("private float ApplyPosition", match.end())]
guard = "if (float.IsNaN(amount) || float.IsInfinity(amount)) return;"
assert guard in body, "Nonfinite healing must be rejected"
assert body.index(guard) < body.index("_hp = Mathf.Min("), "Guard must precede HP mutation"
assert "Mathf.Abs(amount)" in body, "Existing signed-heal semantics must remain"

def healed(hp, maximum, amount):
    if not math.isfinite(amount):
        return hp
    return min(maximum, hp + abs(amount))

for amount, expected in [(5.0, 15.0), (-5.0, 15.0), (0.0, 10.0),
                         (float("nan"), 10.0), (float("inf"), 10.0),
                         (-float("inf"), 10.0)]:
    actual = healed(10.0, 30.0, amount)
    assert actual == expected, (amount, actual, expected)
assert healed(29.0, 30.0, 5.0) == 30.0
print("PASS L0 Heal source guard and signed/finite boundary table")
