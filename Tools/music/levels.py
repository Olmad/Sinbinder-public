# Громкость каждого голоса по отдельности — вместо уха при сведении.
#   python3 Tools/music/levels.py тема.mid [банк] [--parts 0:12,12:40,...]
# Каждый канал отрисовывается один (тем же FluidSynth, что и целое),
# печатается его громкость — целиком и по частям (секунды). Так видно,
# кто кого забивает: мелодия должна быть громче подкладки, хор — заметно
# тише мелодии. Громкость взвешена по слуху (кривая A): низ ухо слышит
# слабее, и без взвешивания виолончель с контрабасом «громче всех».
import os, subprocess, sys, tempfile
import mido
import numpy as np
import soundfile as sf

HERE = os.path.dirname(os.path.abspath(__file__))


def render(mid, wav, bank):
    cmd = ['sh', os.path.join(HERE, 'render.sh'), mid, wav] + ([bank] if bank else [])
    subprocess.run(cmd, check=True)


def solo(src, ch, dst):
    m = mido.MidiFile(src)
    out = mido.MidiFile(ticks_per_beat=m.ticks_per_beat)
    out.tracks.append(m.tracks[0])
    for tr in m.tracks[1:]:
        chans = {x.channel for x in tr if hasattr(x, 'channel')}
        if ch in chans: out.tracks.append(tr)
    out.save(dst)


def db(x, sr=44100):
    """Громкость по слуху, дБ(A)."""
    if len(x) == 0: return -99.0
    S = np.abs(np.fft.rfft(x)) ** 2
    f = np.fft.rfftfreq(len(x), 1 / sr)
    f2 = f ** 2
    ra = (12194 ** 2 * f2 ** 2) / ((f2 + 20.6 ** 2) * np.sqrt((f2 + 107.7 ** 2) * (f2 + 737.9 ** 2)) * (f2 + 12194 ** 2))
    p = (S * (ra * 10 ** (2 / 20)) ** 2).sum() / len(x) ** 2 * 2
    return 10 * np.log10(p + 1e-12)


if __name__ == '__main__':
    src = sys.argv[1]
    bank = next((a for a in sys.argv[2:] if a.endswith(('.sf2', '.sf3'))), None)
    parts = []
    if '--parts' in sys.argv:
        for p in sys.argv[sys.argv.index('--parts') + 1].split(','):
            a, b = p.split(':'); parts.append((float(a), float(b)))
    m = mido.MidiFile(src)
    chans = sorted({x.channel for tr in m.tracks for x in tr if hasattr(x, 'channel')})
    names = {}
    for tr in m.tracks:
        for x in tr:
            if x.type == 'program_change': names[x.channel] = x.program
    with tempfile.TemporaryDirectory() as tmp:
        render(src, f'{tmp}/all.wav', bank)
        full, sr = sf.read(f'{tmp}/all.wav', always_2d=True); full = full.mean(axis=1)
        head = 'голос (программа)'.ljust(22) + 'целиком'.rjust(9) + ''.join(f'{int(a)}–{int(b)} с'.rjust(11) for a, b in parts)
        print(head)
        rows = [('всё вместе', full)]
        for ch in chans:
            solo(src, ch, f'{tmp}/s.mid'); render(f'{tmp}/s.mid', f'{tmp}/s.wav', bank)
            x, _ = sf.read(f'{tmp}/s.wav', always_2d=True); rows.append((f'канал {ch} ({names.get(ch, "?")})', x.mean(axis=1)))
        for name, x in rows:
            cells = [db(x, sr)] + [db(x[int(a * sr):int(b * sr)], sr) for a, b in parts]
            print(name.ljust(22) + ''.join(f'{c:9.1f}' if i == 0 else f'{c:11.1f}' for i, c in enumerate(cells)))
