# Assets/Scripts/Tools/blender/compare.py
"""
Наша кость рядом с эталоном — одной камерой, одним светом.

Так собирался скелет 27 сентября (`anatomy.py`, `14-HANDOFF.md` §112.1):
«сравнивать кадром рядом, а не на глаз» — слово из задания, и два прежних
захода на череп по образцу, сделанные на глаз, были откачены.

Эталон — `Skeleton02(SKETCHFAB).glb` с флешки автора. Он перекладывается
в наши координаты: весь скелет ростом 1,0 с опорой в нуле, череп отдельно —
темя 1,0, подбородок 0,862 (голова у нас 0,138 роста). Переложенный эталон
кешируется в `.blend`: сам образец весит 64 МБ и грузится минуту.

Запуск:

    blender --background --python Tools/blender/compare.py -- --out <папка> --part skull --view front,side,q
    blender --background --python Tools/blender/compare.py -- --out <папка> --part pelvis+lumbar+legs \
        --ref body --box -0.13,-0.08,0.44,0.13,0.08,0.70

`--part` — функции `anatomy.py` через «+»; `--ref` — skull или body;
`--box` — рамка кадра в координатах тела (без неё — рамка нашей части);
`--res` — сторона кадра. Кадры: `<папка>/<часть>-<вид>.png`, слева эталон,
справа наше.

Образец с флешки Блендер иногда не читает (`OSError: [Errno 22] Invalid
argument`, 27 сентября) — поэтому он сперва копируется в папку кеша.
"""

import shutil
import sys
import time
from pathlib import Path

import bpy
import bmesh
import numpy as np
from mathutils import Matrix, Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import anatomy
import bodies
import reference

# Рамка эталона в его собственных координатах (замер 27 сентября):
# опора, темя, середина по X и по Y. У другого образца — свои числа.
BASE_Z, TOP_Z, MID_X, MID_Y = 0.176, 7.154, 1.338, -0.959

VIEWS = dict(front=(0, -1, 0), side=(1, 0, 0), top=(0, -0.05, 1), q=(0.62, -0.78, 0.15),
             below=(0.2, -0.7, -0.6), back=(0, 1, 0))


def opt(argv, key, default=None):
    return argv[argv.index(key) + 1] if key in argv else default


def bake(objs, matrix, name, keep):
    """Слить объекты в один меш с переносом и прореживанием."""
    bm = bmesh.new()
    for o in objs:
        tmp = bmesh.new()
        tmp.from_mesh(o.data)
        tmp.transform(matrix @ o.matrix_world)
        me = bpy.data.meshes.new("t")
        tmp.to_mesh(me)
        tmp.free()
        bm.from_mesh(me)
        bpy.data.meshes.remove(me)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    if keep < 1.0:
        mod = obj.modifiers.new("d", "DECIMATE")
        mod.ratio = keep
        graph = bpy.context.evaluated_depsgraph_get()
        thin = bpy.data.meshes.new_from_object(obj.evaluated_get(graph))
        obj.modifiers.clear()
        old = obj.data
        obj.data = thin
        bpy.data.meshes.remove(old)
    return obj


