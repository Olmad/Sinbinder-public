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
ROOF = ("Old Boards", (0.120, 0.115, 0.110, 1.0))
ROOF_DARK = ("Old Boards Dark", (0.085, 0.080, 0.078, 1.0))   # тёс крыши — серый, выгоревший
DARK = props.DARK
STONE = props.STONE
IRON = props.IRON
STRAW = ("Thatch", (0.300, 0.245, 0.150, 1.0))


# ---------------------------------------------------------------- изба

def _board(x, y0, y1, z0, h0, h1, thick):
    """Вертикальная доска со скошенным верхом: низ на z0, верх — h0 у y0 и h1 у y1; толщина по X."""
    a, c = x - thick / 2, x + thick / 2
    v = [(a, y0, z0), (a, y1, z0), (a, y1, z0 + h1), (a, y0, z0 + h0),
         (c, y0, z0), (c, y1, z0), (c, y1, z0 + h1), (c, y0, z0 + h0)]
    f = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    return v, f

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


class _Mute:
    """Тело, что ничего не берёт: чтобы из одной избы собрать только стены или только крышу."""
    def add(self, *a, **k):
        pass


def izba(b, part="all"):
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

    # Части: стены с полом и крыша — раздельно (IzbaWalls, IzbaRoof): крыша гаснет, когда Греховод внутри.
    bw = b if part != "roof" else _Mute()
    br = b if part != "walls" else _Mute()
    # Пол — тёс по лагам, вровень с нижним венцом: внутри ходят.
    bw.add(*box((0, 0, 0.31), (W - 0.18, D - 0.18, 0.05)), bone="", mat=1)

    # Валуны-подкладки под углами и серединами — сруб стоит не на земле.
    for i, (x, y) in enumerate(((-hw, -hd), (hw, -hd), (hw, hd), (-hw, hd), (0, -hd), (0, hd))):
        bw.add(*sphere((x, y, 0.10), 0.26, scale=(1.2, 1.0, 0.6), segs=8, rings=5), bone="", mat=4)

    # Венцы: длинные стены (вдоль X) и короткие (вдоль Y) — со сдвигом на полвенца, чтобы углы сцепились.
    for k in range(courses):
        z = 0.30 + k * step
        zy = z + step * 0.5
        front_gap = [door] if z < door_top else []
        _wall_x(bw, -hd, -hw, hw, z, r, gaps=front_gap)
        _wall_x(bw, hd, -hw, hw, z, r)
        side_gap = [win] if win_z[0] < zy < win_z[1] else []
        _wall_y(bw, -hw, -hd, hd, zy, r)
        _wall_y(bw, hw, -hd, hd, zy, r, gaps=side_gap)

    top = 0.30 + courses * step       # верх сруба
    # Торцы брёвен на углах — светлые кружки: по ним сруб узнаётся сверху.
    for k in range(courses):
        z = 0.30 + k * step
        for x in (-hw - 0.28, hw + 0.28):
            for y in (-hd, hd):
                bw.add(*tube((x - 0.01 * math.copysign(1, x), y, z), (x + 0.005 * math.copysign(1, x), y, z), r * 0.92, r * 0.92, segs=8), bone="", mat=1)

    # Дверь: доски тёмные, две железные полосы; косяки.
    bw.add(*box((door[0] + 0.05, -hd + 0.47, 0.30 + door_top / 2 - 0.05), (0.06, 0.88, door_top - 0.1)), bone="", mat=2)   # распахнута внутрь
    for z in (0.75, 1.55):
        bw.add(*box((door[0] + 0.09, -hd + 0.47, z), (0.02, 0.80, 0.06)), bone="", mat=5)
    for x in door:
        bw.add(*box((x, -hd, 0.30 + door_top / 2), (0.10, 0.26, door_top)), bone="", mat=1)
    bw.add(*box((0, -hd, 0.30 + door_top + 0.04), (1.10, 0.26, 0.10)), bone="", mat=1)

    # Крыльцо: две ступени и площадка.
    bw.add(*box((0, -hd - 0.55, 0.22), (1.4, 0.9, 0.12)), bone="", mat=1)
    bw.add(*box((0, -hd - 1.15, 0.10), (1.2, 0.35, 0.10)), bone="", mat=1)

    # Окошко: тёмный проём, рама, ставни раскрыты.
    wz = (win_z[0] + win_z[1]) / 2 - 0.08
    bw.add(*box((hw - 0.02, 0, wz), (0.06, win[1] - win[0], win_z[1] - win_z[0])), bone="", mat=2)
    bw.add(*box((hw + 0.05, 0, wz), (0.05, win[1] - win[0] + 0.12, 0.08)), bone="", mat=1)       # подоконник
    bw.add(*box((hw + 0.05, 0, wz + (win_z[1] - win_z[0]) / 2 + 0.04), (0.05, win[1] - win[0] + 0.12, 0.08)), bone="", mat=1)
    for y in win:
        bw.add(*box((hw + 0.12, y + math.copysign(0.24, y), wz), (0.05, 0.42, 0.60)), bone="", mat=3)   # ставни — тёс крыши

    # Крыша: два ската из тёса (продольные доски), угол 38°, свес 0,45; конёк вдоль X.
    pitch = math.radians(38)
    over = 0.45
    run = hd + over
    # Скат кладётся от свеса: у края крыши (hd + over) — на уровне верха сруба, и выпуски брёвен под ним.
    rise = math.tan(pitch) * (hd + over)
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
            br.add(v, f, bone="", mat=3 if i % 2 == 0 else 6)   # через доску — темнее: тёс читается полосами
    # Причелины — доски по краю ската над фронтонами: закрывают торцы тёса, край крыши читается.
    for x in (-hw - over, hw + over):
        for side in (-1, 1):
            v, f = box((0, 0, 0), (0.07, slope + 0.1, 0.22))
            v = _tilted(v, -side * pitch, (x, side * run * 0.5, ridge_z - run * 0.5 * math.tan(pitch) + 0.02))
            br.add(v, f, bone="", mat=1)
    # Охлупень — тёсаное бревно на коньке, с выпуском.
    br.add(*tube((-hw - over - 0.15, 0, ridge_z + 0.08), (hw + over + 0.15, 0, ridge_z + 0.08), 0.15, 0.15, segs=8), bone="", mat=1)

    # Фронтоны — над короткими стенами (x = ±hw): вертикальные доски треугольником под скаты.
    for x in (-hw, hw):
        n = 9
        for i in range(n):
            y = -hd + (i + 0.5) * D / n
            # Верх доски скошен ровно под скат (было ступенькой — торчало пилой из-под причелины).
            y0, y1 = -hd + i * D / n, -hd + (i + 1) * D / n
            h0 = ridge_z - abs(y0) * math.tan(pitch) - top - 0.08
            h1 = ridge_z - abs(y1) * math.tan(pitch) - top - 0.08
            if y0 < 0 < y1:
                h0 = h1 = ridge_z - top - 0.08 - 0.02
            bw.add(*_board(x, y0, y1, top, max(0.03, h0), max(0.03, h1), 0.06), bone="", mat=1)

    # Труба: камень, выше конька.
    br.add(*box((hw * 0.45, hd * 0.35, top + rise * 0.5 + 0.4), (0.55, 0.55, rise + 1.0)), bone="", mat=4)
    br.add(*box((hw * 0.45, hd * 0.35, ridge_z + 0.95), (0.68, 0.68, 0.12)), bone="", mat=4)


