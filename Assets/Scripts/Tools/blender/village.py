# Assets/Scripts/Tools/blender/village.py
"""
Деревня «Перетягивания»: изба, амбар, колодец, плетень, поленница (автор, 28.10: «не хватает строений
и окружения для деревень»; «почему ты не можешь сделать новые модели, наши, смотря на скачанные?»).

Тот же конвейер, что props.py: всё числами, без bpy.ops в геометрии, один FBX на вещь в
Assets/Resources/Props/. Размеры настоящие: воин 1,2 м, изба — сруб 5×4 м, стены 2,2 м, конёк 4 м.
Стиль — как у наших палаток и сухих деревьев: тёмное дерево, мало цвета, силуэт читается сверху.

Запуск:
    blender --background --python Tools/blender/village.py -- --preview <папка>        (только превью)
    blender --background --python Tools/blender/village.py -- --export                 (в игру)
    ... -- --prop Izba ...                                                               (одна вещь)
"""

import math
import os
import sys
from pathlib import Path

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bodies
import props
from bodies import box, sphere, tube
from props import Prop, _tilted, _wobble

LOG = ("Log", (0.235, 0.175, 0.125, 1.0))           # бревно сруба — теплее палаточного дерева
LOG_END = ("Log End", (0.330, 0.265, 0.190, 1.0))   # торцы и тёс — светлее, по ним читается сруб
ROOF = ("Old Boards", (0.120, 0.115, 0.110, 1.0))   # тёс крыши — серый, выгоревший
DARK = props.DARK
STONE = props.STONE
IRON = props.IRON
STRAW = ("Thatch", (0.300, 0.245, 0.150, 1.0))


# ---------------------------------------------------------------- изба

def _wall_x(b, y, x0, x1, z, r, gaps=(), extend=0.28):
    """Бревно вдоль X с выпусками на углах; gaps — проёмы (x0, x1) по этому венцу."""
    a, e = x0 - extend, x1 + extend
    cuts = sorted(gaps)
    cur = a
    for g0, g1 in cuts:
        if g0 > cur: b.add(*tube((cur, y, z), (g0, y, z), r, r, segs=8), bone="", mat=0)
        cur = max(cur, g1)
    if e > cur: b.add(*tube((cur, y, z), (e, y, z), r, r, segs=8), bone="", mat=0)


def _wall_y(b, x, y0, y1, z, r, gaps=(), extend=0.28):
    a, e = y0 - extend, y1 + extend
    cur = a
    for g0, g1 in sorted(gaps):
        if g0 > cur: b.add(*tube((x, cur, z), (x, g0, z), r, r, segs=8), bone="", mat=0)
        cur = max(cur, g1)
    if e > cur: b.add(*tube((x, cur, z), (x, e, z), r, r, segs=8), bone="", mat=0)


