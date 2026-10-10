# Боевые темы архива автора — по задумке. Автор, 9 октября: «давай доделаем
# музыку». Шесть боёв полной игры, каждый — петля без шва (--loop).
#   python3 Tools/music/battles.py <тема> выход.mid [--loop]
#   темы: rat raid hunt gate war final hunted (вторая волна набега — в игре)
#
# Урок «Grind» (автор: повторяющийся звук «вечно звучит в ушах и
# раздражает») — во всех шести:
#   — пульс есть всегда, но внизу и мягко (басы, литавры, барабан), а не
#     высоко и колко;
#   — гармония ходит: ни одна тема не стоит на ре дольше восьми тактов;
#   — пульс дышит: такт тишины через три, части без пульса;
#   — высокий повтор — только краска на несколько тактов.
# И для игры: бой идёт минуты, и музыка не спорит с ударами и голосами —
# середина (1–4 кГц) отдана им, мелодия — у меди и струнных пониже.
# Без жребия.
import sys
from score import loop_tempo
from orchestra import (Orchestra, CHORD, bar, lord_theme, HORN, STR, PAD, CELLO, BASS, BONE,
                       CHOIR, TIMP, ORGAN, HARP, OOHS, CEL, HARPSI, PICC, PIANO,
                       DRUMS, BD, SD, CYM, CYM2, WOOD_HI, WOOD_LO)


# ---------- общее ----------

def low_pulse(s, at, chords, vel=70, acc=82, breathe=4, cello=True):
    """Пульс внизу: корень аккорда на долях 3+3+2 (раз, «два-и», четыре).
    Каждый breathe-й такт молчит — пульс дышит."""
    for b, name in enumerate(chords):
        if breathe and b % breathe == breathe - 1: continue
        c = CHORD[name]
        for k, st in enumerate((0, 1.5, 3)):
            v = acc if k == 0 else vel
            s.add(BASS, c['bass'], at + b * 4 + st, 1.2, v)
            if cello: s.add(CELLO, c['cello'], at + b * 4 + st, 1.2, v - 8)


def drum(s, note, at, vel, dur=0.5):
    s.add(DRUMS, note, at, dur, vel)


def cymbal(s, at, vel=80):
    drum(s, CYM, at, vel, 3)


# ---------- The Rat Pack ----------

def rat():
    """«Хаотичная стычка. Быстро и суетливо», темп 140. У автора: фагот
    ре–фа–соль-диез–ми–ре (ре–фа–соль-диез — уменьшённое трезвучие: крысы
    бегут по нему) и пиццикато ре четвертями.
    Своё: деревянные коробочки — лапки; канон фагота с кларнетом — суета
    (кларнет внизу, на кварту выше фагота: октавой выше он колол слух —
    98 % всей энергии 2–6 кГц);
    бас уходит из-под мотива на си-бемоль, соль-диез (тритон) и ля; в середине
    мотив восьмыми идёт вверх по малым терциям (ре, фа, соль-диез, си) —
    круг без выхода. Ни меди, ни хора: стычка, а не битва. 32 такта, 0:55."""
    BSN, CLAR, PIZZ = CEL, HARPSI, HARP
    s = Orchestra({BSN: 110, CLAR: 96, PIZZ: 100, PICC: 110, CELLO: 96, BASS: 96, TIMP: 90,
                   DRUMS: 100, PAD: 60}, program={BSN: 70, CLAR: 71, PIZZ: 45})
    MOTIF = [(50, 0, 1), (53, 1, 1), (56, 2, 1), (52, 3, 1), (50, 4, 2)]   # автора: ре–фа–соль-диез–ми–ре

    def feet(at, bars, vel=56):
        """Лапки: шестнадцатые на коробочках 3+3+2, такт тишины через три.
        Высоко — значит, только краской: начало, середина, конец."""
        for b in range(bars):
            if b % 4 == 3: continue
            for k in (0, 3, 6, 8, 11, 14):
                drum(s, WOOD_HI if k % 2 == 0 else WOOD_LO, at + b * 4 + k * 0.25, vel + (10 if k == 0 else 0), 0.25)

    def pizz(at, roots, vel=62):
        """Пиццикато автора — четвертями, но внизу и через корни."""
        for b, r in enumerate(roots):
            if b % 4 == 3: continue
            for k in range(4):
                s.add(PIZZ, r if k % 2 == 0 else r - 5, at + b * 4 + k, 0.5, vel + (8 if k == 0 else 0))

    def squeak(at, vel=70):
        """Писк пикколо: короткая трель ре–ми-бемоль."""
        for k in range(6):
            s.add(PICC, 86 if k % 2 == 0 else 87, at + k * 0.125, 0.125, vel)

    # A — такты 1–8: лапки (четыре такта — краска, не обои), пиццикато, мотив, писк
    feet(bar(1), 4)
    pizz(bar(1), [50] * 8)
    s.line(BSN, MOTIF, bar(1) + 1, 92); s.line(BSN, MOTIF, bar(5) + 1, 96)
    squeak(bar(4) + 2); squeak(bar(8) + 2, 76)
    # B — такты 9–16: канон фагот — кларнет; бас уходит из-под мотива
    roots = [38, 38, 34, 34, 32, 32, 33, 33]                              # ре, си-бемоль, соль-диез, ля
    for b, r in enumerate(roots):
        if b % 4 != 3:
            s.add(BASS, r, bar(9) + b * 4, 1, 80); s.add(BASS, r, bar(9) + b * 4 + 2, 1, 70)
    for k in range(4):
        s.line(BSN, MOTIF, bar(9) + k * 8, 96)
        s.line(CLAR, MOTIF, bar(9) + k * 8 + 1, 84, shift=5)             # на долю позже, на кварту выше — низкий кларнет, тёмный
    squeak(bar(12) + 3); squeak(bar(16) + 3, 78)
    # C — такты 17–24: мотив восьмыми вверх по малым терциям — круг без выхода
    feet(bar(17), 8, 64)
    for b in range(8):
        root = [50, 53, 56, 59][b % 4]
        for k, (p, st, d) in enumerate(MOTIF):
            s.add(BSN, p - 50 + root, bar(17) + b * 4 + st * 0.5, d * 0.5, 90 + 2 * k)
            s.add(CLAR, p - 50 + root + 5, bar(17) + b * 4 + 2 + st * 0.5, d * 0.5, 78)
        dim = {50: 'Dm', 53: 'G#dim', 56: 'G#dim', 59: 'G#dim'}[root]
        if b % 4 != 3:
            s.add(PIZZ, CHORD[dim]['cello'] + 12, bar(17) + b * 4, 0.5, 70)
            s.add(TIMP, 38 if root == 50 else 45, bar(17) + b * 4, 0.5, 72)
    for p in (47, 50, 53, 56): s.add(PAD, p, bar(17), 32, 40)             # уменьшённый септаккорд — тихо, под всем
    # D — такты 25–32: как в начале, тоньше; к петле
    feet(bar(29), 4, 54)
    pizz(bar(25), [50] * 8, 58)
    s.line(BSN, MOTIF, bar(25) + 1, 88); s.line(CLAR, MOTIF, bar(29) + 1, 80, shift=5)
    squeak(bar(28) + 2, 64)
    return s, 140, bar(33)


