# Assets/Scripts/Tools/blender/sdf.py
"""
Поле расстояний: кость, которую лепят числами.

Зачем. До 27 сентября скелет собирался из ящиков, шаров и трубок
(`bodies.py`), и автор дважды сказал одно и то же: «качество моделей
не очень», «по-прежнему модели старые». Из примитивов, поставленных
рядом, кость не получается: череп выходил шаром с дырами, таз —
кронштейном, рёбра — стопкой обручей. У настоящей кости формы
перетекают друг в друга: скула — в дугу, дуга — в висок, крыло таза —
в вертлужную впадину. Ящики так не умеют.

Поле умеет. Каждая форма здесь — не сетка, а функция «насколько
далеко точка от поверхности»; формы складываются и вычитаются
с мягким швом (smin), и шов выглядит так, будто кость так выросла.
Глазница — это вычтенный эллипсоид с острым краем, скула — прибавленный
с мягким. Поверхность потом снимается с поля решёткой («surface nets»)
и дотягивается до нуля поля.

**Ничего случайного** — тот же закон, что в игре и в `bodies.py`: поле
и решётка — чистая арифметика, один запуск даёт одну и ту же сетку.

Нужен numpy — он есть в самом Блендере.
"""

import numpy as np

# Дальше этого от любой формы поле не считает: значение «очень снаружи».
FAR = 1.0e3


# ---------------------------------------------------------------- формы

class Shape:
    """
    Форма поля. Знает свою рамку (lo, hi) — дальше неё её не считают:
    на решётке в полмиллиона точек сорок форм иначе стоили бы минуты.
    """

    lo = None
    hi = None

    def dist(self, p):
        raise NotImplementedError


def turn(yaw=0.0, pitch=0.0, roll=0.0):
    """
    Поворот формы: рыскание вокруг Z, тангаж вокруг X, крен вокруг Y,
    в градусах. Возвращает матрицу «мир → форма».
    """
    a, b, c = np.radians([yaw, pitch, roll])
    rz = np.array([[np.cos(a), -np.sin(a), 0.0], [np.sin(a), np.cos(a), 0.0], [0.0, 0.0, 1.0]])
    rx = np.array([[1.0, 0.0, 0.0], [0.0, np.cos(b), -np.sin(b)], [0.0, np.sin(b), np.cos(b)]])
    ry = np.array([[np.cos(c), 0.0, np.sin(c)], [0.0, 1.0, 0.0], [-np.sin(c), 0.0, np.cos(c)]])
    # Мир → форма: обратный (транспонированный) поворот.
    return (rz @ rx @ ry).T


class Ball(Shape):
    def __init__(self, c, r):
        self.c = np.asarray(c, dtype=np.float64)
        self.r = float(r)
        self.lo, self.hi = self.c - self.r, self.c + self.r

    def dist(self, p):
        return np.linalg.norm(p - self.c, axis=1) - self.r


class Egg(Shape):
    """
    Эллипсоид — главная форма черепа, скулы, мыщелка. Расстояние
    приближённое (по Иньиго Кильесу: k0·(k0−1)/k1), у поверхности — точное
    в пределах процентов, а решётке больше и не нужно.

    power > 2 — сверхэллипсоид: скруглённый квадрат. Глазница у эталона
    именно такая, и вырезанная овалом читалась не черепом, а маской.
    """

    def __init__(self, c, radii, rot=None, power=2.0):
        self.c = np.asarray(c, dtype=np.float64)
        self.r = np.asarray(radii, dtype=np.float64)
        self.rot = rot
        self.power = float(power)
        big = float(np.max(self.r))
        self.lo, self.hi = self.c - big, self.c + big

    def dist(self, p):
        q = p - self.c
        if self.rot is not None:
            q = q @ self.rot.T
        if self.power != 2.0:
            e = np.sum(np.abs(q / self.r) ** self.power, axis=1) ** (1.0 / self.power)
            return (e - 1.0) * float(np.min(self.r))
        k0 = np.linalg.norm(q / self.r, axis=1)
        k1 = np.linalg.norm(q / (self.r * self.r), axis=1)
        return k0 * (k0 - 1.0) / np.maximum(k1, 1e-9)


