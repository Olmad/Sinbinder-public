# «The Architect's Ultimatum» — по задумке. Автор, 9 октября: «давай доделаем
# музыку». Задумка со страницы архива: «Манифест. Злой, громкий, с тритоном»,
# темп 100. У автора: орган играет тему Владыки быстро, четвертями; хор бьёт
# ре через долю; литавры гремят, к концу — восьмыми; виолончель ре — ля —
# ре — си-бемоль — до-диез — ре.
#
# Тритон уже сидит в теме Владыки: ми над си-бемолем и до-диез над соль —
# оба раза уменьшённая квинта. Здесь он стержень:
#   — удары меди: ре минор — и ля-бемоль мажор, тритоном от ре;
#   — середина: вся тема тритоном выше, в соль-диез миноре — мир наизнанку;
#     её последний аккорд ре-диез — это ми-бемоль, и он падает в ре
#     (та же нота отказа, что во всех темах);
#   — конец: ультиматум как вопрос — ре и ля-бемоль, такт тишины (ждут
#     ответа), и ответ — ре в унисон всеми.
# Куда: ролик (курс п. 15 — «тридцать секунд ролика продают лучше самого
# демо») и экран названия (п. 10), которого пока нет.
#
#   1. Вызов     (0:00) — тема автора как написана: орган, хор, литавры, виолончель;
#   2. Удары     (0:10) — шаг виолончелей, медь ре — ля-бемоль, «Ха!»; орган снова;
#   3. Манифест  (0:29) — тема у валторн, скрипок и тромбонов, орган аккордами;
#   4. Наизнанку (0:48) — тема тритоном выше; ре-диез = ми-бемоль — и вниз, в ре;
#   5. Ультиматум(1:07) — тема всеми с органом, литавры восьмыми;
#   кода         (1:26) — вопрос, такт тишины, ответ.
# 40 тактов, 1:36. Темп 100: доля — 0,6 с, такт — 2,4 с. Без жребия.
#   python3 Tools/music/ultimatum_concept.py выход.mid
import sys
from orchestra import (Orchestra, CHORD, bar, lord_theme, HORN, STR, PAD, CELLO, BASS, BONE,
                       CHOIR, TIMP, ORGAN)

VOLUME = {HORN: 100, STR: 105, PAD: 70, CELLO: 110, BASS: 110, BONE: 70, CHOIR: 90,
          TIMP: 120, ORGAN: 80}
s = Orchestra(VOLUME)

ORDER = ['Dm', 'Dm', 'Bb', 'Bb', 'Gm', 'A', 'Dm', 'A']
INVERTED = ['G#m', 'G#m', 'E', 'E', 'C#m', 'Eb', 'G#m', 'Eb']   # то же тритоном выше; ре-диез = ми-бемоль
FINAL = ['Dm', 'Dm', 'Bb', 'Bb', 'Gm', 'A', 'Dm', 'Dm']
AUTHOR = [(62, 0, 1), (69, 1, 1), (67, 2, 1), (65, 3, 1), (64, 4, 2), (62, 6, 2),
          (58, 8, 1), (61, 9, 1), (62, 10, 4)]                         # тема автора, четвертями


def stabs(at, bars, vel=100):
    """Удары меди: ре минор на раз и на «два-и», ля-бемоль — на четыре."""
    for b in range(bars):
        t = at + b * 4
        for st, chord in ((0, 'Dm'), (1.5, 'Dm'), (3, 'Ab')):
            c = CHORD[chord]
            for p in c['bone']: s.add(BONE, p, t + st, 0.45, vel)
            for p in c['mid']: s.add(HORN, p + 12, t + st, 0.45, vel - 8)


def ha(at, bars, vel=90):
    for b in range(bars):
        for beat in (1, 3):
            for p in (50, 57): s.add(CHOIR, p, at + b * 4 + beat, 0.35, vel)


def drums(at, bars, eighths=False, vel=96):
    for b in range(bars):
        t = at + b * 4
        if eighths:
            for k in range(8): s.add(TIMP, 38 if k < 6 else 45, t + k * 0.5, 0.5, vel if k in (0, 3, 6) else vel - 18)
        else:
            s.add(TIMP, 38, t, 1, vel); s.add(TIMP, 38, t + 2, 1, vel - 10)


def organ_chords(at, chords, vel=70):
    for i, name in enumerate(chords):
        c = CHORD[name]
        for p in [c['bass'] + 12, *c['mid']]: s.add(ORGAN, p, at + i * 4, 4, vel)


# 1. Вызов — такты 1–4: тема автора как написана
s.line(ORGAN, AUTHOR, 0, 110)
s.line(ORGAN, AUTHOR, 0, 96, shift=-12)                                 # октавой ниже — вес
for p, st, d in [(38, 0, 4), (33, 4, 2), (38, 6, 2), (34, 8, 1), (37, 9, 1), (38, 10, 4)]:
    s.add(CELLO, p + 12, st, d, 84); s.add(BASS, p, st, d, 90)          # автора: ре–ля–ре–си-бемоль–до-диез–ре
