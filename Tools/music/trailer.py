# Музыка трейлера — черновик направления (автор, 29.10: «попробуй собрать музыку»).
#
# Ни одна запись проекта не строена дугой, какая нужна ролику: тишина → нарастание → удар → девиз.
# Здесь дуга собрана из наших же мотивов, чтобы игру узнавали по звуку:
#
#   1. Костёр   (0:00) — балалайка одна, тремоло: мотив «Крыс» вдвое медленнее, под ней — тихий ре внизу.
#                         Автору звук балалайки Ждана «мега крутой» — с него ролик и начинается.
#   2. Крысы    (0:13) — фагот вырастает из гаснущей балалайки, кларнет отвечает, пиццикато шагом; сердце — еле.
#   3. Охота    (0:25) — «Барабаны охоты»: сердце громче, крадущийся бас, тромбоны ближе, литавры — вверх.
#   4. Удар     (0:44) — все разом на ре минор; литавры гаснут дробью, хор без слов тянет мост.
#   5. Путь     (0:47) — тема Безымянного широко, валторны вырастают за такт.
#   6. Слияние  (1:00) — поверх неё возвращаются свои: фагот и кларнет мотивом «Крыс», балалайка, шаг, сердце.
#   7. Конец    (1:12) — все тянут ре, нарастая, — снято; кларнет взвизгом мотивом вверх — и удар в ре. Точка.
#
# Темп 76, 25 тактов, около 1:20. Без жребия. Сводит и меряет master.py.
#   python Tools/music/trailer.py выход.mid
import sys
from orchestra import (Orchestra, CHORD, bar, HORN, STR, PAD, CELLO, BASS, BONE, CHOIR, TIMP,
                       HARP, OOHS, CEL, HARPSI, PIANO, PICC, BD, CYM)
from battles import drum, cymbal, low_pulse
from rats import MOTIF, motif

