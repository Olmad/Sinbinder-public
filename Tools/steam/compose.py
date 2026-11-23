# Tools/steam/compose.py
# Обложки Steam: кадр сцены Assets/Scenes/SteamCover.unity (Editor/SteamCover.cs снимает его без
# логотипа в Logs/workbench/shots/steam-*.png) + логотип игры (Resources/Art/SinbinderLogo*.png).
#
#   python Tools/steam/compose.py [папка вывода]       (по умолчанию D:/Claude/Обложка Steam)
#
# Под логотипом — мягкая тень, чтобы золото читалось на огне и в тумане. Фон библиотеки (hero)
# Steam просит без логотипа — он выходит как есть. Русский логотип — отдельные файлы *_ru.
import os
import sys
from PIL import Image, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SHOTS = os.path.join(ROOT, "Logs", "workbench", "shots")
ART = os.path.join(ROOT, "Assets", "Resources", "Art")
OUT = sys.argv[1] if len(sys.argv) > 1 else "D:/Claude/Обложка Steam"

# имя кадра: (доля ширины под логотип, центр логотипа x, центр y — доли кадра)
LAYOUT = {
    "main_1232x706": (0.44, 0.76, 0.22),
    "header_920x430": (0.46, 0.75, 0.24),
    "small_462x174": (0.62, 0.66, 0.42),
    "library_600x900": (0.86, 0.50, 0.12),
}


def logo(path):
    im = Image.open(path).convert("RGBA")
    return im.crop(im.split()[3].getbbox())


def shadow(mark, grow):
    alpha = mark.split()[3]
    pad = grow * 3
    canvas = Image.new("L", (mark.width + pad * 2, mark.height + pad * 2), 0)
    canvas.paste(alpha, (pad, pad))
    canvas = canvas.filter(ImageFilter.MaxFilter(grow * 2 + 1)).filter(ImageFilter.GaussianBlur(grow * 1.4))
    dark = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    dark.putalpha(canvas.point(lambda a: int(a * 0.78)))
    return dark, pad


def place(frame, mark, share, cx, cy):
    w = int(frame.width * share)
    h = int(mark.height * w / mark.width)
    m = mark.resize((w, h), Image.LANCZOS)
    dark, pad = shadow(m, max(4, w // 60))
    x = int(frame.width * cx - w / 2)
    y = int(frame.height * cy - h / 2)
    frame.alpha_composite(dark, (x - pad, y - pad))
    frame.alpha_composite(m, (x, y))
    return frame


def main():
    os.makedirs(OUT, exist_ok=True)
    marks = {"": logo(os.path.join(ART, "SinbinderLogo.png")),
             "_ru": logo(os.path.join(ART, "SinbinderLogoRu.png"))}
    made = []
    for name, (share, cx, cy) in LAYOUT.items():
        src = os.path.join(SHOTS, f"steam-{name}.png")
        if not os.path.exists(src):
            print("нет кадра", src)
            continue
        for suffix, mark in marks.items():
            frame = Image.open(src).convert("RGBA")
            out = os.path.join(OUT, f"{name}{suffix}.png")
            place(frame, mark, share, cx, cy).convert("RGB").save(out)
            made.append(out)
    hero = os.path.join(SHOTS, "steam-hero_3840x1240.png")
    if os.path.exists(hero):
        out = os.path.join(OUT, "library_hero_3840x1240.png")
        Image.open(hero).convert("RGB").save(out)
        made.append(out)
    for m in made:
        print(m)


if __name__ == "__main__":
    main()
