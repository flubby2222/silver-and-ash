"""Hoerprobe 3 – Ghul, Pistole, Klinge, Schritt: nach den Tutorials verbessert, je sauber und durch die PS1-Kette.

Grundlage ist je die Variante aus Hoerprobe 2, die den Regeln der Audio Bible am
besten folgt (Ghul d, Pistole d, Klinge d, Schritt d). Was sich gegenueber
Hoerprobe 2 aendert und woher die Idee stammt, steht in den Funktionen und im
Nachtrag von design/grundlagen/grundlagen-audio.md.

Dateien (je "-sauber" und "-ps1"; PS1 = ps1_klang.process mit 22 050 Hz, ohne Hall):
  ghul-sauber.wav / ghul-ps1.wav
  pistole-sauber.wav / pistole-ps1.wav
  klinge-sauber-1..3.wav / klinge-ps1-1..3.wav
  schritt-sauber-1..4.wav / schritt-ps1-1..4.wav, dazu je -folge.wav (Hoerhilfe)

Aufruf: python geraeusche.py
"""
import math
import random
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent

import ps1_klang as P  # noqa: E402
from synth import (SR, add_into, curve, env_ad, finish, lp1, mul, n_of, normalize, osc_sine,  # noqa: E402
                   reflections, reverb, svf, white, write_wav, zeros)

PS1_RATE = 22050


def saturate(x, drive):
    """Weiche Saettigung (tanh), wie Farnells Guns-Patch; Pegel danach wieder auf die alte Spitze."""
    peak = max(abs(v) for v in x) or 1.0
    y = [math.tanh(drive * v / peak) for v in x]
    return normalize(y, peak)


def pressure_pulse(n, length_ms, neg=0.25):
    T = length_ms / 1000.0 * SR
    out = [0.0] * n
    for i in range(n):
        t = i / T
        out[i] = (1.0 - t) * math.exp(-2.0 * t) if t < 1.0 else \
            -neg * math.sin(math.pi * min(1.0, (t - 1.0) / 2.0)) * math.exp(-(t - 1.0))
    return out


def click(rng, n, hp, tau):
    return mul(svf(white(n, rng), hp, 0.7, "hp"), env_ad(n, 0.0002, tau))


# --- Ghul ---------------------------------------------------------------------

HIT = 0.50


def voice_source(f0, subharm=0.45):
    """Glottis-Impulszug mit erhoehtem Kosinus (wie Farnells "cord model"); Rauheit ueber Subharmonische."""
    n = len(f0)
    out = [0.0] * n
    ph, odd, prev, amp = 0.0, False, 0.0, 1.0
    for i in range(n):
        ph += f0[i] / SR
        if ph >= 1.0:
            ph -= 1.0
            odd = not odd
            amp = (1.0 - subharm) if odd else 1.0
        p = math.sin(math.pi * ph / 0.55) ** 2 if ph < 0.55 else 0.0
        out[i] = p * amp - prev
        prev = p * amp
    return out


def ghul():
    """Eine Geste: das Maul oeffnet sich in 0,5 s und schliesst sich beim Schlag.

    Neu gegenueber Hoerprobe 2 (ghul-d):
    - Eine Steuerkurve "Maul offen" m(t) treibt zugleich Lautstaerke und einen Tiefpass,
      der von 280 Hz (Maul zu) auf 2,5 kHz (offen) aufgeht (Springbound-Devlog: Tiefpass
      um 280 Hz fuer geschlossenen Mund). Das ist eine Bewegung, nicht zwei.
    - Formanten nach Farnells Vokaltabelle ("U": 450 / 1030 / 2380 Hz) statt frei gewaehlter Werte.
    - Zweite Schicht: tiefer Sinus auf dem Grundton fuer Gewicht (Violet Recording: Sub unter dem Knurren).
      Der Atem aus Hoerprobe 2 entfaellt.
    - Parallele Saettigung: eine gesaettigte Kopie leise darunter (Violet Recording).
    """
    total = 0.62
    n = n_of(total)
    hit = n_of(HIT)
    rel = n_of(0.04)
    m = [0.0] * n
    for i in range(n):
        if i < hit:
            m[i] = (i / hit) ** 1.5
        elif i < hit + rel:
            m[i] = 0.5 + 0.5 * math.cos(math.pi * (i - hit) / rel)
    f0 = 58.0
    src = voice_source([f0] * n)
    tract = zeros(n)
    for f, q, g in ((450.0, 5.0, 1.0), (1030.0, 6.0, 0.5), (2380.0, 8.0, 0.2)):
        add_into(tract, svf(src, f, q, "bp"), 0, g)
    add_into(tract, svf(src, 160.0, 1.0, "lp"), 0, 0.6)
    mouth = svf(tract, [280.0 + 2220.0 * v for v in m], 0.8, "lp")
    voice = normalize(mouth, 1.0)
    voice = add_into(voice, saturate(voice, 3.0), 0, 0.3)
    sub = osc_sine([f0] * n)
    out = add_into(voice, sub, 0, 0.35)
    env = [10.0 ** (-30.0 * (1.0 - v) / 20.0) if v > 0 else 0.0 for v in m]
    for i in range(hit, n):  # nach dem Schlag folgt die Lautstaerke direkt der Schliessbewegung
        env[i] = m[i]
    return finish(mul(out, env), -3.0, length=total, fade_in_ms=10.0, fade_out_ms=20.0)


