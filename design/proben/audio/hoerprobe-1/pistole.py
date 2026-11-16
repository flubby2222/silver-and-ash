"""Hoerprobe 1 – Pistolenschuss (Silberkugel im Steingewoelbe).

Erzeugt drei Varianten als mono WAV, 16 Bit, 44,1 kHz, jeweils unter 0,6 s:
  pistole-a-trocken.wav     Knall + Druck + tiefer Stoss, nur zwei fruehe Reflexionen
  pistole-b-gewoelbe.wav    derselbe Kern, dazu dunkler Hall des Gewoelbes (RT60 0,6 s)
  pistole-c-metallisch.wav  Hahn-Klick vorweg, kuerzerer Knall, metallisches Klingen, heller kurzer Raum

Aufruf: python pistole.py   (schreibt in den Ordner dieses Skripts)
"""
import math
import random
from pathlib import Path

from synth import (SR, add_into, env_ad, finish, hp1, lp1, mul, n_of, osc_sine,
                   curve, reflections, reverb, scale, svf, white, write_wav, zeros)

OUT = Path(__file__).resolve().parent
PEAK_DB = -1.5


def core(rng, n, crack_tau=0.006, body_tau=0.025, thump_gain=0.8):
    """Kern des Schusses: heller Knall, Druckwelle, tiefer Stoss."""
    crack = mul(svf(white(n, rng), 900.0, 0.7, "hp"), env_ad(n, 0.0003, crack_tau))
    body = mul(svf(white(n, rng), 1600.0, 0.7, "lp"), env_ad(n, 0.0008, body_tau))
    thump = mul(osc_sine(curve(n, [(0.0, 110.0), (0.08, 45.0)])), env_ad(n, 0.002, 0.04))
    out = zeros(n)
    add_into(out, crack, 0, 1.0)
    add_into(out, body, 0, 0.9)
    add_into(out, thump, 0, thump_gain)
    return out


def variant_a():
    rng = random.Random(101)
    n = n_of(0.30)
    dry = core(rng, n)
    er = reflections(dry, [(7.0, 0.32), (13.0, 0.2)], tail=0.0, damp_hz=3000.0)
    out = add_into(list(dry), er, 0, 1.0)
    return finish(out, PEAK_DB, length=0.30, fade_out_ms=40.0)


def variant_b():
    rng = random.Random(202)
    n = n_of(0.12)
    dry = core(rng, n, body_tau=0.03, thump_gain=1.0)
    wet = reverb(dry, rt60=0.6, size=1.4, damp_hz=1800.0, tail=0.46, seed=2)
    wet = lp1(wet, 2500.0)
    out = zeros(n_of(0.58))
    add_into(out, dry, 0, 1.0)
    add_into(out, wet, 0, 2.2)
    return finish(out, PEAK_DB, length=0.58, fade_out_ms=80.0)


def variant_c():
    rng = random.Random(303)
    total = n_of(0.45)
    out = zeros(total)
    # Hahn-Klick 18 ms vor dem Schuss
    click_n = n_of(0.012)
    click = mul(svf(white(click_n, rng), 3200.0, 4.0, "bp"), env_ad(click_n, 0.0002, 0.0015))
    add_into(out, click, 0, 0.35)
    shot_at = n_of(0.018)
    n = total - shot_at
    dry = core(rng, n, crack_tau=0.004, body_tau=0.015, thump_gain=0.45)
    # metallisches Klingen: unharmonische Teiltoene, leicht verstimmte Paare fuer Schwebung
    ring = zeros(n)
    for f, tau, g in ((2350.0, 0.11, 0.5), (2363.0, 0.11, 0.35), (3710.0, 0.08, 0.4),
                      (5180.0, 0.06, 0.3), (6890.0, 0.04, 0.2)):
        w = 2.0 * math.pi * f / SR
        env = env_ad(n, 0.0005, tau)
        for i in range(n):
            ring[i] += math.sin(w * i) * env[i] * g
    add_into(dry, ring, 0, 0.22)
    room = reverb(dry, rt60=0.35, size=0.8, damp_hz=7000.0, tail=0.0, seed=3)
    add_into(out, dry, shot_at, 1.0)
    add_into(out, room, shot_at, 1.0)
    return finish(out, PEAK_DB, length=0.45, fade_out_ms=60.0)


def main():
    for name, fn in (("pistole-a-trocken", variant_a),
                     ("pistole-b-gewoelbe", variant_b),
                     ("pistole-c-metallisch", variant_c)):
        write_wav(OUT / f"{name}.wav", [fn()])
        print(name)


if __name__ == "__main__":
    main()
