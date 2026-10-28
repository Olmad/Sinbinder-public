# Tools/audio/balalaika.py
# Балалайка Косого Ждана (автор, 27.10: «Ждан брынькает без звука»; решение 27.10: «балалайка
# у Ждана, мимо нот — да»). Своя мелодия на русский лад (ля минор, 2/4) — не чужая запись и
# не чужой напев: щипок струны по Карплусу — Стронгу, три струны примы (ми, ми, ля), бряцание
# тремоло на долгих нотах, дека — два резонанса. Мимо нот — три места: ля-диез вместо си
# с поправкой, до-диез вместо ре, и «ля» струны подтянута на четверть тона выше нужного.
#
# Шум щипка — из счётчика с одним и тем же зерном: один и тот же файл при каждой сборке.
# Запуск: python Tools/audio/balalaika.py → Assets/Resources/Sounds/Balalaika.wav
import math
import struct
import wave
from pathlib import Path

SR = 44100
ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets" / "Resources" / "Sounds" / "Balalaika.wav"

EIGHTH = 0.30          # 100 ударов в минуту, восьмая — 0,3 с
TREMOLO = 1.0 / 12.0   # бряцание: двенадцать ударов в секунду


def hz(midi, cents=0.0):
    return 440.0 * 2.0 ** ((midi - 69 + cents / 100.0) / 12.0)


class Noise:
    def __init__(self, seed):
        self.x = seed

    def next(self):
        self.x = (1103515245 * self.x + 12345) & 0x7FFFFFFF
        return self.x / 0x7FFFFFFF * 2.0 - 1.0