# --- Pistole ------------------------------------------------------------------

def pistole():
    """Knall nach Goj (Game Developer) und Farnell (Practical 30), Raum wie Hoerprobe 2 d.

    Neu gegenueber Hoerprobe 2 (pistole-d):
    - Koerper aus EINER Rauschquelle in zwei Baendern: Band um 550 Hz und Tiefpass 250 Hz,
      Ausklingen etwa 120 ms (Werte aus Gojs Presets), statt Tiefpass 1,4 kHz.
    - Knall-Rauschen erst Hochpass, dann Bandpass (Farnell: "crack").
    - Weiche Saettigung des trockenen Schusses (Farnell: tanh), macht ihn dichter.
    - Tiefer Sinus-Stoss bleibt als Sub-Schicht (Splice: Low-End-Element).
    """
    rng = random.Random(13101)
    n = n_of(0.40)
    dry = zeros(n)
    noise = white(n, rng)
    crack = mul(svf(svf(noise, 1500.0, 0.7, "hp"), 3200.0, 1.0, "bp"), env_ad(n, 0.0002, 0.004))
    band = svf(noise, 550.0, 1.2, "bp")
    low = svf(noise, 250.0, 0.7, "lp")
    body = mul([0.7 * a + 1.0 * b for a, b in zip(band, low)], env_ad(n, 0.001, 0.03))
    sub = mul(osc_sine(curve(n, [(0.0, 90.0), (0.08, 45.0)])), env_ad(n, 0.002, 0.04))
    add_into(dry, pressure_pulse(n, 1.2), 0, 0.8)
    add_into(dry, crack, 0, 0.9)
    add_into(dry, normalize(body, 1.0), 0, 0.9)
    add_into(dry, sub, 0, 0.6)
    dry = saturate(dry, 2.0)
    er = reflections(dry, [(6.0, 0.3), (11.0, 0.22), (17.0, 0.15)], tail=0.0, damp_hz=6000.0)
    room = reverb(dry, rt60=0.35, size=0.8, damp_hz=5000.0, tail=0.0, seed=131)
    out = add_into(add_into(dry, er, 0, 1.0), room, 0, 0.5)
    return finish(out, -1.5, length=0.40, fade_out_ms=60.0)


# --- Klinge -------------------------------------------------------------------

def klinge(take):
    """Treffer auf Fleisch: Rauschen durch einen resonanten Tiefpass mit schnell fallender
    Grenzfrequenz (Farnell, Practical 31 "blast"), darunter ein Ton mit fallender Tonhoehe
    (Farnell, Practical 7). Drei Varianten mit zufaelligen Abweichungen (Splice/Goj: je
    Abspielen leicht variieren).

    Neu gegenueber Hoerprobe 2 (klinge-d): Der Schlag bewegt sich (Grenzfrequenz 1,8 kHz -> 180 Hz
    in 50 ms) statt eines festen Tiefpasses bei 350 Hz; dazu drei Varianten statt einer.
    """
    rng = random.Random(14100 + take)
    j = rng.uniform
    n = n_of(0.24)
    fc0 = 1800.0 * j(0.85, 1.15)
    cut = [180.0 + (fc0 - 180.0) * math.exp(-i / (0.015 * SR)) for i in range(n)]
    body = mul(svf(white(n, rng), cut, 2.0, "lp"), env_ad(n, 0.001, 0.03))
    p = j(0.92, 1.08)
    tone = mul(osc_sine(curve(n, [(0.0, 110.0 * p), (0.08, 55.0 * p)])), env_ad(n, 0.002, 0.045))
    x = add_into(normalize(body, 1.0), tone, 0, 0.6)
    add_into(x, click(rng, n, 2500.0, 0.0015), 0, 0.12)
    return finish(x, -4.0 + j(-1.5, 0.0), length=0.24, fade_out_ms=40.0)


