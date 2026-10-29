# Монтаж трейлера: кадры съёмки (Editor/TrailerShoot → D:\Claude\трейлер\кадры), заставка с названием и девизом,
# музыка — в один mp4. Автор, 29.10: «попробуй сам снять трейлер».
#
#   python Tools/trailer/cut.py [кадры] [музыка.ogg] [выход.mp4]
#
# После последнего кадра — тьма на последнем ударе музыки, затем из тьмы проступает название (Cormorant SC,
# шрифт игры) и под ним девиз; к концу музыки — гаснет. Кадры заставки рисует PIL, склеивает ffmpeg
# (из imageio_ffmpeg). Склейки кадров съёмки — как сняты: планы режет TrailerCam по музыке.
import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont
import imageio_ffmpeg

FPS = 30
W, H = 1920, 1080
ROOT = Path(__file__).resolve().parents[2]
FONT_TITLE = ROOT / "Assets/Resources/Fonts/CormorantSC-Bold.ttf"
FONT_LINE = ROOT / "Assets/Resources/Fonts/PTSerif-Italic.ttf"
GOLD = (196, 160, 92)
TITLE = "SINBINDER"
MOTTO = "Не командуй. Искушай."


def card(folder, start, seconds, total):
    """Заставка: 0,6 с тьмы, название проступает за 1,2 с, девиз — следом, к концу всё гаснет."""
    big = ImageFont.truetype(str(FONT_TITLE), 150)
    small = ImageFont.truetype(str(FONT_LINE), 54)
    n = int(seconds * FPS)
    for i in range(n):
        t = i / FPS
        a_title = min(1.0, max(0.0, (t - 0.6) / 1.2))
        a_motto = min(1.0, max(0.0, (t - 1.8) / 1.0))
        fade = min(1.0, max(0.0, (seconds - t) / 1.5))
        img = Image.new("RGB", (W, H), (0, 0, 0))
        d = ImageDraw.Draw(img)
        for text, font, y, a in ((TITLE, big, H * 0.42, a_title * fade), (MOTTO, small, H * 0.58, a_motto * fade)):
            if a <= 0: continue
            colour = tuple(int(c * a) for c in GOLD)
            box = d.textbbox((0, 0), text, font=font)
            d.text(((W - (box[2] - box[0])) / 2, y - (box[3] - box[1]) / 2), text, fill=colour, font=font)
        img.save(folder / "{:05d}.jpg".format(start + i), quality=92)
    return n


def main():
    frames = Path(sys.argv[1] if len(sys.argv) > 1 else "D:/Claude/трейлер/кадры")
    music = Path(sys.argv[2] if len(sys.argv) > 2 else "D:/Claude/музыка/Трейлер — черновик 3.ogg")
    out = Path(sys.argv[3] if len(sys.argv) > 3 else "D:/Claude/трейлер/Трейлер — демо.mp4")
    shot = sorted(frames.glob("*.jpg"))
    if not shot:
        raise SystemExit("[МОНТАЖ] кадров нет: " + str(frames))
    # Заставка — с кадра после последнего снятого и до конца музыки (хвост зала — под ней).
    import soundfile as sf
    info = sf.info(str(music))
    tail = max(4.0, info.duration - len(shot) / FPS)
    made = card(frames, int(shot[-1].stem) + 1, tail, info.duration)
    ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()
    cmd = [ffmpeg, "-y", "-framerate", str(FPS), "-i", str(frames / "%05d.jpg"), "-i", str(music),
           "-c:v", "libx264", "-preset", "slow", "-crf", "18", "-pix_fmt", "yuv420p",
           "-c:a", "aac", "-b:a", "256k", "-shortest", "-movflags", "+faststart", str(out)]
    subprocess.run(cmd, check=True)
    print("[МОНТАЖ] {}: снято {} кадров, заставка {}, {:.1f} с".format(out, len(shot), made, (len(shot) + made) / FPS))


if __name__ == "__main__":
    main()
