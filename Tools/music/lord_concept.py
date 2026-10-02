# «Возвращение лорда» — по задумке. Автор, 2 октября: «Попробуй сам сделать
# „Возвращение лорда“ по задумке». Задумка со страницы архива: «Главная
# тема. Мрачное величие и власть»; в чате — «лицо Sinbinder», и «для главной
# мелодии как-то очень слабовато».
#
# Тема автора — ядро, нота в ноту: ре–ля–соль–фа–ми–ре, си-бемоль–до-диез–ре
# на ре минор, си-бемоль, соль минор и ля (Dm–Bb–Gm–A). Своё — форма,
# инструменты и одна нота. Слабой тему делало не мелодия, а то, что под ней:
# долгие ноты без шага. Здесь есть шаг: виолончели восьмыми, литавры, тромбоны.
#
# Форма рассказывает игру — «Не командуй. Искушай.»:
#   1. Пробуждение  (0:00) — рояль один играет начало темы над гулом органа;
#   2. Возвращение  (0:16) — тема у валторн, под ней шаг виолончелей;
#   3. Величие      (0:48) — тема у всех: скрипки октавой выше, тромбоны, хор;
#   4. Отказ        (1:20) — всё нарастает к ля мажору, где ждёшь развязки
#                            на ре, — и обрывается. Полторы секунды тишины,
#                            как в игре на первом отказе (RefusalSilence).
#                            Виолончель одна сползает: ре–ля–соль–фа —
#                            и ми-бемоль вместо ми. Не та нота: отказ;
#   5. Искушение    (1:36) — та же тема у рояля, но с ми-бемолем (фригийский
#                            лад — грех в гармонии), арфа, орган, хор шёпотом;
#   6. Власть       (2:08) — тема у всех снова, с ми-бемолем, и теперь развязка
#                            на ре приходит: приказ не прошёл, искушение — да;
#   кода            (2:40) — удар, и рояль один, как в начале: тема вернулась.
#
# Темп 60: доля — секунда, такт — четыре. Без жребия.
#   python3 Tools/music/lord_concept.py выход.mid
import sys
from score import Score

PIANO, HORN, STR, PAD, CELLO, BASS, BONE, CHOIR, TIMP, ORGAN, HARP, OOHS = 0, 1, 2, 3, 4, 5, 6, 7, 8, 10, 11, 12
PROGRAM = {PIANO: 0, HORN: 60, STR: 48, PAD: 49, CELLO: 42, BASS: 43, BONE: 57, CHOIR: 52,
           TIMP: 47, ORGAN: 19, HARP: 46, OOHS: 53}
# Сведение — по замеру голосов (levels.py), а не на слух: мелодия громче
# всех, тромбоны на шесть децибел ниже неё, хор — шёпотом (автор: хор
# «забивает»), шаг виолончелей и контрабас — чтобы был низ.
VOLUME = {PIANO: 127, HORN: 100, STR: 105, PAD: 80, CELLO: 105, BASS: 100, BONE: 60, CHOIR: 62,
          TIMP: 120, ORGAN: 70, HARP: 60, OOHS: 70}
# рассадка оркестра: скрипки слева, виолончели и басы справа, медь в глубине
PAN = {PIANO: 64, HORN: 54, STR: 40, PAD: 50, CELLO: 82, BASS: 90, BONE: 80, CHOIR: 64,
       TIMP: 64, ORGAN: 64, HARP: 34, OOHS: 70}
REVERB = {PIANO: 60, HORN: 80, STR: 75, PAD: 85, CELLO: 70, BASS: 60, BONE: 80, CHOIR: 95,
          TIMP: 70, ORGAN: 90, HARP: 80, OOHS: 95}

s = Score()
for ch in PROGRAM:
    s.expression(ch, 0, 100)


def bar(n):
    """Начало такта n (с единицы), в долях."""
    return (n - 1) * 4


# Аккорды: корень у виолончели, у контрабаса октавой ниже, три голоса
# середины, медь (корень и квинта), литавры (настроены на ре и ля).
CHORD = {
    'Dm': dict(cello=38, bass=26, mid=[50, 53, 57], bone=[50, 57], timp=38),
    'Bb': dict(cello=46, bass=34, mid=[50, 53, 58], bone=[46, 53], timp=None),
    'Gm': dict(cello=43, bass=31, mid=[50, 55, 58], bone=[43, 50], timp=38),
    'A':  dict(cello=45, bass=33, mid=[49, 52, 57], bone=[45, 52], timp=45),
    'Eb': dict(cello=39, bass=27, mid=[51, 55, 58], bone=[51, 58], timp=None),
}
ORDER = ['Dm', 'Dm', 'Bb', 'Bb', 'Gm', 'A', 'Dm', 'A']     # тема как у автора
TEMPT = ['Dm', 'Dm', 'Eb', 'Bb', 'Gm', 'A', 'Dm', 'A']     # ми-бемоль — свой аккорд
FINAL = ['Dm', 'Dm', 'Eb', 'Bb', 'Gm', 'A', 'Dm', 'Dm']    # и развязка в конце


