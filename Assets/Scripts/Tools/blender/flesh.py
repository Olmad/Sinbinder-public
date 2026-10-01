# Assets/Scripts/Tools/blender/flesh.py
"""
Тела из плоти и ткани: живой человек — оболочка Living (охотники,
инквизиторы, следопыты, ловчие).

До 1 октября человек (`bodies.build_human`) был собран из трубок и шара:
на портретах прогона охотник читался роботом в шлеме — торс-бочка,
голова-шар, ноги-трубы, плечи шире живого человека, ноги расставлены
куклой. Автор, 29 сентября: «займись наконец-то улучшением моделей».

Тот же путь, что у скелета (`anatomy.py`): формы — полем расстояний
(`sdf.py`), сверка — кадром рядом с образцами (X Bot из набора Mixamo —
пропорции, священник в плаще из `D:\\Claude\\Модели` — одежда). И две
вещи, которых у скелета нет, потому что кость не гнётся, а ткань гнётся:

* **Мягкая привязка.** Тело — одна поверхность от шеи до сапог, и каждая
  её вершина привязана к двум-трём костям с весами по расстоянию до них
  (`bind`). Рукав сгибается в локте, штанина — в колене, без щели между
  кусками, которая у прежнего человека раскрывалась на каждом шаге.
  Голова и кисти — отдельно и жёстко: они не гнутся.
* **Материал по форме.** У каждой формы свой материал — куртка, кожа,
  сапоги, штаны; грань берёт материал той формы, на чьей поверхности
  лежит (`owner`).

Ничего случайного: одно и то же поле — одна и та же сетка и те же веса.
"""

import math

import numpy as np

import anatomy
import bodies
import sdf
from sdf import Ball, Bar, Brick, Disc, Egg, Field, Plate, chain, turn

# Материалы живого тела. Первые четыре — как были (их порядок знает
# гардероб и SinEyes по именам), пятый — штаны: одним сукном с курткой
# фигура читалась комбинезоном.
CLOTH, SKIN, DARK, EYE, TROUSERS = 0, 1, 2, 3, 4

# Ширина шва между формами в привязке: вершина, лежащая на стыке двух
# форм, берёт кости обеих. Шире — мягче стык, но кости соседей тянут
# дальше, чем надо.
SEAM = 0.006

# Насколько кость тянет вершину, отстоящую от неё дальше ближайшей:
# вес падает в e раз на каждые SIGMA. Около двух сантиметров у человека
# ростом метр семьдесят — столько и занимает сгиб локтя или колена.
SIGMA = 0.012


class Sculpt:
    """
    Поле с памятью: каждая прибавленная форма помнит свой материал
    и свои кости. Вычитания помнить незачем — у выреза поверхности своей
    нет, она принадлежит той форме, из которой вырезали.

    rig — кости формы с весом-склонностью: {"LeftUpperArm": 1.0, …}.
    Склонность меньше единицы — кость тянет вершины этой формы слабее,
    чем ближе всех стоящая: полы куртки идут за тазом больше, чем за ногой.
    """

    def __init__(self):
        self.field = Field()
        self.parts = []

    def add(self, shapes, k=0.0, mat=CLOTH, rig=None):
        for s in (shapes if isinstance(shapes, (list, tuple)) else [shapes]):
            self.field.add(s, k=k)
            self.parts.append((s, mat, rig or {}))
        return self

    def cut(self, shapes, k=0.0):
        self.field.cut(shapes, k=k)
        return self


def distances(parts, points, far=1.0):
    """Расстояния от точек до каждой формы (S × N); вне рамки формы — far."""
    out = np.full((len(parts), len(points)), far)
    for i, (shape, _, _) in enumerate(parts):
        lo, hi = shape.lo - 0.05, shape.hi + 0.05
        inside = np.all((points >= lo) & (points <= hi), axis=1)
        if inside.any():
            out[i, inside] = shape.dist(points[inside])
    return out


