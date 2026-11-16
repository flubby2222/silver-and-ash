"""Hoerprobe 2 – Pistole, Klinge und Schritt nach den Regeln der Recherche.

Regel: ein Geraeusch = eine Geste aus hoechstens drei verschmelzenden Schichten
(Anschlag, Koerper, Ausklang), kein Vorlauf, keine Zusatzereignisse. Die zwei
neuen Varianten je Geraeusch unterscheiden sich im Charakter, nicht in der
Zahl der Ereignisse.

  pistole-d-hell-kurz.wav       Druckpuls + kurzes Rauschen | Knall 22 ms + Stoss 100->45 Hz | kalte fruehe Reflexionen, Hall 0,35 s
  pistole-e-dunkel-schwer.wav   weicher Druckpuls | breiter, tiefer Knall 40 ms + Stoss 80->38 Hz | dunkler Gewoelbe-Hall 0,5 s
  pistole-d-hell-kurz-22k.wav   Zusatz zur Frage "Klang der Aera": Variante d, mit ffmpeg auf 22 050 Hz und
                                zurueck gerechnet (nichts ueber etwa 11 kHz); sonst identisch
  klinge-d-kurz-dumpf.wav       leiser Kanten-Klick | dumpfer Schlag 25 ms + Koerperton 110->60 Hz | kein Ausklang
  klinge-e-schwer-nass.wav      kaum Klick | tieferer Schlag 40 ms + Koerperton 80->45 Hz | ein kurzes nasses Nachgeben (Bandrauschen 500 Hz)
  schritt-d-klar-1..4.wav       Klick 1 ms | Sohle 1,3 kHz 9 ms + Stoss 80 Hz | zwei kalte Reflexionen
  schritt-e-schwer-1..4.wav     kaum Klick | Sohle 700 Hz 14 ms + kraeftiger Stoss 60 Hz | zwei Reflexionen
  schritt-d/e-...-folge.wav     Hoerhilfe: die vier Schritte zweimal im Gehtempo

Als Vergleich wird je die Variante aus Hoerprobe 1 kopiert, die den Regeln am
naechsten kommt: pistole-a-trocken, klinge-a-dumpf, schritt-a-weich.

Aufruf: python treffer_schuss_schritt.py
"""
import math
import random

from gemeinsam import HERE, copy_reference
from synth import (SR, add_into, curve, env_ad, finish, mul, n_of, osc_sine,
                   reflections, reverb, svf, white, write_wav, zeros)


def pressure_pulse(n, length_ms, neg=0.25):
    """Druckpuls nach Friedlander-Form: steiler Anstieg, positiver Teil, kleiner negativer Nachschwinger."""
    T = length_ms / 1000.0 * SR
    out = [0.0] * n
    for i in range(n):
        t = i / T
        out[i] = (1.0 - t) * math.exp(-2.0 * t) if t < 1.0 else -neg * math.sin(math.pi * min(1.0, (t - 1.0) / 2.0)) * math.exp(-(t - 1.0))
    return out


def body(rng, n, cutoff, tau, f_start, f_end, thud_tau, thud_gain):
    x = mul(svf(white(n, rng), cutoff, 0.7, "lp"), env_ad(n, 0.0008, tau))
    s = mul(osc_sine(curve(n, [(0.0, f_start), (0.08, f_end)])), env_ad(n, 0.002, thud_tau))
    return add_into(x, s, 0, thud_gain)


def click(rng, n, hp, tau):
    return mul(svf(white(n, rng), hp, 0.7, "hp"), env_ad(n, 0.0002, tau))


# --- Pistole ----------------------------------------------------------------

def pistole_d():
    rng = random.Random(8101)
    n = n_of(0.40)
    dry = zeros(n)
    add_into(dry, pressure_pulse(n, 1.2), 0, 0.9)
    add_into(dry, click(rng, n, 2000.0, 0.003), 0, 0.6)
    add_into(dry, body(rng, n, 1400.0, 0.022, 100.0, 45.0, 0.035, 0.8), 0, 1.0)
    er = reflections(dry, [(6.0, 0.3), (11.0, 0.22), (17.0, 0.15)], tail=0.0, damp_hz=6000.0)
    room = reverb(dry, rt60=0.35, size=0.8, damp_hz=5000.0, tail=0.0, seed=81)
    out = add_into(add_into(dry, er, 0, 1.0), room, 0, 0.5)
    return finish(out, -1.5, length=0.40, fade_out_ms=60.0)


def pistole_e():
    rng = random.Random(8201)
    n = n_of(0.55)
    dry = zeros(n)
    add_into(dry, pressure_pulse(n, 2.5), 0, 0.6)
    add_into(dry, body(rng, n, 800.0, 0.04, 80.0, 38.0, 0.06, 1.1), 0, 1.0)
    room = reverb(dry, rt60=0.5, size=1.3, damp_hz=1500.0, tail=0.0, seed=82)
    out = add_into(dry, room, 0, 1.2)
    return finish(out, -1.5, length=0.55, fade_out_ms=80.0)


