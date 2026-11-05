#!/usr/bin/env python3
"""Крысиное семейство: тела шести крыс в OBJ — и превью.

    python generate_rat_models.py            — записать OBJ в Assets/Resources/Rats/Models
    python generate_rat_models.py --preview  — нарисовать D:/Claude/runs/sheets/rats-preview.png

05.11 переделано «под крыс» (автор: были мышата из мультика): уши малые и
отнесены назад, глаза по бокам головы, морда длинная и острая, спина горбом
к крупу, голова ниже спины. Хвост — не модель: его строит игра трубкой из колец
(RatTail.cs), чтобы он гнулся волной; превью рисует его той же формулой.

Нужны только numpy и Pillow. Нос — к +Z, верх — +Y, лапы на Y = 0.
Части — отдельные «o», Unity красит их по имени (RatFamilyVisual.Pick).
"""
import sys
from math import sin, cos, pi, radians
from pathlib import Path
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Resources/Rats/Models'
NAMES = ['Mother', 'Pip', 'Dot', 'Sprig', 'Moss', 'Tuck']
TITLES = ['Мама-крыса', 'Пискун', 'Пятнышко', 'Прыг', 'Мох', 'Соня']

# Длина тела, полнота, голова, уши, морда. Крысята — головастее и курносее:
# так у молодых крыс и есть.
SHAPE = [
    (1.00, 1.08, 1.00, 1.00, 1.00),
    (0.95, 0.95, 1.08, 1.00, 0.90),
    (0.92, 0.92, 1.10, 1.08, 0.88),
    (1.00, 0.88, 1.06, 1.00, 0.92),
    (0.90, 1.00, 1.10, 0.95, 0.88),
    (0.90, 1.12, 1.12, 0.95, 0.84),
]

# Те же числа, что в RatFamilyVisual (Coats и части) — для превью.
COATS = [(.27, .25, .23), (.40, .31, .24), (.36, .36, .34), (.47, .39, .29), (.27, .29, .25), (.45, .41, .38)]


def tail_root(i):
    """Корень хвоста у крупа, в единицах модели (RatFamilyVisual.TailRoot)."""
    L = SHAPE[i][0]
    return np.array([0.0, 0.20, -0.66 * L])


# ---------- сфера ----------

def icosphere(sub):
    t = (1 + 5 ** .5) / 2
    v = [(-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0), (0, -1, t), (0, 1, t), (0, -1, -t), (0, 1, -t),
         (t, 0, -1), (t, 0, 1), (-t, 0, -1), (-t, 0, 1)]
    f = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6),
         (7, 1, 8), (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5), (2, 4, 11), (6, 2, 10),
         (8, 6, 7), (9, 8, 1)]
    verts = [np.array(p, float) / np.linalg.norm(p) for p in v]
    for _ in range(sub):
        cache, nf = {}, []

        def mid(a, b):
            key = (min(a, b), max(a, b))
            if key not in cache:
                m = verts[a] + verts[b]
                verts.append(m / np.linalg.norm(m))
                cache[key] = len(verts) - 1
            return cache[key]
        for a, b, c in f:
            ab, bc, ca = mid(a, b), mid(b, c), mid(c, a)
            nf += [(a, ab, ca), (b, bc, ab), (c, ca, bc), (ab, bc, ca)]
        f = nf
    return np.array(verts), np.array(f, int)


_SPHERES = {s: icosphere(s) for s in (1, 2)}


def rot(rx=0.0, ry=0.0, rz=0.0):
    x, y, z = radians(rx), radians(ry), radians(rz)
    X = np.array([[1, 0, 0], [0, cos(x), -sin(x)], [0, sin(x), cos(x)]])
    Y = np.array([[cos(y), 0, sin(y)], [0, 1, 0], [-sin(y), 0, cos(y)]])
    Z = np.array([[cos(z), -sin(z), 0], [sin(z), cos(z), 0], [0, 0, 1]])
    return Y @ X @ Z


def blob(pos, radii, r=(0, 0, 0), sub=2):
    v, f = _SPHERES[sub]
    v = (v * np.array(radii)) @ rot(*r).T + np.array(pos)
    return v, f.copy()


def tube(points, radii, sides=5):
    verts, faces = [], []
    pts = [np.array(p, float) for p in points]
    for i, p in enumerate(pts):
        t = pts[min(len(pts) - 1, i + 1)] - pts[max(0, i - 1)]
        t /= np.linalg.norm(t) + 1e-9
        a = np.cross(t, [0, 1, 0])
        if np.linalg.norm(a) < .01:
            a = np.cross(t, [1, 0, 0])
        a /= np.linalg.norm(a)
        b = np.cross(t, a)
        for j in range(sides):
            g = 2 * pi * j / sides
            verts.append(p + radii[i] * (cos(g) * a + sin(g) * b))
        if i:
            s = (i - 1) * sides
            for j in range(sides):
                k = (j + 1) % sides
                faces += [(s + j, s + k, s + sides + j), (s + k, s + sides + k, s + sides + j)]
    return np.array(verts), np.array(faces, int)


