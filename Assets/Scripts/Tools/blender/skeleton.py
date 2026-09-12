# Assets/Scripts/Tools/blender/skeleton.py
"""
Скелет: тело, арматура и три движения — одним скриптом.

Техзадание в docs/14-HANDOFF.md §12. Смысл затеи тот же, на котором
стоит весь проект: модель, собранная скриптом, воспроизводима
и параметризована. Один и тот же запуск даёт один и тот же файл,
правку видно диффом, а стиль одинаков по построению, а не по подбору
глазом.

Запуск:

    blender --background --python Tools/blender/skeleton.py
    blender --background --python Tools/blender/skeleton.py -- --preview <папка>

Выдаёт Assets/Resources/Bodies/Skeleton.fbx — ровно то, что ищет
WarriorLook.Build.

Четыре условия из техзадания, и каждое здесь исполнено явно:

* опора у ног, в нуле — ступни лежат на z = 0, объекты в начале координат;
* рост около 1.0 — макушка почти на единице, потому что код умножает
  модель на свой рост: 1.2 рядовому, 1.5 опытному;
* лицом в +Z по-юнитевски — в Блендере это -Y, и при экспорте
  осями (-Z вперёд, Y вверх) одно переходит в другое;
* арматура humanoid — двадцать одна кость, все пятнадцать обязательных
  Unity на месте, имена общепринятые, поза привязки T-образная.

Ни одного жребия: ни Random, ни времени, ни порядка словаря, от которого
зависел бы результат. Это то же правило, что и в игре (CLAUDE.md):
один и тот же запуск даёт одну и ту же модель.

Оговорка, проверенная замером, а не предположенная: **сам файл побайтно
не повторяется.** Блендер пишет в FBX время создания и внутренние
идентификаторы объектов, и они меняются от запуска к запуску
(PYTHONHASHSEED=0 не помогает — проверено). Геометрия, веса и ключи
при этом те же. Отсюда правило: перезапускать скрипт стоит тогда,
когда модель действительно изменилась, — иначе в историю ляжет
полмегабайта шума, в котором настоящая правка не видна.
"""

import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector, Quaternion

# ---------------------------------------------------------------- размеры

FPS = 30

# Ключевые высоты. Пропорции человеческие, а не героические: воин
# в полтора роста получится из этого умножением, а обратно — нет.
Z_ANKLE = 0.055
Z_KNEE = 0.275
Z_HIP = 0.500
Z_WAIST = 0.560
Z_CHEST = 0.665
Z_SHOULDER = 0.775
Z_NECK = 0.795
Z_SKULL = 0.905

X_LEG = 0.075    # полуширина таза: на столько разведены ноги
X_SHOULDER = 0.100

BONE = (0.855, 0.830, 0.760, 1.0)   # цвет кости
DARK = (0.035, 0.030, 0.032, 1.0)   # то, что видно сквозь глазницы

# --------------------------------------------------------------- арматура

