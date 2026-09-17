# Assets/Scripts/Tools/blender/wear.py
"""
Гардероб: части, которые надеваются на тело поверх оболочки.

Замысел — `docs/22-LOOK.md`: девять воинов различаются не девятью
моделями, а **двенадцатью предметами**, которые надеваются на общее
тело. Здесь эти предметы и лежат.

Каждый предмет — отдельный FBX в `Assets/Resources/Wear/`, без костей
и без движений: он жёсткий и висит на одной кости. Крепит его
`Gameplay.Wardrobe` во время игры, а выбирает — по тому, кем воин
является: ремесло, легенда, братство, опыт. Ни одного жребия
(`22-LOOK.md` §3).

**Всё считается в том же пространстве, что и тело** (`bodies.py`,
`BASE`): капюшон строится ровно там, где у тела голова. В Unity предмет
садится на кость матрицей привязки — `bindposes` того же меша, — и
потому попадает туда же, куда попал бы, будь он частью тела.

Запуск:

    blender --background --python Tools/blender/wear.py
    blender --background --python Tools/blender/wear.py -- --item Hood --preview <папка>
"""

import math
import os
import sys
from collections import namedtuple
from pathlib import Path

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bodies
from bodies import BASE, Body, box, ring, sphere, tube

P = BASE

# Материалы. Порядок смысла тот же, что у тел: 0 — из чего сделано,
# 1 — чем отличается, 2 — тёмное.
CLOTH = ("Cloth", (0.215, 0.190, 0.165, 1.0))
LEATHER = ("Leather", (0.165, 0.125, 0.095, 1.0))
IRON = ("Iron", (0.330, 0.340, 0.355, 1.0))
BONE = ("Bone", (0.855, 0.830, 0.760, 1.0))
STRAW = ("Straw", (0.520, 0.455, 0.300, 1.0))
DARK = ("Dark", (0.060, 0.055, 0.050, 1.0))
FEATHER = ("Feather", (0.085, 0.080, 0.090, 1.0))
GLASS = ("Glass", (0.300, 0.380, 0.330, 1.0))

Item = namedtuple("Item", "name bone build materials")


# ------------------------------------------------------------- головное

def hood(b):
    """Капюшон: полусфера с вырезом под лицо и валик по краю."""
    z = P["skull"]
    b.add(*sphere((0, 0.020, z + 0.010), 0.088, scale=(1.0, 1.08, 1.08),
                  segs=26, rings=16,
                  holes=[((0.0, -1.0, -0.22), 62.0), ((0.0, 0.0, -1.0), 44.0)]),
          bone="Head")
    b.add(*ring((0, 0.020, z - 0.026), 0.086, 0.013, scale=(1.0, 1.08, 1.0), segs=22),
          bone="Head", mat=1)


def wide_hat(b):
    """Широкополая шляпа ловчего: сверху видно её одну."""
    z = P["skull"] + 0.052
    b.add(*ring((0, 0.010, z), 0.115, 0.014, scale=(1.0, 1.0, 0.45), segs=24), bone="Head")
    b.add(*tube((0, 0.010, z - 0.006), (0, 0.010, z + 0.062), 0.072, 0.058, segs=18),
          bone="Head")
    b.add(*ring((0, 0.010, z + 0.010), 0.072, 0.010, scale=(1.0, 1.0, 0.8), segs=20),
          bone="Head", mat=1)


def straw_hat(b):
    """Соломенная шляпа крестьянина: поля шире, тулья ниже."""
    z = P["skull"] + 0.046
    b.add(*ring((0, 0.010, z), 0.125, 0.013, scale=(1.0, 1.0, 0.40), segs=24), bone="Head")
    b.add(*tube((0, 0.010, z - 0.004), (0, 0.010, z + 0.040), 0.074, 0.066, segs=16),
          bone="Head")


def inquisitor_cap(b):
    """
    Высокий колпак инквизитора. Он рангом выше охотников, и сверху
    это видно раньше всего остального: колпак — самая высокая точка
    силуэта.
    """
    z = P["skull"] + 0.040
    b.add(*ring((0, 0.010, z), 0.086, 0.012, scale=(1.0, 1.0, 0.55), segs=22), bone="Head")
    b.add(*tube((0, 0.010, z), (0, 0.010, z + 0.155), 0.070, 0.040, segs=16), bone="Head")
    b.add(*box((0, -0.058, z + 0.070), (0.030, 0.014, 0.090)), bone="Head", mat=1)


def circlet(b):
    """Обруч на лбу. Гордыня 90: он сам себя таким видит."""
    # Прижат ко лбу, а не парит над макушкой: первый заход дал нимб.
    b.add(*ring((0, 0.014, P["skull"] + 0.004), 0.070, 0.008, scale=(1.0, 1.10, 1.0), segs=20),
          bone="Head")


def crack(b):
    """Скол черепа: тёмная трещина через темя. Личная примета."""
    b.add(*box((0.020, 0.006, P["skull"] + 0.058), (0.012, 0.086, 0.030)), bone="Head")
    b.add(*box((0.040, -0.030, P["skull"] + 0.040), (0.010, 0.030, 0.040)), bone="Head")


