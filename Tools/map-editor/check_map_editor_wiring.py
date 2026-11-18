#!/usr/bin/env python3
"""Fail-closed source wiring checks: does not represent Unity PlayMode."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
def src(path): return (root/path).read_text(encoding="utf-8")
data=src("Assets/Scripts/Core/MapData.cs")
editor=src("Assets/Scripts/Gameplay/MapEditor.cs")
runner=src("Assets/Scripts/Gameplay/MapLogicRunner.cs")
loader=src("Assets/Scripts/Gameplay/MapLoader.cs")
gear=src("Assets/Scripts/UI/GearPanel.cs")
scene=(root/"Assets/Scenes/Tug/MapEditor.unity").read_text(encoding="utf-8")
metadata=src("Assets/Scripts/Gameplay/MapEditor.cs.meta")
logicmeta=src("Assets/Scripts/Gameplay/MapLogicRunner.cs.meta")
assert "guid: 545372576548c774b8f6dca21dc8760b" in metadata, "Existing MapEditor Unity reference changed"
assert "guid: 5da4d5bc71fa4e9b92a503154d59b1c7" in logicmeta
assert "Sinbinder.Gameplay.MapEditor" in scene, "Scene no longer references the existing map editor"
assert 'public string Id = ""; public string Prop = "Pine";' in data
assert "public List<Region> Regions = new();" in data and "public List<Trigger> Triggers = new();" in data
assert "map.EnsureLists();" in loader and "MapLogicRunner logic = null;" in loader
assert "logic.Configure(map);" in loader
assert "logic.RegisterObject(t.Id, model.transform);" in loader
assert "MapLogicRunner.OfferInteractions(Offer);" in gear, "Object events not reachable through actual F stack"
assert "public static void OfferInteractions(Action<float, string, Action> offer)" in runner
assert '"OnInteractObject"' in runner and '"OnEnterRegion"' in runner and '"OnStart"' in runner
assert 'case "ShowMessage":' in runner and 'case "SetFlag":' in runner
assert "default: return false;" in runner, "Missing fail-closed rule dispatch"
assert "File.Replace(temp, path, path + \".bak\");" in data
assert "File.Move(temp, path);" in data
assert "private void Undo()" in editor and "private void Redo()" in editor
assert "private void Select(Vector3 at)" in editor
assert "private List<string> Validate()" in editor
assert "if (errors.Count > 0)" in editor
assert "private void DrawTrigger(MapData.Trigger t)" in editor
assert "UnityEditor" not in runner and "System.Reflection" not in runner
print("PASS: scene GUID intact; data and loader compatible; map ECA wired into existing F; atomic saving and validation present")