# (имя, родитель, голова, хвост). Имена общепринятые: Unity сопоставляет
# humanoid по именам и иерархии, и «имена не важны» верно только до тех
# пор, пока кто-нибудь не откроет окно настройки аватара.
BONES = [
    ("Hips",          None,        (0, 0, Z_HIP),            (0, 0, Z_WAIST)),
    ("Spine",         "Hips",      (0, 0, Z_WAIST),          (0, 0, Z_CHEST)),
    ("Chest",         "Spine",     (0, 0, Z_CHEST),          (0, 0, Z_NECK)),
    ("Neck",          "Chest",     (0, 0, Z_NECK),           (0, 0, Z_SKULL - 0.055)),
    ("Head",          "Neck",      (0, 0, Z_SKULL - 0.055),  (0, 0, 0.99)),

    ("LeftShoulder",  "Chest",        (0.02, 0, Z_SHOULDER),       (X_SHOULDER, 0, Z_SHOULDER)),
    ("LeftUpperArm",  "LeftShoulder", (X_SHOULDER, 0, Z_SHOULDER), (0.255, 0, Z_SHOULDER)),
    ("LeftLowerArm",  "LeftUpperArm", (0.255, 0, Z_SHOULDER),      (0.405, 0, Z_SHOULDER)),
    ("LeftHand",      "LeftLowerArm", (0.405, 0, Z_SHOULDER),      (0.480, 0, Z_SHOULDER)),

    ("RightShoulder", "Chest",         (-0.02, 0, Z_SHOULDER),       (-X_SHOULDER, 0, Z_SHOULDER)),
    ("RightUpperArm", "RightShoulder", (-X_SHOULDER, 0, Z_SHOULDER), (-0.255, 0, Z_SHOULDER)),
    ("RightLowerArm", "RightUpperArm", (-0.255, 0, Z_SHOULDER),      (-0.405, 0, Z_SHOULDER)),
    ("RightHand",     "RightLowerArm", (-0.405, 0, Z_SHOULDER),      (-0.480, 0, Z_SHOULDER)),

    ("LeftUpperLeg",  "Hips",         (X_LEG, 0, Z_HIP),        (X_LEG, 0, Z_KNEE)),
    ("LeftLowerLeg",  "LeftUpperLeg", (X_LEG, 0, Z_KNEE),       (X_LEG, 0, Z_ANKLE)),
    ("LeftFoot",      "LeftLowerLeg", (X_LEG, 0, Z_ANKLE),      (X_LEG, -0.070, 0.012)),
    ("LeftToes",      "LeftFoot",     (X_LEG, -0.070, 0.012),   (X_LEG, -0.115, 0.012)),

    ("RightUpperLeg", "Hips",          (-X_LEG, 0, Z_HIP),       (-X_LEG, 0, Z_KNEE)),
    ("RightLowerLeg", "RightUpperLeg", (-X_LEG, 0, Z_KNEE),      (-X_LEG, 0, Z_ANKLE)),
    ("RightFoot",     "RightLowerLeg", (-X_LEG, 0, Z_ANKLE),     (-X_LEG, -0.070, 0.012)),
    ("RightToes",     "RightFoot",     (-X_LEG, -0.070, 0.012),  (-X_LEG, -0.115, 0.012)),
]


def wipe():
    """Пустая сцена. Стартовый файл Блендера несёт куб, лампу и камеру."""
    for block in (bpy.data.objects, bpy.data.meshes, bpy.data.armatures,
                  bpy.data.actions, bpy.data.materials, bpy.data.cameras,
                  bpy.data.lights):
        for item in list(block):
            block.remove(item)


def build_armature():
    data = bpy.data.armatures.new("Skeleton")
    obj = bpy.data.objects.new("Skeleton", data)
    bpy.context.scene.collection.objects.link(obj)

    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")

    made = {}
    for name, parent, head, tail in BONES:
        eb = data.edit_bones.new(name)
        eb.head = Vector(head)
        eb.tail = Vector(tail)
        eb.use_connect = False
        if parent is not None:
            eb.parent = made[parent]
        made[name] = eb

    bpy.ops.object.mode_set(mode="OBJECT")
    return obj


# ------------------------------------------------------------- геометрия

class Body:
    """
    Копилка треугольников и четырёхугольников.

    Сделано без bpy.ops нарочно: операторы в фоновом режиме зависят
    от контекста, а здесь всё считается числами и потому одинаково
    на любой машине. Заодно каждая деталь сразу привязана к своей кости —
    жёстко, весом 1. Для скелета это не упрощение, а правда: кость
    не гнётся.
    """

    def __init__(self):
        self.verts = []
        self.faces = []
        self.mats = []
        self.groups = {}

    def add(self, verts, faces, bone, mat=0):
        base = len(self.verts)
        self.verts.extend(verts)
        for f in faces:
            self.faces.append([i + base for i in f])
            self.mats.append(mat)
        self.groups.setdefault(bone, []).extend(range(base, base + len(verts)))


def box(center, size):
    cx, cy, cz = center
    sx, sy, sz = (s * 0.5 for s in size)
    v = [(cx - sx, cy - sy, cz - sz), (cx + sx, cy - sy, cz - sz),
         (cx + sx, cy + sy, cz - sz), (cx - sx, cy + sy, cz - sz),
         (cx - sx, cy - sy, cz + sz), (cx + sx, cy - sy, cz + sz),
         (cx + sx, cy + sy, cz + sz), (cx - sx, cy + sy, cz + sz)]
    f = [[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4],
         [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]]
    return v, f