class Bar(Shape):
    """
    Кость между двумя точками, с разной толщиной концов (круглый конус
    со сферами на концах). Точное расстояние — формула Кильеса.
    """

    def __init__(self, a, b, ra, rb=None):
        self.a = np.asarray(a, dtype=np.float64)
        self.b = np.asarray(b, dtype=np.float64)
        self.ra = float(ra)
        self.rb = float(ra if rb is None else rb)
        big = max(self.ra, self.rb)
        self.lo = np.minimum(self.a, self.b) - big
        self.hi = np.maximum(self.a, self.b) + big

    def dist(self, p):
        ba = self.b - self.a
        l2 = float(ba @ ba)
        if l2 < 1e-12:
            return np.linalg.norm(p - self.a, axis=1) - max(self.ra, self.rb)

        rr = self.ra - self.rb
        a2 = l2 - rr * rr
        il2 = 1.0 / l2

        pa = p - self.a
        y = pa @ ba
        z = y - l2
        w = pa * l2 - np.outer(y, ba)
        x2 = np.einsum("ij,ij->i", w, w)
        y2 = y * y * l2
        z2 = z * z * l2
        k = np.sign(rr) * rr * rr * x2

        top = np.sqrt(x2 + z2) * il2 - self.rb
        bottom = np.sqrt(x2 + y2) * il2 - self.ra
        side = (np.sqrt(np.maximum(x2 * a2 * il2, 0.0)) + y * rr) * il2 - self.ra

        out = np.where(np.sign(y) * a2 * y2 < k, bottom, side)
        return np.where(np.sign(z) * a2 * z2 > k, top, out)


class Brick(Shape):
    """Брусок со скруглёнными рёбрами: пластина лопатки, тело грудины."""

    def __init__(self, c, half, r=0.0, rot=None):
        self.c = np.asarray(c, dtype=np.float64)
        self.h = np.asarray(half, dtype=np.float64)
        self.r = float(r)
        self.rot = rot
        big = float(np.linalg.norm(self.h)) + self.r
        self.lo, self.hi = self.c - big, self.c + big

    def dist(self, p):
        q = p - self.c
        if self.rot is not None:
            q = q @ self.rot.T
        q = np.abs(q) - self.h
        outside = np.linalg.norm(np.maximum(q, 0.0), axis=1)
        inside = np.minimum(np.max(q, axis=1), 0.0)
        return outside + inside - self.r


class Disc(Shape):
    """
    Цилиндр со скруглённым краем, ось — Z формы: тело позвонка,
    надколенник, край вертлужной впадины. Радиус R, полувысота H,
    скругление края e.
    """

    def __init__(self, c, radius, half, edge, rot=None):
        self.c = np.asarray(c, dtype=np.float64)
        self.R, self.H, self.e = float(radius), float(half), float(edge)
        self.rot = rot
        big = float(np.hypot(self.R, self.H))
        self.lo, self.hi = self.c - big, self.c + big

    def dist(self, p):
        q = p - self.c
        if self.rot is not None:
            q = q @ self.rot.T
        radial = np.hypot(q[:, 0], q[:, 1]) - (self.R - self.e)
        axial = np.abs(q[:, 2]) - (self.H - self.e)
        inside = np.minimum(np.maximum(radial, axial), 0.0)
        outside = np.hypot(np.maximum(radial, 0.0), np.maximum(axial, 0.0))
        return inside + outside - self.e


class Plate(Shape):
    """
    Плоский выпуклый многоугольник, раздутый на r: пластина со
    скруглёнными краями. Тело нижней челюсти, ветвь, лопатка, крыло
    таза, отростки позвонка — всё, что у кости плоское. Яйцами такое
    лепилось волной: подкова челюсти из шести яиц читалась ожерельем.

    Углы — в порядке обхода и в одной плоскости.
    """

    def __init__(self, corners, r):
        self.c = np.asarray(corners, dtype=np.float64)
        self.r = float(r)
        n = np.cross(self.c[1] - self.c[0], self.c[2] - self.c[0])
        self.n = n / np.linalg.norm(n)
        self.lo = self.c.min(axis=0) - self.r
        self.hi = self.c.max(axis=0) + self.r

    def dist(self, p):
        w = (p - self.c[0]) @ self.n
        q = p - np.outer(w, self.n)

        inside = np.ones(len(p), dtype=bool)
        edge = np.full(len(p), np.inf)
        count = len(self.c)
        for i in range(count):
            a, b = self.c[i], self.c[(i + 1) % count]
            ab = b - a
            side = np.cross(ab, q - a) @ self.n
            inside &= side >= 0.0
            t = np.clip((q - a) @ ab / (ab @ ab), 0.0, 1.0)
            near = a + np.outer(t, ab)
            edge = np.minimum(edge, np.linalg.norm(q - near, axis=1))

        plane = np.where(inside, 0.0, edge)
        return np.sqrt(w * w + plane * plane) - self.r