def outward(v, f):
    """Грани — наружу от своего центра (после поворотов и растяжений)."""
    c = v.mean(axis=0)
    out = []
    for a, b, d in f:
        n = np.cross(v[b] - v[a], v[d] - v[a])
        out.append((a, b, d) if np.dot(n, (v[a] + v[b] + v[d]) / 3 - c) >= 0 else (a, d, b))
    return v, np.array(out, int)


# ---------- крыса ----------

def rat(i):
    L, P, H, E, S = SHAPE[i]
    g = {k: [] for k in ['Fur', 'Belly', 'Spot', 'Ear', 'EarInner', 'Paws', 'Eyes', 'Glint', 'Nose', 'Whiskers']}

    def add(part, mesh, closed=True):
        g[part].append(outward(*mesh) if closed else mesh)

    # Спина горбом: круп выше и шире груди, голова ниже холки.
    add('Fur', blob((0, .25 * P, -.36 * L), (.25 * P, .22 * P, .34 * L)))
    for sx in (-1, 1):
        add('Fur', blob((sx * .15 * P, .19, -.38 * L), (.11 * P, .13, .18 * L)))       # ляжки
    add('Fur', blob((0, .23, .04 * L), (.19 * P, .18, .30 * L)))                         # грудь
    # Перемычки: без них грудь, круп и голова читались тремя шарами.
    add('Fur', blob((0, .265 * P, -.16 * L), (.215 * P, .205 * P, .30 * L)))             # поясница
    add('Fur', blob((0, .255, .22 * L), (.155, .16, .2)))                                 # холка
    add('Fur', blob((0, .27, .32 * L), (.135, .14, .16)))                                 # шея
    hz = .44 * L
    add('Fur', blob((0, .29, hz), (.13 * H, .125 * H, .16 * H), (8, 0, 0)))              # голова
    tip = hz + .16 * H + .14 * S + .04
    add('Fur', blob((0, .255, hz + .16 * H + .05 * S), (.075 * H, .068 * H, .15 * S), (12, 0, 0)))  # морда
    add('Nose', blob((0, .228, tip), (.032, .028, .03), sub=1))
    add('Belly', blob((0, .15, -.12 * L), (.18 * P, .09, .44 * L)))
    add('Belly', blob((0, .205, .34 * L), (.09, .07, .15)))                               # горло

    for sx in (-1, 1):
        # Уши малые, круглые, отнесены назад и развёрнуты вбок.
        ez = hz - .04
        ear = ((sx * .088 * H, .395 * H + .03, ez), (.058 * E, .062 * E, .016), (-10, sx * 38, sx * 18))
        add('Ear', blob(*ear))
        face = rot(*ear[2]) @ np.array([0, 0, 1.0])
        add('EarInner', blob(tuple(np.array(ear[0]) + face * .012), (.040 * E, .044 * E, .007), ear[2]))
        # Глаза по бокам головы, бусинки.
        eye = np.array((sx * .10 * H, .315, hz + .075 * H))
        add('Eyes', blob(tuple(eye), (.022, .024, .022), sub=1))
        add('Glint', blob(tuple(eye + (sx * .014, .01, .008)), (.007, .007, .006), sub=1))
        # Лапы: передние малые, задние длинные.
        add('Paws', blob((sx * .11, .03, .17 * L), (.038, .028, .06), sub=1))
        add('Paws', blob((sx * .16 * P, .03, -.42 * L), (.042, .028, .10), sub=1))
        # Усы — по два в сторону, назад и вбок.
        for k in range(2):
            o = (sx * .05, .245 + (k - .5) * .02, tip - .06)
            p = (sx * (.22 + k * .04), .26 + (k - .5) * .05, tip - .16 - k * .03)
            add('Whiskers', tube([o, ((o[0] + p[0]) / 2, o[1] + .01, (o[2] + p[2]) / 2), p], [.006, .005, .002]), closed=False)

    # Пятнышко — светлое пятно на крупе: имя должно быть видно.
    if i == 2:
        add('Spot', blob((0, .25 * P + .22 * P - .03, -.30 * L), (.12, .045, .15)))
    return g


def write_obj(path, groups):
    lines = ['# Sinbinder: крысиное семейство (Tools/rats/generate_rat_models.py)', 'mtllib RatPalette.mtl']
    start = 1
    for part, meshes in groups.items():
        if not meshes:
            continue
        lines += ['o ' + part, 'usemtl ' + part]
        for v, f in meshes:
            lines += [f'v {x:.5f} {y:.5f} {z:.5f}' for x, y, z in v]
            lines += ['f ' + ' '.join(str(start + int(k)) for k in face) for face in f]
            start += len(v)
    path.write_text('\n'.join(lines) + '\n', encoding='utf-8', newline='\n')
    return start - 1


