# Надпись «Sinbinder» для открытия игры (Gameplay/OpeningReveal, 15 октября).
# «S» — коса: буква — древко, из верхнего конца — лезвие со светлой кромкой;
# точка второй «i» — склянка с огоньком души; буквы — старым золотом,
# тёмная обводка, мягкая тень. Рисуется в SS раз крупнее и уменьшается.
#
# Запуск из корня проекта:
#   python Tools/art/logo.py Assets/Resources/Art/SinbinderLogo.png
# Шрифт — Cormorant Garamond (OFL, лицензия рядом), только для этой картинки:
# в игру идёт PNG, а не шрифт.
import math, os, sys
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageChops

HERE = os.path.dirname(os.path.abspath(__file__))
FONT = os.path.join(HERE, "CormorantGaramond-VF.ttf")
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "SinbinderLogo.png")

SS = 4                      # во сколько раз крупнее рисуем
W, H = 1600, 520            # итог
CW, CH = W * SS, H * SS

GOLD_TOP = (246, 214, 140)
GOLD_MID = (214, 168, 84)
GOLD_LOW = (150, 102, 42)
INK = (20, 12, 8)
SOUL = (150, 235, 255)


def font(size, weight=b"Bold"):
    f = ImageFont.truetype(FONT, size)
    f.set_variation_by_name(weight)
    return f


def gradient_fill(mask):
    """Золото: светлое сверху, тёмное снизу, по маске."""
    w, h = mask.size
    grad = Image.new("RGB", (1, h))
    box = mask.getbbox() or (0, 0, w, h)
    top, bottom = box[1], box[3]
    for y in range(h):
        t = 0.0 if bottom <= top else min(1.0, max(0.0, (y - top) / (bottom - top)))
        if t < 0.45:
            k = t / 0.45
            c = tuple(int(GOLD_TOP[i] + (GOLD_MID[i] - GOLD_TOP[i]) * k) for i in range(3))
        else:
            k = (t - 0.45) / 0.55
            c = tuple(int(GOLD_MID[i] + (GOLD_LOW[i] - GOLD_MID[i]) * k) for i in range(3))
        grad.putpixel((0, y), c)
    grad = grad.resize((w, h))
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    out.paste(grad, (0, 0), mask)
    return out


def ellipse_points(cx, cy, rx, ry, a0, a1, n=120):
    """Точки дуги эллипса от a0 до a1 градусов (y вниз, угол — против часовой на экране)."""
    pts = []
    for i in range(n + 1):
        a = math.radians(a0 + (a1 - a0) * i / n)
        pts.append((cx + rx * math.cos(a), cy - ry * math.sin(a)))
    return pts


def bezier(p0, p1, p2, n=90):
    """Квадратичная кривая Безье: точки от p0 до p2 через притяжение p1."""
    pts = []
    for i in range(n + 1):
        t = i / n
        a, b, c = (1 - t) ** 2, 2 * (1 - t) * t, t ** 2
        pts.append((a * p0[0] + b * p1[0] + c * p2[0], a * p0[1] + b * p1[1] + c * p2[1]))
    return pts


def scythe_s(height):
    """
    «S»-коса: буква целиком — изогнутое древко, из её верхнего конца растёт
    серповидное лезвие, нависает над буквой и уходит влево, к острию.
    Возвращает маску (L), где в ней буква и высоту буквы.
    """
    f = font(int(height * 1.32))
    probe = Image.new("L", (int(height * 2), int(height * 2)), 0)
    ImageDraw.Draw(probe).text((height * 0.3, 0), "S", font=f, fill=255)
    glyph = probe.crop(probe.getbbox())
    gw, gh = glyph.size

    mask = Image.new("L", (int(gw * 2.6), int(gh * 1.6)), 0)
    ox, oy = int(gw * 1.35), int(gh * 0.55)          # где буква в маске
    mask.paste(glyph, (ox, oy), glyph)

    # Верхний конец буквы: самая правая точка верхней четверти.
    top = glyph.crop((0, 0, gw, int(gh * 0.28)))
    tb = top.getbbox()
    tx = ox + tb[2] - gw * 0.06
    ty = oy + gh * 0.07

    d = ImageDraw.Draw(mask)

    # Обух и лезвие сходятся в острие слева внизу; у пятки (на древке)
    # лезвие широкое, к острию сходит на нет.
    tip = (tx - gw * 1.30, ty + gh * 0.24)
    back = bezier((tx + gw * 0.04, ty - gh * 0.14), (tx - gw * 0.48, ty - gh * 0.54), tip)
    edge = bezier((tx - gw * 0.05, ty + gh * 0.06), (tx - gw * 0.56, ty - gh * 0.21), tip)
    d.polygon(back + list(reversed(edge)), fill=255)

    # Обойма на древке: где лезвие сидит на рукояти.
    r = gw * 0.07
    d.ellipse([tx - r * 1.1, ty - r * 0.9, tx + r * 1.1, ty + r * 1.3], fill=255)

    # Кромка — где светлит сталь: чуть внутрь от режущего края, до острия.
    shift = gh * 0.022
    kromka = [(x, y - shift) for (x, y) in edge[8:-6]]
    return mask, ox, oy, gh, kromka


