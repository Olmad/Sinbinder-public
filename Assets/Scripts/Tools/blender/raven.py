# Assets/Scripts/Tools/blender/raven.py
# Ворон Каргана — своя модель по строению настоящего ворона (автор, 27.10: «ворон у Каргана
# странный. Ты можешь найти нормальную модель и на основе неё сделать свою»; «ворон не улетает
# и не прилетает»). Готовой свободной (CC0) модели поиск не дал — строим по приметам вида:
# тяжёлый клюв с дугой надклювья, крутой лоб, «борода» перьев на горле, клиновидный хвост
# (у вороны он прямой — по нему ворона от ворона и отличают), длинные «пальцы» маховых.
#
# Четыре части, чтобы птица летала (Gameplay/RavenPerch.cs):
#   Body   — туловище, голова, клюв, глаза, лапы, хвост;
#   Folded — сложенные крылья: видны, пока сидит;
#   WingL, WingR — раскрытые крылья, опора — в плече: в полёте машут поворотом вокруг оси тела.
# Сидит клювом вперёд (−Y), опора — лапы (z = 0). Длина от клюва до хвоста 38 см.
#
# Запуск: blender -b --python raven.py -- [--preview <папка>]
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[4]
OUT = ROOT / "Assets" / "Resources" / "Props" / "Raven.fbx"

FEATHER, BEAK, EYE, LEG = 0, 1, 2, 3
MATERIALS = [
    ("Raven Feather", (0.026, 0.028, 0.040, 1.0), 0.42),   # чёрный с синим отливом
    ("Raven Beak", (0.050, 0.048, 0.052, 1.0), 0.30),
    ("Raven Eye", (0.030, 0.018, 0.012, 1.0), 0.08),
    ("Raven Leg", (0.060, 0.056, 0.056, 1.0), 0.65),
]

# Наклон тела сидящего: в полёте птицу клонят носом вниз на столько же, и крылья,
# построенные с этим наклоном, ложатся горизонтально.
FLIGHT_PITCH = 25.0


def wipe():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def materials(mesh):
    for name, rgba, rough in MATERIALS:
        m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        m.diffuse_color = rgba
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if bsdf is not None:
            bsdf.inputs["Base Color"].default_value = rgba
            bsdf.inputs["Roughness"].default_value = rough
        mesh.materials.append(m)


def loft(bm, rings, mat, seg=12, cap=(True, True), squash=None):
    """
    Трубка по сечениям: каждое — (центр, rx поперёк, rz вверх). Сечение — эллипс поперёк
    касательной к оси; ось X мира — «вбок». squash(i, a, point) может сдвинуть точку кольца.
    """
    centres = [Vector(c) for c, _, _ in rings]
    loops = []
    for i, (c, rx, rz) in enumerate(rings):
        c = Vector(c)
        a = centres[max(0, i - 1)]
        b = centres[min(len(centres) - 1, i + 1)]
        t = (b - a).normalized()
        side = Vector((1.0, 0.0, 0.0))
        side = (side - t * side.dot(t)).normalized()
        up = side.cross(t).normalized()
        if up.z < 0:
            up = -up
        ring = []
        for k in range(seg):
            ang = 2.0 * math.pi * k / seg
            p = c + side * (rx * math.cos(ang)) + up * (rz * math.sin(ang))
            if squash is not None:
                p = squash(i, ang, p)
            ring.append(bm.verts.new(p))
        loops.append(ring)

    for i in range(len(loops) - 1):
        r0, r1 = loops[i], loops[i + 1]
        for k in range(seg):
            f = bm.faces.new((r0[k], r0[(k + 1) % seg], r1[(k + 1) % seg], r1[k]))
            f.material_index = mat
    if cap[0]:
        tip = bm.verts.new(centres[0] + (centres[0] - centres[1]).normalized() * 0.002)
        for k in range(seg):
            f = bm.faces.new((loops[0][(k + 1) % seg], loops[0][k], tip))
            f.material_index = mat
    if cap[1]:
        tip = bm.verts.new(centres[-1] + (centres[-1] - centres[-2]).normalized() * 0.002)
        for k in range(seg):
            f = bm.faces.new((loops[-1][k], loops[-1][(k + 1) % seg], tip))
            f.material_index = mat


