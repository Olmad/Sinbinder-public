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
from mathutils import Quaternion, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import anatomy
import bodies
from bodies import BASE, Body, box, ring, sphere, tilt, tube
from sdf import Ball, Bar, Brick, Disc, Egg, Field, Plate, chain, loop, turn

import numpy as np

P = BASE

# Материалы. Порядок смысла тот же, что у тел: 0 — из чего сделано,
# 1 — чем отличается, 2 — тёмное.
CLOTH = ("Cloth", (0.215, 0.190, 0.165, 1.0))
LEATHER = ("Leather", (0.165, 0.125, 0.095, 1.0))
IRON = ("Iron", (0.330, 0.340, 0.355, 1.0))
BONE = ("Bone", (0.855, 0.830, 0.760, 1.0))
STRAW = ("Straw", (0.520, 0.455, 0.300, 1.0))
DARK = ("Dark", (0.060, 0.055, 0.050, 1.0))

# Тьма под капюшоном — не «тёмно-серый», а почти ноль: у Греховода
# лица нет, есть темнота и два глаза (слово автора, стиль Overlord).
# Имя «Eye» не случайно: по нему SinEyes находит, чему гореть.
VOID = ("Void", (0.020, 0.018, 0.022, 1.0))
EYE = ("Eye", (0.62, 0.60, 0.58, 1.0))
FEATHER = ("Feather", (0.085, 0.080, 0.090, 1.0))
GLASS = ("Glass", (0.300, 0.380, 0.330, 1.0))

Item = namedtuple("Item", "name bone build materials")


# ------------------------------------------------------------- головное

def hood(b):
    """
    Капюшон: полусфера с вырезом под лицо и валик по краю.

    Размеры считаны с черепа: голова у тел 0,118 роста в ширину,
    капюшон — 0,138, то есть на полтора сантиметра свободнее. Прежние
    0,176 были шире головы в полтора раза, и в игре это читалось
    не капюшоном, а шаром на плечах.
    """
    z = P["skull"]
    b.add(*sphere((0, 0.016, z + 0.008), 0.076, scale=(1.0, 1.08, 1.08),
                  segs=26, rings=16,
                  holes=[((0.0, -1.0, -0.22), 62.0), ((0.0, 0.0, -1.0), 44.0)]),
          bone="Head")
    b.add(*ring((0, 0.016, z - 0.026), 0.074, 0.011, scale=(1.0, 1.08, 1.0), segs=22),
          bone="Head", mat=1)


def wide_hat(b):
    """Широкополая шляпа ловчего: сверху видно её одну."""
    z = P["skull"] + 0.052
    b.add(*ring((0, 0.010, z), 0.100, 0.013, scale=(1.0, 1.0, 0.45), segs=24), bone="Head")
    b.add(*tube((0, 0.010, z - 0.006), (0, 0.010, z + 0.058), 0.062, 0.050, segs=18),
          bone="Head")
    b.add(*ring((0, 0.010, z + 0.010), 0.062, 0.009, scale=(1.0, 1.0, 0.8), segs=20),
          bone="Head", mat=1)


def straw_hat(b):
    """Соломенная шляпа крестьянина: поля шире, тулья ниже."""
    z = P["skull"] + 0.046
    b.add(*ring((0, 0.010, z), 0.108, 0.012, scale=(1.0, 1.0, 0.40), segs=24), bone="Head")
    b.add(*tube((0, 0.010, z - 0.004), (0, 0.010, z + 0.038), 0.064, 0.057, segs=16),
          bone="Head")


def inquisitor_cap(b):
    """
    Высокий колпак инквизитора. Он рангом выше охотников, и сверху
    это видно раньше всего остального: колпак — самая высокая точка
    силуэта.
    """
    z = P["skull"] + 0.040
    b.add(*ring((0, 0.010, z), 0.074, 0.011, scale=(1.0, 1.0, 0.55), segs=22), bone="Head")
    b.add(*tube((0, 0.010, z), (0, 0.010, z + 0.150), 0.061, 0.035, segs=16), bone="Head")
    b.add(*box((0, -0.058, z + 0.070), (0.030, 0.014, 0.090)), bone="Head", mat=1)


def circlet(b):
    """Обруч на лбу. Гордыня 90: он сам себя таким видит."""
    # Прижат ко лбу, а не парит над макушкой: первый заход дал нимб.
    b.add(*ring((0, 0.012, P["skull"] + 0.004), 0.061, 0.007, scale=(1.0, 1.10, 1.0), segs=20),
          bone="Head")


def shade(b):
    """
    Тень под капюшоном: лица нет, есть темнота и два глаза.

    Слово автора от 20 сентября — «как в Overlord». Сделано оболочкой
    поверх черепа, а не правкой тела: Греховод носит скелета, как и все
    прочие, и отличается от них тем, что надето, — тем же способом,
    которым инквизитор отличается от охотника (`22-LOOK.md`).

    Скорлупа на четыре миллиметра шире черепа и почти чёрная, глаза —
    отдельным материалом «Eye»: его и зажигает игра.
    """
    z = P["skull"]
    b.add(*sphere((0, 0.010, z), 0.064, scale=(0.95, 1.06, 1.12),
                  segs=24, rings=16), bone="Head")

    for side in (1, -1):
        b.add(*box((side * 0.022, -0.0585, z + 0.006), (0.019, 0.004, 0.007)),
              bone="Head", mat=1)


