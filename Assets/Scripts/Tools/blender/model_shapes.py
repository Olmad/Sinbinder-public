# Assets/Scripts/Tools/blender/model_shapes.py
"""Вспомогательная геометрия для предметов гардероба Sinbinder.

Чистый Python: можно проверять размеры, намотку граней и отсутствие
вырожденных полигонов без запуска Blender. Координаты как в bodies.py:
Z вверх, -Y вперёд, масштаб долями роста персонажа.

Не содержит случайности и НЕ создаёт/переименовывает кости.
"""

import math


def hat_brim(z, outer, inner, thickness=0.006, upturn=0.003,
             squash=0.94, segments=32, wave=0.0):
    """Замкнутые поля шляпы в виде настоящей кольцевой пластины.

    Старые поля были только тором (узким обручем). Теперь между тульей
    и краем есть верхняя и нижняя поверхность; Unity видит обе стороны.
    Внешний край слегка приподнят, но центр остаётся на высоте z.
    """
    if not (segments >= 8 and 0.0 < inner < outer and thickness > 0.0):
        raise ValueError('Некорректные размеры полей шляпы')
    verts, faces = [], []
    for i in range(segments):
        a = 2.0 * math.pi * i / segments
        cs, sn = math.cos(a), math.sin(a)
        ripple = wave * math.sin(a * 5.0) * math.sin(a * 3.0)
        zz = z + upturn + ripple
        verts.extend([
            (outer * cs, 0.010 + outer * squash * sn, zz),
            (inner * cs, 0.010 + inner * squash * sn, z),
            (outer * cs, 0.010 + outer * squash * sn, zz - thickness),
            (inner * cs, 0.010 + inner * squash * sn, z - thickness),
        ])
    for i in range(segments):
        a, b = i * 4, ((i + 1) % segments) * 4
        # Верх, низ, внешний борт и внутренний борт.
        faces.extend([
            [a+1, a+0, b+0, b+1],
            [a+3, b+3, b+2, a+2],
            [a+0, a+2, b+2, b+0],
            [a+1, b+1, b+3, a+3],
        ])
    return verts, faces


def cloth_tabard(top, bottom, half_width=0.075, y=-0.097,
                 thickness=0.006, columns=8, rows=6):
    """Мягко изогнутый табард с чуть расширенным подолом и закрытыми краями.

    В отличие от прежнего box() работает светом на слегка изогнутом
    полотне. Привязка по-прежнему одна, Spine: риг не меняется.
    """
    if not (top > bottom and columns >= 2 and rows >= 2 and thickness > 0):
        raise ValueError('Некорректные размеры табарда')
    verts, faces = [], []
    stride = columns + 1
    count = stride * (rows + 1)
    for back in (False, True):
        for j in range(rows + 1):
            t = j / rows
            z = top * (1.0-t) + bottom*t
            width = half_width * (1.0 + 0.14*t)
            for i in range(columns + 1):
                u = i / columns * 2.0 - 1.0
                # Слегка выпуклая дека ткани, а не плоская доска.
                dy = 0.006 * (u*u) + 0.0025 * math.sin(math.pi*t) * (1.0-u*u)
                verts.append((width*u, y+dy+(thickness if back else 0.0), z))
    idx = lambda layer, j, i: layer*count + j*stride + i
    for j in range(rows):
        for i in range(columns):
            # Передняя грань смотрит в -Y.
            faces.append([idx(0,j,i), idx(0,j,i+1), idx(0,j+1,i+1), idx(0,j+1,i)])
            faces.append([idx(1,j,i+1), idx(1,j,i), idx(1,j+1,i), idx(1,j+1,i+1)])
    for j in range(rows):
        faces.append([idx(0,j,0), idx(0,j+1,0), idx(1,j+1,0), idx(1,j,0)])
        faces.append([idx(0,j+1,columns), idx(0,j,columns), idx(1,j,columns), idx(1,j+1,columns)])
    for i in range(columns):
        faces.append([idx(0,0,i+1), idx(0,0,i), idx(1,0,i), idx(1,0,i+1)])
        faces.append([idx(0,rows,i), idx(0,rows,i+1), idx(1,rows,i+1), idx(1,rows,i)])
    # Полотно замкнуто: внешняя нормаль спереди в -Y.
    return verts, [list(reversed(face)) for face in faces]


def bow_path(shoulder, segments=16):
    """Дуга длинного лука и две точки натянутой тетивы за спиной."""
    if segments < 4:
        raise ValueError('Слишком мало сегментов лука')
    points = []
    for i in range(segments + 1):
        t = i / segments
        points.append((0.040 + 0.104*math.sin(math.pi*t),
                       0.106,
                       shoulder - 0.195 + 0.390*t))
    return points, (points[0], points[-1])


def assert_mesh(verts, faces, min_area=1e-12):
    """Проверка геометрии без Blender: индексы, NaN и вырожденные грани."""
    if not verts or not faces:
        raise ValueError('Пустая геометрия')
    for v in verts:
        if len(v) != 3 or not all(math.isfinite(c) for c in v):
            raise ValueError('Некорректная координата вершины')
    for f in faces:
        if len(f) < 3 or any(i < 0 or i >= len(verts) for i in f):
            raise ValueError('Некорректные индексы грани')
        a = verts[f[0]]
        area2 = 0.0
        for j in range(1, len(f)-1):
            b, c = verts[f[j]], verts[f[j+1]]
            ab = (b[0]-a[0], b[1]-a[1], b[2]-a[2])
            ac = (c[0]-a[0], c[1]-a[1], c[2]-a[2])
            cross = (ab[1]*ac[2]-ab[2]*ac[1],
                     ab[2]*ac[0]-ab[0]*ac[2],
                     ab[0]*ac[1]-ab[1]*ac[0])
            area2 += math.sqrt(sum(v*v for v in cross))
        if area2 < min_area:
            raise ValueError('Вырожденная грань')
    return True
