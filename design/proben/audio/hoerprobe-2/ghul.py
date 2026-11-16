"""Hoerprobe 2 – Ghul-Laut, deutlich einfacher.

Regel aus der Recherche: ein Laut = eine Geste aus hoechstens zwei Schichten,
die Huellkurve ist die Uhr. Der Schlag liegt bei t = 0,50 s.
  ghul-d-anschwellen.wav  Eine tiefe, raue Stimme auf gleicher Tonhoehe schwillt
                          gleichmaessig an und bricht beim Schlag ab.
                          Schichten: Stimme + leiser Atem durch denselben Rachen.
  ghul-e-fauchen.wav      Ein einziges Fauchen (Rauschen durch den Rachen) schwillt
                          an und wird dabei heller, Abbruch beim Schlag.
                          Schicht: nur das Fauchen.
  ghul-f-aufsteigen.wav   Eine raue Stimme gleitet gleichmaessig von 45 auf 110 Hz
                          und erreicht beim Schlag den hoechsten Ton, dann kurzer Abfall.
                          Schicht: nur die Stimme.

Keine Zufallsbewegungen ausser der Rauheit (jede zweite Periode leiser).
Mono WAV, 16 Bit, 44,1 kHz.
Aufruf: python ghul.py   (schreibt in den Ordner dieses Skripts)
"""
import math
import random

from gemeinsam import HERE
from synth import (SR, add_into, curve, finish, mul, n_of, normalize, svf,
                   white, write_wav, zeros)

PEAK_DB = -3.0
HIT = 0.50
RELEASE = 0.04  # Abbruch nach dem Schlag


def voice_source(f0, subharm=0.45):
    """Gleichmaessiger Stimmimpulszug ohne Zittern; Rauheit nur ueber Subharmonische."""
    n = len(f0)
    out = [0.0] * n
    ph = 0.0
    odd = False
    prev = 0.0
    open_q = 0.55
    amp = 1.0
    for i in range(n):
        ph += f0[i] / SR
        if ph >= 1.0:
            ph -= 1.0
            odd = not odd
            amp = (1.0 - subharm) if odd else 1.0
        p = math.sin(math.pi * ph / open_q) ** 2 if ph < open_q else 0.0
        v = p * amp
        out[i] = v - prev
        prev = v
    return out


def throat(x, f1, f2):
    """Zwei Formanten und ein tiefer Anteil – ein fester Rachen."""
    out = zeros(len(x))
    add_into(out, svf(x, f1, 5.0, "bp"), 0, 1.0)
    add_into(out, svf(x, f2, 6.0, "bp"), 0, 0.5)
    add_into(out, svf(x, 160.0, 1.0, "lp"), 0, 0.6)
    return out


def swell(n, start_db=-30.0):
    """Gleichmaessiges Anschwellen (linear in dB) bis zum Schlag, dann Abbruch."""
    env = [0.0] * n
    hit = n_of(HIT)
    rel = n_of(RELEASE)
    for i in range(n):
        if i < hit:
            env[i] = 10.0 ** (start_db * (1.0 - i / hit) / 20.0)
        elif i < hit + rel:
            env[i] = 0.5 + 0.5 * math.cos(math.pi * (i - hit) / rel)
    return env


def variant_d():
    rng = random.Random(4001)
    total = 0.62
    n = n_of(total)
    src = voice_source([58.0] * n)
    voice = normalize(throat(src, 380.0, 700.0), 1.0)
    breath = normalize(throat(white(n, rng), 380.0, 700.0), 1.0)
    mix = add_into(voice, breath, 0, 0.18)
    return finish(mul(mix, swell(n)), PEAK_DB, length=total, fade_in_ms=10.0, fade_out_ms=20.0)


def variant_e():
    rng = random.Random(5001)
    total = 0.62
    n = n_of(total)
    f1 = curve(n, [(0.0, 450.0), (HIT, 900.0)])
    noise = white(n, rng)
    hiss = zeros(n)
    add_into(hiss, svf(noise, f1, 4.0, "bp"), 0, 1.0)
    add_into(hiss, svf(noise, [v * 2.2 for v in f1], 5.0, "bp"), 0, 0.5)
    hiss = normalize(hiss, 1.0)
    return finish(mul(hiss, swell(n)), PEAK_DB, length=total, fade_in_ms=10.0, fade_out_ms=20.0)


def variant_f():
    total = 0.70
    n = n_of(total)
    f0 = curve(n, [(0.0, 45.0), (HIT, 110.0)])
    src = voice_source(f0)
    voice = normalize(throat(src, 320.0, 620.0), 1.0)
    env = [0.0] * n
    hit = n_of(HIT)
    for i in range(n):
        if i < hit:
            env[i] = 10.0 ** (-12.0 * (1.0 - i / hit) / 20.0)
        else:
            env[i] = math.exp(-(i - hit) / (0.03 * SR))
    return finish(mul(voice, env), PEAK_DB, length=total, fade_in_ms=30.0, fade_out_ms=40.0)


def main():
    for name, fn in (("ghul-d-anschwellen", variant_d),
                     ("ghul-e-fauchen", variant_e),
                     ("ghul-f-aufsteigen", variant_f)):
        write_wav(HERE / f"{name}.wav", [fn()])
        print(name)


if __name__ == "__main__":
    main()
