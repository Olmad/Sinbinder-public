# Assets/Scripts/Tools/cursors/cursors.py
"""
Курсоры Греховода — скелетная рука. Автор, 26 октября: «вариант 3 на основе
костяного пальца, и банка с прыгающей душой при ожидании»; «было бы хорошо,
если бы курсор отображал какие-нибудь статусы — при наведении на воина рука
держит банку, соответствующую душе воина».

Рисуется здесь, а не руками: курсоры — те же правила, что модели (wear.py):
форма в коде, видна диффом, пересобирается одной командой. Цвета греха здесь
нет: душа в банке рисуется белой маской (`*_soul.png`), а красит её игра
из `Core.SinPalette` (`UI.GameCursor`) — одна палитра на всю игру.

Рисуем в SS раз крупнее и уменьшаем — так края гладкие. Остриё пальца
у всех рук в одной точке (HOT): клик не прыгает при смене курсора.

    python Assets/Scripts/Tools/cursors/cursors.py
"""

import math
from pathlib import Path

from PIL import Image, ImageDraw

SIZE = 48
SS = 4
W = SIZE * SS
HOT = (2, 2)                      # остриё — в пикселях готового курсора

BONE = (226, 216, 194, 255)
BONE_DARK = (168, 156, 134, 255)
INK = (24, 20, 18, 255)
CLAW_INK = (96, 22, 18, 255)
GLASS = (150, 175, 170, 120)
GLASS_EDGE = (205, 225, 220, 220)
CORK = (120, 84, 52, 255)

OUT = Path(__file__).resolve().parents[3] / "Resources" / "Cursors"


def s(v):
    return v * SS


def wd(v):
    """Толщина линии — целым числом пикселей (PIL не берёт дробную)."""
    return max(1, int(round(v * SS)))


def bone(d, p0, p1, r, ink=INK, fill=BONE):
    """Фаланга: капсула с тёмным контуром, костяшки — кружки на концах."""
    (x0, y0), (x1, y1) = p0, p1
    for colour, extra in ((ink, s(1.6)), (fill, 0)):
        rr = s(r) + extra
        d.line([s(x0), s(y0), s(x1), s(y1)], fill=colour, width=int(rr * 2))
        for x, y in (p0, p1):
            d.ellipse([s(x) - rr, s(y) - rr, s(x) + rr, s(y) + rr], fill=colour)


def knuckle(d, p, r):
    x, y = p
    d.ellipse([s(x) - s(r), s(y) - s(r), s(x) + s(r), s(y) + s(r)], fill=BONE_DARK)


def palm(d, pts, ink=INK):
    poly = [(s(x), s(y)) for x, y in pts]
    d.polygon(poly, fill=ink)
    cx = sum(x for x, _ in pts) / len(pts)
    cy = sum(y for _, y in pts) / len(pts)
    inner = [(s(cx + (x - cx) * 0.82), s(cy + (y - cy) * 0.82)) for x, y in pts]
    d.polygon(inner, fill=BONE)


def _hand(u, v):
    """
    Точка руки в своих осях: u — от кончика пальца назад, к запястью; v — вбок, к поджатым пальцам.
    Палец смотрит в остриё (левый верхний угол) под 45°, рука уходит вниз вправо.
    """
    k = 0.74
    return (HOT[0] + 1.0 + k * (u + v), HOT[1] + 1.0 + k * (u - v))


def finger(d, ink=INK):
    """
    Рука, что указывает (автор, 29.10: «не выглядит как вытянутый указательный палец, а как единственный
    палец»): прежде — одна длинная кость и маленькая ладонь, кулака не было. Теперь кисть целиком: запястье,
    ладонь с пястными косточками, три пальца поджаты кулаком — костяшки выступают вперёд, — большой
    прижат вдоль, и вытянут один указательный.
    """
    P = _hand
    # Запястье — две кости предплечья, коротко: курсор — рука, а не рука по локоть.
    bone(d, P(32, 2.0), P(39, 2.6), 2.2, ink)
    bone(d, P(32, 8.0), P(38.5, 9.0), 2.0, ink)
    # Ладонь: тёмный контур и кость внутри; две пястные черточки — видно, что кисть костяная.
    palm(d, [P(19.5, -2.4), P(20.0, 13.0), P(32.5, 11.0), P(33.5, 0.5), P(29.5, -3.4)], ink)
    for v in (3.6, 7.6):
        a, b = P(31, v + 0.4), P(22.5, v)
        d.line([s(a[0]), s(a[1]), s(b[0]), s(b[1])], fill=BONE_DARK, width=wd(0.8))
    # Поджатые пальцы — кулак: ряд круглых костяшек по краю ладони, без фаланг наружу.
    for v in (4.6, 8.4, 11.8):
        bone(d, P(20.6, v), P(18.8, v + 0.3), 2.35, ink)
    # Большой палец — прижат вдоль указательного, снизу.
    bone(d, P(29.5, -3.0), P(24.5, -5.2), 2.35, ink)
    bone(d, P(24.5, -5.2), P(20.0, -4.4), 2.0, ink)
    # Указательный — три фаланги, длинный: в нём весь смысл руки.
    tip, j1, j2, base = P(0, 0), P(6.6, 0.1), P(13.2, 0.25), P(20.0, 0.4)
    bone(d, base, j2, 2.7, ink)
    bone(d, j2, j1, 2.45, ink)
    bone(d, j1, tip, 2.1, ink)
    for p in (j1, j2):
        knuckle(d, p, 1.0)


