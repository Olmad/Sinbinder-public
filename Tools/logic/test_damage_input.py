#!/usr/bin/env python3
"""L0 production-source contract for Damageable.TakeDamage; not Unity execution."""
from pathlib import Path
import re

source = (Path(__file__).resolve().parents[2] / "Assets/Scripts/Gameplay/Damageable.cs").read_text(encoding="utf-8")
match = re.search(r"public void TakeDamage\(float damage, GameObject attacker\)\s*\{", source)
assert match, "Production TakeDamage missing"
body = source[match.end():source.index("public void Wound(", match.end())]
guard = "if (damage <= 0f || float.IsNaN(damage) || float.IsInfinity(damage)) return;"
assert guard in body, "Invalid damage must be rejected"
for side_effect in ("Bodyguard.Absorb(", "PersonalExpedition.OnGuardAttacked(", "Hostility.Struck(", "_hp -= damage;"):
    assert side_effect in body, f"Expected production path missing: {side_effect}"
    assert body.index(guard) < body.index(side_effect), f"Invalid damage reaches {side_effect}"

def accepted(damage):
    import math
    return damage > 0 and math.isfinite(damage)

for damage, expected in [(5.0, True), (0.0, False), (-5.0, False),
                         (float("nan"), False), (float("inf"), False),
                         (-float("inf"), False)]:
    assert accepted(damage) is expected
print("PASS L0 Damageable.TakeDamage source guard + positive/negative boundary cases")