def well(b):
    """
    Колодец с журавлём: сруб-квадрат из брёвен в обло (шесть венцов, 1,1 м), тёмная вода внутри; рядом
    столб-рогатина и длинный шест-журавль с ведром на верёвке и камнем-противовесом. Материалы: 0 — бревно,
    1 — торцы, 2 — тёмное (вода, верёвка), 3 — камень, 4 — железо (обод ведра).
    """
    hw = 0.55
    r = 0.10
    for k in range(6):
        z = 0.10 + k * 0.18
        _wall_x(b, -hw, -hw, hw, z, r, extend=0.18)
        _wall_x(b, hw, -hw, hw, z, r, extend=0.18)
        _wall_y(b, -hw, -hw, hw, z + 0.09, r, extend=0.18)
        _wall_y(b, hw, -hw, hw, z + 0.09, r, extend=0.18)
    b.add(*box((0, 0, 0.55), (2 * hw - 0.15, 2 * hw - 0.15, 0.04)), bone="", mat=2)   # вода
    # Столб-рогатина и журавль.
    post = (1.2, 0.0)
    b.add(*tube((post[0], post[1], 0), (post[0], post[1], 2.6), 0.11, 0.08, segs=8), bone="", mat=0)
    for s in (-1, 1):
        b.add(*tube((post[0], post[1], 2.45), (post[0] + 0.12 * s, post[1], 2.85), 0.05, 0.035, segs=6), bone="", mat=0)
    pivot = Vector((post[0], post[1], 2.70))
    long_end = Vector((-0.1, 0.0, 3.6))
    short_end = pivot + (pivot - long_end) * 0.45
    b.add(*tube(tuple(short_end), tuple(long_end), 0.06, 0.035, segs=7), bone="", mat=0)
    b.add(*sphere(tuple(short_end - Vector((0, 0, 0.12))), 0.22, scale=(1.1, 1.0, 0.8), segs=8, rings=5), bone="", mat=3)
    # Верёвка и ведро.
    bucket = Vector((long_end.x, long_end.y, 1.55))
    b.add(*tube(tuple(long_end), tuple(bucket + Vector((0, 0, 0.2))), 0.012, 0.012, segs=5), bone="", mat=2)
    b.add(*tube(tuple(bucket), tuple(bucket + Vector((0, 0, 0.22))), 0.13, 0.15, segs=10), bone="", mat=1)
    b.add(*tube(tuple(bucket + Vector((0, 0, 0.20))), tuple(bucket + Vector((0, 0, 0.23))), 0.155, 0.155, segs=10), bone="", mat=4)


