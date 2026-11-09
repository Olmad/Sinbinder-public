#!/usr/bin/env python3
"""Static integrity checks for multiplayer QA content and the original demo.

No Unity installation is needed. This checks assets as shipped and catches
an accidentally missing/misregistered scene that C# compilation cannot see.
"""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
scenes = ROOT / "Assets/Scenes/Tug"
original = (scenes / "Field.unity").read_bytes()
replica = (scenes / "Multiplayer_Test.unity").read_bytes()

assert original.startswith(b"%YAML 1.1"), "Original Field scene not Unity YAML"
assert replica == original, (
    "Mechanics test scene drifted from Field: review references explicitly"
)
assert b"MapLoader" in replica or b"m_EditorClassIdentifier" in replica, (
    "Expected original scene data missing"
)

meta = (scenes / "Multiplayer_Test.unity.meta").read_text("utf-8")
assert "guid: 82df2b7319bc4ab393db801f19f3b580" in meta
assert "168dd53c90aa7de4aa9eeb0feee559f2" not in meta

build = (ROOT / "ProjectSettings/EditorBuildSettings.asset").read_text("utf-8")
assert "path: Assets/Scenes/Prologue_Camp.unity" in build
assert "path: Assets/Scenes/Tug/Multiplayer_Test.unity" in build
assert "guid: 82df2b7319bc4ab393db801f19f3b580" in build
enabled = re.findall(r"-\s+enabled:\s+([01])\s+path:\s+(\S+)", build)
assert enabled[0] == ("1", "Assets/Scenes/Prologue_Camp.unity"), (
    "Start scene changed: protect the existing single-player demo"
)
assert ("1", "Assets/Scenes/Tug/Multiplayer_Test.unity") in enabled

loader = (ROOT / "Assets/Scripts/Gameplay/MapLoader.cs").read_text("utf-8")
assert '== "Multiplayer_Test"' in loader
assert "MultiplayerTestMap.Choose()" in loader
entry = (ROOT / "Assets/Scripts/Multiplayer/SinbinderLanBootstrap.cs").read_text("utf-8")
assert "SceneManager.LoadScene(\"Multiplayer_Test\")" in entry
assert '"-mp-test-map"' in entry

altar = (ROOT / "Assets/Scripts/Gameplay/AltarVisualEnhancer.cs").read_text("utf-8")
assert "Object.Destroy(collision)" in altar
assert "collision.enabled = false" in altar
assert "BindingDevice" in altar
assert "Altar" in altar
assert "UnityEditor" not in altar

metas = {
    "Assets/Scripts/Core/MultiplayerTestMap.cs.meta": "04b1ad50829e46bda6e0e8adac13f623",
    "Assets/Scripts/Gameplay/AltarVisualEnhancer.cs.meta": "12b92499cd714343b7a9acf0254d4e31",
    "Assets/Scenes/Tug/Multiplayer_Test.unity.meta": "82df2b7319bc4ab393db801f19f3b580",
}
all_seen = set()
for name, expected in metas.items():
    actual = re.search(r"guid:\s*([0-9a-f]{32})", (ROOT / name).read_text("utf-8"))
    assert actual and actual.group(1) == expected, f"{name} GUID mismatch"
    assert actual.group(1) not in all_seen, f"{name} GUID duplicated"
    all_seen.add(actual.group(1))

print("PASS: standalone multiplayer test scene, launch, original demo and altar safety")
