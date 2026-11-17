"""Gemeinsame Synthese-Bausteine fuer Hoerprobe 1.

Nur Python-Standardbibliothek. Alle Signale sind Listen von float im Bereich
etwa -1..1, Abtastrate 44 100 Hz. Die fuenf Erzeuger-Skripte importieren
dieses Modul; es erzeugt selbst keine Dateien.
"""
import math
import random
import struct
import wave

SR = 44100


def n_of(seconds):
    return int(round(seconds * SR))


def zeros(n):
    return [0.0] * n


def white(n, rng):
    u = rng.uniform
    return [u(-1.0, 1.0) for _ in range(n)]


def smooth_noise(n, rate_hz, rng):
    """Langsam schwankendes Rauschen (zweimal tiefpassgefiltert), Spitze 1."""
    x = lp1(lp1(white(n, rng), rate_hz), rate_hz)
    return normalize(x, 1.0)


def normalize(x, peak):
    m = max((abs(v) for v in x), default=0.0)
    if m == 0.0:
        return list(x)
    s = peak / m
    return [v * s for v in x]


def db(lin):
    return -200.0 if lin <= 0 else 20.0 * math.log10(lin)


def lin(db_value):
    return 10.0 ** (db_value / 20.0)


# --- Filter -----------------------------------------------------------------

def lp1(x, f):
    """Einpoliger Tiefpass."""
    a = 1.0 - math.exp(-2.0 * math.pi * f / SR)
    y = 0.0
    out = [0.0] * len(x)
    for i, v in enumerate(x):
        y += a * (v - y)
        out[i] = y
    return out


def hp1(x, f):
    """Einpoliger Hochpass."""
    low = lp1(x, f)
    return [a - b for a, b in zip(x, low)]


def svf(x, freq, q=0.707, mode="lp"):
    """Zustandsvariablen-Filter (TPT). freq: Zahl oder Liste je Sample.

    mode: 'lp' Tiefpass, 'bp' Bandpass (Spitze 1), 'hp' Hochpass.
    """
    n = len(x)
    out = [0.0] * n
    k = 1.0 / q
    ic1 = ic2 = 0.0
    sweep = isinstance(freq, list)
    nyq = 0.49 * SR

    def coeffs(f):
        g = math.tan(math.pi * min(max(f, 5.0), nyq) / SR)
        a1 = 1.0 / (1.0 + g * (g + k))
        a2 = g * a1
        return a1, a2, g * a2

    if not sweep:
        a1, a2, a3 = coeffs(freq)
    m = 0 if mode == "lp" else 1 if mode == "bp" else 2
    for i in range(n):
        if sweep and (i & 15) == 0:
            a1, a2, a3 = coeffs(freq[i])
        v0 = x[i]
        v3 = v0 - ic2
        v1 = a1 * ic1 + a2 * v3
        v2 = ic2 + a2 * ic1 + a3 * v3
        ic1 = 2.0 * v1 - ic1
        ic2 = 2.0 * v2 - ic2
        if m == 0:
            out[i] = v2
        elif m == 1:
            out[i] = k * v1
        else:
            out[i] = v0 - k * v1 - v2
    return out


# --- Huellkurven und Oszillatoren ------------------------------------------

def env_ad(n, attack, tau, delay=0.0):
    """Linearer Anstieg in `attack` s, dann exponentieller Abfall mit Zeitkonstante `tau` s."""
    out = [0.0] * n
    d = n_of(delay)
    a = max(1, n_of(attack))
    for i in range(d, n):
        t = i - d
        if t < a:
            out[i] = t / a
        else:
            out[i] = math.exp(-(t - a) / (tau * SR))
    return out


def env_points(n, points):
    """Stueckweise lineare Huellkurve aus (Zeit in s, Wert)-Paaren."""
    out = [0.0] * n
    pts = [(n_of(t), v) for t, v in points]
    for j in range(len(pts) - 1):
        i0, v0 = pts[j]
        i1, v1 = pts[j + 1]
        span = max(1, i1 - i0)
        for i in range(max(0, i0), min(n, i1)):
            out[i] = v0 + (v1 - v0) * (i - i0) / span
    last_i, last_v = pts[-1]
    for i in range(max(0, last_i), n):
        out[i] = last_v
    return out


def curve(n, points):
    """Wie env_points, fuer Frequenzverlaeufe."""
    return env_points(n, points)


def osc_sine(freqs):
    """Sinus mit Frequenz je Sample (Liste) oder fest (Zahl, dann Laenge per Liste)."""
    out = [0.0] * len(freqs)
    ph = 0.0
    for i, f in enumerate(freqs):
        out[i] = math.sin(2.0 * math.pi * ph)
        ph += f / SR
        if ph >= 1.0:
            ph -= 1.0
    return out


