#!/usr/bin/env python3
"""Saeule-Klang Runde 2 (#605): Stein auf Stein statt Bassnote plus Rauschen.

Bausteine statt Runde 1:
- Anschlag: 1 bis 4 ms hartes Rauschen im Band 300 Hz bis 3 kHz (Transient).
- Koerper: unharmonische, schnell abklingende Moden (Verhaeltnisse wie bei einer Steinplatte),
  keine feste Bassnote; ein kurzer tiefer Anteil traegt nur das Gewicht.
- Broeckeln: viele einzelne Koerner (Mini-Aufschlaege) mit unregelmaessiger Zeit und Groesse.
- Raum: fruehe Reflexionen einer Steinhalle (Verzoegerungen 6 bis 90 ms), dunkel gedaempft.
Nur Python-Standardbibliothek. Aufruf: python bauen.py [Zielordner]
"""
import math
import random
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
import bauen as b1  # noqa: E402  (Helfer der Runde 1: lowpass, highpass, add, ...)

SR = b1.SR
TICK = b1.TICK
T_HIT = b1.BRUCH_AUFSCHLAG

# Modenverhaeltnisse einer dicken Steinplatte/eines Brockens (unharmonisch)
MODEN = (1.0, 1.58, 2.21, 2.93, 3.74)


def transient(rng, ms, lo=300, hi=3000):
    n = max(8, int(ms / 1000 * SR))
    r = b1.band(b1.rauschen(rng, n + 200), lo, hi)[100:100 + n]
    h = [math.exp(-i / (n * 0.35)) for i in range(n)]
    return b1.mul(r, h)


def brocken(rng, dauer, f0, tau, gain_t=1.0, gain_m=1.0, tief=0.0, tief_f=85.0, tief_tau=0.06, hart=3.0):
    """Ein Steinaufschlag: Transient + unharmonische Moden (+ optional tiefes Gewicht)."""
    n = int(dauer * SR)
    out = [0.0] * n
    t = transient(rng, hart, 300, 3200)
    b1.add(out, t, 0.0, gain_t * 2.2)
    for k, ratio in enumerate(MODEN):
        f = f0 * ratio * rng.uniform(0.96, 1.04)
        if f > 3200:
            break
        m = b1.modus(n, f, tau / (1 + 0.6 * k), 0.0005)
        b1.add(out, m, 0.0, gain_m * rng.uniform(0.5, 1.0) / (1 + 0.3 * k))
    if tief > 0:
        # kurzer dumpfer Druck, kein Ton: tiefgefiltertes Rauschen, Huelle fix
        r = b1.lowpass(b1.rauschen(rng, n), tief_f * 1.6, 2)
        r = b1.mul(r, b1.huellkurve_exp(n, 0.002, tief_tau))
        b1.add(out, r, 0.0, tief * 5.0)
    return out


def koerner(rng, dauer, dichte_a, dichte_e, groesse=1.0, gain=0.3, f_lo=700, f_hi=2600, grosse_selten=0.07):
    """Viele einzelne kleine Aufschlaege; Dichte linear von a nach e (Koerner/s); selten ein groesseres Stueck."""
    n = int(dauer * SR)
    out = [0.0] * n
    t = 0.0
    while True:
        p = min(1.0, t / dauer)
        d = dichte_a + (dichte_e - dichte_a) * p
        t += rng.expovariate(max(d, 1.0))
        if t >= dauer:
            break
        gross = rng.random() < grosse_selten
        s = groesse * (rng.uniform(1.8, 3.0) if gross else rng.uniform(0.5, 1.2))
        f0 = rng.uniform(f_lo, f_hi) / s
        kl = int((0.035 if gross else 0.018) * SR)
        k = brocken(rng, kl / SR, f0, 0.006 * s ** 0.5, 1.0, 0.8, hart=1.5)
        g = gain * rng.uniform(0.35, 1.0) * (0.45 + 0.55 * s / 1.5) * (1 - 0.5 * p)
        b1.add(out, k, t, g)
    return out