def theme(e=64):
    """Тема автора: (нота, начало, длина) в долях; e — ми или ми-бемоль."""
    return [(62, 0, 2), (69, 2, 2), (67, 4, 2), (65, 6, 2), (e, 8, 4), (62, 12, 4),
            (58, 16, 2), (61, 18, 2), (62, 20, 8)]


def melody(ch, at, vel, shift=0, e=64, hold=0):
    for p, st, d in theme(e):
        s.add(ch, p + shift, at + st, d + (hold if st == 20 else 0), vel)


def ostinato(at, chords, vel, acc):
    """Шаг: виолончели восьмыми — корень, корень, квинта, корень."""
    for i, name in enumerate(chords):
        r = CHORD[name]['cello']
        for k, off in enumerate([0, 0, 7, 0, 0, 0, 7, 0]):
            s.add(CELLO, r + off, at + i * 4 + k * 0.5, 0.42, acc if k in (0, 4) else vel)


def roots(at, chords, vel):
    for i, name in enumerate(chords):
        s.add(BASS, CHORD[name]['bass'], at + i * 4, 4, vel)


def pads(ch, at, chords, vel):
    for i, name in enumerate(chords):
        for p in CHORD[name]['mid']:
            s.add(ch, p, at + i * 4, 4, vel)


def brass(at, chords, vel):
    for i, name in enumerate(chords):
        for half in (0, 2):
            for p in CHORD[name]['bone']:
                s.add(BONE, p, at + i * 4 + half, 1.9, vel + (8 if half == 0 else 0))


def steps(at, chords, vel, both=False):
    for i, name in enumerate(chords):
        t = CHORD[name]['timp']
        if t is None: continue
        s.add(TIMP, t, at + i * 4, 1.5, vel)
        if both: s.add(TIMP, t, at + i * 4 + 2, 1.5, vel - 12)


def roll(note, at, beats, v0, v1):
    """Тремоло литавр шестнадцатыми с нарастанием."""
    n = int(beats * 4)
    for i in range(n):
        s.add(TIMP, note, at + i * 0.25, 0.25, v0 + (v1 - v0) * i / max(1, n - 1))


def tolls(at, chords, vel):
    """Рояль низко, октавой, раз в два такта — как колокол."""
    for i in (0, 2, 4, 6):
        b = CHORD[chords[i]]['bass']
        s.add(PIANO, b, at + i * 4, 4, vel); s.add(PIANO, b + 12, at + i * 4, 4, vel)


# 1. Пробуждение — такты 1–4
s.add(ORGAN, 38, 0, 16, 60); s.add(ORGAN, 45, 0, 16, 60)
s.swell(ORGAN, 0, 10, 40, 95)
s.add(PAD, 50, 0, 16, 55); s.add(PAD, 57, 0, 16, 55)
s.swell(PAD, 0, 12, 20, 85)
for p, st, d in [(62, 0, 2), (69, 2, 2), (67, 4, 2), (65, 6, 2), (64, 8, 4), (62, 12, 4)]:
    s.add(PIANO, p, st, d, 92)
s.add(TIMP, 38, 12, 1.5, 48); s.add(TIMP, 38, 14, 1.5, 58)

# 2. Возвращение — такты 5–12: тема у валторн, шаг виолончелей
at = bar(5)
melody(HORN, at, 88)
s.add(HORN, 57, at + 30, 2, 80)                       # затакт ля — к величию
pads(PAD, at, ORDER, 62)
ostinato(at, ORDER, 66, 80)
roots(at, ORDER, 55)
steps(at, ORDER[:7], 70)
s.swell(PAD, bar(12), bar(13), 70, 115)
roll(45, bar(12), 4, 45, 92)

# 3. Величие — такты 13–20: скрипки октавой выше, валторны, тромбоны, хор
at = bar(13)
s.expression(PAD, at, 100)
melody(STR, at, 96, shift=12)
melody(HORN, at, 90)
pads(CHOIR, at, ORDER, 58)
brass(at, ORDER, 74)
ostinato(at, ORDER, 74, 90)
roots(at, ORDER, 62)
steps(at, ORDER, 84, both=True)
tolls(at, ORDER, 70)

# 4. Отказ — такты 21–24
at = bar(21)
# си-бемоль мажор, потом ля мажор во всю силу — ждёшь ре
for p in [70, 74, 77]: s.add(STR, p, at, 4, 100)               # си-бемоль, ре, фа
for p in [53, 58, 62]: s.add(HORN, p, at, 4, 92)
for p in [46, 53]: s.add(BONE, p, at, 4, 88)
for p in [50, 53, 58]: s.add(CHOIR, p, at, 4, 62)
s.add(BASS, 34, at, 4, 70)
ostinato(at, ['Bb'], 80, 94)
for p in [69, 73, 76, 81]: s.add(STR, p, at + 4, 4, 110)       # ля, до-диез, ми, ля — до-диез тянет к ре
for p in [57, 61, 64]: s.add(HORN, p, at + 4, 4, 104)
for p in [45, 52]: s.add(BONE, p, at + 4, 4, 100)
for p in [49, 52, 57]: s.add(CHOIR, p, at + 4, 4, 70)
s.add(BASS, 33, at + 4, 4, 80)
for k in range(16): s.add(CELLO, 45, at + 4 + k * 0.25, 0.25, 70 + k * 3)   # тремоло
roll(45, at + 4, 4, 70, 118)
for ch in (STR, HORN, BONE, CHOIR):
    s.swell(ch, at + 4, at + 8, 85, 127)