def owner(parts, points):
    """Материал по форме: грань лежит на поверхности той, что ближе всех."""
    d = distances(parts, points)
    mats = np.array([m for _, m, _ in parts])
    return mats[np.argmin(d, axis=0)]


def segment(points, a, b):
    """Расстояние от точек до отрезка кости."""
    a, b = np.asarray(a, dtype=np.float64), np.asarray(b, dtype=np.float64)
    ab = b - a
    t = np.clip((points - a) @ ab / max(ab @ ab, 1e-12), 0.0, 1.0)
    return np.linalg.norm(points - (a + np.outer(t, ab)), axis=1)


def bind(parts, points, p, top=4):
    """
    Веса вершин по костям: список словарей {кость: вес}, по одному на вершину.

    Кость берёт вершину, только если её зовёт форма, на которой вершина
    лежит (rig формы), — иначе внутренняя сторона левого бедра тянулась
    бы за правым, а он ближе, чем кажется. Вершина на стыке двух форм
    (в пределах SEAM) берёт кости обеих — так стык не рвётся.

    Среди позванных костей вес падает с расстоянием до кости: ближайшая —
    больше всех, следующая — в e раз меньше на каждые SIGMA сверх того.
    """
    made = bodies.bones(p)
    names = [n for n, _, _, _ in made]
    index = {n: i for i, n in enumerate(names)}

    d = distances(parts, points)
    dmin = d.min(axis=0)
    member = np.exp(-np.clip(d - dmin, 0.0, None) / SEAM)
    member[d - dmin > 4.0 * SEAM] = 0.0

    called = np.zeros((len(names), len(points)))
    for s, (_, _, rig) in enumerate(parts):
        for bone, bias in rig.items():
            i = index[bone]
            called[i] = np.maximum(called[i], member[s] * bias)

    reach = np.stack([segment(points, h, t) for _, _, h, t in made])
    reach_masked = np.where(called > 0.02, reach, np.inf)
    nearest = reach_masked.min(axis=0)

    w = called * np.exp(-np.clip(reach - nearest, 0.0, None) / SIGMA)
    w[called <= 0.02] = 0.0

    out = []
    for j in range(len(points)):
        col = w[:, j]
        best = np.argsort(col)[::-1][:top]
        best = [i for i in best if col[i] > 0.03 * col[best[0]]]
        total = sum(col[i] for i in best)
        if total <= 0.0:
            # Никто не позвал — к ближайшей кости вообще: лучше так,
            # чем вершина, повисшая в покое при любом движении.
            i = int(np.argmin(reach[:, j]))
            out.append({names[i]: 1.0})
            continue
        out.append({names[i]: float(col[i] / total) for i in best})
    return out


def region(b, sculpt, cell, keep, p, rigid=None, paint=None):
    """
    Снять поверхность с поля, прорядить до keep треугольников, раскрасить
    по формам и привязать: rigid — кость, если жёстко (голова, кисть),
    иначе — веса по `bind`. paint(середины граней, материалы) → материалы —
    поправка раскраски там, где граница должна идти по линии, а не по
    стыку форм (подол куртки).
    """
    verts, faces = sculpt.field.mesh(cell)
    if len(faces) == 0:
        raise SystemExit("[ТЕЛА] поле пустое — нечего снимать.")
    if sdf.volume(verts, faces) <= 0.0:
        raise SystemExit("[ТЕЛА] грани намотаны внутрь — тело просвечивало бы.")

    raw = len(faces)
    verts, faces = anatomy.thin(verts, faces, keep)
    centres, _ = anatomy.facing(verts, faces)
    mats = owner(sculpt.parts, centres)
    if paint is not None:
        mats = paint(centres, mats)
    mats = mats.tolist()

    if rigid is not None:
        b.add(verts.tolist(), faces, bone=rigid, mat=mats, smooth=True)
    else:
        b.add_weighted(verts.tolist(), faces, bind(sculpt.parts, verts, p), mat=mats, smooth=True)
    print(f"[ТЕЛА] {rigid or 'тело'}: с поля {raw} граней, оставлено {len(faces)}")


