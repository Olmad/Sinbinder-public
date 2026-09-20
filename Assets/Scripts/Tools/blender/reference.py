# Assets/Scripts/Tools/blender/reference.py
"""
Замер чужих моделей. Не для того, чтобы их использовать, — чтобы знать
числа.

Решение автора от 17 сентября: готовые модели в игру не идут, они
образец. До 20 сентября это исполнялось наполовину: тела собирались
кодом, но образец при этом никто не открывал, и все пропорции в
`bodies.py` были выбраны на глаз. Видно это стало сразу, как только
появились снимки: человек у нас отличался от скелета только одеждой,
потому что `Living` и `Skeleton` буквально делят один набор чисел.

Этот сценарий читает модели из загрузок автора и печатает их
пропорции в долях роста. Дальше числа сверяются с нашими руками —
не переносятся, а сверяются: у образцов свой стиль, и копировать
их значило бы делать чужую игру.

**Как меряем.** Рига у большинства образцов нет, поэтому меряем
силуэт: рост делим на слои и в каждом берём полуширину. Плечи — самый
широкий слой верхней половины, шея — самый узкий над ним, талия —
самый узкий в середине, бёдра — самый широкий под ней. Это те же
величины, которыми задан наш костяк (`bodies.py`, BASE), и потому
их можно сравнивать напрямую.

Если риг есть, кости меряются заодно и печатаются отдельной строкой:
они точнее силуэта, но есть не у всех.

Запуск:

    blender --background --python Tools/blender/reference.py
    blender --background --python Tools/blender/reference.py -- --shots <папка>
"""

import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

# Blender запускает сценарий, не добавляя его папку в пути импорта:
# без этой строки соседний `bodies` не находится.
sys.path.insert(0, str(Path(__file__).resolve().parent))

# Человекоподобные образцы. Оружие, мебель и города пропущены:
# у них нет ни роста, ни плеч.
#
# Часть лежит в загрузках, часть — на диске D автора, в папках
# `_inspect_*`. Скелет оттуда автор назвал образцовым, и это главный
# образец для оболочки Skeleton: у него есть то, чего силуэт не даёт, —
# как устроены рёбра, таз и кисть.
ELSEWHERE = [
    r"D:\_inspect_skeleton\source\Skeleton02(SKETCHFAB).glb",
    r"D:\_inspect_manthing\source\ManThing.fbx",
    r"D:\_inspect_chibi\source\chibi body.obj",
    r"D:/3d_scan_man_1.glb",
    r"D:\military_soldier.glb",
    r"D:\private_military_contractor.glb",
]

HUMANOIDS = [
    "armored_executioner_-_horned_helm__flail.glb",
    "bone_knight_-_horned_skull_greatsword.glb",
    "corrupted_fallen_priest_-_dark_fantasy.glb",
    "dark_necromancer_-_corrupted_staff__skulls.glb",
    "dragon_hunter_warrior.glb",
    "fallen_paladin_in_corrupted_black_plate_armor.glb",
    "faceted_character_locomotion_animation.glb",
    "low_poly_female_warrior_character_3d_model.glb",
    "stylized_barbarian.glb",
    "twintip_dragonborn.glb",
]

SLICES = 72


def downloads():
    """Папка загрузок автора."""
    return Path.home() / "Downloads"


def wipe():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)

    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.armatures,
                  bpy.data.actions, bpy.data.images):
        for item in list(block):
            block.remove(item)


def load(path):
    """Загрузить образец. Возвращает False, если формат не по зубам."""
    suffix = path.suffix.lower()

    try:
        if suffix in (".glb", ".gltf"):
            bpy.ops.import_scene.gltf(filepath=str(path))
        elif suffix == ".fbx":
            bpy.ops.import_scene.fbx(filepath=str(path))
        elif suffix == ".obj":
            bpy.ops.wm.obj_import(filepath=str(path))
        else:
            print(f"[ОБРАЗЕЦ] {path.name}: формат {suffix} не читаем")
            return False

        return True
    except Exception as e:                                  # noqa: BLE001
        print(f"[ОБРАЗЕЦ] {path.name}: не читается ({e})")
        return False