# ---------- The Routine Raid ----------

def raid():
    """«Лёгкая битва. Марш в стиле Overlord 2», темп 120. У автора: струнные
    играют тему Владыки половинными (ре–ля–соль–фа–ми–ре), литавры и хор
    на ре половинными.
    Своё: малый барабан — марш; туба «ум-па» на раз и три, пиццикато «па» на
    два и четыре — марш Overlord, ориентир всей музыки (автор, 9 октября);
    ответы валторн четвертями — фанфара; середина в фа мажоре — рутина
    уверенная, светлее ре минора; брейк одних барабанов. Лёгкая: хор — только
    в начале, медь без тромбонов на полную. 48 тактов, 1:36."""
    TUBA, PIZZ = PIANO, HARP
    s = Orchestra({STR: 105, HORN: 96, BONE: 64, CELLO: 96, BASS: 100, TIMP: 110, CHOIR: 70,
                   DRUMS: 110, PAD: 60, TUBA: 127, PIZZ: 86}, program={TUBA: 58, PIZZ: 45})
    MOTIF = [(62, 0, 2), (69, 2, 2), (67, 4, 2), (65, 6, 2), (64, 8, 4), (62, 12, 4)]    # автора
    FANFARE = [(62, 0, 1), (69, 1, 1), (67, 2, 1), (65, 3, 1), (64, 4, 2), (62, 6, 2)]

    def snare(at, bars, vel=62, mode='full'):
        """Марш: 'full' — малый барабан дробью; 'light' — только раз и три;
        'bass' — один большой. Малый барабан не звучит стеной весь трек."""
        for b in range(bars):
            t = at + b * 4
            if mode == 'full':
                for st, v in ((0, 16), (1, 0), (1.5, -8), (2, 12), (3, 0), (3.25, -10), (3.5, -4)):
                    drum(s, SD, t + st, vel + v)
            elif mode == 'light':
                drum(s, SD, t, vel + 6); drum(s, SD, t + 2, vel)
            drum(s, BD, t, vel + 10); drum(s, BD, t + 2, vel)

    def umpa(at, chords, vel=76, pah=False):
        """Бас марша: туба — корень на раз, квинта на три, контрабас под корнем.
        pah — «па» пиццикато на два и четыре; такт тишины через три: «па»
        высоко над тубой, и без передышки оно станет клавесином «Grind»."""
        for b, name in enumerate(chords):
            c = CHORD[name]
            t = at + b * 4
            s.add(TUBA, c["cello"], t, 0.9, vel + 16); s.add(TUBA, c["bone"][1] - 12, t + 2, 0.9, vel + 8)
            s.add(BASS, c['bass'], t, 1, vel - 6)
            if pah and b % 4 != 3:
                for st in (1, 3):
                    for p in c['mid']: s.add(PIZZ, p, t + st, 0.4, vel - 14)

    D8 = ['Dm', 'Dm', 'Bb', 'Bb', 'Gm', 'Gm', 'A', 'A']
    ORDER = ['Dm', 'Dm', 'Bb', 'Bb', 'Gm', 'A', 'Dm', 'A']
    F8 = ['F', 'F', 'C', 'C', 'Bb', 'Bb', 'C', 'A']
    # A — такты 1–8: два такта барабана, мотив автора у струнных, ответ валторн
    snare(bar(1), 8)
    s.line(STR, MOTIF, bar(3), 92)
    umpa(bar(3), ['Dm', 'Dm', 'Bb', 'Dm', 'Gm', 'A'], pah=True)
    for b in range(3, 7):
        s.add(TIMP, 38, bar(b), 2, 84); s.add(TIMP, 38, bar(b) + 2, 2, 74)
    for b in (3, 4): s.add(CHOIR, 50, bar(b), 2, 56); s.add(CHOIR, 50, bar(b) + 2, 2, 52)   # автора, тихо
    s.line(HORN, FANFARE, bar(7), 90)
    # B — такты 9–16: тема Владыки у струнных, валторны держат аккорды
    snare(bar(9), 8, 58, 'light')
    s.line(STR, lord_theme(), bar(9), 96, shift=12)
    s.pads(HORN, bar(9), ORDER, 58)
    umpa(bar(9), ORDER)
    s.steps(bar(9), ORDER, 86)
    # C — такты 17–24: фа мажор — мотив у валторн, струнные отвечают
    snare(bar(17), 8, 56, 'bass')
    s.line(HORN, [(65, 0, 2), (72, 2, 2), (70, 4, 2), (69, 6, 2), (67, 8, 4), (65, 12, 4)], bar(17), 92)
    s.line(STR, [(77, 16, 1), (84, 17, 1), (82, 18, 1), (81, 19, 1), (79, 20, 2), (77, 22, 2), (76, 24, 8)], bar(17), 86)
    umpa(bar(17), F8, pah=True)
    s.pads(PAD, bar(17), F8, 48)
    # D — такты 25–32: тема Владыки у меди, струнные пульсом — только здесь
    snare(bar(25), 8, 64)
    s.line(HORN, lord_theme(), bar(25), 98)
    s.line(BONE, lord_theme(), bar(25), 80, shift=-12)
    for b, name in enumerate(D8):
        c = CHORD[name]
        for k in range(8):
            s.add(STR, c['mid'][2] + 12 if k % 2 == 0 else c['mid'][1] + 12, bar(25) + b * 4 + k * 0.5, 0.3, 58 if k % 4 else 70)
    umpa(bar(25), ORDER, 80)
    s.steps(bar(25), ORDER, 92, both=True)
    cymbal(s, bar(25), 70)
    # E — такты 33–40: брейк барабанов, потом мотив автора снова
    snare(bar(33), 8, 70)
    for b in range(33, 37): s.add(TIMP, 38, bar(b), 1, 96); s.add(TIMP, 45, bar(b) + 3, 1, 88)
    s.line(STR, MOTIF, bar(37), 96)
    umpa(bar(37), ['Dm', 'Bb', 'Gm', 'A'])
    # F — такты 41–48: фанфара всеми, к ре — и в начало
    snare(bar(41), 8, 66)
    for k in range(2):
        s.line(HORN, FANFARE, bar(41) + k * 8, 96)
        s.line(STR, FANFARE, bar(41) + k * 8, 90, shift=12)
    umpa(bar(41), D8, 80, pah=True)
    s.steps(bar(41), D8, 92, both=True)
    s.pads(PAD, bar(41), D8, 52)
    s.roll(45, bar(48), 4, 60, 100)
    return s, 120, bar(49)


