#!/usr/bin/env python3
"""L0 production-source Wound contract, not a Unity runtime test."""
from pathlib import Path
import re
import math

source = (Path(__file__).resolve().parents[2] / "Assets/Scripts/Gameplay/Damageable.cs").read_text(encoding="utf-8")
match = re.search(r"public void Wound\(float remaining\)\s*\{", source)
assert match, "Wound method missing"
body = source[match.end():source.index("public void Heal(", match.end())]
guard = "if (float.IsNaN(remaining) || float.IsInfinity(remaining)) return;"
assert guard in body, "Missing nonfinite fraction guard"
assert body.index(guard) < body.index("_hp = Mathf.Clamp("), "Guard must precede HP mutation"
assert "if (IsDead) return;" in body, "Dead units must not be wounded"

def wound(hp, maximum, fraction):
    if not math.isfinite(fraction) or hp <= 0:
        return hp
    return max(1.0, min(maximum, maximum * fraction))

for fraction, expected in [(0.5, 15.0), (0.0, 1.0), (-1.0, 1.0),
                           (2.0, 30.0), (float("nan"), 20.0),
                           (float("inf"), 20.0), (-float("inf"), 20.0)]:
    assert wound(20.0, 30.0, fraction) == expected, (fraction, expected)
assert wound(0.0, 30.0, 0.5) == 0.0
print("PASS L0 Wound source contract and positive/negative boundaries")