def crack(b):
    """Скол черепа: тёмная трещина через темя. Личная примета."""
    b.add(*box((0.017, 0.006, P["skull"] + 0.050), (0.011, 0.074, 0.026)), bone="Head")
    b.add(*box((0.034, -0.026, P["skull"] + 0.034), (0.009, 0.026, 0.034)), bone="Head")


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


def drape(z0, z1, r0, r1, spread=205.0, thick=0.010, segs=15, rows=6):
    """
    Полотно, обнимающее спину: дуга от плеча к подолу, расходящаяся
    книзу. Оболочка замкнутая — два слоя и кромка по краю, — потому что
    односторонняя ткань исчезает, стоит зайти спереди.

    Плащ до 20 сентября был коробкой 0,215 × 0,018 и читался доской
    во всю спину. Ткани нужна дуга: она ловит свет по-разному сверху
    и с боков, и только из-за этого выглядит тканью.
    """
    import math

    a0 = math.radians(90.0 - spread * 0.5)
    step = math.radians(spread) / segs

    verts, faces = [], []
    ring_len = (segs + 1) * 2          # внешний ряд и внутренний

    for row in range(rows + 1):
        t = row / rows
        z = z0 + (z1 - z0) * t
        r = r0 + (r1 - r0) * t

        for layer, dr in ((0, 0.0), (1, -thick)):
            for i in range(segs + 1):
                a = a0 + step * i
                verts.append((math.cos(a) * (r + dr),
                              math.sin(a) * (r + dr) * 0.88,
                              z))

    def at(row, layer, i):
        return row * ring_len + layer * (segs + 1) + i

    for row in range(rows):
        for i in range(segs):
            # наружная сторона
            faces.append([at(row, 0, i), at(row, 0, i + 1),
                          at(row + 1, 0, i + 1), at(row + 1, 0, i)])
            # изнанка — намотка в другую сторону
            faces.append([at(row, 1, i + 1), at(row, 1, i),
                          at(row + 1, 1, i), at(row + 1, 1, i + 1)])

        # боковые кромки
        faces.append([at(row, 0, 0), at(row + 1, 0, 0),
                      at(row + 1, 1, 0), at(row, 1, 0)])
        faces.append([at(row, 1, segs), at(row + 1, 1, segs),
                      at(row + 1, 0, segs), at(row, 0, segs)])

    # верхняя и нижняя кромки
    for i in range(segs):
        faces.append([at(0, 1, i), at(0, 1, i + 1), at(0, 0, i + 1), at(0, 0, i)])
        faces.append([at(rows, 0, i), at(rows, 0, i + 1),
                      at(rows, 1, i + 1), at(rows, 1, i)])

    return verts, faces


def cloak(b):
    """Длинный плащ следопыта: от плеч до колен, сзади."""
    sh = P["shoulder"]
    b.add(*ring((0, 0.010, sh + 0.018), 0.108, 0.016, scale=(1.0, 0.95, 0.7), segs=20),
          bone="Chest")
    b.add(*drape(sh + 0.010, P["knee"] + 0.030, 0.110, 0.150, spread=152.0),
          bone="Spine", mat=1)


# ------------------------------------------------------------- оружие

# Оружие — в руке, а не слотом-кубом (автор, 27 сентября: «займись
# оружием»). Вещь висит на кости кисти и собрана в пространстве тела,
# как всё в гардеробе, — вокруг точки хвата: там, где кулак скелета
# сжат под рукоять (`anatomy.grip`). Рукоять идёт вперёд (−Y), костяшки
# смотрят вверх (+Z) — туда же смотрит лезвие топора и лезвие меча.
#
# Размеры — в долях роста, как тело: меч в 0,62 роста — длинный меч
# человека, у воина ростом 1,2 м он 0,74 м.
STEEL = ("Steel", (0.470, 0.480, 0.500, 1.0))
WOOD = ("Wood", (0.240, 0.170, 0.110, 1.0))


def held(a, b=0.0, c=0.0):
    """
    Точка оружия в пространстве тела: a — вдоль рукояти к острию,
    b — к костяшкам (вверх в Т-позе), c — вбок, наружу от тела.
    Начало — середина рукояти в правом кулаке.
    """
    x, _, z = anatomy.grip(P, -1)
    return (x - c, -a, z + b)


def blade(b, bone, a0, a1, width, thick, tip, mat):
    """
    Клинок: ромб в сечении, от пяты (a0) к острию (a1), сужаясь; последние
    `tip` — остриё. Гранями, а не гладко: сталь читается гранью.
    """
    stations = []
    steps = 6
    for i in range(steps + 1):
        t = i / steps
        a = a0 + (a1 - tip - a0) * t
        w = width * (1.0 - 0.45 * t)
        stations.append((a, w, thick * (1.0 - 0.35 * t)))

    verts, faces = [], []
    for a, w, th in stations:
        verts += [held(a, w * 0.5, 0.0), held(a, 0.0, th * 0.5),
                  held(a, -w * 0.5, 0.0), held(a, 0.0, -th * 0.5)]
    point = len(verts)
    verts.append(held(a1, 0.0, 0.0))
    base = 0
    # Пята закрыта.
    faces.append([3, 2, 1, 0])
    for s in range(steps):
        r0, r1 = s * 4, (s + 1) * 4
        for k in range(4):
            faces.append([r0 + k, r0 + (k + 1) % 4, r1 + (k + 1) % 4, r1 + k])
    last = steps * 4
    for k in range(4):
        faces.append([last + k, last + (k + 1) % 4, point])
    if bodies.signed_volume(verts, faces) < 0.0:
        faces = [f[::-1] for f in faces]
    b.add(verts, faces, bone=bone, mat=mat)