def loop(centre, half, rot, radius, count=14, power=3.0):
    """
    Замкнутое кольцо из брусков по сверхэллипсу в плоскости XZ формы:
    край глазницы. half — полуширина и полувысота, rot — «мир → форма»
    (как у `turn`), radius(угол) — толщина края в этом месте: сверху
    надбровный край толще, у переносицы тоньше.
    """
    centre = np.asarray(centre, dtype=np.float64)
    back = rot.T
    points, radii = [], []
    for i in range(count):
        t = 2.0 * np.pi * i / count
        c, s = np.cos(t), np.sin(t)
        u = half[0] * np.sign(c) * abs(c) ** (2.0 / power)
        v = half[1] * np.sign(s) * abs(s) ** (2.0 / power)
        points.append(centre + back @ np.array([u, 0.0, v]))
        radii.append(radius(t))
    return [Bar(points[i], points[(i + 1) % count], radii[i], radii[(i + 1) % count])
            for i in range(count)]


def chain(points, radii):
    """Изогнутая кость — цепочка брусков через точки: ребро, ключица, дуга."""
    return [Bar(a, b, ra, rb) for a, b, ra, rb
            in zip(points, points[1:], radii, radii[1:])]


# ----------------------------------------------------------------- поле

def smin(a, b, k):
    """Мягкий минимум: объединение со швом шириной k."""
    if k <= 0.0:
        return np.minimum(a, b)
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0.0, 1.0)
    return b * (1.0 - h) + a * h - k * h * (1.0 - h)


def smax(a, b, k):
    """Мягкий максимум: вычитание со швом шириной k."""
    return -smin(-a, -b, k)


ADD, CUT = 0, 1


