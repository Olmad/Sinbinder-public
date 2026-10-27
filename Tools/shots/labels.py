"""
Ярлыки пластинок (автор, 27.10: «пластинкам картинка в центре, как у крутых дорогих
пластинок»). Рисуются кодом, без жребия: круг с прозрачным краем, двойной золотой обод,
картинка в середине. Пишет Assets/Resources/Records/Labels/<имя>.png.

    python Tools/shots/labels.py
"""
import math
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

N = 512
C = N / 2
ROOT = Path(__file__).resolve().parents[2] / "Assets" / "Resources" / "Records" / "Labels"

CRIMSON = (92, 14, 22)
CRIMSON_DEEP = (60, 8, 14)
GOLD = (206, 168, 82)
GOLD_DIM = (150, 118, 52)
BONE = (226, 214, 186)


def base(field, field_deep):
    img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    # Поле — с лёгким переходом к краю (как печать на бумаге ярлыка).
    for r in range(int(C) - 4, 0, -2):
        k = r / C
        col = tuple(int(field[i] * (1 - 0.35 * k * k) + field_deep[i] * 0.35 * k * k) for i in range(3))
        d.ellipse([C - r, C - r, C + r, C + r], fill=col + (255,))
    # Двойной обод и пробитое отверстие под ось.
    for r, w, col in ((C - 6, 6, GOLD), (C - 22, 2, GOLD_DIM)):
        d.ellipse([C - r, C - r, C + r, C + r], outline=col + (255,), width=w)
    return img, d


def dots(d, r, count, size, col):
    for i in range(count):
        a = 2 * math.pi * i / count + 0.1
        x, y = C + math.cos(a) * r, C + math.sin(a) * r
        d.ellipse([x - size, y - size, x + size, y + size], fill=col + (255,))


def hole(d):
    d.ellipse([C - 9, C - 9, C + 9, C + 9], fill=(10, 8, 8, 255))


def rat(d, cx, cy, s, col):
    """Сидящая крыса в профиль: тело, голова, ухо, хвост дугой."""
    d.ellipse([cx - 34 * s, cy - 22 * s, cx + 26 * s, cy + 26 * s], fill=col + (255,))             # тело
    d.ellipse([cx + 10 * s, cy - 44 * s, cx + 46 * s, cy - 12 * s], fill=col + (255,))             # голова
    d.polygon([(cx + 40 * s, cy - 32 * s), (cx + 62 * s, cy - 22 * s), (cx + 40 * s, cy - 20 * s)],
              fill=col + (255,))                                                                     # морда
    d.ellipse([cx + 14 * s, cy - 58 * s, cx + 32 * s, cy - 38 * s], fill=col + (255,))             # ухо
    d.arc([cx - 70 * s, cy - 6 * s, cx - 10 * s, cy + 50 * s], 90, 250, fill=col + (255,), width=max(2, int(5 * s)))  # хвост


def moonlight():
    img, d = base(CRIMSON, CRIMSON_DEEP)
    dots(d, C - 44, 24, 3, GOLD_DIM)
    # Полумесяц: золотой круг минус сдвинутый круг цвета поля.
    r = 120
    d.ellipse([C - r, C - r - 10, C + r, C + r - 10], fill=GOLD + (255,))
    d.ellipse([C - r + 52, C - r - 34, C + r + 52, C + r - 34], fill=CRIMSON + (255,))
    # На роге луны сидит крыса — силуэт кости.
    rat(d, C - 40, C + 40, 1.15, BONE)
    # Звёзды.
    for x, y, s in ((C + 70, C - 60, 6), (C + 100, C + 10, 4), (C + 40, C + 90, 5), (C - 110, C - 70, 4)):
        d.polygon([(x, y - s * 2), (x + s * 0.6, y - s * 0.6), (x + s * 2, y), (x + s * 0.6, y + s * 0.6),
                   (x, y + s * 2), (x - s * 0.6, y + s * 0.6), (x - s * 2, y), (x - s * 0.6, y - s * 0.6)],
                  fill=GOLD + (255,))
    hole(d)
    return img


def rats():
    img, d = base((24, 22, 26), (10, 9, 11))
    dots(d, C - 44, 30, 3, GOLD_DIM)
    # Герб: щит, крыса на нём.
    d.polygon([(C - 100, C - 110), (C + 100, C - 110), (C + 100, C + 10), (C, C + 120), (C - 100, C + 10)],
              fill=CRIMSON + (255,), outline=GOLD + (255,))
    rat(d, C - 6, C - 34, 1.05, GOLD)   # выше середины: ось — под крысой, не в ней
    hole(d)
    return img


# Металл ступени (автор, 27.10: «медные, серебряные, золотые — сразу чувствуется ценность
# находки»): медная — услышанная тема, серебряная — с вылазки, золотая — скрытая.
METALS = {
    "copper": ((184, 104, 62), (128, 70, 40)),
    "silver": ((200, 204, 210), (132, 136, 144)),
    "gold": ((214, 172, 74), (150, 118, 52)),
}


def tinted(make, metal):
    """Тот же ярлык, но обод и картинка — в металл ступени."""
    global GOLD, GOLD_DIM
    was = GOLD, GOLD_DIM
    GOLD, GOLD_DIM = METALS[metal]
    try:
        return make()
    finally:
        GOLD, GOLD_DIM = was


def main():
    ROOT.mkdir(parents=True, exist_ok=True)
    for name, make, metal in (("Moonlight", moonlight, "gold"), ("Rats_copper", rats, "copper"),
                              ("Rats_silver", rats, "silver"), ("Rats_gold", rats, "gold")):
        img = tinted(make, metal).filter(ImageFilter.SMOOTH)
        img.save(ROOT / (name + ".png"))
        print("ярлык:", ROOT / (name + ".png"))


if __name__ == "__main__":
    main()