def grip_parts(b, bone, back, front, radius, wraps, leather, iron, pommel):
    """Рукоять в коже с обмотками и навершие."""
    b.add(*tube(held(-back), held(front), radius, radius * 0.95, segs=10), bone=bone, mat=leather,
          smooth=True)
    for i in range(wraps):
        a = -back + (back + front) * (i + 0.5) / wraps
        b.add(*tube(held(a - 0.004), held(a + 0.004), radius * 1.12, radius * 1.12, segs=10),
              bone=bone, mat=leather, smooth=True)
    if pommel > 0.0:
        b.add(*sphere(held(-back - pommel * 0.6), pommel, scale=(1.0, 0.8, 1.0), segs=12, rings=8),
              bone=bone, mat=iron, smooth=True)


def sword(b):
    """Меч: клинок с долом, гарда чуть вниз, рукоять в коже, навершие."""
    bone = "RightHand"
    grip_parts(b, bone, 0.048, 0.050, 0.0098, 4, 1, 2, 0.0150)
    # Гарда — поперёк клинка, по линии лезвий (к костяшкам и от них).
    guard = [held(0.056, -0.066, 0.0), held(0.054, -0.034, 0.0), held(0.053, 0.0, 0.0),
             held(0.054, 0.034, 0.0), held(0.056, 0.066, 0.0)]
    for p0, p1 in zip(guard, guard[1:]):
        b.add(*tube(p0, p1, 0.0072, 0.0072, segs=8), bone=bone, mat=2, smooth=True)
    for end in (guard[0], guard[-1]):
        b.add(*sphere(end, 0.0092, segs=10, rings=6), bone=bone, mat=2, smooth=True)
    blade(b, bone, 0.058, 0.520, 0.046, 0.0090, 0.070, 0)
    # Дол — тёмная полоса по плоскости клинка, с обеих сторон.
    for side in (1, -1):
        a0, a1 = 0.070, 0.330
        verts, faces = box(((held(a0)[0] + held(a1)[0]) * 0.5 - side * 0.0038,
                            (held(a0)[1] + held(a1)[1]) * 0.5, held(0)[2]),
                           (0.0016, a1 - a0, 0.0100))
        b.add(verts, faces, bone=bone, mat=2)


def dagger(b):
    """Кинжал: короткий клинок, малая гарда."""
    bone = "RightHand"
    grip_parts(b, bone, 0.044, 0.046, 0.0090, 3, 1, 2, 0.0120)
    b.add(*tube(held(0.052, -0.030), held(0.052, 0.030), 0.0062, 0.0062, segs=8),
          bone=bone, mat=2, smooth=True)
    blade(b, bone, 0.054, 0.215, 0.030, 0.0075, 0.040, 0)


def axe(b):
    """
    Топор: длинное топорище с железными кольцами и бородатое лезвие
    к костяшкам — как у образца `axe.glb`, но с одним лезвием: топор
    рядового, а не палача.
    """
    bone = "RightHand"
    b.add(*tube(held(-0.110), held(0.420), 0.0110, 0.0125, segs=10), bone=bone, mat=1, smooth=True)
    for a in (-0.100, 0.060, 0.330):
        b.add(*tube(held(a - 0.009), held(a + 0.009), 0.0138, 0.0138, segs=10), bone=bone, mat=2,
              smooth=True)

    f = Field()
    # Обух вокруг топорища.
    f.add(Egg(held(0.392, 0.0, 0.0), (0.0140, 0.030, 0.0160)))
    # Лезвие — пластина к костяшкам, с бородой к рукояти.
    edge = [held(0.360, 0.012), held(0.318, 0.070), held(0.296, 0.108), held(0.332, 0.121),
            held(0.392, 0.122), held(0.432, 0.104), held(0.425, 0.060), held(0.410, 0.012)]
    f.add(Plate(edge, 0.0036), k=0.010)
    # Шип обуха — назад, от лезвия.
    f.add(Bar(held(0.392, -0.012), held(0.392, -0.038), 0.0080, 0.0030), k=0.006)
    anatomy.part(b, f, 0.0014, bone, keep=900, reach=0.004, recolor=lambda c: np.zeros(len(c), int))