# --- Schritt ------------------------------------------------------------------

def grf(n, heel_end, ball_start, ball_level):
    """Bodenreaktions-Kurve eines Schritts (Farnell, Practical 26): Ferse, Abrollen, Ballen –
    eine durchgehende Kurve ohne Luecke."""
    out = [0.0] * n
    for i in range(n):
        t = i / SR
        heel = math.exp(-t / heel_end) * min(1.0, t / 0.0015)
        ball = ball_level * math.sin(math.pi * min(1.0, max(0.0, (t - ball_start) / 0.05))) if t >= ball_start else 0.0
        out[i] = max(heel, ball, 0.25 * math.exp(-t / 0.06))
    return out


def schritt(take):
    """Neu gegenueber Hoerprobe 2 (schritt-d): Statt eines einzelnen Abklingens traegt eine
    Bodenreaktions-Kurve (Ferse, Abrollen, Ballen) das Rauschen (Farnell). Der Ballen setzt
    weich ein, ohne Pause dazwischen, damit es EIN Aufsatz bleibt. Vier Schritte, die sich
    in Tempo (Ballen nach 45-65 ms), Filter und Pegel unterscheiden."""
    rng = random.Random(15100 + take)
    j = rng.uniform
    n = n_of(0.16)
    curve_ = grf(n, 0.008 * j(0.8, 1.2), j(0.045, 0.065), j(0.25, 0.4))
    tex = svf(svf(white(n, rng), 1300.0 * j(0.85, 1.15), 0.8, "lp"), 200.0, 0.7, "hp")
    x = mul(tex, curve_)
    thud = mul(osc_sine(curve(n, [(0.0, 80.0 * j(0.9, 1.1)), (0.05, 56.0)])), env_ad(n, 0.002, 0.015))
    add_into(x, thud, 0, 0.5)
    add_into(x, click(rng, n, 3000.0 * j(0.85, 1.15), 0.001), 0, 0.3 * j(0.7, 1.0))
    er = reflections(x, [(4.0, 0.25), (9.0, 0.15)], tail=0.0, damp_hz=5000.0)
    add_into(x, er, 0, 1.0)
    return finish(x, -12.0 + j(-1.5, 0.0), length=0.16, fade_out_ms=30.0)


# --- Ausgabe ------------------------------------------------------------------

def ps1(x):
    """Float-Signal (-1..1) durch die PS1-Kette; Ergebnis wieder als Float."""
    ints = [int(round(v * 32767)) for v in x]
    out = P.process([ints], rate=PS1_RATE)[0]
    return [v / 32767.0 for v in out]


def save_pair(name, x):
    write_wav(HERE / f"{name}-sauber.wav", [x])
    y = ps1(x)
    write_wav(HERE / f"{name}-ps1.wav", [y])
    return y


def save_takes(name, xs):
    for k, x in enumerate(xs, 1):
        write_wav(HERE / f"{name}-sauber-{k}.wav", [x])
    ys = [ps1(x) for x in xs]
    for k, y in enumerate(ys, 1):
        write_wav(HERE / f"{name}-ps1-{k}.wav", [y])
    return ys


def folge(takes):
    seq = zeros(n_of(0.5 * 8 + 0.3))
    for k in range(8):
        add_into(seq, takes[k % len(takes)], n_of(0.1 + 0.5 * k), 1.0)
    return seq


def main():
    save_pair("ghul", ghul())
    save_pair("pistole", pistole())
    save_takes("klinge", [klinge(t) for t in range(1, 4)])
    clean = [schritt(t) for t in range(1, 5)]
    dirty = save_takes("schritt", clean)
    write_wav(HERE / "schritt-sauber-folge.wav", [folge(clean)])
    write_wav(HERE / "schritt-ps1-folge.wav", [folge(dirty)])
    print("fertig")


if __name__ == "__main__":
    main()
