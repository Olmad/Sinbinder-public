# Assets/Scripts/Tools/blender/bodies.py
"""
Четыре оболочки: тело, арматура и три движения — одним скриптом.

Вырос из `skeleton.py`, которым 12 сентября проверяли, годится ли путь
через Блендер. Путь оказался годен, и тогда же было записано, чем
остальные три силуэта отличаются от первого: **параметрами, а не новой
работой** (`docs/14-HANDOFF.md` §14.7). Здесь это исполнено — один
скрипт, четыре профиля.

Запуск:

    blender --background --python Tools/blender/bodies.py
    blender --background --python Tools/blender/bodies.py -- --shell Ghost
    blender --background --python Tools/blender/bodies.py -- --preview <папка>

Без ключей собирает все четыре: `Assets/Resources/Bodies/Skeleton.fbx`,
`Zombie.fbx`, `Ghost.fbx`, `Golem.fbx` — ровно то, что ищет
`WarriorLook.Build` по имени оболочки.

Условия у всех четырёх одни и те же, и они не украшение:

* опора у ног, в нуле — код ставит модель в ноль и не поднимает;
* рост около 1.0 — код умножает её на свой: 1.2 рядовому, 1.5 опытному;
* лицом в +Z по-юнитевски (в Блендере это -Y);
* арматура одна на всех, humanoid-совместимая, двадцать одна кость,
  поза привязки T-образная. Одна арматура — чтобы движения можно было
  переносить между оболочками, а не писать по три клипа на каждую.

**Оболочка видна, и видно именно то, что о ней говорит игра.** Числа
оболочек лежат в `DemoAssetBuilder.BuildShells`, и силуэты собраны
по ним, а не по вкусу:

| оболочка | что о ней сказано | что из этого видно |
|---|---|---|
| Скелет | «кость помнит только усталость» | голая кость, ничего лишнего |
| Зомби | «гниющее тело помнит голод» | раздутый живот, обнажённая рука, хромота |
| Призрак | «бесплотный не может взять» | ног нет вовсе, длинные пустые руки |
| Голем | «камень знает, что он камень» | глыба, трещины, тяжёлый шаг |

Походка — тоже параметр (<see cref="Gait"/> ниже): те же три ключевые
позы, но у голема размах короче и подскок тяжелее, а зомби тащит правую
ногу. Хромота — не украшение: зомби медленнее всех (2.4 против 3.6),
и это должно быть видно ногами, а не только в журнале.

Ни одного жребия: ни Random, ни времени, ни порядка словаря, от которого
зависел бы результат. Это то же правило, что и в игре (CLAUDE.md):
один и тот же запуск даёт одну и ту же модель.

Оговорка, проверенная замером, а не предположенная: **сам файл побайтно
не повторяется.** Блендер пишет в FBX время создания и внутренние
идентификаторы объектов, и они меняются от запуска к запуску
(PYTHONHASHSEED=0 не помогает — проверено). Геометрия, веса и ключи
при этом те же. Отсюда правило: перезапускать скрипт стоит тогда,
когда модель действительно изменилась, — иначе в историю ляжет
полтора мегабайта шума, в котором настоящая правка не видна.
"""

import math
import sys
from collections import namedtuple
from pathlib import Path

import bpy
from mathutils import Vector, Quaternion

FPS = 30

# --------------------------------------------------------------- пропорции

# Высоты и полушири́ны. Человеческие, а не героические: воин в полтора
# роста получается из этого умножением, а обратно — нет.
BASE = dict(
    ankle=0.055, knee=0.275, hip=0.500, waist=0.560, chest=0.665,
    shoulder=0.775, neck=0.795, skull=0.905, top=0.990,
    leg_x=0.075, shoulder_x=0.100,
    elbow=0.255, wrist=0.405, finger=0.480,
)