# ------------------------------------------------------------- человек

def human(b, p):
    """Живой человек: тело, голова, две кисти."""
    torso(b, p)
    head(b, p)
    for s, tag in ((1, "Left"), (-1, "Right")):
        glove(b, p, s, tag)


def torso(b, p):
    """
    Тело от шеи до сапог — слоями, как шьют персонажей: куртка, штаны,
    сапоги, ремень, наручи — каждая своей поверхностью поверх нижней.
    Подол куртки тогда — настоящий край ткани над штаниной, а не граница
    цвета: одной поверхностью (первый заход 1 октября) она шла по крупным
    треугольникам рваной кромкой.

    Торс мужской — угловатый, а не песочными часами: грудная клетка
    скруглённым бруском, талия чуть уже груди, таз — уже плеч. Первый
    заход яйцами читался женской фигурой.

    Пропорции — человеческие, чуть геройские: плечи с курткой на 0,30
    роста (у живого — 0,26), голова чуть крупнее — сверху и в наезде её
    читают первой. Сверено с X Bot (Mixamo) и священником.
    """
    hip, knee, ankle = p["hip"], p["knee"], p["ankle"]
    waist, chest, sh, neck = p["waist"], p["chest"], p["shoulder"], p["neck"]
    sx, el, wr = p["shoulder_x"], p["elbow"], p["wrist"]
    lx, kx = p["leg_x"], p.get("knee_x", p["leg_x"])

    TORSO = {"Hips": 1.0, "Spine": 1.0, "Chest": 1.0}
    SKIRT = {"Hips": 1.0, "LeftUpperLeg": 0.6, "RightUpperLeg": 0.6}

    # ================================================================ куртка
    J = Sculpt()
    J.add(Brick((0, 0.004, chest + 0.010), (0.070, 0.032, 0.080), 0.032), mat=CLOTH, rig=TORSO)
    J.add(Egg((0, 0.006, waist), (0.086, 0.060, 0.075)), k=0.035, mat=CLOTH, rig=TORSO)
    J.add(Egg((0, 0.008, sh - 0.028), (0.122, 0.060, 0.046)), k=0.035, mat=CLOTH,
          rig={"Chest": 1.0, "LeftShoulder": 0.6, "RightShoulder": 0.6, "Neck": 0.3})
    for s in (1, -1):
        # Лопатки сзади — спина не доска.
        J.add(Egg((s * 0.050, 0.046, chest + 0.045), (0.048, 0.022, 0.055)), k=0.02, mat=CLOTH, rig=TORSO)
    # Полы — прямой трубой вокруг обоих бёдер, до середины бедра,
    # расходятся книзу. Скруглённые бруски, а не яйца: яйцо снизу
    # скругляется, и полы читались фартуком (кадр 1 октября), а бёдра
    # вылезали из-под них по бокам.
    # Полы начинаются под ремнём: выше они прятали ремень в себе.
    J.add(Brick((0, 0.004, hip - 0.010), (0.068, 0.044, 0.030), 0.028), k=0.03, mat=CLOTH, rig=SKIRT)
    J.add(Brick((0, 0.004, hip - 0.045), (0.076, 0.048, 0.030), 0.030), k=0.03, mat=CLOTH, rig=SKIRT)

    # Шея — в куртке же: её стык с воротом прячет ворот.
    J.add(Bar((0, 0.010, sh - 0.01), (0, 0.014, neck + 0.045), 0.033, 0.029), k=0.02, mat=SKIN,
          rig={"Chest": 0.7, "Neck": 1.0, "Head": 1.0})

    # Рукава: дельта, плечо, локоть, предплечье.
    for s, tag in ((1, "Left"), (-1, "Right")):
        ARM = {tag + "Shoulder": 0.6, tag + "UpperArm": 1.0, tag + "LowerArm": 1.0}
        FORE = {tag + "UpperArm": 1.0, tag + "LowerArm": 1.0, tag + "Hand": 0.6}
        J.add(Egg((s * (sx - 0.006), 0.006, sh + 0.004), (0.046, 0.044, 0.044)), k=0.03, mat=CLOTH,
              rig={"Chest": 0.5, tag + "Shoulder": 1.0, tag + "UpperArm": 1.0})
        J.add(Bar((s * sx, 0.004, sh), (s * (el - 0.01), 0.002, sh), 0.040, 0.032), k=0.02, mat=CLOTH, rig=ARM)
        J.add(Egg((s * (sx + el) * 0.5, -0.004, sh + 0.004), (0.060, 0.034, 0.033)), k=0.02, mat=CLOTH, rig=ARM)
        J.add(Ball((s * el, 0.004, sh), 0.031), k=0.015, mat=CLOTH, rig=FORE)
        J.add(Bar((s * el, 0.002, sh), (s * (wr - 0.02), 0.0, sh), 0.031, 0.026), k=0.015, mat=CLOTH, rig=FORE)

    # Борт — валик чуть правее середины от ворота до подола, пуговицы.
    # На поверхность — лучами изнутри: число на глаз оставило бы валик
    # висеть в полусантиметре.
    zs = [sh - 0.045 - i * (sh - 0.045 - (hip - 0.070)) / 13 for i in range(14)]
    front, _ = J.field.surface([(0.010, 0.0, z) for z in zs], [(0.0, -1.0, 0.0)] * len(zs), far=0.16)
    edge = [(x, y + 0.002, z) for x, y, z in front.tolist()]
    J.add(chain(edge, [0.0050] * len(edge)), k=0.003, mat=CLOTH,
          rig={"Hips": 1.0, "Spine": 1.0, "Chest": 1.0})
    for i in (1, 3, 5, 7):
        x, y, z = edge[i]
        J.add(Ball((x, y - 0.004, z), 0.0058), k=0.0, mat=DARK, rig=TORSO)

    # Ворот-стойка, открытый спереди: дуга от бока через затылок к боку.
    collar = [(math.sin(math.radians(a)) * 0.045, 0.012 + math.cos(math.radians(a)) * 0.042,
               neck - 0.008) for a in np.linspace(-130.0, 130.0, 12)]
    J.add(chain(collar, [0.009] * len(collar)), k=0.010, mat=CLOTH, rig={"Chest": 1.0, "Neck": 1.0})

    # Подол снизу открыт не будет — внутри штанина, и низ «трубы» полов
    # снизу никто не увидит; срез по линии подола — ровный край.
    J.cut(Brick((0, 0.004, hip - 0.070 - 0.20), (0.30, 0.30, 0.20), 0.0), k=0.0)
    region(b, J, 0.0040, 4200, p)

    # ================================================================ штаны
    T = Sculpt()
    T.add(Egg((0, 0.006, hip + 0.015), (0.080, 0.058, 0.055)), mat=TROUSERS,
          rig={"Hips": 1.0, "LeftUpperLeg": 0.5, "RightUpperLeg": 0.5})
    for s, tag in ((1, "Left"), (-1, "Right")):
        THIGH = {"Hips": 0.5, tag + "UpperLeg": 1.0, tag + "LowerLeg": 1.0}
        SHIN = {tag + "UpperLeg": 1.0, tag + "LowerLeg": 1.0, tag + "Foot": 0.4}
        T.add(Bar((s * lx, 0.004, hip + 0.01), (s * kx, 0.0, knee + 0.03), 0.049, 0.038), k=0.02,
              mat=TROUSERS, rig=THIGH)
        T.add(Egg((s * (lx + kx) * 0.5, -0.008, (hip + knee) * 0.5 + 0.02), (0.046, 0.048, 0.090)), k=0.02,
              mat=TROUSERS, rig=THIGH)
        T.add(Ball((s * kx, -0.004, knee), 0.037), k=0.015, mat=TROUSERS, rig=SHIN)
        T.add(Bar((s * kx, 0.0, knee), (s * kx, 0.004, ankle + 0.10), 0.035, 0.030), k=0.015,
              mat=TROUSERS, rig=SHIN)
        T.add(Egg((s * kx, 0.016, knee - 0.075), (0.033, 0.035, 0.068)), k=0.015, mat=TROUSERS, rig=SHIN)
    region(b, T, 0.0042, 2600, p)

    # ================================================================ сапоги
    for s, tag in ((1, "Left"), (-1, "Right")):
        BOOT = {tag + "LowerLeg": 1.0, tag + "Foot": 1.0}
        FOOT = {tag + "LowerLeg": 0.4, tag + "Foot": 1.0, tag + "Toes": 1.0}
        B = Sculpt()
        B.add(Bar((s * kx, 0.004, 0.040), (s * kx, 0.006, knee - 0.085), 0.038, 0.041), mat=DARK, rig=BOOT)
        B.add(Disc((s * kx, 0.006, knee - 0.085), 0.045, 0.012, 0.006), k=0.004, mat=DARK, rig=BOOT)
        B.add(Egg((s * kx, -0.040, 0.032), (0.036, 0.074, 0.032)), k=0.012, mat=DARK, rig=FOOT)
        B.add(Egg((s * kx, 0.026, 0.030), (0.032, 0.032, 0.030)), k=0.010, mat=DARK, rig=BOOT)
        # Подошва — по контуру ступни, а не доской: брусок торчал
        # спереди и сзади скейтом.
        B.add(Egg((s * kx, -0.031, 0.007), (0.037, 0.087, 0.008)), k=0.006, mat=DARK, rig=FOOT)
        region(b, B, 0.0036, 900, p)

    # ================================================== ремень и наручи
    R = Sculpt()
    R.add(Egg((0, 0.006, waist - 0.022), (0.094, 0.067, 0.013)), mat=DARK, rig=TORSO)
    front, _ = R.field.surface([(0.0, 0.0, waist - 0.022)], [(0.0, -1.0, 0.0)], far=0.12)
    R.add(Brick((0, front[0][1] - 0.003, waist - 0.022), (0.012, 0.005, 0.011), 0.003), k=0.002, mat=DARK,
          rig=TORSO)
    R.add(Brick((0.088, -0.020, waist - 0.052), (0.014, 0.022, 0.028), 0.008), k=0.004, mat=DARK,
          rig={"Hips": 1.0})
    region(b, R, 0.0030, 900, p)

    for s, tag in ((1, "Left"), (-1, "Right")):
        A = Sculpt()
        A.add(Bar((s * (el + 0.05), 0.002, sh), (s * (wr - 0.008), 0.0, sh), 0.032, 0.028), mat=DARK,
              rig={tag + "LowerArm": 1.0, tag + "Hand": 0.3})
        region(b, A, 0.0030, 400, p)


