"""Hoerprobe 1 – Katakomben-Ambiente (30 s, nahtlose Schleife, stereo).

Drei Varianten, alle mit derselben Grundflaeche (tiefes Grollen unter etwa
120 Hz und sehr leise Luft ueber 6 kHz), die Mitte des Hoerbereichs bleibt
weitgehend frei:
  ambiente-a-grollen.wav / .ogg            nur die Grundflaeche, langsam atmend
  ambiente-b-tropfen.wav / .ogg            Grundflaeche + seltene hohe Wassertropfen (3-5,5 kHz) mit Echo
  ambiente-c-ferne-ereignisse.wav / .ogg   Grundflaeche + ferne, dumpfe Ereignisse, meist unter 400 Hz
                                           (Steinpoltern, Schaben, tiefes Wehen)

Nahtlosigkeit: Die Rauschflaechen werden 2 s laenger erzeugt und das
Ueberstehende mit gleichleistender Blende in den Anfang gemischt. Die
Lautstaerke-Schwankungen haben ganzzahlig viele Perioden in 30 s. Ereignisse
werden kreisfoermig eingefuegt: was ueber das Ende ragt, klingt am Anfang aus.
Die OGG-Datei entsteht mit ffmpeg aus der WAV-Datei.

Aufruf: python ambiente.py   (schreibt in den Ordner dieses Skripts)
"""
import math
import random
import subprocess
from pathlib import Path

from synth import (SR, add_into, add_wrapped, curve, env_ad, env_points, lin, lp1,
                   mul, n_of, reverb, scale, svf, white, write_wav, zeros)

OUT = Path(__file__).resolve().parent
LENGTH = 30.0
XFADE = 2.0
RMS_DB = -38.0  # Ziel fuer die mittlere Lautstaerke (beide Kanaele)


def loop_noise(rng, make):
    """Erzeugt LENGTH+XFADE s mit make(white) und blendet das Ende in den Anfang."""
    n = n_of(LENGTH)
    L = n_of(XFADE)
    x = make(white(n + L, rng))
    out = x[:n]
    for i in range(L):
        a = (i + 0.5) / L
        out[i] = x[i] * math.sin(0.5 * math.pi * a) + x[n + i] * math.cos(0.5 * math.pi * a)
    return out


def periodic_lfo(n, terms):
    """Summe von Sinus-Schwankungen mit ganzzahlig vielen Perioden pro Schleife."""
    out = [1.0] * n
    for cycles, depth, phase in terms:
        w = 2.0 * math.pi * cycles / n
        for i in range(n):
            out[i] += depth * math.sin(w * i + phase)
    return out


def bed(rng, channel):
    """Grundflaeche eines Kanals: Grollen + Luft."""
    n = n_of(LENGTH)
    rumble = loop_noise(rng, lambda w: svf(svf(w, 70.0, 0.7, "lp"), 120.0, 0.7, "lp"))
    rumble = mul(rumble, periodic_lfo(n, [(1, 0.35, channel * 1.3), (3, 0.15, 0.7 + channel)]))
    air = loop_noise(rng, lambda w: svf(svf(w, 6500.0, 0.7, "hp"), 9000.0, 0.7, "lp"))
    air = mul(air, periodic_lfo(n, [(2, 0.4, 2.0 + channel)]))
    out = zeros(n)
    add_into(out, rumble, 0, 1.0)
    add_into(out, air, 0, 0.012)
    return out


def pan(x, p):
    """Gleichleistendes Panorama, p von -1 (links) bis 1 (rechts)."""
    a = (p + 1.0) * 0.25 * math.pi
    return scale(x, math.cos(a)), scale(x, math.sin(a))