def sine(n, f, phase=0.0):
    w = 2.0 * math.pi * f / SR
    return [math.sin(w * i + phase) for i in range(n)]


# --- Mischen ----------------------------------------------------------------

def mul(a, b):
    return [x * y for x, y in zip(a, b)]


def scale(x, g):
    return [v * g for v in x]


def add_into(dst, src, offset=0, gain=1.0):
    """Addiert src ab Sample `offset` in dst (abgeschnitten am Ende von dst)."""
    end = min(len(dst), offset + len(src))
    for i in range(max(0, offset), end):
        dst[i] += src[i - offset] * gain
    return dst


def add_wrapped(dst, src, offset, gain=1.0):
    """Addiert src kreisfoermig: was ueber das Ende ragt, landet am Anfang (fuer Schleifen)."""
    n = len(dst)
    for i, v in enumerate(src):
        dst[(offset + i) % n] += v * gain
    return dst


def pad(x, n):
    return (list(x) + [0.0] * n)[:n]


# --- Raum -------------------------------------------------------------------

def reflections(x, taps, tail=0.05, damp_hz=4000.0):
    """Einzelne fruehe Reflexionen: taps = [(Verzoegerung in ms, Pegel), ...]."""
    n = len(x) + n_of(tail)
    out = zeros(n)
    for ms, g in taps:
        add_into(out, x, n_of(ms / 1000.0), g)
    return lp1(out, damp_hz)


def reverb(x, rt60, size=1.0, damp_hz=5000.0, tail=None, seed=0):
    """Einfacher Schroeder-Hall (4 Kammfilter mit Daempfung, 2 Allpaesse). Gibt nur den Hallanteil zurueck."""
    if tail is None:
        tail = rt60
    n = len(x) + n_of(tail)
    src = pad(x, n)
    rng = random.Random(seed)
    comb_ms = [29.7, 37.1, 41.1, 43.7]
    damp = 1.0 - math.exp(-2.0 * math.pi * damp_hz / SR)
    wet = zeros(n)
    for ms in comb_ms:
        d = max(1, n_of(ms * size * (1.0 + rng.uniform(-0.02, 0.02)) / 1000.0))
        g = 10.0 ** (-3.0 * (d / SR) / rt60)
        buf = [0.0] * d
        idx = 0
        f = 0.0
        for i in range(n):
            y = buf[idx]
            f += damp * (y - f)
            buf[idx] = src[i] + g * f
            wet[i] += y * 0.25
            idx += 1
            if idx == d:
                idx = 0
    for ms, g in ((5.0, 0.7), (1.7, 0.7)):
        d = max(1, n_of(ms * size / 1000.0))
        buf = [0.0] * d
        idx = 0
        out = [0.0] * n
        for i in range(n):
            b = buf[idx]
            v = wet[i] + g * b
            out[i] = b - g * v
            buf[idx] = v
            idx += 1
            if idx == d:
                idx = 0
        wet = out
    return wet


# --- Abschluss und Datei ----------------------------------------------------

def fade_edges(x, fade_in_ms, fade_out_ms):
    """Halbe Kosinus-Blenden; erstes und letztes Sample werden exakt 0."""
    n = len(x)
    out = list(x)
    fi = max(1, int(SR * fade_in_ms / 1000.0))
    fo = max(1, int(SR * fade_out_ms / 1000.0))
    for i in range(min(fi, n)):
        out[i] *= 0.5 - 0.5 * math.cos(math.pi * i / fi)
    for j in range(min(fo, n)):
        i = n - 1 - j
        out[i] *= 0.5 - 0.5 * math.cos(math.pi * j / fo)
    if n:
        out[0] = 0.0
        out[-1] = 0.0
    return out


def finish(x, peak_db, length=None, fade_in_ms=1.0, fade_out_ms=20.0):
    """Gleichanteil entfernen, auf Laenge bringen, Raender blenden, Spitzenpegel setzen."""
    if length is not None:
        x = pad(x, n_of(length))
    x = hp1(x, 20.0)
    x = fade_edges(x, fade_in_ms, fade_out_ms)
    return normalize(x, lin(peak_db))


def write_wav(path, channels):
    """Schreibt 16-Bit-PCM, 44,1 kHz. channels: Liste von Kanal-Listen gleicher Laenge."""
    n = len(channels[0])
    frames = bytearray()
    pack = struct.Struct("<h").pack
    for i in range(n):
        for ch in channels:
            v = int(round(ch[i] * 32767.0))
            v = 32767 if v > 32767 else -32768 if v < -32768 else v
            frames += pack(v)
    with wave.open(str(path), "wb") as w:
        w.setnchannels(len(channels))
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(bytes(frames))