# ------------------------------------------------------------- плечевое

def raven_mantle(b):
    """
    Плащ из вороньих перьев на оба плеча — Карган Старый Ворон
    (`23-PROMPTS.md` §3). Имя носят там, где его видно сверху.
    """
    sh = P["shoulder"]
    b.add(*ring((0, 0.008, sh + 0.024), 0.096, 0.018, scale=(1.0, 0.95, 0.65), segs=22),
          bone="Chest")

    # Перья. Узкие и разной длины, иначе ряд одинаковых пластин читается
    # бочкой, а не оперением (так вышло с первого раза). Сзади длиннее:
    # плащ, а не воротник.
    for i in range(18):
        a = math.radians(14 + i * 19)
        back = 0.5 + 0.5 * math.sin(a)
        length = 0.055 + 0.135 * back
        r = 0.090 + 0.012 * back
        x, y = math.cos(a) * r, math.sin(a) * r * 0.92
        b.add(*box((x, y + 0.008, sh + 0.012 - length * 0.5),
                   (0.022, 0.014, length)), bone="Chest", mat=1)


def pauldron(b, side):
    """Наплечник опытного. Сверху — единственное, чем он отличим."""
    sx = P["shoulder_x"]
    # Лежит на плече, а не висит рядом: первый заход отставил его
    # на три сантиметра вверх и наружу, и он читался шаром в воздухе.
    b.add(*sphere((side * (sx - 0.004), 0.004, P["shoulder"] + 0.008), 0.055,
                  scale=(1.05, 0.95, 0.70), segs=16, rings=10,
                  holes=[((0.0, 0.0, -1.0), 78.0)]),
          bone=("Left" if side > 0 else "Right") + "Shoulder")


def brother_band(b):
    """
    Лента брата по оружию. Одна и та же у обоих — тем и узнаётся пара.
    Была бирюзовым кубом над головой; стала вещью на плече.
    """
    el = P["elbow"]
    sx = P["shoulder_x"]
    x = (sx + el) * 0.5
    b.add(*tube((x - 0.016, 0, P["shoulder"]), (x + 0.016, 0, P["shoulder"]),
                0.038, 0.038, segs=14), bone="LeftUpperArm")


def quiver(b):
    """Колчан за спиной — охотник и лучник."""
    sh = P["shoulder"]
    b.add(*tube((-0.045, 0.085, sh - 0.075), (0.055, 0.105, sh + 0.075), 0.032, 0.036, segs=12),
          bone="Chest")
    for i in range(4):
        b.add(*tube((0.040 + i * 0.012, 0.104, sh + 0.070),
                    (0.046 + i * 0.012, 0.108, sh + 0.145), 0.004, 0.004, segs=6),
              bone="Chest", mat=1)


def bow(b):
    """Лук за спиной. Дуга, а не палка: сверху читается сразу."""
    sh = P["shoulder"]
    for i in range(9):
        t = i / 8.0
        a = math.radians(-70 + t * 140)
        x = math.sin(a) * 0.150
        z = sh + math.cos(a) * 0.150 - 0.030
        nx = math.sin(math.radians(-70 + (t + 0.14) * 140)) * 0.150
        nz = sh + math.cos(math.radians(-70 + (t + 0.14) * 140)) * 0.150 - 0.030
        b.add(*tube((x, 0.092, z), (nx, 0.092, nz), 0.010, 0.010, segs=6), bone="Chest")


def net(b):
    """Свёрнутая сеть ловчего: моток на плече."""
    b.add(*ring((-(P["shoulder_x"] + 0.020), 0.030, P["shoulder"] + 0.022), 0.052, 0.020,
                scale=(1.0, 1.0, 0.8), segs=16), bone="RightShoulder")


# --------------------------------------------------------------- поясное

def flasks(b):
    """Склянки алхимика на поясе."""
    for i, a in enumerate((-40, -8, 26)):
        r = math.radians(a)
        x, y = math.sin(r) * 0.096, -math.cos(r) * 0.086
        b.add(*tube((x, y, P["hip"] - 0.010), (x, y, P["hip"] + 0.048), 0.019, 0.016, segs=10),
              bone="Spine", mat=1)
        b.add(*box((x, y, P["hip"] + 0.056), (0.016, 0.016, 0.014)), bone="Spine")


def tabard(b):
    """
    Табард инквизитора: полотнище с знаком ордена спереди.
    Знак — простой крест из двух брусков: сверху видно не рисунок,
    а то, что на груди что-то есть.
    """
    b.add(*box((0, -0.096, P["chest"] - 0.030), (0.150, 0.016, 0.230)), bone="Spine")
    b.add(*box((0, -0.106, P["chest"] + 0.010), (0.024, 0.010, 0.120)), bone="Spine", mat=1)
    b.add(*box((0, -0.106, P["chest"] + 0.040), (0.090, 0.010, 0.024)), bone="Spine", mat=1)