def club(b):
    """
    Дубина: суковатая, толстеющая к концу, с вбитыми гвоздями.
    Оружие того, у кого оружия не было, — зомби-крестьянина, ловчего.
    """
    bone = "RightHand"
    f = Field()
    f.add(Bar(held(-0.080), held(0.300), 0.0120, 0.0300))
    for a, b_, c, r in ((0.150, 0.016, 0.006, 0.010), (0.235, -0.020, -0.010, 0.012),
                        (0.270, 0.018, 0.012, 0.011)):
        f.add(Ball(held(a, b_, c), r), k=0.010)
    nails = []
    for i in range(7):
        ang = i * 2.39996
        a = 0.200 + 0.012 * i
        r = 0.020 + 0.0012 * i
        root = held(a, math.cos(ang) * r * 0.9, math.sin(ang) * r * 0.9)
        tip = held(a + 0.004, math.cos(ang) * (r + 0.020), math.sin(ang) * (r + 0.020))
        nail = Bar(root, tip, 0.0026, 0.0012)
        nails.append(nail)
        f.add(nail, k=0.0)

    def colour(centres):
        mats = np.zeros(len(centres), int)
        for nail in nails:
            mats[nail.dist(centres) < 0.0012] = 1
        return mats

    anatomy.part(b, f, 0.0016, bone, keep=1200, reach=0.006, recolor=colour)


def shield(b):
    """
    Круглый щит на левом предплечье, лицом вперёд: доски, железный
    обод и умбон. В Т-позе лицом к −Y — и в покое, с опущенной рукой,
    он висит сбоку лицом вперёд, а не смотрит в землю.
    """
    bone = "LeftLowerArm"
    el, wr, sh = P["elbow"], P["wrist"], P["shoulder"]
    centre = np.array([(el + wr) * 0.5, -0.042, sh])
    R = 0.150
    f = Field()
    f.add(Disc(tuple(centre), R, 0.0075, 0.0035, rot=turn(pitch=90.0)))
    # Обод — кольцо по краю, умбон — посередине.
    f.add(loop(tuple(centre + [0.0, -0.004, 0.0]), (R - 0.004, R - 0.004), turn(),
               lambda t: 0.0068, count=28, power=2.0), k=0.004)
    boss = centre + [0.0, -0.012, 0.0]
    f.add(Egg(tuple(boss), (0.036, 0.020, 0.036)), k=0.006)
    # Доски — швы тёмными прорезями.
    for x in (-0.06, 0.0, 0.06):
        f.cut(Brick(tuple(centre + [x + 0.03, -0.0075, 0.0]), (0.0014, 0.0020, R), 0.0), k=0.0)

    def colour(centres):
        rel = centres - centre
        radial = np.hypot(rel[:, 0], rel[:, 2])
        mats = np.zeros(len(centres), int)
        mats[radial > R - 0.014] = 1
        mats[(radial < 0.040) & (rel[:, 1] < -0.010)] = 1
        return mats

    anatomy.part(b, f, 0.0018, bone, keep=1600, reach=0.006, recolor=colour)


# ------------------------------------------------------------- в ножнах

# Оружие убрано — висит на тазу, там, где кисть его отпускает в клипе
# «убрать» (`retarget.py`, кадр посадки). Вещь «в ножнах» — то же оружие,
# переведённое из позы привязки кисти в позу кисти на этом кадре и оттуда
# в позу привязки таза: на кость таза она садится ровно туда, где её
# отпустила рука, и подмена в игре не видна (`Armament`).


def settled():
    """
    Перевод точки, привязанной к правой кисти, в точку, привязанную
    к тазу, — на кадре, где кисть отпускает рукоять. Считается по тем же
    поворотам, что и клип (`bodies.retargeted`), прямой кинематикой.
    """
    data = bodies.motion("Sheathe")
    i = data["settle"]
    row = data["frames"][i]
    made = bodies.bones(P)
    head = {n: Vector(h) for n, _, h, _ in made}
    turn_ = {n: Quaternion(row[n]) for n, _, _, _ in made}
    pos = {}
    for n, up, _, _ in made:
        if up is None:
            pos[n] = head[n] + Vector(data["hips"][i]) * P["hip"]
        else:
            pos[n] = pos[up] + turn_[up] @ (head[n] - head[up])

    def move(p):
        world = pos["RightHand"] + turn_["RightHand"] @ (Vector(p) - head["RightHand"])
        return tuple(head["Hips"] + turn_["Hips"].inverted() @ (world - pos["Hips"]))

    return move


def stowed(build):
    """Сборщик вещи «в ножнах»: то же оружие, переведённое на таз."""
    def made(b):
        tmp = Body()
        build(tmp)
        stowed_mesh(b, tmp)
    return made


def scabbard(b):
    """
    Ножны меча: кожа по клинку, железное устье и наконечник. Стоят
    на тазу всегда, пока у воина меч, — пустые, когда меч в руке.
    Строятся вокруг клинка в руке и переводятся на таз тем же ходом,
    что и меч: клинок входит в них без зазора.
    """
    tmp = Body()
    blade(tmp, "RightHand", 0.056, 0.500, 0.060, 0.020, 0.030, 0)
    tmp.add(*tube(held(0.050), held(0.080), 0.0170, 0.0165, segs=12), bone="RightHand", mat=1, smooth=True)
    tmp.add(*sphere(held(0.496), 0.0100, scale=(0.7, 1.0, 1.4), segs=10, rings=6), bone="RightHand", mat=1,
            smooth=True)
    # Ремень к поясу — от устья вверх, к тазу.
    tmp.add(*box(held(0.060, 0.0, -0.016), (0.012, 0.030, 0.050)), bone="RightHand", mat=2)
    stowed_mesh(b, tmp)


