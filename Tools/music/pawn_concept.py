# «From Pawn to Queen» («От пешки до ферзя») — по задумке. Автор в архиве:
# «мрачную мелодию, больше в стиле Overlord 2. Это будет мелодия максимально
# улучшенного склепа. Она должна быть величественной. Длительность 6 минут».
# У автора: челеста играет тему Владыки — по ноте на два такта; виолончель
# держит ре; хор шёпотом — ре, потом соль; медь поднимается ре — ре октавой
# выше — ми — фа-диез, к концу — ля, си, ре, ми; темп 60.
#
# Пешка идёт по доске и на восьмой горизонтали становится ферзём. Здесь
# так же: мотив пешки — фагот, «топ-топ, шаг, шаг» (ре–ре–ми–фа) и голова
# темы Владыки скороговоркой (ре–ля–соль–фа–ми) — пешка несёт ферзя в себе.
# С каждой горизонталью мотив на ступень выше и у инструмента крупнее:
# фагот — валторна — валторны с тромбонами — все. На седьмой горизонтали
# его бьют (тритон, ми-бемоль — нота отказа); на восьмой ре пешки стало
# ре октавой выше — и это тема Владыки всеми, с органом.
# Overlord — в подмигивании: фагот, кларнет, пиццикато; величие — в органе,
# хоре и меди. Урок «Grind»: гармония ходит каждый такт-два, пульс дышит.
#
#   1. Пешка спит      (0:00) — вступление автора: челеста, ре виолончели, хор шёпотом;
#   2. Пешка           (0:48) — фагот, кларнет отвечает, пиццикато;
#   3. Ход за ходом    (1:36) — горизонтали 3–6: ми, фа, соль, ля; марш растёт;
#   4. Седьмая         (3:12) — пешку бьют: орган, тритон, ми-бемоль; до-диез тянет;
#   5. Ферзь           (4:16) — тема Владыки всеми; медь автора: ре — ми — фа-диез — ля,
#                               си-бемоль — до — ре мажор;
#   кода               (5:20) — тема Владыки в мажоре, потом ре минор: склеп тот же;
#                               челеста вспоминает пешку.
# 90 тактов, 6:00. Темп 60: доля — секунда. Без жребия.
#   python3 Tools/music/pawn_concept.py выход.mid
import sys
from orchestra import (Orchestra, CHORD, bar, lord_theme, HORN, STR, PAD, CELLO, BASS, BONE,
                       CHOIR, TIMP, ORGAN, HARP, OOHS, CEL, HARPSI, PICC, PIANO, DRUMS, BD)
from battles import low_pulse, drum, cymbal

BSN, CLAR, PIZZ = HARPSI, PICC, PIANO
s = Orchestra({CEL: 120, BSN: 112, CLAR: 92, PIZZ: 96, HORN: 100, STR: 105, PAD: 70, CELLO: 100,
               BASS: 100, BONE: 76, CHOIR: 70, TIMP: 118, ORGAN: 80, HARP: 88, OOHS: 90, DRUMS: 96},
              program={BSN: 70, CLAR: 71, PIZZ: 45})

INTRO = [(62, 0, 8), (69, 8, 8), (67, 16, 8), (65, 24, 8), (64, 32, 8), (62, 40, 8)]   # автора: челеста
STEP = [(50, 0, 0.5), (50, 0.5, 0.5), (52, 1, 0.5), (53, 1.5, 1.5), (52, 3, 0.5), (50, 3.5, 0.5)]
DREAM = [(50, 4, 1), (57, 5, 1), (55, 6, 0.5), (53, 6.5, 0.5), (52, 7, 1)]          # голова темы Владыки
PAWN = STEP + DREAM

SCALE = [0, 2, 3, 5, 7, 8, 10]          # от ре: ре ми фа соль ля си-бемоль до