def fruehe_reflexionen(x, taps, nachlauf=0.2, fb_hall=0.0):
    """Steinhalle: einzelne fruehe Reflexionen (ms, Gain), jede dumpfer; optional kurzer Schweif."""
    n = len(x) + int(nachlauf * SR)
    x = x + [0.0] * (n - len(x))
    out = list(x)
    for ms, g in taps:
        d = int(ms / 1000 * SR)
        gedaempft = b1.lowpass(x, 2800 - 12 * ms, 1)
        for i in range(n - d):
            out[i + d] += gedaempft[i] * g
    if fb_hall > 0:
        h = b1.hall(out, 0.0, fb_hall)
        return h
    return out


TAPS_NAH = ((6.5, 0.28), (13.0, 0.2), (23.0, 0.12), (37.0, 0.07))
TAPS_RAUM = ((8.0, 0.4), (15.5, 0.34), (27.0, 0.28), (41.0, 0.22), (58.0, 0.16), (79.0, 0.12), (103.0, 0.08))


def fertig(x, fade, dauer):
    x = b1.lowpass(x, 7000, 1)  # nur ganz oben entschaerft, Mitten bleiben (Runde 1: 4,2 kHz hart)
    # weiche Saettigung: hebt die mittlere Lautstaerke gegenueber der Spitze (Anschlaege sind sonst sehr spitz)
    p = b1.spitze(x)
    x = [math.tanh(2.2 * v / p) for v in x]
    return b1.fertig(x, -3.0, dauer, fade)


# ---------- Treffer ----------
def treffer(seed, stufe, v):
    rng = random.Random(seed)
    dauer = 0.30 if stufe == 1 else 0.42
    ges = dauer + 0.15
    n = int(ges * SR)
    x = [0.0] * n
    if stufe == 1:
        if v == "a":  # trocken, nah, hart
            b1.add(x, brocken(rng, 0.25, 520, 0.030, 1.0, 1.0, tief=0.45, tief_f=95, tief_tau=0.045, hart=2.5), 0)
            b1.add(x, koerner(rng, 0.20, 110, 25, 1.0, 0.16), 0.03)
            taps = TAPS_NAH
        else:  # schwerer Brocken, mehr Raum, dumpfer
            b1.add(x, brocken(rng, 0.30, 330, 0.045, 0.8, 1.1, tief=0.8, tief_f=75, tief_tau=0.08, hart=3.5), 0)
            b1.add(x, koerner(rng, 0.26, 80, 20, 1.3, 0.2, 500, 1900), 0.04)
            taps = TAPS_RAUM
    else:
        if v == "a":  # harter Doppelschlag, dann viel Bruchstuecke, trocken
            b1.add(x, brocken(rng, 0.30, 430, 0.040, 1.0, 1.1, tief=0.9, tief_f=80, tief_tau=0.07, hart=3.0), 0)
            b1.add(x, brocken(rng, 0.20, 700, 0.025, 0.7, 0.8, tief=0.2, tief_f=110), 0.045, 0.55)
            b1.add(x, koerner(rng, 0.34, 170, 30, 1.0, 0.24), 0.04)
            taps = TAPS_NAH
        else:  # grosser Schlag, Platte platzt ab, Schutt, mit Raum
            b1.add(x, brocken(rng, 0.35, 300, 0.055, 0.9, 1.2, tief=1.1, tief_f=70, tief_tau=0.10, hart=4.0), 0)
            b1.add(x, brocken(rng, 0.25, 520, 0.03, 0.6, 0.8, tief=0.3, tief_f=95), 0.09, 0.5)
            b1.add(x, koerner(rng, 0.36, 130, 24, 1.4, 0.26, 450, 1800), 0.06)
            taps = TAPS_RAUM
    x = fruehe_reflexionen(x, taps, 0.08 if v == "a" else 0.12)
    return fertig(x, 0.2 if stufe == 1 else 0.26, 0.45 if stufe == 1 else 0.58)


