#!/usr/bin/env python3
"""License-free Unity project preflight, NOT compilation/import/Play Mode."""
import json
import re
import sys
from pathlib import Path, PurePosixPath

EXPECTED_UNITY = "6000.3.22f1"
REQUIRED_SCENES = (
    "Assets/Scenes/Prologue_Camp.unity",
    "Assets/Scenes/Prologue_Dream.unity",
    "Assets/Scenes/Tug/Field.unity",
    "Assets/Scenes/Tug/Multiplayer_Test.unity",
)
TEST_ASMDEF = "Assets/Tests/Editor/Sinbinder.CloudSmokeTests.asmdef"
TEST_SOURCE = "Assets/Tests/Editor/CloudProjectSmokeTests.cs"
GUID = re.compile(r"(?m)^\s*guid:\s*([0-9a-fA-F]{32})\s*$")
BUILD_ENTRY = re.compile(
    r"(?m)^[ \t]*-[ \t]+enabled:[ \t]*([01])[ \t]*\r?\n"
    r"[ \t]+path:[ \t]*(Assets/[^\r\n]+?\.unity)[ \t]*\r?\n"
    r"[ \t]+guid:[ \t]*([0-9a-fA-F]{32})[ \t]*(?:\r?\n|$)"
)

class PreflightError(Exception):
    """Static input contract failed."""

def require(condition, description):
    if not condition:
        raise PreflightError(description)

def read(root, relative):
    path = root / relative
    require(path.is_file(), f"Missing project file: {relative}")
    return path.read_text(encoding="utf-8-sig")

def check(root):
    """Verify scene/meta/asmdef wiring; returns number of build scene entries."""
    version = read(root, "ProjectSettings/ProjectVersion.txt")
    require(re.search(rf"(?m)^m_EditorVersion: {re.escape(EXPECTED_UNITY)}\s*$", version) is not None,
            f"Unity Editor version must be {EXPECTED_UNITY}")

    try:
        manifest = json.loads(read(root, "Packages/manifest.json"))
    except json.JSONDecodeError as exc:
        raise PreflightError(f"Invalid Packages/manifest.json: {exc}") from exc
    require("com.unity.test-framework" in manifest.get("dependencies", {}),
            "Unity Test Framework missing from manifest.json")

    build = read(root, "ProjectSettings/EditorBuildSettings.asset")
    entries = BUILD_ENTRY.findall(build)
    declared = re.findall(r"(?m)^[ \t]*-[ \t]+enabled:", build)
    require(len(entries) == len(declared),
            f"Malformed EditorBuildSettings scene entry: parsed {len(entries)} of {len(declared)}")
    require(entries, "No parsed scene entries in EditorBuildSettings.asset")
    seen_paths, seen_guids, enabled_paths = set(), set(), set()
    for enabled, rel, guid in entries:
        pure = PurePosixPath(rel)
        require(rel.startswith("Assets/Scenes/") and ".." not in pure.parts and pure.suffix == ".unity",
                f"Unexpected or unsafe scene path: {rel}")
        require(rel not in seen_paths, f"Duplicate build scene path: {rel}")
        seen_paths.add(rel)
        require(guid.lower() not in seen_guids, f"Duplicate build scene GUID: {guid}")
        seen_guids.add(guid.lower())
        scene = read(root, rel)
        require(scene.lstrip("\ufeff").startswith("%YAML 1.1"), f"Not a Unity YAML scene: {rel}")
        meta = read(root, rel + ".meta")
        actual = GUID.search(meta)
        require(actual is not None, f"Scene .meta has no valid GUID: {rel}")
        require(actual.group(1).lower() == guid.lower(),
                f"Build Settings GUID disagrees with scene .meta: {rel}")
        if enabled == "1":
            enabled_paths.add(rel)
    for required in REQUIRED_SCENES:
        require(required in enabled_paths, f"Required scene not enabled: {required}")
    require(entries[0][0] == "1" and entries[0][1] == "Assets/Scenes/Prologue_Camp.unity",
            "Solo demo start scene must remain Prologue_Camp")

    for rel in ("Assets/Tests.meta", "Assets/Tests/Editor.meta",
                TEST_ASMDEF + ".meta", TEST_SOURCE + ".meta"):
        meta = read(root, rel)
        require(GUID.search(meta) is not None, f"Missing Unity GUID in: {rel}")
    try:
        assembly = json.loads(read(root, TEST_ASMDEF))
    except json.JSONDecodeError as exc:
        raise PreflightError(f"Invalid Unity test .asmdef JSON: {exc}") from exc
    require(assembly.get("includePlatforms") == ["Editor"], "Cloud smoke tests must be Editor-only")
    require("TestAssemblies" in assembly.get("optionalUnityReferences", []),
            "Unity test .asmdef must reference TestAssemblies")
    require("UNITY_INCLUDE_TESTS" in assembly.get("defineConstraints", []),
            "Cloud smoke assembly must use UNITY_INCLUDE_TESTS")
    source = read(root, TEST_SOURCE)
    require("[Test]" in source and "NUnit.Framework" in source,
            "No NUnit Test attribute or import in the cloud smoke test file")
    return len(entries)

def main():
    root = Path(sys.argv[1] if len(sys.argv) > 1 else ".").resolve()
    try:
        scene_count = check(root)
    except (PreflightError, UnicodeError) as exc:
        print(f"FAIL [STATIC/L0]: {exc}", file=sys.stderr)
        return 1
    print(f"PASS [STATIC/L0]: {scene_count} build scenes and cloud Unity smoke assets validated")
    print("NOT TESTED: Unity Editor import, C# compilation, PlayMode, screenshots, Windows builds, human UX")
    return 0

if __name__ == "__main__":
    sys.exit(main())
