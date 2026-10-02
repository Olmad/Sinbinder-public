# «Daydream of the Architect» — по задумке, музыка лагеря. Автор, 2 октября:
# «просто продолжай. И думаю ты можешь начать интегрировать эти мелодии».
# Задумка со страницы архива: «Размышления на работе. Светлая грусть»,
# темп 60. У автора: тема Владыки медленно, по ноте на такт, у рояля
# (ре–ля–соль–фа–ми–ре); долгое ре виолончели; хор шёпотом — ре, потом соль.
#
# Лагерь — то место пролога, где игрок верит, что он бог (09-PROLOGUE §1):
# воины слушаются, у костра тепло. Музыка под этим — не событие, а погода:
# тихая, без кульминаций, чтобы не спорить с облачками разговоров и голосом
# Греховода, и долгая — лагерь идёт минуты, и петля не должна приесться.
#
# «Светлая грусть» — в гармонии: ре минор, но третий аккорд — соль МАЖОР
# (си, а не си-бемоль: свет), и сразу за ним си-бемоль (свет гаснет).
# «Размышления» — в обращении темы: тема Владыки идёт вниз, её зеркало —
# вверх: ре–соль–ля–си–до–ре. Грёза — это тема Владыки, перевёрнутая.
#
#   1. Как у автора  (0:00) — рояль, ре виолончели, хор шёпотом;
#   2. Арфа          (0:32) — то же, под ним аккорды и арфа;
#   3. Грёза         (1:04) — виолончель поёт тему вверх, к ре мажору;
#   4. Вместе        (1:36) — рояль октавой выше и виолончель вверх разом;
#   5. Снова тихо    (2:08) — как в начале: следующий круг.
# 40 тактов, 2:40, петля без шва (--loop, как у «Grind»). Без жребия.
#   python3 Tools/music/daydream_concept.py выход.mid [--loop]
import sys
from orchestra import (Orchestra, CHORD, bar, PIANO, HORN, PAD, CELLO, BASS, HARP, OOHS)

# Сведение — по замеру голосов (levels.py): рояль ведёт тему и громче всех;
# по первому замеру его забивало долгое ре виолончели (−50 против −46).
VOLUME = {PIANO: 127, HORN: 70, PAD: 58, CELLO: 96, BASS: 80, HARP: 80, OOHS: 80}
BARS = 40
s = Orchestra(VOLUME)

THEME = [(62, 0, 4), (69, 4, 4), (67, 8, 4), (65, 12, 4), (64, 16, 8), (62, 24, 8)]   # автора, нота в ноту
HARM = ['Dm', 'F', 'G', 'Bb', 'Asus', 'A', 'Dm', 'Dm']
MIRROR = [(50, 0, 4), (55, 4, 4), (57, 8, 4), (59, 12, 4), (60, 16, 8), (62, 24, 8)]  # ре–соль–ля–си–до–ре
DREAM = ['G', 'C', 'F', 'G', 'Am', 'Am', 'D', 'D']
# Вместе с темой зеркало поёт си-бемоль, а не си: над си-бемоль мажором си
# резало бы полутоном. Свет гаснет — и это та же «светлая грусть».
MIRROR_LOW = [(50, 0, 4), (55, 4, 4), (57, 8, 4), (58, 12, 4), (60, 16, 8), (62, 24, 8)]
TOGETHER = ['Dm', 'F', 'G', 'Bb', 'Asus', 'Asus', 'Dm', 'Dm']    # до над ля — без до-диеза


def whisper(at, vel=46):
    """Хор автора: ре, потом соль — шёпотом."""
    s.add(OOHS, 62, at + 8, 8, vel); s.add(OOHS, 67, at + 16, 8, vel + 6)


def harp(at, chords, vel=52):
    """Арфа: по аккорду вверх восьмыми и обратно, тихо."""
    for i, name in enumerate(chords):
        c = CHORD[name]
        for k, p in enumerate([c['cello'] + 12, *c['mid'], c['mid'][0] + 12, c['mid'][1] + 12, c['mid'][2] + 12, c['mid'][1] + 12]):
            s.add(HARP, p, at + i * 4 + k * 0.5, 2, vel + (8 if k == 0 else 0))


def bass(at, chords, vel):
    for i, name in enumerate(chords):
        s.add(CELLO, CHORD[name]['cello'], at + i * 4, 4, vel)
        s.add(BASS, CHORD[name]['bass'], at + i * 4, 4, vel - 16)


def piece(o):
    # 1. Как у автора — такты 1–8
    s.line(PIANO, THEME, o + bar(1), 108)
    s.add(CELLO, 38, o + bar(1), 32, 44)
    whisper(o + bar(1))
    # 2. Арфа — такты 9–16
    s.line(PIANO, THEME, o + bar(9), 110)
    s.pads(PAD, o + bar(9), HARM, 50)
    harp(o + bar(9), HARM)
    bass(o + bar(9), HARM, 46)
    whisper(o + bar(9), 42)
    # 3. Грёза — такты 17–24: тема вверх у виолончели, к ре мажору
    s.line(CELLO, MIRROR, o + bar(17), 68)
    s.pads(PAD, o + bar(17), DREAM, 52)
    harp(o + bar(17), DREAM, 50)
    for i, name in enumerate(DREAM):
        s.add(BASS, CHORD[name]['bass'], o + bar(17) + i * 4, 4, 48)
    for st, p in [(2, 74), (10, 79), (18, 76), (26, 78)]:            # рояль каплями сверху
        s.add(PIANO, p, o + bar(17) + st, 2, 62)
    # 4. Вместе — такты 25–32: рояль октавой выше и зеркало разом
    s.line(PIANO, THEME, o + bar(25), 118, shift=12)
    s.line(CELLO, MIRROR_LOW, o + bar(25), 56)
    s.pads(PAD, o + bar(25), TOGETHER, 56)
    harp(o + bar(25), TOGETHER, 54)
    for i, name in enumerate(TOGETHER):
        s.add(BASS, CHORD[name]['bass'], o + bar(25) + i * 4, 4, 52)
    s.add(HORN, 62, o + bar(27), 24, 58)                              # валторна держит ре — тепло
    # 5. Снова тихо — такты 33–40: как в начале, рояль с левой рукой
    s.line(PIANO, THEME, o + bar(33), 104)
    for i, name in enumerate(HARM):
        for k, p in enumerate(CHORD[name]['mid']):
            s.add(PIANO, p - 12 if p >= 57 else p, o + bar(33) + i * 4 + k * 0.5, 3, 50)
    s.add(CELLO, 38, o + bar(33), 32, 42)
    whisper(o + bar(33), 40)


loop = '--loop' in sys.argv
piece(0)
if loop:
    piece(bar(BARS + 1))
else:
    s.add(PIANO, 62, bar(BARS + 1), 6, 70); s.add(PIANO, 50, bar(BARS + 1), 6, 60)
    s.add(CELLO, 38, bar(BARS + 1), 6, 50)
s.save(sys.argv[1], 60)
print(f'{sys.argv[1]}: нот {s.count()}, конец на {s.end()} долях = {s.end():.0f} с'
      + (f'; петля — с {bar(BARS + 1)} по {2 * bar(BARS + 1)} с' if loop else ''))
