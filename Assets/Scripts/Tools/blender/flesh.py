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


# --------------------------------------------------------------- зомби

# Материалы зомби — номера из `bodies.SHELLS`: плоть, кость, пустота,
# глаз и пятым — лохмотья.
FLESH, ZBONE, ZHOLLOW, ZEYE, RAGS = 0, 1, 2, 3, 4


def borrowed(b, build, remap):
    """
    Деталь, собранная чужим сборщиком (рука скелета из `anatomy.py`),
    с материалами, переложенными на номера этой оболочки: у скелета
    кость — нулевой материал, у зомби нулевой — плоть.
    """
    tmp = bodies.Body()
    build(tmp)
    base = len(b.verts)
    b.verts.extend(tmp.verts)
    for f, m, sm in zip(tmp.faces, tmp.mats, tmp.smooth):
        b.faces.append([i + base for i in f])
        b.mats.append(remap.get(m, m))
        b.smooth.append(sm)
    for bone, idx in tmp.groups.items():
        b.groups.setdefault(bone, []).extend(i + base for i in idx)


def zombie(b, p):
    """
    Зомби: «гниющее тело помнит голод» (`DemoAssetBuilder.BuildShells`).
    Видно три вещи раньше цвета: раздутый живот, обглоданная до кости
    правая рука и рёбра наружу слева. Хромоту даёт походка (`bodies.Gait`).

    Тело — плотью, мягкой привязкой, как у живого (`human`): зомби был
    человеком. Поверх — лохмотья рубахи и штанов: те же формы, раздутые
    на семь миллиметров (`Field.grow`). Отдельными формами «чуть больше
    плоти» (первый заход) лохмотья после прореживания тонули в ней,
    и рубахи не было видно вовсе. Ноги босые. Лицо — полусгнившее.
    """
    hip, knee, ankle = p["hip"], p["knee"], p["ankle"]
    waist, chest, sh, neck = p["waist"], p["chest"], p["shoulder"], p["neck"]
    sx, el, wr = p["shoulder_x"], p["elbow"], p["wrist"]
    lx, kx = p["leg_x"], p.get("knee_x", p["leg_x"])
    TORSO = {"Hips": 1.0, "Spine": 1.0, "Chest": 1.0}

    hole = Egg((0.074, -0.026, chest - 0.010), (0.034, 0.050, 0.050))
    stump = Egg((-(sx + 0.030), 0.006, sh), (0.034, 0.050, 0.050))

    def torso(S, mat):
        """Корпус и левое плечо — одни формы для плоти и рубахи."""
        S.add(Egg((0, 0.008, hip + 0.020), (0.086, 0.064, 0.070)), mat=mat,
              rig={"Hips": 1.0, "Spine": 0.5, "LeftUpperLeg": 0.4, "RightUpperLeg": 0.4})
        # Живот висит вперёд и вниз — голод, а не сытость.
        S.add(Egg((0, -0.034, waist - 0.006), (0.090, 0.082, 0.088)), k=0.03, mat=mat, rig=TORSO)
        S.add(Brick((0, 0.008, chest + 0.020), (0.064, 0.032, 0.066), 0.032), k=0.035, mat=mat, rig=TORSO)
        S.add(Egg((0, 0.014, sh - 0.030), (0.120, 0.060, 0.046)), k=0.035, mat=mat,
              rig={"Chest": 1.0, "LeftShoulder": 0.6, "RightShoulder": 0.6, "Neck": 0.3})
        S.add(Egg((sx - 0.008, 0.008, sh + 0.002), (0.044, 0.042, 0.042)), k=0.03, mat=mat,
              rig={"Chest": 0.5, "LeftShoulder": 1.0, "LeftUpperArm": 1.0})
        S.add(Egg((-(sx - 0.012), 0.008, sh + 0.004), (0.040, 0.040, 0.040)), k=0.03, mat=mat,
              rig={"Chest": 0.5, "RightShoulder": 1.0})

    def thighs(S, mat):
        """Бёдра и колени — одни формы для плоти и штанов."""
        for s, tag in ((1, "Left"), (-1, "Right")):
            THIGH = {"Hips": 0.5, tag + "UpperLeg": 1.0, tag + "LowerLeg": 1.0}
            S.add(Bar((s * lx, 0.004, hip + 0.01), (s * kx, 0.0, knee + 0.03), 0.050, 0.036), k=0.02,
                  mat=mat, rig=THIGH)
            S.add(Ball((s * kx, -0.004, knee), 0.034), k=0.015, mat=mat,
                  rig={tag + "UpperLeg": 1.0, tag + "LowerLeg": 1.0, tag + "Foot": 0.5})

    # ============================================================ плоть
    F = Sculpt()
    torso(F, FLESH)
    F.add(Bar((0, 0.012, sh - 0.010), (0, 0.016, neck + 0.045), 0.032, 0.028), k=0.02, mat=FLESH,
          rig={"Chest": 0.7, "Neck": 1.0, "Head": 1.0})

    # Рёбра слева — в прорехе плоти, кость поверх тьмы.
    F.cut(hole, k=0.010)
    for dz in (-0.034, -0.012, 0.010, 0.032):
        z = chest - 0.010 + dz
        F.add(chain([(0.040, -0.058, z - 0.006), (0.066, -0.052, z - 0.002), (0.090, -0.028, z),
                     (0.098, 0.004, z + 0.002)], [0.0052, 0.0050, 0.0050, 0.0048]), k=0.004,
              mat=ZBONE, rig=TORSO)

    # Левая рука — во плоти; правая — культя, дальше кость.
    F.add(Bar((sx, 0.004, sh), (el - 0.01, 0.002, sh), 0.036, 0.030), k=0.02, mat=FLESH,
          rig={"LeftShoulder": 0.6, "LeftUpperArm": 1.0, "LeftLowerArm": 1.0})
    F.add(Ball((el, 0.004, sh), 0.029), k=0.015, mat=FLESH,
          rig={"LeftUpperArm": 1.0, "LeftLowerArm": 1.0})
    F.add(Bar((el, 0.002, sh), (wr - 0.006, 0.0, sh), 0.029, 0.022), k=0.015, mat=FLESH,
          rig={"LeftUpperArm": 0.6, "LeftLowerArm": 1.0, "LeftHand": 0.5})
    F.cut(stump, k=0.006)

    # Ноги: бёдра под штанами, голени и ступни — голые.
    thighs(F, FLESH)
    for s, tag in ((1, "Left"), (-1, "Right")):
        SHIN = {tag + "UpperLeg": 1.0, tag + "LowerLeg": 1.0, tag + "Foot": 0.5}
        FOOT = {tag + "LowerLeg": 0.4, tag + "Foot": 1.0, tag + "Toes": 1.0}
        F.add(Bar((s * kx, 0.0, knee), (s * kx, 0.004, ankle + 0.02), 0.030, 0.021), k=0.015,
              mat=FLESH, rig=SHIN)
        F.add(Egg((s * kx, 0.014, knee - 0.070), (0.028, 0.030, 0.060)), k=0.015, mat=FLESH, rig=SHIN)
        F.add(Egg((s * kx, -0.032, 0.020), (0.030, 0.062, 0.020)), k=0.012, mat=FLESH, rig=FOOT)
        F.add(Egg((s * kx, 0.016, 0.026), (0.024, 0.026, 0.026)), k=0.010, mat=FLESH,
              rig={tag + "LowerLeg": 1.0, tag + "Foot": 1.0})
        for i, off in enumerate((-0.016, -0.007, 0.002, 0.010, 0.017)):
            F.add(Ball((s * kx + s * off, -0.090 + abs(off) * 0.6, 0.010), 0.0075 - 0.0006 * i), k=0.004,
                  mat=FLESH, rig={tag + "Toes": 1.0, tag + "Foot": 0.4})

    inside = Egg(hole.c, (0.040, 0.056, 0.056))

    def flesh_paint(c, mats):
        # Тьма в прорехе: стенки ямы — пустота, кость — кость.
        mats = mats.copy()
        mats[(inside.dist(c) < 0.0) & (mats == FLESH)] = ZHOLLOW
        return mats

    region(b, F, 0.0040, 5600, p, paint=flesh_paint)

    # Правая рука — кость скелета (`anatomy.arm`), толще скелетной:
    # тонкая кость на теле в полтора раза шире скелета читалась прутом.
    # Кисть не толстим — её кулак держит рукоять оружия.
    def bone_arm(tmp):
        anatomy.arm(tmp, p, -1, "Right")
        for bone in ("RightUpperArm", "RightLowerArm"):
            for i in tmp.groups.get(bone, []):
                x, y, z = tmp.verts[i]
                tmp.verts[i] = (x, y * 1.45, sh + (z - sh) * 1.45)

    borrowed(b, bone_arm, {0: ZBONE, 1: ZBONE, 2: ZHOLLOW, 3: ZEYE})

    # Левая кисть — скрюченная, сжатая под рукоять, как у всех.
    claw = Sculpt()
    gx, gz = wr + anatomy.GRIP_AHEAD, sh - anatomy.GRIP_BELOW
    wrap = anatomy.GRIP_R + 0.0060
    claw.add(Egg((wr + 0.018, 0.0, sh - 0.002), (0.020, 0.024, 0.012)), mat=FLESH)
    for y, r in ((-0.0125, 0.0050), (-0.0042, 0.0052), (0.0042, 0.0050), (0.0118, 0.0045)):
        knuckle = (gx, y, gz + wrap)
        claw.add(Bar((wr + 0.020, y * 0.5, sh), knuckle, r * 1.1, r * 1.15), k=0.004, mat=FLESH)
        angle, at = math.pi / 2.0, knuckle
        for n, length in enumerate((0.022, 0.016, 0.012)):
            angle -= length / wrap
            nxt = (gx + math.cos(angle) * wrap, y, gz + math.sin(angle) * wrap)
            claw.add(Bar(at, nxt, r * (1.0 - 0.1 * n), r * (0.9 - 0.12 * n)), k=0.003, mat=FLESH)
            at = nxt
    claw.add(chain([(wr + 0.012, -0.013, sh - 0.002), (wr + 0.030, -0.022, sh - 0.010),
                    (gx - 0.006, -0.022, gz - 0.006), (gx + 0.004, -0.018, gz - wrap + 0.002)],
                   [0.0060, 0.0056, 0.0050, 0.0042]), k=0.003, mat=FLESH)
    region(b, claw, 0.0012, 600, p, rigid="LeftHand")

    # ========================================================= рубаха
    R = Sculpt()
    torso(R, RAGS)
    R.add(Bar((sx, 0.004, sh), (sx + 0.075, 0.002, sh), 0.036, 0.033), k=0.02, mat=RAGS,
          rig={"LeftShoulder": 0.6, "LeftUpperArm": 1.0})
    R.field.grow(0.007)
    # Без ворота у шеи, прорехи, рваный подол. Вырезы — с запасом на рост.
    R.cut(Egg((0, 0.012, neck - 0.006), (0.058, 0.058, 0.046)), k=0.006)
    R.cut(Egg(hole.c, (0.048, 0.068, 0.066)), k=0.006)
    R.cut(Egg(stump.c, (0.044, 0.062, 0.062)), k=0.006)
    R.cut(Brick((sx + 0.110, 0.0, sh), (0.040, 0.10, 0.10), 0.0), k=0.0)
    for x, y, z, r in ((-0.040, -0.110, chest - 0.040, 0.026), (0.030, 0.082, chest + 0.030, 0.024),
                       (-0.088, 0.050, waist + 0.020, 0.026)):
        R.cut(Ball((x, y, z), r), k=0.004)
    # Подол рубахи — над пупом: живот вываливается из-под неё. Рубаха
    # до пояса (первый заход) сливалась со штанами в комбинезон и прятала
    # главную примету зомби.
    # Зубцы — на самой кромке, а не над ней: над кромкой вырезы
    # выходили рядом круглых дыр.
    hem = waist - 0.020
    for i in range(11):
        a = 2.0 * math.pi * i / 11 + 0.3
        R.cut(Egg((math.sin(a) * 0.104, -0.030 + math.cos(a) * 0.094, hem + 0.010 + 0.008 * (i % 3)),
                  (0.020, 0.024, 0.022)), k=0.004)
    R.cut(Brick((0, 0, hem - 0.10), (0.30, 0.30, 0.10), 0.0), k=0.0)
    region(b, R, 0.0036, 2600, p)

    # ========================================================= штаны
    T = Sculpt()
    thighs(T, RAGS)
    T.add(Egg((0, 0.008, hip + 0.020), (0.086, 0.064, 0.070)), k=0.02, mat=RAGS,
          rig={"Hips": 1.0, "LeftUpperLeg": 0.4, "RightUpperLeg": 0.4})
    T.field.grow(0.007)
    for s in (1, -1):
        for i in range(5):
            a = 2.0 * math.pi * i / 5 + s * 0.5
            T.cut(Egg((s * kx + math.sin(a) * 0.044, math.cos(a) * 0.044, knee - 0.040 + 0.010 * (i % 2)),
                      (0.018, 0.018, 0.024)), k=0.004)
        T.cut(Brick((s * kx, 0.0, knee - 0.140), (0.08, 0.08, 0.10), 0.0), k=0.0)
    # Пояс штанов — под животом, на бёдрах.
    T.cut(Brick((0, 0, hip + 0.012 + 0.10), (0.30, 0.30, 0.10), 0.0), k=0.0)
    region(b, T, 0.0036, 2200, p)

    zombie_head(b, p)