def bones(p):
    """
    Двадцать одна кость. Набор один для всех оболочек — меняются только
    числа, и потому клип, написанный для одной, ложится на любую.

    Имена общепринятые: Unity сопоставляет humanoid по именам
    и иерархии, и «имена не важны» верно ровно до того дня, когда
    кто-нибудь откроет окно настройки аватара.
    """
    sh, lg = p["shoulder"], p["leg_x"]
    sx, el, wr, fg = p["shoulder_x"], p["elbow"], p["wrist"], p["finger"]

    made = [
        ("Hips",  None,    (0, 0, p["hip"]),   (0, 0, p["waist"])),
        ("Spine", "Hips",  (0, 0, p["waist"]), (0, 0, p["chest"])),
        ("Chest", "Spine", (0, 0, p["chest"]), (0, 0, p["neck"])),
        ("Neck",  "Chest", (0, 0, p["neck"]),  (0, 0, p["skull"] - 0.055)),
        ("Head",  "Neck",  (0, 0, p["skull"] - 0.055), (0, 0, p["top"])),
    ]

    for side, tag in ((1, "Left"), (-1, "Right")):
        s = side
        made += [
            (tag + "Shoulder", "Chest",          (s * 0.02, 0, sh), (s * sx, 0, sh)),
            (tag + "UpperArm", tag + "Shoulder", (s * sx, 0, sh),   (s * el, 0, sh)),
            (tag + "LowerArm", tag + "UpperArm", (s * el, 0, sh),   (s * wr, 0, sh)),
            (tag + "Hand",     tag + "LowerArm", (s * wr, 0, sh),   (s * fg, 0, sh)),

            (tag + "UpperLeg", "Hips",           (s * lg, 0, p["hip"]),   (s * lg, 0, p["knee"])),
            (tag + "LowerLeg", tag + "UpperLeg", (s * lg, 0, p["knee"]),  (s * lg, 0, p["ankle"])),
            (tag + "Foot",     tag + "LowerLeg", (s * lg, 0, p["ankle"]), (s * lg, -0.070, 0.012)),
            (tag + "Toes",     tag + "Foot",     (s * lg, -0.070, 0.012), (s * lg, -0.115, 0.012)),
        ]

    return made


# ------------------------------------------------------------------ сцена

def wipe():
    """Пустая сцена. Стартовый файл Блендера несёт куб, лампу и камеру."""
    for block in (bpy.data.objects, bpy.data.meshes, bpy.data.armatures,
                  bpy.data.actions, bpy.data.materials, bpy.data.cameras,
                  bpy.data.lights, bpy.data.worlds):
        for item in list(block):
            block.remove(item)


def build_armature(name, p):
    data = bpy.data.armatures.new(name)
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)

    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")

    made = {}
    for bone, parent, head, tail in bones(p):
        eb = data.edit_bones.new(bone)
        eb.head = Vector(head)
        eb.tail = Vector(tail)
        eb.use_connect = False
        if parent is not None:
            eb.parent = made[parent]
        made[bone] = eb

    bpy.ops.object.mode_set(mode="OBJECT")
    return obj


# -------------------------------------------------------------- геометрия

class Body:
    """
    Копилка треугольников и четырёхугольников.

    Без bpy.ops нарочно: операторы в фоновом режиме зависят от контекста,
    а здесь всё считается числами и потому одинаково на любой машине.
    Заодно каждая деталь сразу привязана к своей кости — жёстко, весом 1.
    Для скелета это не упрощение, а правда: кость не гнётся; для
    остальных трёх этого хватает, потому что все они тоже не мягкие.

    Материалов ровно три, и смысл у них один во всех оболочках:
    <b>0</b> — из чего она сделана, <b>1</b> — чем отличается,
    <b>2</b> — пустота, которую видно насквозь.
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


def sphere(center, r, scale=(1, 1, 1), segs=20, rings=12, holes=(), only=()):
    """
    Шар, из которого можно вырезать конусы (<c>holes</c>) или оставить
    только их (<c>only</c>). Глазницы и шапка волос делаются именно так —
    пропуском граней, а не булевой операцией: результат тот же,
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

    def cones(spec):
        out = []
        for axis, deg in spec:
            a = Vector(axis)
            a.normalize()
            out.append((a, math.cos(math.radians(deg))))
        return out

    cut, keepin = cones(holes), cones(only)

    def keep(idx):
        if not cut and not keepin:
            return True
        n = Vector((0.0, 0.0, 0.0))
        for i in idx:
            n = n + Vector(dirs[i])
        n.normalize()
        for a, limit in cut:
            if n.dot(a) > limit:
                return False
        if keepin:
            return any(n.dot(a) > limit for a, limit in keepin)
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
    """Кость, мышца или столб дыма — между двумя точками, с сужением."""
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


def limb(b, bone, p0, p1, r0, r1, joint=0.0, mat=0, segs=8):
    b.add(*tube(p0, p1, r0, r1, segs=segs), bone=bone, mat=mat)
    if joint > 0.0:
        b.add(*sphere(p0, joint, segs=10, rings=6), bone=bone, mat=mat)


def sides():
    """Левая и правая, в этом порядке. Ни разу не наоборот."""
    return ((1, "Left"), (-1, "Right"))


# ------------------------------------------------------------ скелет