def dagger_sheath(b):
    """Ножны кинжала — короткие, кожа и железное устье."""
    tmp = Body()
    blade(tmp, "RightHand", 0.052, 0.200, 0.042, 0.017, 0.022, 0)
    tmp.add(*tube(held(0.048), held(0.070), 0.0140, 0.0135, segs=12), bone="RightHand", mat=1, smooth=True)
    stowed_mesh(b, tmp)


def stowed_mesh(b, tmp):
    """Перевести собранное у кисти на таз и сложить в b."""
    move = settled()
    base = len(b.verts)
    b.verts.extend(move(v) for v in tmp.verts)
    for f, m, sm in zip(tmp.faces, tmp.mats, tmp.smooth):
        b.faces.append([i + base for i in f])
        b.mats.append(m)
        b.smooth.append(sm)
    b.groups.setdefault("Hips", []).extend(range(base, base + len(tmp.verts)))


ITEMS = [
    Item("Hood",           "Head",  hood,            [CLOTH, LEATHER, DARK]),
    Item("WideHat",        "Head",  wide_hat,        [LEATHER, DARK, DARK]),
    Item("StrawHat",       "Head",  straw_hat,       [STRAW, LEATHER, DARK]),
    Item("InquisitorCap",  "Head",  inquisitor_cap,  [CLOTH, IRON, DARK]),
    Item("Circlet",        "Head",  circlet,         [IRON, IRON, DARK]),
    Item("Crack",          "Head",  crack,           [DARK, DARK, DARK]),
    Item("Shade",          "Head",  shade,           [VOID, EYE, DARK]),

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

    Item("Sword",          "RightHand",    sword,    [STEEL, LEATHER, IRON]),
    Item("Dagger",         "RightHand",    dagger,   [STEEL, LEATHER, IRON]),
    Item("Axe",            "RightHand",    axe,      [STEEL, WOOD, IRON]),
    Item("Club",           "RightHand",    club,     [WOOD, IRON, DARK]),
    Item("Shield",         "LeftLowerArm", shield,   [WOOD, IRON, DARK]),

    # Убранное оружие и ножны — на тазу (`Armament` показывает одно
    # из двух: в руке или в ножнах).
    Item("SwordStowed",    "Hips", stowed(sword),    [STEEL, LEATHER, IRON]),
    Item("DaggerStowed",   "Hips", stowed(dagger),   [STEEL, LEATHER, IRON]),
    Item("AxeStowed",      "Hips", stowed(axe),      [STEEL, WOOD, IRON]),
    Item("ClubStowed",     "Hips", stowed(club),     [WOOD, IRON, DARK]),
    Item("Scabbard",       "Hips", scabbard,         [LEATHER, IRON, DARK]),
    Item("DaggerSheath",   "Hips", dagger_sheath,    [LEATHER, IRON, DARK]),
]


# ------------------------------------------------------------- скелет

# С 27 сентября скелет стоит на своих суставах (`bodies.SKELETON`: уже
# в плечах и бёдрах, длиннее в ноге) и носит череп по эталону
# (`anatomy.skull_field`). Вещи, сшитые по общему телу, на нём висели бы:
# наплечник — в двух сантиметрах от плеча, лук — в двух за спиной, обруч —
# вокруг пустоты, тень Греховода — с зубами наружу. Для него — своя сборка
# в `Wear/Skeleton`; `Wardrobe` ищет вещь сперва там, потом в общей папке.
S = bodies.SKELETON

# Середина черепа — откуда пускать лучи к его поверхности.
SKULL_MIDDLE = (0.0, 0.006, 0.950)


def hug(k, y0=0.0):
    """Сжать к оси тела по горизонтали: одежда на узком теле."""
    return lambda v: [(x * k, y0 + (y - y0) * k, z) for x, y, z in v]


def shift(dy):
    """Придвинуть к спине: вещь за спиной на узкой спине."""
    return lambda v: [(x, y + dy, z) for x, y, z in v]


def about(centre, k, dz=0.0):
    """
    Уменьшить вокруг точки и опустить на dz: наплечник на костлявом
    плече. Уменьшенный, он висел над ним на полтора сантиметра —
    у кости под ним нет мышцы, на которую он ложился.
    """
    cx, cy, cz = centre
    return lambda v: [(cx + (x - cx) * k, cy + (y - cy) * k, cz + (z - cz) * k + dz)
                      for x, y, z in v]


def around_arm(k):
    """Сжать вокруг оси плеча: лента на кости, а не на мышце."""
    z0 = S["shoulder"]
    return lambda v: [(x, y * k, z0 + (z - z0) * k) for x, y, z in v]


def on_skull(field, origins, dirs):
    """Точки на поверхности черепа и нормали в них — по лучам изнутри."""
    pts, found = field.surface(origins, dirs, far=0.12)
    if not found.all():
        raise SystemExit("[ГАРДЕРОБ] луч не вышел из черепа — вещи не на чем лежать.")
    normals, _ = field.normal(pts, 0.0008)
    return pts, normals


