# Последний шаг после FluidSynth: мягкий эквалайзер, громкость, OGG — и замер.
#   python3 Tools/music/master.py сырой.wav выход.ogg [--cut с:по]
# --cut — вырезать кусок (секунды) после обработки. Для петли без шва:
# тема отрисована дважды подряд, берётся второй проход — хвост зала от его
# конца уже лежит в его начале, и стык не слышен.
# Эквалайзер: низ ниже 160 Гц тише на 3 дБ (виолончель с контрабасом
# и зал копят гул), 1–4 кГц громче на 2 дБ (там различимость челесты,
# рояля и согласных хора). Потом ограничитель: удар молоточка рояля или
# литавры выше остального на 10–15 дБ, и по пику вся тема выходит тихой.
# Пики придавлены не больше чем на 6 дБ, плавно (5 мс вперёд, отпуск
# 150 мс) — без хрипа. Пик — 0,89: запас, чтобы OGG не хрипел.
# Замер печатается: длина, пик, тихие секунды и самая длинная тишина,
# доля энергии по полосам — я не слышу, я меряю.
import sys
import numpy as np
import soundfile as sf
from scipy.ndimage import maximum_filter1d
from scipy.signal import butter, sosfiltfilt

PEAK = 0.89
SQUEEZE_DB = 6.0   # насколько ограничитель вправе придавить пик
QUIET_DB = -45.0   # секунда тише этого — «тишина» на слух в игре


def master(x, sr):
    low = sosfiltfilt(butter(2, 160, 'low', fs=sr, output='sos'), x, axis=0)
    mid = sosfiltfilt(butter(2, [1000, 4000], 'band', fs=sr, output='sos'), x, axis=0)
    y = x - low * (1 - 10 ** (-3 / 20)) + mid * (10 ** (2 / 20) - 1)
    y = limit(y / np.abs(y).max(), sr)
    return y * (PEAK / np.abs(y).max())


def limit(y, sr):
    """Пики выше порога — тише, остальное как есть. Огибающая берётся
    с заглядыванием вперёд, глушит сразу, отпускает медленно: усиление
    не дрожит. Считается по миллисекундам, потом плавно на каждый отсчёт."""
    thr = 10 ** (-SQUEEZE_DB / 20)
    env = maximum_filter1d(np.abs(y).max(axis=1), size=int(0.010 * sr))   # ±5 мс
    hop = sr // 1000
    blocks = env[:len(env) // hop * hop].reshape(-1, hop).max(axis=1)
    need = np.maximum(0.0, 20 * np.log10(np.maximum(blocks, 1e-9) / thr))   # дБ, сколько придавить
    fall = 20 * np.log10(np.e) / 150.0     # отпуск: ~150 мс на 8,7 дБ
    held = np.empty_like(need); cur = 0.0
    for i, n in enumerate(need):
        cur = max(n, cur - fall)
        held[i] = cur
    gain_db = np.interp(np.arange(len(y)), np.arange(len(held)) * hop + hop / 2, held)
    return y * (10 ** (-gain_db / 20))[:, None]


def measure(y, sr):
    mono = y.mean(axis=1)
    secs = len(mono) // sr
    db = np.array([20 * np.log10(np.sqrt((mono[i * sr:(i + 1) * sr] ** 2).mean()) + 1e-9) for i in range(secs)])
    quiet = db < QUIET_DB
    # самая длинная тишина — до хвоста зала в конце (он тихий по природе)
    last = secs - 1
    while last > 0 and quiet[last]:
        last -= 1
    run, best, at = 0, 0, 0
    for i in range(last + 1):
        run = run + 1 if quiet[i] else 0
        if run > best: best, at = run, i - run + 1
    S = np.abs(np.fft.rfft(mono)) ** 2
    f = np.fft.rfftfreq(len(mono), 1 / sr)
    bands = [(20, 150), (150, 500), (500, 2000), (2000, 6000)]
    e = [S[(f >= a) & (f < b)].sum() for a, b in bands]
    tot = sum(e)
    return {
        'длина': f'{len(mono) / sr:.1f} с',
        'пик': f'{np.abs(y).max():.2f}',
        'тихих секунд до хвоста': f'{int(quiet[:last + 1].sum())} из {last + 1}',
        'самая длинная тишина': f'{best} с' + (f' (с {at // 60}:{at % 60:02d})' if best else ''),
        'полосы 20–150/150–500/500–2к/2–6к Гц': ' / '.join(f'{100 * v / tot:.1f}' for v in e) + ' %',
        'громкость по секундам (медиана)': f'{np.median(db[:last + 1]):.0f} дБ',
    }


if __name__ == '__main__':
    x, sr = sf.read(sys.argv[1], always_2d=True)
    y = master(x, sr)
    if '--cut' in sys.argv:
        a, b = (float(v) for v in sys.argv[sys.argv.index('--cut') + 1].split(':'))
        y = y[int(round(a * sr)):int(round(b * sr))]
    with sf.SoundFile(sys.argv[2], 'w', sr, y.shape[1], format='OGG', subtype='VORBIS') as out:
        for i in range(0, len(y), sr):          # кусками: libsndfile не любит длинный Vorbis разом
            out.write(y[i:i + sr])
    print(sys.argv[2])
    for k, v in measure(y, sr).items():
        print(f'  {k}: {v}')
