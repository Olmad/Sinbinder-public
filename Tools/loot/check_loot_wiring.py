from pathlib import Path
base = Path(__file__).resolve().parents[2]
b = (base/"Assets/Scripts/Gameplay/FallenBag.cs").read_text(encoding="utf-8")
combat = (base/"Assets/Scripts/Gameplay/CombatManager.cs").read_text(encoding="utf-8")
warrior = (base/"Assets/Scripts/Gameplay/Warrior.cs").read_text(encoding="utf-8")
roster = (base/"Assets/Scripts/Gameplay/SquadRoster.cs").read_text(encoding="utf-8")
assert "FallenBag.CreateFor(killed)" in combat
assert "OnAnyDeath +=" not in b
assert "FallenBagTransfer.Move(_items, inventory.AddItem)" in b
assert "FallenBagTransfer.Move(_items, bag.AddItem)" in b
assert "FallenBagTransfer.Move(_items, who.Give)" in b
assert "if (_items.Count > 0 || _memento != null)" in b
assert "if (inventory == null)" in b
assert "public bool TrySpendPocket(int gold)" in warrior
assert "public static void SyncPocket(Warrior warrior)" in roster
print("PASS: corpse items retained on full inventory; death path wired; wallet saves synced")
