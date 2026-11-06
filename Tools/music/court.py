# Тема Суда Души. Автор, 06.11: «после сборки — займись музыкой для суда».
#
# Суд взвешивает приказ владыки — и музыка взвешивает его тему. Тема Владыки
# (lord_theme) звучит дважды, медленно, как в зале под сводами:
#   с ми      — «послушается»: под нотой ми — ля-мажор, ждущий ре;
#   с ми-бемолем — «откажет»: нота отказа (та же, что у Магистра), под ней —
#                  ми-бемоль-мажор, чужой ре на полтона сверху.
# Весы качаются между двумя нотами — и конец не решает: орган и хор застывают
# на ля (доминанта) и уходят на круг. Приговор выносит суд, а не музыка.
#
# Под голосами: Смерть гудит около 42 Гц и шепчет на 180–700 Гц, судьи говорят
# 60–150 Гц с формантами до 3 кГц. Поэтому всё тихо и протяжно, и ниже 70 Гц
# ничего: гул Смерти — её, бас и педаль органа — с ре малой октавы (73 Гц).
# Орган — мягкая середина, хор — далеко вверху, колокол — редко. Ничего ударного:
# колокол зала бьёт сама игра (SoulCourt.Toll) на входе и на вердикте.
#
#   1. Своды    (такты 1–4)   — орган держит ре, хор вверху, колокол ре и ля;
#   2. Приказ   (такты 5–11)  — тема у виолончелей, с ми;
#   3. Весы     (такты 12–18) — тема у валторны, с ми-бемолем; хор нарастает;
#   4. Колокол  (такты 19–20) — ре и ля, орган на соль-миноре;
#   5. Ожидание (такты 21–24) — ля без разрешения: петля.
# 24 такта, темп 54 — 1:47 на круг. Без жребия.
#   python Tools/music/court.py выход.mid [--loop]
import sys
from orchestra import (Orchestra, CHORD, bar, lord_theme, HORN, STR, CELLO, BASS,
                       CHOIR, TIMP, ORGAN, OOHS, PICC)
from score import loop_tempo

BELLS = PICC
s = Orchestra({BELLS: 84, HORN: 78, STR: 80, CELLO: 96, BASS: 92, CHOIR: 58, OOHS: 66,
               TIMP: 80, ORGAN: 62}, program={BELLS: 14})

# Гармония темы: (начало, длина, аккорд). На месте ми — ля-мажор («послушается»)
# или ми-бемоль-мажор («откажет»).
OBEY = [(0, 4, 'Dm'), (4, 4, 'Gm'), (8, 4, 'A'), (12, 4, 'Dm'),
        (16, 2, 'Bb'), (18, 2, 'A'), (20, 8, 'Dm')]
REFUSE = [(0, 4, 'Dm'), (4, 4, 'Gm'), (8, 4, 'Eb'), (12, 4, 'Dm'),
          (16, 2, 'Bb'), (18, 2, 'A'), (20, 8, 'Gm')]


def chords(at, harm, vel, strings=False):
    """Бас — корнем, орган — тихой серединой; струнные — по желанию, ещё тише."""
    for st, d, name in harm:
        c = CHORD[name]
        s.add(BASS, c['bass'] + 12, at + st, d, vel + 6)
        for p in c['mid']:
            s.add(ORGAN, p, at + st, d, vel - 4)
            if strings: s.add(STR, p + 12, at + st, d, vel - 10)


def toll(at, notes, vel, step=4):
    for i, p in enumerate(notes):
        s.add(BELLS, p, at + i * step, step, vel)


# 1. Своды — такты 1–4: низ, верх и пустота между ними
at = bar(1)
s.add(ORGAN, 38, at, 16, 62); s.add(ORGAN, 50, at, 16, 50)
s.add(BASS, 38, at, 16, 70)
s.add(OOHS, 74, at, 16, 40); s.add(OOHS, 81, at + 4, 12, 36)
s.swell(OOHS, at, bar(5), 50, 100)
toll(at, [62, 57], 74, step=8)

# 2. Приказ — такты 5–11: тема у виолончелей, с ми
at = bar(5)
s.line(CELLO, lord_theme(64), at, 72, shift=-12, hold=0)
chords(at, OBEY, 52)
s.pads(OOHS, at, ['Dm', 'Gm', 'A', 'Dm', 'Bb', 'Dm', 'Dm'], 34)
s.swell(CELLO, at, at + 28, 90, 105)

# 3. Весы — такты 12–18: тема у валторны, с ми-бемолем; хор нарастает
at = bar(12)
s.line(HORN, lord_theme(63), at, 74, hold=0)
s.line(CELLO, lord_theme(63), at, 60, shift=-12, hold=0)
chords(at, REFUSE, 56, strings=True)
for p in (62, 69): s.add(CHOIR, p, at, 28, 44)
s.swell(CHOIR, at, at + 28, 60, 110)
# на ноте отказа — глухой удар литавры, как шаг судьи
s.add(TIMP, 38, at + 8, 2, 62)
s.add(TIMP, 38, at + 20, 3, 56)

# 4. Колокол — такты 19–20: ре и ля, орган на соль-миноре
at = bar(19)
toll(at, [62, 57], 78)
for p in CHORD['Gm']['mid']: s.add(ORGAN, p, at, 8, 50)
s.add(ORGAN, 43, at, 8, 58); s.add(BASS, 43, at, 8, 66)
s.add(OOHS, 74, at, 8, 38)

# 5. Ожидание — такты 21–24: ля без разрешения
at = bar(21)
for p in CHORD['Asus']['mid']: s.add(ORGAN, p, at, 8, 52)
for p in CHORD['A']['mid']: s.add(ORGAN, p, at + 8, 8, 52)
s.add(ORGAN, 45, at, 16, 60); s.add(BASS, 45, at, 16, 68)
s.add(CHOIR, 69, at, 16, 40); s.add(CHOIR, 73, at + 8, 8, 36)
toll(at, [57], 70, step=8); toll(at + 8, [57], 62, step=8)
s.swell(CHOIR, at, at + 16, 100, 70)

length = bar(25)
tempo = 60e6 / 54
if '--loop' in sys.argv:
    s.repeat(length)                         # второй проход вырезается: хвост зала уже в начале
    tempo = loop_tempo(54, length)           # круг кратен блоку синтезатора (43-SOUND, «Шов петли»)
s.save(sys.argv[1], 60e6 / tempo)
print(f'{sys.argv[1]}: нот {s.count()}, конец на {s.end()} долях = {s.end() * tempo / 1e6:.0f} с'
      + (f'; петля — с {length * tempo / 1e6:.6f} по {2 * length * tempo / 1e6:.6f} с' if '--loop' in sys.argv else ''))
