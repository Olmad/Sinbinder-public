# «The Eternal Grind» — по задумке. Автор, 2 октября: «„Grind“ — вот его
# невозможно хорошо сделать по тому плану, который собрал предшественник.
# Давай посмотрим, получится ли у тебя». Задумка со страницы архива:
# «Убей — забери душу. Гипнотический транс», темп 100.
#
# Материал автора, весь: остинато клавесина ре–ля восьмыми; тема Владыки
# быстро и низко (ре–ля–соль–фа–ми–ре у виолончели); «Ха!» хора на слабые
# доли; сигнал пикколо (та же тема, высоко и быстро); уход в фа минор
# (фа–до–си-бемоль–ля-бемоль–соль); литавры на ре.
#
# Почему по прежнему плану не выходило: два звука остинато и редкие вставки
# поверх — это не транс, а стоп-кадр. Транс держится на том, что пласты
# входят и уходят по одному, а пульс не меняется никогда. Поэтому здесь:
#   — остинато ре–ля автора, но с ударением 3+3+2 (восьмые: раз-два-три,
#     раз-два-три, раз-два) — два звука начинают качать;
#   — тема Владыки у басов — двухтактный рифф, на нём всё стоит;
#   — бой и жатва чередуются: «Убей» — удары меди и «Ха!» хора, на ре
#     и ми-бемоле (та же нота отказа, что в «Лорде» и «Bad is good»);
#     «Забери» — удары уходят, души поднимаются: хор «у» нарастает,
#     арфа взлетает, скрипки тянут тему Владыки целыми нотами;
#
#   1. Транс        (0:00) — клавесин один, потом литавры, потом бас;
#   2. Шаг          (0:19) — рифф басов, «Ха!» хора;
#   3. Сигнал       (0:38) — пикколо, литавры каждой долей;
#   4. Убей         (0:58) — медь и хор ударами, скрипки спускаются ре–до–си-бемоль–ля;
#   5. Забери       (1:17) — удары уходят, души поднимаются;
#   6. Глубже       (1:36) — фа минор, как у автора: всё на малую терцию выше;
#   7. Вершина      (1:55) — тема Владыки у валторн, скрипок и тромбонов, всё разом;
#   8. Отлив        (2:14) — пласты уходят;
#   9. Снова        (2:34) — транс, как в начале: следующий круг.
#
# Бой не кончается — и тема тоже: 72 такта ложатся петлёй без шва (--loop).
# Без --loop — тот же круг и последний удар в конце, для слушания.
# Темп 100: доля — 0,6 с, такт — 2,4 с. Без жребия.
#   python3 Tools/music/grind_concept.py выход.mid [--loop]
import sys
from orchestra import (Orchestra, bar, lord_theme, HORN, STR, PAD, CELLO, BASS, BONE, CHOIR,
                       TIMP, HARP, OOHS, HARPSI, PICC)

# Сведение — по замеру голосов (levels.py). «Ха!» хора — коротко, поэтому
# канал хора громче, чем в других темах: иначе удар тонет (первый замер —
# на 15–20 дБ ниже ведущего голоса).
VOLUME = {HORN: 100, STR: 105, PAD: 80, CELLO: 115, BASS: 110, BONE: 70, CHOIR: 100,
          TIMP: 120, HARP: 90, OOHS: 90, HARPSI: 95, PICC: 100}
BARS = 72
s = Orchestra(VOLUME)


def ostinato(at, bars, hi=62, lo=57, vel=70, acc=88):
    """Клавесин автора: ре–ля восьмыми; ударение 3+3+2 — качает."""
    for b in range(bars):
        for k in range(8):
            s.add(HARPSI, hi if k % 2 == 0 else lo, at + b * 4 + k * 0.5, 0.45, acc if k in (0, 3, 6) else vel)


RIFF = [(0, 0, 0.9), (-5, 1, 0.9), (-7, 2, 0.45), (-9, 2.5, 0.45), (-10, 3, 0.45), (0, 3.5, 1.4),
        (0, 5, 0.45), (0, 5.5, 0.45), (0, 6, 0.45), (0, 6.5, 0.45), (7, 7, 0.45), (0, 7.5, 0.45)]


def riff(at, pairs, root=38, vel=84, cello=True):
    """Тема Владыки у басов — ре–ля–соль–фа–ми–ре, потом шаг на ре.
    Два такта; контрабас как написано, виолончели октавой выше."""
    for p in range(pairs):
        for off, st, d in RIFF:
            t = at + p * 8 + st
            v = vel + (10 if st in (0, 3.5) else 0)
            s.add(BASS, root + off, t, d, v)
            if cello: s.add(CELLO, root + 12 + off, t, d, v - 6)


