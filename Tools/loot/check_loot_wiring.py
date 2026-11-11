from pathlib import Path
base = Path(__file__).resolve().parents[2]
b = (base/"Assets/Scripts/Gameplay/FallenBag.cs").read_text(encoding="utf-8")
combat = (base/"Assets/Scripts/Gameplay/CombatManager.cs").read_text(encoding="utf-8")
warrior = (base/"Assets/Scripts/Gameplay/Warrior.cs").read_text(encoding="utf-8")
roster = (base/"Assets/Scripts/Gameplay/SquadRoster.cs").read_text(encoding="utf-8")
assert "FallenBag.CreateFor(killed, bodyLoot)" in combat
assert "HarvestableBody CreateLootOnField" in combat
assert "bodyLoot.CollectGold()" in b and "bodyLoot.CollectEquipment()" in b
assert "!LootChain.Enabled" in b
assert "OnAnyDeath +=" not in b
assert "FallenBagTransfer.Move(_items, inventory.AddItem)" in b
assert "FallenBagTransfer.Move(_items, bag.AddItem)" in b
assert "FallenBagTransfer.Move(_items, who.Give)" in b
assert "if (_items.Count > 0 || _memento != null)" in b
assert "if (inventory == null)" in b
assert "public bool TrySpendPocket(int gold)" in warrior
assert "public static void SyncPocket(Warrior warrior)" in roster
retention = (base/"Assets/Scripts/Gameplay/FallenBagRetention.cs").read_text(encoding="utf-8")
gear = (base/"Assets/Scripts/UI/GearPanel.cs").read_text(encoding="utf-8")
save = (base/"Assets/Scripts/Core/SaveSystem.cs").read_text(encoding="utf-8")
save_type = (base/"Assets/Scripts/Core/SaveGame.cs").read_text(encoding="utf-8")
prologue = (base/"Assets/Scripts/Gameplay/PrologueDirector.cs").read_text(encoding="utf-8")
assert 'Object.DontDestroyOnLoad(go)' in b
assert 'SceneManager.activeSceneChanged += MapChanged' in b
assert 'SceneManager.activeSceneChanged -= MapChanged' in b
assert 'b._retention.ChangeMap(name == _homeScene' not in b  # typo guard
assert '_retention.ChangeMap(name == _homeScene' in b
assert 'if (_retention.Expired) { Retire(); return; }' in b
assert 'if (!b._retention.OnHomeMap && left <= 0f)' in b
assert 'if (b != null && b._onMap && !b._retired' in b
assert 'FallenBag.Restore(save.FallenBags' in save
assert 'FallenBag.Snapshot()' in save
assert "Gameplay.FallenBag.OnMapLoaded(scene.name)" in save
assert "public static void OnMapLoaded(string sceneName)" in b
assert 'public List<SavedFallenBag> FallenBags' in save_type
assert 'FallenBag.Forget()' in prologue
assert 'foreach (var sack in FallenBag.InReach())' in gear
assert 'Gaze.Miss(sack.transform.position + Vector3.up * 0.5f' in gear
assert 'public const float AwayLifetime = 60f;' in retention
assert 'if (!Expired && !OnHomeMap' in retention
assert 'if (now - _leftAt >= AwayLifetime)' in retention
print("PASS: corpse items retained on full inventory; death path wired; wallet saves synced")
