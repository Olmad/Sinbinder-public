# Тема Магистра — антагониста полной версии. Автор, 9 октября, о «Final
# Confrontation»: «звучит так, как будто тема главного антагониста
# и протагониста сливаются, и это красиво. Но, вероятно, нам нужно написать
# и саму тему антагониста». В архиве автора такой темы нет — она своя.
#
# Магистр в `00-GDD.md` §4 — «Греховод-отступник, убийца Корвина. Тёмное
# зеркало героя — использует ту же силу без ограничений». Так и здесь:
# его тема — тема Владыки в зеркале (каждая нота отражена вокруг ре).
#   Владыка:  ре–ля–соль–фа–ми–ре, си-бемоль–до-диез–ре — падает;
#   Магистр:  ре–соль–ля–си–до–ре, фа-диез–ми-бемоль–ре — поднимается.
# До-диез героя тянется к ре снизу — у Магистра на его месте ми-бемоль,
# нота отказа: давит на ре сверху. Ритм тот же: темы кровные.
# «Без ограничений» — в гармонии: каждая нота мелодии — терция мажорного
# аккорда, и аккорды лезут вверх без остановки: ми-бемоль, фа, соль,
# ля-бемоль, си-бемоль (у ферзя в «От пешки до ферзя» тот же ход — три
# ступени и дома; у Магистра — пять и дальше). Колокола (трубчатые) бьют
# ре и ля-бемоль — тритон; этого звука больше нигде нет: его услышат и узнают.
# Overlord — без подмигивания: Магистр всерьёз.
#
#   1. Колокол     (0:00) — колокола ре — ля-бемоль, орган держит ре, хор гудит;
#   2. Зеркало     (0:15) — тема у тромбонов и виолончелей, аккорды лезут вверх;
#   3. Без предела (0:44) — тема у валторн и скрипок, всем; подъём не кончается;
#   4. Двое        (1:13) — тема Владыки падает, тема Магистра поднимается —
#                           одновременно, над ре органа; сходятся на ре;
#   5. Колокол     (1:42) — ре и ля-бемоль, тритон не разрешается: Магистр жив.
# 32 такта, 1:56. Темп 66. Без жребия.
#   python3 Tools/music/magister_concept.py выход.mid
import sys
from orchestra import (Orchestra, CHORD, bar, lord_theme, HORN, STR, PAD, CELLO, BASS, BONE,
                       CHOIR, TIMP, ORGAN, OOHS, PICC, DRUMS)
from battles import cymbal

BELLS = PICC
s = Orchestra({BELLS: 100, HORN: 100, STR: 105, PAD: 66, CELLO: 105, BASS: 105, BONE: 100,
               CHOIR: 66, TIMP: 116, ORGAN: 84, OOHS: 80, DRUMS: 96}, program={BELLS: 14})


def mirror():
    """Тема Владыки в зеркале вокруг ре (124 − нота): тот же ритм."""
    return [(124 - p, st, d) for p, st, d in lord_theme()]


# под каждой нотой зеркала — мажор, где эта нота терция (кроме краёв)
HARM = [(0, 2, 'Dm'), (2, 2, 'Eb'), (4, 2, 'F'), (6, 2, 'G'), (8, 4, 'Ab'), (12, 4, 'Bb'),
        (16, 2, 'D'), (18, 2, 'Cm'), (20, 8, 'Dm')]


def chords(at, vel, organ=True, bone=False):
    for st, d, name in HARM:
        c = CHORD[name]
        s.add(BASS, c['bass'], at + st, d, vel + 8)
        if organ:
            for p in [c['bass'] + 12, *c['mid']]: s.add(ORGAN, p, at + st, d, vel)
        if bone:
            for p in c['bone']: s.add(BONE, p - 12, at + st, d, vel)


def toll(at, notes, vel):
    for i, p in enumerate(notes):
        s.add(BELLS, p, at + i * 4, 4, vel)


# 1. Колокол — такты 1–4
toll(bar(1), [62, 56, 62, 56], 92)
s.add(ORGAN, 26, bar(1), 16, 70); s.add(ORGAN, 38, bar(1), 16, 64)
for k in range(64): s.add(CELLO, 38, bar(1) + k * 0.25, 0.25, 46 + (8 if k % 4 == 0 else 0))
s.add(OOHS, 38, bar(1), 16, 60); s.add(OOHS, 50, bar(3), 8, 52)
s.swell(OOHS, bar(1), bar(5), 60, 100)

