"""Hoerprobe 4 – Klingentreffer, nicht mehr stumpf. Drei Fassungen, alle durch die PS1-Kette.

Befund aus Hoerprobe 3: Der Treffer hatte fast nur Tiefe – im Band 2-8 kHz lag er
etwa 18 dB unter dem Gesamtpegel, ueber 8 kHz etwa 36 dB darunter; die PS1-Kette
nahm ueber 8 kHz noch einmal 10-13 dB weg. Es fehlte ein heller, metallischer Anteil.

Jede Fassung bleibt EINE Geste (alle Schichten setzen gleichzeitig ein und verschmelzen):
  klinge-a-schneide   dumpfer Koerper wie Hoerprobe 3 + heller Schnitt-Anschlag (Hochpass-Rauschen
                      ueber 3,5 kHz, 10 ms) + sehr kurzer Metallklang (drei unharmonische Teiltoene
                      um 3,1/4,7/6,4 kHz, Zeitkonstante 35 ms)
  klinge-b-biss       eine Bewegung: Rauschen durch einen Tiefpass, der bei 9 kHz beginnt und in 60 ms
                      auf 250 Hz faellt (hell -> dumpf), dazu der Koerperton und ein kurzer Metallklang (25 ms)
  klinge-c-klirr      dumpfer Koerper + deutlicherer, etwas laengerer Metallklang (2,6/4,1/5,9/7,3 kHz,
                      55 ms) mit Schwebung, wenig Rauschen – am metallischsten

Ausgleich vor der PS1-Kette: Die Kette daempft die Hoehen (gemessen in Hoerprobe 3:
-1,7 dB bei 4 kHz, -6,9 dB bei 8 kHz). Vor der Kette werden deshalb die Hoehen ueber
etwa 3 kHz angehoben (Anteil eines Hochpasses zugemischt, etwa +6 dB bei 8 kHz).
messen.py vergleicht: sauber, PS1 ohne Ausgleich, PS1 mit Ausgleich.
Gespeichert werden nur die PS1-Fassungen mit Ausgleich (je drei Treffer).

Aufruf: python klinge.py
"""
import math
import random
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
HP1 = HERE.parent / "hoerprobe-1"
HP3 = HERE.parent / "hoerprobe-3"
sys.path.insert(0, str(HP1))
sys.path.insert(0, str(HP3))

import ps1_klang as P  # noqa: E402
from synth import (SR, add_into, curve, env_ad, finish, mul, n_of, normalize, osc_sine,  # noqa: E402
                   svf, white, write_wav, zeros)

LEN = 0.26


def body(rng, n, j):
    """Koerper wie Hoerprobe 3: fallender Tiefpass + Ton 110 -> 55 Hz."""
    fc0 = 1800.0 * j(0.85, 1.15)
    cut = [180.0 + (fc0 - 180.0) * math.exp(-i / (0.015 * SR)) for i in range(n)]
    b = mul(svf(white(n, rng), cut, 2.0, "lp"), env_ad(n, 0.001, 0.03))
    p = j(0.92, 1.08)
    t = mul(osc_sine(curve(n, [(0.0, 110.0 * p), (0.08, 55.0 * p)])), env_ad(n, 0.002, 0.045))
    return add_into(normalize(b, 1.0), t, 0, 0.6)


def metal(n, partials, tau, j):
    out = [0.0] * n
    for f, g in partials:
        f *= j(0.97, 1.03)
        w = 2.0 * math.pi * f / SR
        for i in range(n):
            out[i] += g * math.sin(w * i) * math.exp(-i / (tau * SR)) * min(1.0, i / (0.0005 * SR))
    return normalize(out, 1.0)


def variant_a(take):
    rng = random.Random(16100 + take)
    j = rng.uniform
    n = n_of(LEN)
    x = body(rng, n, j)
    edge = mul(svf(white(n, rng), 3500.0, 0.7, "hp"), env_ad(n, 0.0003, 0.010))
    add_into(x, normalize(edge, 1.0), 0, 0.55)
    add_into(x, metal(n, [(3100.0, 1.0), (4700.0, 0.7), (6400.0, 0.5)], 0.035, j), 0, 0.30)
    return finish(x, -4.0 + j(-1.5, 0.0), length=LEN, fade_out_ms=40.0)


def variant_b(take):
    rng = random.Random(16200 + take)
    j = rng.uniform
    n = n_of(LEN)
    f0 = 9000.0 * j(0.9, 1.1)
    cut = [250.0 + (f0 - 250.0) * math.exp(-i / (0.012 * SR)) for i in range(n)]
    sweep = normalize(mul(svf(white(n, rng), cut, 1.5, "lp"), env_ad(n, 0.0005, 0.035)), 1.0)
    p = j(0.92, 1.08)
    t = mul(osc_sine(curve(n, [(0.0, 110.0 * p), (0.08, 55.0 * p)])), env_ad(n, 0.002, 0.045))
    x = add_into(sweep, t, 0, 0.6)
    add_into(x, metal(n, [(3300.0, 1.0), (5100.0, 0.6)], 0.025, j), 0, 0.22)
    return finish(x, -4.0 + j(-1.5, 0.0), length=LEN, fade_out_ms=40.0)


def variant_c(take):
    rng = random.Random(16300 + take)
    j = rng.uniform
    n = n_of(LEN)
    x = body(rng, n, j)
    m = metal(n, [(2600.0, 1.0), (2614.0, 0.8), (4100.0, 0.7), (5900.0, 0.5), (7300.0, 0.35)], 0.055, j)
    add_into(x, m, 0, 0.45)
    edge = mul(svf(white(n, rng), 3500.0, 0.7, "hp"), env_ad(n, 0.0003, 0.004))
    add_into(x, normalize(edge, 1.0), 0, 0.25)
    return finish(x, -4.0 + j(-1.5, 0.0), length=LEN, fade_out_ms=50.0)


def pre_emphasis(x, amount=1.0):
    """Hebt die Hoehen an: Anteil eines Hochpasses (3 kHz, 2. Ordnung) zugemischt.
    amount = 1.0 entspricht etwa +6 dB weit oberhalb von 3 kHz."""
    hp = svf(x, 3000.0, 0.707, "hp")
    return [a + amount * b for a, b in zip(x, hp)]


def ps1(x, compensate=True):
    y = pre_emphasis(x) if compensate else x
    peak = max(abs(v) for v in x)
    y = [v * peak / max(abs(w) for w in y) for v in y]  # Ausgleich darf den Spitzenpegel nicht erhoehen
    out = P.process([[int(round(v * 32767)) for v in y]], rate=22050)[0]
    return [v / 32767.0 for v in out]


VARIANTS = {"a-schneide": variant_a, "b-biss": variant_b, "c-klirr": variant_c}


def main():
    for key, fn in VARIANTS.items():
        for take in range(1, 4):
            write_wav(HERE / f"klinge-{key}-{take}-ps1.wav", [ps1(fn(take))])
        print(key)


if __name__ == "__main__":
    main()