class Field:
    """
    Кость как поле. Формы кладутся по порядку: `add` прибавляет, `cut`
    вырезает. Порядок важен так же, как
    у скульптора: вырезанная глазница, к которой потом прибавили скулу,
    зарастает.
    """

    def __init__(self):
        self.ops = []
        self.level = 0.0

    def grow(self, d):
        """
        Раздуть поверхность на d наружу: вещь, облегающая кость, —
        тень Греховода поверх черепа (`wear.py`). Поле — расстояние,
        и его ноль, сдвинутый на d, — ровно оболочка на d толще.
        """
        self.level += d
        return self

    def reach(self):
        """
        Докуда форму надо считать за её рамкой.

        Мягкое сложение раздувает поверхность на четверть шва там, где
        две формы близко, — и выводит её за рамку первой формы. Если
        за рамкой поле первой считать «очень далеко», поверхность там
        обрывается: 27 сентября так срезалось темя, два яйца свода
        дали плоскую макушку. Считаем с запасом в самый широкий шов
        поля: дальше него форма на поверхность уже не влияет.
        """
        return max([k for _, _, k in self.ops] + [0.0])

    def add(self, shape, k=0.0):
        if isinstance(shape, (list, tuple)):
            for s in shape:
                self.ops.append((ADD, s, k))
        else:
            self.ops.append((ADD, shape, k))
        return self

    def cut(self, shape, k=0.0):
        if isinstance(shape, (list, tuple)):
            for s in shape:
                self.ops.append((CUT, s, k))
        else:
            self.ops.append((CUT, shape, k))
        return self

    def bounds(self, pad):
        adds = [s for op, s, _ in self.ops if op == ADD]
        lo = np.min([s.lo for s in adds], axis=0) - pad - self.level
        hi = np.max([s.hi for s in adds], axis=0) + pad + self.level
        return lo, hi

    def at(self, p):
        """Поле в произвольных точках (N×3)."""
        p = np.asarray(p, dtype=np.float64)
        d = np.full(len(p), FAR)
        m = self.reach() + 1e-3 + self.level

        for op, shape, k in self.ops:
            lo, hi = shape.lo - m, shape.hi + m
            inside = np.all((p >= lo) & (p <= hi), axis=1)

            if not inside.any():
                continue

            s = shape.dist(p[inside])
            if op == ADD:
                d[inside] = smin(d[inside], s, k)
            else:
                d[inside] = smax(d[inside], -s, k)

        return d - self.level

    # -------------------------------------------------------- решётка

    def sample(self, cell):
        """
        Поле на решётке с шагом cell. Каждая форма считается только
        в своей рамке: решётка черепа — полмиллиона точек, а форм сорок.
        """
        lo, hi = self.bounds(pad=self.reach() * 0.3 + 3.0 * cell)
        n = np.ceil((hi - lo) / cell).astype(int) + 1
        grid = np.full(tuple(n), FAR, dtype=np.float64)

        def span(a, b, axis):
            i0 = max(0, int(np.floor((a - lo[axis]) / cell)))
            i1 = min(n[axis], int(np.ceil((b - lo[axis]) / cell)) + 1)
            return i0, i1

        m = self.reach() + 2.0 * cell + self.level
        for op, shape, k in self.ops:
            (i0, i1), (j0, j1), (k0, k1) = (span(shape.lo[a] - m, shape.hi[a] + m, a)
                                            for a in range(3))

            if i1 <= i0 or j1 <= j0 or k1 <= k0:
                continue

            xs = lo[0] + np.arange(i0, i1) * cell
            ys = lo[1] + np.arange(j0, j1) * cell
            zs = lo[2] + np.arange(k0, k1) * cell
            gx, gy, gz = np.meshgrid(xs, ys, zs, indexing="ij")
            pts = np.stack([gx.ravel(), gy.ravel(), gz.ravel()], axis=1)
            s = shape.dist(pts).reshape(gx.shape)

            block = grid[i0:i1, j0:j1, k0:k1]
            if op == ADD:
                grid[i0:i1, j0:j1, k0:k1] = smin(block, s, k)
            else:
                grid[i0:i1, j0:j1, k0:k1] = smax(block, -s, k)

        return grid - self.level, lo

    def mesh(self, cell, settle=3):
        """
        Поверхность поля: вершины и четырёхугольники, намотанные наружу.

        Surface nets: в каждой ячейке решётки, через которую проходит
        поверхность, — одна вершина, среднее точек пересечения рёбер;
        на каждое пересечённое ребро решётки — один четырёхугольник
        из четырёх ячеек вокруг него. Потом вершины дотягиваются
        до нуля поля шагом Ньютона (settle раз): без этого поверхность
        лежит «ступеньками» в полшага решётки.
        """
        g, lo = self.sample(cell)
        inside = g < 0.0
        nx, ny, nz = g.shape

        # Восемь углов ячейки и двенадцать рёбер между ними.
        corners = [(0, 0, 0), (1, 0, 0), (0, 1, 0), (1, 1, 0),
                   (0, 0, 1), (1, 0, 1), (0, 1, 1), (1, 1, 1)]
        edges = [(0, 1), (2, 3), (4, 5), (6, 7), (0, 2), (1, 3),
                 (4, 6), (5, 7), (0, 4), (1, 5), (2, 6), (3, 7)]

        def corner(c):
            i, j, k = c
            return (slice(i, nx - 1 + i), slice(j, ny - 1 + j), slice(k, nz - 1 + k))

        vals = [g[corner(c)] for c in corners]
        ins = [inside[corner(c)] for c in corners]

        any_in = np.zeros_like(ins[0])
        all_in = np.ones_like(ins[0])
        for a in ins:
            any_in |= a
            all_in &= a
        active = any_in & ~all_in

        total = np.zeros(active.shape + (3,))
        count = np.zeros(active.shape)
        for a, b in edges:
            cross = ins[a] != ins[b]
            va, vb = vals[a], vals[b]
            t = np.where(cross, va / np.where(cross, va - vb, 1.0), 0.0)
            pa = np.asarray(corners[a], dtype=np.float64)
            pb = np.asarray(corners[b], dtype=np.float64)
            for axis in range(3):
                total[..., axis] += np.where(cross, pa[axis] + t * (pb[axis] - pa[axis]), 0.0)
            count += cross

        ci = np.argwhere(active)
        index = np.full(active.shape, -1, dtype=np.int64)
        index[tuple(ci.T)] = np.arange(len(ci))

        local = total[tuple(ci.T)] / count[tuple(ci.T)][:, None]
        verts = lo + (ci + local) * cell

        faces = []

        # Рёбра решётки вдоль X: четыре ячейки вокруг — (j−1..j, k−1..k).
        def quads(axis):
            if axis == 0:
                a, b = inside[:-1, 1:-1, 1:-1], inside[1:, 1:-1, 1:-1]
            elif axis == 1:
                a, b = inside[1:-1, :-1, 1:-1], inside[1:-1, 1:, 1:-1]
            else:
                a, b = inside[1:-1, 1:-1, :-1], inside[1:-1, 1:-1, 1:]
            hit = np.argwhere(a != b)
            if len(hit) == 0:
                return
            flip = ~a[tuple(hit.T)]
            i, j, k = hit[:, 0], hit[:, 1], hit[:, 2]
            if axis == 0:
                j, k = j + 1, k + 1
                ring = [(i, j - 1, k - 1), (i, j, k - 1), (i, j, k), (i, j - 1, k)]
            elif axis == 1:
                i, k = i + 1, k + 1
                ring = [(i - 1, j, k - 1), (i - 1, j, k), (i, j, k), (i, j, k - 1)]
            else:
                i, j = i + 1, j + 1
                ring = [(i - 1, j - 1, k), (i, j - 1, k), (i, j, k), (i - 1, j, k)]
            quad = np.stack([index[r] for r in ring], axis=1)
            quad[flip] = quad[flip][:, ::-1]
            ok = np.all(quad >= 0, axis=1)
            faces.append(quad[ok])

        for axis in range(3):
            quads(axis)

        faces = np.concatenate(faces, axis=0) if faces else np.zeros((0, 4), dtype=np.int64)

        for _ in range(settle):
            verts = self.settle(verts, cell)

        return verts, faces

    def surface(self, origins, dirs, far, steps=48):
        """
        Где луч изнутри выходит на поверхность: грубо — шагами до первой
        точки снаружи, точно — делением пополам. Вещь на кости (обруч,
        скол) ставится по этим точкам и потому лежит на кости, какой бы
        череп ни стал после следующей правки.

        Возвращает точки и признак «нашлось» по каждому лучу.
        """
        o = np.asarray(origins, dtype=np.float64)
        d = np.asarray(dirs, dtype=np.float64)
        d = d / np.linalg.norm(d, axis=1)[:, None]

        ts = np.linspace(0.0, far, steps)
        vals = np.stack([self.at(o + d * t) for t in ts], axis=1)
        outside = vals > 0.0
        first = np.argmax(outside, axis=1)
        found = outside[np.arange(len(o)), first] & (first > 0)

        lo = ts[np.maximum(first - 1, 0)]
        hi = ts[first]
        for _ in range(28):
            mid = (lo + hi) * 0.5
            inside = self.at(o + d * mid[:, None]) < 0.0
            lo = np.where(inside, mid, lo)
            hi = np.where(inside, hi, mid)

        return o + d * ((lo + hi) * 0.5)[:, None], found

    def normal(self, p, eps):
        """Направление наружу — разностями поля."""
        e = np.eye(3) * eps
        g = np.stack([self.at(p + e[a]) - self.at(p - e[a]) for a in range(3)], axis=1)
        n = np.linalg.norm(g, axis=1)
        return g / np.maximum(n, 1e-12)[:, None], n / (2.0 * eps)

    def settle(self, verts, cell):
        """Шаг Ньютона к нулю поля, не дальше полшага решётки."""
        d = self.at(verts)
        n, slope = self.normal(verts, cell * 0.25)
        step = np.clip(d / np.maximum(slope, 0.2), -0.5 * cell, 0.5 * cell)
        return verts - n * step[:, None]

    def hollow(self, centres, normals, reach):
        """
        Насколько место зажато: 0 — открытая поверхность, 1 — дно ямы.

        Глазница, ноздря, щель между рёбрами. Считается по полю, как
        затенение у Кильеса: отходим от поверхности наружу на несколько
        шагов и смотрим, насколько поле меньше пройденного, — значит,
        рядом другая стена. Этим красится дно глазницы: в Unity нет
        затенения, которое рисует Блендер на превью, и без краски яма
        читается бугром.
        """
        occ = np.zeros(len(centres))
        weight = 1.0
        total = 0.0
        for step in (0.18, 0.36, 0.6, 1.0):
            h = reach * step
            d = self.at(centres + normals * h)
            occ += weight * np.clip((h - d) / h, 0.0, 1.0)
            total += weight
            weight *= 0.75
        return occ / total


# --------------------------------------------------------------- проверка

def volume(verts, faces):
    """
    Объём со знаком: плюс — грани намотаны наружу (`bodies.signed_volume`,
    только по массивам). Сетка из поля обязана давать плюс, иначе в Unity
    кость просвечивает.
    """
    v = np.asarray(verts)
    total = 0.0
    for f in faces:
        a = v[f[0]]
        for k in range(1, len(f) - 1):
            total += a @ np.cross(v[f[k]], v[f[k + 1]])
    return total / 6.0