# 2. Зеркало — такты 5–12: тема внизу, аккорды лезут вверх
at = bar(5)
s.line(BONE, mirror(), at, 100, hold=4)
s.line(CELLO, mirror(), at, 92, shift=-12, hold=4)
chords(at, 62)
s.pads(OOHS, at, ['Dm', 'Eb', 'G', 'Ab', 'Bb', 'Bb', 'D', 'Dm'], 46)
for b in range(8): s.add(TIMP, 38, at + b * 4, 1.5, 78)
toll(at, [62], 80); toll(at + 28, [56], 80)

# 3. Без предела — такты 13–20: тема всем; подъём продолжается
at = bar(13)
s.line(HORN, mirror(), at, 104, hold=0)
s.line(STR, mirror(), at, 100, shift=12, hold=0)
s.line(BONE, mirror(), at, 66, shift=-12, hold=0)               # тромбоны под валторнами
chords(at, 72, bone=False)
s.ostinato(at, ['Dm', 'Eb', 'G', 'Ab', 'Bb', 'D'], 76, 90)
s.pads(CHOIR, at, ['Dm', 'Eb', 'G', 'Ab', 'Bb', 'D'], 56)
cymbal(s, at, 70)
# такты 19–20: вместо «дома» — дальше вверх: си-бемоль, до, ре, ми-бемоль — и тритон
CLIMB = [('Bb', 74), ('C', 76), ('D', 78), ('Eb', 79)]
for i, (name, top) in enumerate(CLIMB):
    t = bar(19) + i * 2
    c = CHORD[name]
    s.add(HORN, top - 12, t, 2, 100 + i * 4); s.add(STR, top, t, 2, 98 + i * 4)
    for p in c['mid']: s.add(ORGAN, p + 12, t, 2, 74)
    for p in c['bone']: s.add(BONE, p, t, 2, 90 + i * 3)
    s.add(BASS, c['bass'], t, 2, 96); s.add(TIMP, 38 if name in ('D', 'Bb') else 45, t, 1, 92 + i * 6)
s.roll(45, bar(20) + 2, 2, 90, 118)

# 4. Двое — такты 21–28: Владыка падает, Магистр поднимается — разом, над ре
at = bar(21)
s.expression(STR, at, 108)
s.line(STR, lord_theme(), at, 100, shift=12, hold=4)
s.line(HORN, lord_theme(), at, 92, hold=4)
s.line(BONE, mirror(), at, 96, hold=4)                          # Магистр чуть громче — его тема
s.line(CELLO, mirror(), at, 96, shift=-12, hold=4)
for p in (26, 38, 45): s.add(ORGAN, p, at, 32, 76)
s.add(BASS, 26, at, 32, 92)
for p in (38, 50): s.add(CHOIR, p, at, 32, 60)
toll(at, [62, 56, 62, 56, 62, 56, 62, 56], 86)
for b in range(8):
    s.add(TIMP, 38, at + b * 4, 1.5, 92); s.add(TIMP, 38, at + b * 4 + 2.5, 1, 78)
cymbal(s, bar(25), 76)

# 5. Колокол — такты 29–32: тритон не разрешается
at = bar(29)
for p in (26, 38, 45): s.add(ORGAN, p, at, 16, 72)
s.add(ORGAN, 44, bar(31), 8, 60)                                          # ля-бемоль под ре: тритон внизу
s.add(BASS, 26, at, 16, 72); s.add(OOHS, 50, at, 16, 52)
toll(at, [62, 56, 62], 80); s.add(BELLS, 56, bar(32), 6, 72)
s.add(TIMP, 38, at, 3, 84)
s.swell(ORGAN, bar(30), bar(33) + 2, 100, 0)
s.swell(OOHS, bar(30), bar(33) + 2, 100, 0)

s.save(sys.argv[1], 66)
print(f'{sys.argv[1]}: нот {s.count()}, конец на {s.end()} долях = {s.end() * 60 / 66:.1f} с')