# ---------- The Hunt Begins ----------

def hunt():
    """«Средняя битва. Тактическое напряжение», темп 110. У автора: валторна
    поднимается ре–ми–фа–соль по такту на ноту, литавры бьют ре в каждый такт.
    Своё: подъём доведён до октавы через си-бемоль и до-диез (гармонический
    минор — петля затягивается); под каждой ступенью свой аккорд; в середине
    тема Владыки у тромбонов — у охоты есть хозяин; потом передышка —
    охотники перегруппировались — и подъём снова. 48 тактов, 1:45."""
    TREM, PIZZ = HARP, CEL
    s = Orchestra({HORN: 100, STR: 100, BONE: 78, CELLO: 100, BASS: 100, TIMP: 115, PAD: 62,
                   TREM: 80, PIZZ: 90, DRUMS: 100}, program={TREM: 44, PIZZ: 45})
    RISE = [(62, 0, 4), (64, 4, 4), (65, 8, 4), (67, 12, 4), (69, 16, 4), (70, 20, 4), (73, 24, 4), (74, 28, 4)]
    STEPS = ['Dm', 'Edim', 'F', 'Gm', 'A', 'Bb', 'C#dim', 'Dm']
    ORDER = ['Dm', 'Dm', 'Bb', 'Bb', 'Gm', 'A', 'Dm', 'A']

    def ticks(at, bars, vel=52):
        """Часы охоты: пиццикато на два и четыре — тихо, по ступени."""
        for b in range(bars):
            p = CHORD[STEPS[b % 8]]['mid'][2]
            s.add(PIZZ, p, at + b * 4 + 1, 0.4, vel); s.add(PIZZ, p, at + b * 4 + 3, 0.4, vel - 6)

    def trem(at, chords, vel=50):
        for i, name in enumerate(chords):
            for p in CHORD[name]['mid'][1:]: s.add(TREM, p + 12, at + i * 4, 4, vel)

    # A — такты 1–8: подъём у валторны (автора — первые четыре ступени), литавры ре
    s.line(HORN, RISE, bar(1), 88)
    low_pulse(s, bar(1), STEPS, 64, 76)
    s.pads(PAD, bar(1), STEPS, 46)
    for b in range(8): s.add(TIMP, 38, bar(1 + b), 1.5, 84)
    # B — такты 9–16: подъём у скрипок и валторны каноном, часы
    s.line(STR, RISE, bar(9), 86, shift=12)
    s.line(HORN, RISE, bar(9) + 2, 84)
    low_pulse(s, bar(9), STEPS, 68, 80)
    ticks(bar(9), 8)
    for b in range(8): s.add(TIMP, 38, bar(9 + b), 1.5, 88)
    # C — такты 17–24: тема Владыки у тромбонов — у охоты есть хозяин
    s.line(BONE, lord_theme(), bar(17), 96, shift=-12)
    s.line(HORN, lord_theme(), bar(17), 74)
    low_pulse(s, bar(17), ORDER, 70, 82)
    trem(bar(17), ORDER)
    s.steps(bar(17), ORDER, 90)
    # D — такты 25–32: всё поднимается разом, литавры восьмыми
    for ch, sh, v in ((HORN, 0, 98), (STR, 12, 96), (BONE, -12, 86)):
        s.line(ch, RISE, bar(25), v, shift=sh)
    low_pulse(s, bar(25), STEPS, 74, 88, breathe=0)
    trem(bar(25), STEPS, 58)
    for b in range(8):
        for k in range(8):
            s.add(TIMP, 38 if k < 6 else 45, bar(25 + b) + k * 0.5, 0.5, (98 if k in (0, 3, 6) else 78))
    cymbal(s, bar(25), 64); cymbal(s, bar(29), 74); cymbal(s, bar(32), 84)
    # E — такты 33–40: передышка — охотники перегруппировались
    low_pulse(s, bar(33), ['Dm'] * 8, 56, 66, breathe=2)
    s.add(HORN, 62, bar(33), 16, 60); s.add(HORN, 61, bar(37), 8, 58); s.add(HORN, 62, bar(39), 8, 60)
    s.add(TREM, 62, bar(33), 32, 40); s.add(TREM, 69, bar(33), 32, 40)
    s.add(TIMP, 38, bar(33), 2, 80)
    # F — такты 41–48: подъём снова — и в начало
    s.line(HORN, RISE, bar(41), 92)
    s.line(STR, RISE, bar(41) + 2, 84, shift=12)
    low_pulse(s, bar(41), STEPS, 68, 80)
    ticks(bar(41), 8, 50)
    s.pads(PAD, bar(41), STEPS, 50)
    for b in range(8): s.add(TIMP, 38, bar(41 + b), 1.5, 90)
    s.roll(45, bar(48) + 2, 2, 60, 96)
    return s, 110, bar(49)