def zombie_head(b, p):
    """
    Голова зомби: человеческая (`head`), но носа нет — дыра, глазницы
    запали, губ нет — зубы наружу, справа щека прорвана до кости,
    волосы клочьями — шапкой с проплешинами (шарики клочьев читались
    ушами). Жёстко на кости головы.
    """
    z = p["skull"]
    S = Sculpt()
    S.add(Egg((0, 0.012, z + 0.010), (0.050, 0.060, 0.056)), mat=FLESH)
    S.add(Egg((0, -0.018, z - 0.030), (0.042, 0.043, 0.040)), k=0.02, mat=FLESH)
    S.add(Egg((0, -0.040, z - 0.058), (0.020, 0.016, 0.015)), k=0.012, mat=FLESH)
    for s in (1, -1):
        S.add(Egg((s * 0.031, -0.040, z - 0.010), (0.014, 0.012, 0.010)), k=0.010, mat=FLESH)
        S.add(Egg((s * 0.048, 0.012, z - 0.010), (0.006, 0.011, 0.016)), k=0.004, mat=FLESH)
    S.add(Bar((-0.034, -0.050, z + 0.016), (0.034, -0.050, z + 0.016), 0.008, 0.008), k=0.012, mat=FLESH)
    # Волосы — шапка чуть больше черепа сверху и сзади…
    S.add(Egg((0, 0.020, z + 0.020), (0.053, 0.064, 0.052)), k=0.004, mat=ZHOLLOW)

    # …с проплешинами: темя и висок — до кожи.
    hair_holes = [Ball((0.018, 0.000, z + 0.066), 0.024), Ball((-0.036, 0.030, z + 0.040), 0.020),
                  Ball((0.030, 0.050, z + 0.030), 0.018)]

    # Нос — дыра; глазницы — глубокие; рот — без губ; щека справа — до кости.
    S.cut(Egg((0, -0.060, z - 0.010), (0.010, 0.020, 0.012)), k=0.004)
    for s in (1, -1):
        S.cut(Egg((s * 0.021, -0.056, z + 0.002), (0.016, 0.018, 0.012)), k=0.006)
        S.add(Egg((s * 0.021, -0.042, z + 0.002), (0.0085, 0.004, 0.006)), k=0.0, mat=ZEYE)
    S.cut(Brick((0, -0.060, z - 0.040), (0.020, 0.016, 0.008), 0.004), k=0.004)
    S.cut(Egg((-0.034, -0.040, z - 0.034), (0.016, 0.022, 0.014)), k=0.004)

    mouth = Brick((0, -0.050, z - 0.040), (0.026, 0.016, 0.010), 0.004)
    cheek = Egg((-0.034, -0.036, z - 0.034), (0.019, 0.025, 0.017))
    socket = [Egg((s * 0.021, -0.050, z + 0.002), (0.017, 0.016, 0.013)) for s in (1, -1)]
    nose = Egg((0, -0.054, z - 0.010), (0.012, 0.020, 0.014))

    def paint(c, mats):
        mats = mats.copy()
        dark = (nose.dist(c) < 0.0) | (mouth.dist(c) < 0.0) | (cheek.dist(c) < 0.0)
        for e in socket:
            dark |= e.dist(c) < 0.0
        mats[dark & (mats == FLESH)] = ZHOLLOW
        bald = np.zeros(len(c), dtype=bool)
        for h in hair_holes:
            bald |= h.dist(c) < 0.0
        mats[bald & (mats == ZHOLLOW) & ~dark] = FLESH
        return mats

    region(b, S, 0.0016, 2600, p, rigid="Head", paint=paint)

    # Зубы — наружу, как у черепа: в прорехе рта и щеки.
    for x in (-0.026, -0.018, -0.010, -0.003, 0.004, 0.011, 0.018):
        anatomy.pill(b, "Head", (x, -0.058 + abs(x) * 0.35, z - 0.036), (0.0030, 0.0022, 0.0042),
                     yaw=x * 900.0, mat=ZBONE, square=0.55)
        anatomy.pill(b, "Head", (x * 0.95, -0.056 + abs(x) * 0.35, z - 0.045), (0.0028, 0.0022, 0.0038),
                     yaw=x * 900.0, mat=ZBONE, square=0.55)


