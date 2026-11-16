"""Hoerprobe 1 – Ghul-Laut (Ankuendigung vor dem Schlag).

Der Ghul holt etwa 0,5 s aus. Jede Variante setzt den Moment des Schlags
genau auf t = 0,50 s und macht ihn auf andere Weise hoerbar:
  ghul-a-knurren-steigend.wav  Knurren wird 0,5 s lang lauter und hoeher, bei 0,5 s ein harter Ausstoss
  ghul-b-atem-schrei.wav       feuchtes Einatmen steigt an, reisst 20 ms vor dem Schlag ab, bei 0,5 s Schrei
  ghul-c-gurgeln-zaehlen.wav   drei kurze Gurgel-Stoesse in kuerzer werdenden Abstaenden (0 / 0,25 / 0,40 s), bei 0,5 s der vierte, laute

Mono WAV, 16 Bit, 44,1 kHz, jeweils unter 1,5 s.
Aufruf: python ghul.py   (schreibt in den Ordner dieses Skripts)
"""
import math
import random
from pathlib import Path

from synth import (SR, add_into, curve, env_ad, env_points, finish, lp1, mul,
                   n_of, normalize, smooth_noise, svf, white, write_wav, zeros)

OUT = Path(__file__).resolve().parent
PEAK_DB = -3.0
HIT = 0.50  # Zeitpunkt des Schlags in Sekunden


def glottal(f0, rng, jitter=0.06, shimmer=0.3, subharm=0.35):
    """Kehliger Stimmquellen-Impulszug.

    f0: Grundfrequenz je Sample (Liste). jitter: Tonhoehenschwankung,
    shimmer: Lautstaerkeschwankung je Periode, subharm: jede zweite Periode
    leiser (rauer, knurrender Eindruck).
    """
    n = len(f0)
    wob = smooth_noise(n, 18.0, rng)
    out = [0.0] * n
    ph = 0.0
    amp = 1.0
    odd = False
    prev = 0.0
    open_q = 0.55
    for i in range(n):
        ph += f0[i] * (1.0 + jitter * wob[i]) / SR
        if ph >= 1.0:
            ph -= 1.0
            odd = not odd
            amp = (1.0 - shimmer * rng.random()) * ((1.0 - subharm) if odd else 1.0)
        p = math.sin(math.pi * ph / open_q) ** 2 if ph < open_q else 0.0
        v = p * amp
        out[i] = v - prev  # Ableitung: Abstrahlung am Mund, mehr Obertoene
        prev = v
    return out


def throat(src, f1, rng, breath=0.25):
    """Formant-Filter fuer einen tiefen, kehligen Vokal, dazu Hauch-Rauschen."""
    n = len(src)
    noise = mul(white(n, rng), [abs(v) * 8.0 for v in src])
    exc = [a + breath * b for a, b in zip(src, noise)]
    f2 = [v * 1.8 for v in f1] if isinstance(f1, list) else f1 * 1.8
    out = zeros(n)
    add_into(out, svf(exc, f1, 5.0, "bp"), 0, 1.0)
    add_into(out, svf(exc, f2, 6.0, "bp"), 0, 0.6)
    add_into(out, svf(exc, 2400.0, 8.0, "bp"), 0, 0.25)
    add_into(out, svf(exc, 180.0, 1.5, "lp"), 0, 0.5)
    return out


def wet(x, rng, depth=0.6, rate=28.0):
    """Feuchtigkeit: unregelmaessige Amplitudenmodulation wie Gurgeln."""
    am = smooth_noise(len(x), rate, rng)
    return [v * (1.0 - depth + depth * (0.5 + 0.5 * m)) for v, m in zip(x, am)]


def bubbles(n, rng, count, start, end, gain):
    """Kurze aufsteigende Blasen-Toene (feucht), zufaellig zwischen start und end."""
    out = zeros(n)
    for _ in range(count):
        t = rng.uniform(start, end)
        ln = n_of(rng.uniform(0.006, 0.012))
        f_a = rng.uniform(300.0, 600.0)
        f_b = f_a * rng.uniform(1.6, 2.4)
        ph = 0.0
        blip = [0.0] * ln
        for i in range(ln):
            f = f_a + (f_b - f_a) * i / ln
            ph += f / SR
            blip[i] = math.sin(2.0 * math.pi * ph) * math.sin(math.pi * i / ln)
        add_into(out, blip, n_of(t), gain * rng.uniform(0.5, 1.0))
    return out


