# Общее для тем автора: ноты → MIDI-файл.
# Время — в четвертях. События пишутся по абсолютному времени
# и сортируются, а шаг между ними считается уже потом. Иначе (как
# в прежнем писателе страницы) длина ноты становится паузой перед
# следующей: ноты звучат по очереди, и трёхминутная тема длится сорок.
import mido

TPB = 480


class Score:
    def __init__(self):
        self.events = {}   # канал -> [(начало, длина, нота, сила)]

    def add(self, ch, pitch, start, dur, vel):
        self.events.setdefault(ch, []).append((start, dur, pitch, max(1, min(127, int(vel)))))

    def end(self):
        return max(s + d for lst in self.events.values() for s, d, p, v in lst)

    def count(self):
        return sum(len(l) for l in self.events.values())

    def write(self, path, bpm, program, volume, pan, reverb):
        mid = mido.MidiFile(ticks_per_beat=TPB)
        meta = mido.MidiTrack(); mid.tracks.append(meta)
        meta.append(mido.MetaMessage('set_tempo', tempo=mido.bpm2tempo(bpm), time=0))
        meta.append(mido.MetaMessage('time_signature', numerator=4, denominator=4, time=0))
        for ch, lst in sorted(self.events.items()):
            tr = mido.MidiTrack(); mid.tracks.append(tr)
            tr.append(mido.Message('program_change', channel=ch, program=program[ch], time=0))
            tr.append(mido.Message('control_change', channel=ch, control=7, value=volume[ch], time=0))
            tr.append(mido.Message('control_change', channel=ch, control=10, value=pan[ch], time=0))
            tr.append(mido.Message('control_change', channel=ch, control=91, value=reverb[ch], time=0))
            ev = []
            for s, d, p, v in restrike(lst):
                ev.append((int(round(s * TPB)), 1, p, v))                 # включение
                ev.append((int(round((s + d) * TPB)) - 1, 0, p, 0))       # выключение — на тик раньше
            ev.sort(key=lambda e: (e[0], e[1]))
            now = 0
            for t, on, p, v in ev:
                tr.append(mido.Message('note_on' if on else 'note_off', channel=ch, note=p, velocity=v, time=t - now))
                now = t
        mid.save(path)


def restrike(lst):
    """Та же нота на том же канале, пока прежняя звучит, — как у живого
    музыканта: прежняя кончается, новая берётся заново и звучит всю свою
    длину. Иначе выключение прежней обрывает и новую (синтезатор гасит
    все голоса этой клавиши разом). Две одинаковые ноты в один миг — одна,
    длиной и силой большей из двух: так их и слышно на странице."""
    out = []
    by_pitch = {}
    for n in sorted(lst):
        by_pitch.setdefault(n[2], []).append(list(n))
    for notes in by_pitch.values():
        kept = []
        for n in notes:
            if kept and n[0] == kept[-1][0]:
                kept[-1][1] = max(kept[-1][1], n[1]); kept[-1][3] = max(kept[-1][3], n[3])
                continue
            if kept and n[0] < kept[-1][0] + kept[-1][1]:
                kept[-1][1] = n[0] - kept[-1][0]
            kept.append(n)
        out.extend(tuple(n) for n in kept)
    return sorted(out)