def drums(at, bars, mode, low=38, high=45, vel=80):
    """Литавры: '13' — на раз и три, 'all' — каждой долей, '8' — восьмыми."""
    for b in range(bars):
        t0 = at + b * 4
        if mode == '13':
            s.add(TIMP, low, t0, 1, vel); s.add(TIMP, low, t0 + 2, 1, vel - 10)
        elif mode == 'all':
            for k in range(4): s.add(TIMP, low if k != 3 else high, t0 + k, 1, vel if k == 0 else vel - 12)
        else:
            for k in range(8):
                s.add(TIMP, low if k not in (6, 7) else high, t0 + k * 0.5, 0.5, vel if k in (0, 3, 6) else vel - 20)


def ha(at, bars, notes=(50, 57), vel=80, every=2):
    """«Ха!» хора — коротко, на вторую и четвёртую доли."""
    for b in range(0, bars, every):
        for beat in (1, 3):
            for p in notes: s.add(CHOIR, p, at + b * 4 + beat, 0.35, vel)


def signal(at, root=74, vel=88):
    """Сигнал пикколо автора: тема Владыки высоко и быстро, с третьей доли."""
    for off, st, d in [(0, 2, 0.5), (7, 2.5, 0.5), (5, 3, 0.33), (3, 3.33, 0.33), (2, 3.67, 0.33), (0, 4, 2)]:
        s.add(PICC, root + off, at + st, d, vel)


DM, EB = ([50, 57], [62, 65, 69]), ([51, 58], [63, 67, 70])


def kill(at, bars, vel=100):
    """«Убей»: удары меди 3+3+2 — ре минор, через такт ми-бемоль."""
    for b in range(bars):
        for k, st in enumerate((0, 1.5, 3)):
            low, high = EB if (b % 2 == 1 and k < 2) else DM
            for p in low: s.add(BONE, p, at + b * 4 + st, 0.4, vel)
            for p in high: s.add(HORN, p, at + b * 4 + st, 0.4, vel - 6)


def flight(at, chord, vel=70):
    """Душа взлетает: арфа шестнадцатыми на две с лишним октавы вверх."""
    for k, p in enumerate(chord):
        s.add(HARP, p, at + k * 0.25, 1.5, vel + k * 2)


