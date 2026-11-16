"""Hoerprobe 1 – Schritt auf Stein.

Drei Varianten mit je vier leicht verschiedenen Schritten (mono WAV, 16 Bit,
44,1 kHz, jeder Schritt unter 0,3 s):
  schritt-a-weich-1..4.wav    weiche Sohle: kurzer, tiefpassgefilterter Aufsatz, kein Raum
  schritt-b-stiefel-1..4.wav  Stiefel: harte Ferse, 35-55 ms spaeter die Spitze, dazu Sandknirschen
  schritt-c-hall-1..4.wav     heller Aufsatz mit kurzem Raumhall (RT60 0,35 s)

Zusaetzlich je Variante eine Hoerhilfe schritt-<v>-folge.wav: die vier Schritte
zweimal im Gehtempo (0,5 s Abstand). Die Folge ist kein Spiel-Asset, sie zeigt
nur, wie sich Wiederholung anhoert; fuer sie gilt die 0,3-s-Grenze nicht.

Aufruf: python schritt.py   (schreibt in den Ordner dieses Skripts)
"""
import random
from pathlib import Path

from synth import (add_into, curve, env_ad, finish, lp1, mul, n_of, osc_sine,
                   reverb, svf, white, write_wav, zeros)

OUT = Path(__file__).resolve().parent
PEAK_DB = -12.0
TAKES = 4


def impact(rng, n, cutoff, tau, thud_f, thud_gain):
    x = mul(svf(white(n, rng), cutoff, 0.9, "lp"), env_ad(n, 0.0008, tau))
    t = mul(osc_sine(curve(n, [(0.0, thud_f), (0.05, thud_f * 0.7)])), env_ad(n, 0.002, 0.018))
    return add_into(x, t, 0, thud_gain)


def grit(rng, n, start, length, density, gain):
    """Sandknirschen: verstreute kurze Impulse, hochpassgefiltert."""
    imp = zeros(n)
    s0, s1 = n_of(start), n_of(start + length)
    for i in range(s0, min(n, s1)):
        if rng.random() < density:
            imp[i] = rng.uniform(-1.0, 1.0) * (1.0 - (i - s0) / max(1, s1 - s0))
    return svf(imp, 2500.0, 0.7, "hp"), gain


def variant_a(take):
    rng = random.Random(500 + take)
    j = rng.uniform
    length = 0.14
    n = n_of(length)
    x = impact(rng, n, 1100.0 * j(0.85, 1.15), 0.012 * j(0.8, 1.2), 85.0 * j(0.9, 1.1), 0.5)
    return finish(x, PEAK_DB + j(-1.5, 0.0), length=length, fade_out_ms=30.0)


def variant_b(take):
    rng = random.Random(600 + take)
    j = rng.uniform
    length = 0.22
    n = n_of(length)
    x = impact(rng, n, 2800.0 * j(0.85, 1.15), 0.008 * j(0.8, 1.2), 75.0 * j(0.9, 1.1), 0.9)
    toe_at = j(0.035, 0.055)
    toe_n = n - n_of(toe_at)
    toe = impact(rng, toe_n, 1800.0 * j(0.85, 1.15), 0.010, 110.0, 0.3)
    add_into(x, toe, n_of(toe_at), 0.45 * j(0.8, 1.1))
    g, gain = grit(rng, n, 0.0, toe_at + 0.04, 0.01, 0.35)
    add_into(x, g, 0, gain)
    return finish(x, PEAK_DB + j(-1.5, 0.0), length=length, fade_out_ms=40.0)


def variant_c(take):
    rng = random.Random(700 + take)
    j = rng.uniform
    length = 0.28
    dry_n = n_of(0.06)
    dry = impact(rng, dry_n, 3500.0 * j(0.85, 1.15), 0.007 * j(0.8, 1.2), 90.0 * j(0.9, 1.1), 0.4)
    wet = reverb(dry, rt60=0.35, size=0.7, damp_hz=6000.0, tail=length, seed=take)
    x = zeros(n_of(length))
    add_into(x, dry, 0, 1.0)
    add_into(x, lp1(wet, 5000.0), 0, 1.4)
    return finish(x, PEAK_DB + j(-1.5, 0.0), length=length, fade_out_ms=60.0)


def main():
    for key, label, fn in (("a", "weich", variant_a), ("b", "stiefel", variant_b), ("c", "hall", variant_c)):
        takes = []
        for t in range(1, TAKES + 1):
            x = fn(t)
            takes.append(x)
            write_wav(OUT / f"schritt-{key}-{label}-{t}.wav", [x])
        seq = zeros(n_of(0.5 * 2 * TAKES + 0.3))
        for k in range(2 * TAKES):
            add_into(seq, takes[k % TAKES], n_of(0.1 + 0.5 * k), 1.0)
        write_wav(OUT / f"schritt-{key}-{label}-folge.wav", [seq])
        print(f"schritt-{key}-{label}")


if __name__ == "__main__":
    main()