def izba(b):
    """
    Изба: сруб «в обло» (брёвна перекрещены на углах с выпуском), десять венцов; дверь в торце с крыльцом,
    окошко со ставнями сбоку; двускатная крыша из тёса с охлупнем, фронтоны зашиты досками; каменная труба;
    валуны под углами. Материалы: 0 — бревно, 1 — торцы и тёс светлее, 2 — тёмное (проёмы, дверь), 3 — крыша,
    4 — камень.
    """
    W, D = 5.0, 4.0                    # по X — длина, по Y — ширина; дверь — в торце -Y
    r = 0.12
    step = 0.22
    courses = 10
    hw, hd = W / 2, D / 2
    door = (-0.45, 0.45)               # по X на торце -Y
    door_top = 1.75
    win = (-0.35, 0.35)                # по Y на стене +X
    win_z = (0.95, 1.45)

    # Валуны-подкладки под углами и серединами — сруб стоит не на земле.
    for i, (x, y) in enumerate(((-hw, -hd), (hw, -hd), (hw, hd), (-hw, hd), (0, -hd), (0, hd))):
        b.add(*sphere((x, y, 0.10), 0.26, scale=(1.2, 1.0, 0.6), segs=8, rings=5), bone="", mat=4)

    # Венцы: длинные стены (вдоль X) и короткие (вдоль Y) — со сдвигом на полвенца, чтобы углы сцепились.
    for k in range(courses):
        z = 0.30 + k * step
        zy = z + step * 0.5
        front_gap = [door] if z < door_top else []
        _wall_x(b, -hd, -hw, hw, z, r, gaps=front_gap)
        _wall_x(b, hd, -hw, hw, z, r)
        side_gap = [win] if win_z[0] < zy < win_z[1] else []
        _wall_y(b, -hw, -hd, hd, zy, r)
        _wall_y(b, hw, -hd, hd, zy, r, gaps=side_gap)

    top = 0.30 + courses * step       # верх сруба
    # Торцы брёвен на углах — светлые кружки: по ним сруб узнаётся сверху.
    for k in range(courses):
        z = 0.30 + k * step
        for x in (-hw - 0.28, hw + 0.28):
            for y in (-hd, hd):
                b.add(*tube((x - 0.01 * math.copysign(1, x), y, z), (x + 0.005 * math.copysign(1, x), y, z), r * 0.92, r * 0.92, segs=8), bone="", mat=1)

    # Дверь: доски тёмные, две железные полосы; косяки.
    b.add(*box((0, -hd + 0.02, 0.30 + door_top / 2 - 0.05), (0.88, 0.06, door_top - 0.1)), bone="", mat=2)
    for z in (0.75, 1.55):
        b.add(*box((0, -hd - 0.02, z), (0.80, 0.02, 0.06)), bone="", mat=5)
    for x in door:
        b.add(*box((x, -hd, 0.30 + door_top / 2), (0.10, 0.26, door_top)), bone="", mat=1)
    b.add(*box((0, -hd, 0.30 + door_top + 0.04), (1.10, 0.26, 0.10)), bone="", mat=1)

    # Крыльцо: две ступени и площадка.
    b.add(*box((0, -hd - 0.55, 0.22), (1.4, 0.9, 0.12)), bone="", mat=1)
    b.add(*box((0, -hd - 1.15, 0.10), (1.2, 0.35, 0.10)), bone="", mat=1)

    # Окошко: тёмный проём, рама, ставни раскрыты.
    wz = (win_z[0] + win_z[1]) / 2 - 0.08
    b.add(*box((hw - 0.02, 0, wz), (0.06, win[1] - win[0], win_z[1] - win_z[0])), bone="", mat=2)
    b.add(*box((hw + 0.05, 0, wz), (0.05, win[1] - win[0] + 0.12, 0.08)), bone="", mat=1)       # подоконник
    b.add(*box((hw + 0.05, 0, wz + (win_z[1] - win_z[0]) / 2 + 0.04), (0.05, win[1] - win[0] + 0.12, 0.08)), bone="", mat=1)
    for y in win:
        b.add(*box((hw + 0.12, y + math.copysign(0.24, y), wz), (0.05, 0.42, 0.60)), bone="", mat=3)   # ставни — тёс крыши

    # Крыша: два ската из тёса (продольные доски), угол 38°, свес 0,45; конёк вдоль X.
    pitch = math.radians(38)
    over = 0.45
    run = hd + over
    rise = math.tan(pitch) * (hd + 0.1)
    ridge_z = top + rise
    slope = run / math.cos(pitch)
    boards = 9
    for side in (-1, 1):
        for i in range(boards):
            t = (i + 0.5) / boards
            dy = side * (t * run)
            dz = ridge_z - t * run * math.tan(pitch)
            w = slope / boards * 1.05
            v, f = box((0, 0, 0), (W + 2 * over, w * 1.02, 0.09))
            v = _tilted(v, -side * pitch, (0.0, dy, dz))
            b.add(v, f, bone="", mat=3)
    # Причелины — доски по краю ската над фронтонами: закрывают торцы тёса, край крыши читается.
    for x in (-hw - over, hw + over):
        for side in (-1, 1):
            v, f = box((0, 0, 0), (0.07, slope + 0.1, 0.22))
            v = _tilted(v, -side * pitch, (x, side * run * 0.5, ridge_z - run * 0.5 * math.tan(pitch) + 0.02))
            b.add(v, f, bone="", mat=1)
    # Охлупень — тёсаное бревно на коньке, с выпуском.
    b.add(*tube((-hw - over - 0.15, 0, ridge_z + 0.08), (hw + over + 0.15, 0, ridge_z + 0.08), 0.15, 0.15, segs=8), bone="", mat=1)

    # Фронтоны — над короткими стенами (x = ±hw): вертикальные доски треугольником под скаты.
    for x in (-hw, hw):
        n = 9
        for i in range(n):
            y = -hd + (i + 0.5) * D / n
            # Под скат с запасом: верх доски не выше плоскости крыши у ближнего к краю ребра доски.
            edge = abs(y) + D / n * 0.5
            h = max(0.04, ridge_z - edge * math.tan(pitch) - top - 0.10)
            b.add(*box((x, y, top + h / 2), (0.06, D / n * 0.98, h)), bone="", mat=1)

    # Труба: камень, выше конька.
    b.add(*box((hw * 0.45, hd * 0.35, top + rise * 0.5 + 0.4), (0.55, 0.55, rise + 1.0)), bone="", mat=4)
    b.add(*box((hw * 0.45, hd * 0.35, ridge_z + 0.95), (0.68, 0.68, 0.12)), bone="", mat=4)


