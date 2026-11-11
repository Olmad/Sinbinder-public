"""Negative regression checks for Unity cloud static preflight."""
import json
import tempfile
import unittest
from pathlib import Path
from unity_preflight import EXPECTED_UNITY, REQUIRED_SCENES, PreflightError, check

class PreflightTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.guid = {p: f"{i:032x}" for i, p in enumerate(REQUIRED_SCENES, start=1)}
        self.write("ProjectSettings/ProjectVersion.txt", f"m_EditorVersion: {EXPECTED_UNITY}\n")
        self.write("Packages/manifest.json", json.dumps({"dependencies": {"com.unity.test-framework": "1.6.0"}}))
        for rel, marker in (
            ("Assets/Tests.meta", "a"), ("Assets/Tests/Editor.meta", "b"),
            ("Assets/Tests/Editor/Sinbinder.CloudSmokeTests.asmdef.meta", "c"),
            ("Assets/Tests/Editor/CloudProjectSmokeTests.cs.meta", "d")
        ):
            self.write(rel, "guid: " + marker * 32)
        self.asm = {"includePlatforms": ["Editor"], "optionalUnityReferences": ["TestAssemblies"],
                    "defineConstraints": ["UNITY_INCLUDE_TESTS"]}
        self.write_asm()
        self.write("Assets/Tests/Editor/CloudProjectSmokeTests.cs",
                   "using NUnit.Framework; class Smoke { [Test] void Check() {} }")
        for rel in REQUIRED_SCENES:
            self.write(rel, "%YAML 1.1\n--- !u!1 &1\nGameObject:\n")
            self.write(rel + ".meta", "guid: " + self.guid[rel])
        self.write_build(REQUIRED_SCENES)

    def write(self, path, content):
        target = self.root / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(content, encoding="utf-8")

    def write_asm(self):
        self.write("Assets/Tests/Editor/Sinbinder.CloudSmokeTests.asmdef", json.dumps(self.asm))

    def write_build(self, paths):
        entries = "".join(f"  - enabled: 1\n    path: {p}\n    guid: {self.guid[p]}\n" for p in paths)
        self.write("ProjectSettings/EditorBuildSettings.asset", "m_Scenes:\n" + entries)

    def assert_rejected(self, message):
        with self.assertRaisesRegex(PreflightError, message):
            check(self.root)

    def test_valid_minimal_project_passes(self):
        self.assertEqual(check(self.root), 4)

    def test_mismatched_meta_guid_fails(self):
        self.write(REQUIRED_SCENES[2] + ".meta", "guid: " + "e" * 32)
        self.assert_rejected("disagrees")

    def test_duplicate_path_fails(self):
        self.write_build(list(REQUIRED_SCENES) + [REQUIRED_SCENES[2]])
        self.assert_rejected("Duplicate build scene path")

    def test_missing_scene_fails(self):
        (self.root / REQUIRED_SCENES[2]).unlink()
        self.assert_rejected("Missing project file")

    def test_duplicate_guids_fail(self):
        second = REQUIRED_SCENES[1]
        self.guid[second] = self.guid[REQUIRED_SCENES[0]]
        self.write(second + ".meta", "guid: " + self.guid[second])
        self.write_build(REQUIRED_SCENES)
        self.assert_rejected("Duplicate build scene GUID")

    def test_wrong_unity_version_fails(self):
        self.write("ProjectSettings/ProjectVersion.txt", "m_EditorVersion: 2022.3.0f1\n")
        self.assert_rejected("Unity Editor version")

    def test_invalid_asmdef_fails(self):
        self.asm["includePlatforms"] = ["Standalone"]
        self.write_asm()
        self.assert_rejected("Editor-only")

    def test_asmdef_no_test_reference_fails(self):
        self.asm["optionalUnityReferences"] = []
        self.write_asm()
        self.assert_rejected("TestAssemblies")

    def test_missing_nunit_tests_fails(self):
        self.write("Assets/Tests/Editor/CloudProjectSmokeTests.cs", "// nothing to test")
        self.assert_rejected("NUnit Test")

    def test_prologue_camp_must_start(self):
        paths = list(REQUIRED_SCENES)
        paths[0], paths[1] = paths[1], paths[0]
        self.write_build(paths)
        self.assert_rejected("Solo demo start")

    def test_cloud_workflow_covers_gameplay_and_import_dependencies(self):
        # A green cloud gate on a different SHA is not a valid gate for code
        # that did not trigger this workflow at all.
        workflow = (Path(__file__).resolve().parents[2]
                    / ".github/workflows/unity-cloud-smoke.yml").read_text(encoding="utf-8")
        push_section = workflow.split("  push:", 1)[1].split("  workflow_dispatch:", 1)[0]
        for changed_path in ("Assets/**", "Packages/**", "ProjectSettings/**"):
            self.assertIn(f'      - "{changed_path}"', push_section,
                          f"Unity import gate would not run for {changed_path}")

if __name__ == "__main__":
    unittest.main()