def plate(bm, outline, mat, lift=None):
    """Плоская пластина по контуру (x, y) на высоте lift(x, y) — перо, хвост; толщину даёт solidify."""
    verts = []
    for x, y in outline:
        z = lift(x, y) if lift else 0.0
        verts.append(bm.verts.new((x, y, z)))
    f = bm.faces.new(verts)
    f.material_index = mat
    bmesh.ops.triangulate(bm, faces=[f])


def finish(name, bm, subsurf=0, solidify=0.0, location=(0, 0, 0), rotation=(0, 0, 0)):
    mesh = bpy.data.meshes.new(name)
    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()
    materials(mesh)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    if solidify > 0.0:
        m = obj.modifiers.new("Толщина", "SOLIDIFY")
        m.thickness = solidify
        m.offset = 0.0
    if subsurf > 0:
        m = obj.modifiers.new("Гладкость", "SUBSURF")
        m.levels = subsurf
        m.render_levels = subsurf
    if obj.modifiers:
        dg = bpy.context.evaluated_depsgraph_get()
        baked = bpy.data.meshes.new_from_object(obj.evaluated_get(dg))
        obj.modifiers.clear()
        old = obj.data
        obj.data = baked
        bpy.data.meshes.remove(old)
        if len(obj.data.materials) == 0:
            materials(obj.data)
    for p in obj.data.polygons:
        p.use_smooth = True
    obj.location = location
    obj.rotation_euler = rotation
    return obj


# ---------------------------------------------------------------- туловище

def body():
    bm = bmesh.new()

    # Ось: от гузки (сзади, внизу) к шее и голове; тело сидящего поднято к голове на ~40°.
    spine = [
        ((0.0, 0.092, 0.084), 0.022, 0.020),
        ((0.0, 0.070, 0.096), 0.042, 0.040),
        ((0.0, 0.040, 0.110), 0.056, 0.056),
        ((0.0, 0.005, 0.127), 0.062, 0.064),
        ((0.0, -0.028, 0.150), 0.058, 0.060),
        ((0.0, -0.050, 0.178), 0.047, 0.048),
        ((0.0, -0.062, 0.204), 0.036, 0.038),   # шея — «борода» внизу (squash)
        ((0.0, -0.068, 0.229), 0.034, 0.035),
        ((0.0, -0.086, 0.240), 0.033, 0.031),
        ((0.0, -0.104, 0.239), 0.025, 0.023),   # крутой лоб
        ((0.0, -0.113, 0.236), 0.016, 0.017),
    ]

    def beard(i, ang, p):
        # Перья горла торчат вниз и вперёд — у ворона это видно издали.
        if i in (5, 6, 7) and math.sin(ang) < -0.3:
            k = (-math.sin(ang) - 0.3) / 0.7
            p = p + Vector((0.0, -0.010, -0.012)) * k * (1.0 if i == 6 else 0.6)
        return p

    loft(bm, spine, FEATHER, seg=12, squash=beard)

    # Клюв: дуга надклювья книзу к концу, подклювье прямое.
    loft(bm, [
        ((0.0, -0.110, 0.236), 0.013, 0.016),
        ((0.0, -0.128, 0.235), 0.010, 0.0125),
        ((0.0, -0.146, 0.231), 0.0068, 0.0088),
        ((0.0, -0.160, 0.2245), 0.0034, 0.0050),
        ((0.0, -0.168, 0.2185), 0.0010, 0.0014),
    ], BEAK, seg=10, cap=(False, True))

    # Лапы: цевка и пальцы — три вперёд, один назад; цепко, как на плече.
    for s in (-1.0, 1.0):
        x = s * 0.019
        loft(bm, [((x, 0.012, 0.090), 0.0062, 0.0062), ((x * 1.05, 0.004, 0.050), 0.0055, 0.0055),
                  ((x * 1.08, -0.004, 0.012), 0.0050, 0.0050)], LEG, seg=6)
        foot = Vector((x * 1.08, -0.004, 0.007))
        for ang, length in ((-22.0, 0.036), (0.0, 0.040), (22.0, 0.036), (180.0, 0.026)):
            a = math.radians(ang)
            d = Vector((math.sin(a) * s * 0.9, -math.cos(a), 0.0)).normalized()
            loft(bm, [(foot, 0.0036, 0.0034), (foot + d * length * 0.6, 0.0031, 0.0030),
                      (foot + d * length + Vector((0, 0, -0.003)), 0.0016, 0.0016)], LEG, seg=6)

    obj = finish("Body", bm, subsurf=2)

    # Глаза и хвост — отдельно от гладкости: им она не к лицу; потом в одно тело.
    eyes = bmesh.new()
    for s in (-1.0, 1.0):
        r = bmesh.ops.create_uvsphere(eyes, u_segments=10, v_segments=7, radius=0.0062)
        for v in r["verts"]:
            v.co += Vector((s * 0.0265, -0.094, 0.247))
        for f in eyes.faces:
            f.material_index = EYE
    eye_obj = finish("Eyes", eyes)

    tail = bmesh.new()
    # Клин: средние перья длиннее крайних — хвост ворона (у вороны он прямой).
    outline = [(-0.024, 0.0), (-0.038, 0.055), (-0.046, 0.108), (-0.020, 0.135), (0.0, 0.152),
               (0.020, 0.135), (0.046, 0.108), (0.038, 0.055), (0.024, 0.0)]
    # Перья хвоста лежат домиком: середина выше краёв, к концу хвост чуть провисает.
    plate(tail, outline, FEATHER, lift=lambda x, y: -0.012 * (y / 0.15) ** 2 + 0.008 * (1 - abs(x) / 0.046))
    tail_obj = finish("Tail", tail, solidify=0.011, location=(0.0, 0.086, 0.090), rotation=(math.radians(-30.0), 0.0, 0.0))

    join(obj, [eye_obj, tail_obj])
    return obj