def build_skeleton(b, p):
    """Кость и ничего кроме. Из оболочек она самая быстрая и самая
    хрупкая, и выглядеть обязана соответственно."""
    hip, knee, ankle = p["hip"], p["knee"], p["ankle"]
    sh, sx, el, wr, fg = (p["shoulder"], p["shoulder_x"],
                          p["elbow"], p["wrist"], p["finger"])

    # таз: обод, а не куб. Сверху по нему скелет и узнаётся.
    b.add(*ring((0, 0.004, hip - 0.012), 0.078, 0.019, scale=(1.0, 0.78, 1.1)), bone="Hips")
    for side, _ in sides():
        b.add(*box((side * 0.072, 0.004, hip + 0.018), (0.030, 0.062, 0.055)), bone="Hips")
    b.add(*box((0, 0.045, hip + 0.030), (0.034, 0.026, 0.070)), bone="Hips")

    for i in range(3):
        b.add(*box((0, 0.040, p["waist"] - 0.005 + i * 0.036), (0.036, 0.032, 0.024)),
              bone="Spine")

    ribs = [(0.060, 0.673), (0.070, 0.700), (0.077, 0.726),
            (0.079, 0.750), (0.073, 0.771)]
    for major, z in ribs:
        b.add(*ring((0, 0.006, z), major, 0.0105, scale=(1.0, 0.80, 1.25)), bone="Chest")
    b.add(*box((0, -0.052, 0.722), (0.030, 0.016, 0.098)), bone="Chest")
    for i in range(4):
        b.add(*box((0, 0.048, 0.676 + i * 0.030), (0.036, 0.030, 0.022)), bone="Chest")

    for i in range(2):
        b.add(*box((0, 0.008, p["neck"] + 0.012 + i * 0.024), (0.032, 0.032, 0.020)),
              bone="Neck")

    skull(b, p, r=0.067, socket=13.0, jaw=True)

    for side, tag in sides():
        b.add(*box((side * 0.058, 0.010, sh + 0.010), (0.082, 0.048, 0.019)),
              bone=tag + "Shoulder")

        limb(b, tag + "UpperArm", (side * sx, 0, sh), (side * el, 0, sh),
             0.020, 0.016, joint=0.024)

        # предплечье из двух костей: лучевая и локтевая читаются
        # как скелет вернее любой другой детали.
        for off in (-0.014, 0.014):
            limb(b, tag + "LowerArm", (side * el, off, sh), (side * (wr - 0.005), off * 0.4, sh),
                 0.0105, 0.0090)
        b.add(*sphere((side * el, 0, sh), 0.021, segs=10, rings=6), bone=tag + "LowerArm")

        hand(b, tag + "Hand", side, wr, fg, sh, thick=0.018)

        limb(b, tag + "UpperLeg", (side * p["leg_x"], 0, hip), (side * p["leg_x"], 0, knee),
             0.026, 0.020, joint=0.030)
        for off in (-0.013, 0.013):
            limb(b, tag + "LowerLeg",
                 (side * p["leg_x"] + off, 0, knee), (side * p["leg_x"] + off * 0.5, 0, ankle),
                 0.0135, 0.0105)
        b.add(*sphere((side * p["leg_x"], 0, knee), 0.026, segs=10, rings=6),
              bone=tag + "LowerLeg")

        foot(b, tag, side, p["leg_x"])


def skull(b, p, r, socket, jaw, mat=0, hair=False):
    """
    Череп с настоящими дырами. Внутри — тёмный шар: сквозь глазницы
    видно пустоту, и это единственное, что превращает шар в череп.
    """
    z = p["skull"]
    b.add(*sphere((0, 0.014, z), r, scale=(1.0, 1.18, 1.06), segs=44, rings=26,
                  holes=[((0.46, -0.86, 0.04), socket),
                         ((-0.46, -0.86, 0.04), socket),
                         ((0.0, -1.0, -0.30), socket * 0.55)]), bone="Head", mat=mat)
    b.add(*sphere((0, 0.014, z), r - 0.005, scale=(1.0, 1.18, 1.06), segs=24, rings=14),
          bone="Head", mat=2)

    if hair:
        # Шапка волос — тот же шар чуть больше, от которого оставлен
        # только верх. У скелета её нет: волосам не на чем держаться
        # (22-LOOK.md §4).
        b.add(*sphere((0, 0.020, z + 0.004), r + 0.006, scale=(1.0, 1.16, 1.06),
                      segs=28, rings=18, only=[((0.0, 0.35, 1.0), 62.0)]),
              bone="Head", mat=2)

    if jaw:
        b.add(*box((0, -0.016, z - 0.058), (0.080, 0.082, 0.026)), bone="Head", mat=mat)
        b.add(*box((0, -0.052, z - 0.044), (0.064, 0.016, 0.018)), bone="Head", mat=mat)


def hand(b, bone, side, wrist, finger, z, thick, mat=0):
    b.add(*box((side * (wrist + 0.020), 0, z), (0.044, 0.046, thick)), bone=bone, mat=mat)
    for k in (-1, 0, 1):
        b.add(*tube((side * (wrist + 0.042), k * 0.016, z),
                    (side * finger, k * 0.018, z), 0.006, 0.005, segs=6),
              bone=bone, mat=mat)


def foot(b, tag, side, x, mat=0, wide=1.0):
    b.add(*box((side * x, -0.030, 0.022), (0.048 * wide, 0.090, 0.036)),
          bone=tag + "Foot", mat=mat)
    b.add(*box((side * x, -0.098, 0.012), (0.046 * wide, 0.052, 0.020)),
          bone=tag + "Toes", mat=mat)