def wattle(b):
    """
    Плетень — звено 2 м: семь кольев и прутья, вплетённые волной (через кол — то спереди, то сзади). Ставится
    цепью вокруг двора. Материалы: 0 — колья, 1 — прутья светлее.
    """
    stakes = 7
    for i in range(stakes):
        x = -1.0 + i * 2.0 / (stakes - 1)
        h = 1.05 + 0.08 * _wobble(i, 2)
        b.add(*tube((x, 0, -0.05), (x, 0, h), 0.04, 0.03, segs=6), bone="", mat=0)
    # Прутья часто и тонко — плетень читается стенкой, а не лестницей (превью 29.10).
    for row in range(13):
        z = 0.10 + row * 0.068
        pts = []
        for i in range(stakes):
            x = -1.0 + i * 2.0 / (stakes - 1)
            y = 0.032 * (1 if (i + row) % 2 == 0 else -1)
            pts.append((x, y, z + 0.01 * _wobble(row, i)))
        for p0, p1 in zip(pts, pts[1:]):
            b.add(*tube(p0, p1, 0.017, 0.017, segs=5), bone="", mat=1)


def woodpile(b):
    """Поленница под навесом: поленья торцами наружу, четыре ряда, два столба и скат из досок. 0 — поленья, 1 — торцы, 3 — навес."""
    for row in range(4):
        for i in range(9):
            x = -0.8 + i * 0.2 + (0.1 if row % 2 else 0.0)
            if x > 0.85: continue
            z = 0.12 + row * 0.18
            b.add(*tube((x, -0.3, z), (x, 0.3, z), 0.085, 0.085, segs=7), bone="", mat=0)
            b.add(*tube((x, 0.3, z), (x, 0.31, z), 0.08, 0.08, segs=7), bone="", mat=1)
    for x in (-1.0, 1.0):
        b.add(*tube((x, -0.45, 0), (x, -0.45, 1.25), 0.05, 0.045, segs=6), bone="", mat=0)
        b.add(*tube((x, 0.45, 0), (x, 0.45, 1.05), 0.05, 0.045, segs=6), bone="", mat=0)
    v, f = box((0, 0, 0), (2.3, 1.15, 0.05))
    b.add(_tilted(v, -math.radians(10), (0.0, 0.0, 1.17)), f, bone="", mat=3)