PALETTE = {'Belly': (.62, .58, .52), 'Ear': None, 'EarInner': (.50, .38, .37), 'Paws': (.52, .42, .40),
           'Eyes': (.03, .03, .03), 'Glint': (.9, .9, .85), 'Nose': (.50, .36, .36), 'Whiskers': (.55, .53, .50),
           'Tail': (.48, .40, .38), 'Spot': (.66, .64, .59), 'Fur': None}


def colour(part, i):
    coat = np.array(COATS[i])
    if part == 'Fur':
        return coat
    if part == 'Ear':
        return coat * .8
    if part == 'Belly':
        return coat + (np.array(PALETTE['Belly']) - coat) * .45
    return np.array(PALETTE[part])


def write_all():
    OUT.mkdir(parents=True, exist_ok=True)
    mtl = ''.join(f'newmtl {k}\nKd {c[0]:.3f} {c[1]:.3f} {c[2]:.3f}\n\n'
                  for k, c in [(k, colour(k, 0)) for k in PALETTE])
    (OUT / 'RatPalette.mtl').write_text(mtl, encoding='utf-8')
    for i, n in enumerate(NAMES):
        print(n, write_obj(OUT / f'{n}Body.obj', rat(i)), 'вершин')


# ---------- превью: те же тела и хвост формулой RatTail ----------

def tail_mesh(i, wave=0.6):
    L = SHAPE[i][0]
    length, height = 1.45 * L, .20
    r0, r1 = .05, .008
    root = tail_root(i)
    pts, rad = [], []
    for k in range(14):
        s = k / 13
        x = .16 * L * s ** 1.3 * sin(wave - s * 4.5)
        y = -height * (1 - (1 - s) ** 2) + r1 * s
        pts.append(root + (x, y, -length * s))
        rad.append(r0 + (r1 - r0) * s ** .8)
    return tube(pts, rad, 6)


def preview(out):
    from PIL import Image, ImageDraw, ImageFont
    W, Hh, cols = 1680, 1040, 3
    im = Image.new('RGB', (W, Hh), (27, 30, 34))
    d = ImageDraw.Draw(im)
    try:
        font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 30)
    except OSError:
        font = ImageFont.load_default()
    cam = np.array([2.6, 1.3, 1.2]); cam /= np.linalg.norm(cam)
    right = np.cross([0, 1, 0], cam); right /= np.linalg.norm(right)
    up = np.cross(cam, right)
    light = np.array([.3, 1.0, .6]); light /= np.linalg.norm(light)
    sizes = [1.0, .62, .58, .64, .6, .58]          # как в игре, мать к крысятам
    for i, n in enumerate(NAMES):
        cx, cy = (i % cols) * 560 + 190, (i // cols) * 520 + 300
        d.rounded_rectangle(((i % cols) * 560 + 20, (i // cols) * 520 + 20,
                             (i % cols) * 560 + 540, (i // cols) * 520 + 500), 16, outline=(70, 74, 80), width=2)
        d.text(((i % cols) * 560 + 44, (i // cols) * 520 + 36), TITLES[i], fill=(230, 226, 214), font=font)
        tris = []
        parts = dict(rat(i))
        parts['Tail'] = [tail_mesh(i)]
        for part, meshes in parts.items():
            for v, f in meshes:
                for a, b, c in f:
                    p = v[[a, b, c]]
                    nrm = np.cross(p[1] - p[0], p[2] - p[0])
                    ln = np.linalg.norm(nrm)
                    if ln < 1e-12:
                        continue
                    nrm /= ln
                    depth = float(np.dot(p.mean(axis=0), cam))
                    shade = .35 + .65 * max(0.0, float(np.dot(nrm, light)))
                    if np.dot(nrm, cam) < 0 and part not in ('Whiskers',):
                        continue
                    col = tuple(int(255 * min(1, ch * shade * 1.6)) for ch in colour(part, i))
                    pts = [(cx + float(np.dot(q, right)) * 165 * sizes[i], cy - float(np.dot(q, up)) * 165 * sizes[i]) for q in p]
                    tris.append((depth, pts, col))
        for _, pts, col in sorted(tris, key=lambda t: t[0]):
            d.polygon(pts, fill=col)
    im.save(out)
    print(out)


if __name__ == '__main__':
    if '--preview' in sys.argv:
        preview(sys.argv[sys.argv.index('--preview') + 1] if len(sys.argv) > sys.argv.index('--preview') + 1
                else 'D:/Claude/runs/sheets/rats-preview.png')
    else:
        write_all()