# обрыв на такте 23 — полторы секунды тишины
at = bar(23) + 1.5
for p, st, d in [(50, 0, 1), (57, 1, 1), (55, 2, 1), (53, 3, 1.5), (51, 4.5, 2), (50, 6.5, 4)]:
    s.add(CELLO, p, at + st, d, 78)                    # ре–ля–соль–фа — ми-бемоль — ре
for ch in (STR, HORN, BONE, CHOIR):
    s.expression(ch, bar(23), 100)
for p in [39, 46, 55]: s.add(ORGAN, p, at + 4.5, 2, 52)   # ми-бемоль мажор, тихо
s.expression(ORGAN, at + 4.5, 80)

# 5. Искушение — такты 25–32: тема у рояля, с ми-бемолем
at = bar(25)
melody(PIANO, at, 100, e=63)
melody(PIANO, at, 78, shift=12, e=63)                   # октава сверху — тише
s.expression(ORGAN, at, 60)                             # орган — фоном, не вровень с роялем
for i, name in enumerate(TEMPT):                       # арфа: вверх-вниз по аккорду
    c = CHORD[name]; seq = [c['cello'], *c['mid'], c['mid'][1], c['mid'][0], c['mid'][1], c['mid'][2]]
    for k, p in enumerate(seq):
        s.add(HARP, p, at + i * 4 + k * 0.5, 1.2, 58 if k else 66)
pads(ORGAN, at, TEMPT, 50)
roots(at, TEMPT, 45)
for i, name in enumerate(TEMPT[4:], start=4):
    c = CHORD[name]['mid']
    s.add(OOHS, c[0], at + i * 4, 4, 52); s.add(OOHS, c[2], at + i * 4, 4, 52)
ostinato(at + 16, TEMPT[4:], 52, 62)
s.swell(PAD, bar(31), bar(33), 30, 110)
pads(PAD, bar(31), ['Dm', 'A'], 60)
roll(45, bar(32), 4, 40, 100)

# 6. Власть — такты 33–40: тема у всех, с ми-бемолем, и развязка на ре
at = bar(33)
s.expression(PAD, at, 100)
melody(STR, at, 104, shift=12, e=63, hold=4)
melody(HORN, at, 98, e=63, hold=4)
pads(CHOIR, at, FINAL, 64)
pads(PAD, at, FINAL, 64)
brass(at, FINAL, 84)
ostinato(at, FINAL[:7], 80, 98)
roots(at, FINAL, 70)
steps(at, FINAL[:7], 92, both=True)
tolls(at, FINAL, 78)
roll(38, bar(40), 4, 70, 118)                         # к последнему удару
for ch in (STR, HORN, CHOIR):
    s.swell(ch, bar(40), bar(41), 100, 127)

# кода — такты 41–45: удар, и рояль один
at = bar(41)
for ch in (STR, HORN, CHOIR):
    s.expression(ch, at + 0.01, 110)
for p in [50, 57, 62, 69, 74]: s.add(STR, p, at, 3, 110)
for p in [57, 62, 65]: s.add(HORN, p, at, 3, 104)
for p in [50, 57]: s.add(BONE, p, at, 3, 104)
for p in [50, 57, 62]: s.add(CHOIR, p, at, 3, 70)
s.add(BASS, 26, at, 4, 84); s.add(CELLO, 38, at, 4, 96)
s.add(TIMP, 38, at, 3, 120)
s.add(ORGAN, 38, at, 22, 62); s.add(ORGAN, 45, at, 22, 62)
s.expression(ORGAN, at, 90)
s.swell(ORGAN, at + 12, at + 22, 90, 0)
s.add(PAD, 50, at + 2, 20, 50); s.add(PAD, 57, at + 2, 20, 50)
s.swell(PAD, at + 2, at + 6, 40, 80); s.swell(PAD, at + 14, at + 22, 80, 0)
for p, st, d in [(62, 4, 2), (69, 6, 2), (67, 8, 2), (65, 10, 2), (64, 12, 4), (62, 16, 6)]:
    s.add(PIANO, p, at + st, d, 88)

s.write(sys.argv[1], 60, PROGRAM, VOLUME, PAN, REVERB)
print(f'{sys.argv[1]}: нот {s.count()}, конец на {s.end()} долях = {s.end():.0f} с')
