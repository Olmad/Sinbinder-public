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
assert "m_Fog: 0" in scene, "Authoring scene must not start with gameplay fog"
assert "m_AmbientSkyColor: {r: 0.6, g: 0.66, b: 0.74, a: 1}" in scene, "Authoring scene is too dark"
assert "m_RenderPostProcessing: 0" in scene, "Gameplay post processing must not darken the editor"
assert "m_GameObject: {fileID: 1309223881}\\n  m_Enabled: 0" .replace("\\n", "\n") in scene, "Horror Volume still active"
assert "m_GameObject: {fileID: 423919928}\\n  m_Enabled: 0" .replace("\\n", "\n") in scene, "NavMesh baker must not run while authoring"
assert "MissingModelMarker(at, prop);" in editor and "_missingPreviewModels.Add" in editor
assert "GUIUtility.systemCopyBuffer" in editor and "MapData.Folder" in editor

assert 'public string Prop = "Pine";' in data and 'public string MaterialId = "";' in data
assert "public List<Region> Regions = new();" in data and "public List<Trigger> Triggers = new();" in data
assert "MapTerrain.ApplyGround(ground, map, ref _terrainMesh, ref _originalGroundMesh);" in loader
assert "MapTerrain.HeightAt(map," in loader, "Placed gameplay objects not attached to real terrain height"
assert "MapTerrain.ApplyGround(ground, _map, ref _terrainMesh, ref _originalGroundMesh);" in editor
assert "private void Sculpt(Vector3 at)" in editor and "_map.TerrainStrokes.Add" in editor
assert "public List<TerrainStroke> TerrainStrokes = new();" in data
assert "collider.sharedMesh = next != null ? next : original;" in data
assert "map.EnsureLists();" in loader
assert "MapAssets.ApplyObjectMaterial(model, t.MaterialId);" in loader
assert 'MapAssets.UserPrefab(prop)' in loader
assert 'public List<TerrainPaint> TerrainPaints = new();' in data
assert 'mesh.SetTriangles(groups[g].ToArray(), g);' in data
assert 'MapAssets.TerrainMaterial(ids[i])' in data
assert 'private void PaintTerrain(Vector3 at)' in editor
assert 'private void ScatterVegetation(Vector3 at)' in editor
importer = src("Assets/Editor/MapAssetImporter.cs")
assert 'PrefabUtility.SaveAsPrefabAsset(obj, target);' in importer
assert 'AssetDatabase.CreateAsset(material, target);' in importer
assert 'Path.GetExtension(file).ToLowerInvariant()' in importer
assert 'if (!CheckNewAsset(target)) return;' in importer
for mat in ["Ground048", "Ground077", "Gravel043", "Rock022"]:
    assert (root / ("Assets/Resources/MapTerrainMaterials/" + mat + ".mat")).is_file(), mat and "MapLogicRunner logic = null;" in loader
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
assert "private void Select(Vector3 at, bool additive = false)" in editor
assert "private void SelectAllThings()" in editor
assert "private void SelectRectangle(Vector3 first, Vector3 last, bool additive = false)" in editor
assert "GetMouseButtonUp(0)" in editor
assert "private void DuplicateSelectedThings()" in editor
assert "private void DeleteSelectedThings()" in editor
assert "private void RotateSelectedThings(float degrees)" in editor
assert "private void ArrangeSelectedThings(MapObjectArrange.Axis axis, MapObjectArrange.Operation operation)" in editor
assert "MapObjectArrange.TryPlan(selected, _map.Size, axis, operation, out var planned)" in editor
assert "private void DrawGroupInspector()" in editor
assert "Равные интервалы по X" in editor and "Равные интервалы по Z" in editor
layout=src("Assets/Scripts/Gameplay/MapObjectArrange.cs")
assert "internal static bool TryPlan(" in layout and "sorted.Sort" in layout
assert "guid: b4b295570ea247138f4a5ea2896c29f3" in src("Assets/Scripts/Gameplay/MapObjectArrange.cs.meta")
assert "private void MoveSelectedThings(float dx, float dz)" in editor
assert "rule.TargetId = copy.Id" in editor or "TargetId = copy.Id" in editor
assert "private List<string> Validate()" in editor
assert "if (errors.Count > 0)" in editor
assert "private void DrawTrigger(MapData.Trigger t)" in editor
assert "UnityEditor" not in runner and "System.Reflection" not in runner
print("PASS: scene GUID intact; imported material/prefab authoring paths; terrain paint/vegetation/mesh; F-ECA integration; atomic save")