def sphere(center, r, scale=(1, 1, 1), segs=20, rings=12, holes=()):
    """
    Шар, из которого можно вырезать конусы. Глазницы делаются именно
    так — пропуском граней, а не булевой операцией: результат тот же,
    а зависимости от солвера нет.
    """
    cx, cy, cz = center
    dirs = []
    verts = []
    for i in range(rings + 1):
        th = math.pi * i / rings
        for j in range(segs):
            ph = 2 * math.pi * j / segs
            d = (math.sin(th) * math.cos(ph),
                 math.sin(th) * math.sin(ph),
                 math.cos(th))
            dirs.append(d)
            verts.append((cx + d[0] * r * scale[0],
                          cy + d[1] * r * scale[1],
                          cz + d[2] * r * scale[2]))

    cut = []
    for axis, deg in holes:
        a = Vector(axis)
        a.normalize()
        cut.append((a, math.cos(math.radians(deg))))

    def keep(idx):
        if not cut:
            return True
        n = Vector((0.0, 0.0, 0.0))
        for i in idx:
            n = n + Vector(dirs[i])
        n.normalize()
        for a, limit in cut:
            if n.dot(a) > limit:
                return False
        return True

    faces = []
    for i in range(rings):
        for j in range(segs):
            a = i * segs + j
            b = i * segs + (j + 1) % segs
            c = (i + 1) * segs + (j + 1) % segs
            d = (i + 1) * segs + j
            if i == 0:
                face = [a, c, d]
            elif i == rings - 1:
                face = [a, b, c]
            else:
                face = [a, b, c, d]
            if keep(face):
                faces.append(face)
    return verts, faces


def tube(p0, p1, r0, r1, segs=8):
    """Кость между двумя точками. Слегка сужается — так читается лучше."""
    a = Vector(p0)
    b = Vector(p1)
    axis = b - a
    length = axis.length
    if length < 1e-6:
        return [], []
    axis = axis / length

    ref = Vector((0.0, 0.0, 1.0))
    if abs(axis.dot(ref)) > 0.95:
        ref = Vector((1.0, 0.0, 0.0))
    u = axis.cross(ref)
    u.normalize()
    w = axis.cross(u)

    verts = []
    for r, p in ((r0, a), (r1, b)):
        for j in range(segs):
            ph = 2 * math.pi * j / segs
            verts.append(tuple(p + u * (math.cos(ph) * r) + w * (math.sin(ph) * r)))

    faces = []
    for j in range(segs):
        k = (j + 1) % segs
        faces.append([j, k, segs + k, segs + j])
    faces.append(list(range(segs - 1, -1, -1)))
    faces.append(list(range(segs, segs * 2)))
    return verts, faces


def ring(center, major, minor, scale=(1, 1, 1), segs=22, rsegs=6):
    """Ребро, обод таза. Кольцо в плоскости XY, сплющенное по вкусу."""
    cx, cy, cz = center
    verts = []
    for i in range(segs):
        ph = 2 * math.pi * i / segs
        dx, dy = math.cos(ph), math.sin(ph)
        for j in range(rsegs):
            th = 2 * math.pi * j / rsegs
            rr = major + minor * math.cos(th)
            verts.append((cx + dx * rr * scale[0],
                          cy + dy * rr * scale[1],
                          cz + minor * math.sin(th) * scale[2]))
    faces = []
    for i in range(segs):
        n = (i + 1) % segs
        for j in range(rsegs):
            m = (j + 1) % rsegs
            faces.append([i * rsegs + j, n * rsegs + j,
                          n * rsegs + m, i * rsegs + m])
    return verts, faces


def limb(body, bone, p0, p1, r0, r1, joint=0.0):
    body.add(*tube(p0, p1, r0, r1), bone=bone)
    if joint > 0.0:
        body.add(*sphere(p0, joint, segs=10, rings=6), bone=bone)