# ------------------------------------------------------------ зомби

def build_zombie(b, p):
    """
    Плоть поверх той же кости. Три вещи говорят «зомби» раньше цвета:
    раздутый живот (голод +25), обнажённая правая рука (гниёт) и то,
    что видно уже в движении, — хромота.
    """
    hip, knee, ankle = p["hip"], p["knee"], p["ankle"]
    sh, sx, el, wr, fg = (p["shoulder"], p["shoulder_x"],
                          p["elbow"], p["wrist"], p["finger"])

    # Весь столб — на одной кости. Жёсткие веса рвут поверхность
    # на границе костей, и на наклоне в двадцать восемь градусов шов
    # по поясу разошёлся открытой щелью (замечено на превью бегства).
    # Гнуться зомби нечем: он не сгибается, он тащится.
    b.add(*tube((0, 0.004, hip - 0.030), (0, 0.004, p["waist"]), 0.098, 0.101, segs=16),
          bone="Spine")

    # Туловище — столб, а не шары друг на друге: два шара подряд читаются
    # снеговиком, и, что хуже, верхний из них читается грудью. Пол в этой
    # игре назначается поимённо и только в авторских списках (CLAUDE.md),
    # и порода зомби такого права не имеет.
    b.add(*tube((0, 0.004, p["waist"]), (0, 0.004, p["chest"]), 0.101, 0.104, segs=16),
          bone="Spine")
    b.add(*tube((0, 0.004, p["chest"]), (0, 0.006, p["neck"] + 0.010), 0.104, 0.092, segs=16),
          bone="Spine")

    # Живот висит вперёд, а не раздувается кругом. «Гниющее тело помнит
    # голод» — помнит именно так: брюхом, а не объёмом.
    b.add(*sphere((0, -0.052, p["waist"] + 0.010), 0.074, scale=(1.10, 0.80, 0.86),
                  segs=20, rings=12), bone="Spine")

    # Рёбра наружу, и только слева: гниёт не симметрично, а симметрия
    # здесь читалась бы как броня.
    for z in (0.686, 0.714, 0.742):
        b.add(*tube((0.010, -0.082, z), (0.098, -0.028, z), 0.011, 0.008, segs=6),
              bone="Spine", mat=1)

    b.add(*tube((0, 0.010, p["neck"]), (0, 0.014, p["skull"] - 0.050), 0.036, 0.030),
          bone="Spine")

    skull(b, p, r=0.070, socket=10.0, jaw=True, hair=True)

    for side, tag in sides():
        bare = side < 0          # правая рука обнажена до кости

        b.add(*sphere((side * (sx - 0.010), 0.004, sh + 0.004), 0.052,
                      scale=(1.0, 0.86, 0.80), segs=14, rings=10), bone="Spine")

        if bare:
            limb(b, tag + "UpperArm", (side * sx, 0, sh), (side * el, 0, sh),
                 0.021, 0.017, joint=0.025, mat=1)
            for off in (-0.014, 0.014):
                limb(b, tag + "LowerArm", (side * el, off, sh),
                     (side * (wr - 0.005), off * 0.4, sh), 0.0105, 0.0090, mat=1)
            b.add(*sphere((side * el, 0, sh), 0.021, segs=10, rings=6),
                  bone=tag + "LowerArm", mat=1)
            hand(b, tag + "Hand", side, wr, fg, sh, thick=0.018, mat=1)
        else:
            limb(b, tag + "UpperArm", (side * sx, 0, sh), (side * el, 0, sh),
                 0.040, 0.032, joint=0.042)
            limb(b, tag + "LowerArm", (side * el, 0, sh), (side * (wr - 0.005), 0, sh),
                 0.031, 0.024, joint=0.032)
            hand(b, tag + "Hand", side, wr, fg, sh, thick=0.030)

        limb(b, tag + "UpperLeg", (side * p["leg_x"], 0, hip), (side * p["leg_x"], 0, knee),
             0.052, 0.038, joint=0.054)
        limb(b, tag + "LowerLeg", (side * p["leg_x"], 0, knee), (side * p["leg_x"], 0, ankle),
             0.038, 0.028, joint=0.040)
        foot(b, tag, side, p["leg_x"], wide=1.15)


# ------------------------------------------------------------ призрак

