# Assets/Scripts/Tools/blender/props.py
"""
Предметы мира: палатки, костёр, стол совета, сундук, врата склепа.

Третья часть того же конвейера, что и `bodies.py` с `wear.py`: всё
считается числами, собирается без единого `bpy.ops` в геометрии
и кладётся в `Assets/Resources/Props/` одним FBX на предмет.

Список взят из `docs/24-MODELS.md` §2.3–2.4 — то есть из того, что
игра уже называет в сценах и документах. Ни одного предмета «на всякий
случай»: сборщик сцен ставит ровно эти.

**Размеры настоящие, в метрах.** Воин ростом 1,2; палатка выше него
вдвое, стол по пояс, сундук по колено. Масштаб — это то, чего нельзя
поправить цветом: неверно собранная палатка портит лагерь сильнее,
чем неверный оттенок.

Запуск:

    blender --background --python Tools/blender/props.py
    blender --background --python Tools/blender/props.py -- --prop Tent --preview <папка>
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
from bodies import Body, box, ring, sphere, tube

# Материалы. Смысл индексов тот же, что у тел и гардероба:
# 0 — из чего сделано, 1 — чем отличается, 2 — тёмное.
CLOTH = ("Tent Cloth", (0.300, 0.270, 0.230, 1.0))
WOOD = ("Wood", (0.210, 0.160, 0.115, 1.0))
IRON = ("Iron", (0.300, 0.310, 0.325, 1.0))
STONE = ("Stone", (0.340, 0.335, 0.320, 1.0))
DARK = ("Dark", (0.055, 0.050, 0.048, 1.0))
EMBER = ("Ember", (0.520, 0.240, 0.110, 1.0))
BONE = ("Bone", (0.820, 0.795, 0.725, 1.0))
BLOSSOM = ("Blossom", (0.640, 0.430, 0.470, 1.0))
BARK = ("Bark", (0.175, 0.140, 0.120, 1.0))

# Геральдика: багрец и старое золото. Единственные два цвета в игре,
# которым позволено быть насыщенными, — и то лишь на знамени.
# Описание стиля (00-GDD.md §9, «Knightcore») держится на встрече
# тёмной земли с этими двумя, и до 20 сентября знамя отряда было
# серым полотнищем, то есть этой встречи не было нигде.
CRIMSON = ("Crimson", (0.310, 0.045, 0.070, 1.0))
GOLD = ("Old Gold", (0.455, 0.355, 0.140, 1.0))
STEEL = ("Steel", (0.520, 0.530, 0.545, 1.0))

Prop = namedtuple("Prop", "name build materials")


# --------------------------------------------------------------- лагерь

def tent(b):
    """
    Двускатная палатка: конёк, два ската, задний полог, колышки.

    Скаты — наклонные грани, а не коробки: первый заход собрал их
    из `box`, то есть двумя отвесными щитами, и палатка читалась
    ширмой. Форма у палатки ровно одна — призма, и считать её надо
    вершинами, а не примитивами.

    Высота 1,9 — выше воина: лагерь обязан читаться жильём, а не рядом
    спальных мешков. Перед открыт: в него входят, и сверху видно, что
    это вход.
    """
    length, half, top = 2.4, 1.05, 1.9
    y0, y1 = -length * 0.5, length * 0.5

    a = (-half, y0, 0.0)
    c = (-half, y1, 0.0)
    bb = (half, y0, 0.0)
    d = (half, y1, 0.0)
    r0 = (0.0, y0, top)
    r1 = (0.0, y1, top)

    # Порядок вершин — против часовой стрелки снаружи: наружу смотрит
    # нормаль, а не изнанка (bodies.audit_primitives про то же).
    verts = [a, bb, c, d, r0, r1]
    faces = [
        [0, 4, 5, 2],   # левый скат
        [1, 3, 5, 4],   # правый скат
        [2, 5, 3],      # задний полог
    ]
    b.add(verts, faces, bone="", mat=0)

    b.add(*tube((0, y0, top), (0, y1, top), 0.045, 0.045, segs=8), bone="", mat=1)
    for y in (y0, y1):
        b.add(*tube((0, y, 0), (0, y, top + 0.06), 0.04, 0.035, segs=8), bone="", mat=1)

    # Колышки по углам: за них палатку и держат.
    for sx in (1, -1):
        for sy in (1, -1):
            b.add(*tube((sx * (half + 0.12), sy * (length * 0.5 - 0.2), 0.0),
                        (sx * (half + 0.12), sy * (length * 0.5 - 0.2), 0.22),
                        0.022, 0.018, segs=6), bone="", mat=1)


def tent_peg(b):
    """
    Колышек с дощечкой под имя. Пустая палатка с именем — вся
    предыстория, которая нужна (`09-PROLOGUE.md` §4).
    """
    b.add(*tube((0, 0, 0), (0, 0, 0.52), 0.028, 0.022, segs=8), bone="", mat=0)
    b.add(*box((0, -0.012, 0.58), (0.34, 0.03, 0.16)), bone="", mat=1)


def campfire(b):
    """Костёр: круг камней, поленья домиком, угли."""
    for i in range(9):
        a = math.radians(i * 40)
        b.add(*sphere((math.cos(a) * 0.62, math.sin(a) * 0.62, 0.06), 0.13,
                      scale=(1.0, 1.0, 0.7), segs=10, rings=6), bone="", mat=1)

    for i in range(5):
        a = math.radians(18 + i * 72)
        b.add(*tube((math.cos(a) * 0.34, math.sin(a) * 0.34, 0.0),
                    (math.cos(a) * 0.07, math.sin(a) * 0.07, 0.46),
                    0.055, 0.045, segs=6), bone="", mat=0)

    b.add(*sphere((0, 0, 0.07), 0.26, scale=(1.0, 1.0, 0.35), segs=12, rings=6),
          bone="", mat=2)


def council_table(b):
    """Стол совета: столешница по пояс и четыре ноги."""
    top = 0.92
    b.add(*box((0, 0, top), (2.0, 1.1, 0.10)), bone="", mat=0)
    b.add(*box((0, 0, top - 0.09), (1.86, 0.96, 0.08)), bone="", mat=1)

    for sx in (1, -1):
        for sy in (1, -1):
            b.add(*box((sx * 0.86, sy * 0.44, (top - 0.10) * 0.5),
                       (0.14, 0.14, top - 0.10)), bone="", mat=1)


def chest(b):
    """
    Сундук Марги. Крышка — отдельный объект: её поворачивает код
    (`TrophyChest._lid`), и приделывать её к корпусу нельзя.
    """
    b.add(*box((0, 0, 0.28), (0.98, 0.62, 0.56)), bone="", mat=0)
    for x in (-0.34, 0.34):
        b.add(*box((x, 0, 0.28), (0.07, 0.66, 0.60)), bone="", mat=1)
    b.add(*box((0, -0.32, 0.34), (0.16, 0.05, 0.14)), bone="", mat=1)


def chest_lid(b):
    """Крышка сундука. Опора — по заднему ребру: там она и вращается."""
    b.add(*box((0, 0.28, 0.05), (0.98, 0.62, 0.10)), bone="", mat=0)
    for x in (-0.34, 0.34):
        b.add(*box((x, 0.28, 0.05), (0.07, 0.66, 0.12)), bone="", mat=1)


def barrel(b):
    b.add(*tube((0, 0, 0), (0, 0, 0.78), 0.30, 0.30, segs=14), bone="", mat=0)
    for z in (0.12, 0.66):
        b.add(*ring((0, 0, z), 0.31, 0.022, segs=16), bone="", mat=1)


def crate(b):
    b.add(*box((0, 0, 0.30), (0.62, 0.62, 0.60)), bone="", mat=0)
    for z in (0.06, 0.54):
        b.add(*box((0, 0, z), (0.66, 0.66, 0.06)), bone="", mat=1)


def soul_shelf(b):
    """
    Полка с банками: доска на кронштейнах, невысокий бортик спереди —
    не даёт банке скатиться, ей самой полка не занята (`SoulShelf`
    расставляет банки поверх сама, это только мебель).
    """
    top = 0.94
    b.add(*box((0, 0, top), (4.2, 0.5, 0.06)), bone="", mat=0)
    b.add(*box((0, -0.24, top + 0.05), (4.2, 0.02, 0.09)), bone="", mat=1)
    for x in (-1.9, -0.65, 0.65, 1.9):
        b.add(*box((x, 0, top * 0.5), (0.10, 0.42, top)), bone="", mat=1)


def body_table(b):
    """Стол тел: длинная каменная плита на четырёх ногах."""
    top = 0.86
    b.add(*box((0, 0, top), (3.4, 0.85, 0.10)), bone="", mat=0)
    for sx in (1, -1):
        for sy in (1, -1):
            b.add(*box((sx * 1.55, sy * 0.34, (top - 0.10) * 0.5),
                       (0.14, 0.14, top - 0.10)), bone="", mat=1)


def binding_device(b):
    """
    Устройство связывания: каменное основание под два гнезда и рычаг —
    сами гнёзда, ложе и рычаг остаются отдельными объектами
    (`BindingSocket`, `BindingHandle` двигают именно их), это только
    постамент под них, с приподнятым ободом по краю.
    """
    top = 0.5
    b.add(*box((0, 0, top * 0.5), (2.2, 1.2, top)), bone="", mat=0)
    b.add(*box((0, 0, top + 0.03), (2.0, 1.0, 0.06)), bone="", mat=1)
    for sx, sy in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        b.add(*box((sx * 1.02, sy * 0.52, (top - 0.05) * 0.5),
                   (0.08, 0.08, top - 0.05)), bone="", mat=2)


def upgrade_plinth(b):
    """
    Гнездо улучшения: квадратный постамент с пустой выемкой сверху —
    пустота и есть замысел (`18-CRYPT.md`, зона 2): место есть,
    а принести в него можно только с вылазки.
    """
    top = 0.5
    b.add(*box((0, 0, top * 0.5), (1.0, 1.0, top)), bone="", mat=0)
    b.add(*ring((0, 0, top + 0.005), 0.34, 0.05, segs=14), bone="", mat=1)


def log_bench(b):
    """Бревно у костра: на нём сидят, и оно задаёт круг."""
    b.add(*tube((-0.95, 0, 0.22), (0.95, 0, 0.22), 0.22, 0.20, segs=12), bone="", mat=0)
    for x in (-0.6, 0.6):
        b.add(*tube((x, 0, 0.0), (x, 0, 0.14), 0.09, 0.08, segs=8), bone="", mat=1)


def sheet(x0, x1, z0, z1, wave=0.055, cols=9, rows=6, thick=0.018):
    """
    Полотнище с волной: плоская доска тканью не читается ни с какого
    расстояния, а одна пологая волна вдоль полотна ловит свет по-разному
    сверху и снизу — и этого хватает.

    Оболочка замкнутая, в два слоя с кромкой: односторонняя ткань
    исчезает, стоит обойти её с другой стороны.
    """
    verts, faces = [], []
    per = (cols + 1) * 2

    for r in range(rows + 1):
        tz = r / rows
        z = z0 + (z1 - z0) * tz

        for layer, dy in ((0, 0.0), (1, thick)):
            for c in range(cols + 1):
                tx = c / cols
                x = x0 + (x1 - x0) * tx
                # Волна сильнее у свободного края и затухает у древка.
                y = math.sin(tx * math.pi * 1.6) * wave * tx + dy
                verts.append((x, y, z))

    def at(r, layer, c):
        return r * per + layer * (cols + 1) + c

    for r in range(rows):
        for c in range(cols):
            faces.append([at(r, 0, c), at(r, 0, c + 1),
                          at(r + 1, 0, c + 1), at(r + 1, 0, c)])
            faces.append([at(r, 1, c + 1), at(r, 1, c),
                          at(r + 1, 1, c), at(r + 1, 1, c + 1)])

        faces.append([at(r, 0, 0), at(r + 1, 0, 0), at(r + 1, 1, 0), at(r, 1, 0)])
        faces.append([at(r, 1, cols), at(r + 1, 1, cols),
                      at(r + 1, 0, cols), at(r, 0, cols)])

    for c in range(cols):
        faces.append([at(0, 1, c), at(0, 1, c + 1), at(0, 0, c + 1), at(0, 0, c)])
        faces.append([at(rows, 0, c), at(rows, 0, c + 1),
                      at(rows, 1, c + 1), at(rows, 1, c)])

    return verts, faces


def banner(b):
    """Знамя отряда: шест, багровое полотнище и золотая полоса."""
    b.add(*tube((0, 0, 0), (0, 0, 2.6), 0.045, 0.035, segs=8), bone="", mat=1)
    b.add(*sheet(0.02, 0.63, 1.33, 2.38), bone="", mat=0)
    b.add(*sheet(0.02, 0.63, 1.27, 1.35, wave=0.050, rows=2), bone="", mat=2)


def palisade(b):
    """Звено частокола: пять кольев и перекладина."""
    for i in range(5):
        x = -0.8 + i * 0.4
        h = 1.55 + (i % 2) * 0.12
        b.add(*tube((x, 0, 0), (x, 0, h), 0.075, 0.055, segs=7), bone="", mat=0)
        b.add(*sphere((x, 0, h), 0.055, scale=(1, 1, 1.5), segs=7, rings=5), bone="", mat=0)

    b.add(*box((0, 0, 1.05), (2.0, 0.07, 0.10)), bone="", mat=1)


def torch(b):
    """Факел на стене склепа: держатель, палка, пламя-огарок."""
    b.add(*box((0, 0.06, 0.0), (0.16, 0.12, 0.30)), bone="", mat=1)
    b.add(*tube((0, 0, 0.05), (0, -0.18, 0.62), 0.032, 0.028, segs=8), bone="", mat=0)
    b.add(*sphere((0, -0.20, 0.70), 0.10, scale=(1.0, 1.0, 1.5), segs=10, rings=7),
          bone="", mat=2)


# ---------------------------------------------------------------- склеп

def crypt_gate(b):
    """Врата склепа: два столба, перемычка и чёрный проём."""
    for x in (-1.25, 1.25):
        b.add(*box((x, 0, 1.45), (0.55, 0.75, 2.9)), bone="", mat=0)
        b.add(*box((x, 0, 2.95), (0.72, 0.88, 0.22)), bone="", mat=1)

    b.add(*box((0, 0, 3.20), (3.30, 0.90, 0.40)), bone="", mat=1)
    b.add(*box((0, 0.24, 1.55), (1.95, 0.30, 3.10)), bone="", mat=2)


def throne(b):
    """Пустой трон. Пустой — в этом всё дело."""
    b.add(*box((0, 0, 0.26), (1.15, 1.05, 0.52)), bone="", mat=0)
    b.add(*box((0, 0.42, 1.25), (1.15, 0.22, 1.95)), bone="", mat=0)
    for x in (-0.52, 0.52):
        b.add(*box((x, 0, 0.78), (0.14, 0.95, 0.20)), bone="", mat=1)
    b.add(*box((0, 0.42, 2.28), (0.95, 0.26, 0.16)), bone="", mat=1)


def altar(b):
    b.add(*box((0, 0, 0.12), (1.65, 0.95, 0.24)), bone="", mat=0)
    b.add(*box((0, 0, 0.58), (1.15, 0.65, 0.72)), bone="", mat=1)
    b.add(*box((0, 0, 1.00), (1.75, 1.05, 0.14)), bone="", mat=0)


def coffin(b):
    """Замурованный гроб в нише: плита и кладка поверх."""
    b.add(*box((0, 0, 1.00), (1.05, 0.42, 2.00)), bone="", mat=0)
    for i in range(5):
        z = 0.20 + i * 0.40
        off = 0.10 if i % 2 else -0.10
        b.add(*box((off, -0.22, z), (1.15, 0.16, 0.34)), bone="", mat=1)


# ---------------------------------------------------------------- прочее

def rock(b):
    """Валун. Неправильный, но не случайный: формула, а не жребий."""
    b.add(*sphere((0, 0, 0.42), 0.62, scale=(1.25, 0.95, 0.68), segs=12, rings=8),
          bone="", mat=0)
    b.add(*sphere((0.42, 0.18, 0.22), 0.34, scale=(1.0, 1.1, 0.8), segs=10, rings=6),
          bone="", mat=1)


def sakura(b):
    """
    Сакура для отдельной сцены (просьба автора от 14 сентября):
    ствол с наклоном, три ветви, три облака цвета.
    """
    b.add(*tube((0, 0, 0), (0.18, 0.05, 1.5), 0.20, 0.13, segs=10), bone="", mat=0)

    limbs = ((0.9, 55, 1.15), (-0.6, 40, 1.35), (0.2, 70, 1.05))
    for dx, angle, height in limbs:
        a = math.radians(angle)
        tipx = 0.18 + dx * 1.1
        tipy = 0.05 + math.cos(a) * 0.55
        tipz = 1.5 + math.sin(a) * height

        b.add(*tube((0.18, 0.05, 1.45), (tipx, tipy, tipz), 0.09, 0.05, segs=8),
              bone="", mat=0)
        b.add(*sphere((tipx, tipy, tipz + 0.22), 0.72,
                      scale=(1.0, 0.95, 0.62), segs=14, rings=9), bone="", mat=1)

    b.add(*sphere((0.18, 0.05, 2.55), 0.85, scale=(1.05, 1.0, 0.58), segs=16, rings=10),
          bone="", mat=1)


def katana(b):
    """
    Катана: рукоять, цуба, клинок с лёгким изгибом.

    Оружие в руке мешает ретаргету (`23-PROMPTS.md` §2), поэтому
    в прологе его нет. Эта — для отдельной сцены с двумя скелетами.
    """
    b.add(*tube((0, 0, -0.26), (0, 0, 0.0), 0.021, 0.023, segs=8), bone="", mat=2)
    b.add(*ring((0, 0, 0.01), 0.048, 0.012, scale=(1.0, 1.0, 0.45), segs=14), bone="", mat=1)

    steps = 10
    for i in range(steps):
        t0, t1 = i / steps, (i + 1) / steps
        z0, z1 = 0.02 + t0 * 0.86, 0.02 + t1 * 0.86
        y0, y1 = t0 * t0 * 0.085, t1 * t1 * 0.085
        r0 = 0.019 * (1.0 - t0 * 0.45)
        r1 = 0.019 * (1.0 - t1 * 0.45)
        b.add(*tube((0, y0, z0), (0, y1, z1), r0, r1, segs=4), bone="", mat=0)


PROPS = [
    Prop("Tent", tent, [CLOTH, WOOD, DARK]),
    Prop("TentPeg", tent_peg, [WOOD, BONE, DARK]),
    Prop("Campfire", campfire, [WOOD, STONE, EMBER]),
    Prop("CouncilTable", council_table, [WOOD, DARK, IRON]),
    Prop("Chest", chest, [WOOD, IRON, DARK]),
    Prop("ChestLid", chest_lid, [WOOD, IRON, DARK]),
    Prop("Barrel", barrel, [WOOD, IRON, DARK]),
    Prop("Crate", crate, [WOOD, DARK, IRON]),
    Prop("LogBench", log_bench, [WOOD, BARK, DARK]),
    Prop("Banner", banner, [CRIMSON, WOOD, GOLD]),
    Prop("Palisade", palisade, [WOOD, DARK, IRON]),
    Prop("Torch", torch, [WOOD, IRON, EMBER]),

    Prop("CryptGate", crypt_gate, [STONE, DARK, DARK]),
    Prop("Throne", throne, [STONE, DARK, DARK]),
    Prop("Altar", altar, [STONE, DARK, BONE]),
    Prop("Coffin", coffin, [STONE, DARK, BONE]),

    Prop("SoulShelf", soul_shelf, [WOOD, IRON, DARK]),
    Prop("BodyTable", body_table, [STONE, DARK, DARK]),
    Prop("BindingDevice", binding_device, [STONE, IRON, DARK]),
    Prop("UpgradePlinth", upgrade_plinth, [STONE, DARK, DARK]),

    Prop("Rock", rock, [STONE, DARK, DARK]),
    Prop("Sakura", sakura, [BARK, BLOSSOM, DARK]),
    Prop("Katana", katana, [STEEL, IRON, DARK]),
]


def unwrap(mesh):
    """
    Развёртка коробкой: грань проецируется на ту плоскость, к которой
    она ближе всего лежит.

    Без развёртки у меша нет ни одной координаты текстуры, и материал
    со сканом ложится одним цветом — тем, что в точке (0,0). Палатки
    и ящики так и стояли крашеными плоскостями, хотя текстуры в проекте
    уже были.

    Единица развёртки — метр мира. Сколько раз текстура ляжет на этот
    метр, решает тайлинг материала (`MaterialBuilder`), и решает в одном
    месте: иначе у каждой модели была бы своя плотность, и доски на ящике
    не совпали бы с досками на столе.
    """
    uv = mesh.uv_layers.new(name="UVMap")

    for poly in mesh.polygons:
        n = poly.normal
        axis = max(range(3), key=lambda i: abs(n[i]))

        for loop in poly.loop_indices:
            co = mesh.vertices[mesh.loops[loop].vertex_index].co
            if axis == 0:
                uv.data[loop].uv = (co.y, co.z)
            elif axis == 1:
                uv.data[loop].uv = (co.x, co.z)
            else:
                uv.data[loop].uv = (co.x, co.y)


def build(prop):
    bodies.wipe()

    b = Body()
    prop.build(b)

    mesh = bpy.data.meshes.new(prop.name)
    mesh.from_pydata(b.verts, [], b.faces)
    mesh.validate(verbose=False)
    unwrap(mesh)

    for label, rgba in prop.materials:
        m = bpy.data.materials.new(label)
        m.diffuse_color = rgba
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if bsdf is not None:
            bsdf.inputs["Base Color"].default_value = rgba
            bsdf.inputs["Roughness"].default_value = 0.72
        mesh.materials.append(m)

    for i, mat in enumerate(b.mats):
        if i < len(mesh.polygons):
            mesh.polygons[i].material_index = mat

    obj = bpy.data.objects.new(prop.name, mesh)
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
        bake_anim=False,
        axis_forward="-Z",
        axis_up="Y",
        path_mode="COPY",
        embed_textures=False,
    )

    allowed = set(bpy.ops.export_scene.fbx.get_rna_type().properties.keys())
    bpy.ops.export_scene.fbx(**{k: v for k, v in want.items() if k in allowed})


def preview(prop, obj, folder):
    folder = Path(folder)
    folder.mkdir(parents=True, exist_ok=True)

    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.color_type = "MATERIAL"
    sc.display.shading.light = "STUDIO"
    sc.display.shading.show_shadows = True
    sc.render.resolution_x = 420
    sc.render.resolution_y = 420
    sc.world = bpy.data.worlds.new("W")
    sc.world.color = (0.16, 0.16, 0.18)

    lo = Vector((99, 99, 99))
    hi = Vector((-99, -99, -99))
    for v in obj.data.vertices:
        lo = Vector(map(min, lo, v.co))
        hi = Vector(map(max, hi, v.co))
    mid = (lo + hi) * 0.5
    size = max(0.4, (hi - lo).length)

    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = size * 0.95
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.location = mid + Vector((size * 0.8, -size * 1.0, size * 0.55))
    cam.rotation_euler = (mid - cam.location).to_track_quat("-Z", "Y").to_euler()

    sc.render.filepath = str(folder / (prop.name + ".png"))
    bpy.ops.render.render(write_still=True)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

    def opt(key):
        return argv[argv.index(key) + 1] if key in argv else None

    only = opt("--prop")
    shots = opt("--preview")

    out = bodies.unity_root() / "Assets" / "Resources" / "Props"

    chosen = [p for p in PROPS if only is None or p.name == only]
    if not chosen:
        raise SystemExit("Нет такого предмета: " + str(only)
                         + ". Есть: " + ", ".join(p.name for p in PROPS))

    for prop in chosen:
        obj = build(prop)

        lo = min(v.co.z for v in obj.data.vertices)
        hi = max(v.co.z for v in obj.data.vertices)
        print("[ПРЕДМЕТЫ] {}: граней {}, высота {:.2f}, опора {:.2f}".format(
            prop.name, len(obj.data.polygons), hi - lo, lo))

        export(out / (prop.name + ".fbx"))
        if shots:
            preview(prop, obj, shots)

    print("[ПРЕДМЕТЫ] записано в " + str(out))


if __name__ == "__main__":
    main()