def circlet_skull(b):
    """
    Обруч по черепу скелета: по поверхности, над надбровьем, к затылку
    чуть ниже — так его носят. Прежний, кольцом 0,061, на новом черепе
    висел бы в сантиметре от висков.
    """
    skull, _ = anatomy.skull_field(S)
    n = 36
    turns = [2.0 * math.pi * i / n for i in range(n)]
    # Спереди (−Y) выше, сзади ниже: лоб — 0,962, затылок — 0,952.
    origins = [(0.0, 0.006, 0.957 - 0.005 * math.sin(t)) for t in turns]
    dirs = [(math.cos(t), math.sin(t), 0.0) for t in turns]
    pts, nrm = on_skull(skull, origins, dirs)

    r = 0.0034
    band = pts + nrm * (r + 0.0004)
    f = Field()
    for i in range(n):
        f.add(Bar(band[i], band[(i + 1) % n], r), k=0.0015)

    # Бляха на лбу — единственное, что отличает обруч от ремня.
    front = int(n * 0.75)
    f.add(Egg(tuple(band[front] + nrm[front] * 0.0012), (0.0055, 0.0030, 0.0075)), k=0.0015)
    anatomy.part(b, f, 0.0007, "Head", keep=1400, reach=0.003)


def crack_skull(b):
    """
    Скол черепа скелета: рваная тёмная трещина от лба через темя назад,
    отросток к виску и выбоина там, где пришёлся удар. Лежит на кости —
    по лучам к поверхности нового черепа, — и шире прежней: её обязаны
    видеть сверху, иначе Кир не отличим от Ждана (22-LOOK.md §5).
    """
    skull, _ = anatomy.skull_field(S)

    main = [(0.30, -0.62, 0.72), (0.36, -0.42, 0.84), (0.24, -0.22, 0.95), (0.31, 0.00, 0.95),
            (0.20, 0.22, 0.95), (0.27, 0.44, 0.86), (0.18, 0.62, 0.74)]
    branch = [(0.24, -0.22, 0.95), (0.50, -0.20, 0.84), (0.70, -0.06, 0.70)]

    f = Field()
    for path, r0, r1 in ((main, 0.0034, 0.0018), (branch, 0.0026, 0.0014)):
        pts, nrm = on_skull(skull, [SKULL_MIDDLE] * len(path), path)
        # Утоплена: наружу выходит на миллиметр — трещина, а не проволока.
        line = pts - nrm * 0.0009
        radii = [r0 + (r1 - r0) * i / (len(line) - 1) for i in range(len(line))]
        f.add(chain([tuple(p) for p in line], radii), k=0.0012)

    # Выбоина: тёмное пятно там, где трещина начинается от удара.
    pts, nrm = on_skull(skull, [SKULL_MIDDLE], [main[1]])
    f.add(Ball(tuple(pts[0] - nrm[0] * 0.0030), 0.0068), k=0.002)
    anatomy.part(b, f, 0.0007, "Head", keep=900, reach=0.003, paint=False)


def shade_skull(b):
    """
    Тень под капюшоном Греховода — по черепу скелета: тот же череп
    без глазниц и ноздри, раздутый на четыре миллиметра, и два уголька
    там, где глаза. Прежняя скорлупа была шаром под старый череп: новый,
    вытянутый вперёд, выходил бы из неё зубами и носом.
    """
    skull, _ = anatomy.skull_field(S, cuts=False)
    # Лицо — одной гладкой формой: тьма, а не череп из тьмы. Без неё
    # скулы и челюсть читались из-под капюшона чёрной бородой.
    skull.add(Egg((0, -0.034, 0.912), (0.036, 0.030, 0.052)), k=0.016)
    skull.grow(0.004)
    anatomy.part(b, skull, 0.0020, "Head", keep=1400, reach=0.010, paint=False)

    # Глаза — щелями, чуть сведёнными к переносице, как у прежней тени:
    # точки на её месте читались пуговицами, а не угольками.
    for side in (1, -1):
        pts, nrm = on_skull(skull, [(side * 0.0186, -0.030, 0.9335)], [(side * 0.25, -1.0, 0.0)])
        at = pts[0] - nrm[0] * 0.0012
        verts, faces = box(tuple(at), (0.0175, 0.0050, 0.0048))
        verts = tilt(verts, tuple(at), "z", side * 16.0)
        verts = tilt(verts, tuple(at), "y", side * 12.0)
        b.add(verts, faces, bone="Head", mat=1)


def raven_mantle_skeleton(b):
    """
    Плащ из вороньих перьев на скелете — сзади и с боков, спереди открыт.
    Общий обнимает грудь по кругу: на живом перья лежат на теле, а на
    скелете висели на пустоте клетки кольцом — бочкой вокруг рёбер.
    """
    sh = P["shoulder"]
    k = 0.82
    b.add(*ring((0, 0.008, sh + 0.024), 0.096 * k, 0.018, scale=(1.0, 0.95, 0.65), segs=22),
          bone="Chest")
    for i in range(18):
        a = math.radians(14 + i * 19)
        if math.sin(a) < -0.35:
            continue
        back = 0.5 + 0.5 * math.sin(a)
        length = 0.055 + 0.135 * back
        r = (0.090 + 0.012 * back) * k
        x, y = math.cos(a) * r, math.sin(a) * r * 0.92
        b.add(*box((x, y + 0.008, sh + 0.012 - length * 0.5), (0.022, 0.014, length)),
              bone="Chest", mat=1)


