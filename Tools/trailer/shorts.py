# Монтаж вертикальных Shorts 1080×1920 (автор, 24.11: «Можешь ли ты снять 5 эпичных вертикальных Shorts?»).
# Кадры снимает Editor/TrailerShorts (верстак: «film лагерь», «film поле») в D:\Claude\Shorts\кадры\<ролик>.
# Музыка — кусок трейлерной (Tools/music/trailer.py, темп 76: охота с 25 с, удар 44,2, «Путь» 47,2, последний
# удар 75,8): каждый ролик кончается на своём ударе, и на нём же из тьмы встаёт заставка — логотип и девиз.
#
#   python Tools/trailer/shorts.py [ролик|номер …]      — без имён: все, чьи кадры есть
#
# Пишет D:\Claude\Shorts\<ролик>.mp4 и лист кадров «лист — <ролик>.jpg» (проверить глазами без просмотра видео).
import shutil
import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont
import imageio_ffmpeg

FPS = 30
W, H = 1080, 1920
ROOT = Path(__file__).resolve().parents[2]
FRAMES = Path("D:/Claude/Shorts/кадры")
WORK = Path("D:/Claude/Shorts/монтаж")
OUT = Path("D:/Claude/Shorts")
MUSIC = Path("D:/Claude/музыка/Трейлер — черновик 3.ogg")
FONT = ROOT / "Assets/Resources/Fonts/PTSerif-Italic.ttf"
GOLD = (196, 160, 92)
LOGO, MOTTO = "SinbinderLogoRu.png", "Не командуй. Искушай."
CARD = 4.5

# ролик: (удар музыки, на котором встаёт заставка, с; гамма; подписи — (с, по, строка), черновики: на слово автора).
# Гамма > 1 высветляет тени: ночной лагерь на экране телефона уходит в чёрную кашу (дубль 1). Шару — нет: тьма и есть сцена.
SHORTS = {
    "1 Огни в шаре": (75.8, 1.0, [(0.4, 4.6, "Огни в шаре — наши отряды."),
                                  (4.9, 9.4, "Пока огонь горит — они живы.")]),
    "2 Ворон над лагерем": (25.0, 1.15, []),
    "3 Набег": (47.2, 1.2, []),
    "4 Греховод в бою": (75.8, 1.15, []),
    "5 Перетягивание": (47.2, 1.15, []),
}


def caption(text, alpha, font):
    """Строка вверху кадра: золото с мягкой тенью, по ширине — не шире 88 % кадра."""
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    box = d.textbbox((0, 0), text, font=font)
    x, y = (W - (box[2] - box[0])) / 2, H * 0.15
    shadow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).text((x + 3, y + 4), text, font=font, fill=(0, 0, 0, int(220 * alpha)))
    layer.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(6)))
    d.text((x, y), text, font=font, fill=GOLD + (int(255 * alpha),))
    return layer


def fit(text, size):
    font = ImageFont.truetype(str(FONT), size)
    while ImageDraw.Draw(Image.new("RGB", (1, 1))).textbbox((0, 0), text, font=font)[2] > W * 0.88 and size > 30:
        size -= 2
        font = ImageFont.truetype(str(FONT), size)
    return font


