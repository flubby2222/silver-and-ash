"""Hoerprobe 1 – Klingentreffer (Silberklinge trifft einen Untoten).

Erzeugt drei Varianten als mono WAV, 16 Bit, 44,1 kHz, jeweils unter 0,6 s:
  klinge-a-dumpf.wav     nur der Aufprall: tiefer Schlag, Koerper-Ton, feuchtes Quetschen
  klinge-b-schnitt.wav   heller, abwaerts gleitender Schnitt (70 ms), dann ein leiserer dumpfer Aufprall
  klinge-c-klirren.wav   dumpfer Aufprall plus nachklingende Silberklinge (unharmonische Teiltoene)

Aufruf: python klinge.py   (schreibt in den Ordner dieses Skripts)
"""
import math
import random
from pathlib import Path

from synth import (SR, add_into, curve, env_ad, env_points, finish, mul, n_of,
                   osc_sine, smooth_noise, svf, white, write_wav, zeros)

OUT = Path(__file__).resolve().parent
PEAK_DB = -4.0


def thud(rng, n, gain_wet=0.6):
    """Dumpfer Treffer: tiefpassgefiltertes Rauschen, fallender Koerperton, feuchtes Quetschen."""
    hit = mul(svf(white(n, rng), 420.0, 0.8, "lp"), env_ad(n, 0.001, 0.03))
    body = mul(osc_sine(curve(n, [(0.0, 95.0), (0.1, 58.0)])), env_ad(n, 0.002, 0.05))
    # Quetschen: Bandrauschen um 700 Hz, unregelmaessig moduliert
    am = [0.5 + 0.5 * v for v in smooth_noise(n, 35.0, rng)]
    squish = mul(mul(svf(white(n, rng), 700.0, 2.5, "bp"), am), env_ad(n, 0.008, 0.06))
    out = zeros(n)
    add_into(out, hit, 0, 1.0)
    add_into(out, body, 0, 0.7)
    add_into(out, squish, 0, gain_wet)
    return out


def variant_a():
    rng = random.Random(11)
    n = n_of(0.30)
    return finish(thud(rng, n), PEAK_DB, length=0.30, fade_out_ms=50.0)


def variant_b():
    rng = random.Random(22)
    total = n_of(0.38)
    out = zeros(total)
    sl = n_of(0.07)
    sweep = curve(sl, [(0.0, 7000.0), (0.07, 2500.0)])
    slice_ = mul(svf(white(sl, rng), sweep, 3.0, "bp"),
                 env_points(sl, [(0.0, 0.0), (0.05, 1.0), (0.07, 0.6)]))
    add_into(out, slice_, 0, 0.55)
    n = total - sl
    add_into(out, thud(rng, n, gain_wet=0.8), sl, 1.0)
    return finish(out, PEAK_DB, length=0.38, fade_in_ms=3.0, fade_out_ms=50.0)


def variant_c():
    rng = random.Random(33)
    n = n_of(0.55)
    out = thud(rng, n, gain_wet=0.4)
    click = mul(svf(white(n, rng), 3000.0, 0.7, "hp"), env_ad(n, 0.0002, 0.003))
    add_into(out, click, 0, 0.5)
    ring = zeros(n)
    for f, tau, g in ((1870.0, 0.25, 0.5), (1876.0, 0.25, 0.4), (3020.0, 0.18, 0.35),
                      (4410.0, 0.14, 0.3), (5960.0, 0.09, 0.2), (7550.0, 0.06, 0.12)):
        w = 2.0 * math.pi * f / SR
        env = env_ad(n, 0.001, tau)
        for i in range(n):
            ring[i] += math.sin(w * i) * env[i] * g
    add_into(out, ring, 0, 0.18)
    return finish(out, PEAK_DB, length=0.55, fade_out_ms=80.0)


def main():
    for name, fn in (("klinge-a-dumpf", variant_a),
                     ("klinge-b-schnitt", variant_b),
                     ("klinge-c-klirren", variant_c)):
        write_wav(OUT / f"{name}.wav", [fn()])
        print(name)


if __name__ == "__main__":
    main()
