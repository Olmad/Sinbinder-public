# Опытный образец: dungeon synth по нотам, без жребия.
# ре минор, 60 ударов в минуту, такт — 4 с; i–VI–iv–V (Dm Bb Gm A).
# Тембры: пэд — три расстроенные пилы через мягкий фильтр; бурдон — синус;
# мелодия — «тёмная флейта» с вибрато; восьмибитное арпеджио — тихо, в конце.
# Всё — в зал (ревербератор Шрёдера). Выход: ogg 44,1 кГц стерео.
import numpy as np, soundfile as sf, sys
from scipy.signal import lfilter

SR = 44100
BAR = 4.0
BARS = 17
TAIL = 5.0
N = int((BARS * BAR + TAIL) * SR)
t_all = np.arange(N) / SR

def hz(m): return 440.0 * 2 ** ((m - 69) / 12)

def env(n, a, r, sustain_len):
    """Огибающая: подъём a, держать до sustain_len, спад r (секунды)."""
    e = np.ones(n)
    ai = max(1, int(a * SR)); ri = max(1, int(r * SR)); si = int(sustain_len * SR)
    e[:ai] = np.linspace(0, 1, ai) ** 1.5
    if si + ri <= n:
        e[si:si + ri] *= np.linspace(1, 0, ri) ** 2
        e[si + ri:] = 0
    return e

def onepole_lp(x, cutoff):
    a = np.exp(-2 * np.pi * cutoff / SR)
    return lfilter([1 - a], [1, -a], x)

def saw(f, t, phase=0.0):
    x = (f * t + phase) % 1.0
    return 2 * x - 1

def comb(x, D, g):
    """y[n] = x[n] + g*y[n-D] — поблочно, за линейное время."""
    y = x.copy()
    for k in range(D, len(x), D):
        e = min(len(x), k + D)
        y[k:e] += g * y[k - D:e - D]
    return y

def allpass(x, D, g):
    """y[n] = -g*x[n] + x[n-D] + g*y[n-D] — поблочно."""
    y = -g * x
    y[D:] += x[:-D]
    for k in range(D, len(x), D):
        e = min(len(x), k + D)
        y[k:e] += g * y[k - D:e - D]
    return y

def place(buf, start, sig, gain_l, gain_r):
    i = int(start * SR); j = min(N, i + len(sig))
    buf[0, i:j] += sig[:j - i] * gain_l
    buf[1, i:j] += sig[:j - i] * gain_r

mix = np.zeros((2, N))

# ---- аккорды и бурдон ----
chords = {
    'Dm': [57, 62, 65, 69], 'Bb': [58, 62, 65, 70],
    'Gm': [55, 62, 67, 70], 'A':  [57, 61, 64, 69],
}
roots = {'Dm': 26, 'Bb': 22, 'Gm': 19, 'A': 21}   # на две октавы ниже
cycle = ['Dm', 'Bb', 'Gm', 'A']
prog = [cycle[i % 4] for i in range(16)] + ['Dm']

for b, name in enumerate(prog):
    start = b * BAR
    hold = BAR + (2.5 if b == BARS - 1 else 0.6)
    n = int((hold + 2.0) * SR)
    t = np.arange(n) / SR
    e = env(n, 1.4, 2.0, hold)
    for k, m in enumerate(chords[name]):
        f = hz(m)
        v = sum(saw(f * 2 ** (c / 1200), t, 0.13 * k + 0.31 * j) for j, c in enumerate((-9, 0, 8))) / 3
        # фильтр дышит медленно: так пэд не стоит стеной
        cut = 700 + 250 * np.sin(2 * np.pi * 0.06 * (start + t))
        v = onepole_lp(onepole_lp(v, 2800), 4000)
        v = v - onepole_lp(v, 140)   # снизу убрать гул: низ — у бурдона
        v *= e * 0.40
        pan = 0.35 + 0.1 * k
        place(mix, start, v, 1 - pan * 0.6, 0.4 + pan * 0.6)
    # корень снизу — синус, чуть-чуть
    rf = hz(roots[name] + 12)
    r = np.sin(2 * np.pi * rf * t) * env(n, 1.0, 1.8, hold) * 0.094
    place(mix, start, r, 0.9, 0.9)

