"""Hoerprobe 5 – Zugriff auf die Kenney-Originale und Messungen fuer die Vorauswahl.

Die Originale liegen NICHT im Repository. Den Ordner gibt man als Parameter an
(--quellen) oder ueber die Umgebungsvariable SILVERASH_KLANGQUELLEN; Standard ist
%LOCALAPPDATA%\\SilverAndAsh\\klangquellen. Erwartete Struktur:
  <quellen>/kenney/<paket>/inhalt/...   (entpacktes Archiv)

Nur Python-Standardbibliothek und ffmpeg. Aus den Quellordnern wird nichts ausgefuehrt,
nur Audiodateien werden von ffmpeg gelesen.
"""
import hashlib
import math
import os
import struct
import subprocess
import tempfile
import wave
from pathlib import Path

SR = 44100


def default_root():
    env = os.environ.get("SILVERASH_KLANGQUELLEN")
    if env:
        return Path(env)
    return Path(os.environ.get("LOCALAPPDATA", ".")) / "SilverAndAsh" / "klangquellen"


def find(root, paket, name):
    """Sucht eine Datei im entpackten Paket, z. B. find(root, 'impact-sounds', 'footstep_concrete_000.ogg')."""
    base = Path(root) / "kenney" / paket / "inhalt"
    hits = [p for p in base.rglob(name)]
    if len(hits) != 1:
        raise FileNotFoundError(f"{name} in {base}: {len(hits)} Treffer")
    return hits[0]


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 16), b""):
            h.update(chunk)
    return h.hexdigest()


def load(path):
    """Liest eine beliebige Audiodatei ueber ffmpeg als mono float (-1..1), 44,1 kHz."""
    with tempfile.TemporaryDirectory() as td:
        out = Path(td) / "x.wav"
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(path), "-ac", "1", "-ar", str(SR),
                        "-c:a", "pcm_s16le", str(out)], check=True)
        with wave.open(str(out), "rb") as w:
            n = w.getnframes()
            data = struct.unpack("<%dh" % n, w.readframes(n))
    return [v / 32768.0 for v in data]


def info(path):
    """Originalformat laut ffprobe-Ausgabe von ffmpeg (Rate, Kanaele, Laenge)."""
    r = subprocess.run(["ffmpeg", "-hide_banner", "-i", str(path)], capture_output=True, text=True)
    line = next((l.strip() for l in r.stderr.splitlines() if "Audio:" in l), "")
    dur = next((l.strip() for l in r.stderr.splitlines() if "Duration:" in l), "")
    return dur.split(",")[0].replace("Duration: ", ""), line.split("Audio:")[-1].strip()


# --- Messungen ----------------------------------------------------------------

def db(x):
    return -200.0 if x <= 0 else 20.0 * math.log10(x)


def rms(x):
    return math.sqrt(sum(v * v for v in x) / max(1, len(x)))


def envelope(x, win=0.05, hop=0.01):
    w, h = int(win * SR), int(hop * SR)
    return [(s / SR + win / 2, db(rms(x[s:s + w]))) for s in range(0, max(1, len(x) - w), h)]


def onsets(x):
    """Einsaetze nach Regel 2 (50-ms-Fenster, Anstieg > 8 dB gegenueber 50 ms vorher, Beginn zaehlt mit)."""
    env = envelope(x)
    if not env:
        return 0
    peak = max(v for _, v in env)
    count, last_t = 0, -1.0
    for k, (t, v) in enumerate(env):
        if v < peak - 40:
            continue
        prev = env[k - 5][1] if k >= 5 else peak - 60
        if v - max(prev, peak - 60) > 8.0:
            if t - last_t > 0.06:
                count += 1
            last_t = t
    return count


def noise_floor(x):
    """Ruhigster 10-ms-Abschnitt relativ zur Spitze (dB) – Mass fuer Rauschgrund."""
    w = int(0.01 * SR)
    peak = max(abs(v) for v in x) or 1e-9
    vals = [rms(x[s:s + w]) for s in range(0, len(x) - w, w)]
    vals = [v for v in vals if v > 0] or [1e-9]
    return db(min(vals)) - db(peak)


def clipped(x, thresh=0.999):
    run = best = 0
    for v in x:
        run = run + 1 if abs(v) >= thresh else 0
        best = max(best, run)
    return best


def decay(x, th=60):
    """Zeit vom lautesten 5-ms-Fenster, bis der Pegel dauerhaft th dB darunter liegt."""
    w = int(0.005 * SR)
    env = [db(rms(x[s:s + w])) for s in range(0, len(x) - w, w)]
    if not env:
        return None
    k0 = max(range(len(env)), key=lambda k: env[k])
    for k in range(k0, len(env)):
        if all(v < env[k0] - th for v in env[k:]):
            return (k - k0) * 0.005
    return None


def bands(x):
    """Mittlerer Pegel in dB: gesamt, <300 Hz, 300-2000, 2000-8000, >8000 (je zwei Filter 2. Ordnung)."""
    import sys
    sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "hoerprobe-1"))
    from synth import svf

    def p(y):
        return 10 * math.log10(max(1e-20, sum(v * v for v in y) / len(y)))

    def lp(y, f):
        return svf(svf(y, f, 0.707, "lp"), f, 0.707, "lp")

    def hp(y, f):
        return svf(svf(y, f, 0.707, "hp"), f, 0.707, "hp")
    return p(x), p(lp(x, 300)), p(lp(hp(x, 300), 2000)), p(lp(hp(x, 2000), 8000)), p(hp(x, 8000))