def head(b, p):
    """
    Голова: череп, челюсть, скулы, надбровье, нос, уши, глазницы
    с угольками глаз (материал «Глаз» — их красит SinEyes), коротко
    стриженные волосы. Жёстко на кости головы.

    Размер — под шапки и капюшоны гардероба: они сшиты по прежней голове
    (полуширина 0,059), новая — 0,052 с волосами: капюшон сидит свободнее,
    а не тесней.
    """
    z = p["skull"]
    S = Sculpt()

    S.add(Egg((0, 0.012, z + 0.012), (0.050, 0.060, 0.058)), mat=SKIN)
    S.add(Egg((0, -0.018, z - 0.028), (0.043, 0.044, 0.042)), k=0.02, mat=SKIN)
    S.add(Egg((0, -0.043, z - 0.056), (0.020, 0.016, 0.016)), k=0.012, mat=SKIN)
    for s in (1, -1):
        S.add(Egg((s * 0.036, -0.006, z - 0.040), (0.012, 0.022, 0.020)), k=0.012, mat=SKIN)
        S.add(Egg((s * 0.032, -0.040, z - 0.010), (0.014, 0.012, 0.010)), k=0.010, mat=SKIN)
        S.add(Egg((s * 0.048, 0.012, z - 0.010), (0.006, 0.011, 0.016)), k=0.004, mat=SKIN)
    S.add(Bar((-0.034, -0.050, z + 0.016), (0.034, -0.050, z + 0.016), 0.008, 0.008), k=0.012, mat=SKIN)
    S.add(Bar((0, -0.052, z + 0.012), (0, -0.064, z - 0.020), 0.006, 0.009), k=0.008, mat=SKIN)
    S.add(Ball((0, -0.064, z - 0.022), 0.010), k=0.006, mat=SKIN)

    # Волосы — шапка чуть больше черепа сверху и сзади, внутри — спереди.
    S.add(Egg((0, 0.020, z + 0.022), (0.053, 0.064, 0.052)), k=0.004, mat=DARK)

    for s in (1, -1):
        S.cut(Egg((s * 0.021, -0.054, z + 0.000), (0.014, 0.012, 0.008)), k=0.006)
    for s in (1, -1):
        S.add(Egg((s * 0.021, -0.045, z + 0.000), (0.010, 0.005, 0.0045)), k=0.0, mat=EYE)
    S.cut(Brick((0, -0.058, z - 0.044), (0.013, 0.012, 0.0012), 0.0), k=0.0015)

    region(b, S, 0.0016, 2600, p, rigid="Head")


