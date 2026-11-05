"""Fast geometry and output-routing checks. Does not import Blender or export assets."""
import argparse
import ast
from collections import Counter
from contextlib import redirect_stdout
import io
import math
from pathlib import Path
import runpy
from types import SimpleNamespace


def geometry(shapes):
    cases = [
        ("wide_hat", shapes["hat_brim"](.977, .108, .057, thickness=.005, upturn=.002)),
        ("straw_hat", shapes["hat_brim"](.971, .116, .058, thickness=.007, upturn=.005, wave=.002)),
        ("inquisitor_cap", shapes["hat_brim"](.965, .077, .054, thickness=.005, upturn=.0015, segments=28)),
        ("tabard", shapes["cloth_tabard"](.785, .555)),
    ]
    for name, (vertices, faces) in cases:
        assert shapes["assert_mesh"](vertices, faces), name
        directed = Counter((a, b) for f in faces for a, b in zip(f, f[1:] + f[:1]))
        assert all(count == 1 and directed[b, a] == 1 for (a, b), count in directed.items()), name
        volume = 0.0
        for f in faces:
            for i in range(1, len(f)-1):
                a, b, c = (vertices[f[j]] for j in (0, i, i+1))
                volume += (a[0]*(b[1]*c[2]-b[2]*c[1]) + a[1]*(b[2]*c[0]-b[0]*c[2])
                           + a[2]*(b[0]*c[1]-b[1]*c[0])) / 6.0
        assert volume > 0, (name, volume)
        print(name, len(vertices), "vertices; closed, consistently oriented, positive volume")
    vertices, faces = cases[-1][1]
    a, b, c = (vertices[i] for i in faces[0][:3])
    normal_y = (b[2]-a[2])*(c[0]-a[0]) - (b[0]-a[0])*(c[2]-a[2])
    assert normal_y < 0, "Tabard front must face -Y"
    path, (lower, upper) = shapes["bow_path"](.800)
    assert len(path) == 17 and lower == path[0] and upper == path[-1]
    assert all(a[2] < b[2] for a, b in zip(path, path[1:]))
    assert math.isclose(upper[2]-lower[2], .390)
    assert path[8][0] > lower[0] and math.isclose(lower[0], upper[0])
    for call in (lambda: shapes["hat_brim"](1, .05, .1),
                 lambda: shapes["cloth_tabard"](.5, .8),
                 lambda: shapes["bow_path"](.8, segments=2)):
        try:
            call()
        except ValueError:
            continue
        raise AssertionError("Invalid dimensions accepted")
    print("bow and invalid-dimension checks: OK")


def routing(folder, source):
    # Execute the actual CLI function with export/build replaced by recorders.
    # Geometry functions are tested separately above; this tests destination selection.
    tree = ast.parse((folder / source).read_text(encoding="utf-8-sig"))
    main = next(n for n in tree.body if isinstance(n, ast.FunctionDef) and n.name == "main")
    code = compile(ast.Module(body=[main], type_ignores=[]), source, "exec")
    wear = source == "wear.py"
    key, name, resource = ("--item", "WideHat", "Wear") if wear else ("--prop", "Chest", "Props")
    item = SimpleNamespace(name=name, bone="Head")
    obj = SimpleNamespace(data=SimpleNamespace(polygons=[], vertices=[SimpleNamespace(co=SimpleNamespace(z=0.0))]))
    for custom in (False, True):
        outputs = []
        args = [source, "--", key, name]
        if custom:
            args += ["--out", "preview-out"]
        env = {"Path": Path, "sys": SimpleNamespace(argv=args),
               "bodies": SimpleNamespace(unity_root=lambda: Path("project-root")),
               "ITEMS": [item], "PROPS": [item], "SHELL_ITEMS": {"Living": ({}, [(name, None, None)])},
               "build": lambda *a: obj, "build_for_shell": lambda *a: obj,
               "export": outputs.append}
        exec(code, env)
        with redirect_stdout(io.StringIO()):
            env["main"]()
        root = Path("preview-out") if custom else Path("project-root/Assets/Resources") / resource
        expected = [root / (name + ".fbx")]
        if wear:
            expected += [root / "Living" / (name + ".fbx")]
        assert outputs == expected, (source, custom, outputs)
    print(source, "default/custom export routing: OK (export stubbed)")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    args = parser.parse_args()
    folder = args.root / "Assets/Scripts/Tools/blender"
    for name in ("wear.py", "props.py", "flesh.py", "stone.py", "model_shapes.py"):
        ast.parse((folder / name).read_text(encoding="utf-8-sig"), filename=name)
    geometry(runpy.run_path(str(folder / "model_shapes.py")))
    for name in ("wear.py", "props.py"):
        routing(folder, name)
    print("PASS: Python source, helper geometry, CLI routing. Blender/FBX/Unity not tested.")


if __name__ == "__main__":
    main()
