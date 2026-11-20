"""E0 merge inventory contract tests. No real game branches are modified."""
import importlib.util
import subprocess
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "e0_merge_inventory.py"
spec = importlib.util.spec_from_file_location("e0_merge_inventory", SCRIPT)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


def run_git(root, *args):
    result = subprocess.run(["git", "-C", str(root), *args],
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if result.returncode:
        raise AssertionError(result.stderr.decode("utf-8", "replace"))
    return result.stdout.decode("utf-8", "replace").strip()


def write(root, name, value):
    path = root / name
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(value, encoding="utf-8")


class E0MergeInventoryTests(unittest.TestCase):
    def setUp(self):
        tmp = tempfile.TemporaryDirectory()
        self.addCleanup(tmp.cleanup)
        self.root = Path(tmp.name)
        run_git(self.root, "init", "-q")
        run_git(self.root, "config", "user.email", "e0-test@example.invalid")
        run_git(self.root, "config", "user.name", "E0 Test")
        write(self.root, "Assets/Scripts/Gameplay/DreamRoad.cs", "base\n")
        run_git(self.root, "add", ".")
        run_git(self.root, "commit", "-qm", "base")
        self.base = run_git(self.root, "rev-parse", "HEAD")

    def commit(self, name):
        run_git(self.root, "add", ".")
        run_git(self.root, "commit", "-qm", name)

    def make_author(self):
        run_git(self.root, "checkout", "-qb", "author", self.base)
        write(self.root, module.CANON[0], "# 68. Actual map canon\n")
        write(self.root, module.CANON[1], "fileFormatVersion: 2\nguid: " + "a" * 32 + "\n")
        write(self.root, "Assets/Scripts/Gameplay/DreamRoad.cs", "author\n")
        self.commit("author canon")
        return run_git(self.root, "rev-parse", "HEAD")

    def make_incoming(self, doc="# 68. Actual map canon\n",
                      meta="fileFormatVersion: 2\nguid: " + "a" * 32 + "\n",
                      omit_meta=False):
        run_git(self.root, "checkout", "-qb", "incoming", self.base)
        write(self.root, module.CANON[0], doc)
        if not omit_meta:
            write(self.root, module.CANON[1], meta)
        write(self.root, "Assets/Scripts/Gameplay/DreamRoad.cs", "bot\n")
        write(self.root, "Tools/тест с пробелом.py", "print('hi')\n")
        self.commit("incoming canon and bot code")
        return run_git(self.root, "rev-parse", "HEAD")

    def test_identical_canon_and_protected_overlap_read_only(self):
        self.make_author()
        head = self.make_incoming()
        report = module.inventory(self.root, "author", "incoming")
        self.assertEqual(report["merge_base"], self.base)
        self.assertTrue(report["canonical_docs_identical"])
        self.assertEqual(report["overlap_count"], 3)
        self.assertIn("Assets/Scripts/Gameplay/DreamRoad.cs",
                      report["overlapping_paths"])
        self.assertIn("Tools/тест с пробелом.py", report["incoming_only"])
        self.assertTrue(report["protected_paths"][
            "Assets/Scripts/Gameplay/DreamRoad.cs"]["both_changed"])
        self.assertEqual(run_git(self.root, "rev-parse", "HEAD"), head)
        self.assertEqual(run_git(self.root, "status", "--porcelain"), "")

    def test_missing_canon_meta_blocks_without_merge(self):
        self.make_author()
        self.make_incoming(omit_meta=True)
        report = module.inventory(self.root, "author", "incoming")
        self.assertFalse(report["canonical_docs_identical"])
        self.assertEqual(report["canonical_docs"][module.CANON[1]]["status"],
                         "missing")
        self.assertEqual(module.main(["--repo", str(self.root),
                                      "--author-ref", "author",
                                      "--incoming-ref", "incoming"]), 2)

    def test_modified_canon_blob_is_not_accepted(self):
        self.make_author()
        self.make_incoming(doc="# 68. Rewritten by a bot\n")
        report = module.inventory(self.root, "author", "incoming")
        self.assertFalse(report["canonical_docs_identical"])
        self.assertEqual(report["canonical_docs"][module.CANON[0]]["status"],
                         "different")

    def test_missing_branch_fails_without_modification(self):
        self.make_author()
        head = run_git(self.root, "rev-parse", "HEAD")
        with self.assertRaises(RuntimeError):
            module.inventory(self.root, "author", "nonexistent-branch")
        self.assertEqual(run_git(self.root, "rev-parse", "HEAD"), head)

    def test_cli_report_contains_sha_and_does_not_change_git_tree(self):
        self.make_author()
        before = self.make_incoming()
        output = Path(tempfile.mkdtemp()) / "e0-report.json"
        self.addCleanup(lambda: output.unlink(missing_ok=True))
        self.addCleanup(lambda: output.parent.rmdir())
        result = module.main(["--repo", str(self.root), "--author-ref", "author",
                              "--incoming-ref", "incoming", "--json-out", str(output)])
        self.assertEqual(result, 0)
        self.assertTrue(output.is_file())
        import json
        self.assertTrue(json.loads(output.read_text(encoding="utf-8"))[
            "canonical_docs_identical"])
        self.assertEqual(run_git(self.root, "rev-parse", "HEAD"), before)


if __name__ == "__main__":
    unittest.main()
