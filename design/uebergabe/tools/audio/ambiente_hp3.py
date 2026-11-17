"""Hoerprobe 3 – Katakomben-Ambiente: tiefe Tropfen nach Farnells Rezept, sauber und durch die PS1-Kette.

Grundflaeche: unveraendert die aus Hoerprobe 1 (Grollen + Luft, gleiche Zufallszahlen).
Tropfen: tiefste Stufe aus Hoerprobe 2 (anderthalb Oktaven tiefer, Grundton etwa
1,1-1,9 kHz), an denselben sieben Stellen. Neu nach Farnell (Designing Sound,
Practical 12 "Bubbles", "drips in cave"):
  - Tonhoehe steigt exponentiell statt mit Wurzelkurve,
  - weicher Einsatz (2 ms) und laengeres Ausklingen (Zeitkonstante 20 ms statt 6 ms),
  - statt zweier fester Echos ein kleiner Hoehlenhall: vier Verzoegerungen im
    Verhaeltnis des goldenen Schnitts (61,8 / 100 / 161,8 / 261,8 ms) mit
    Summen-/Differenz-Mischung und Tiefpass in der Rueckfuehrung.

Dateien:
  ambiente-sauber.wav / .ogg
  ambiente-ps1.wav / .ogg     durch ps1_klang (22 050 Hz, ohne Hall), kreisfoermig
                              verarbeitet, damit die Naht erhalten bleibt
Die Fassung OHNE Tropfen fuer die Messung nach Regel 6 erzeugt messen.py ueber
flaeche() in diesem Skript; sie wird nicht gespeichert.

Aufruf: python ambiente.py
"""
import importlib.util
import math
import random
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
HP1 = HERE
sys.path.insert(0, str(HP1))

import ps1_klang as P  # noqa: E402
from synth import SR, add_into, add_wrapped, lin, n_of, scale, write_wav, zeros  # noqa: E402

spec = importlib.util.spec_from_file_location("ambiente_hp1", HERE / "ambiente_hp1.py")
hp1 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(hp1)

FACTOR = 2.0 ** -1.5
LENGTH = hp1.LENGTH


def cave(x, tail=1.2, feedback=0.45, damp_hz=2500.0):
    """Kleiner Hoehlenhall nach Farnell: 4 Verzoegerungen (goldener Schnitt), Summen-/Differenz-Matrix."""
    n = len(x) + n_of(tail)
    src = x + [0.0] * (n - len(x))
    delays = [n_of(ms / 1000.0) for ms in (61.8, 100.0, 161.8, 261.8)]
    bufs = [[0.0] * d for d in delays]
    idx = [0, 0, 0, 0]
    lp = [0.0] * 4
    a = 1.0 - math.exp(-2.0 * math.pi * damp_hz / SR)
    out = [0.0] * n
    for i in range(n):
        o = [bufs[k][idx[k]] for k in range(4)]
        for k in range(4):
            lp[k] += a * (o[k] - lp[k])
        s0, s1, s2, s3 = lp
        # Summen-/Differenz-Mischung (Hadamard), haelt die Energie
        m = [0.5 * (s0 + s1 + s2 + s3), 0.5 * (s0 - s1 + s2 - s3),
             0.5 * (s0 + s1 - s2 - s3), 0.5 * (s0 - s1 - s2 + s3)]
        for k in range(4):
            bufs[k][idx[k]] = src[i] + feedback * m[k]
            idx[k] = (idx[k] + 1) % delays[k]
        out[i] = 0.25 * (o[0] + o[1] + o[2] + o[3])
    return out


def drip(rng):
    """Ein Tropfen: Sinus mit exponentiell steigender Tonhoehe, weicher Einsatz, ~80 ms Ausklang, Hoehlenhall.
    Verbraucht genau zwei Zufallszahlen wie in Hoerprobe 1 und 2, damit die Zeitpunkte gleich bleiben."""
    f_a = rng.uniform(3000.0, 4200.0) * FACTOR
    f_b = f_a * rng.uniform(1.15, 1.3)
    ln = n_of(0.12)
    ph = 0.0
    out = [0.0] * ln
    for i in range(ln):
        t = i / ln
        f = f_a * (f_b / f_a) ** (1.0 - math.exp(-5.0 * t))
        ph += f / SR
        out[i] = math.sin(2.0 * math.pi * ph) * math.exp(-i / (0.02 * SR)) * min(1.0, i / (0.002 * SR))
    full = add_into(cave(out), out, 0, 1.0)
    return full


def silent_drip(rng):
    rng.uniform(0.0, 1.0)
    rng.uniform(0.0, 1.0)
    return [0.0]


def build(with_drips=True):
    """Wie Variante b aus Hoerprobe 1; Tropfen nach neuem Rezept. Gibt float-Kanaele zurueck."""
    hp1.drip = drip if with_drips else silent_drip
    return hp1.variant("b")


def flaeche():
    """Die Grundflaeche allein (fuer Regel 6), gleicher Ablauf, Tropfen stumm."""
    return build(with_drips=False)


def ps1_loop(chans, pad_s=1.0):
    """PS1-Kette fuer eine Schleife: vorne das Ende, hinten den Anfang anhaengen, verarbeiten, Mitte ausschneiden.
    So beginnt die Verarbeitung an der Naht nicht bei Stille. Keine Randblenden (es ist eine Schleife)."""
    n = len(chans[0])
    p = n_of(pad_s)
    ints = [[int(round(v * 32767)) for v in c] for c in chans]
    padded = [c[-p:] + c + c[:p] for c in ints]
    low = P.resample(padded, SR, 22050)
    out = []
    for c in low:
        played = P.play(P.adpcm_roundtrip(c), 22050)
        out.append([v / 32767.0 for v in played[p:p + n]])
    peak_in = max(max(abs(v) for v in c) for c in chans)
    peak_out = max(max(abs(v) for v in c) for c in out)
    return [scale(c, peak_in / peak_out) for c in out]


def save(name, chans):
    wav = HERE / f"{name}.wav"
    write_wav(wav, chans)
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(wav), "-c:a", "libvorbis", "-q:a", "5",
                    str(HERE / f"{name}.ogg")], check=True)


def main():
    left, right = build()
    save("ambiente-sauber", [left, right])
    save("ambiente-ps1", ps1_loop([left, right]))
    print("fertig")


if __name__ == "__main__":
    main()