def cache_reference(src, cache):
    """Эталон в наших координатах — REF_BODY и REF_SKULL — в cache/эталон.blend."""
    local = cache / "эталон.glb"
    if not local.exists():
        shutil.copyfile(src, local)

    bodies.wipe()
    bpy.ops.import_scene.gltf(filepath=str(local))
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]

    def lowest(o):
        return min((o.matrix_world @ v.co).z for v in o.data.vertices)

    # Ниже опоры — служебные кольца рига, не кости.
    bones = [o for o in meshes if lowest(o) > BASE_Z - 0.01]
    s = 1.0 / (TOP_Z - BASE_Z)
    body = Matrix.Diagonal((s, s, s, 1.0)) @ Matrix.Translation(Vector((-MID_X, -MID_Y, -BASE_Z)))
    bake(bones, body, "REF_BODY", 0.08)

    skull_parts = [o for o in bones if o.name.startswith(("Schädel", "Unterkiefer"))
                   or (o.name.startswith("Icosphere") and lowest(o) > 6.2)]
    vs = [o.matrix_world @ v.co for o in skull_parts for v in o.data.vertices]
    zlo, zhi = min(v.z for v in vs), max(v.z for v in vs)
    ylo, yhi = min(v.y for v in vs), max(v.y for v in vs)
    k = 0.138 / (zhi - zlo)
    skull = (Matrix.Translation(Vector((0, 0, 1.0))) @ Matrix.Diagonal((k, k, k, 1.0))
             @ Matrix.Translation(Vector((-MID_X, -(ylo + yhi) * 0.5, -zhi))))
    bake(skull_parts, skull, "REF_SKULL", 0.25)

    for o in list(bpy.context.scene.objects):
        if o.name not in ("REF_BODY", "REF_SKULL"):
            bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(cache / "эталон.blend"))


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = Path(opt(argv, "--out"))
    out.mkdir(parents=True, exist_ok=True)
    cache = Path(opt(argv, "--cache", str(out)))
    cache.mkdir(parents=True, exist_ok=True)
    part = opt(argv, "--part", "skull")
    views = opt(argv, "--view", "front,side,q").split(",")
    ref_name = "REF_" + opt(argv, "--ref", "skull").upper()
    box = opt(argv, "--box")
    res = int(opt(argv, "--res", "480"))

    if not (cache / "эталон.blend").exists():
        src = opt(argv, "--src")
        src = Path(src) if src else reference.elsewhere(reference.ELSEWHERE[0])
        if not src.exists():
            raise SystemExit(f"[СВЕРКА] эталона нет: {src} — подключите флешку или --src")
        cache_reference(src, cache)

    bodies.wipe()
    b = bodies.Body()
    t = time.time()
    for name in part.split("+"):
        getattr(anatomy, name)(b, dict(bodies.SKELETON))
    print(f"[СВЕРКА] {part}: граней {len(b.faces)} за {time.time() - t:.1f} с")

    mesh = bpy.data.meshes.new("наше")
    mesh.from_pydata(b.verts, [], b.faces)
    mesh.validate(verbose=False)
    tones = [(0.72, 0.695, 0.63, 1), (0.50, 0.48, 0.43, 1), (0.035, 0.03, 0.032, 1), (0.9, 0.5, 0.1, 1)]
    for i, c in enumerate(tones):
        m = bpy.data.materials.new("m%d" % i)
        m.diffuse_color = c
        mesh.materials.append(m)
    if len(b.mats) == len(mesh.polygons):
        mesh.polygons.foreach_set("material_index", b.mats)
        mesh.polygons.foreach_set("use_smooth", b.smooth)
    ours = bpy.data.objects.new("наше", mesh)
    bpy.context.scene.collection.objects.link(ours)

    with bpy.data.libraries.load(str(cache / "эталон.blend")) as (src, dst):
        dst.objects = [ref_name]
    ref = dst.objects[0]
    bpy.context.scene.collection.objects.link(ref)
    rm = bpy.data.materials.new("эталон")
    rm.diffuse_color = tones[0]
    ref.data.materials.clear()
    ref.data.materials.append(rm)
    for pl in ref.data.polygons:
        pl.use_smooth = True

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.show_cavity = True
    scene.display.shading.color_type = "MATERIAL"
    scene.world = bpy.data.worlds.new("w")
    scene.world.color = (0.2, 0.2, 0.22)
    cam_data = bpy.data.cameras.new("c")
    cam_data.type = "ORTHO"
    cam = bpy.data.objects.new("c", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam

    if box:
        v = [float(x) for x in box.split(",")]
        lo, hi = Vector(v[:3]), Vector(v[3:])
    else:
        vs = [Vector(v) for v in b.verts]
        lo = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
        hi = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
    centre, size = (lo + hi) * 0.5, max(hi - lo)

    for tag in views:
        tiles = []
        for show, hide in ((ref, ours), (ours, ref)):
            show.hide_render, hide.hide_render = False, True
            d = Vector(VIEWS[tag]).normalized()
            cam.location = centre + d * 5.0
            cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
            cam_data.ortho_scale = size * 1.2
            scene.render.resolution_x = scene.render.resolution_y = res
            path = out / "_кадр.png"
            scene.render.filepath = str(path)
            bpy.ops.render.render(write_still=True)
            img = bpy.data.images.load(str(path))
            tiles.append(np.array(img.pixels[:], dtype=np.float32).reshape(res, res, 4))
            bpy.data.images.remove(img)
        joined = bpy.data.images.new("рядом", res * 2, res)
        joined.pixels[:] = np.concatenate(tiles, axis=1).ravel()
        joined.filepath_raw = str(out / f"{part}-{tag}.png")
        joined.file_format = "PNG"
        joined.save()
        bpy.data.images.remove(joined)
        print(f"[СВЕРКА] {out / (part + '-' + tag + '.png')}")


if __name__ == "__main__":
    main()
