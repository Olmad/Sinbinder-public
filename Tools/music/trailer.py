# Музыка трейлера — черновик направления (автор, 29.10: «попробуй собрать музыку»).
#
# Ни одна запись проекта не строена дугой, какая нужна ролику: тишина → нарастание → удар → девиз.
# Здесь дуга собрана из наших же мотивов, чтобы игру узнавали по звуку:
#
#   1. Костёр   (0:00) — балалайка одна, тремоло: мотив «Крыс» вдвое медленнее, под ней — тихий ре внизу.
#                         Автору звук балалайки Ждана «мега крутой» — с него ролик и начинается.
#   2. Крысы    (0:13) — фагот ведёт мотив, кларнет отвечает, пиццикато шагом: отряд беглецов.
#   3. Охота    (0:25) — «Барабаны охоты»: сердце, крадущийся бас, тромбоны всё ближе, литавры — вверх.
#   4. Удар     (0:44) — все разом на ре минор — и полтакта тишины.
#   5. Путь     (0:50) — тема Безымянного широко: валторны и струнные, хор, арфа, литавры шагами.
#   6. Обрыв    (1:15) — всё смолкает; фагот один: ре–фа–соль-диез — и не разрешается. Девиз на экране.
#
# Темп 76, 26 тактов, около 1:25. Без жребия. Сводит и меряет master.py.
#   python Tools/music/trailer.py выход.mid
import sys
from orchestra import (Orchestra, CHORD, bar, HORN, STR, PAD, CELLO, BASS, BONE, CHOIR, TIMP,
                       HARP, OOHS, CEL, HARPSI, PIANO, BD, CYM)
from battles import drum, cymbal, low_pulse
from rats import MOTIF, motif

BALA, BSN, CLAR, PIZZ = PIANO, CEL, HARPSI, HARP
PROGRAM = {BALA: 105, BSN: 70, CLAR: 71, PIZZ: 45}
ROAD = [(62, 0, 1), (64, 1, 1), (65, 2, 1), (67, 3, 1), (69, 4, 2), (67, 6, 1), (65, 7, 1),
        (62, 8, 1), (67, 9, 1), (69, 10, 1), (71, 11, 1), (74, 12, 2), (71, 14, 1), (69, 15, 1),
        (69, 16, 1.5), (67, 17.5, 0.5), (65, 18, 1), (64, 19, 1), (62, 20, 1), (65, 21, 1), (69, 22, 2),
        (67, 24, 1.5), (65, 25.5, 0.5), (64, 26, 1), (62, 27, 1), (64, 28, 3)]
ROAD_CH = ['Dm', 'Dm', 'G', 'G', 'Dm', 'Dm', 'C', 'A']


def tremolo(s, ch, pitch, at, beats, vel, rate=0.125):
    """Балалаечное тремоло: одна нота частыми ударами, слабая доля тише."""
    n = int(beats / rate)
    for i in range(n):
        s.add(ch, pitch, at + i * rate, rate * 0.9, vel - (10 if i % 2 else 0))