def piece(o):
    """Один круг: 72 такта от доли o."""
    # 1. Транс — такты 1–8
    ostinato(o + bar(1), 8, vel=66, acc=84)
    drums(o + bar(3), 6, '13', vel=72)
    for b in range(5, 9):
        for k in (0, 1.5, 3): s.add(BASS, 26, o + bar(b) + k, 0.9, 70)       # пульс баса 3+3+2
    # 2. Шаг — такты 9–16
    ostinato(o + bar(9), 8, vel=70, acc=88)
    riff(o + bar(9), 4, vel=78)
    drums(o + bar(9), 8, '13', vel=80)
    ha(o + bar(10), 7, vel=70)
    # 3. Сигнал — такты 17–24
    ostinato(o + bar(17), 8)
    riff(o + bar(17), 4, vel=82)
    drums(o + bar(17), 4, '13', vel=84); drums(o + bar(21), 4, 'all', vel=88)
    ha(o + bar(18), 7, vel=74)
    signal(o + bar(20)); signal(o + bar(24))                               # второй — прямо в «Убей»
    for p in (50, 57): s.add(PAD, p, o + bar(17), 32, 55)
    s.add(HORN, 62, o + bar(21), 16, 74)                                       # долгое ре, как у автора у пикколо
    # 4. Убей — такты 25–32
    ostinato(o + bar(25), 8, vel=74, acc=92)
    riff(o + bar(25), 4, vel=88)
    drums(o + bar(25), 8, '8', vel=96)
    kill(o + bar(25), 8)
    ha(o + bar(25), 8, vel=82, every=1)
    for i, p in enumerate((74, 72, 70, 69)):                                   # ре — до — си-бемоль — ля
        s.add(STR, p, o + bar(25) + i * 8, 8, 92); s.add(STR, p - 12, o + bar(25) + i * 8, 8, 84)
    # 5. Забери — такты 33–40: удары ушли, души поднимаются
    ostinato(o + bar(33), 8, vel=62, acc=78)
    riff(o + bar(33), 4, vel=70, cello=False)
    drums(o + bar(33), 8, '13', vel=70)
    rising = [[50, 53, 57], [53, 58, 62], [55, 58, 62], [57, 61, 64]]          # ре минор, си-бемоль, соль минор, ля
    for i, ch in enumerate(rising):
        t = o + bar(33) + i * 8
        for p in ch: s.add(OOHS, p, t, 8, 66)
        s.swell(OOHS, t, t + 6, 40, 110)
        flight(t, [ch[0] - 12, ch[0], ch[1], ch[2], ch[0] + 12, ch[1] + 12, ch[2] + 12, ch[0] + 24, ch[1] + 24, ch[2] + 24])
    s.line(STR, [(74, 0, 8), (81, 8, 8), (79, 16, 8), (77, 24, 4), (76, 28, 4)], o + bar(33), 84)  # тема Владыки целыми нотами
    s.expression(OOHS, o + bar(41), 100)
    signal(o + bar(39), vel=80)                                               # на ре, до фа минора
    # 6. Глубже — такты 41–48: фа минор
    ostinato(o + bar(41), 8, hi=65, lo=60, vel=74, acc=92)
    riff(o + bar(41), 4, root=41, vel=88)
    drums(o + bar(41), 4, '13', low=41, high=48, vel=88); drums(o + bar(45), 4, 'all', low=41, high=48, vel=92)
    ha(o + bar(41), 8, notes=(53, 60), vel=80)
    for p in (53, 56, 60): s.add(PAD, p, o + bar(41), 28, 60)
    for p in (53, 60): s.add(HORN, p, o + bar(41), 28, 54)                  # подкладка, не громче сигнала
    signal(o + bar(43), root=77); signal(o + bar(46), root=77)
    # к ре: ля мажор на последнем такте — тянет вниз, домой
    for p in (45, 52): s.add(BONE, p, o + bar(48), 4, 96)
    for p in (61, 64, 69): s.add(HORN, p, o + bar(48), 4, 96)
    for p in (57, 61, 64): s.add(PAD, p, o + bar(48), 4, 70)
    s.roll(45, o + bar(48), 4, 70, 118)
    # 7. Вершина — такты 49–56: тема Владыки у всех
    ostinato(o + bar(49), 8, vel=78, acc=96)
    riff(o + bar(49), 4, vel=92)
    drums(o + bar(49), 7, '8', vel=100)
    s.roll(38, o + bar(56), 4, 80, 120)
    s.line(HORN, lord_theme(), o + bar(49), 100, hold=4)
    s.line(STR, lord_theme(), o + bar(49), 104, shift=12, hold=4)
    s.line(BONE, lord_theme(), o + bar(49), 90, shift=-12, hold=4)
    ha(o + bar(49), 8, vel=84, every=1)
    # 8. Отлив — такты 57–64
    ostinato(o + bar(57), 8, vel=70, acc=86)
    riff(o + bar(57), 2, vel=84); riff(o + bar(61), 2, vel=74, cello=False)
    drums(o + bar(57), 4, 'all', vel=86); drums(o + bar(61), 4, '13', vel=76)
    ha(o + bar(58), 3, vel=66)
    for p in (50, 57): s.add(PAD, p, o + bar(57), 32, 50)
    s.swell(PAD, o + bar(61), o + bar(65), 100, 30)
    signal(o + bar(60), vel=66)
    # 9. Снова — такты 65–72: транс, как в начале
    s.expression(PAD, o + bar(65), 100)
    ostinato(o + bar(65), 8, vel=66, acc=84)
    drums(o + bar(65), 8, '13', vel=72)
    for b in range(65, 73):
        for k in (0, 1.5, 3): s.add(BASS, 26, o + bar(b) + k, 0.9, 70)


loop = '--loop' in sys.argv
piece(0)
if loop:
    piece(bar(BARS + 1))                     # второй круг: из него вырезается петля
else:
    at = bar(BARS + 1)                       # последний удар — для слушания
    for p in (50, 57): s.add(BONE, p, at, 3, 110)
    for p in (62, 65, 69): s.add(HORN, p, at, 3, 106)
    for p in (62, 65, 69, 74): s.add(STR, p, at, 3, 108)
    for p in (50, 57): s.add(CHOIR, p, at, 2, 84)
    s.add(BASS, 26, at, 4, 110); s.add(CELLO, 38, at, 4, 100); s.add(TIMP, 38, at, 3, 124)
    s.add(HARPSI, 62, at, 2, 96); s.add(HARPSI, 57, at, 2, 96)
s.save(sys.argv[1], 100)
sec = 0.6
print(f'{sys.argv[1]}: нот {s.count()}, конец на {s.end()} долях = {s.end() * sec:.1f} с'
      + (f'; петля — с {bar(BARS + 1) * sec:.1f} по {2 * bar(BARS + 1) * sec:.1f} с' if loop else ''))