def jar(size):
    """Склянка с огоньком души: (RGBA стекло+пробка, RGBA свечение)."""
    s = size
    img = Image.new("RGBA", (int(s * 1.0), int(s * 1.6)), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    w, h = img.size
    # пробка
    d.rounded_rectangle([w * 0.32, h * 0.02, w * 0.68, h * 0.14], radius=w * 0.05, fill=GOLD_LOW + (255,))
    # горлышко
    d.rectangle([w * 0.36, h * 0.12, w * 0.64, h * 0.24], fill=(120, 150, 160, 120))
    # тело склянки
    d.rounded_rectangle([w * 0.10, h * 0.22, w * 0.90, h * 0.98], radius=w * 0.22,
                        fill=(110, 150, 165, 95), outline=GOLD_MID + (255,), width=max(2, int(w * 0.06)))
    # огонёк души: капля пламени, белое сердце
    flame = [(w * 0.50, h * 0.40)]
    flame += [(w * 0.50 + w * 0.22 * math.sin(math.radians(a)), h * 0.72 - h * 0.16 * math.cos(math.radians(a)))
              for a in range(30, 331, 10)]
    d.polygon(flame, fill=SOUL + (235,))
    d.ellipse([w * 0.40, h * 0.62, w * 0.60, h * 0.80], fill=(245, 255, 255, 255))
    # блик стекла
    d.rounded_rectangle([w * 0.20, h * 0.32, w * 0.30, h * 0.80], radius=w * 0.05, fill=(255, 255, 255, 70))

    # Свечение — на холсте с запасом: размытое не должно упираться в край.
    glow = Image.new("RGBA", (int(s * 4.0), int(s * 4.0)), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    gw, gh = glow.size
    for r, a in ((0.30, 40), (0.21, 90), (0.13, 170), (0.07, 255)):
        gd.ellipse([gw * (0.5 - r), gh * (0.5 - r), gw * (0.5 + r), gh * (0.5 + r)],
                   fill=SOUL + (a,))
    glow = glow.filter(ImageFilter.GaussianBlur(s * 0.16))
    return img, glow


def main():
    canvas = Image.new("RGBA", (CW, CH), (0, 0, 0, 0))
    letters = Image.new("L", (CW, CH), 0)
    ld = ImageDraw.Draw(letters)

    size = int(CH * 0.48)
    f = font(size)
    baseline = int(CH * 0.70)

    # «S»-коса — крупнее строчных, как буквица.
    s_mask, s_ox, s_oy, s_gh, kromka = scythe_s(int(size * 0.98))
    s_x = int(CW * 0.02)
    s_y = baseline - (s_oy + s_gh) + int(size * 0.06)
    letters.paste(s_mask, (s_x, s_y), s_mask)
    kromka = [(px + s_x, py + s_y) for (px, py) in kromka]
    x = s_x + s_ox + int(s_gh * 0.62)

    # «inbinder»: вторая «i» — без точки, над ней склянка.
    word = ["i", "n", "b", "ı", "n", "d", "e", "r"]
    ascent = f.getmetrics()[0]
    xheight = f.getbbox("x")[3] - f.getbbox("x")[1]
    jar_at = None
    for ch in word:
        ld.text((x, baseline - ascent), ch, font=f, fill=255)
        adv = f.getlength(ch)
        if ch == "ı":
            gb = f.getbbox("ı")
            jar_at = (x + (gb[0] + gb[2]) / 2, baseline - xheight)
        x += adv + size * 0.012

    # Обрезать по ширине надписи, отцентровать.
    box = letters.getbbox()
    shift = int((CW - (box[2] - box[0])) / 2 - box[0])

    letters = ImageChops.offset(letters, shift, 0)
    gold = gradient_fill(letters)

    # Тень и обводка.
    shadow = Image.new("RGBA", (CW, CH), INK + (0,))
    shadow.putalpha(letters.filter(ImageFilter.MaxFilter(9)).filter(ImageFilter.GaussianBlur(SS * 10)))
    stroke = Image.new("RGBA", (CW, CH), INK + (0,))
    stroke.putalpha(letters.filter(ImageFilter.MaxFilter(SS * 2 + 1)))

    canvas = Image.alpha_composite(canvas, shadow)
    canvas = Image.alpha_composite(canvas, stroke)
    canvas = Image.alpha_composite(canvas, gold)

    # Светлая кромка лезвия: сталь поверх золота.
    kd = ImageDraw.Draw(canvas)
    kd.line([(px + shift, py) for (px, py) in kromka], fill=(255, 244, 214, 230), width=SS * 2)

    # Склянка над «ı» — там, где была бы точка: донышком чуть выше буквы.
    if jar_at is not None:
        jsize = int(size * 0.18)
        body, glow = jar(jsize)
        jx = int(jar_at[0] + shift - body.size[0] / 2)
        jy = int(jar_at[1] - size * 0.06 - body.size[1])
        gx = int(jx + body.size[0] / 2 - glow.size[0] / 2)
        gy = int(jy + body.size[1] * 0.68 - glow.size[1] / 2)
        canvas.alpha_composite(glow, (gx, gy))
        canvas.alpha_composite(body, (jx, jy))

    # Черта с ромбом под словом.
    d = ImageDraw.Draw(canvas)
    lb = letters.getbbox()
    ly = int(baseline + size * 0.16)
    mid = (lb[0] + lb[2]) // 2
    half = int((lb[2] - lb[0]) * 0.30)
    lw = max(2, SS * 2)
    d.line([(mid - half, ly), (mid - size * 0.10, ly)], fill=GOLD_MID + (220,), width=lw)
    d.line([(mid + size * 0.10, ly), (mid + half, ly)], fill=GOLD_MID + (220,), width=lw)
    r = size * 0.05
    d.polygon([(mid, ly - r), (mid + r, ly), (mid, ly + r), (mid - r, ly)], fill=GOLD_TOP + (255,))

    out = canvas.resize((W, H), Image.LANCZOS)
    out.save(OUT)
    print(OUT, out.size)


if __name__ == "__main__":
    main()