def variant_a():
    rng = random.Random(1001)
    total = 0.85
    n = n_of(total)
    f0 = curve(n, [(0.0, 55.0), (HIT, 95.0), (HIT + 0.01, 135.0), (total, 80.0)])
    f1 = curve(n, [(0.0, 350.0), (HIT, 600.0), (HIT + 0.01, 700.0), (total, 450.0)])
    voice = throat(glottal(f0, rng), f1, rng)
    voice = normalize(wet(voice, rng, 0.5), 1.0)
    # Lautstaerke: exponentiell steigend bis zum Schlag, dann kurzer Ausstoss, Abfall
    env = zeros(n)
    hit_i = n_of(HIT)
    for i in range(n):
        if i < hit_i:
            env[i] = 10.0 ** ((-20.0 + 14.0 * (i / hit_i) ** 1.5) / 20.0)
        else:
            t = (i - hit_i) / SR
            env[i] = math.exp(-t / 0.12)
    voice = mul(voice, env)
    burst = mul(svf(white(n, rng), 900.0, 1.0, "bp"), env_ad(n, 0.003, 0.05, delay=HIT))
    add_into(voice, burst, 0, 0.5)
    add_into(voice, bubbles(n, rng, 6, 0.1, HIT, 1.0), 0, 0.05)
    return finish(voice, PEAK_DB, length=total, fade_in_ms=20.0, fade_out_ms=80.0)


def variant_b():
    rng = random.Random(2002)
    total = 1.1
    n = n_of(total)
    out = zeros(n)
    # Einatmen bis 0,48 s
    inh_n = n_of(HIT - 0.02)
    sweep = curve(inh_n, [(0.0, 300.0), (HIT - 0.02, 1300.0)])
    inhale = svf(white(inh_n, rng), sweep, 2.0, "bp")
    inhale = normalize(wet(inhale, rng, 0.7, 22.0), 1.0)
    inhale = mul(inhale, env_points(inh_n, [(0.0, 0.0), (0.1, 0.15), (HIT - 0.03, 0.6), (HIT - 0.02, 0.0)]))
    add_into(out, inhale, 0, 0.35)
    add_into(out, bubbles(inh_n, rng, 8, 0.05, HIT - 0.04, 1.0), 0, 0.06)
    # Schrei ab 0,5 s
    sn = n - n_of(HIT)
    f0 = curve(sn, [(0.0, 150.0), (0.08, 125.0), (total - HIT, 85.0)])
    f1 = curve(sn, [(0.0, 750.0), (total - HIT, 500.0)])
    roar = throat(glottal(f0, rng, jitter=0.1, shimmer=0.5, subharm=0.5), f1, rng, breath=0.6)
    roar = mul(normalize(roar, 1.0), env_points(sn, [(0.0, 0.0), (0.006, 1.0), (0.15, 0.7), (total - HIT, 0.0)]))
    add_into(out, roar, n_of(HIT), 1.0)
    return finish(out, PEAK_DB, length=total, fade_in_ms=20.0, fade_out_ms=80.0)


def grunt(rng, length, f0_start, f0_end, f1):
    n = n_of(length)
    f0 = curve(n, [(0.0, f0_start), (length, f0_end)])
    g = throat(glottal(f0, rng, jitter=0.08), f1, rng, breath=0.4)
    g = normalize(wet(g, rng, 0.7, 35.0), 1.0)
    return mul(g, env_points(n, [(0.0, 0.0), (0.008, 1.0), (length * 0.5, 0.7), (length, 0.0)]))


def variant_c():
    rng = random.Random(3003)
    total = 0.80
    n = n_of(total)
    out = zeros(n)
    for t, f_a, f_b, gain, ln in ((0.0, 70.0, 66.0, 0.35, 0.09),
                                   (0.25, 80.0, 76.0, 0.45, 0.08),
                                   (0.40, 92.0, 88.0, 0.55, 0.07)):
        add_into(out, grunt(rng, ln, f_a, f_b, 420.0), n_of(t), gain)
    add_into(out, grunt(rng, 0.28, 125.0, 85.0, 620.0), n_of(HIT), 1.0)
    add_into(out, bubbles(n, rng, 5, 0.0, HIT, 1.0), 0, 0.05)
    return finish(out, PEAK_DB, length=total, fade_in_ms=5.0, fade_out_ms=60.0)


def main():
    for name, fn in (("ghul-a-knurren-steigend", variant_a),
                     ("ghul-b-atem-schrei", variant_b),
                     ("ghul-c-gurgeln-zaehlen", variant_c)):
        write_wav(OUT / f"{name}.wav", [fn()])
        print(name)


if __name__ == "__main__":
    main()