def jar(d, mask, x, y, w, h, soul_y=None, empty=False, dim=False):
    """Банка души: стекло, пробка; душа — в маску (красит игра)."""
    glass = (GLASS[0], GLASS[1], GLASS[2], 70 if dim else GLASS[3])
    d.rounded_rectangle([s(x), s(y), s(x + w), s(y + h)], radius=wd(3), fill=glass,
                        outline=INK, width=wd(1.4))
    d.rounded_rectangle([s(x + 1.2), s(y + 1.2), s(x + w - 1.2), s(y + h - 1.2)], radius=wd(2.4),
                        outline=GLASS_EDGE if not dim else (140, 150, 150, 120), width=wd(0.6))
    if not empty:
        d.rectangle([s(x + w * 0.22), s(y - 3.0), s(x + w * 0.78), s(y + 0.6)], fill=CORK,
                    outline=INK, width=wd(1.0))
    # Блик на стекле.
    d.line([s(x + 2.2), s(y + 3), s(x + 2.2), s(y + h - 4)], fill=(255, 255, 255, 110), width=wd(1))
    if empty or mask is None:
        return
    cy = y + h * 0.58 if soul_y is None else soul_y
    r = min(w, h) * 0.30
    m = ImageDraw.Draw(mask)
    a = 120 if dim else 255
    m.ellipse([s(x + w / 2 - r), s(cy - r), s(x + w / 2 + r), s(cy + r)], fill=(255, 255, 255, a))
    m.ellipse([s(x + w / 2 - r * 0.45), s(cy - r * 1.55), s(x + w / 2 + r * 0.45), s(cy - r * 0.6)],
              fill=(255, 255, 255, int(a * 0.8)))


def canvas():
    return Image.new("RGBA", (W, W), (0, 0, 0, 0))


def save(img, name):
    OUT.mkdir(parents=True, exist_ok=True)
    img.resize((SIZE, SIZE), Image.LANCZOS).save(OUT / (name + ".png"))


def pointer():
    img = canvas()
    finger(ImageDraw.Draw(img))
    save(img, "pointer")


def holding(dim):
    """Палец держит банку души — над своим воином. Душа — маской."""
    img, mask = canvas(), canvas()
    d = ImageDraw.Draw(img)
    finger(d)
    jar(d, mask, 27, 26, 15, 18, dim=dim)
    bone(d, (28, 38), (33, 41), 2.0)                 # пальцы поверх банки
    bone(d, (33, 41), (38, 41), 1.9)
    name = "warrior_far" if dim else "warrior"
    save(img, name)
    save(mask, name + "_soul")


def claw():
    """Над врагом: сжатая клешня, контур — тёмно-красный."""
    img = canvas()
    d = ImageDraw.Draw(img)
    palm(d, [(16, 16), (28, 18), (32, 30), (22, 35), (13, 28)], CLAW_INK)
    for k, (a, b, c) in enumerate((((6, 4), (11, 9), (16, 16)),
                                    ((14, 4), (17, 10), (22, 17)),
                                    ((22, 7), (24, 12), (27, 19)))):
        bone(d, c, b, 2.4, CLAW_INK)
        bone(d, b, a, 2.0, CLAW_INK)
        knuckle(d, b, 1.0)
    bone(d, (3, 3), (6, 4), 1.4, CLAW_INK)          # кончик — в остриё
    bone(d, (14, 28), (9, 31), 2.3, CLAW_INK)
    save(img, "enemy")


def open_hand():
    """Над вещью на F: раскрытая ладонь, пальцы врозь."""
    img = canvas()
    d = ImageDraw.Draw(img)
    palm(d, [(17, 18), (30, 20), (33, 32), (23, 37), (14, 30)])
    for tip, mid, base in (((3, 3), (9, 9), (17, 18)),
                           ((15, 4), (18, 10), (22, 18)),
                           ((26, 7), (27, 12), (27, 19)),
                           ((35, 12), (33, 16), (31, 21))):
        bone(d, base, mid, 2.5)
        bone(d, mid, tip, 2.1)
        knuckle(d, mid, 1.0)
    bone(d, (15, 29), (9, 33), 2.4)
    save(img, "interact")


def harvest():
    """Над угасающей душой: палец и пустая открытая банка — «забрать»."""
    img = canvas()
    d = ImageDraw.Draw(img)
    finger(d)
    jar(d, None, 27, 26, 15, 18, empty=True)
    bone(d, (28, 38), (33, 41), 2.0)
    save(img, "harvest")


def waiting(frames=8):
    """Ожидание: банка, в ней подпрыгивает душа. Пальца нет — остриё не нужно."""
    for i in range(frames):
        img, mask = canvas(), canvas()
        d = ImageDraw.Draw(img)
        hop = abs(math.sin(math.pi * i / frames))     # отскок: быстро у дна
        jar(d, mask, 14, 12, 20, 28, soul_y=34 - hop * 13)
        save(img, "wait_{}".format(i))
        save(mask, "wait_{}_soul".format(i))


def main():
    pointer()
    holding(dim=False)
    holding(dim=True)
    claw()
    open_hand()
    harvest()
    waiting()
    print("[КУРСОРЫ] записано в " + str(OUT))


main()