def up(p, k):
    """Нота на k ступеней ре минора выше (по ладу, а не по полутонам)."""
    o, r = divmod(p - 50, 12)
    i = SCALE.index(r) + k
    return 50 + 12 * (o + i // 7) + SCALE[i % 7]


def pawn(ch, at, k, vel, octave=0, notes=PAWN, stretch=1):
    """Мотив пешки с k-й ступени; stretch — шире во столько раз."""
    for p, st, d in notes:
        s.add(ch, up(p, k) + 12 * octave, at + st * stretch, d * stretch, vel)


def pizz(at, chords, vel=62):
    """Пиццикато внизу: корень на раз, квинта на три; такт тишины через три."""
    for b, name in enumerate(chords):
        if b % 4 == 3: continue
        r = CHORD[name]['cello']
        s.add(PIZZ, r, at + b * 4, 0.5, vel + 8); s.add(PIZZ, r + 7, at + b * 4 + 2, 0.5, vel)


def chords_on(ch, at, chords, vel, shift=0):
    for b, name in enumerate(chords):
        for p in CHORD[name]['mid']: s.add(ch, p + shift, at + b * 4, 4, vel)


def organ(at, chords, vel):
    for b, name in enumerate(chords):
        c = CHORD[name]
        for p in [c['bass'] + 12, *c['mid']]: s.add(ORGAN, p, at + b * 4, 4, vel)


def ha(at, bars, vel=70):
    for b in range(bars):
        for beat in (1, 3):
            for p in (50, 57): s.add(CHOIR, p, at + b * 4 + beat, 0.35, vel)


# 1. Пешка спит — такты 1–12: вступление автора
s.line(CEL, INTRO, bar(1), 84)
for k in range(6): s.add(CELLO, 38, bar(1) + k * 8, 8, 42)                 # автора: ре виолончели — под челестой
s.add(BASS, 26, bar(1), 48, 44)
s.add(OOHS, 50, bar(6), 16, 54); s.add(OOHS, 55, bar(11), 8, 58)           # автора: хор — ре, потом соль
chords_on(PAD, bar(1), [x for x in ['Dm', 'Dm', 'Gm', 'Dm', 'A', 'Gm'] for _ in (0, 1)], 46)
s.add(BSN, 38, bar(12) + 2, 0.5, 72); s.add(BSN, 38, bar(12) + 2.5, 0.5, 66)   # пешка проснулась

# 2. Пешка — такты 13–24: фагот, кларнет отвечает
P1 = ['Dm', 'Dm', 'Gm', 'Gm', 'Dm', 'Dm', 'Am', 'Am', 'Dm', 'Bb', 'A', 'A']
pizz(bar(13), P1)
chords_on(PAD, bar(13), P1, 38)
pawn(BSN, bar(13), 0, 82)                                                  # робко: пешка только проснулась
pawn(CLAR, bar(15), 3, 80)                                                 # с соль: над соль минором
pawn(BSN, bar(17), 0, 96)
pawn(BSN, bar(19), 4, 90); pawn(CLAR, bar(19) + 1, 6, 74, notes=STEP)      # ля минор: пешка смотрит вперёд
pawn(BSN, bar(21), 0, 98); pawn(CLAR, bar(21), 0, 80, octave=1)
s.add(TIMP, 45, bar(23), 2, 70); s.add(TIMP, 45, bar(24), 2, 76)
s.add(CELLO, 45, bar(23), 8, 70); s.add(HORN, 57, bar(23), 8, 64)
s.swell(HORN, bar(23), bar(25), 70, 100)

# 3. Ход за ходом — такты 25–48: горизонтали 3–6, по шесть тактов
RANKS = [(['C', 'C', 'Am', 'Am', 'Gm', 'C'], 1),           # 3: с ми
         (['F', 'F', 'Dm', 'Dm', 'Bb', 'D'], 2),           # 4: с фа
         (['Gm', 'Gm', 'Eb', 'Eb', 'Bb', 'E'], 3),         # 5: с соль
         (['Am', 'Am', 'F', 'F', 'Gm', 'F'], 4)]           # 6: с ля
for r, (ch, k) in enumerate(RANKS):
    at = bar(25 + r * 6)
    chords_on(PAD, at, ch, 40 + r * 3)
    if r == 0:
        pizz(at, ch, 64)
        pawn(BSN, at, k, 96)
        pawn(HORN, at + 8, k, 84)                                          # пешка выросла: валторна
        low_pulse(s, at + 16, ch[4:], 54, 64, breathe=0)                   # шаг входит заранее: без провала перед маршем
        s.add(HORN, 57, at + 16, 8, 70); s.swell(HORN, at + 16, at + 24, 80, 100)
        s.add(TIMP, 45, at + 20, 2, 66); s.add(TIMP, 45, at + 22, 2, 72)
    elif r == 1:
        low_pulse(s, at, ch, 62, 74)
        pawn(HORN, at, k, 92)
        pawn(STR, at + 8, k, 84, octave=1)
        s.add(BSN, 38, at + 16 + 2, 0.5, 74); s.add(BSN, 38, at + 16 + 2.5, 0.5, 68)
    elif r == 2:
        low_pulse(s, at, ch, 66, 80)
        pawn(HORN, at, k, 96); pawn(BONE, at, k, 82, octave=-1)
        pawn(STR, at + 8, 5, 88, octave=1)                                  # над ми-бемолем — с си-бемоля
        for b in range(6): drum(s, BD, at + b * 4, 70); drum(s, BD, at + b * 4 + 2, 60)
        ha(at + 16, 2, 66)
        s.steps(at, ch, 82)
    else:
        low_pulse(s, at, ch, 70, 84, breathe=0)
        pawn(HORN, at, k, 100); pawn(STR, at, k, 92, octave=1); pawn(BONE, at, k, 86, octave=-1)
        pawn(STR, at + 8, 2, 88, octave=2, notes=STEP)
        s.pads(OOHS, at, ch, 52)
        for b in range(6): drum(s, BD, at + b * 4, 76); drum(s, BD, at + b * 4 + 2, 64)
        s.steps(at, ch, 88, both=True)
        cymbal(s, at, 66)
        s.roll(41, at + 20, 4, 60, 100)                                    # фа — в си-бемоль седьмой

# 4. Седьмая горизонталь — такты 49–64: пешку бьют
S7 = ['Bb', 'Bb', 'C#dim', 'C#dim', 'Gm', 'Gm', 'Eb', 'Eb', 'C#dim', 'C#dim', 'Bb', 'A']
organ(bar(49), S7, 58)
s.ostinato(bar(49), S7, 72, 86)
s.roots(bar(49), S7, 72)
pawn(HORN, bar(49), 5, 96, notes=STEP, stretch=2)                         # с си-бемоля, вдвое шире
pawn(STR, bar(53), 3, 90, octave=1, notes=STEP, stretch=2)
for b in range(0, 12, 2):                                                  # удары: ре минор — ля-бемоль, тритон
    t = bar(49 + b) + 1.5
    for p in CHORD['Dm']['bone']: s.add(BONE, p, t, 0.5, 88)
    for p in CHORD['Ab']['bone']: s.add(BONE, p, t + 4 + 1.5, 0.5, 92)
ha(bar(53), 4, 72)
s.line(HORN, [(70, 0, 4), (73, 4, 4), (75, 8, 6), (73, 14, 2)], bar(57), 98)  # си-бемоль — до-диез — МИ-БЕМОЛЬ — назад
for b in range(57, 61):
    for k in range(8): s.add(TIMP, 38 if k < 6 else 45, bar(b) + k * 0.5, 0.5, 92 if k in (0, 3, 6) else 74)
# такты 61–64: до-диез тянет к ре; литавры нарастают — без провала перед ферзём
for p in (49, 52, 57): s.add(STR, p + 12, bar(61), 16, 90); s.add(PAD, p, bar(61), 16, 60)
s.add(HORN, 73, bar(61), 16, 96); s.add(BONE, 45, bar(61), 16, 84); s.add(BASS, 33, bar(61), 16, 90)
s.add(CELLO, 45, bar(61), 16, 88); s.add(ORGAN, 45, bar(61), 16, 66); s.add(ORGAN, 57, bar(61), 16, 66)
s.add(OOHS, 49, bar(61), 16, 60); s.add(OOHS, 57, bar(61), 16, 56)
s.swell(STR, bar(61), bar(65), 80, 124)
s.swell(HORN, bar(61), bar(65), 80, 120)
s.roll(45, bar(61), 16, 66, 118)

# 5. Ферзь — такты 65–80
ORDER = ['Dm', 'Dm', 'Bb', 'Bb', 'Gm', 'A', 'Dm', 'A']
at = bar(65)
s.expression(STR, at, 112); s.expression(HORN, at, 112)
s.line(ORGAN, lord_theme(), at, 100, shift=12, hold=4)
s.line(HORN, lord_theme(), at, 104, hold=4)
s.line(STR, lord_theme(), at, 106, shift=12, hold=4)
s.line(BONE, lord_theme(), at, 90, shift=-12, hold=4)
organ(at, ORDER, 70)
s.roots(at, ORDER, 84)
s.ostinato(at, ORDER, 78, 92)
s.steps(at, ORDER, 100, both=True)
s.pads(CHOIR, at, ORDER, 58)
s.harp(at, ORDER, 60, 72)
for b in range(8): drum(s, BD, at + b * 4, 84)
cymbal(s, at, 90)
# такты 73–80: медь автора — ре, ми, фа-диез, ля над си-бемолем, до, ре мажором
ASCENT = [('Bb', 74), ('Bb', 74), ('C', 76), ('C', 76), ('D', 78), ('D', 78), ('D', 81), ('D', 81)]
at = bar(73)
s.expression(STR, at, 92); s.swell(STR, at + 4, bar(77), 92, 116)
for b, (name, top) in enumerate(ASCENT):
    t = at + b * 4
    c = CHORD[name]
    lift = 0 if b >= 4 else -14 + b * 3                                  # тише в начале: вершина — ре мажор
    if b % 2 == 0:
        s.add(HORN, top, t, 8, 100 + lift); s.add(STR, top, t, 8, 98 + lift); s.add(STR, top - 12, t, 8, 90 + lift)
    for p in c['mid']: s.add(ORGAN, p, t, 4, 72 + lift); s.add(OOHS, p, t, 4, 56)
    for p in c['bone']: s.add(BONE, p, t, 4, 84 + lift)
    s.add(BASS, c['bass'], t, 4, 90); s.add(CELLO, c['cello'], t, 4, 86); s.add(ORGAN, c['bass'] + 12, t, 4, 70 + lift)
    for k in (0, 1.5, 3): s.add(CELLO, c['cello'] + 12, t + k, 1, 76 + lift)
s.roll(41, bar(75), 8, 70, 112)                                            # фа к фа-диезу
s.add(TIMP, 38, bar(77), 2, 118); cymbal(s, bar(77), 96)
s.steps(bar(78), ['D', 'D', 'D'], 96)

# кода — такты 81–90: тема Владыки в мажоре; потом ре минор — склеп тот же
at = bar(81)
for p in (50, 54, 57): s.add(ORGAN, p, at, 16, 72); s.add(OOHS, p, at, 16, 58)
s.add(ORGAN, 38, at, 16, 72); s.add(BASS, 26, at, 16, 80); s.add(CELLO, 38, at, 16, 74)
s.line(HORN, [(62, 0, 2), (69, 2, 2), (67, 4, 2), (66, 6, 2), (64, 8, 4), (62, 12, 4)], at, 90)
s.line(STR, [(74, 0, 2), (81, 2, 2), (79, 4, 2), (78, 6, 2), (76, 8, 4), (74, 12, 4)], at, 80)
s.swell(ORGAN, at + 8, at + 16, 100, 70)
at = bar(85)
for p in (38, 50, 53, 57): s.add(ORGAN, p, at, 24, 64)                     # ре минор: фа-диез стал фа
s.add(OOHS, 50, at, 24, 52); s.add(OOHS, 57, at, 24, 46)
s.add(BASS, 26, at, 24, 64); s.add(CELLO, 38, at, 24, 56)
pawn(CEL, at + 2, 0, 74, octave=2, notes=STEP)                             # челеста вспоминает пешку
pawn(CEL, at + 10, 0, 66, octave=2, notes=DREAM)
s.add(TIMP, 38, bar(87), 3, 60)
s.swell(ORGAN, bar(87), bar(91), 100, 0)
s.swell(OOHS, bar(87), bar(91), 100, 0)

s.save(sys.argv[1], 60)
print(f'{sys.argv[1]}: нот {s.count()}, конец на {s.end()} долях = {s.end():.0f} с')