# бурдон ре на всю длину
drone = np.sin(2 * np.pi * hz(38) * t_all) * 0.06
drone *= np.clip(t_all / 6, 0, 1) * np.clip((BARS * BAR + 2 - t_all) / 4, 0, 1)
mix += drone

# ---- мелодия ----
# (нота, начало в ударах от такта 5, длина в ударах)
mel = [
    (69,0,2),(65,2,1),(64,3,1),   (62,4,2),(65,6,2),
    (67,8,1),(69,9,1),(70,10,2),  (69,12,2),(67,14,1),(64,15,1),
    (65,16,2),(64,18,1),(62,19,1),(65,20,1),(67,21,1),(69,22,2),
    (70,24,1),(69,25,1),(67,26,1),(65,27,1), (64,28,2),(61,30,1),(64,31,1),
    (74,32,2),(69,34,2),  (70,36,2),(65,38,2),
    (67,40,1),(70,41,1),(74,42,2), (73,44,2),(76,46,1),(69,47,1),
    (62,48,4),
]
for m, s, d in mel:
    start = 4 * BAR + s * 1.0
    dur = d * 1.0
    n = int((dur + 0.6) * SR); t = np.arange(n) / SR
    f = hz(m + 12)   # октавой выше: в жанре ведёт флейта, а не виолончель
    vib = 1 + 0.0035 * np.sin(2 * np.pi * 5.0 * t) * np.clip((t - 0.3) / 0.4, 0, 1)
    ph = 2 * np.pi * f * np.cumsum(vib) / SR
    v = np.sin(ph) + 0.25 * np.sin(2 * ph) + 0.30 * np.sin(3 * ph) + 0.12 * np.sin(4 * ph) + 0.08 * np.sin(5 * ph)
    v = onepole_lp(v, 6000)
    v *= env(n, 0.07, 0.45, dur - 0.05) * 0.175
    place(mix, start, v, 0.8, 1.0)

# ---- восьмибитное арпеджио: такты 13–16, тихо, слева, с эхом ----
arp = np.zeros((2, N))
for b in range(12, 16):
    tones = list(chords[prog[b]][1:]) + [chords[prog[b]][1] + 12]
    for i in range(8):
        m = tones[i % len(tones)]
        start = b * BAR + i * 0.5
        n = int(0.42 * SR); t = np.arange(n) / SR
        v = np.where((hz(m) * t) % 1.0 < 0.25, 1.0, -1.0)
        v = onepole_lp(v, 4500) * env(n, 0.005, 0.12, 0.25) * 0.12
        place(arp, start, v, 1.0, 0.45)
d = int(0.75 * SR)
for ch in range(2):
    arp[ch] = comb(arp[ch], d, 0.35)
mix += arp

# ---- зал: Шрёдер, гребёнки и всепропускающие, правый канал — со сдвигом ----
def reverb(x, offset):
    x = onepole_lp(x, 6000)
    out = np.zeros_like(x)
    for ms in (29.7, 37.1, 41.1, 43.7, 47.3, 53.9):
        D = int((ms + offset) * SR / 1000)
        out += comb(x, D, 0.84)
    out /= 6
    for ms in (5.0, 1.7):
        D = int(ms * SR / 1000); g = 0.7
        out = allpass(out, D, g)
    return out

wet = np.vstack([reverb(mix[0], 0.0), reverb(mix[1], 1.3)])
out = mix * 0.72 + wet * 0.55

# мягкое ограничение, вход и выход
out = np.tanh(out * 1.0) / np.tanh(1.0)
out[:, :int(2 * SR)] *= np.linspace(0, 1, int(2 * SR))
out[:, -int(4 * SR):] *= np.linspace(1, 0, int(4 * SR))
# громкость — как у образцов жанра (rms около 0,09), пик не выше 0,9
out *= min(0.09 / np.sqrt((out ** 2).mean()), 0.9 / np.abs(out).max())

path = sys.argv[1] if len(sys.argv) > 1 else 'dungeon_proto.ogg'
sf.write(path.replace('.ogg', '.wav'), out.T.astype(np.float32), SR, subtype='PCM_16')
with sf.SoundFile(path, 'w', SR, 2, format='OGG', subtype='VORBIS') as f:
    for i in range(0, out.shape[1], SR):
        f.write(out[:, i:i + SR].T)
rms = np.sqrt((out ** 2).mean())
print(f'{path}: {N / SR:.1f} с, пик {np.abs(out).max():.2f}, rms {rms:.3f}')
