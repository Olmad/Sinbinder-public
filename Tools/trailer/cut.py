# Монтаж трейлера: кадры съёмки (Editor/TrailerShoot → D:\Claude\трейлер\кадры), заставка с логотипом и девизом,
# музыка — в mp4. Автор, 29.10: «попробуй сам снять трейлер»; «у нас же наша фирменная красивая надпись есть»;
# «давай сделаем 2 варианта трейлера с Русской надписью в конце и Английской, именно Sinbinder».
#
#   python Tools/trailer/cut.py [кадры] [музыка.ogg] [папка выхода]
#
# Пишет два ролика: «… (RU).mp4» — логотип «Греховод» и «Не командуй. Искушай.», «… (EN).mp4» — логотип
# «Sinbinder» и «Don't command. Tempt.» (тот же перевод, что в игре, Lang/en.txt). После последнего кадра —
# тьма на последнем ударе музыки, из тьмы проступает логотип (Resources/Art), под ним — девиз; к концу гаснет.
import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont
import imageio_ffmpeg
import soundfile as sf

FPS = 30
W, H = 1920, 1080
ROOT = Path(__file__).resolve().parents[2]
FONT_LINE = ROOT / "Assets/Resources/Fonts/PTSerif-Italic.ttf"
GOLD = (196, 160, 92)
VERSIONS = {
    "RU": ("SinbinderLogoRu.png", "Не командуй. Искушай."),
    "EN": ("SinbinderLogo.png", "Don't command. Tempt."),
    # Автор, 29.10: «и с Английской надписью "Sinbinder", но русским девизом».
    "Sinbinder, RU": ("SinbinderLogo.png", "Не командуй. Искушай."),
}


def card(folder, start, seconds, logo_file, motto):
    """Заставка: 0,6 с тьмы, логотип проступает за 1,4 с, девиз — следом, к концу всё гаснет."""
    logo = Image.open(ROOT / "Assets/Resources/Art" / logo_file).convert("RGBA")
    width = 1300
    logo = logo.resize((width, int(logo.height * width / logo.width)), Image.LANCZOS)
    small = ImageFont.truetype(str(FONT_LINE), 54)
    n = int(seconds * FPS)
    for i in range(n):
        t = i / FPS
        fade = min(1.0, max(0.0, (seconds - t) / 1.5))
        a_logo = min(1.0, max(0.0, (t - 0.6) / 1.4)) * fade
        a_motto = min(1.0, max(0.0, (t - 2.0) / 1.0)) * fade
        img = Image.new("RGBA", (W, H), (0, 0, 0, 255))
        if a_logo > 0:
            layer = logo.copy()
            layer.putalpha(layer.getchannel("A").point(lambda v: int(v * a_logo)))
            img.alpha_composite(layer, ((W - logo.width) // 2, int(H * 0.40 - logo.height / 2)))
        if a_motto > 0:
            d = ImageDraw.Draw(img)
            box = d.textbbox((0, 0), motto, font=small)
            colour = tuple(int(c * a_motto) for c in GOLD)
            d.text(((W - (box[2] - box[0])) / 2, H * 0.60), motto, fill=colour, font=small)
        img.convert("RGB").save(folder / "{:05d}.jpg".format(start + i), quality=92)
    return n


def main():
    frames = Path(sys.argv[1] if len(sys.argv) > 1 else "D:/Claude/трейлер/кадры")
    music = Path(sys.argv[2] if len(sys.argv) > 2 else "D:/Claude/музыка/Трейлер — черновик 3.ogg")
    out_dir = Path(sys.argv[3] if len(sys.argv) > 3 else "D:/Claude/трейлер")
    stem = sys.argv[4] if len(sys.argv) > 4 else "Трейлер — демо"
    # Снятое — кадры до первого кадра заставки: прошлый монтаж мог дописать заставку в ту же папку.
    shot = sorted(p for p in frames.glob("*.jpg"))
    shot_count = int((frames / "снято.txt").read_text()) if (frames / "снято.txt").exists() else len(shot)
    (frames / "снято.txt").write_text(str(shot_count))
    if shot_count == 0:
        raise SystemExit("[МОНТАЖ] кадров нет: " + str(frames))
    tail = max(4.0, sf.info(str(music)).duration - shot_count / FPS)
    ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()
    for lang, (logo, motto) in VERSIONS.items():
        made = card(frames, shot_count, tail, logo, motto)
        out = out_dir / "{} ({}).mp4".format(stem, lang)
        cmd = [ffmpeg, "-y", "-loglevel", "error", "-framerate", str(FPS), "-i", str(frames / "%05d.jpg"), "-i", str(music),
               "-frames:v", str(shot_count + made), "-c:v", "libx264", "-preset", "slow", "-crf", "18", "-pix_fmt", "yuv420p",
               "-c:a", "aac", "-b:a", "256k", "-shortest", "-movflags", "+faststart", str(out)]
        subprocess.run(cmd, check=True)
        print("[МОНТАЖ] {}: снято {} кадров, заставка {}, {:.1f} с".format(out, shot_count, made, (shot_count + made) / FPS))


if __name__ == "__main__":
    main()
