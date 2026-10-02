# «A Lord Returns — ровно 3 минуты»: та же партитура, но инструментами,
# а не волнами. Пишет MIDI: рояль, виолончель (и контрабас октавой ниже —
# как страница подкладывала пол-частоты), хор «а», литавры. Темп 50.
#   python3 Tools/music/a_lord_returns.py выход.mid [--octave]
# --octave — вторая версия: мелодия рояля удвоена октавой выше, чуть тише
# основной (тема лежит низко, ре–ля первой октавы). Ноты страницы не тронуты.
import sys
from score import Score

PIANO, CELLO, BASS, CHOIR, TIMP, HIGH = 0, 1, 2, 3, 4, 5
PROGRAM = {PIANO: 0, CELLO: 42, BASS: 43, CHOIR: 52, TIMP: 47, HIGH: 0}
REVERB = {PIANO: 55, CELLO: 70, BASS: 60, CHOIR: 85, TIMP: 60, HIGH: 75}
PAN = {PIANO: 58, CELLO: 44, BASS: 54, CHOIR: 78, TIMP: 64, HIGH: 70}
VOLUME = {PIANO: 104, CELLO: 92, BASS: 64, CHOIR: 96, TIMP: 96, HIGH: 100}

octave = '--octave' in sys.argv
s = Score()
add = s.add


def piano(p, st, d, v=0.4, chord=False):
    add(PIANO, p, st, d, (28 + v * 95) if chord else (45 + v * 115))
    if octave and not chord:
        add(HIGH, p + 12, st, d, 45 + v * 115)
def cello(p, st, d, v=0.35):
    add(CELLO, p, st, d, 45 + v * 110); add(BASS, p - 12, st, d, 22 + v * 70)
def choir(p, st, d, v=0.2): add(CHOIR, p, st, d, 45 + v * 160)
def timpani(p, st, d, v=0.5): add(TIMP, p, st, min(d, 4), 55 + v * 75)

def notes(lst, fn, shift=0.0, vol=None):
    for n in lst:
        v = vol if vol is not None else (n[3] if len(n) > 3 else 0.4)
        chord = isinstance(n[0], list)
        ps = n[0] if chord else [n[0]]
        for p in ps:
            if fn is piano: fn(p, n[1] + shift, n[2], v, chord)
            else: fn(p, n[1] + shift, n[2], v)

notes([[62,0,2,.25],[69,2,2,.25],[67,4,2,.25],[65,6,2,.3],[64,8,4,.3],[62,12,4,.35]], piano)
notes([[[50,53,57],0,8,.2],[[46,50,53],8,4,.2]], piano)
t1 = 12
theme = [[62,0,2],[69,2,2],[67,4,2],[65,6,2],[64,8,4],[62,12,4],[58,16,2],[61,18,2],[62,20,8]]
chords = [[[50,53,57],0,8],[[46,50,53],8,8],[[43,46,50],16,4],[[45,49,52],20,4]]
celloL = [[38,0,12],[34,12,8],[33,20,8]]
choirL = [[50,4,4,.12],[50,12,4,.15],[57,20,8,.2]]
notes(theme, piano, t1, .4); notes(chords, piano, t1, .35); notes(celloL, cello, t1, .35); notes(choirL, choir, t1)
t2 = 40
notes([[65,0,2],[72,2,2],[70,4,2],[67,6,2],[65,8,4],[69,12,4],[62,16,2],[64,18,2],[65,20,8]], piano, t2, .45)
notes([[[53,57,60],0,8],[[48,52,55],8,8],[[45,48,52],16,4],[[47,51,54],20,4]], piano, t2, .4)
notes([[41,0,12],[36,12,8],[35,20,8]], cello, t2, .4)
notes([[53,4,4,.15],[53,12,4,.2],[60,20,8,.28]], choir, t2)
notes([[38,12,4],[38,20,8]], timpani, t2, .55)
t3 = 68
notes(theme, piano, t3, .5); notes(chords, piano, t3, .45); notes(celloL, cello, t3, .45)
notes([[n[0], n[1], n[2], n[3]*1.3] for n in choirL], choir, t3)
notes([[38,12,4,.7],[38,20,8,.8]], timpani, t3 - t1)
t4 = 96
notes([[[50,53,57],0,12],[[46,50,53],12,12],[[43,46,50],24,12],[[50,53,57],36,18]], piano, t4, .35)
notes([[38,0,24],[34,24,12],[38,36,18]], cello, t4, .3)
notes([[38,36,12,.5]], timpani, t4)

s.write(sys.argv[1], 50, PROGRAM, VOLUME, PAN, REVERB)
print(f'{sys.argv[1]}: нот {s.count()}, конец на {s.end()} долях = {s.end() * 1.2:.1f} с'
      + (', мелодия удвоена октавой выше' if octave else ''))