def build_mesh(arm):
    b = Body()

    # --- таз. Обод, а не куб: по нему сверху и узнаётся скелет.
    b.add(*ring((0, 0.004, Z_HIP - 0.012), 0.078, 0.019, scale=(1.0, 0.78, 1.1)), bone="Hips")
    for side in (1, -1):
        b.add(*box((side * 0.072, 0.004, Z_HIP + 0.018), (0.030, 0.062, 0.055)), bone="Hips")
    b.add(*box((0, 0.045, Z_HIP + 0.030), (0.034, 0.026, 0.070)), bone="Hips")

    # --- позвоночник: позвонки видны отдельными шайбами
    for i in range(3):
        z = Z_WAIST - 0.005 + i * 0.036
        b.add(*box((0, 0.040, z), (0.036, 0.032, 0.024)), bone="Spine")

    # --- грудная клетка. Пять рёбер, расширяясь кверху, плюс грудина.
    ribs = [(0.060, 0.673), (0.070, 0.700), (0.077, 0.726),
            (0.079, 0.750), (0.073, 0.771)]
    for major, z in ribs:
        b.add(*ring((0, 0.006, z), major, 0.0105, scale=(1.0, 0.80, 1.25)), bone="Chest")
    b.add(*box((0, -0.052, 0.722), (0.030, 0.016, 0.098)), bone="Chest")
    for i in range(4):
        b.add(*box((0, 0.048, 0.676 + i * 0.030), (0.036, 0.030, 0.022)), bone="Chest")

    # --- шея
    for i in range(2):
        b.add(*box((0, 0.008, Z_NECK + 0.012 + i * 0.024), (0.032, 0.032, 0.020)), bone="Neck")

    # --- череп. Глазницы и носовое отверстие — вырезы, а внутри тёмный
    #     шар: сквозь дыры видно пустоту, и это единственное, что
    #     превращает шар в череп.
    b.add(*sphere((0, 0.014, Z_SKULL), 0.067, scale=(1.0, 1.18, 1.06),
                  segs=44, rings=26,
                  holes=[((0.46, -0.86, 0.04), 13.0),
                         ((-0.46, -0.86, 0.04), 13.0),
                         ((0.0, -1.0, -0.30), 7.0)]), bone="Head")
    b.add(*sphere((0, 0.014, Z_SKULL), 0.062, scale=(1.0, 1.18, 1.06),
                  segs=24, rings=14), bone="Head", mat=1)
    # челюсть
    b.add(*box((0, -0.016, Z_SKULL - 0.058), (0.080, 0.082, 0.026)), bone="Head")
    b.add(*box((0, -0.052, Z_SKULL - 0.044), (0.064, 0.016, 0.018)), bone="Head")

    # --- руки. Предплечье из двух костей: лучевая и локтевая читаются
    #     как скелет вернее любой детали.
    for side, tag in ((1, "Left"), (-1, "Right")):
        sx = side
        b.add(*box((sx * 0.058, 0.010, Z_SHOULDER + 0.010), (0.082, 0.048, 0.019)),
              bone=tag + "Shoulder")

        limb(b, tag + "UpperArm",
             (sx * X_SHOULDER, 0, Z_SHOULDER), (sx * 0.255, 0, Z_SHOULDER),
             0.020, 0.016, joint=0.024)

        for off in (-0.014, 0.014):
            limb(b, tag + "LowerArm",
                 (sx * 0.255, off, Z_SHOULDER), (sx * 0.400, off * 0.4, Z_SHOULDER),
                 0.0105, 0.0090)
        b.add(*sphere((sx * 0.255, 0, Z_SHOULDER), 0.021, segs=10, rings=6),
              bone=tag + "LowerArm")

        b.add(*box((sx * 0.425, 0, Z_SHOULDER), (0.044, 0.046, 0.018)), bone=tag + "Hand")
        for k in (-1, 0, 1):
            b.add(*tube((sx * 0.447, k * 0.016, Z_SHOULDER),
                        (sx * 0.482, k * 0.018, Z_SHOULDER), 0.006, 0.005, segs=6),
                  bone=tag + "Hand")

        # --- ноги
        limb(b, tag + "UpperLeg",
             (sx * X_LEG, 0, Z_HIP), (sx * X_LEG, 0, Z_KNEE),
             0.026, 0.020, joint=0.030)

        for off in (-0.013, 0.013):
            limb(b, tag + "LowerLeg",
                 (sx * X_LEG + off, 0, Z_KNEE), (sx * X_LEG + off * 0.5, 0, Z_ANKLE),
                 0.0135, 0.0105)
        b.add(*sphere((sx * X_LEG, 0, Z_KNEE), 0.026, segs=10, rings=6),
              bone=tag + "LowerLeg")

        b.add(*box((sx * X_LEG, -0.030, 0.022), (0.048, 0.090, 0.036)), bone=tag + "Foot")
        b.add(*box((sx * X_LEG, -0.098, 0.012), (0.046, 0.052, 0.020)), bone=tag + "Toes")

    # ---- в настоящий меш
    mesh = bpy.data.meshes.new("SkeletonMesh")
    mesh.from_pydata(b.verts, [], b.faces)
    mesh.validate(verbose=False)

    for name, rgba in (("Bone", BONE), ("Socket", DARK)):
        m = bpy.data.materials.new(name)
        m.diffuse_color = rgba
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if bsdf is not None:
            bsdf.inputs["Base Color"].default_value = rgba
            bsdf.inputs["Roughness"].default_value = 0.62
        mesh.materials.append(m)

    for i, mat in enumerate(b.mats):
        if i < len(mesh.polygons):
            mesh.polygons[i].material_index = mat

    obj = bpy.data.objects.new("SkeletonMesh", mesh)
    bpy.context.scene.collection.objects.link(obj)

    for bone, idx in b.groups.items():
        vg = obj.vertex_groups.new(name=bone)
        vg.add(idx, 1.0, "REPLACE")

    obj.parent = arm
    mod = obj.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    mod.use_vertex_groups = True

    return obj