def points(body_only=False):
    """
    Вершины сцены в мировых координатах.

    <b>body_only</b> оставляет только самый крупный меш. Это грубо,
    но работает: у образцов оружие, плащ и рога лежат отдельными
    объектами, и без такого отбора «плечи» находились на конце косы,
    а «шея» — на острие рога.
    """
    meshes = [o for o in bpy.context.scene.objects
              if o.type == "MESH" and o.visible_get() and len(o.data.vertices) > 0]

    if body_only and meshes:
        meshes = [max(meshes, key=lambda o: len(o.data.vertices))]

    out = []
    for obj in meshes:
        matrix = obj.matrix_world
        for v in obj.data.vertices:
            out.append(matrix @ v.co)

    return out


def profile(verts):
    """
    Силуэт: рост, и в каждом слое — полуширина и полуглубина
    в долях роста. Начало отсчёта — подошвы.
    """
    zs = [v.z for v in verts]
    low, high = min(zs), max(zs)
    tall = high - low
    if tall < 1e-6:
        return None

    mid_x = (max(v.x for v in verts) + min(v.x for v in verts)) * 0.5
    mid_y = (max(v.y for v in verts) + min(v.y for v in verts)) * 0.5

    wide = [0.0] * SLICES
    deep = [0.0] * SLICES

    # Копим все полуширины слоя, а ширину берём не крайнюю, а девяносто
    # вторую по сотне: одна вершина рога или пряжки не должна решать,
    # где у образца плечи.
    bins_x = [[] for _ in range(SLICES)]
    bins_y = [[] for _ in range(SLICES)]

    for v in verts:
        i = min(SLICES - 1, int((v.z - low) / tall * SLICES))
        bins_x[i].append(abs(v.x - mid_x) / tall)
        bins_y[i].append(abs(v.y - mid_y) / tall)

    def cut(values):
        if not values:
            return 0.0
        values.sort()
        return values[min(len(values) - 1, int(len(values) * 0.92))]

    for i in range(SLICES):
        wide[i] = cut(bins_x[i])
        deep[i] = cut(bins_y[i])

    return dict(tall=tall, wide=wide, deep=deep)


def widest(wide, lo, hi):
    """Самый широкий слой в промежутке — и его высота в долях роста."""
    a, b = int(lo * SLICES), int(hi * SLICES)
    best, at = -1.0, lo

    for i in range(max(0, a), min(SLICES, b)):
        if wide[i] > best:
            best, at = wide[i], (i + 0.5) / SLICES

    return at, best


