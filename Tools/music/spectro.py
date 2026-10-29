# Спектрограмма — глаза вместо ушей (автор, 29.10: «а точно нет способа тебе послушать?»). Я не слышу;
# по картинке видно, где громко и где тихо, кто кого закрывает, где каша в середине, где пусто наверху.
#
#   python Tools/music/spectro.py звук.wav|ogg картинка.png [--stems папка]
#
# Ось X — время (метки каждые 5 с), ось Y — высота в октавах (до 40 Гц … 12 кГц, ноты «до» подписаны),
# яркость — громкость в дБ (от −70 до 0). Сверху — полоса громкости по времени.
# --stems: в папке лежат дорожки по инструментам (*.wav) — картинки стопкой, по одной на дорожку,
# в одной шкале громкости: видно, кто где звучит и кто кого перекрывает.
import sys
from pathlib import Path

import numpy as np
import soundfile as sf
from PIL import Image, ImageDraw

W, H, BAR = 1600, 360, 60
LO, HI = 40.0, 12000.0
FLOOR = -70.0


def spectrum(x, sr, cols):
    if x.ndim > 1: x = x.mean(axis=1)
    n = 4096
    hop = max(1, (len(x) - n) // cols)
    win = np.hanning(n)
    frames = [x[i:i + n] * win for i in range(0, max(1, len(x) - n), hop)][:cols]
    mag = np.abs(np.fft.rfft(np.array(frames), axis=1))
    freqs = np.fft.rfftfreq(n, 1 / sr)
    rows = np.geomspace(LO, HI, H)
    idx = np.clip(np.searchsorted(freqs, rows), 1, len(freqs) - 1)
    db = 20 * np.log10(mag[:, idx] + 1e-9)
    loud = 20 * np.log10(np.sqrt(np.mean(np.array(frames) ** 2, axis=1)) + 1e-9)
    return db, loud, hop / sr


def colour(v):
    """Тьма → фиолет → огонь → белый: громкое видно сразу."""
    t = np.clip((v - FLOOR) / -FLOOR, 0, 1)
    r = np.clip(t * 2.2, 0, 1); g = np.clip(t * 2.0 - 0.8, 0, 1); b = np.clip(np.sin(t * np.pi) * 0.9 + t ** 4, 0, 1)
    return (np.stack([r, g, b], axis=-1) * 255).astype(np.uint8)


def panel(x, sr, title, ref):
    db, loud, step = spectrum(x, sr, W)
    db = db - ref
    img = Image.new("RGB", (W + 60, H + BAR + 24), (14, 12, 16))
    pic = Image.fromarray(colour(db.T[::-1]))
    img.paste(pic.resize((W, H)), (60, BAR + 24))
    d = ImageDraw.Draw(img)
    d.text((6, 4), title, fill=(230, 220, 200))
    # Громкость по времени.
    l = np.clip((loud - ref - FLOOR) / -FLOOR, 0, 1)
    xs = np.linspace(0, W - 1, len(l))
    d.line([(60 + xx, 24 + BAR - 4 - v * (BAR - 8)) for xx, v in zip(xs, l)], fill=(255, 190, 90), width=2)
    # Время.
    secs = len(db) * step
    for t in range(0, int(secs) + 1, 5):
        xx = 60 + t / secs * W
        d.line([(xx, BAR + 24), (xx, BAR + 30)], fill=(200, 200, 200))
        d.text((xx + 2, BAR + 26), "{}:{:02d}".format(t // 60, t % 60), fill=(170, 170, 170))
    # Ноты «до» по высоте.
    for c, name in ((65.4, "C2"), (130.8, "C3"), (261.6, "C4"), (523.3, "C5"), (1046.5, "C6"), (2093, "C7"), (4186, "C8")):
        yy = BAR + 24 + H - (np.log(c / LO) / np.log(HI / LO)) * H
        d.line([(52, yy), (60, yy)], fill=(200, 200, 200))
        d.text((4, yy - 6), name, fill=(170, 170, 170))
    return img


def read(path):
    x, sr = sf.read(str(path), dtype="float32")
    return x, sr


def main():
    src, out = Path(sys.argv[1]), Path(sys.argv[2])
    x, sr = read(src)
    ref = 20 * np.log10(np.max(np.abs(x)) + 1e-9)
    panels = [panel(x, sr, src.stem, ref)]
    if "--stems" in sys.argv:
        for stem in sorted(Path(sys.argv[sys.argv.index("--stems") + 1]).glob("*.wav")):
            y, r = read(stem)
            panels.append(panel(y, r, stem.stem, ref))
    sheet = Image.new("RGB", (panels[0].width, sum(p.height for p in panels)), (0, 0, 0))
    yy = 0
    for p in panels:
        sheet.paste(p, (0, yy)); yy += p.height
    sheet.save(out)
    print("[СПЕКТР] " + str(out))


if __name__ == "__main__":
    main()