# ------------------------------------------------------------- движения

X = (1.0, 0.0, 0.0)
Y = (0.0, 1.0, 0.0)
Z = (0.0, 0.0, 1.0)


def turn(pb, pairs):
    """
    Поворот кости вокруг мировых осей, а не вокруг её собственных.

    Иначе пришлось бы держать в голове крен каждой кости, а он у Блендера
    выводится сам и меняется от мелкой правки головы или хвоста. Здесь
    ось задаётся как в мире и переносится в базис кости матрицей покоя.
    Пары перечисляются изнутри наружу.
    """
    m = pb.bone.matrix_local.to_3x3()
    mi = m.inverted()
    q = Quaternion((1.0, 0.0, 0.0, 0.0))
    for axis, deg in pairs:
        a = mi @ Vector(axis)
        a.normalize()
        q = Quaternion(a, math.radians(deg)) @ q
    pb.rotation_mode = "QUATERNION"
    pb.rotation_quaternion = q


def stance(legs, arms, torso, bob):
    """
    Одна поза, собранная по-человечески.

    legs  = (бедро Л, колено Л, стопа Л, бедро П, колено П, стопа П)
    arms  = (насколько опущены, взмах Л, взмах П, локоть)
    torso = (поясница, грудь, кивок, поворот головы)

    Знаки геометрические, а не условные: поворот вокруг мирового X
    уводит хвост кости в +Y, то есть назад — потому у ноги, смотрящей
    вниз, плюс это шаг назад, а у позвоночника, смотрящего вверх, плюс
    это наклон вперёд. Это не путаница, это одна и та же формула.
    """
    lh, lk, lf, rh, rk, rf = legs
    down, la, ra, elbow = arms
    waist, chest, nod, look = torso

    return {
        "Hips": ([], bob),
        "Spine": ([(X, waist)], None),
        "Chest": ([(X, chest)], None),
        "Neck": ([(X, nod * 0.4)], None),
        "Head": ([(X, nod), (Z, look)], None),

        "LeftUpperArm": ([(Y, down), (X, la)], None),
        "RightUpperArm": ([(Y, -down), (X, ra)], None),
        "LeftLowerArm": ([(Z, -elbow)], None),
        "RightLowerArm": ([(Z, elbow)], None),

        "LeftUpperLeg": ([(X, lh)], None),
        "LeftLowerLeg": ([(X, lk)], None),
        "LeftFoot": ([(X, lf)], None),
        "RightUpperLeg": ([(X, rh)], None),
        "RightLowerLeg": ([(X, rk)], None),
        "RightFoot": ([(X, rf)], None),
    }