def join(target, others):
    """Слить части в одно тело — без операторов, через bmesh: фоном и без окна."""
    bm = bmesh.new()
    bm.from_mesh(target.data)
    for o in others:
        o.data.transform(o.matrix_basis)
        bm.from_mesh(o.data)
        bpy.data.objects.remove(o)
    bm.to_mesh(target.data)
    bm.free()
    for p in target.data.polygons:
        p.use_smooth = True


# ---------------------------------------------------------------- крылья

def folded():
    """Сложенные крылья вдоль боков: концы маховых сходятся над хвостом."""
    bm = bmesh.new()
    for s in (-1.0, 1.0):
        loft(bm, [
            ((s * 0.047, -0.048, 0.176), 0.010, 0.030),
            ((s * 0.056, -0.010, 0.150), 0.012, 0.038),
            ((s * 0.052, 0.040, 0.124), 0.011, 0.034),
            ((s * 0.036, 0.100, 0.098), 0.008, 0.022),
            ((s * 0.016, 0.150, 0.076), 0.003, 0.006),
        ], FEATHER, seg=10)
    return finish("Folded", bm, subsurf=2)


def spread(name, side):
    """
    Раскрытое крыло от плеча (опора — в плече): плечо и кисть — одна пластина, на конце — пять
    разведённых маховых, «пальцы» ворона. Строится в полёте горизонтально, а в модели наклонено
    с телом сидящего (FLIGHT_PITCH).
    """
    bm = bmesh.new()
    arm = [(0.000, -0.016), (0.060, -0.030), (0.140, -0.040), (0.190, -0.034), (0.235, -0.018),
           (0.245, 0.030), (0.225, 0.062), (0.160, 0.088), (0.080, 0.094), (0.000, 0.080)]
    camber = lambda x, y: 0.010 * math.sin(math.pi * min(1.0, max(0.0, (y + 0.04) / 0.13))) * (1.0 - x / 0.5)
    plate(bm, [(side * x, y) for x, y in (arm if side > 0 else list(reversed(arm)))], FEATHER,
          lift=lambda x, y: camber(abs(x), y))

    # Пять маховых: веером от кисти, крайнее — вперёд, последнее — назад.
    for i, ang in enumerate((-10.0, -1.0, 8.0, 17.0, 27.0)):
        a = math.radians(ang)
        base = Vector((0.225 + 0.004 * i, -0.012 + 0.016 * i, 0.0))
        d = Vector((math.cos(a), math.sin(a), 0.0))
        n = Vector((-d.y, d.x, 0.0))
        length = (0.150, 0.165, 0.160, 0.145, 0.125)[i]
        tip = base + d * length
        pts = [base - n * 0.011, base + d * length * 0.55 - n * 0.010, tip - n * 0.003,
               tip + d * 0.006, tip + n * 0.003, base + d * length * 0.55 + n * 0.010, base + n * 0.011]
        if side < 0:
            pts = [Vector((-p.x, p.y, p.z)) for p in reversed(pts)]
        plate(bm, [(p.x, p.y) for p in pts], FEATHER, lift=lambda x, y: -0.004 * ((abs(x) - 0.22) / 0.17) ** 2)

    return finish(name, bm, solidify=0.005, location=(side * 0.046, -0.040, 0.170),
                  rotation=(math.radians(-FLIGHT_PITCH), 0.0, 0.0))


