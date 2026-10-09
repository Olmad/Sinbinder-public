# «Bad is good, but Evil is better» — по задумке. Автор, 2 октября: своя
# версия «Возвращения лорда» понравилась больше — «давай теперь „Хорошего —
# плохого“». Задумка со страницы архива: «Полная версия. Путь от надежды
# до тьмы»; её начало, «Evil in mind», — «Ночная тема Склепа. Хрусталь
# и шёпот». Части страницы: ночь, восхищение, разочарование, собирание сил,
# шаг вперёд, кода органа.
#
# Два мотива автора, и они из одних нот:
#   хрустальный «Evil in mind» — ля–соль–ре–фа–ми (челеста, высоко);
#   «A Lord Returns»           — ре–ля–соль–фа–ми.
# Мысль о зле и тема Владыки — одни и те же пять нот в другом порядке.
# На этом и стоит версия: в кульминации они звучат разом, одна над другой.
#
#   1. Ночь склепа    (0:00) — хрусталь челесты, шёпот хора, ре и фа виолончели;
#                              в конце — намёк на тему Владыки;
#   2. Надежда        (0:32) — тема Владыки в ре МАЖОРЕ у скрипок, арфа:
#                              у автора — «восхищение», арпеджио ре мажора;
#   3. Разочарование  (1:04) — мажор гаснет в минор; ре и ми-бемоль разом
#                              у виолончелей (как у автора); бас сползает
#                              по полутонам — ре, до-диез, до, си, си-бемоль, ля;
#                              хор стонет: ми — ми-бемоль — ре;
#   4. Собирание сил  (1:28) — хрустальный мотив внизу, у валторн, с ми-бемолем:
#                              мысль стала намерением; шаг растёт из ничего;
#   5. Шаг вперёд     (2:00) — тема Владыки у всех, с ми-бемолем, орган и хор;
#                              над ней челеста ведёт хрустальный мотив;
#   6. Кода           (2:32) — ми-бемоль мажор — ре минор (фригийская развязка),
#                              орган один; челеста вспоминает хрусталь.
# Ми-бемоль — нота отказа из «Возвращения лорда»: та же нота, тот же смысл.
#
# Темп 60: доля — секунда, такт — четыре. Без жребия.
#   python3 Tools/music/bad_concept.py выход.mid [--loop] [--less-celesta]
import sys
from score import loop_tempo
from orchestra import (Orchestra, CHORD, bar, lord_theme, HORN, STR, PAD, CELLO, BASS, BONE,
                       CHOIR, TIMP, ORGAN, HARP, OOHS, CEL, PICC)

# Сведение — как у «Возвращения лорда», по замеру голосов (levels.py):
# ведущий голос части громче всех, хор — шёпотом; стон хора в «Разочаровании»
# и арфа «Надежды» подняты: по первому замеру их было не слышно (−57 и −56).
VOLUME = {HORN: 100, STR: 105, PAD: 80, CELLO: 105, BASS: 100, BONE: 60, CHOIR: 62,
          TIMP: 120, ORGAN: 70, HARP: 90, OOHS: 95, CEL: 127, PICC: 96}
s = Orchestra(VOLUME)

# --less-celesta — автор, 2 октября: «местами колокольчик кажется не сильно
# уместным. Но в конце он очень актуален». Челеста остаётся ночи склепа
# и коде; блеск «Надежды» и обрывки «Разочарования» — арфе, хрусталь над
# оркестром в «Шаге вперёд» — пикколо: она прорезает тутти, а не звенит.
less = '--less-celesta' in sys.argv
SHINE = HARP if less else CEL      # блеск и обрывки
ABOVE = PICC if less else CEL      # хрусталь над оркестром

CRYSTAL = [(81, 0, 2), (79, 2, 2), (86, 4, 2), (89, 6, 2), (88, 8, 4)]   # ля–соль–ре–фа–ми


# 1. Ночь склепа — такты 1–8
s.add(PAD, 50, 0, 32, 50); s.add(PAD, 57, 0, 32, 50)
s.swell(PAD, 0, 8, 20, 70)
s.add(CELLO, 38, 2, 30, 52); s.add(CELLO, 41, 10, 16, 48)          # ре и фа, как у автора
s.line(CEL, CRYSTAL, 4, 90)
s.line(CEL, [(81, 0, 2), (79, 2, 4)], 18, 78)                     # отзвук: ля–соль
s.line(CEL, [(62, 0, 1), (69, 1, 1), (67, 2, 1), (65, 3, 1), (64, 4, 2), (62, 6, 2)], 24, 70)  # тема Владыки — намёком
for p, st, d in [(50, 6, 2), (50, 14, 2), (62, 22, 4)]:
    s.add(OOHS, p, st, d, 50)                                    # шёпот