def cloak(b):
    """Длинный плащ следопыта: от плеч до колен, сзади."""
    sh = P["shoulder"]
    b.add(*ring((0, 0.010, sh + 0.018), 0.108, 0.016, scale=(1.0, 0.95, 0.7), segs=20),
          bone="Chest")
    b.add(*box((0, 0.098, (sh + P["knee"]) * 0.5), (0.215, 0.018, sh - P["knee"])),
          bone="Spine", mat=1)


ITEMS = [
    Item("Hood",           "Head",  hood,            [CLOTH, LEATHER, DARK]),
    Item("WideHat",        "Head",  wide_hat,        [LEATHER, DARK, DARK]),
    Item("StrawHat",       "Head",  straw_hat,       [STRAW, LEATHER, DARK]),
    Item("InquisitorCap",  "Head",  inquisitor_cap,  [CLOTH, IRON, DARK]),
    Item("Circlet",        "Head",  circlet,         [IRON, IRON, DARK]),
    Item("Crack",          "Head",  crack,           [DARK, DARK, DARK]),

    Item("RavenMantle",    "Chest", raven_mantle,    [LEATHER, FEATHER, DARK]),
    Item("PauldronLeft",   "LeftShoulder",  lambda b: pauldron(b, 1),  [IRON, IRON, DARK]),
    Item("PauldronRight",  "RightShoulder", lambda b: pauldron(b, -1), [IRON, IRON, DARK]),
    Item("BrotherBand",    "LeftUpperArm",  brother_band,              [(("Band"), (0.420, 0.300, 0.190, 1.0)), LEATHER, DARK]),
    Item("Quiver",         "Chest", quiver,          [LEATHER, BONE, DARK]),
    Item("Bow",            "Chest", bow,             [LEATHER, LEATHER, DARK]),
    Item("Net",            "RightShoulder", net,     [CLOTH, CLOTH, DARK]),

    Item("Flasks",         "Spine", flasks,          [LEATHER, GLASS, DARK]),
    Item("Tabard",         "Spine", tabard,          [CLOTH, IRON, DARK]),
    Item("Cloak",          "Chest", cloak,           [CLOTH, CLOTH, DARK]),
]


# ----------------------------------------------------------------- сборка

def build(item):
    bodies.wipe()

    b = Body()
    item.build(b)

    mesh = bpy.data.meshes.new(item.name)
    mesh.from_pydata(b.verts, [], b.faces)
    mesh.validate(verbose=False)

    for label, rgba in item.materials:
        m = bpy.data.materials.new(label)
        m.diffuse_color = rgba
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if bsdf is not None:
            bsdf.inputs["Base Color"].default_value = rgba
            bsdf.inputs["Roughness"].default_value = 0.66
        mesh.materials.append(m)

    for i, mat in enumerate(b.mats):
        if i < len(mesh.polygons):
            mesh.polygons[i].material_index = mat

    obj = bpy.data.objects.new(item.name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def export(path):
    path.parent.mkdir(parents=True, exist_ok=True)

    want = dict(
        filepath=str(path),
        use_selection=False,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_NONE",
        global_scale=1.0,
        object_types={"MESH"},
        use_mesh_modifiers=False,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        bake_anim=False,
        axis_forward="-Z",
        axis_up="Y",
        path_mode="COPY",
        embed_textures=False,
    )

    allowed = set(bpy.ops.export_scene.fbx.get_rna_type().properties.keys())
    bpy.ops.export_scene.fbx(**{k: v for k, v in want.items() if k in allowed})


def preview(item, obj, folder):
    folder = Path(folder)
    folder.mkdir(parents=True, exist_ok=True)

    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.color_type = "MATERIAL"
    sc.display.shading.light = "STUDIO"
    sc.render.resolution_x = 420
    sc.render.resolution_y = 420
    sc.world = bpy.data.worlds.new("W")
    sc.world.color = (0.16, 0.16, 0.18)

    lo = Vector((9, 9, 9))
    hi = Vector((-9, -9, -9))
    for v in obj.data.vertices:
        lo = Vector(map(min, lo, v.co))
        hi = Vector(map(max, hi, v.co))
    mid = (lo + hi) * 0.5
    size = max(0.2, (hi - lo).length)

    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = size * 1.3
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.location = mid + Vector((size * 0.9, -size * 1.1, size * 0.5))
    cam.rotation_euler = (mid - cam.location).to_track_quat("-Z", "Y").to_euler()

    sc.render.filepath = str(folder / (item.name + ".png"))
    bpy.ops.render.render(write_still=True)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

    def opt(key):
        return argv[argv.index(key) + 1] if key in argv else None

    only = opt("--item")
    shots = opt("--preview")

    out = bodies.unity_root() / "Assets" / "Resources" / "Wear"

    chosen = [i for i in ITEMS if only is None or i.name == only]
    if not chosen:
        raise SystemExit("Нет такой части: " + str(only)
                         + ". Есть: " + ", ".join(i.name for i in ITEMS))

    for item in chosen:
        obj = build(item)
        print("[ГАРДЕРОБ] {}: кость {}, граней {}".format(
            item.name, item.bone, len(obj.data.polygons)))

        export(out / (item.name + ".fbx"))
        if shots:
            preview(item, obj, shots)

    print("[ГАРДЕРОБ] записано в " + str(out))


if __name__ == "__main__":
    main()