# ---------- The Gate of Bone ----------

def gate():
    """«Тяжёлый оборонительный бой», темп 75. У автора: тромбон ре — ми-бемоль —
    ре — до-диез — ре (осада с двух сторон: полтона сверху, полтона снизу),
    хор держит ре, литавры — целыми.
    Своё: таран — большой барабан с литаврами «бум-бум — БУМ» (и раз в четыре
    такта — один удар: передышка); в середине тема Владыки у виолончелей
    и контрабасов — гордость защитников, а медь давит ми-бемолем
    и до-диезом; потом стена дрожит — ми-бемоль мажор и уменьшённый на
    до-диез над ре; и линия автора всеми. 36 тактов, 1:55."""
    s = Orchestra({BONE: 96, HORN: 92, STR: 96, CELLO: 105, BASS: 110, CHOIR: 70, TIMP: 120,
                   PAD: 64, DRUMS: 110, ORGAN: 64})
    LINE = [(50, 0, 4), (51, 4, 4), (50, 8, 4), (49, 12, 4), (50, 16, 8)]     # автора
    LINE_CH = ['Dm', 'Eb', 'Dm', 'C#dim', 'Dm', 'Dm']

    def ram(at, bars, vel=96):
        for b in range(bars):
            t = at + b * 4
            if b % 4 == 3:
                drum(s, BD, t, vel - 10, 1); s.add(TIMP, 38, t, 1, vel - 10); continue
            for st, v, d in ((0, 0, 0.8), (1, -12, 0.8), (2, 14, 1.8)):
                drum(s, BD, t + st, vel + v, d); s.add(TIMP, 38, t + st, d, vel + v - 6)

    # A — такты 1–6: таран, линия автора у тромбонов, хор держит ре
    ram(bar(1), 6, 90)
    s.line(BONE, LINE, bar(1), 92)
    s.add(CHOIR, 50, bar(1), 8, 64); s.add(CHOIR, 50, bar(3), 8, 68)
    s.add(BASS, 26, bar(1), 24, 70)
    # B — такты 7–12: линия у тромбонов и валторн октавами, виолончели дрожат
    ram(bar(7), 6, 96)
    s.line(BONE, LINE, bar(7), 96); s.line(HORN, LINE, bar(7), 90, shift=12)
    for i, name in enumerate(LINE_CH):
        r = CHORD[name]['cello']
        for k in range(16): s.add(CELLO, r, bar(7) + i * 4 + k * 0.25, 0.25, 62 + (10 if k % 4 == 0 else 0))
    s.pads(PAD, bar(7), LINE_CH, 50)
    s.add(CHOIR, 50, bar(7), 24, 62)
    # C — такты 13–24: тема Владыки внизу — гордость; медь давит полутонами
    ORDER = ['Dm', 'Dm', 'Bb', 'Bb', 'Gm', 'A', 'Dm', 'A']
    s.line(CELLO, lord_theme(), bar(13), 98, shift=-12)
    s.line(BASS, lord_theme(), bar(13), 92, shift=-24)
    s.pads(ORGAN, bar(13), ORDER, 56)
    ram(bar(13), 8, 88)
    for b in range(0, 8, 2):                                               # давление: ми-бемоль и до-диез на слабые доли
        for p in (51, 63): s.add(BONE, p, bar(13 + b) + 1.5, 0.6, 90)
        for p in (49, 61): s.add(HORN, p, bar(14 + b) + 3, 0.6, 86)
    s.roll(38, bar(20) + 2, 2, 70, 104)
    s.line(HORN, LINE, bar(21), 86)
    ram(bar(21), 4, 92)
    s.add(BASS, 26, bar(21), 16, 74)
    # D — такты 25–30: стена дрожит — ми-бемоль мажор и уменьшённый над ре
    for b in range(6):
        name = 'Eb' if b % 2 == 0 else 'C#dim'
        t = bar(25 + b)
        for p in CHORD[name]['mid']: s.add(STR, p + 12, t, 4, 82); s.add(PAD, p, t, 4, 60)
        for p in CHORD[name]['bone']: s.add(BONE, p, t, 4, 80)
        s.add(BASS, 26, t, 4, 84)
        s.swell(STR, t, t + 4, 70, 110)
        if b % 2 == 0: cymbal(s, t, 60 + b * 6)
    ram(bar(25), 6, 100)
    s.add(CHOIR, 51, bar(25), 4, 64); s.add(CHOIR, 49, bar(26), 4, 64); s.add(CHOIR, 50, bar(27), 16, 70)
    # E — такты 31–36: линия автора всеми — и в начало
    s.expression(STR, bar(31), 100)
    s.line(BONE, LINE, bar(31), 104); s.line(HORN, LINE, bar(31), 98, shift=12)
    s.line(STR, LINE, bar(31), 96, shift=24)
    s.add(CHOIR, 50, bar(31), 24, 72); s.add(CHOIR, 57, bar(33), 16, 64)
    s.add(BASS, 26, bar(31), 24, 88)
    ram(bar(31), 6, 104)
    cymbal(s, bar(31), 80)
    return s, 75, bar(37)