BALA, BSN, CLAR, PIZZ = PIANO, CEL, HARPSI, PICC   # пиццикато — на канале пикколо: арфа нужна своя
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
    s = Orchestra({BALA: 104, BSN: 110, CLAR: 96, PIZZ: 96, HARP: 84, HORN: 108, STR: 104, PAD: 70, CELLO: 100,
                   BASS: 100, BONE: 104, CHOIR: 88, TIMP: 104, OOHS: 74, 9: 104}, program=PROGRAM)

    # Переходы — внахлёст (автор, 29.10, черновик 2: «слишком резкие переходы инструментов»): уходящий голос
    # гаснет под входящим, входящий вырастает из тишины. Громкость канала (выразительность) ставится заново
    # там, где канал вступает: иначе спад одной части держится до конца (виолончели после вступления
    # так и остались тихими до удара — черновики 1 и 2).

    # 1. Костёр — 1–4: мотив вдвое медленнее тремоло балалайки; ре внизу у виолончели, еле слышно.
    for p, st, d in MOTIF:
        tremolo(s, BALA, p + 12, bar(1) + st * 2, d * 2 - 0.25, 50)
    tremolo(s, BALA, 62, bar(4), 8, 44)                 # держит ре и под входом фагота…
    s.swell(BALA, bar(5), bar(7), 100, 0)               # …и гаснет за два такта
    s.add(CELLO, 38, bar(2), 12, 40)
    s.swell(CELLO, bar(2), bar(5), 24, 56)

    # 2. Крысы — 5–8: фагот вырастает из балалайки, кларнет отвечает, пиццикато шагом, тихий подклад.
    s.swell(BSN, bar(5), bar(6), 55, 100)
    motif(s, BSN, bar(5), 78)
    motif(s, CLAR, bar(6) + 2, 70, root=55)
    motif(s, BSN, bar(7), 80, root=46)
    s.line(CLAR, [(69, 0, 1), (67, 1, 1), (65, 2, 1), (64, 3, 1)], bar(8), 70)
    # Шаг пиццикато доходит и в охоту — гаснет на 10-м такте: отряд идёт, а за ним уже идут.
    for b, r in enumerate([38, 38, 34, 33, 38, 38]):
        for q in range(4):
            s.add(PIZZ, r if q % 2 == 0 else r + 7, bar(5) + b * 4 + q, 0.45, max(30, 66 - (b - 3) * 12 if b > 3 else 66) + (8 if q == 0 else 0))
    s.pads(PAD, bar(5), ['Dm', 'Dm', 'Bb', 'A'], 46)
    # Сердце начинается ещё под «Крысами» — еле слышно.
    for b in (2, 3):
        drum(s, BD, bar(5) + b * 4, 30 + b * 4, 0.5); drum(s, BD, bar(5) + b * 4 + 0.6, 22 + b * 4, 0.5)

    # 3. Охота — 9–14: сердце громче, крадущийся бас, тромбоны ближе с каждым тактом, струнные тремоло
    #    нарастают, к удару — дробь литавр. Обрывок мотива у фагота внизу — отряд ещё здесь.
    s.expression(CELLO, bar(9), 100)
    motif(s, BSN, bar(9), 62, root=38, upto=3, hold=1)
    s.swell(BSN, bar(9), bar(11), 100, 30)
    hunt = ['Dm', 'Dm', 'Bb', 'Bb', 'Gm', 'A']
    for b in range(6):
        v = 48 + b * 13
        drum(s, BD, bar(9) + b * 4, v, 0.5); drum(s, BD, bar(9) + b * 4 + 0.6, v - 14, 0.5)
        s.add(TIMP, 38, bar(9) + b * 4, 1, v - 6)
        if b >= 3:
            drum(s, BD, bar(9) + b * 4 + 2, v - 6, 0.5); drum(s, BD, bar(9) + b * 4 + 2.6, v - 18, 0.5)
    low_pulse(s, bar(9), hunt, vel=62, acc=74, breathe=0)
    s.swell(BASS, bar(9), bar(10), 50, 100)
    for b, name in enumerate(hunt):
        if b < 2: continue
        for p in CHORD[name]['bone']:
            s.add(BONE, p, bar(9) + b * 4 + 3, 1, 70 + b * 6)
    for b, name in enumerate(hunt):
        for p in CHORD[name]['mid']:
            for i in range(16):
                s.add(STR, p + 12, bar(9) + b * 4 + i * 0.25, 0.25, 50)
    s.swell(STR, bar(9), bar(15), 22, 122)
    s.roll(45, bar(14), 4, 60, 118)

    # 4. Удар — 15: все на ре минор, две доли. Тишины нет: литавры гаснут дробью, хор без слов тянет ре —
    #    мост в «Путь».
    hit = bar(15)
    s.expression(BSN, hit, 100); s.expression(BALA, hit, 100); s.expression(STR, hit + 2.5, 100)
    for ch in (HORN, STR, PAD):
        for p in CHORD['Dm']['mid']: s.add(ch, p + (12 if ch == STR else 0), hit, 2, 118)
    for p in CHORD['Dm']['bone']: s.add(BONE, p, hit, 2, 122)
    for p in (62, 65, 69): s.add(CHOIR, p, hit, 2.5, 110)
    s.add(BASS, 26, hit, 2, 120); s.add(CELLO, 38, hit, 2, 118)
    s.add(TIMP, 38, hit, 1.5, 124)
    drum(s, BD, hit, 124, 1); cymbal(s, hit, 118)
    s.roll(38, hit + 1.5, 2.5, 70, 30)
    for p in (50, 57): s.add(OOHS, p, hit + 1.5, 3.5, 60)
    s.swell(OOHS, hit + 1.5, hit + 5, 50, 90)

    # 5. Путь — 16–23: тема Безымянного широко; валторны вырастают за такт, а не врубаются.
    road = bar(16)
    s.swell(HORN, road, road + 4, 55, 105)
    s.line(HORN, [(p - 12, st, d) for p, st, d in ROAD], road, 100)
    s.line(STR, [(p + 12, st, d) for p, st, d in ROAD], road, 92)
    s.pads(PAD, road, ROAD_CH, 70)
    s.pads(OOHS, road, ROAD_CH, 66)
    s.harp(road, ROAD_CH, 62, 74)
    s.roots(road, ROAD_CH, 80)
    # без шага виолончелей: низ и так полон (спектр черновика 1: каша ниже до малой)
    s.steps(road, ROAD_CH, 92, both=True)

    #    Слияние (автор: «может ближе к концу они должны слиться?») — вторая половина пути, 20–23: поверх
    #    темы Безымянного возвращаются свои — фагот и кларнет перекликаются мотивом «Крыс», балалайка тремоло
    #    вторит теме сверху, пиццикато снова шагает, сердце бьётся. Все темы — разом, одной дорогой.
    merge = bar(20)
    s.swell(HORN, merge, merge + 16, 105, 124)
    motif(s, BSN, merge, 86, root=50)
    motif(s, CLAR, merge + 4, 84, root=62)
    motif(s, BSN, merge + 8, 90, root=55)
    motif(s, CLAR, merge + 12, 92, root=65, upto=4, hold=1)
    for p, st, d in ROAD[14:]:
        tremolo(s, BALA, p + 12, road + st, d, 70)
    for b, r in enumerate([38, 38, 36, 33]):
        for q in range(4):
            s.add(PIZZ, r if q % 2 == 0 else r + 7, merge + b * 4 + q, 0.45, 70 + (8 if q == 0 else 0))
    for b in range(4):
        v = 70 + b * 12
        drum(s, BD, merge + b * 4, v, 0.5); drum(s, BD, merge + b * 4 + 0.6, v - 14, 0.5)

    # 6. Конец — 24–25 (автор: «не хватает логического окончания — типа резкий кларнет и удар барабана»).
    #    Все тянут ре минор, нарастая, дробь литавр вверх — снято разом; миг тишины; кларнет взвизгом
    #    взлетает мотивом (ре–фа–соль-диез) — и удар: низы, медь, барабан, тарелка и балалайка — в ре, коротко.
    #    Соль-диез разрешился: дорога кончилась точкой, а не многоточием.
    end = bar(24)
    for ch, notes, v in ((HORN, CHORD['Dm']['mid'], 116), (STR, [p + 12 for p in CHORD['Dm']['mid']], 112),
                         (PAD, CHORD['Dm']['mid'], 96), (OOHS, CHORD['Dm']['mid'], 86), (CHOIR, (62, 65, 69), 100),
                         (BONE, CHORD['Dm']['bone'], 108)):
        for p in notes: s.add(ch, p, end, 3, v)
    s.add(BASS, 26, end, 3, 100); s.add(CELLO, 38, end, 3, 96)
    s.swell(STR, end, end + 3, 80, 127); s.swell(CHOIR, end, end + 3, 80, 127)
    s.roll(38, end, 3, 70, 124)
    shriek = end + 3.25
    for i, p in enumerate((74, 77, 80)):
        s.add(CLAR, p, shriek + i * 0.18, 0.17, 118)
    last = end + 4
    s.expression(CLAR, last - 1, 127)
    s.add(CLAR, 86, last, 0.6, 127)
    for p in (26, 38): s.add(BASS if p == 26 else CELLO, p, last, 0.8, 127)
    for p in (38, 50): s.add(BONE, p, last, 0.8, 127)
    s.add(HORN, 50, last, 0.8, 124)
    s.add(TIMP, 38, last, 1.0, 127)
    drum(s, BD, last, 127, 1); cymbal(s, last, 124)
    for p in (62, 65, 69): s.add(BALA, p, last, 0.4, 110)
    return s, 76


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "trailer.mid"
    s, bpm = build()
    s.save(out, bpm)
    # --stems папка — каждый канал своим файлом: отрисовать порознь и смотреть спектром (spectro.py --stems),
    # кто где звучит и кто кого закрывает. Имена — по инструменту.
    if "--stems" in sys.argv:
        from pathlib import Path
        names = {BALA: "балалайка", BSN: "фагот", CLAR: "кларнет", PIZZ: "пиццикато", HORN: "валторны", STR: "скрипки",
                 PAD: "струнные аккорды", CELLO: "виолончели", BASS: "контрабасы", BONE: "тромбоны", CHOIR: "хор",
                 TIMP: "литавры", OOHS: "хор без слов", 9: "ударные", HARP: "арфа"}
        folder = Path(sys.argv[sys.argv.index("--stems") + 1]); folder.mkdir(parents=True, exist_ok=True)
        for ch in list(s.events):
            one = Orchestra(s.volume, program=PROGRAM)
            one.events = {ch: s.events[ch]}
            one.controls = {ch: s.controls.get(ch, [])}
            one.save(str(folder / "{:02d} {}.mid".format(ch, names.get(ch, "канал {}".format(ch)))), bpm)
    print("[ТРЕЙЛЕР] {}: нот {}, долей {:.0f}, {:.0f} с".format(out, s.count(), s.end(), s.end() * 60 / bpm))