def drop(wide, neck):
    """
    Плечи: первый слой ниже шеи, который заметно шире её.

    Самый широкий слой верхней половины годится только для фигуры
    по стойке смирно. У образца с поднятой косой он приходится
    на локоть, и «плечи» оказывались ниже талии.
    """
    a = int(neck * SLICES)
    base = wide[min(SLICES - 1, a)]

    for i in range(a, max(0, a - SLICES // 4), -1):
        if wide[i] > base * 1.45:
            return (i + 0.5) / SLICES, wide[i]

    return neck, base


def narrowest(wide, lo, hi):
    """Самый узкий слой — но только там, где вообще есть тело."""
    a, b = int(lo * SLICES), int(hi * SLICES)
    best, at = 1e9, lo

    for i in range(max(0, a), min(SLICES, b)):
        if wide[i] < 1e-4:          # пустой слой — это не талия, а дыра
            continue
        if wide[i] < best:
            best, at = wide[i], (i + 0.5) / SLICES

    return at, best


def bones():
    """Высоты костей в долях роста — если у образца есть риг."""
    rigs = [o for o in bpy.context.scene.objects if o.type == "ARMATURE"]
    if not rigs:
        return None

    verts = points()
    if not verts:
        return None

    low = min(v.z for v in verts)
    tall = max(v.z for v in verts) - low
    if tall < 1e-6:
        return None

    want = ("hips", "spine", "chest", "neck", "head", "shoulder",
            "upperarm", "lowerarm", "hand", "upperleg", "lowerleg", "foot")
    out = {}

    for rig in rigs:
        for bone in rig.data.bones:
            name = bone.name.lower().replace("_", "").replace(".", "").replace(" ", "")
            for key in want:
                if key in name and key not in out:
                    head = rig.matrix_world @ bone.head_local
                    out[key] = round((head.z - low) / tall, 3)

    return out or None


def measure(path):
    wipe()
    if not load(path):
        return None

    verts = points(body_only=True)
    if not verts:
        print(f"[ОБРАЗЕЦ] {path.name}: ни одной вершины")
        return None

    p = profile(verts)
    if p is None:
        return None

    wide = p["wide"]

    neck, neck_x = narrowest(wide, 0.78, 0.94)
    shoulder, shoulder_x = drop(wide, neck)
    waist, waist_x = narrowest(wide, 0.42, 0.66)
    hip, hip_x = widest(wide, 0.34, waist)
    knee, knee_x = narrowest(wide, 0.16, 0.34)

    # Макушка — там, где тело кончается; темя черепа считаем от шеи.
    top = 1.0

    return dict(
        name=path.stem,
        tall=round(p["tall"], 3),
        vertices=len(verts),
        knee=round(knee, 3),
        hip=round(hip, 3),
        waist=round(waist, 3),
        shoulder=round(shoulder, 3),
        neck=round(neck, 3),
        top=top,
        head_share=round(1.0 - neck, 3),
        shoulder_x=round(shoulder_x, 3),
        waist_x=round(waist_x, 3),
        hip_x=round(hip_x, 3),
        knee_x=round(knee_x, 3),
        bones=bones(),
    )


# ------------------------------------------------------------------ наше

# Как задан наш костяк сейчас (bodies.py, BASE). Здесь — копия для
# сравнения; единственная правда по-прежнему там.
OURS = dict(knee=0.275, hip=0.500, waist=0.560, shoulder=0.775,
            neck=0.795, top=0.990, shoulder_x=0.100)


def report(rows):
    if not rows:
        print("[ОБРАЗЕЦ] нечего мерить")
        return

    keys = ("knee", "hip", "waist", "shoulder", "neck", "shoulder_x")

    print()
    print("[ОБРАЗЕЦ] пропорции в долях роста")
    print("  {:38} {:>6} {:>6} {:>6} {:>8} {:>6} {:>8}".format(
        "модель", "колено", "бедро", "талия", "плечи", "шея", "полуплечо"))

    for r in rows:
        print("  {:38} {:6.3f} {:6.3f} {:6.3f} {:8.3f} {:6.3f} {:8.3f}".format(
            r["name"][:38], r["knee"], r["hip"], r["waist"],
            r["shoulder"], r["neck"], r["shoulder_x"]))

    print("  " + "-" * 82)

    avg = {k: sum(r[k] for r in rows) / len(rows) for k in keys}
    print("  {:38} {:6.3f} {:6.3f} {:6.3f} {:8.3f} {:6.3f} {:8.3f}".format(
        "среднее по образцам", avg["knee"], avg["hip"], avg["waist"],
        avg["shoulder"], avg["neck"], avg["shoulder_x"]))
    print("  {:38} {:6.3f} {:6.3f} {:6.3f} {:8.3f} {:6.3f} {:8.3f}".format(
        "наше (bodies.py BASE)", OURS["knee"], OURS["hip"], OURS["waist"],
        OURS["shoulder"], OURS["neck"], OURS["shoulder_x"]))

    print()
    print("[ОБРАЗЕЦ] расхождение нашего со средним, в долях роста:")
    for k in keys:
        d = OURS[k] - avg[k]
        mark = "" if abs(d) < 0.02 else ("  <-- заметно" if abs(d) < 0.05
                                         else "  <-- сильно")
        print(f"  {k:12} {d:+.3f}{mark}")


def frame(folder, name, tall, low):
    """
    Снять то, что сейчас в сцене, спереди и в три четверти.

    Камера ортографическая и подогнана по росту: образец в три метра
    и наше тело в метр обязаны лечь в кадр одинаково, иначе сравнивать
    нечего. Свет тоже один на всех — по той же причине.
    """
    folder = Path(folder)
    folder.mkdir(parents=True, exist_ok=True)

    mid = low + tall * 0.5

    cam_data = bpy.data.cameras.new("c")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = tall * 1.15
    cam = bpy.data.objects.new("c", cam_data)
    bpy.context.scene.collection.objects.link(cam)
    bpy.context.scene.camera = cam

    for lamp_at, energy in (((tall, -tall * 1.4, low + tall * 1.3), 1200.0),
                            ((-tall * 1.1, -tall * 0.7, low + tall * 0.7), 300.0)):
        light = bpy.data.lights.new("l", type="POINT")
        light.energy = energy * (tall * tall)
        obj = bpy.data.objects.new("l", light)
        obj.location = lamp_at
        bpy.context.scene.collection.objects.link(obj)

    scene = bpy.context.scene
    scene.render.resolution_x = 520
    scene.render.resolution_y = 760
    scene.world = bpy.data.worlds.new("w")
    scene.world.color = (0.21, 0.21, 0.23)

    for tag, angle in (("спереди", 0.0), ("три-четверти", 38.0)):
        a = math.radians(angle)
        cam.location = (math.sin(a) * tall * 2.0, -math.cos(a) * tall * 2.0, mid)
        cam.rotation_euler = (Vector((0.0, 0.0, mid)) - Vector(cam.location))             .to_track_quat("-Z", "Y").to_euler()

        scene.render.filepath = str(folder / f"{name}-{tag}.png")
        bpy.ops.render.render(write_still=True)

    print(f"[ОБРАЗЕЦ] {name}: снято в {folder}")


def ours(folder):
    """
    Наши тела тем же кадром и тем же светом.

    Без этого сравнение нечестное: наши превью снимаются своей камерой
    и своим светом, и разница вышла бы между съёмками, а не между
    моделями.
    """
    import bodies

    for shell in bodies.SHELLS:
        wipe()

        b = bodies.Body()
        shell.build(b, shell.parts)

        mesh = bpy.data.meshes.new(shell.name)
        mesh.from_pydata(b.verts, [], b.faces)
        mesh.validate(verbose=False)

        for label, rgba in shell.materials:
            m = bpy.data.materials.new(label)
            m.diffuse_color = rgba
            m.use_nodes = True
            bsdf = m.node_tree.nodes.get("Principled BSDF")
            if bsdf is not None:
                bsdf.inputs["Base Color"].default_value = rgba
            mesh.materials.append(m)

        for i, mat in enumerate(b.mats):
            if i < len(mesh.polygons):
                mesh.polygons[i].material_index = mat

        obj = bpy.data.objects.new(shell.name, mesh)
        bpy.context.scene.collection.objects.link(obj)

        low = min(v[2] for v in b.verts)
        tall = max(v[2] for v in b.verts) - low
        frame(folder, "НАШЕ-" + shell.name, tall, low)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

    def opt(key):
        return argv[argv.index(key) + 1] if key in argv else None

    shots = opt("--shots")
    rows = []

    paths = [downloads() / name for name in HUMANOIDS]
    paths += [Path(p) for p in ELSEWHERE]

    for path in paths:
        if not path.exists():
            print(f"[ОБРАЗЕЦ] {path.name}: нет на месте ({path.parent})")
            continue

        row = measure(path)
        if row is None:
            continue

        rows.append(row)

        # Снимаем сразу, пока образец в сцене: второй раз его грузить
        # незачем, а в памяти он не остаётся — wipe() чистит всё.
        if shots is not None:
            verts = points()
            if verts:
                low = min(v.z for v in verts)
                frame(shots, path.stem[:38], max(v.z for v in verts) - low, low)

    report(rows)

    if shots is not None:
        ours(shots)

    out = Path(argv[argv.index("--out") + 1]) if "--out" in argv else None
    if out is not None:
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(json.dumps(rows, ensure_ascii=False, indent=2),
                       encoding="utf-8")
        print(f"[ОБРАЗЕЦ] замеры записаны: {out}")


if __name__ == "__main__":
    main()