def glove(b, p, s, tag):
    """
    Кулак в кожаной перчатке, сжатый под рукоять (`anatomy.grip`): оружие
    гардероба садится в него так же, как в кулак скелета.
    """
    wr, sh = p["wrist"], p["shoulder"]
    gx, gz = wr + anatomy.GRIP_AHEAD, sh - anatomy.GRIP_BELOW
    wrap = anatomy.GRIP_R + 0.0065
    S = Sculpt()

    # Раструб перчатки поверх наруча и ладонь.
    S.add(Bar((s * (wr - 0.022), 0.0, sh), (s * (wr + 0.006), 0.0, sh), 0.030, 0.026), mat=DARK)
    S.add(Egg((s * (wr + 0.018), 0.0, sh - 0.002), (0.022, 0.026, 0.014)), k=0.008, mat=DARK)

    for y, r in ((-0.0125, 0.0058), (-0.0042, 0.0060), (0.0042, 0.0058), (0.0118, 0.0052)):
        knuckle = (s * gx, y, gz + wrap)
        S.add(Bar((s * (wr + 0.020), y * 0.5, sh), knuckle, r * 1.1, r * 1.15), k=0.004, mat=DARK)
        angle, at = math.pi / 2.0, knuckle
        for n, length in enumerate((0.022, 0.016, 0.012)):
            angle -= length / wrap
            nxt = (s * (gx + math.cos(angle) * wrap), y, gz + math.sin(angle) * wrap)
            S.add(Bar(at, nxt, r * (1.0 - 0.08 * n), r * (0.94 - 0.08 * n)), k=0.003, mat=DARK)
            at = nxt

    thumb = [(s * (wr + 0.012), -0.013, sh - 0.002), (s * (wr + 0.030), -0.023, sh - 0.010),
             (s * (gx - 0.006), -0.023, gz - 0.006), (s * (gx + 0.004), -0.019, gz - wrap + 0.002)]
    S.add(chain(thumb, [0.0072, 0.0068, 0.0062, 0.0056]), k=0.003, mat=DARK)

    region(b, S, 0.0012, 700, p, rigid=tag + "Hand")
