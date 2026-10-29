# Сборка трейлера из лучших кусков разных дублей (автор, 29.10: «ты можешь использовать чистые фрагменты из
# других. Не?»). У дублей одна раскладка по времени (планы режет TrailerCam по музыке), поэтому кусок дубля N
# с a по b секунду ложится ровно на то же место музыки. Видео — из готовых роликов, звук — музыка заново.
#
#   python Tools/trailer/compose.py
import subprocess
import tempfile
from pathlib import Path

import imageio_ffmpeg

DIR = Path("D:/Claude/трейлер")
MUSIC = Path("D:/Claude/музыка/Трейлер — черновик 3.ogg")
END = 75.8      # последний кадр съёмки — дальше заставка своего варианта
TAKES = {3: "Трейлер — демо ({v}).mp4", 4: "Трейлер — демо, дубль 4 ({v}).mp4",
         5: "Трейлер — демо, дубль 5 ({v}).mp4", 6: "Трейлер — демо, дубль 6 ({v}).mp4"}
# (с, по, дубль) — по кадрам каждого дубля, глазами.
CUT = [
    (0.0, 13.0, 6),     # сверху к огню, кругом у огня — котёл уже не жёлтый
    (13.0, 17.0, 6),    # Ждан с балалайкой на бревне
    (17.0, 25.0, 3),    # ворон сбоку
    (25.0, 29.0, 5),    # охотники выходят с оружием
    (29.0, 32.0, 3),    # факел
    (32.0, 36.0, 5),    # схватка: павший в свете
    (36.0, 39.2, 6),    # Карган в бою
    (39.2, 41.7, 4),    # охотник крупно
    (41.7, 44.2, 3),    # вверх над огнём перед ударом
    (44.2, 51.8, 3),    # удар — лагерь сверху, облёт
    (51.8, 55.8, 6),    # Греховод у огня в бою
    (55.8, 71.8, 3),    # облёт, ворон сзади, к дороге
    (71.8, END, 5),     # лицо Греховода — чище всего в пятом
]
VERSIONS = ["RU", "EN", "Sinbinder, RU"]


def run(cmd):
    subprocess.run(cmd, check=True)


def main():
    ff = imageio_ffmpeg.get_ffmpeg_exe()
    with tempfile.TemporaryDirectory() as tmp:
        tmp = Path(tmp)
        for v in VERSIONS:
            parts = []
            for i, (a, b, take) in enumerate(CUT + [(END, None, 5)]):
                src = DIR / TAKES[take].format(v=v)
                out = tmp / "{:02d}.mp4".format(i)
                cmd = [ff, "-y", "-loglevel", "error", "-ss", "{:.3f}".format(a), "-i", str(src)]
                if b is not None:
                    cmd += ["-t", "{:.3f}".format(b - a)]
                cmd += ["-an", "-c:v", "libx264", "-preset", "veryfast", "-crf", "16", "-r", "30", "-pix_fmt", "yuv420p", str(out)]
                run(cmd)
                parts.append(out)
            lst = tmp / "list.txt"
            lst.write_text("".join("file '{}'\n".format(p.as_posix()) for p in parts), encoding="utf-8")
            final = DIR / "Трейлер — демо, сборка ({}).mp4".format(v)
            run([ff, "-y", "-loglevel", "error", "-f", "concat", "-safe", "0", "-i", str(lst), "-i", str(MUSIC),
                 "-map", "0:v", "-map", "1:a", "-c:v", "libx264", "-preset", "slow", "-crf", "18", "-pix_fmt", "yuv420p",
                 "-c:a", "aac", "-b:a", "256k", "-shortest", "-movflags", "+faststart", str(final)])
            print("[СБОРКА] " + str(final))


if __name__ == "__main__":
    main()