# ---------- The Architect's War ----------

def war():
    """«Субботняя битва. Продуктивная ярость», темп 120. У автора: рояль молотит
    ре–ре–фа–ми–ре, ля–соль–фа–ми–ре; хор бьёт ре четвертями; литавры четвертями.
    В игре — штурм: игрок нападает и строит победу.
    Своё: молот и наковальня — рояль октавами и литавры; ярость строит этажи —
    мотив поднимается: ре, ми, фа, соль; тема Владыки у меди над шагом
    виолончелей; брейк — один рояль с литаврами, потом барабаны.
    48 тактов, 1:36."""
    s = Orchestra({PIANO: 120, HORN: 100, STR: 100, BONE: 76, CELLO: 105, BASS: 105, CHOIR: 80,
                   TIMP: 118, PAD: 60, DRUMS: 100})
    MOTIF = [(62, 0, 1), (62, 1, 1), (65, 2, 1), (64, 3, 1), (62, 4, 2), (69, 6, 1), (67, 7, 1),
             (65, 8, 1), (64, 9, 1), (62, 10, 4)]                                # автора
    ORDER = ['Dm', 'Dm', 'Bb', 'Bb', 'Gm', 'A', 'Dm', 'A']

    def hammer(at, shift=0, vel=96):
        s.line(PIANO, MOTIF, at, vel, shift=shift)
        s.line(PIANO, MOTIF, at, vel - 10, shift=shift - 12)

    def anvil(at, bars, vel=90):
        for b in range(bars):
            for k in range(4): s.add(TIMP, 38 if k != 3 else 45, at + b * 4 + k, 1, vel if k == 0 else vel - 14)

    # A — такты 1–8: молот и наковальня; хор автора в первых двух тактах
    hammer(bar(1)); anvil(bar(1), 4)
    for k in range(4): s.add(CHOIR, 50, bar(1) + k, 1, 76)
    s.add(CHOIR, 50, bar(2), 2, 84)
    s.line(HORN, MOTIF, bar(5), 88)
    low_pulse(s, bar(5), ['Dm'] * 4, 66, 80)
    anvil(bar(5), 4, 84)
    # B — такты 9–16: шаг басов по аккордам, удары меди, мотив у меди и рояля
    low_pulse(s, bar(9), ORDER, 70, 84)
    for b, name in enumerate(ORDER):
        for st in (0, 1.5, 3):
            for p in CHORD[name]['bone']: s.add(BONE, p, bar(9 + b) + st, 0.4, 84 if st else 92)
    s.line(HORN, MOTIF, bar(9), 94); hammer(bar(13), vel=90)
    s.steps(bar(9), ORDER, 92, both=True)
    # C — такты 17–24: этажи — мотив поднимается: ре, ми, фа, соль
    for i, (sh, name) in enumerate(((0, 'Dm'), (2, 'Em'), (3, 'Fm'), (5, 'Gm'))):
        t = bar(17 + i * 2)
        s.line(HORN, MOTIF[:5], t, 90 + i * 3, shift=sh)
        s.line(PIANO, MOTIF[:5], t, 88 + i * 3, shift=sh - 12)
        low_pulse(s, t, [name, name], 70 + i * 2, 84 + i * 2, breathe=0)
        s.pads(PAD, t, [name, name], 50)
        s.add(TIMP, 38, t, 1, 90 + i * 4); s.add(TIMP, 45, t + 4, 1, 86 + i * 4)
    cymbal(s, bar(23), 70)
    # D — такты 25–32: тема Владыки у меди, рояль бьёт сильные доли
    s.line(HORN, lord_theme(), bar(25), 100)
    s.line(BONE, lord_theme(), bar(25), 88, shift=-12)
    s.line(STR, lord_theme(), bar(25), 94, shift=12)
    s.ostinato(bar(25), ORDER, 80, 94)
    s.roots(bar(25), ORDER, 78)
    for b, name in enumerate(ORDER):
        bb = CHORD[name]['bass']
        s.add(PIANO, bb + 12, bar(25 + b), 2, 96); s.add(PIANO, bb + 24, bar(25 + b), 2, 90)
    s.steps(bar(25), ORDER, 96, both=True)
    # E — такты 33–40: брейк — один рояль с литаврами, потом барабаны
    hammer(bar(33), vel=100); anvil(bar(33), 4, 96)
    for b in range(37, 41):
        t = bar(b)
        drum(s, BD, t, 96); drum(s, BD, t + 2, 88)
        for st in (1, 3): drum(s, SD, t + st, 70 + (b - 37) * 6)
    hammer(bar(37), vel=96)
    s.roll(38, bar(40) + 2, 2, 70, 110)
    # F — такты 41–48: мотив всеми — и в начало
    cymbal(s, bar(41), 84)
    for k in range(2):
        t = bar(41) + k * 16
        s.line(HORN, MOTIF, t, 98); s.line(STR, MOTIF, t, 94, shift=12); hammer(t, vel=96)
    low_pulse(s, bar(41), ['Dm', 'Dm', 'Bb', 'A'] * 2, 74, 88)
    anvil(bar(41), 8, 92)
    for k in range(4): s.add(CHOIR, 50, bar(41) + k, 1, 80)
    return s, 120, bar(49)