def cowl_skeleton(b):
    """
    Капюшон скелета — куколь, а не шапка. Автор, 27 сентября: «почини
    голову Греховода» — прежний, шаром с вырезом, сидел на голове шлемом,
    и тень под ним читалась забралом.

    Куколь больше головы и выдвинут вперёд: край выреза впереди лица,
    лицо — в глубине, в тени. Сверху и сзади — лёгкий острый клюв,
    снизу — воротник, лежащий на плечах. Изнанка красится тёмным сама:
    она зажата — полость внутри, — а краска идёт по зажатости поля
    (`anatomy.part`).
    """
    f = Field()
    f.add(Egg((0.0, 0.004, 0.946), (0.069, 0.088, 0.080)))
    f.add(Egg((0.0, 0.040, 0.992), (0.038, 0.058, 0.040)), k=0.022)
    # Воротник — раструб на плечи, продолжение капюшона вниз.
    f.add(Egg((0.0, 0.002, 0.866), (0.086, 0.090, 0.030)), k=0.024)

    # Полость — голова с тенью и запасом на ткань.
    f.cut(Egg((0.0, -0.002, 0.936), (0.058, 0.079, 0.071)), k=0.006)
    f.cut(Egg((0.0, 0.004, 0.862), (0.070, 0.074, 0.034)), k=0.010)
    # Вырез лица — высокий овал спереди; край — впереди лица.
    f.cut(Egg((0.0, -0.096, 0.922), (0.037, 0.050, 0.054)), k=0.010)
    # Снизу открыт — голова входит.
    f.cut(Egg((0.0, 0.010, 0.824), (0.060, 0.060, 0.030)), k=0.004)

    # Ткань или тьма, без полутени: полутень — материал «Кожа», и край
    # выреза ложился бы светлыми заплатами.
    anatomy.part(b, f, 0.0016, "Head", keep=2400, reach=0.020, deep=0.50, dim=0.50)


def flasks_skeleton(b):
    """
    Склянки алхимика на ремне. У скелета пояса нет, и склянки,
    сжатые к узкому тазу, висели бы в воздухе перед ним — ремень
    лежит на крыльях таза, склянки — на ремне.
    """
    tmp = Body()
    flasks(tmp)
    b.add(hug(0.80)(tmp.verts), tmp.faces, bone="Spine", mat=tmp.mats)
    b.add(*ring((0, 0.006, P["hip"] + 0.048), 0.078, 0.006, scale=(1.0, 0.74, 1.0), segs=22),
          bone="Spine", mat=0)


# Что носит скелет своего: (имя, свой сборщик или None, подгонка общего).
# Остального скелету шить не нужно — общая вещь на нём сидит.
SKELETON_ITEMS = [
    ("Circlet", circlet_skull, None),
    ("Crack", crack_skull, None),
    ("Shade", shade_skull, None),
    ("Flasks", flasks_skeleton, None),
    ("Hood", cowl_skeleton, None),
    ("PauldronLeft", None, about((S["shoulder_x"] - 0.004, 0.004, S["shoulder"] + 0.008), 0.80, -0.012)),
    ("PauldronRight", None, about((-(S["shoulder_x"] - 0.004), 0.004, S["shoulder"] + 0.008), 0.80, -0.012)),
    ("BrotherBand", None, around_arm(0.55)),
    ("Bow", None, shift(-0.022)),
    ("Cloak", None, hug(0.74, y0=0.010)),
    ("RavenMantle", raven_mantle_skeleton, None),
    # Оружие — то же, но на суставах скелета: кулак у него свой.
    ("Sword", None, None),
    ("Dagger", None, None),
    ("Axe", None, None),
    ("Club", None, None),
    ("Shield", None, None),
    ("SwordStowed", None, None),
    ("DaggerStowed", None, None),
    ("AxeStowed", None, None),
    ("ClubStowed", None, None),
    ("Scabbard", None, None),
    ("DaggerSheath", None, None),
]


# ----------------------------------------------------------------- сборка

def build(item, fit=None):
    bodies.wipe()

    b = Body()
    item.build(b)
    if fit is not None:
        b.verts = fit(b.verts)

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
            mesh.polygons[i].use_smooth = b.smooth[i]

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


# Живое тело (охотники) — с 1 октября своё (`flesh.py`): уже в плечах,
# ноги ближе, голова человеческая. Оружие для него — своё (кулак на своих
# суставах), и вещи, сшитые по прежнему телу, подогнаны: наплечники
# тонули в новых плечах, табард висел доской в двух сантиметрах от груди,
# плащ резал плечи, капюшон сидел на лице шлемом.
LIVING = next(sh.parts for sh in bodies.SHELLS if sh.name == "Living")
ARMS = ["Sword", "Dagger", "Axe", "Club", "Shield", "SwordStowed", "DaggerStowed", "AxeStowed",
        "ClubStowed", "Scabbard", "DaggerSheath"]


def cowl_living(b):
    """
    Капюшон охотника — по голове человека: просторнее головы, край выреза
    впереди лица, лицо видно; воротник лежит на плечах. Тот же путь, что
    куколь Греховода (`cowl_skeleton`), — только лицо открыто: охотники —
    люди, их узнают в лицо.
    """
    f = Field()
    f.add(Egg((0.0, 0.010, 0.944), (0.073, 0.094, 0.082)))
    f.add(Egg((0.0, 0.050, 0.992), (0.040, 0.060, 0.040)), k=0.022)
    # Ворот капюшона — узкий, по шее, внутри ворота куртки: широкий
    # (как у куколя Греховода) читался надувным кругом.
    f.add(Egg((0.0, 0.008, 0.856), (0.072, 0.070, 0.022)), k=0.024)
    f.cut(Egg((0.0, 0.004, 0.932), (0.062, 0.085, 0.075)), k=0.006)
    f.cut(Egg((0.0, 0.010, 0.852), (0.052, 0.052, 0.026)), k=0.008)
    f.cut(Egg((0.0, -0.104, 0.918), (0.044, 0.062, 0.062)), k=0.010)
    f.cut(Egg((0.0, 0.010, 0.806), (0.064, 0.064, 0.030)), k=0.004)
    anatomy.part(b, f, 0.0016, "Head", keep=2400, reach=0.020, deep=0.50, dim=0.50)


