# Оркестр для своих версий тем: каналы, рассадка, аккорды и приёмы
# (шаг виолончелей, корни контрабаса, медь, литавры, арфа). Тема пишется
# нотами в своём файле, а как звучит оркестр — здесь, один раз на все.
from score import Score

PIANO, HORN, STR, PAD, CELLO, BASS, BONE, CHOIR, TIMP, ORGAN, HARP, OOHS, CEL, HARPSI, PICC = \
    0, 1, 2, 3, 4, 5, 6, 7, 8, 10, 11, 12, 13, 14, 15      # девятый — ударные по стандарту, не занимать
PROGRAM = {PIANO: 0, HORN: 60, STR: 48, PAD: 49, CELLO: 42, BASS: 43, BONE: 57, CHOIR: 52,
           TIMP: 47, ORGAN: 19, HARP: 46, OOHS: 53, CEL: 8, HARPSI: 6, PICC: 72}
# рассадка оркестра: скрипки слева, виолончели и басы справа, медь в глубине
PAN = {PIANO: 64, HORN: 54, STR: 40, PAD: 50, CELLO: 82, BASS: 90, BONE: 80, CHOIR: 64,
       TIMP: 64, ORGAN: 64, HARP: 34, OOHS: 70, CEL: 46, HARPSI: 30, PICC: 76}
REVERB = {PIANO: 60, HORN: 80, STR: 75, PAD: 85, CELLO: 70, BASS: 60, BONE: 80, CHOIR: 95,
          TIMP: 70, ORGAN: 90, HARP: 80, OOHS: 95, CEL: 85, HARPSI: 55, PICC: 75}

# Аккорды: корень у виолончели, у контрабаса октавой ниже, три голоса
# середины, медь (корень и квинта), литавры (настроены на ре и ля).
CHORD = {
    'Dm': dict(cello=38, bass=26, mid=[50, 53, 57], bone=[50, 57], timp=38),
    'Bb': dict(cello=46, bass=34, mid=[50, 53, 58], bone=[46, 53], timp=None),
    'Gm': dict(cello=43, bass=31, mid=[50, 55, 58], bone=[43, 50], timp=38),
    'A':  dict(cello=45, bass=33, mid=[49, 52, 57], bone=[45, 52], timp=45),
    'Eb': dict(cello=39, bass=27, mid=[51, 55, 58], bone=[51, 58], timp=None),
    'D':  dict(cello=38, bass=26, mid=[50, 54, 57], bone=[50, 57], timp=38),
    'G':  dict(cello=43, bass=31, mid=[50, 55, 59], bone=[43, 50], timp=38),
    'Bm': dict(cello=47, bass=35, mid=[50, 54, 59], bone=[47, 54], timp=None),
    'Em': dict(cello=40, bass=28, mid=[52, 55, 59], bone=[40, 47], timp=None),
}


def bar(n):
    """Начало такта n (с единицы), в долях; такт — четыре доли."""
    return (n - 1) * 4


def lord_theme(e=64):
    """Тема автора из «A Lord Returns»: (нота, начало, длина) в долях;
    e — ми или ми-бемоль (нота отказа)."""
    return [(62, 0, 2), (69, 2, 2), (67, 4, 2), (65, 6, 2), (e, 8, 4), (62, 12, 4),
            (58, 16, 2), (61, 18, 2), (62, 20, 8)]


class Orchestra(Score):
    def __init__(self, volume):
        super().__init__()
        self.volume = volume
        for ch in PROGRAM:
            self.expression(ch, 0, 100)

    def save(self, path, bpm):
        self.write(path, bpm, PROGRAM, self.volume, PAN, REVERB)

    def line(self, ch, notes, at, vel, shift=0, hold=0, last=None):
        """Мелодия списком (нота, начало, длина); hold продлевает ноту,
        начинающуюся в last (по умолчанию — последнюю)."""
        last = notes[-1][1] if last is None else last
        for p, st, d in notes:
            self.add(ch, p + shift, at + st, d + (hold if st == last else 0), vel)

    def ostinato(self, at, chords, vel, acc):
        """Шаг: виолончели восьмыми — корень, корень, квинта, корень."""
        for i, name in enumerate(chords):
            r = CHORD[name]['cello']
            for k, off in enumerate([0, 0, 7, 0, 0, 0, 7, 0]):
                self.add(CELLO, r + off, at + i * 4 + k * 0.5, 0.42, acc if k in (0, 4) else vel)

    def roots(self, at, chords, vel):
        for i, name in enumerate(chords):
            self.add(BASS, CHORD[name]['bass'], at + i * 4, 4, vel)

    def pads(self, ch, at, chords, vel):
        for i, name in enumerate(chords):
            for p in CHORD[name]['mid']:
                self.add(ch, p, at + i * 4, 4, vel)

    def brass(self, at, chords, vel):
        for i, name in enumerate(chords):
            for half in (0, 2):
                for p in CHORD[name]['bone']:
                    self.add(BONE, p, at + i * 4 + half, 1.9, vel + (8 if half == 0 else 0))

    def steps(self, at, chords, vel, both=False):
        for i, name in enumerate(chords):
            t = CHORD[name]['timp']
            if t is None: continue
            self.add(TIMP, t, at + i * 4, 1.5, vel)
            if both: self.add(TIMP, t, at + i * 4 + 2, 1.5, vel - 12)

    def roll(self, note, at, beats, v0, v1):
        """Тремоло литавр шестнадцатыми с нарастанием."""
        n = int(beats * 4)
        for i in range(n):
            self.add(TIMP, note, at + i * 0.25, 0.25, v0 + (v1 - v0) * i / max(1, n - 1))

    def tolls(self, at, chords, vel):
        """Рояль низко, октавой, раз в два такта — как колокол."""
        for i in (0, 2, 4, 6):
            b = CHORD[chords[i]]['bass']
            self.add(PIANO, b, at + i * 4, 4, vel); self.add(PIANO, b + 12, at + i * 4, 4, vel)

    def harp(self, at, chords, vel, accent):
        """Арфа: вверх-вниз по аккорду восьмыми."""
        for i, name in enumerate(chords):
            c = CHORD[name]; seq = [c['cello'], *c['mid'], c['mid'][1], c['mid'][0], c['mid'][1], c['mid'][2]]
            for k, p in enumerate(seq):
                self.add(HARP, p, at + i * 4 + k * 0.5, 1.2, accent if k == 0 else vel)
