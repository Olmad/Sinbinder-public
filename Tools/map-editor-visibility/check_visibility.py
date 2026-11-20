#!/usr/bin/env python3
"""Static source/scene visibility contract, NOT a Unity screenshot or PlayMode run."""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]
read = lambda p: (root / p).read_text(encoding="utf-8")
scene = read("Assets/Scenes/Tug/MapEditor.unity")
editor = read("Assets/Scripts/Gameplay/MapEditor.cs")
loader = read("Assets/Scripts/Gameplay/MapLoader.cs")
fog_source = read("Assets/Scripts/Gameplay/FogOfWar.cs")
meta = read("Assets/Scripts/Gameplay/MapEditor.cs.meta")
pine = read("Assets/Resources/MapProps/Pine.prefab")
pine_meta = read("Assets/Resources/Props/Pine.fbx.meta")

def require(check, description):
    if not check:
        raise AssertionError(description)

def enabled_component(game_object_id, state):
    return (f"m_GameObject: {{fileID: {game_object_id}}}\n"
            f"  m_Enabled: {state}\n") in scene

# Rendering fog is separate from RenderSettings.fog: gameplay FogOfWar installs
# itself whenever GroundNavMesh exists, so the map editor needs an early exit.
install_start = fog_source.index("private static void Install()")
install_end = fog_source.index("private void Build(", install_start)
install = fog_source[install_start:install_end]
editor_guard = "if (Object.FindFirstObjectByType<MapEditor>() != null) return;"
ground_lookup = "var ground = Object.FindFirstObjectByType<GroundNavMesh>();"
require(editor_guard in install, "runtime FogOfWar would black out WorldForge viewport")
require(ground_lookup in install and install.index(editor_guard) < install.index(ground_lookup),
        "FogOfWar's editor exclusion must run before gameplay ground discovery")
require('go.AddComponent<FogOfWar>().Build(renderer.bounds);' in install,
        "gameplay fog must continue installing outside the editor")

require('m_Fog: 0' in scene, "editor scene fog is still active")
require('m_AmbientSkyColor: {r: 0.6, g: 0.66, b: 0.74, a: 1}' in scene,
        "authoring ambient light must be bright")
require('m_Sun: {fileID: 819642460}' in scene,
        "editor directional sunlight must be assigned")
require(enabled_component(819642459, 1) or 'm_Intensity: 1.6' in scene,
        "editor key light missing")
require(enabled_component(1309223881, 0),
        "gameplay horror Volume is still enabled in map authoring")
require(enabled_component(423919928, 0),
        "editor should not bake gameplay NavMesh on startup")
require('m_RenderPostProcessing: 0' in scene,
        "gameplay postprocessing still darkens editor camera")
require('m_BackGroundColor: {r: 0.28, g: 0.38, b: 0.48, a: 1}' in scene,
        "camera still has dark gameplay background")
require('m_TagString: MainCamera' in scene and 'm_Name: Main Camera' in scene,
        "editor camera missing or incorrectly tagged")
require('guid: 545372576548c774b8f6dca21dc8760b' in meta,
        "original MapEditor MonoBehaviour asset GUID changed")
require('Assembly-CSharp::Sinbinder.Gameplay.MapEditor' in scene,
        "scene no longer contains the existing MapEditor script")
require('Assets/Resources/Props/Pine.fbx' and (root / "Assets/Resources/Props/Pine.fbx").is_file(),
        "Pine FBX source is not present")
guid = re.search(r"^guid: ([0-9a-f]{32})$", pine_meta, re.MULTILINE)
require(guid is not None and guid.group(1) in pine,
        "Pine prefab has a broken source model GUID")
require('Resources.Load<GameObject>("MapProps/" + prop)' in loader,
        "editor loader no longer queries bundled prefabs")
require('MissingModelMarker(at, prop);' in editor,
        "missing models would still be silently invisible")
require('private void CenterCamera()' in editor,
        "lost editor camera has no recovery action")
require('new GameObject("Sinbinder - резервная камера редактора")' in editor,
        "editor lacks fallback camera")
require('Не загружено видов моделей' in editor,
        "editor has no visual missing-asset diagnostics")
require('GUIUtility.systemCopyBuffer' in editor and 'MapData.Folder' in editor,
        "editor must expose exact map save location for build-to-build investigations")
print("PASS: editor scene daylight, no gameplay FogOfWar, valid bundled Pine GUID, camera recovery and missing-model diagnostics (SOURCE ONLY)")