def card(folder, start):
    """Заставка: 0,6 с тьмы, логотип проступает за 1,4 с, девиз — следом, к концу всё гаснет."""
    logo = Image.open(ROOT / "Assets/Resources/Art" / LOGO).convert("RGBA")
    width = 900
    logo = logo.resize((width, int(logo.height * width / logo.width)), Image.LANCZOS)
    font = fit(MOTTO, 64)
    n = int(CARD * FPS)
    for i in range(n):
        t = i / FPS
        fade = min(1.0, max(0.0, (CARD - t) / 1.2))
        a_logo = min(1.0, max(0.0, (t - 0.6) / 1.4)) * fade
        a_motto = min(1.0, max(0.0, (t - 2.0) / 1.0)) * fade
        img = Image.new("RGBA", (W, H), (0, 0, 0, 255))
        if a_logo > 0:
            layer = logo.copy()
            layer.putalpha(layer.getchannel("A").point(lambda v: int(v * a_logo)))
            img.alpha_composite(layer, ((W - logo.width) // 2, int(H * 0.43 - logo.height / 2)))
        if a_motto > 0:
            d = ImageDraw.Draw(img)
            box = d.textbbox((0, 0), MOTTO, font=font)
            d.text(((W - (box[2] - box[0])) / 2, H * 0.43 + logo.height / 2 + 60), MOTTO,
                   fill=tuple(int(c * a_motto) for c in GOLD), font=font)
        img.convert("RGB").save(folder / "{:05d}.jpg".format(start + i), quality=92)
    return n


def sheet(frames, name):
    """Лист: восемь кадров ролика поровну, 4×2."""
    pick = [frames[int(i * (len(frames) - 1) / 7)] for i in range(8)]
    tw, th = 270, 480
    out = Image.new("RGB", (tw * 4, th * 2), (20, 20, 20))
    for i, f in enumerate(pick):
        out.paste(Image.open(f).convert("RGB").resize((tw, th), Image.LANCZOS), ((i % 4) * tw, (i // 4) * th))
    path = OUT / "лист — {}.jpg".format(name)
    out.save(path, quality=88)
    return path


def make(name):
    hit, gamma, lines = SHORTS[name]
    src = FRAMES / name
    frames = sorted(src.glob("*.jpg"))
    if not frames:
        print("[SHORTS] нет кадров:", src)
        return
    work = WORK / name
    if work.exists():
        shutil.rmtree(work)
    work.mkdir(parents=True)
    fonts = {text: fit(text, 66) for _, _, text in lines}
    for i, f in enumerate(frames):
        t = i / FPS
        on = [(a, b, text) for a, b, text in lines if a <= t <= b]
        target = work / "{:05d}.jpg".format(i)
        if not on:
            shutil.copyfile(f, target)
            continue
        img = Image.open(f).convert("RGBA")
        for a, b, text in on:
            alpha = min(1.0, (t - a) / 0.35, (b - t) / 0.35)
            img.alpha_composite(caption(text, alpha, fonts[text]))
        img.convert("RGB").save(target, quality=93)
    shot = len(frames)
    made = card(work, shot)
    total = (shot + made) / FPS
    start = max(0.0, hit - shot / FPS)
    out = OUT / "{}.mp4".format(name)
    cmd = [imageio_ffmpeg.get_ffmpeg_exe(), "-y", "-loglevel", "error",
           "-framerate", str(FPS), "-i", str(work / "%05d.jpg"),
           "-ss", "{:.3f}".format(start), "-t", "{:.3f}".format(total), "-i", str(MUSIC),
           "-vf", "eq=gamma={:.2f}:saturation=1.06".format(gamma),
           # Громкость — одна на все ролики (−14 LUFS, как ждут Shorts): «Ворон» берёт тихое начало
           # трейлера и без этого шёл на −28 дБ против −16 у остальных (25.11).
           "-af", "loudnorm=I=-14:TP=-1.0:LRA=11,afade=t=in:st=0:d=0.25,afade=t=out:st={:.2f}:d=1.4".format(total - 1.4),
           "-ar", "48000",
           "-c:v", "libx264", "-preset", "slow", "-crf", "18", "-pix_fmt", "yuv420p",
           "-c:a", "aac", "-b:a", "192k", "-shortest", "-movflags", "+faststart", str(out)]
    subprocess.run(cmd, check=True)
    print("[SHORTS] {}: {:.1f} с (снято {:.1f} + заставка {:.1f}), музыка с {:.1f} с; лист: {}".format(
        out, total, shot / FPS, made / FPS, start, sheet(frames, name)))


def main():
    # Ролик можно звать номером: «shorts.py 1 4» — то же, что полными именами.
    names = [next((n for n in SHORTS if n == a or n.split(" ", 1)[0] == a), a) for a in sys.argv[1:]]
    for n in names or [n for n in SHORTS if (FRAMES / n).exists()]:
        make(n)


if __name__ == "__main__":
    main()
