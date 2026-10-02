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
from orchestra import (Orchestra, CHORD, bar, lord_theme, PIANO, HORN, STR, PAD, CELLO, BASS, BONE,
                       CHOIR, TIMP, ORGAN, HARP, OOHS)

# Сведение — по замеру голосов (levels.py), а не на слух: мелодия громче
# всех, тромбоны на шесть децибел ниже неё, хор — шёпотом (автор: хор
# «забивает»), шаг виолончелей и контрабас — чтобы был низ.
VOLUME = {PIANO: 127, HORN: 100, STR: 105, PAD: 80, CELLO: 105, BASS: 100, BONE: 60, CHOIR: 62,
          TIMP: 120, ORGAN: 70, HARP: 60, OOHS: 70}
s = Orchestra(VOLUME)

ORDER = ['Dm', 'Dm', 'Bb', 'Bb', 'Gm', 'A', 'Dm', 'A']     # тема как у автора
TEMPT = ['Dm', 'Dm', 'Eb', 'Bb', 'Gm', 'A', 'Dm', 'A']     # ми-бемоль — свой аккорд
FINAL = ['Dm', 'Dm', 'Eb', 'Bb', 'Gm', 'A', 'Dm', 'Dm']    # и развязка в конце


def melody(ch, at, vel, shift=0, e=64, hold=0):
    s.line(ch, lord_theme(e), at, vel, shift, hold)


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
s.pads(PAD, at, ORDER, 62)
s.ostinato(at, ORDER, 66, 80)
s.roots(at, ORDER, 55)
s.steps(at, ORDER[:7], 70)
s.swell(PAD, bar(12), bar(13), 70, 115)
s.roll(45, bar(12), 4, 45, 92)

# 3. Величие — такты 13–20: скрипки октавой выше, валторны, тромбоны, хор
at = bar(13)
s.expression(PAD, at, 100)
melody(STR, at, 96, shift=12)
melody(HORN, at, 90)
s.pads(CHOIR, at, ORDER, 58)
s.brass(at, ORDER, 74)
s.ostinato(at, ORDER, 74, 90)
s.roots(at, ORDER, 62)
s.steps(at, ORDER, 84, both=True)
s.tolls(at, ORDER, 70)

# 4. Отказ — такты 21–24
at = bar(21)
# си-бемоль мажор, потом ля мажор во всю силу — ждёшь ре
for p in [70, 74, 77]: s.add(STR, p, at, 4, 100)               # си-бемоль, ре, фа
for p in [53, 58, 62]: s.add(HORN, p, at, 4, 92)
for p in [46, 53]: s.add(BONE, p, at, 4, 88)
for p in [50, 53, 58]: s.add(CHOIR, p, at, 4, 62)
s.add(BASS, 34, at, 4, 70)
s.ostinato(at, ['Bb'], 80, 94)
for p in [69, 73, 76, 81]: s.add(STR, p, at + 4, 4, 110)       # ля, до-диез, ми, ля — до-диез тянет к ре
for p in [57, 61, 64]: s.add(HORN, p, at + 4, 4, 104)
for p in [45, 52]: s.add(BONE, p, at + 4, 4, 100)
for p in [49, 52, 57]: s.add(CHOIR, p, at + 4, 4, 70)
s.add(BASS, 33, at + 4, 4, 80)
for k in range(16): s.add(CELLO, 45, at + 4 + k * 0.25, 0.25, 70 + k * 3)   # тремоло
s.roll(45, at + 4, 4, 70, 118)
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
s.harp(at, TEMPT, 58, 66)                              # арфа: вверх-вниз по аккорду
s.pads(ORGAN, at, TEMPT, 50)
s.roots(at, TEMPT, 45)
for i, name in enumerate(TEMPT[4:], start=4):
    c = CHORD[name]['mid']
    s.add(OOHS, c[0], at + i * 4, 4, 52); s.add(OOHS, c[2], at + i * 4, 4, 52)
s.ostinato(at + 16, TEMPT[4:], 52, 62)
s.swell(PAD, bar(31), bar(33), 30, 110)
s.pads(PAD, bar(31), ['Dm', 'A'], 60)
s.roll(45, bar(32), 4, 40, 100)

# 6. Власть — такты 33–40: тема у всех, с ми-бемолем, и развязка на ре
at = bar(33)
s.expression(PAD, at, 100)
melody(STR, at, 104, shift=12, e=63, hold=4)
melody(HORN, at, 98, e=63, hold=4)
s.pads(CHOIR, at, FINAL, 64)
s.pads(PAD, at, FINAL, 64)
s.brass(at, FINAL, 84)
s.ostinato(at, FINAL[:7], 80, 98)
s.roots(at, FINAL, 70)
s.steps(at, FINAL[:7], 92, both=True)
s.tolls(at, FINAL, 78)
s.roll(38, bar(40), 4, 70, 118)                         # к последнему удару
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

s.save(sys.argv[1], 60)
print(f'{sys.argv[1]}: нот {s.count()}, конец на {s.end()} долях = {s.end():.0f} с')