VILLAGE = [
    Prop("Izba", izba, [LOG, LOG_END, DARK, ROOF, STONE, IRON]),
]


# ---------------------------------------------------------------- превью

def preview(prop, obj, folder):
    """Два кадра: с угла на уровне глаз и сверху под углом камеры игры; свет — сумерки, как в игре."""
    folder = Path(folder)
    folder.mkdir(parents=True, exist_ok=True)
    sc = bpy.context.scene
    engines = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
    sc.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in engines else "BLENDER_EEVEE"
    sc.render.resolution_x, sc.render.resolution_y = 960, 640
    sc.view_settings.view_transform = "Standard"     # цвета как в игре, без киношного осветления
    world = bpy.data.worlds.new("W"); sc.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.05, 0.055, 0.07, 1.0)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.9
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sun.data.energy = 2.2
    sun.data.color = (0.75, 0.78, 0.95)
    sun.rotation_euler = (math.radians(55), 0, math.radians(35))
    sc.collection.objects.link(sun)
    fire = bpy.data.objects.new("Fire", bpy.data.lights.new("Fire", "POINT"))
    fire.data.energy = 600; fire.data.color = (1.0, 0.55, 0.25)
    fire.location = (-3.5, -5.0, 1.0)
    sc.collection.objects.link(fire)
    ground = bpy.data.meshes.new("Ground")
    ground.from_pydata([(-20, -20, 0), (20, -20, 0), (20, 20, 0), (-20, 20, 0)], [], [(0, 1, 2, 3)])
    gm = bpy.data.materials.new("Ground"); gm.diffuse_color = (0.09, 0.085, 0.08, 1)
    gm.use_nodes = True; gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.09, 0.085, 0.08, 1)
    ground.materials.append(gm)
    sc.collection.objects.link(bpy.data.objects.new("Ground", ground))

    lo = Vector((99, 99, 99)); hi = Vector((-99, -99, -99))
    for v in obj.data.vertices:
        lo = Vector(map(min, lo, v.co)); hi = Vector(map(max, hi, v.co))
    mid = (lo + hi) * 0.5
    size = (hi - lo).length
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    sc.collection.objects.link(cam); sc.camera = cam
    for tag, offset in (("угол", Vector((size * 0.75, -size * 0.95, size * 0.30))),
                        ("сверху", Vector((size * 0.35, -size * 0.55, size * 1.25)))):
        cam.location = mid + offset
        cam.rotation_euler = (mid - cam.location).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = str(folder / "{} — {}.png".format(prop.name, tag))
        bpy.ops.render.render(write_still=True)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

    def opt(key):
        return argv[argv.index(key) + 1] if key in argv else None

    only = opt("--prop")
    shots = opt("--preview")
    out = bodies.unity_root() / "Assets" / "Resources" / "Props"
    for prop in [p for p in VILLAGE if only is None or p.name == only]:
        obj = props.build(prop)
        print("[ДЕРЕВНЯ] {}: граней {}".format(prop.name, len(obj.data.polygons)))
        if "--export" in argv:
            props.export(out / (prop.name + ".fbx"))
        if shots:
            preview(prop, obj, shots)


if __name__ == "__main__":
    main()