class String:
    """Струна: линия задержки с усреднением; новый щипок перезапускает её, как палец."""

    def __init__(self, seed, rho=0.9965):
        self.noise = Noise(seed)
        self.buf = [0.0, 0.0]
        self.i = 0
        self.rho = rho

    def pluck(self, freq, amp, bright=0.55):
        n = max(2, int(round(SR / freq)))
        raw, prev = [], 0.0
        for _ in range(n):
            prev += bright * (self.noise.next() - prev)
            raw.append(prev * amp)
        # Щипок у подставки: гребёнка на пятой части струны — звон без бубнения.
        p = max(1, n // 5)
        old = self.buf
        self.buf = [raw[k] - 0.45 * raw[(k - p) % n] + 0.15 * old[k % len(old)] for k in range(n)]
        self.i = 0

    def tick(self):
        b, i = self.buf, self.i
        j = i + 1 if i + 1 < len(b) else 0
        out = b[i]
        b[i] = self.rho * 0.5 * (b[i] + b[j])
        self.i = j
        return out


class Biquad:
    """Полоса пропускания (по RBJ) — резонанс деки."""

    def __init__(self, f0, q):
        w = 2.0 * math.pi * f0 / SR
        alpha = math.sin(w) / (2.0 * q)
        a0 = 1.0 + alpha
        self.b0, self.b1, self.b2 = alpha / a0, 0.0, -alpha / a0
        self.a1, self.a2 = -2.0 * math.cos(w) / a0, (1.0 - alpha) / a0
        self.x1 = self.x2 = self.y1 = self.y2 = 0.0

    def run(self, x):
        y = self.b0 * x + self.b1 * self.x1 + self.b2 * self.x2 - self.a1 * self.y1 - self.a2 * self.y2
        self.x2, self.x1, self.y2, self.y1 = self.x1, x, self.y1, y
        return y


# ---------------------------------------------------------------- напев
# (нота MIDI, длительность в восьмых, бряцать ли); None — пауза.
A4, B4, C5, D5, E5, G4, AS4, CS5 = 69, 71, 72, 74, 76, 67, 70, 73

PHRASE = [
    (A4, 2, True), (C5, 1, False), (B4, 1, False),
    (A4, 1, False), (G4, 1, False), (A4, 2, True),
    (C5, 2, True), (D5, 1, False), (E5, 1, False),
    (D5, 1, False), (C5, 1, False), (B4, 2, True),
    (E5, 1, False), (E5, 1, False), (D5, 1, False), (C5, 1, False),
    (B4, 1, False), (C5, 1, False), (D5, 2, True),
    (C5, 1, False), (B4, 1, False), (A4, 1, False), (G4, 1, False),
    (A4, 4, True),
]


def tune():
    """Дважды; на повторе Ждан мажет: ля-диез с поправкой на си и до-диез вместо ре."""
    notes = list(PHRASE)
    again = list(PHRASE)
    again[11] = ("slip", 2, True)          # си: сперва ля-диез, палец съезжает на место
    again[18] = (CS5, 2, True)             # ре — мимо, до-диез
    return notes + again + [("end", 3, False)]


def events():
    """Удары по струнам: (время, нота мелодии, сила, вниз ли). Сдвиги — по номеру ноты, без жребия."""
    out, t = [], 0.40
    for k, (note, length, trem) in enumerate(tune()):
        dur = length * EIGHTH
        late = (0.018 if k % 7 == 3 else 0.0) - (0.010 if k % 5 == 1 else 0.0)   # Ждан торопится и тянет
        if note == "end":
            out.append((t, A4, 1.0, True))
            out.append((t + 0.22, A4, 1.1, True))
            t += dur
            continue
        if note == "slip":
            out.append((t + late, AS4, 0.9, True))
            note, start = B4, t + 0.14
        else:
            start = t + late
        if trem:
            n, down = 0, True
            while start + n * TREMOLO < t + dur - 0.02:
                out.append((start + n * TREMOLO, note, 0.95 if down else 0.70, down))
                n, down = n + 1, not down
        else:
            out.append((start, note, 1.0, True))
        t += dur
    return out, t + 1.6


def render():
    evs, length = events()
    total = int(length * SR)
    melody = String(7)
    drone = [String(11), String(23)]
    deck_low, deck_high = Biquad(290.0, 1.4), Biquad(1450.0, 2.2)

    # Порядок струн в ударе: вниз — ми, ми, ля; вверх — наоборот; струны — с разносом в 7 мс.
    plucks = []
    for at, note, amp, down in evs:
        order = [0, 1, 2] if down else [2, 1, 0]
        for slot, s in enumerate(order):
            when = at + slot * 0.007
            if s == 2:
                plucks.append((when, "m", hz(note, cents=24.0), amp))           # «ля» подтянута: мимо
            else:
                plucks.append((when, s, hz(64, cents=0.0 if s == 0 else 4.0), amp * 0.55))
    plucks.sort(key=lambda p: p[0])

    out = [0.0] * total
    nxt = 0
    for i in range(total):
        t = i / SR
        while nxt < len(plucks) and plucks[nxt][0] <= t:
            _, who, f, amp = plucks[nxt]
            (melody if who == "m" else drone[who]).pluck(f, amp, bright=0.62 if who == "m" else 0.48)
            nxt += 1
        x = melody.tick() + drone[0].tick() + drone[1].tick()
        out[i] = x + 0.55 * deck_low.run(x) + 0.22 * deck_high.run(x)

    peak = max(1e-6, max(abs(v) for v in out))
    gain = 0.70 / peak
    fade = int(0.02 * SR)
    with wave.open(str(OUT), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        frames = bytearray()
        for i, v in enumerate(out):
            k = min(1.0, i / fade, (total - 1 - i) / fade)
            frames += struct.pack("<h", int(max(-1.0, min(1.0, v * gain * k)) * 32767))
        w.writeframes(bytes(frames))
    print("[БАЛАЛАЙКА] {:.1f} с, ударов {}, записано: {}".format(length, len(evs), OUT))


if __name__ == "__main__":
    OUT.parent.mkdir(parents=True, exist_ok=True)
    render()