def build_ghost(b, p):
    """
    «Бесплотный не может взять — только смотреть, как берут другие.»

    Ног нет вовсе, и это не экономия: гардероба у призрака нет по той же
    причине (22-LOOK.md §4). Вместо ног — сужающийся книзу дым,
    привязанный к тазу целиком: он не шагает, он тянется следом.

    Руки нарочно длиннее человеческих, а кисти — пустые: тянется, но
    не берёт.
    """
    hip, sh = p["hip"], p["shoulder"]
    sx, el, wr, fg = p["shoulder_x"], p["elbow"], p["wrist"], p["finger"]

    # Одно сплошное тело от плеч до острия. Собрано звеньями, у которых
    # радиусы на стыках совпадают, — иначе на месте пояса остаётся
    # ступенька, и призрак читается как манекен в юбке.
    #
    # Всё это висит на одной кости, «Spine», и это не лень. Жёсткие веса
    # рвут поверхность на границе костей, а у сплошного тела шов виден
    # сразу; у призрака сгибаться нечему — он не сгибается, он тянется.
    # Подскок при этом наследуется: «Spine» растёт из таза.
    stack = [(p["neck"] + 0.015, 0.086), (p["shoulder"], 0.100),
             (p["chest"], 0.106), (p["waist"], 0.100),
             (hip, 0.104), (0.360, 0.096), (0.200, 0.070),
             (0.075, 0.030), (0.018, 0.009)]
    for (z0, r0), (z1, r1) in zip(stack, stack[1:]):
        b.add(*tube((0, 0.004, z0), (0, 0.004, z1), r0, r1, segs=20), bone="Spine")

    b.add(*tube((0, 0.010, p["neck"]), (0, 0.014, p["skull"] - 0.050), 0.038, 0.032),
          bone="Spine")

    # Голова без челюсти: лица нет, есть две дыры. Глазницы шире всех
    # прочих — это единственное, что у призрака вместо лица.
    b.add(*sphere((0, 0.014, p["skull"]), 0.072, scale=(1.0, 1.14, 1.12),
                  segs=40, rings=24,
                  holes=[((0.40, -0.90, 0.02), 19.0),
                         ((-0.40, -0.90, 0.02), 19.0)]), bone="Head")
    b.add(*sphere((0, 0.014, p["skull"]), 0.066, scale=(1.0, 1.14, 1.12),
                  segs=22, rings=14), bone="Head", mat=2)

    for side, tag in sides():
        b.add(*sphere((side * (sx - 0.012), 0.004, sh), 0.044,
                      scale=(1.0, 0.9, 0.8), segs=12, rings=8), bone="Spine")
        limb(b, tag + "UpperArm", (side * sx, 0, sh), (side * el, 0, sh), 0.030, 0.022)
        limb(b, tag + "LowerArm", (side * el, 0, sh), (side * (wr - 0.005), 0, sh),
             0.022, 0.014)
        # Кисть — три длинных пальца и ничего между ними.
        for k in (-1, 0, 1):
            b.add(*tube((side * wr, k * 0.014, sh),
                        (side * fg, k * 0.026, sh - 0.012), 0.008, 0.004, segs=6),
                  bone=tag + "Hand", mat=1)


# ------------------------------------------------------------ голем

def build_golem(b, p):
    """
    «Камень не завидует. Камень знает, что он камень.»

    Всё прямоугольное: у камня нет мышц, у него сколы. Шеи нет — голова
    сидит на плечах, и это первое, что видно сверху. Трещины — тёмные
    прорези: то же «сквозь него видно пустоту», что у глазниц, только
    пустота тут не в голове, а в самом теле.
    """
    hip, knee, ankle = p["hip"], p["knee"], p["ankle"]
    sh, sx, el, wr, fg = (p["shoulder"], p["shoulder_x"],
                          p["elbow"], p["wrist"], p["finger"])

    # Все плиты корпуса — на одной кости. Порознь они при любом наклоне
    # съезжают друг по другу, и глыба читается кучей ящиков. Камню
    # сгибаться нечем, и это не упрощение, а то, чем камень является.
    b.add(*box((0, 0.004, hip + 0.010), (0.235, 0.150, 0.130)), bone="Spine")
    b.add(*box((0, 0.004, p["waist"] + 0.030), (0.250, 0.160, 0.120)), bone="Spine")
    b.add(*box((0, 0.004, 0.716), (0.265, 0.175, 0.140)), bone="Spine")
    b.add(*box((0, 0.010, 0.782), (0.372, 0.168, 0.050)), bone="Spine")

    # Шея есть в костях и нет в камне: голова сидит прямо на плитах.
    b.add(*box((0, 0.008, p["neck"] + 0.010), (0.120, 0.120, 0.040)), bone="Spine")
    b.add(*box((0, 0.012, p["skull"] - 0.010), (0.170, 0.180, 0.130)), bone="Head", mat=0)
    for side, _ in sides():
        b.add(*box((side * 0.042, -0.072, p["skull"] + 0.008), (0.044, 0.046, 0.032)),
              bone="Head", mat=2)
    # Скол на углу головы: ровная глыба читается изделием, а не камнем.
    b.add(*box((0.082, 0.070, p["skull"] + 0.056), (0.048, 0.060, 0.048)),
          bone="Head", mat=1)
    b.add(*box((0, 0.012, p["skull"] + 0.062), (0.140, 0.150, 0.030)), bone="Head", mat=1)

    # Трещины. Три, и все на разных плитах: одна читалась бы как шов.
    b.add(*box((0.045, -0.090, 0.726), (0.014, 0.014, 0.110)), bone="Spine", mat=2)
    b.add(*box((-0.090, -0.082, 0.700), (0.012, 0.014, 0.062)), bone="Spine", mat=2)
    b.add(*box((0.060, -0.078, hip + 0.010), (0.013, 0.013, 0.080)), bone="Spine", mat=2)

    for side, tag in sides():
        b.add(*box((side * (sx - 0.006), 0.006, sh + 0.010), (0.115, 0.148, 0.128)),
              bone=tag + "Shoulder")

        b.add(*box((side * (sx + el) * 0.5, 0, sh), (abs(el - sx), 0.105, 0.105)),
              bone=tag + "UpperArm")
        b.add(*box((side * (el + wr) * 0.5, 0, sh), (abs(wr - el), 0.090, 0.090)),
              bone=tag + "LowerArm", mat=1)
        b.add(*box((side * (wr + fg) * 0.5, 0, sh), (abs(fg - wr), 0.096, 0.096)),
              bone=tag + "Hand")

        lx = p["leg_x"]
        b.add(*box((side * lx, 0, (hip + knee) * 0.5), (0.135, 0.130, hip - knee)),
              bone=tag + "UpperLeg")
        b.add(*box((side * lx, 0, (knee + ankle) * 0.5), (0.120, 0.115, knee - ankle)),
              bone=tag + "LowerLeg")
        b.add(*box((side * lx, -0.028, 0.030), (0.135, 0.180, 0.060)), bone=tag + "Foot")
        b.add(*box((side * lx, -0.108, 0.024), (0.125, 0.070, 0.044)), bone=tag + "Toes")