def action(arm, name, keys):
    act = bpy.data.actions.new(name)
    act.use_fake_user = True

    if arm.animation_data is None:
        arm.animation_data_create()
    arm.animation_data.action = act

    slots = getattr(act, "slots", None)
    if slots is not None and hasattr(arm.animation_data, "action_slot"):
        try:
            slot = slots[0] if len(slots) else slots.new(id_type="OBJECT", name="Object")
            arm.animation_data.action_slot = slot
        except Exception as err:
            print("[СКЕЛЕТ] слот действия не задан: " + str(err))

    for frame, pose in keys:
        for bone in sorted(pose):
            pairs, bob = pose[bone]
            pb = arm.pose.bones[bone]
            turn(pb, pairs)
            if bob is not None:
                # Локальный Y кости таза смотрит вверх: она сама
                # направлена вверх. Подскок пишем туда, а не в мировые
                # координаты — иначе он не наследуется позой.
                pb.location = (0.0, bob, 0.0)
                pb.keyframe_insert("location", frame=frame)
            pb.keyframe_insert("rotation_quaternion", frame=frame)

    return act


def make_actions(arm):
    # --- Idle: стоит. Скелету дышать нечем, поэтому покой — это
    #     медленный перенос веса и поворот головы, а не вздохи грудью.
    idle = [
        (1,  stance((-2, 4, -1, 2, 4, 1), (78, 1, -1, 12), (2, 1, 1, 0), 0.0)),
        (20, stance((-3, 5, -1, 3, 5, 1), (79, 2, -2, 13), (3, 2, 2, 13), -0.006)),
        (40, stance((-1, 4, -1, 1, 4, 1), (77, 0, 0, 11), (2, 1, 0, -9), 0.004)),
        (61, stance((-2, 4, -1, 2, 4, 1), (78, 1, -1, 12), (2, 1, 1, 0), 0.0)),
    ]

    # --- Walk: шаг. Тридцать два кадра — чуть больше секунды, как
    #     у человека, и на это опирается ощущение скорости отряда.
    walk = [
        (1,  stance((-25, 6, -6, 22, 22, 10), (76, 16, -16, 20), (4, 2, -2, 0), 0.0)),
        (9,  stance((-4, 2, 0, -2, 34, -10), (76, 0, 0, 18), (4, 2, -2, 0), 0.018)),
        (17, stance((22, 22, 10, -25, 6, -6), (76, -16, 16, 20), (4, 2, -2, 0), 0.0)),
        (25, stance((-2, 34, -10, -4, 2, 0), (76, 0, 0, 18), (4, 2, -2, 0), 0.018)),
        (33, stance((-25, 6, -6, 22, 22, 10), (76, 16, -16, 20), (4, 2, -2, 0), 0.0)),
    ]

    # --- Flee: бежит прочь. Ради этого клипа всё и затевалось: подпись
    #     «Сбегает» обязана стать видимой. Отличий от шага три, и все
    #     крупные — наклон вперёд, длинный шаг и высоко поднятые руки.
    #     Мелкая разница здесь была бы хуже отсутствия: игрок сверху
    #     не отличит быструю ходьбу от бегства.
    flee = [
        (1,  stance((-44, 12, -12, 36, 58, 16), (48, 34, -34, 66), (17, 9, -13, 0), 0.0)),
        (6,  stance((-10, 6, -4, -18, 78, -16), (48, 4, -4, 62), (17, 9, -13, 0), 0.034)),
        (11, stance((36, 58, 16, -44, 12, -12), (48, -34, 34, 66), (17, 9, -13, 0), 0.0)),
        (16, stance((-18, 78, -16, -10, 6, -4), (48, -4, 4, 62), (17, 9, -13, 0), 0.034)),
        (21, stance((-44, 12, -12, 36, 58, 16), (48, 34, -34, 66), (17, 9, -13, 0), 0.0)),
    ]

    # Имена обязаны совпасть буква в букву с BodyMotion: Animator.Play
    # по чужому имени молча ничего не делает — худший вид поломки,
    # потому что выглядит как «анимация просто не сделана».
    return [action(arm, "Idle", idle),
            action(arm, "Walk", walk),
            action(arm, "Flee", flee)]


# --------------------------------------------------------------- вывод