def build():
    s = Orchestra({BALA: 104, BSN: 110, CLAR: 96, PIZZ: 96, HORN: 108, STR: 104, PAD: 70, CELLO: 100,
                   BASS: 100, BONE: 104, CHOIR: 88, TIMP: 104, OOHS: 74, 9: 104}, program=PROGRAM)

    # 1. Костёр — 1–4: мотив вдвое медленнее тремоло балалайки; ре внизу у виолончели, еле слышно.
    for p, st, d in MOTIF:
        tremolo(s, BALA, p + 12, bar(1) + st * 2, d * 2 - 0.25, 70)
    tremolo(s, BALA, 62, bar(4) - 2 + 2, 4, 62)
    s.add(CELLO, 38, bar(2), 12, 40)
    s.swell(CELLO, bar(2), bar(5), 30, 70)

    # 2. Крысы — 5–8: фагот, ответ кларнета на кварту выше, пиццикато шагом, тихий подклад.
    motif(s, BSN, bar(5), 92)
    motif(s, CLAR, bar(6) + 2, 84, root=55)
    motif(s, BSN, bar(7), 94, root=46)
    s.line(CLAR, [(69, 0, 1), (67, 1, 1), (65, 2, 1), (64, 3, 1)], bar(8), 82)
    for b, r in enumerate([38, 38, 34, 33]):
        for q in range(4):
            s.add(PIZZ, r if q % 2 == 0 else r + 7, bar(5) + b * 4 + q, 0.45, 64 + (8 if q == 0 else 0))
    s.pads(PAD, bar(5), ['Dm', 'Dm', 'Bb', 'A'], 46)

    # 3. Охота — 9–14: сердце (большой барабан и литавра «тук-тук»), крадущийся бас, тромбоны ближе
    #    с каждым тактом, струнные тремоло нарастают, к удару — дробь литавр.
    hunt = ['Dm', 'Dm', 'Bb', 'Bb', 'Gm', 'A']
    for b in range(6):
        v = 64 + b * 7
        drum(s, BD, bar(9) + b * 4, v, 0.5); drum(s, BD, bar(9) + b * 4 + 0.6, v - 14, 0.5)
        s.add(TIMP, 38, bar(9) + b * 4, 1, v - 6)
        if b >= 3:
            drum(s, BD, bar(9) + b * 4 + 2, v - 6, 0.5); drum(s, BD, bar(9) + b * 4 + 2.6, v - 18, 0.5)
    low_pulse(s, bar(9), hunt, vel=62, acc=74, breathe=0)
    for b, name in enumerate(hunt):
        if b < 2: continue
        for p in CHORD[name]['bone']:
            s.add(BONE, p, bar(9) + b * 4 + 3, 1, 70 + b * 6)
    for b, name in enumerate(hunt):
        for p in CHORD[name]['mid']:
            for i in range(16):
                s.add(STR, p + 12, bar(9) + b * 4 + i * 0.25, 0.25, 50)
    s.swell(STR, bar(9), bar(15), 40, 118)
    s.roll(45, bar(14), 4, 60, 118)

    # 4. Удар — 15: все на ре минор, держат две доли; дальше — тишина до 16-го такта на третьей доле.
    hit = bar(15)
    for ch in (HORN, STR, PAD):
        for p in CHORD['Dm']['mid']: s.add(ch, p + (12 if ch == STR else 0), hit, 2, 118)
    for p in CHORD['Dm']['bone']: s.add(BONE, p, hit, 2, 122)
    for p in (62, 65, 69): s.add(CHOIR, p, hit, 2.5, 110)
    s.add(BASS, 26, hit, 2, 120); s.add(CELLO, 38, hit, 2, 118)
    s.add(TIMP, 38, hit, 1.5, 124)
    drum(s, BD, hit, 124, 1); cymbal(s, hit, 118)

    # 5. Путь — 17–24: тема Безымянного широко. Валторны и скрипки в октаву, хор и струнные аккордами,
    #    арфа перебором, контрабас корнями, литавры шагами. Выход после тишины — мягче удара, но полнее.
    road = bar(16) + 3
    s.line(HORN, [(p - 12, st, d) for p, st, d in ROAD], road + 1, 100)
    s.line(STR, [(p + 12, st, d) for p, st, d in ROAD], road + 1, 92)
    s.pads(PAD, road + 1, ROAD_CH, 70)
    s.pads(OOHS, road + 1, ROAD_CH, 66)
    s.harp(road + 1, ROAD_CH, 62, 74)
    s.roots(road + 1, ROAD_CH, 92)
    s.ostinato(road + 1, ROAD_CH, 60, 74)
    s.steps(road + 1, ROAD_CH, 92, both=True)
    s.swell(HORN, road + 1, road + 1 + 32, 90, 120)

    # 6. Обрыв — 25: смолкли разом; фагот один — ре, фа, соль-диез, и держит без разрешения.
    cut = road + 1 + 32
    s.add(BSN, 50, cut + 1, 1, 84); s.add(BSN, 53, cut + 2, 1, 84); s.add(BSN, 56, cut + 3, 5, 88)
    s.swell(BSN, cut + 3, cut + 8, 100, 40)
    return s, 76


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "trailer.mid"
    s, bpm = build()
    s.save(out, bpm)
    print("[ТРЕЙЛЕР] {}: нот {}, долей {:.0f}, {:.0f} с".format(out, s.count(), s.end(), s.end() * 60 / bpm))