# ---------- The Final Confrontation ----------

def final():
    """«Финальный босс. Эпическая битва», темп 90. У автора: орган — тема
    Владыки целыми нотами; хор ре, потом ля во всю силу; литавры тяжело.
    Своё: последний бой собирает весь саундтрек — тему Владыки, хрустальный
    мотив «Evil in mind» над ней (как в «Bad is good»), удары тритоном
    и тему тритоном выше (как в «Ultimatum»), ми-бемоль — ноту отказа,
    и провал перед последним натиском. 60 тактов, 2:40."""
    s = Orchestra({ORGAN: 90, HORN: 100, STR: 105, BONE: 72, CELLO: 105, BASS: 105, CHOIR: 70,
                   TIMP: 120, PAD: 64, OOHS: 80, PICC: 96, DRUMS: 100})
    AUTHOR = [(62, 0, 4), (69, 4, 4), (67, 8, 4), (65, 12, 4), (64, 16, 8), (62, 24, 8)]
    CYCLE = ['Dm', 'Dm', 'Bb', 'Bb', 'Gm', 'Gm', 'A', 'A']
    ORDER = ['Dm', 'Dm', 'Bb', 'Bb', 'Gm', 'A', 'Dm', 'A']
    INVERTED = ['G#m', 'G#m', 'E', 'E', 'C#m', 'Eb', 'G#m', 'Eb']
    FINAL = ['Dm', 'Dm', 'Eb', 'Bb', 'Gm', 'A', 'Dm', 'Dm']
    CRYSTAL = [(81, 0, 2), (79, 2, 2), (86, 4, 2), (89, 6, 2), (88, 8, 4), (86, 12, 4),
               (82, 16, 2), (85, 18, 2), (86, 20, 8)]

    def organ_chords(at, chords, vel=64):
        for i, name in enumerate(chords):
            c = CHORD[name]
            for p in [c['bass'] + 12, *c['mid']]: s.add(ORGAN, p, at + i * 4, 4, vel)

    # A — такты 1–8: тема автора у органа, хор автора, литавры
    s.line(ORGAN, AUTHOR, bar(1), 100); s.line(ORGAN, AUTHOR, bar(1), 90, shift=-12)
    for k in range(4): s.add(CHOIR, 50, bar(1) + k * 4, 4, 70)
    s.add(CHOIR, 57, bar(5), 8, 80)
    for k in range(4): s.add(TIMP, 38, bar(1) + k * 2, 1.5, 96)
    for k in range(4): s.add(TIMP, 38, bar(3) + k * 4, 2, 110)
    s.add(BASS, 26, bar(1), 32, 72)
    # B — такты 9–16: шаг басов, удары меди (на ре миноре — с ля-бемолем), зов валторн
    low_pulse(s, bar(9), CYCLE, 72, 86)
    for b, name in enumerate(CYCLE):
        t = bar(9 + b)
        hits = ((0, name), (1.5, name), (3, 'Ab' if name == 'Dm' else name))
        for st, ch in hits:
            for p in CHORD[ch]['bone']: s.add(BONE, p, t + st, 0.45, 92)
            for p in CHORD[ch]['mid']: s.add(HORN, p + 12, t + st, 0.45, 80)
    s.line(STR, AUTHOR[:4], bar(11), 88, shift=12)
    s.steps(bar(9), CYCLE, 92, both=True)
    # C — такты 17–24: тема Владыки, над ней хрусталь
    s.line(STR, lord_theme(), bar(17), 100, shift=12)
    s.line(HORN, lord_theme(), bar(17), 96)
    s.line(PICC, CRYSTAL, bar(17), 64)
    organ_chords(bar(17), ORDER)
    low_pulse(s, bar(17), ORDER, 74, 88)
    s.steps(bar(17), ORDER, 96, both=True)
    s.pads(CHOIR, bar(17), ORDER, 54)
    # D — такты 25–32: тема тритоном выше — сила босса
    s.line(STR, lord_theme(), bar(25), 98, shift=18)
    s.line(HORN, lord_theme(), bar(25), 94, shift=6)
    organ_chords(bar(25), INVERTED, 66)
    low_pulse(s, bar(25), INVERTED, 74, 88)
    for b in range(7): s.add(TIMP, 38, bar(25 + b), 1.5, 96)                # литавры держат ре — тритоном под чужим
    s.roll(45, bar(32), 4, 80, 116)
    # E — такты 33–40: провал — ре и ми-бемоль, потом натиск копится
    s.add(ORGAN, 26, bar(33), 16, 70); s.add(ORGAN, 38, bar(33), 16, 66)
    s.add(OOHS, 50, bar(33), 16, 60); s.add(OOHS, 57, bar(33), 16, 54)
    s.line(CELLO, [(50, 0, 2), (57, 2, 2), (55, 4, 2), (53, 6, 2), (51, 8, 4), (50, 12, 4)], bar(33), 84)  # ре–ля–соль–фа — ми-бемоль — ре
    low_pulse(s, bar(37), ['Dm', 'Dm', 'A', 'A'], 70, 84, breathe=0)
    s.roll(38, bar(37), 16, 50, 118)
    for p in (57, 61, 64): s.add(STR, p + 12, bar(39), 8, 84)
    s.swell(STR, bar(39), bar(41), 60, 120)
    # F — такты 41–52: последний натиск — тема с ми-бемолем, всеми
    s.expression(STR, bar(41), 110)
    s.line(STR, lord_theme(63), bar(41), 106, shift=12, hold=4)
    s.line(HORN, lord_theme(63), bar(41), 100, hold=4)
    s.line(BONE, lord_theme(63), bar(41), 88, shift=-12, hold=4)
    s.line(ORGAN, lord_theme(63), bar(41), 96, shift=12, hold=4)
    organ_chords(bar(41), FINAL, 70)
    low_pulse(s, bar(41), FINAL, 78, 92, breathe=0)
    s.pads(CHOIR, bar(41), FINAL, 60)
    for b in range(7):
        for k in range(8):
            s.add(TIMP, 38 if k < 6 else 45, bar(41 + b) + k * 0.5, 0.5, 104 if k in (0, 3, 6) else 82)
    cymbal(s, bar(41), 88); cymbal(s, bar(45), 80)
    for b, name in enumerate(['Gm', 'A', 'A', 'Dm']):                     # такты 49–52: каденция
        t = bar(49 + b)
        for p in CHORD[name]['mid']: s.add(STR, p + 12, t, 4, 100); s.add(HORN, p, t, 4, 96)
        for p in CHORD[name]['bone']: s.add(BONE, p, t, 4, 90)
        s.add(BASS, CHORD[name]['bass'], t, 4, 96)
    s.roll(45, bar(50), 8, 80, 120)
    s.add(TIMP, 38, bar(52), 2, 124); cymbal(s, bar(52), 92)
    # G — такты 53–60: шаг и зовы — натиск копится к началу круга
    low_pulse(s, bar(53), CYCLE, 70, 84)
    for b in (0, 2, 4, 6):
        s.line(HORN, [(62, 0, 1), (69, 1, 1), (67, 2, 1), (65, 3, 1)], bar(53 + b), 88 + b * 2)
    s.pads(PAD, bar(53), CYCLE, 50)
    s.steps(bar(53), CYCLE, 90)
    s.roll(38, bar(60), 4, 70, 110)
    return s, 90, bar(61)


