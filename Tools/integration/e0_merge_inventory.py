#!/usr/bin/env python3
"""Read-only branch-overlap inventory for Sinbinder plan 69, phase E0.

Run in a local clone with fetched refs. This tool NEVER merges, checks out,
resets, deletes or rewrites any Git data. Overlap is NOT a merge conflict:
it only identifies paths that require inspection by the local integrator.

Example:
  python Tools/integration/e0_merge_inventory.py \
      --author-ref origin/claude/3d-horror-survival-game-ckrslc \
      --incoming-ref origin/feature/server-prototype \
      --json-out Logs/e0-branch-overlap.json
"""
from __future__ import annotations

import argparse
import json
import subprocess
import sys
from pathlib import Path

CANON = (
    "Assets/Scripts/docs/68-TUTORIAL-MAP.md",
    "Assets/Scripts/docs/68-TUTORIAL-MAP.md.meta",
)
PROTECTED = (
    *CANON,
    "Assets/Scenes/Prologue_Dream.unity",
    "Assets/Scripts/Gameplay/DreamRoad.cs",
    "Assets/Scripts/Gameplay/DreamRoadMap.cs",
    "Assets/Scripts/Gameplay/DreamTutorialDirector.cs",
    "Assets/Scripts/Editor/DreamWalkthrough.cs",
    "Assets/Scripts/Gameplay/SoulCourt.cs",
    "Assets/Scripts/Gameplay/SinbinderPlayer.cs",
)


def git(root: Path, *args: str, allow_failure: bool = False):
    result = subprocess.run(
        ["git", "-C", str(root), *args],
        stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=False,
    )
    if result.returncode and not allow_failure:
        raise RuntimeError(
            "git " + " ".join(args) + ": " +
            result.stderr.decode("utf-8", "replace").strip()
        )
    return result


def sha(root: Path, ref: str) -> str:
    return git(root, "rev-parse", "--verify", ref + "^{commit}").stdout.decode().strip()


def paths_since(root: Path, ancestor: str, ref: str) -> set[str]:
    # NUL records preserve spaces, Cyrillic and punctuation in file names.
    raw = git(root, "diff", "--name-only", "-z", ancestor, ref).stdout
    return {p.decode("utf-8", "surrogateescape")
            for p in raw.split(b"\0") if p}


def object_id(root: Path, ref: str, path: str) -> str | None:
    result = git(root, "rev-parse", "--verify", ref + ":" + path,
                 allow_failure=True)
    return result.stdout.decode("ascii").strip() if result.returncode == 0 else None


def inventory(root: Path, author_ref: str, incoming_ref: str) -> dict:
    author_sha = sha(root, author_ref)
    incoming_sha = sha(root, incoming_ref)
    ancestor = git(root, "merge-base", author_sha, incoming_sha).stdout.decode().strip()
    author = paths_since(root, ancestor, author_sha)
    incoming = paths_since(root, ancestor, incoming_sha)
    overlap = author & incoming

    canon = {}
    for path in CANON:
        a = object_id(root, author_sha, path)
        b = object_id(root, incoming_sha, path)
        canon[path] = {
            "author_blob": a, "incoming_blob": b,
            "status": "identical" if a is not None and a == b else
                      "missing" if a is None or b is None else "different",
        }

    return {
        "author_ref": author_ref, "author_sha": author_sha,
        "incoming_ref": incoming_ref, "incoming_sha": incoming_sha,
        "merge_base": ancestor,
        "author_changed_count": len(author),
        "incoming_changed_count": len(incoming),
        "overlap_count": len(overlap),
        "author_only": sorted(author - incoming),
        "incoming_only": sorted(incoming - author),
        "overlapping_paths": sorted(overlap),
        "protected_paths": {
            p: {
                "author_changed": p in author,
                "incoming_changed": p in incoming,
                "both_changed": p in overlap,
                "author_blob": object_id(root, author_sha, p),
                "incoming_blob": object_id(root, incoming_sha, p),
            } for p in PROTECTED
        },
        "canonical_docs": canon,
        "canonical_docs_identical": all(
            item["status"] == "identical" for item in canon.values()
        ),
    }


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--author-ref",
                        default="origin/claude/3d-horror-survival-game-ckrslc")
    parser.add_argument("--incoming-ref", default="origin/feature/server-prototype")
    parser.add_argument("--json-out", type=Path)
    args = parser.parse_args(argv)
    try:
        report = inventory(args.repo.resolve(), args.author_ref, args.incoming_ref)
    except RuntimeError as exc:
        print("ERROR:", exc, file=sys.stderr)
        return 1

    print("E0 author-only paths:", len(report["author_only"]))
    print("E0 incoming-only paths:", len(report["incoming_only"]))
    print("E0 overlapping changed paths:", report["overlap_count"])
    for path in PROTECTED:
        state = report["protected_paths"][path]
        if state["both_changed"] or state["author_changed"]:
            print("REVIEW:", path, "BOTH" if state["both_changed"] else "AUTHOR")
    for path, info in report["canonical_docs"].items():
        print("CANON:", info["status"], path)
    if args.json_out:
        args.json_out.parent.mkdir(parents=True, exist_ok=True)
        args.json_out.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n",
                                 encoding="utf-8")
        print("Report:", args.json_out)

    if not report["canonical_docs_identical"]:
        print("BLOCK: author map 68 and/or its Unity meta differ or are missing.",
              file=sys.stderr)
        return 2
    print("No merges performed. Overlaps require human/Unity verification.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