def unity_root():
    """Корень проекта Unity — там, где лежит Assets."""
    here = Path(__file__).resolve()
    for up in here.parents:
        if (up / "Assets").is_dir():
            return up
    # Из облачной копии, где над Assets/Scripts ничего нет, это всё
    # равно не запустить: Блендера там нет. Но молчать нельзя.
    raise SystemExit("Папки Assets над скриптом нет: некуда класть модель.")


def export(path):
    path.parent.mkdir(parents=True, exist_ok=True)

    want = dict(
        filepath=str(path),
        use_selection=False,
        use_visible=False,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_NONE",
        global_scale=1.0,
        object_types={"ARMATURE", "MESH"},
        use_mesh_modifiers=False,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        armature_nodetype="NULL",
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
        axis_forward="-Z",
        axis_up="Y",
        path_mode="COPY",
        embed_textures=False,
    )

    allowed = set(bpy.ops.export_scene.fbx.get_rna_type().properties.keys())
    kw = {k: v for k, v in want.items() if k in allowed}
    dropped = sorted(set(want) - allowed)
    if dropped:
        print("[СКЕЛЕТ] экспортёр не знает: " + ", ".join(dropped))

    bpy.ops.export_scene.fbx(**kw)


# ------------------------------------------------------------- превью

def preview(folder):
    """
    Четыре картинки, чтобы посмотреть на результат до Unity.

    Условие успеха из техзадания проверяется глазами, и импорт ради
    этого — самая дорогая часть проверки. Здесь она стоит секунды.
    """
    folder = Path(folder)
    folder.mkdir(parents=True, exist_ok=True)

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x = 540
    scene.render.resolution_y = 760
    scene.render.film_transparent = False
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.world = bpy.data.worlds.new("W")
    scene.world.color = (0.16, 0.16, 0.18)

    cam_data = bpy.data.cameras.new("Cam")
    cam_data.lens = 55
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam

    arm = bpy.data.objects["Skeleton"]

    def shot(name, act, frame, loc, aim, tall=True):
        chosen = bpy.data.actions[act]
        arm.animation_data.action = chosen
        slots = getattr(chosen, "slots", None)
        if slots is not None and len(slots) and hasattr(arm.animation_data, "action_slot"):
            arm.animation_data.action_slot = slots[0]
        scene.frame_set(frame)

        cam.location = Vector(loc)
        direction = Vector(aim) - Vector(loc)
        cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()

        scene.render.resolution_y = 760 if tall else 540
        scene.render.filepath = str(folder / name)
        bpy.ops.render.render(write_still=True)
        print("[СКЕЛЕТ] превью: " + str(folder / name))

    shot("1-idle-front.png", "Idle", 20, (0.0, -2.2, 0.62), (0.0, 0.0, 0.52))
    shot("2-walk-side.png", "Walk", 9, (2.2, -0.2, 0.62), (0.0, 0.0, 0.50))
    shot("3-flee-side.png", "Flee", 6, (2.1, -0.9, 0.66), (0.0, 0.0, 0.48))
    shot("4-flee-top.png", "Flee", 6, (0.0, 1.3, 2.5), (0.0, 0.0, 0.45), tall=False)


# ---------------------------------------------------------------- запуск

def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

    wipe()

    scene = bpy.context.scene
    scene.render.fps = FPS
    scene.frame_start = 1
    scene.frame_end = 61

    arm = build_armature()
    mesh = build_mesh(arm)
    acts = make_actions(arm)

    heights = [v.co.z for v in mesh.data.vertices]
    print("[СКЕЛЕТ] костей: {}, вершин: {}, граней: {}".format(
        len(arm.data.bones), len(mesh.data.vertices), len(mesh.data.polygons)))
    print("[СКЕЛЕТ] опора {:.3f}, макушка {:.3f}".format(min(heights), max(heights)))
    print("[СКЕЛЕТ] клипы: " + ", ".join(a.name for a in acts))

    out = unity_root() / "Assets" / "Resources" / "Bodies" / "Skeleton.fbx"
    export(out)
    print("[СКЕЛЕТ] записано: " + str(out))

    if "--preview" in argv:
        preview(argv[argv.index("--preview") + 1])


main()