# ------------------------------------------------------------------ меш

def build_mesh(arm, shell):
    b = Body()
    shell.build(b, shell.parts)

    mesh = bpy.data.meshes.new(shell.name + "Mesh")
    mesh.from_pydata(b.verts, [], b.faces)
    mesh.validate(verbose=False)

    for label, rgba in shell.materials:
        m = bpy.data.materials.new(label)
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

    obj = bpy.data.objects.new(shell.name + "Mesh", mesh)
    bpy.context.scene.collection.objects.link(obj)

    for bone, idx in b.groups.items():
        vg = obj.vertex_groups.new(name=bone)
        vg.add(idx, 1.0, "REPLACE")

    obj.parent = arm
    mod = obj.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    mod.use_vertex_groups = True

    return obj


# -------------------------------------------------------------- движения

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


# Походка как параметр. Ключевые позы у всех четырёх одни и те же —
# меняется то, насколько широко они исполняются и с какой осанкой.
#
# Это не экономия строк, а то же правило, что и в игре: оболочка обязана
# быть видна. Зомби медленнее всех (2.4 против 3.6 у скелета), голем ещё
# медленнее (2.0) и вчетверо крепче, — и если это видно только в журнале,
# значит выбор тела у устройства не значит ничего на глаз.
Gait = namedtuple("Gait", "legs arms torso hunch down elbow bob limp")

STRIDE = Gait(legs=1.0, arms=1.0, torso=1.0, hunch=0.0,
              down=0.0, elbow=0.0, bob=1.0, limp=1.0)