# ---------------------------------------------------------------- убранство избы
# Автор, 29.10: «в избу нужна возможность входить, а значит и внутренний интерьер (интерактивный)… разнообразный».
# Каждая вещь — своя модель: набор и места подбирает игра по избе (IzbaInterior), без жребия.

CLAY = ("Clay", (0.520, 0.470, 0.400, 1.0))          # побелка печи, затёртая сажей
SOOT = ("Soot", (0.060, 0.055, 0.052, 1.0))
CLOTH_RED = ("Red Cloth", (0.300, 0.080, 0.070, 1.0))
LINEN = ("Linen", (0.560, 0.520, 0.440, 1.0))
POT = ("Pot", (0.380, 0.230, 0.140, 1.0))


def pechka(b):
    """
    Русская печь: массив 1,6×1,9 м, высота 1,6; сверху лежанка (тёплое место), спереди устье с заслонкой и шесток,
    дымоход вверх до потолка. Материалы: 0 — побелка, 1 — сажа (устье, края), 2 — заслонка (железо), 3 — дерево
    (опечек, лавка у печи).
    """
    b.add(*box((0, 0, 0.30), (1.62, 1.92, 0.60)), bone="", mat=3)          # опечек — деревянное основание
    b.add(*box((0, 0, 1.08), (1.58, 1.88, 0.96)), bone="", mat=0)          # тело печи
    b.add(*box((0, 0.10, 1.62), (1.50, 1.60, 0.12)), bone="", mat=0)       # лежанка — верх
    b.add(*box((0, -0.96, 1.00), (0.62, 0.04, 0.46)), bone="", mat=1)      # устье — тёмный проём
    b.add(*box((0, -1.02, 0.98), (0.56, 0.03, 0.40)), bone="", mat=2)      # заслонка
    b.add(*box((0, -1.05, 0.70), (1.10, 0.25, 0.06)), bone="", mat=0)      # шесток
    b.add(*box((-0.45, -0.70, 2.20), (0.48, 0.48, 1.30)), bone="", mat=0)  # дымоход
    b.add(*box((-0.45, -0.70, 1.56), (0.50, 0.50, 0.05)), bone="", mat=1)  # закопчённый край
    b.add(*box((0.98, 0.0, 0.42), (0.34, 1.80, 0.06)), bone="", mat=3)     # лавка у печи


def table(b):
    """Стол из тёса на козлах и две лавки по бокам; на столе — краюха и миска. 0 — дерево, 1 — светлое (хлеб), 2 — горшок."""
    b.add(*box((0, 0, 0.76), (1.70, 0.80, 0.06)), bone="", mat=0)
    for x in (-0.65, 0.65):
        for s in (-1, 1):
            b.add(*tube((x, s * 0.28, 0.0), (x, -s * 0.28, 0.73), 0.035, 0.035, segs=6), bone="", mat=0)
    for y in (-0.70, 0.70):
        b.add(*box((0, y, 0.44), (1.60, 0.28, 0.05)), bone="", mat=0)
        for x in (-0.65, 0.65):
            b.add(*box((x, y, 0.21), (0.06, 0.22, 0.42)), bone="", mat=0)
    b.add(*sphere((0.35, 0.05, 0.84), 0.11, scale=(1.4, 1.0, 0.6), segs=10, rings=6), bone="", mat=1)   # краюха
    b.add(*tube((-0.30, -0.05, 0.79), (-0.30, -0.05, 0.86), 0.12, 0.15, segs=12), bone="", mat=2)        # миска


def bed(b):
    """Полати — дощатый настил на столбах с тюфяком и свёрнутым тулупом. 0 — дерево, 1 — тюфяк, 2 — тулуп (тёмная шерсть)."""
    b.add(*box((0, 0, 0.55), (1.95, 0.95, 0.07)), bone="", mat=0)
    for x in (-0.92, 0.92):
        for y in (-0.42, 0.42):
            b.add(*box((x, y, 0.27), (0.08, 0.08, 0.55)), bone="", mat=0)
    b.add(*box((0.05, 0, 0.65), (1.80, 0.85, 0.12)), bone="", mat=1)
    b.add(*tube((-0.70, -0.35, 0.78), (-0.70, 0.35, 0.78), 0.13, 0.13, segs=10), bone="", mat=2)