# ---------- Bruch ----------
def bruch(seed, v):
    rng = random.Random(seed)
    gesamt = 2.0
    x = [0.0] * int(gesamt * SR)
    nk = int(T_HIT * SR)
    # Phase 1 bis 0,70 s: Stein knackt und reisst. Koernerwolke mit steigender Dichte (Reissen),
    # darin einzelne harte Risse; leises tiefes Druckrollen darunter
    wolke = koerner(rng, T_HIT, 25, 150, 1.1, 0.10, 500, 2200, grosse_selten=0.03)
    b1.add(x, wolke, 0.0)
    druck = b1.lowpass(b1.rauschen(rng, nk), 220, 3)
    druck = b1.mul(druck, [0.05 + (i / nk) ** 2 for i in range(nk)])
    b1.add(x, druck, 0.0, 1.0 if v == "a" else 1.3)
    risse = (6, 17, 27, 36) if v == "a" else (9, 21, 30, 38)
    for k, tk in enumerate(risse):
        r = brocken(rng, 0.12, 900 - 70 * k, 0.012, 1.0, 0.9, tief=0.05, hart=1.2)
        b1.add(x, r, tk * TICK, 0.45 + 0.12 * k)

    # Phase 2: zwei grosse Trommelstuecke schlagen auf (0,70 s und kurz danach)
    if v == "a":
        stuecke = ((0.0, 1.0, 260, 0.075, 1.2), (0.065, 0.8, 340, 0.06, 1.0))
    else:
        stuecke = ((0.0, 1.0, 230, 0.09, 1.3), (0.095, 0.75, 300, 0.07, 1.0))
    for dt, g, f0, tau, tief in stuecke:
        s = brocken(rng, 0.55, f0, tau, 1.0, 1.2, tief=tief, tief_f=68, tief_tau=0.13, hart=4.0)
        b1.add(x, s, T_HIT + dt, g * 1.4)

    # Phase 3: Schutt rutscht nach und rieselt bis ~1,35 s; b: mehr, groeber, laenger
    if v == "a":
        b1.add(x, koerner(rng, 0.65, 190, 15, 1.1, 0.30), T_HIT + 0.05)
        for dt in (0.17, 0.26, 0.38):
            b1.add(x, brocken(rng, 0.2, rng.uniform(450, 800), 0.02, 0.8, 0.8, tief=0.2, tief_f=100), T_HIT + dt, 0.35)
        taps, fb = TAPS_NAH, 0.14
    else:
        b1.add(x, koerner(rng, 0.85, 240, 22, 1.4, 0.34, 450, 2000), T_HIT + 0.04)
        for dt in (0.13, 0.21, 0.30, 0.42, 0.55):
            b1.add(x, brocken(rng, 0.3, rng.uniform(300, 650), 0.035, 0.8, 0.9, tief=0.4, tief_f=80), T_HIT + dt, 0.42)
        # rutschender Schutt: Rauschen 400 bis 1800 Hz, Huelle laufend an- und abschwellend
        nr = int(0.5 * SR)
        rs = b1.band(b1.rauschen(rng, nr), 400, 1800)
        ph, hh = 0.0, []
        for i in range(nr):
            ph += 2 * math.pi * 11 / SR
            hh.append((math.sin(math.pi * i / nr) ** 1.5) * (0.6 + 0.4 * math.sin(ph) * math.sin(ph * 0.43)) ** 2)
        b1.add(x, b1.mul(rs, hh), T_HIT + 0.12, 0.35)
        taps, fb = TAPS_RAUM, 0.2
    x = fruehe_reflexionen(x, taps, 0.0, fb)
    return fertig(x, 0.5, gesamt)


def main():
    ziel = Path(sys.argv[1]) if len(sys.argv) > 1 else HERE
    ziel.mkdir(parents=True, exist_ok=True)
    d = {}
    for stufe in (1, 2):
        a, b = b1.angleichen([treffer(61000 + stufe, stufe, "a"), treffer(61100 + stufe, stufe, "b")])
        d[f"saeule-treffer-stufe{stufe}-a.wav"], d[f"saeule-treffer-stufe{stufe}-b.wav"] = a, b
    a, b = b1.angleichen([bruch(61201, "a"), bruch(61202, "b")])
    d["saeule-bruch-a.wav"], d["saeule-bruch-b.wav"] = a, b
    for name, x in d.items():
        b1.schreibe(ziel / name, x)
        print(name, f"{len(x) / SR:.3f} s")


if __name__ == "__main__":
    main()