# ------------------------------------------------------------- призрак

# Материалы призрака — номера из `bodies.SHELLS`: дух, дух тусклее,
# пустота, глаз.
SPIRIT, SPIRIT_DIM, GHOLLOW, GEYE = 0, 1, 2, 3


def ghost(b, p):
    """
    Призрак: «бесплотный не может взять — только смотреть, как берут
    другие». Ног нет — саван ниже пояса сужается и рвётся на языки,
    которые не касаются земли. Капюшон, а в нём вместо лица — провал
    и два уголька. Руки длиннее человеческих, в рукавах-колоколах,
    кисти тонкие и раскрытые: тянется, но не берёт.

    Саван — мягкой привязкой (`bind`): верх идёт за грудью и плечами,
    низ — за тазом и не шагает (ног у призрака нет; походка ему
    почти не двигает их — `bodies.Gait`). Капюшон и лицо — жёстко
    на голове, кисти — на кистях.
    """
    hip, waist, chest, sh, neck = p["hip"], p["waist"], p["chest"], p["shoulder"], p["neck"]
    sx, el, wr, fg = p["shoulder_x"], p["elbow"], p["wrist"], p["finger"]
    TORSO = {"Hips": 1.0, "Spine": 1.0, "Chest": 1.0}
    ROBE = {"Hips": 1.0, "Spine": 0.5}

    # ============================================================ саван
    S = Sculpt()
    S.add(Egg((0, 0.010, sh - 0.026), (0.108, 0.056, 0.046)), mat=SPIRIT,
          rig={"Chest": 1.0, "LeftShoulder": 0.6, "RightShoulder": 0.6, "Neck": 0.3})
    S.add(Egg((0, 0.006, chest + 0.006), (0.082, 0.058, 0.090)), k=0.03, mat=SPIRIT, rig=TORSO)
    S.add(Egg((0, 0.006, waist - 0.004), (0.076, 0.056, 0.080)), k=0.03, mat=SPIRIT, rig=TORSO)
    # Ворот савана — к капюшону, и пелерина на плечах: без неё капюшон
    # сидел на тонкой шее, как на палке.
    S.add(Bar((0, 0.012, sh - 0.010), (0, 0.016, neck + 0.030), 0.040, 0.034), k=0.02, mat=SPIRIT,
          rig={"Chest": 0.7, "Neck": 1.0, "Head": 0.5})
    S.add(Egg((0, 0.014, neck - 0.012), (0.080, 0.070, 0.040)), k=0.025, mat=SPIRIT,
          rig={"Chest": 1.0, "Neck": 0.8})

    # Подол: колоколом от пояса вниз, сужаясь; дальше — языки.
    for z0, r0, z1, r1 in ((hip + 0.02, 0.084, 0.38, 0.096), (0.38, 0.096, 0.27, 0.082),
                           (0.27, 0.082, 0.20, 0.064)):
        S.add(Bar((0, 0.010, z0), (0, 0.016, z1), r0, r1), k=0.03, mat=SPIRIT, rig=ROBE)

    # Рукава — колоколом: узко у плеча, широко у кисти, рваный край.
    for s, tag in ((1, "Left"), (-1, "Right")):
        ARM = {tag + "Shoulder": 0.6, tag + "UpperArm": 1.0, tag + "LowerArm": 1.0}
        S.add(Egg((s * (sx - 0.010), 0.008, sh + 0.002), (0.040, 0.040, 0.040)), k=0.03, mat=SPIRIT,
              rig={"Chest": 0.5, tag + "Shoulder": 1.0, tag + "UpperArm": 1.0})
        S.add(Bar((s * sx, 0.004, sh), (s * el, 0.004, sh - 0.004), 0.030, 0.034), k=0.02, mat=SPIRIT, rig=ARM)
        S.add(Bar((s * el, 0.004, sh - 0.004), (s * (wr - 0.020), 0.006, sh - 0.012), 0.034, 0.050), k=0.02,
              mat=SPIRIT, rig={tag + "UpperArm": 0.6, tag + "LowerArm": 1.0})
        # Пустота внутри рукава — кисть выходит из тьмы.
        S.cut(Bar((s * (el + 0.060), 0.006, sh - 0.010), (s * (wr + 0.020), 0.006, sh - 0.014), 0.026, 0.042),
              k=0.004)
        for j in range(3):
            ang = 2.0 * math.pi * j / 3 + s * 0.4
            S.cut(Ball((s * (wr - 0.018), 0.006 + math.cos(ang) * 0.050, sh - 0.012 + math.sin(ang) * 0.050),
                       0.016), k=0.004)

    # Подол — срезом, с зубцами по самой кромке; языки — после среза,
    # и он их не трогает (поле применяет действия по порядку). Зубцы над
    # кромкой (первый заход) выходили рядом круглых дыр.
    hem = 0.235
    S.cut(Brick((0, 0, hem - 0.20), (0.30, 0.30, 0.20), 0.0), k=0.0)
    for i in range(10):
        a = 2.0 * math.pi * i / 10 + 0.2
        S.cut(Egg((math.cos(a) * 0.080, 0.014 + math.sin(a) * 0.072, hem + 0.004 + 0.008 * (i % 3)),
                  (0.020, 0.020, 0.020)), k=0.004)
    # Языки — пять рваных полос вниз и чуть назад, разной длины: савану
    # не на что опереться. Числа — по кругу через золотой угол, а не жребий.
    for i in range(5):
        a = math.radians(-60.0 + i * 30.0) + math.pi / 2.0
        r = 0.060
        top = (math.cos(a) * r, 0.016 + math.sin(a) * r * 0.6 - 0.020, hem + 0.020)
        length = 0.15 + 0.05 * ((i * 0.618) % 1.0)
        mid = (top[0] * 1.15, top[1] + 0.020, top[2] - length * 0.55)
        end = (top[0] * 1.30, top[1] + 0.050, max(0.012, top[2] - length))
        S.add(chain([top, mid, end], [0.030, 0.018, 0.004]), k=0.020, mat=SPIRIT, rig=ROBE)


    # Тьма — только внутри рукава: та же форма, что вырез, на пару
    # миллиметров шире. Шире (первый заход) — и чернела внешняя сторона.
    sleeve_dark = [Bar((s * (el + 0.060), 0.006, sh - 0.010), (s * (wr + 0.020), 0.006, sh - 0.014), 0.028, 0.044)
                   for s in (1, -1)]

    def paint(c, mats):
        mats = mats.copy()
        # Изнутри рукава — тьма, складки савана снизу — тусклее.
        for bar in sleeve_dark:
            mats[(bar.dist(c) < 0.0) & (mats == SPIRIT)] = GHOLLOW
        mats[(c[:, 2] < 0.30) & (mats == SPIRIT)] = SPIRIT_DIM
        return mats

    region(b, S, 0.0040, 5200, p, paint=paint)

    # =================================================== капюшон и лик
    z = p["skull"]
    H = Sculpt()
    H.add(Egg((0, 0.016, z + 0.010), (0.066, 0.078, 0.072)), mat=SPIRIT)
    # Острый край капюшона сзади-сверху — силуэт сверху не шар.
    H.add(Bar((0, 0.040, z + 0.050), (0, 0.086, z + 0.030), 0.034, 0.010), k=0.020, mat=SPIRIT)
    H.cut(Egg((0, -0.076, z - 0.002), (0.040, 0.052, 0.056)), k=0.012)
    # Лик в глубине — тусклый череп без челюсти, глазницы — провалы.
    H.add(Egg((0, -0.010, z - 0.006), (0.040, 0.044, 0.050)), k=0.006, mat=SPIRIT_DIM)
    for s in (1, -1):
        H.cut(Egg((s * 0.018, -0.052, z + 0.004), (0.013, 0.016, 0.015)), k=0.004)
    hood_in = Egg((0, -0.040, z - 0.002), (0.052, 0.050, 0.062))

    def hood_paint(c, mats):
        mats = mats.copy()
        # Изнанка капюшона вокруг лика — пустота: лицо выплывает из тьмы.
        mats[(hood_in.dist(c) < 0.0) & (mats == SPIRIT)] = GHOLLOW
        return mats

    region(b, H, 0.0016, 2400, p, rigid="Head", paint=hood_paint)
    for s in (1, -1):
        anatomy.pill(b, "Head", (s * 0.018, -0.040, z + 0.004), (0.0080, 0.0040, 0.0075),
                     mat=GEYE, segs=10, rings=6)

    # ================================================ кисти — пустые
    for s, tag in ((1, "Left"), (-1, "Right")):
        K = Sculpt()
        # Кисть и пальцы — толще, чем «тонкие»: проволочные пальцы
        # (первый заход, 3 мм) издали не читались вовсе.
        K.add(Bar((s * (wr - 0.030), 0.006, sh - 0.012), (s * (wr + 0.006), 0.004, sh - 0.012), 0.012, 0.014),
              mat=SPIRIT_DIM)
        K.add(Egg((s * (wr + 0.018), 0.002, sh - 0.012), (0.020, 0.024, 0.008)), k=0.006, mat=SPIRIT_DIM)
        # Длинные пальцы — раскрыты веером и чуть согнуты вниз: тянется.
        for j, (dy, r) in enumerate(((-0.020, 0.0056), (-0.007, 0.0060), (0.007, 0.0058), (0.019, 0.0050))):
            base = (s * (wr + 0.030), dy * 0.7, sh - 0.012)
            mid = (s * (wr + 0.030 + 0.034), dy * 1.2, sh - 0.018)
            tip = (s * (fg + 0.010), dy * 1.6, sh - 0.032)
            K.add(chain([base, mid, tip], [r, r * 0.85, r * 0.55]), k=0.003, mat=SPIRIT_DIM)
        thumb = [(s * (wr + 0.010), -0.016, sh - 0.012), (s * (wr + 0.030), -0.030, sh - 0.016),
                 (s * (wr + 0.046), -0.036, sh - 0.026)]
        K.add(chain(thumb, [0.0064, 0.0056, 0.0036]), k=0.003, mat=SPIRIT_DIM)
        region(b, K, 0.0010, 700, p, rigid=tag + "Hand")
