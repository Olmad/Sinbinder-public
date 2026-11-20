"""Source-linked contract for Damageable.TakeDamage post-modifier HP boundary.

This is NOT a Unity runtime test. It verifies the production code's ordering and
uses a small numeric counterexample table to document the intended contract.
"""
from pathlib import Path
import math
import unittest

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Assets/Scripts/Gameplay/Damageable.cs"


class DamagePostModifierContract(unittest.TestCase):
    def test_guard_after_all_modifiers_before_hp_mutation(self):
        code = SOURCE.read_text(encoding="utf-8")
        start = code.index("public void TakeDamage(")
        end = code.index("public void Wound(", start)
        body = code[start:end]
        hp = body.index("_hp -= damage;")
        guard = body.rfind("if (damage <= 0f || float.IsNaN(damage) || float.IsInfinity(damage)) return;", 0, hp)
        self.assertGreater(guard, body.index("damage = ApplyPosition("))
        self.assertGreater(guard, body.index("damage = Gift.Modify("))
        self.assertGreater(guard, body.index("damage = manualCombat.ReduceIncoming("))
        self.assertGreater(guard, body.index("damage = Mathf.Min("))
        self.assertLess(guard, hp)
        self.assertEqual(body.count("_hp -= damage;"), 1)

    def test_positive_and_negative_post_modifier_contract(self):
        hp = 30.0
        for damage, expected in [(5.0, 25.0), (0.5, 29.5),
                                 (0.0, 30.0), (-5.0, 30.0),
                                 (math.nan, 30.0), (math.inf, 30.0),
                                 (-math.inf, 30.0)]:
            with self.subTest(damage=damage):
                result = hp if damage <= 0 or not math.isfinite(damage) else hp - damage
                self.assertEqual(result, expected)


if __name__ == "__main__":
    unittest.main()