def pot_shelf(b):
    """Полка на стене с горшками и крынками — разной высоты, по номеру. 0 — дерево, 1 — глина, 2 — тёмное (горлышки)."""
    for z in (0.9, 1.45):
        b.add(*box((0, 0, z), (1.40, 0.30, 0.04)), bone="", mat=0)
        for i in range(5):
            x = -0.55 + i * 0.27
            h = 0.16 + 0.07 * ((i * 7 + int(z * 10)) % 3)
            r = 0.07 + 0.015 * (i % 2)
            # Крынка точёная, пузатая: дно, брюхо, узкое горло, отогнутый край (было конусом — читалось стаканом).
            shape = [(0.0, 0.0), (0.62, 0.0), (0.92, 0.18), (1.0, 0.42), (0.86, 0.68), (0.55, 0.84),
                     (0.52, 0.92), (0.64, 1.0), (0.50, 1.0), (0.0, 0.97)]
            v, f = props.lathe([(rr * r, zz * h) for rr, zz in shape], 14)
            b.add([(vx + x, vy, vz + z + 0.02) for vx, vy, vz in v], f, bone="", mat=1 if (i + int(z * 10)) % 3 else 2)
    for x in (-0.66, 0.66):
        b.add(*box((x, 0.10, 1.15), (0.04, 0.10, 0.85)), bone="", mat=0)


def spinner(b):
    """Прялка: донце-лавочка, стойка и лопасть с куделью. 0 — дерево, 1 — кудель (светлая), 2 — резьба тёмная."""
    b.add(*box((0, 0, 0.15), (0.60, 0.22, 0.05)), bone="", mat=0)
    b.add(*box((0.22, 0, 0.55), (0.06, 0.06, 0.80)), bone="", mat=0)
    b.add(*box((0.22, 0, 1.08), (0.05, 0.26, 0.34)), bone="", mat=0)
    b.add(*box((0.20, 0, 1.08), (0.02, 0.18, 0.06)), bone="", mat=2)
    b.add(*sphere((0.17, 0, 1.12), 0.12, scale=(0.6, 1.0, 1.6), segs=10, rings=6), bone="", mat=1)


def cradle(b):
    """Колыбель-зыбка на верёвках к потолку (пустая). 0 — дерево (лубяной короб), 1 — пелёнка, 2 — верёвки."""
    b.add(*box((0, 0, 0.75), (0.80, 0.45, 0.26)), bone="", mat=0)
    b.add(*box((0, 0, 0.82), (0.70, 0.36, 0.06)), bone="", mat=1)
    for x in (-0.38, 0.38):
        for y in (-0.20, 0.20):
            b.add(*tube((x, y, 0.88), (x * 0.2, y * 0.2, 2.1), 0.008, 0.008, segs=4), bone="", mat=2)


VILLAGE = [
    Prop("Pechka", pechka, [CLAY, SOOT, IRON, LOG]),
    Prop("IzbaTable", table, [LOG, LINEN, POT]),
    Prop("IzbaBed", bed, [LOG, LINEN, DARK]),
    Prop("PotShelf", pot_shelf, [LOG, POT, SOOT]),
    Prop("Spinner", spinner, [LOG, LINEN, DARK]),
    Prop("Cradle", cradle, [LOG, CLOTH_RED, DARK]),
    Prop("Izba", izba, [LOG, LOG_END, DARK, ROOF, STONE, IRON, ROOF_DARK]),
    Prop("IzbaWalls", lambda b: izba(b, "walls"), [LOG, LOG_END, DARK, ROOF, STONE, IRON, ROOF_DARK]),
    Prop("IzbaRoof", lambda b: izba(b, "roof"), [LOG, LOG_END, DARK, ROOF, STONE, IRON, ROOF_DARK]),
    Prop("Well", well, [LOG, LOG_END, DARK, STONE, IRON]),
    Prop("Wattle", wattle, [LOG, ("Withy", (0.300, 0.240, 0.165, 1.0)), DARK]),
    Prop("Woodpile", woodpile, [LOG, LOG_END, DARK, ROOF]),
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