# 2. Надежда — такты 9–16: тема Владыки в ре мажоре
HOPE = ['D', 'G', 'A', 'Bm', 'Em', 'A', 'D', 'D']
at = bar(9)
hope = [(74, 0, 2), (81, 2, 2), (79, 4, 2), (78, 6, 2), (76, 8, 4), (74, 12, 4),
        (71, 16, 2), (73, 18, 2), (74, 20, 8)]                  # фа-диез, си — мажор
s.expression(STR, at, 80); s.swell(STR, at, at + 8, 80, 100)
s.line(STR, hope, at, 92, hold=4)
s.expression(PAD, at, 90)
s.pads(PAD, at, HOPE, 55)
s.harp(at, HOPE, 56, 64)
for i, name in enumerate(HOPE):
    s.add(CELLO, CHORD[name]['cello'], at + i * 4, 4, 60)
s.roots(at, HOPE, 48)
for st in (0, 24):                                               # арпеджио ре мажора, как у автора
    s.line(SHINE, [(74, 0, 1), (78, 0.5, 1), (81, 1, 1), (86, 1.5, 3)], at + st, 70)
s.line(HORN, [(59, 16, 4), (61, 20, 4), (62, 24, 8)], at, 70)   # валторна поднимается к ре
for p in (50, 54, 57):
    s.add(CHOIR, p, at + 24, 8, 50)                              # сияние

# 3. Разочарование — такты 17–22
at = bar(17)
for p in (50, 53, 57):
    s.add(PAD, p, at, 4, 55); s.add(CHOIR, p, at, 4, 50)        # фа-диез стал фа: мажор погас
s.line(HARP, [(74, 0, 1.2), (69, 0.5, 1.2), (65, 1, 1.2), (62, 1.5, 1.2), (57, 2, 2), (53, 2.5, 2), (50, 3, 2)], at, 56)
s.add(STR, 74, at, 4, 80); s.swell(STR, at, at + 4, 90, 40)
for k in range(16):                                              # ре и ми-бемоль разом — тремоло
    v = 45 + k * 2
    s.add(CELLO, 38, at + 2 + k * 0.25, 0.25, v); s.add(CELLO, 39, at + 2 + k * 0.25, 0.25, v)
s.add(BASS, 26, at + 2, 4, 50); s.add(BASS, 27, at + 2, 4, 50)
# бас сползает по полутонам: до-диез, до, си, си-бемоль, ля
LAMENT = [(49, 37, [52, 57, 61]), (48, 36, [53, 56, 60]), (47, 35, [50, 55, 59]),
          (46, 34, [50, 55, 58]), (45, 33, [49, 52, 55, 57])]
for i, (cello, bass, mid) in enumerate(LAMENT):
    t = bar(18) + i * 4
    s.add(CELLO, cello, t, 4, 58); s.add(BASS, bass, t, 4, 46)
    for p in mid: s.add(PAD, p, t, 4, 52)
s.line(OOHS, [(64, 0, 4), (63, 4, 4), (62, 8, 8), (61, 16, 4)], bar(18), 70)     # ми — ми-бемоль — ре — до-диез
s.line(SHINE, [(81, 0, 2), (79, 2, 2), (86, 8, 2), (89, 10, 2), (88, 16, 4)], bar(18), 66)  # хрусталь — врозь
s.roll(45, bar(22), 4, 35, 70)

# 4. Собирание сил — такты 23–30: хрусталь внизу, у валторн; шаг растёт
GATHER = ['Dm', 'Dm', 'Eb', 'Dm', 'Dm', 'Bb', 'Eb', 'A']
at = bar(23)
s.line(HORN, [(57, 0, 2), (55, 2, 2), (62, 4, 2), (65, 6, 2), (63, 8, 4), (62, 12, 4)], at, 80)
intent = [(57, 16, 2), (55, 18, 2), (62, 20, 2), (65, 22, 2), (63, 24, 4), (64, 28, 4)]   # на ля — снова ми
s.line(HORN, intent, at, 92)
s.line(BONE, intent, at, 78, shift=-12)
s.expression(PAD, at, 50); s.swell(PAD, at, at + 32, 50, 110)
s.pads(PAD, at, GATHER, 55)
for i in range(2):                                               # сначала шаг редкий: литавры половинными
    s.add(TIMP, 38, at + i * 4, 1.5, 55); s.add(TIMP, 38, at + i * 4 + 2, 1.5, 60)
    for k in range(4): s.add(CELLO, 38, at + i * 4 + k, 0.9, 55)