def hat_fit(k=0.86, dz=-0.006):
    """
    Головной убор, сшитый по прежней голове (полуширина 0,06), — на голову
    из `flesh.py` (0,05): уже по ширине, на той же высоте, чуть ниже.
    Сжимать и по высоте нельзя: тулья тогда кончается ниже макушки,
    и голова пробивает шляпу. Без подгонки шляпа висела с зазором вокруг
    головы — нимбом.
    """
    return lambda v: [(x * k, 0.012 + (y - 0.012) * k, z + dz) for x, y, z in v]


LIVING_ITEMS = [(name, None, None) for name in ARMS] + [
    ("InquisitorCap", None, hat_fit()),
    ("WideHat", None, hat_fit()),
    ("Hood", cowl_living, None),
    # Плащ шире в плечах; обод спереди прижат к груди — иначе он стоял
    # полкой перед курткой.
    ("Cloak", None, lambda v: [(x * 1.22, y if y >= 0.0 else y * 0.78, z) for x, y, z in v]),
    ("Tabard", None, shift(0.016)),
    ("PauldronLeft", None, about((LIVING["shoulder_x"] - 0.004, 0.004, LIVING["shoulder"] + 0.008), 1.04, 0.008)),
    ("PauldronRight", None, about((-(LIVING["shoulder_x"] - 0.004), 0.004, LIVING["shoulder"] + 0.008), 1.04, 0.008)),
]

# Зомби — голова из `flesh.py` на общих плечах и руках: подгоняем только
# то, что сидит на голове. Оружие и прочее — общее.
ZOMBIE = next(sh.parts for sh in bodies.SHELLS if sh.name == "Zombie")
ZOMBIE_ITEMS = [("StrawHat", None, hat_fit()), ("Hood", None, hat_fit(0.92, -0.004))]

# Призрак — капюшон из савана (`flesh.ghost`) шире и выше головы: шляпа
# поверх него — шире и с высокой тульей, иначе висит на макушке
# капюшона, а капюшон пробивает тулью.
GHOST = next(sh.parts for sh in bodies.SHELLS if sh.name == "Ghost")
GHOST_BRIM = BASE["skull"] + 0.046


def over_hood(v):
    return [(x * 1.12, 0.012 + (y - 0.012) * 1.12, GHOST_BRIM - 0.014 + (z - GHOST_BRIM) * 1.4)
            for x, y, z in v]


GHOST_ITEMS = [("StrawHat", None, over_hood), ("WideHat", None, over_hood)]

# Оболочка → (её пропорции, её вещи).
SHELL_ITEMS = {"Skeleton": (S, SKELETON_ITEMS), "Living": (LIVING, LIVING_ITEMS),
               "Zombie": (ZOMBIE, ZOMBIE_ITEMS), "Ghost": (GHOST, GHOST_ITEMS)}


def build_for_shell(parts, entry):
    """
    Вещь оболочки: общая, собранная на её суставах и подогнанная, или своя.
    Сборщики читают пропорции из P — на время сборки это пропорции оболочки.
    """
    global P
    name, own, fit = entry
    base = next(i for i in ITEMS if i.name == name)
    P = parts
    try:
        return build(Item(name, base.bone, own or base.build, base.materials), fit)
    finally:
        P = BASE


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

    def opt(key):
        return argv[argv.index(key) + 1] if key in argv else None

    only = opt("--item")
    shots = opt("--preview")

    out = bodies.unity_root() / "Assets" / "Resources" / "Wear"

    chosen = [i for i in ITEMS if only is None or i.name == only]
    if not chosen and not any(e[0] == only for _, es in SHELL_ITEMS.values() for e in es):
        raise SystemExit("Нет такой части: " + str(only)
                         + ". Есть: " + ", ".join(i.name for i in ITEMS))

    for item in chosen:
        obj = build(item)
        print("[ГАРДЕРОБ] {}: кость {}, граней {}".format(
            item.name, item.bone, len(obj.data.polygons)))

        export(out / (item.name + ".fbx"))
        if shots:
            preview(item, obj, shots)

    for shell, (parts, entries) in SHELL_ITEMS.items():
        for entry in entries:
            if only is not None and entry[0] != only:
                continue
            obj = build_for_shell(parts, entry)
            print("[ГАРДЕРОБ] {}/{}: граней {}".format(shell, entry[0], len(obj.data.polygons)))
            export(out / shell / (entry[0] + ".fbx"))
            if shots:
                preview(Item(shell + "-" + entry[0], None, None, None), obj, shots)

    print("[ГАРДЕРОБ] записано в " + str(out))


if __name__ == "__main__":
    main()
