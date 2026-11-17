"""Hoerprobe 5 – Bausteine der Ableitungskette (Konzept Klangbibliothek, Abschnitt 3).

  1 einlesen          quelle.load (ffmpeg -> mono, 44,1 kHz)
  3 auf eine Geste    cut_gesture
  4 Rauschen/Brummen  clean (Hochpass 40 Hz, optional ffmpeg afftdn)
  5 Tonhoehe/Laenge   varispeed (ffmpeg asetrate + aresample: Tonhoehe und Laenge zusammen)
  6 schichten         layer (auf den ersten Einsatz ausgerichtet)
  8 Lautheit          finish aus synth.py (Spitze je Kategorie)
  9 PS1               to_ps1 (Hoehenausgleich + ps1_klang, Einstellung je Kategorie)
"""
import subprocess
import sys
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent

import ps1_klang as P  # noqa: E402
from synth import SR, add_into, svf, write_wav  # noqa: E402
import quelle as Q  # noqa: E402

# PS1-Einstellungen zur Wahl (Hoerprobe 5 fragt je Kategorie nach a oder b)
EINSTELLUNG = {
    "a-leicht": {"rate": 22050, "ausgleich": 1.0, "hall": None, "hall_pegel": 0.0},
    "b-stark": {"rate": 11025, "ausgleich": 0.0, "hall": "Room", "hall_pegel": 0.2},
}


def ff(x, af):
    """Schickt ein Signal durch einen ffmpeg-Filter (z. B. 'afftdn=nr=12')."""
    with tempfile.TemporaryDirectory() as td:
        a, b = Path(td) / "a.wav", Path(td) / "b.wav"
        write_wav(a, [x])
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(a), "-af", af, "-ac", "1", "-ar", str(SR),
                        "-c:a", "pcm_s16le", str(b)], check=True)
        return Q.load(b)


def first_onset(x, below=30.0):
    """Erstes Sample, ab dem der Pegel (5-ms-Fenster) hoechstens `below` dB unter der Spitze liegt."""
    w = int(0.005 * SR)
    peak = max(abs(v) for v in x) or 1e-9
    for s in range(0, len(x) - w, w // 2):
        if Q.rms(x[s:s + w]) > peak * 10 ** (-below / 20):
            return s
    return 0


def cut_gesture(x, max_len, pre_ms=5.0, floor_db=60.0):
    """Schneidet ab 5 ms vor dem ersten Einsatz bis zum dauerhaften Abfall um floor_db (hoechstens max_len s)."""
    s0 = max(0, first_onset(x) - int(pre_ms / 1000 * SR))
    y = x[s0:]
    d = Q.decay(y, floor_db)
    end = len(y) if d is None else min(len(y), int((d + 0.02) * SR) + first_onset(y))
    return y[:min(end, int(max_len * SR))]


def clean(x, denoise=False):
    y = svf(svf(x, 40.0, 0.707, "hp"), 40.0, 0.707, "hp")
    return ff(y, "afftdn=nr=12:nf=-50") if denoise else y


def varispeed(x, semitones):
    """Tonhoehe und Laenge zusammen aendern (wie langsamer abspielen)."""
    if semitones == 0:
        return list(x)
    rate = int(round(SR * 2 ** (semitones / 12)))
    return ff(x, f"asetrate={rate},aresample={SR}")


def head(x, ms, fade_ms=8.0):
    """Nur der Anschlag: die ersten `ms` Millisekunden mit weicher Ausblende."""
    import math
    n = int(ms / 1000 * SR)
    f = int(fade_ms / 1000 * SR)
    y = list(x[:n])
    for j in range(min(f, len(y))):
        y[len(y) - 1 - j] *= 0.5 - 0.5 * math.cos(math.pi * j / f)
    return y


def layer(base, extra, gain, offset_ms=0.0):
    out = list(base) + [0.0] * max(0, len(extra) - len(base))
    return add_into(out, extra, int(offset_ms / 1000 * SR), gain)


def to_ps1(x, einstellung="a-leicht", length=None):
    """Hoehenausgleich (Hoerprobe 4) und PS1-Kette mit der gewaehlten Einstellung. Float rein, float raus."""
    e = EINSTELLUNG[einstellung]
    y = list(x)
    if e["ausgleich"]:
        hp = svf(y, 3000.0, 0.707, "hp")
        y = [a + e["ausgleich"] * b for a, b in zip(y, hp)]
        peak, pk = max(abs(v) for v in x), max(abs(v) for v in y)
        y = [v * peak / pk for v in y]
    n = length or len(y)
    ints = [int(round(v * 32767)) for v in y] + [0] * max(0, n - len(y))
    out = P.process([ints], rate=e["rate"], hall=e["hall"], hall_pegel=e["hall_pegel"], length=n)[0]
    return [v / 32767.0 for v in out]