for st, d, v in [(0, 1, 110), (1, 1, 104), (2, 1, 104), (3, 1, 104), (4, 2, 112), (6, 2, 112),
                 (8, 0.5, 100), (8.5, 0.5, 104), (9, 0.5, 108), (9.5, 0.5, 112), (10, 4, 120)]:
    s.add(TIMP, 38, st, min(d, 1.5), v)                                 # автора: литавры
for p, st, d in [(50, 0, 0.5), (50, 2, 0.5), (50, 4, 0.5), (50, 6, 0.5), (57, 8, 2), (50, 10, 4)]:
    s.add(CHOIR, p, st, d, 92)                                          # автора: хор
s.add(TIMP, 38, 14, 0.5, 100); s.add(TIMP, 45, 15, 0.5, 110)

# 2. Удары — такты 5–12
at = bar(5)
s.ostinato(at, ['Dm'] * 8, 78, 94)
s.roots(at, ['Dm'] * 8, 70)
stabs(at, 8, 96)
ha(at, 8, 80)
drums(at, 4); drums(at + 16, 4, eighths=True, vel=100)
s.line(ORGAN, AUTHOR, bar(9), 104); s.line(ORGAN, AUTHOR, bar(9), 90, shift=-12)
s.line(HORN, AUTHOR, bar(9), 92)
s.roll(45, bar(12) + 2, 2, 70, 115)

# 3. Манифест — такты 13–20: тема у меди и скрипок, орган аккордами
at = bar(13)
s.line(STR, lord_theme(), at, 104, shift=12)
s.line(HORN, lord_theme(), at, 98)
s.line(BONE, lord_theme(), at, 86, shift=-12)
organ_chords(at, ORDER, 64)
s.ostinato(at, ORDER, 82, 98)
s.roots(at, ORDER, 76)
s.steps(at, ORDER, 100, both=True)
ha(at, 8, 76)

# 4. Наизнанку — такты 21–28: та же тема тритоном выше
at = bar(21)
s.line(STR, lord_theme(), at, 100, shift=18)
s.line(HORN, lord_theme(), at, 94, shift=6)
organ_chords(at, INVERTED, 62)
s.ostinato(at, INVERTED, 80, 96)
s.roots(at, INVERTED, 74)
for i in range(7): s.add(TIMP, 38, at + i * 4, 1.5, 92)                 # литавры держат ре — дом, тритоном под чужой тональностью
s.roll(45, bar(28), 4, 80, 122)                                          # ля под ми-бемолем — ещё один тритон, и вниз, в ре
s.swell(STR, bar(28), bar(29), 90, 127)

# 5. Ультиматум — такты 29–36: тема всеми, с органом
at = bar(29)
s.expression(STR, at, 110)
s.line(STR, lord_theme(), at, 110, shift=12, hold=4)
s.line(HORN, lord_theme(), at, 104, hold=4)
s.line(BONE, lord_theme(), at, 94, shift=-12, hold=4)
s.line(ORGAN, lord_theme(), at, 100, shift=12, hold=4)
organ_chords(at, FINAL, 70)
s.ostinato(at, FINAL[:7], 86, 102)
s.roots(at, FINAL, 82)
drums(at, 7, eighths=True, vel=106)
s.roll(38, bar(36), 4, 90, 124)
ha(at, 7, 88)

# кода — такты 37–40: вопрос, такт тишины, ответ
at = bar(37)
s.expression(STR, at, 110)
for st, chord in ((0, 'Dm'), (2, 'Ab')):
    c = CHORD[chord]
    for p in c['bone']: s.add(BONE, p, at + st, 1, 116)
    for p in c['mid']: s.add(HORN, p + 12, at + st, 1, 112); s.add(STR, p + 24, at + st, 1, 112)
    s.add(BASS, c['bass'], at + st, 1, 110); s.add(CELLO, c['cello'], at + st, 1, 106)
    s.add(TIMP, 38 if chord == 'Dm' else 45, at + st, 1, 120)
for ch in (STR, HORN, BONE, ORGAN, CELLO, BASS, CHOIR):                 # ответ: ре всеми, без терции
    for p in {STR: (62, 74, 86), HORN: (50, 62, 74), BONE: (38, 50), ORGAN: (26, 38, 50, 62),
              CELLO: (38, 50), BASS: (26,), CHOIR: (50, 62)}[ch]:
        s.add(ch, p, bar(39), 8, 118 if ch != CHOIR else 96)
s.roll(38, bar(39), 4, 124, 90)
s.swell(STR, bar(39) + 4, bar(41), 110, 0)
s.swell(ORGAN, bar(39) + 4, bar(41), 100, 0)

s.save(sys.argv[1], 100)
print(f'{sys.argv[1]}: нот {s.count()}, конец на {s.end()} долях = {s.end() * 0.6:.1f} с')