def drip(rng):
    ln = n_of(0.03)
    f_a = rng.uniform(3000.0, 4200.0)
    f_b = f_a * rng.uniform(1.15, 1.3)
    ph = 0.0
    out = [0.0] * ln
    for i in range(ln):
        ph += (f_a + (f_b - f_a) * (i / ln) ** 0.5) / SR
        out[i] = math.sin(2.0 * math.pi * ph) * math.exp(-i / (0.006 * SR)) * min(1.0, i / 20.0)
    # zwei Echos aus dem Gang, dunkler
    full = zeros(n_of(0.35))
    add_into(full, out, 0, 1.0)
    add_into(full, lp1(out, 3500.0), n_of(0.09), 0.25)
    add_into(full, lp1(out, 2500.0), n_of(0.17), 0.12)
    return full


def far_thud(rng):
    n = n_of(0.25)
    x = mul(svf(white(n, rng), 220.0, 0.8, "lp"), env_ad(n, 0.004, 0.05))
    # mehrere kleine Steine nach dem ersten
    for _ in range(4):
        d = rng.uniform(0.04, 0.2)
        m = n - n_of(d)
        add_into(x, mul(svf(white(m, rng), 300.0, 0.8, "lp"), env_ad(m, 0.002, 0.015)), n_of(d), rng.uniform(0.2, 0.5))
    wet = reverb(x, rt60=2.5, size=2.2, damp_hz=600.0, tail=3.0, seed=rng.randrange(99))
    return add_into(wet, x, 0, 0.6)


def far_scrape(rng):
    ln = rng.uniform(1.5, 2.5)
    n = n_of(ln)
    sweep = curve(n, [(0.0, 180.0), (ln, 320.0)])
    x = svf(white(n, rng), sweep, 3.0, "bp")
    x = mul(x, env_points(n, [(0.0, 0.0), (ln * 0.3, 1.0), (ln * 0.7, 0.8), (ln, 0.0)]))
    wet = reverb(x, rt60=2.0, size=2.0, damp_hz=500.0, tail=2.0, seed=rng.randrange(99))
    return add_into(wet, x, 0, 0.4)


def far_wind(rng):
    ln = rng.uniform(5.0, 7.0)
    n = n_of(ln)
    sweep = curve(n, [(0.0, 90.0), (ln * 0.5, 240.0), (ln, 110.0)])
    x = svf(svf(white(n, rng), sweep, 4.0, "bp"), 400.0, 0.7, "lp")
    return mul(x, env_points(n, [(0.0, 0.0), (ln * 0.5, 1.0), (ln, 0.0)]))


def place(left, right, sig, t, p, gain):
    l, r = pan(sig, p)
    off = n_of(t)
    add_wrapped(left, l, off, gain)
    add_wrapped(right, r, off, gain)


def variant(key):
    rng = random.Random({"a": 41, "b": 42, "c": 43}[key])
    left = bed(rng, 0)
    right = bed(rng, 1)
    if key == "b":
        # Tropfen in unregelmaessigen Abstaenden von 2,5 bis 6 s
        t = 1.2
        while t < LENGTH - 0.5:
            place(left, right, drip(rng), t, rng.uniform(-0.8, 0.8), 0.15 * rng.uniform(0.6, 1.0))
            t += rng.uniform(2.5, 6.0)
    elif key == "c":
        for t, kind, p, g in ((3.0, far_thud, -0.6, 0.9), (11.5, far_wind, 0.2, 0.5),
                              (17.0, far_scrape, 0.7, 0.35), (24.0, far_thud, 0.4, 0.6),
                              (27.5, far_wind, -0.4, 0.4)):
            place(left, right, kind(rng), t, p, g)
    # auf Ziel-Mittelwert bringen
    n = len(left)
    rms = math.sqrt((sum(v * v for v in left) + sum(v * v for v in right)) / (2 * n))
    g = lin(RMS_DB) / rms
    return scale(left, g), scale(right, g)


def main():
    names = {"a": "ambiente-a-grollen", "b": "ambiente-b-tropfen", "c": "ambiente-c-ferne-ereignisse"}
    for key, name in names.items():
        left, right = variant(key)
        wav = OUT / f"{name}.wav"
        write_wav(wav, [left, right])
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(wav),
                        "-c:a", "libvorbis", "-q:a", "5", str(OUT / f"{name}.ogg")], check=True)
        print(name)


if __name__ == "__main__":
    main()