s.ostinato(bar(25), GATHER[2:4], 55, 66)
s.ostinato(bar(27), GATHER[4:6], 66, 80)
s.ostinato(bar(29), GATHER[6:8], 76, 92)
s.roots(bar(27), GATHER[4:], 60)
for k in range(4): s.add(TIMP, 38, bar(27) + k, 1, 70 + (8 if k == 0 else 0))
s.roll(45, bar(30), 4, 60, 115)
s.pads(CHOIR, bar(27), GATHER[4:], 55)
s.expression(ORGAN, bar(29), 40); s.swell(ORGAN, bar(29), bar(31), 40, 100)
s.pads(ORGAN, bar(29), GATHER[6:], 55)
s.expression(STR, bar(29), 70); s.swell(STR, bar(29), bar(31), 70, 110)
for p in (67, 70, 75): s.add(STR, p, bar(29), 4, 80)             # ми-бемоль мажор наверху
for p in (69, 73, 76): s.add(STR, p, bar(30), 4, 86)             # ля мажор — тянет к ре

# 5. Шаг вперёд — такты 31–38: тема Владыки у всех, хрусталь над ней
STEP = ['Dm', 'Dm', 'Eb', 'Bb', 'Gm', 'A', 'Dm', 'Dm']
at = bar(31)
for ch in (STR, ORGAN): s.expression(ch, at, 100)
s.line(STR, lord_theme(63), at, 104, shift=12, hold=4)
s.line(HORN, lord_theme(63), at, 98, hold=4)
s.line(ABOVE, CRYSTAL[:4] + [(87, 8, 4), (86, 12, 4), (82, 16, 2), (85, 18, 2), (86, 20, 12)], at, 100 if ABOVE == CEL else 60)
s.brass(at, STEP, 84)
s.pads(ORGAN, at, STEP, 60)
s.pads(CHOIR, at, STEP, 62)
s.ostinato(at, STEP[:7], 80, 98)
s.roots(at, STEP, 70)
for i, name in enumerate(STEP[:7]):                              # литавры каждой четвертью, как у автора
    t = CHORD[name]['timp']
    if t is None: continue
    for k, v in enumerate((100, 84, 92, 84)):
        s.add(TIMP, t, at + i * 4 + k, 1, v)
s.roll(38, bar(38), 4, 70, 118)
for ch in (STR, HORN, CHOIR):
    s.swell(ch, bar(38), bar(39), 100, 127)

# 6. Кода — такты 39–45: ми-бемоль мажор — ре минор, и орган один
at = bar(39)
for ch in (STR, HORN, CHOIR):
    s.expression(ch, at + 0.01, 110)
for p in (63, 67, 70, 75): s.add(STR, p, at, 4, 108)
for p in (58, 63, 67): s.add(HORN, p, at, 4, 104)
for p in (51, 58): s.add(BONE, p, at, 4, 100)
for p in (51, 55, 58): s.add(CHOIR, p, at, 4, 70)
for p in (39, 51, 55, 58): s.add(ORGAN, p, at, 4, 70)
s.add(BASS, 27, at, 4, 84); s.add(CELLO, 39, at, 4, 96)
at = bar(40)
for p in (62, 65, 69, 74): s.add(STR, p, at, 2, 112)
for p in (57, 62, 65): s.add(HORN, p, at, 2, 106)
for p in (50, 57): s.add(BONE, p, at, 2, 104)
for p in (50, 53, 57): s.add(CHOIR, p, at, 2, 72)
s.add(BASS, 26, at, 2, 88); s.add(CELLO, 38, at, 2, 98)
s.add(TIMP, 38, at, 3, 120)
for p in (38, 50, 53, 57): s.add(ORGAN, p, at, 24, 70)          # орган держит ре минор один
s.swell(ORGAN, at + 14, at + 24, 100, 0)
s.line(CEL, CRYSTAL + [(86, 12, 8)], bar(41), 80)                # хрусталь — и ре
s.add(OOHS, 50, bar(42), 12, 50)
s.add(CELLO, 38, bar(41), 18, 45)
s.add(TIMP, 38, bar(44), 3, 55)

tempo = 1_000_000                           # мкс на долю: темп 60
if '--loop' in sys.argv:
    s.repeat(bar(46))                       # петля: 45 тактов, второй проход вырезается
    tempo = loop_tempo(60, bar(46))         # 60,48: круг кратен блоку синтезатора (43-SOUND, «Шов петли»)
s.save(sys.argv[1], 60e6 / tempo)
print(f'{sys.argv[1]}: нот {s.count()}, конец на {s.end()} долях = {s.end() * tempo / 1e6:.0f} с'
      + (f'; петля — с {bar(46) * tempo / 1e6:.6f} по {2 * bar(46) * tempo / 1e6:.6f} с' if '--loop' in sys.argv else ''))