# ---------- The Hunted — вторая волна набега ----------

def hunted():
    """Автор, 10 октября: «сделать 2 волну более страшной и с музыкальной
    стороны». Вторая волна — не бой, а бегство: лагерь потерян, «вероятно,
    от одного из наших», видец находит Греховода раз в семь секунд. Значит,
    не подмигивание Overlord, а жуть (ориентир: «смех ломает жуть» — потеря
    звучит всерьёз). Тёмная редакция «The Hunt Begins»: охотились — теперь
    охотятся на нас.

    Из «Hunt» — мотив автора (валторна ре–ми–фа–соль) и часы пиццикато.
    Своё: сердце (большой барабан и литавры «тук-тук»); крадущийся полутон
    в басах — корень и полутон выше, у ре это ми-бемоль, нота отказа; дрожь
    струнных малой секундой; хор шёпотом тритоном (ре и ля-бемоль); тема
    Владыки у тромбонов с ми-бемолем вместо ми и обрывом — хозяина гонят.
    Урок «Grind» — и здесь: басы дышат (такт тишины через три), гармония
    ходит, высокого повтора нет. 40 тактов, около 1:26."""
    TREM, PIZZ = PAD, CEL
    s = Orchestra({HORN: 100, STR: 92, TREM: 72, CELLO: 96, BASS: 104, BONE: 86, OOHS: 84,
                   TIMP: 112, PIZZ: 86, PIANO: 84, DRUMS: 104}, program={TREM: 44, PIZZ: 45})
    MOTIF = [(62, 0, 4), (64, 4, 4), (65, 8, 4), (67, 12, 4)]          # у автора — ре–ми–фа–соль
    RISE = MOTIF + [(69, 16, 4), (70, 20, 4), (73, 24, 4), (74, 28, 4)]  # до октавы, как в «Hunt»

    def heart(at, bars, vel, double=False, skip=4):
        """Сердце: «тук» — барабан с литаврой ре, «тук» тише — на полдоли
        позже. Бегом — дважды в такт. Каждый skip-й такт оно замирает."""
        for b in range(bars):
            if skip and b % skip == skip - 1: continue
            for beat in ((0, 2) if double else (0,)):
                t = at + b * 4 + beat
                drum(s, BD, t, vel); drum(s, BD, t + 0.5, vel - 16)
                s.add(TIMP, 38, t, 0.5, vel - 10)

    def stalk(at, chords, vel, eighths=False):
        """Крадётся: корень и полутон выше, внизу. Такт тишины через три."""
        step = 0.5 if eighths else 1.0
        for b, name in enumerate(chords):
            if b % 4 == 3: continue
            r = CHORD[name]['bass']
            for k in range(int(4 / step)):
                p = r + (1 if k % 2 else 0)
                v = vel + (8 if k == 0 else 0)
                s.add(BASS, p, at + b * 4 + k * step, step * 0.9, v)
                s.add(CELLO, p + 12, at + b * 4 + k * step, step * 0.9, v - 10)

    def ticks(at, bars, vel, fast=False):
        """Часы охоты из «Hunt»: пиццикато на два и четыре; бегом — восьмыми."""
        for b in range(bars):
            for k in (range(8) if fast else (2, 6)):
                s.add(PIZZ, 62 if k % 4 < 2 else 63, at + b * 4 + k * 0.5, 0.3, vel - (6 if k % 2 else 0))

    def shiver(at, bars, v0, v1):
        """Дрожь: тремоло струнных малой секундой — ре и ми-бемоль, с нарастанием."""
        for p in (62, 63):
            s.add(TREM, p, at, bars * 4, 64)
        s.swell(TREM, at, at + bars * 4, v0, v1)

    def whisper(at, bars, vel, notes=(62, 68)):
        """Хор шёпотом — тритон ре и ля-бемоль."""
        for p in notes:
            s.add(OOHS, p, at, bars * 4, vel)

    def hits(at, bars, vel):
        """Удары тритоном: тромбоны ре и ля-бемоль с литаврой на «три-и»."""
        for b in range(bars):
            t = at + b * 4 + 2.5
            s.add(BONE, 50, t, 1.2, vel); s.add(BONE, 56, t, 1.2, vel)
            s.add(TIMP, 38, t, 0.8, vel + 4)

    A = ['Dm', 'Eb', 'Dm', 'Eb', 'Gm', 'Edim', 'Fm', 'A']
    # A — такты 1–8, «След»: сердце, басы крадутся, дрожь, шёпот; мотив автора
    heart(bar(1), 8, 82)
    stalk(bar(1), A, 60)
    shiver(bar(1), 8, 40, 80)
    whisper(bar(1), 8, 40)
    s.pads(STR, bar(1), A, 40)
    s.line(HORN, MOTIF, bar(5), 84)
    # B — такты 9–16, «Гонят»: сердце бегом, басы восьмыми, часы; тема Владыки
    # с нотой отказа у тромбонов и обрыв — хозяина гонят; валторна отвечает
    B = ['Dm', 'Gm', 'Eb', 'Dm', 'Gm', 'Dm', 'Eb', 'A']
    heart(bar(9), 8, 80, double=True)
    stalk(bar(9), B, 64, eighths=True)
    ticks(bar(9), 8, 60)
    shiver(bar(9), 8, 60, 90)
    s.pads(STR, bar(9), B, 44)
    s.line(BONE, [n for n in lord_theme(e=63) if n[1] < 20], bar(9), 92, shift=-12)
    s.line(HORN, MOTIF, bar(13), 90)
    s.roll(38, bar(16), 4, 56, 92)
    # C — такты 17–24, «Загнаны»: подъём до октавы у валторны и струнных,
    # удары тритоном, литавры восьмыми, часы бегут
    C = ['Dm', 'Edim', 'Fm', 'Gm', 'A', 'Bb', 'C#dim', 'Dm']
    s.line(HORN, RISE, bar(17), 100)
    s.line(STR, RISE, bar(17) + 2, 88, shift=-12)
    heart(bar(17), 8, 86, double=True, skip=0)
    stalk(bar(17), C, 70, eighths=True)
    ticks(bar(17), 8, 64, fast=True)
    hits(bar(17), 8, 80)
    whisper(bar(17), 8, 60)
    shiver(bar(17), 8, 80, 100)
    cymbal(s, bar(17), 60); cymbal(s, bar(21), 70); cymbal(s, bar(24), 78)
    # D — такты 25–32, «Затаились»: сердце медленно и замирает, колокол
    # рояля — ре, потом ля-бемоль; валторна держит ре, потом ми-бемоль отказа
    heart(bar(25), 8, 64, skip=2)
    stalk(bar(29), ['Eb', 'Eb', 'C#dim', 'A'], 52)
    whisper(bar(25), 8, 52)
    shiver(bar(25), 8, 70, 36)
    s.add(PIANO, 26, bar(25), 8, 80); s.add(PIANO, 38, bar(25), 8, 74)
    s.add(PIANO, 32, bar(29), 8, 80); s.add(PIANO, 44, bar(29), 8, 74)
    s.add(HORN, 50, bar(25), 16, 66); s.add(HORN, 51, bar(29), 8, 70); s.add(HORN, 49, bar(31), 8, 72)
    # E — такты 33–40, «Снова след»: мотив у валторны, канон струнных,
    # сердце бегом — и в начало
    heart(bar(33), 8, 80, double=True)
    stalk(bar(33), A, 66, eighths=True)
    ticks(bar(33), 8, 62)
    shiver(bar(33), 8, 50, 86)
    whisper(bar(33), 8, 50)
    s.pads(STR, bar(33), A, 42)
    s.line(HORN, MOTIF, bar(33), 88)
    s.line(HORN, MOTIF, bar(37), 94)
    s.line(STR, MOTIF, bar(37) + 2, 82, shift=12)
    s.roll(38, bar(40), 4, 60, 88)
    return s, 112, bar(41)


THEMES = {'rat': rat, 'raid': raid, 'hunt': hunt, 'gate': gate, 'war': war, 'final': final,
          'hunted': hunted}

if __name__ == '__main__':
    name, out = sys.argv[1], sys.argv[2]
    s, bpm, length = THEMES[name]()
    tempo = loop_tempo(bpm, length)                 # мкс на долю; круг кратен блоку синтезатора
    if '--loop' in sys.argv:
        s.repeat(length)
    s.save(out, 60e6 / tempo)
    sec = length * tempo / 1e6
    print(f'{out}: {name}, нот {s.count()}, темп {60e6 / tempo:.2f}, круг {length:.0f} долей = {sec:.6f} с'
          + (f'; петля — с {sec:.6f} по {2 * sec:.6f} с' if '--loop' in sys.argv else ''))