def band_limit_22k(x):
    """Rechnet das Signal mit ffmpeg auf 22 050 Hz herunter und wieder auf 44 100 Hz hoch.
    Danach fehlt alles oberhalb von etwa 11 kHz, wie bei einer mit 22 kHz abgelegten Datei."""
    import struct
    import subprocess
    import tempfile
    import wave
    from pathlib import Path
    with tempfile.TemporaryDirectory() as td:
        src, dst = Path(td) / "src.wav", Path(td) / "dst.wav"
        write_wav(src, [x])
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(src), "-af",
                        "aresample=22050,aresample=44100", "-c:a", "pcm_s16le", str(dst)], check=True)
        with wave.open(str(dst), "rb") as w:
            n = w.getnframes()
            data = struct.unpack("<%dh" % n, w.readframes(n))
    return [v / 32768.0 for v in data][:len(x)]


# --- Klinge -----------------------------------------------------------------

def klinge_d():
    rng = random.Random(9101)
    n = n_of(0.24)
    x = body(rng, n, 350.0, 0.025, 110.0, 60.0, 0.04, 0.8)
    add_into(x, click(rng, n, 2500.0, 0.0015), 0, 0.25)
    return finish(x, -4.0, length=0.24, fade_out_ms=40.0)


def klinge_e():
    rng = random.Random(9201)
    n = n_of(0.32)
    x = body(rng, n, 250.0, 0.04, 80.0, 45.0, 0.06, 1.0)
    add_into(x, click(rng, n, 2500.0, 0.001), 0, 0.08)
    wet = mul(svf(white(n, rng), 500.0, 2.0, "bp"), env_ad(n, 0.01, 0.07, delay=0.005))
    add_into(x, wet, 0, 0.5)
    return finish(x, -4.0, length=0.32, fade_out_ms=50.0)


# --- Schritt ----------------------------------------------------------------

def schritt_d(take):
    rng = random.Random(9500 + take)
    j = rng.uniform
    n = n_of(0.16)
    x = body(rng, n, 1300.0 * j(0.85, 1.15), 0.009 * j(0.8, 1.2), 80.0 * j(0.9, 1.1), 56.0, 0.015, 0.5)
    add_into(x, click(rng, n, 3000.0 * j(0.85, 1.15), 0.001), 0, 0.35 * j(0.7, 1.0))
    er = reflections(x, [(4.0, 0.25), (9.0, 0.15)], tail=0.0, damp_hz=5000.0)
    add_into(x, er, 0, 1.0)
    return finish(x, -12.0 + j(-1.5, 0.0), length=0.16, fade_out_ms=30.0)


def schritt_e(take):
    rng = random.Random(9600 + take)
    j = rng.uniform
    n = n_of(0.20)
    x = body(rng, n, 700.0 * j(0.85, 1.15), 0.014 * j(0.8, 1.2), 60.0 * j(0.9, 1.1), 42.0, 0.025, 1.0)
    add_into(x, click(rng, n, 3000.0, 0.0008), 0, 0.1 * j(0.7, 1.0))
    er = reflections(x, [(6.0, 0.25), (13.0, 0.15)], tail=0.0, damp_hz=3000.0)
    add_into(x, er, 0, 1.0)
    return finish(x, -12.0 + j(-1.5, 0.0), length=0.20, fade_out_ms=40.0)


def steps(prefix, fn):
    takes = []
    for t in range(1, 5):
        x = fn(t)
        takes.append(x)
        write_wav(HERE / f"{prefix}-{t}.wav", [x])
    seq = zeros(n_of(0.5 * 8 + 0.3))
    for k in range(8):
        add_into(seq, takes[k % 4], n_of(0.1 + 0.5 * k), 1.0)
    write_wav(HERE / f"{prefix}-folge.wav", [seq])


def main():
    d = pistole_d()
    write_wav(HERE / "pistole-d-hell-kurz.wav", [d])
    write_wav(HERE / "pistole-d-hell-kurz-22k.wav", [finish(band_limit_22k(d), -1.5, fade_out_ms=60.0)])
    write_wav(HERE / "pistole-e-dunkel-schwer.wav", [pistole_e()])
    write_wav(HERE / "klinge-d-kurz-dumpf.wav", [klinge_d()])
    write_wav(HERE / "klinge-e-schwer-nass.wav", [klinge_e()])
    steps("schritt-d-klar", schritt_d)
    steps("schritt-e-schwer", schritt_e)
    for ref in ("pistole-a-trocken.wav", "klinge-a-dumpf.wav", "schritt-a-weich-1.wav",
                "schritt-a-weich-2.wav", "schritt-a-weich-3.wav", "schritt-a-weich-4.wav",
                "schritt-a-weich-folge.wav"):
        copy_reference(ref)
    print("fertig")


if __name__ == "__main__":
    main()