def stance(legs, arms, torso, bob, g):
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

    lh, lk, lf = lh * g.legs, lk * g.legs, lf * g.legs
    rh, rk, rf = (rh * g.legs * g.limp, rk * g.legs * g.limp, rf * g.legs * g.limp)
    la, ra = la * g.arms, ra * g.arms

    # Наклон множится, а не прибавляется. Слагаемое пришлось бы подбирать
    # под каждый клип отдельно: минус восемь, задуманный чтобы голем
    # не складывался в беге, в покое отклонил бы его назад. Множитель
    # сохраняет знак во всех трёх позах, а горб зомби — отдельным
    # слагаемым, он-то как раз постоянный.
    waist = waist * g.torso + g.hunch
    chest = chest * g.torso

    return {
        "Hips": ([], bob * g.bob),
        "Spine": ([(X, waist)], None),
        "Chest": ([(X, chest)], None),
        "Neck": ([(X, nod * 0.4)], None),
        "Head": ([(X, nod), (Z, look)], None),

        "LeftUpperArm": ([(Y, down + g.down), (X, la)], None),
        "RightUpperArm": ([(Y, -(down + g.down)), (X, ra)], None),
        "LeftLowerArm": ([(Z, -(elbow + g.elbow))], None),
        "RightLowerArm": ([(Z, elbow + g.elbow)], None),

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
            print("[ТЕЛА] слот действия не задан: " + str(err))

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


def make_actions(arm, g):
    def s(legs, arms, torso, bob):
        return stance(legs, arms, torso, bob, g)

    # --- Idle: стоит. Скелету дышать нечем, поэтому покой — это
    #     медленный перенос веса и поворот головы, а не вздохи грудью.
    idle = [
        (1,  s((-2, 4, -1, 2, 4, 1), (78, 1, -1, 12), (2, 1, 1, 0), 0.0)),
        (20, s((-3, 5, -1, 3, 5, 1), (79, 2, -2, 13), (3, 2, 2, 13), -0.006)),
        (40, s((-1, 4, -1, 1, 4, 1), (77, 0, 0, 11), (2, 1, 0, -9), 0.004)),
        (61, s((-2, 4, -1, 2, 4, 1), (78, 1, -1, 12), (2, 1, 1, 0), 0.0)),
    ]

    # --- Walk: шаг. Тридцать два кадра — чуть больше секунды, как
    #     у человека, и на это опирается ощущение скорости отряда.
    walk = [
        (1,  s((-25, 6, -6, 22, 22, 10), (76, 16, -16, 20), (4, 2, -2, 0), 0.0)),
        (9,  s((-4, 2, 0, -2, 34, -10), (76, 0, 0, 18), (4, 2, -2, 0), 0.018)),
        (17, s((22, 22, 10, -25, 6, -6), (76, -16, 16, 20), (4, 2, -2, 0), 0.0)),
        (25, s((-2, 34, -10, -4, 2, 0), (76, 0, 0, 18), (4, 2, -2, 0), 0.018)),
        (33, s((-25, 6, -6, 22, 22, 10), (76, 16, -16, 20), (4, 2, -2, 0), 0.0)),
    ]

    # --- Flee: бежит прочь. Ради этого клипа всё и затевалось: подпись
    #     «Сбегает» обязана стать видимой. Отличий от шага три, и все
    #     крупные — наклон вперёд, длинный шаг и высоко поднятые руки.
    #     Мелкая разница здесь была бы хуже отсутствия: игрок сверху
    #     не отличит быструю ходьбу от бегства.
    flee = [
        (1,  s((-44, 12, -12, 36, 58, 16), (48, 34, -34, 66), (17, 9, -13, 0), 0.0)),
        (6,  s((-10, 6, -4, -18, 78, -16), (48, 4, -4, 62), (17, 9, -13, 0), 0.034)),
        (11, s((36, 58, 16, -44, 12, -12), (48, -34, 34, 66), (17, 9, -13, 0), 0.0)),
        (16, s((-18, 78, -16, -10, 6, -4), (48, -4, 4, 62), (17, 9, -13, 0), 0.034)),
        (21, s((-44, 12, -12, 36, 58, 16), (48, 34, -34, 66), (17, 9, -13, 0), 0.0)),
    ]

    # Имена обязаны совпасть буква в букву с BodyMotion: Animator.Play
    # по чужому имени молча ничего не делает — худший вид поломки,
    # потому что выглядит как «анимация просто не сделана».
    return [action(arm, "Idle", idle),
            action(arm, "Walk", walk),
            action(arm, "Flee", flee)]


# ------------------------------------------------------------- оболочки

Shell = namedtuple("Shell", "name parts materials build gait")

BONE = (0.855, 0.830, 0.760, 1.0)
HOLLOW = (0.035, 0.030, 0.032, 1.0)


def proportions(**over):
    p = dict(BASE)
    p.update(over)
    return p


SHELLS = [
    Shell(
        name="Skeleton",
        parts=proportions(),
        materials=[("Bone", BONE), ("Bone Dry", (0.790, 0.762, 0.690, 1.0)),
                   ("Hollow", HOLLOW)],
        build=build_skeleton,
        gait=STRIDE,
    ),
    Shell(
        name="Zombie",
        parts=proportions(shoulder_x=0.105),
        materials=[("Flesh", (0.415, 0.455, 0.345, 1.0)), ("Bone", BONE),
                   ("Hollow", HOLLOW)],
        build=build_zombie,
        # Тащит правую ногу и держит руки перед собой. Скорость 2.4
        # против 3.6 у скелета — и это видно ногами, а не только
        # в журнале.
        gait=STRIDE._replace(legs=0.72, arms=0.45, torso=0.8, hunch=12.0,
                             down=-14.0, elbow=34.0, bob=0.7, limp=0.42),
    ),
    Shell(
        name="Ghost",
        parts=proportions(leg_x=0.055, shoulder_x=0.088,
                          elbow=0.270, wrist=0.445, finger=0.530),
        materials=[("Spirit", (0.735, 0.800, 0.855, 1.0)),
                   ("Spirit Dim", (0.560, 0.640, 0.720, 1.0)), ("Hollow", HOLLOW)],
        build=build_ghost,
        # Ног нет, шагать нечем: размах почти погашен, зато подскок
        # больше всех — он не идёт, он плывёт. Быстрее всех (5.0),
        # и лёгкость обязана быть видна.
        gait=STRIDE._replace(legs=0.18, arms=1.15, torso=0.55, hunch=-3.0,
                             down=-10.0, elbow=-6.0, bob=1.9),
    ),
    Shell(
        name="Golem",
        parts=proportions(hip=0.470, knee=0.248, waist=0.545, chest=0.660,
                          shoulder=0.760, neck=0.790, skull=0.880, top=0.975,
                          leg_x=0.105, shoulder_x=0.205,
                          elbow=0.330, wrist=0.458, finger=0.528),
        materials=[("Stone", (0.455, 0.445, 0.425, 1.0)),
                   ("Stone Worn", (0.375, 0.365, 0.350, 1.0)), ("Crack", HOLLOW)],
        build=build_golem,
        # Короткий шаг, тяжёлый подскок, руки отведены от корпуса:
        # мешает собственная толщина. Самый медленный из четырёх (2.0).
        # Наклон почти погашен, локоть почти выпрямлен, размах короткий:
        # у глыбы толщиной в тридцать сантиметров согнутая рука входит
        # в собственную грудь. Толщину клип не знает — он написан
        # под скелета, — и учитывать её обязана походка.
        gait=STRIDE._replace(legs=0.55, arms=0.34, torso=0.22, hunch=0.0,
                             down=8.0, elbow=-44.0, bob=1.45),
    ),
]


# ---------------------------------------------------------------- вывод

def unity_root():
    """Корень проекта Unity — там, где лежит Assets."""
    here = Path(__file__).resolve()
    for up in here.parents:
        if (up / "Assets").is_dir():
            return up
    # Из облачной копии, где над Assets/Scripts ничего нет, это всё
    # равно не запустить: Блендера там нет. Но молчать нельзя.
    raise SystemExit("Папки Assets над скриптом нет: некуда класть модели.")


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
        print("[ТЕЛА] экспортёр не знает: " + ", ".join(dropped))

    bpy.ops.export_scene.fbx(**kw)


# -------------------------------------------------------------- превью

def preview(shell, folder):
    """
    Четыре картинки на оболочку, чтобы посмотреть на результат до Unity.

    Это не удобство. Первая версия черепа сливала обе глазницы в одну
    чёрную полосу поперёк лица, и увидеть это можно было только глазами;
    импорт ради такой проверки — самая дорогая её часть, а здесь она
    стоит секунды.
    """
    folder = Path(folder)
    folder.mkdir(parents=True, exist_ok=True)

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x = 540
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

    arm = bpy.data.objects[shell.name]

    def shot(tag, act, frame, loc, aim, tall=True):
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
        scene.render.filepath = str(folder / f"{shell.name}-{tag}.png")
        bpy.ops.render.render(write_still=True)

    shot("1-idle-front", "Idle", 20, (0.0, -2.2, 0.62), (0.0, 0.0, 0.52))
    shot("2-walk-side", "Walk", 9, (2.2, -0.2, 0.62), (0.0, 0.0, 0.50))
    shot("3-flee-side", "Flee", 6, (2.1, -0.9, 0.66), (0.0, 0.0, 0.48))
    shot("4-flee-top", "Flee", 6, (0.0, 1.3, 2.5), (0.0, 0.0, 0.45), tall=False)

    print(f"[ТЕЛА] {shell.name}: превью в {folder}")


# ---------------------------------------------------------------- запуск

def make(shell, out_dir, shots):
    wipe()

    scene = bpy.context.scene
    scene.render.fps = FPS
    scene.frame_start = 1
    scene.frame_end = 61

    arm = build_armature(shell.name, shell.parts)
    mesh = build_mesh(arm, shell)
    acts = make_actions(arm, shell.gait)

    heights = [v.co.z for v in mesh.data.vertices]
    print("[ТЕЛА] {}: костей {}, вершин {}, граней {}, опора {:.3f}, макушка {:.3f}, клипы {}"
          .format(shell.name, len(arm.data.bones), len(mesh.data.vertices),
                  len(mesh.data.polygons), min(heights), max(heights),
                  ", ".join(a.name for a in acts)))

    export(out_dir / (shell.name + ".fbx"))

    if shots:
        preview(shell, shots)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

    def opt(key):
        return argv[argv.index(key) + 1] if key in argv else None

    only = opt("--shell")
    shots = opt("--preview")

    out_dir = unity_root() / "Assets" / "Resources" / "Bodies"

    chosen = [s for s in SHELLS if only is None or s.name == only]
    if not chosen:
        raise SystemExit("Нет такой оболочки: " + str(only)
                         + ". Есть: " + ", ".join(s.name for s in SHELLS))

    for shell in chosen:
        make(shell, out_dir, shots)

    print("[ТЕЛА] записано в " + str(out_dir))


main()