# ---------------------------------------------------------------- выгрузка и кадр

def export(path):
    path.parent.mkdir(parents=True, exist_ok=True)
    want = dict(
        filepath=str(path), use_selection=False, apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_NONE", global_scale=1.0, object_types={"MESH"},
        use_mesh_modifiers=False, mesh_smooth_type="FACE", bake_anim=False,
        axis_forward="-Z", axis_up="Y", path_mode="COPY", embed_textures=False,
    )
    allowed = set(bpy.ops.export_scene.fbx.get_rna_type().properties.keys())
    bpy.ops.export_scene.fbx(**{k: v for k, v in want.items() if k in allowed})


def shot(folder, name, objects, pitch=0.0, view=(0.75, -1.0, 0.45)):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.color_type = "MATERIAL"
    sc.display.shading.light = "STUDIO"
    sc.display.shading.show_shadows = True
    sc.display.shading.show_specular_highlight = True
    sc.render.resolution_x = 640
    sc.render.resolution_y = 480
    sc.world = bpy.data.worlds.new("W")
    sc.world.color = (0.55, 0.56, 0.60)

    pose = bpy.data.objects.new("Поза", None)
    sc.collection.objects.link(pose)
    pose.rotation_euler = (math.radians(pitch), 0.0, 0.0)
    for o in bpy.data.objects:
        if o.type == "MESH":
            o.hide_render = o.name not in objects
            o.parent = pose

    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 0.95 if pitch else 0.50
    sc.collection.objects.link(cam)
    sc.camera = cam
    mid = Vector((0.0, 0.0, 0.14))
    cam.location = mid + Vector(view)
    cam.rotation_euler = (mid - cam.location).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = str(Path(folder) / (name + ".png"))
    bpy.ops.render.render(write_still=True)

    for o in bpy.data.objects:
        if o.type == "MESH":
            o.parent = None
            o.hide_render = False
    bpy.data.objects.remove(cam)
    bpy.data.objects.remove(pose)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    shots = argv[argv.index("--preview") + 1] if "--preview" in argv else None

    wipe()
    parts = [body(), folded(), spread("WingL", 1.0), spread("WingR", -1.0)]
    for p in parts:
        print("[ВОРОН] {}: граней {}".format(p.name, len(p.data.polygons)))
    export(OUT)
    print("[ВОРОН] записано: " + str(OUT))

    if shots:
        shot(shots, "ворон — сидит", {"Body", "Folded"})
        shot(shots, "ворон — сидит, профиль", {"Body", "Folded"}, view=(1.0, 0.0, 0.12))
        shot(shots, "ворон — летит", {"Body", "WingL", "WingR"}, pitch=FLIGHT_PITCH, view=(0.55, -0.6, 0.9))
        shot(shots, "ворон — летит, сверху", {"Body", "WingL", "WingR"}, pitch=FLIGHT_PITCH, view=(0.0, 0.05, 1.2))


if __name__ == "__main__":
    main()
