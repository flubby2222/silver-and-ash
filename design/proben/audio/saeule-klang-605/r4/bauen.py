#!/usr/bin/env python3
"""Saeule-Klang Runde 4 (#605): wie Runde 3, aber dumpfer ("besser. etwas dumpfer").

Urteil zu Runde 3: "besser. etwas dumpfer". (Urteil zu Runde 2: "klingt sehr hoelzern. wie bausteine die fallen. Das pattern koennte etwas chaotischer sein.")
Gegenueber Runde 3: nur Klangfarbe nach unten (Profile a/b/c unten), Muster und Seeds gleich. c = Variante a mit deutlich staerkerem Schritt.
(alt:
Gegenueber Runde 2:
- keine Moden mehr (keine Tonhoehe, kein hohles Ausklingen): jedes Ereignis ist ein breitbandiger
  Geraeuschstoss; Groesse bestimmt Band (gross = dunkel, 80 bis 900 Hz; klein = bis 3,2 kHz), Daempfung und Masse
  (tiefes Rauschen unter 150 Hz statt Sinus);
- Groessen heavy-tailed (wenige schwere, viele kleine), dazu dichtes Griess-Rauschen, sodass Stuecke verschmelzen;
- Zeitmuster: Poisson mit schwankender Rate plus Folgestoesse (Haeufungen, Pausen), Pegel/Laenge/Klangfarbe gestreut.
Nur Python-Standardbibliothek. Aufruf: python bauen.py [Zielordner]
"""
import math
import random
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
import bauen as b1  # noqa: E402

SR = b1.SR
TICK = b1.TICK
T_HIT = b1.BRUCH_AUFSCHLAG

# Klangprofile: Grenzfrequenzen nach unten, mehr Masse. c ist deutlich dumpfer.
PROFILE = {
    "ab": dict(hi_cap=1250.0, hi_k=1400.0, lo_k=1.0, lp_end=2600.0, masse=1.3, griess_hi=1300.0, mahlen_hi=1000.0),
    "c": dict(hi_cap=750.0, hi_k=850.0, lo_k=0.8, lp_end=1500.0, masse=1.6, griess_hi=800.0, mahlen_hi=700.0),
}
P = PROFILE["ab"]
ZIEL_MITTEL = {"treffer-stufe1": -21.7, "treffer-stufe2": -19.5, "bruch": -21.4}

TAPS = ((7.0, 0.30), (14.5, 0.24), (26.0, 0.18), (40.0, 0.12), (61.0, 0.08), (88.0, 0.05))


def ereignis(rng, s, hart=1.0):
    """Ein Steinstoss der Groesse s (0,1 klein bis 3 sehr gross): breitbandig, ohne Tonhoehe."""
    dauer = min(0.32, 0.012 + 0.07 * s)
    n = int(dauer * SR)
    hi = max(300.0, min(P["hi_cap"], P["hi_k"] / (s ** 0.55)) * rng.uniform(0.75, 1.25))
    lo = max(60.0, 480.0 * P["lo_k"] / (s ** 0.8)) * rng.uniform(0.7, 1.3)
    r = b1.rauschen(rng, n + 300)
    r = b1.highpass(b1.lowpass(r, hi, 3), lo)[150:150 + n]
    tau1 = (0.0025 + 0.012 * s) * rng.uniform(0.7, 1.4)
    tau2 = tau1 * rng.uniform(3.0, 6.0)  # Nachrasseln: zweite, langsamere Huelle
    mix = rng.uniform(0.15, 0.45)
    na = max(1, int(0.0004 * SR))
    h = [min(1.0, i / na) * ((1 - mix) * math.exp(-i / SR / tau1) + mix * math.exp(-i / SR / tau2)) for i in range(n)]
    out = [v * w * hart for v, w in zip(r, h)]
    if s > 0.8:  # Masse: tiefes Rauschen, keine Sinuswelle
        m = b1.lowpass(b1.rauschen(rng, n), rng.uniform(90, 150), 3)
        mt = 0.025 + 0.05 * s
        m = [v * math.exp(-i / SR / mt) for i, v in enumerate(m)]
        g = 4.5 * P["masse"] * min(1.0, (s - 0.8) / 1.2)
        out = [o + v * g for o, v in zip(out, m)]
    return out


def pegel(s):
    return 0.05 + 0.35 * s ** 1.3


def groesse(rng, mittel=-1.3, streu=0.95, maxs=2.0):
    return min(maxs, math.exp(rng.gauss(mittel, streu)))


def geroell(rng, dauer, rate_a, rate_e, mittel=-1.3, streu=0.95, maxs=2.0, haeufung=0.35, gain=1.0):
    """Chaotisches Nachstuerzen: Poisson mit schwankender Rate, Folgestoesse, Groessen heavy-tailed."""
    n = int(dauer * SR)
    out = [0.0] * n
    t = 0.0
    faktor, bis = 1.0, 0.0
    while t < dauer:
        if t >= bis:  # Rate wechselt unregelmaessig: Haeufungen und Pausen
            faktor = math.exp(rng.gauss(0, 0.9))
            bis = t + rng.uniform(0.03, 0.14)
        p = t / dauer
        rate = (rate_a + (rate_e - rate_a) * p) * faktor
        t += rng.expovariate(max(rate, 0.5))
        if t >= dauer:
            break
        folge = [(t, groesse(rng, mittel, streu, maxs))]
        while rng.random() < haeufung and len(folge) < 5:
            folge.append((folge[-1][0] + rng.uniform(0.003, 0.05), groesse(rng, mittel - 0.3, streu, maxs)))
        for tt, s in folge:
            if tt < dauer:
                b1.add(out, ereignis(rng, s), tt, gain * pegel(s) * rng.uniform(0.6, 1.4))
    return out


def griess(rng, dauer, lo=500, hi=3000, gain=0.1, huelle=None):
    """Dichtes Rieseln/Staub: Rauschen mit unregelmaessig schwankender Huelle (Zufallspfad)."""
    n = int(dauer * SR)
    r = b1.band(b1.rauschen(rng, n), lo, min(hi, P["griess_hi"]))
    h, wert, ziel, k = [], 0.5, 0.5, 0
    for i in range(n):
        if k <= 0:
            ziel = rng.uniform(0.1, 1.0) ** 2
            k = int(rng.uniform(0.004, 0.03) * SR)
        wert += (ziel - wert) * 0.01
        k -= 1
        h.append(wert * (huelle[i] if huelle else 1.0))
    return [v * w * gain for v, w in zip(r, h)]


def abfall(n, tau, attack=0.01):
    return [min(1.0, i / SR / attack) * math.exp(-i / SR / tau) for i in range(n)]


def reflexionen(x, taps, nachlauf):
    n = len(x) + int(nachlauf * SR)
    x = x + [0.0] * (n - len(x))
    out = list(x)
    for ms, g in taps:
        d = int(ms / 1000 * SR)
        dump = b1.lowpass(x, 2800 - 12 * ms, 1)
        for i in range(n - d):
            out[i + d] += dump[i] * g
    return out


def fertig(x, fade, dauer):
    x = b1.lowpass(x, P["lp_end"], 2)
    p = b1.spitze(x)
    x = [math.tanh(1.7 * v / p) for v in x]
    return b1.fertig(x, -3.0, dauer, fade)


# ---------- Treffer ----------
def treffer(seed, stufe, v):
    rng = random.Random(seed)
    x = [0.0] * int(0.9 * SR)
    if stufe == 1:
        if v == "a":  # ein schwerer Riss, kurzes dichtes Geroell
            b1.add(x, ereignis(rng, 1.5, 1.1), 0.0, 2.0)
            b1.add(x, geroell(rng, 0.16, 160, 40, -1.5, 0.8, 0.8, 0.4, 0.8), 0.015)
            b1.add(x, griess(rng, 0.2, 600, 2800, 0.05, abfall(int(0.2 * SR), 0.07)), 0.0)
        else:  # etwas leichter, loser: Hauptstoss und unregelmaessiges Nachrutschen in Haeufungen
            b1.add(x, ereignis(rng, 1.1, 1.0), 0.0, 1.8)
            b1.add(x, geroell(rng, 0.32, 55, 18, -1.1, 1.0, 1.0, 0.5, 0.85), 0.05)
            b1.add(x, griess(rng, 0.3, 500, 2600, 0.04, abfall(int(0.3 * SR), 0.1)), 0.04)
        ziel, fade = 0.45, 0.2
    else:
        if v == "a":  # schwerer Riss, dichtes kurzes Geroell, Staub
            b1.add(x, ereignis(rng, 2.3, 1.1), 0.0, 2.0)
            b1.add(x, ereignis(rng, 1.0, 1.0), 0.05, 0.5)
            b1.add(x, geroell(rng, 0.26, 190, 35, -1.3, 0.9, 0.9, 0.4, 0.9), 0.02)
            b1.add(x, griess(rng, 0.35, 500, 3000, 0.07, abfall(int(0.35 * SR), 0.1)), 0.0)
        else:  # Hauptstoss, dann laengeres, ungleichmaessiges Nachstuerzen mit Luecken
            b1.add(x, ereignis(rng, 1.9, 1.1), 0.0, 2.0)
            b1.add(x, geroell(rng, 0.46, 70, 14, -0.9, 1.0, 1.5, 0.55, 0.95), 0.05)
            b1.add(x, ereignis(rng, 1.2, 1.0), 0.29, 0.45)
            b1.add(x, griess(rng, 0.4, 450, 2600, 0.06, abfall(int(0.4 * SR), 0.14)), 0.05)
        ziel, fade = 0.6, 0.26
    x = reflexionen(x, TAPS, 0.05)
    return fertig(x, fade, ziel)


# ---------- Bruch ----------
def mahlen(rng, dauer, anfang, ende):
    """Knirschendes Mahlen: Rauschen 120 bis 1400 Hz, Huelle rauh (schnelle Zufallsspruenge), Gesamtanstieg."""
    n = int(dauer * SR)
    r = b1.band(b1.rauschen(rng, n), 120, P["mahlen_hi"])
    h, wert, ziel, k = [], 0.2, 0.2, 0
    for i in range(n):
        if k <= 0:
            ziel = rng.uniform(0.0, 1.0) ** 1.5
            k = int(rng.uniform(0.002, 0.02) * SR)
        wert += (ziel - wert) * 0.02
        k -= 1
        p = i / n
        h.append(wert * (anfang + (ende - anfang) * p ** 1.6))
    return [v * w for v, w in zip(r, h)]


def bruch(seed, v):
    rng = random.Random(seed)
    gesamt = 2.0
    x = [0.0] * int(gesamt * SR)
    # 0 bis 0,70 s: Reissen und Mahlen, steigend; Risse unregelmaessig
    b1.add(x, mahlen(rng, T_HIT, 0.04, 0.5), 0.0, 1.0 if v == "a" else 0.85)
    b1.add(x, geroell(rng, T_HIT, 6, 40, -1.6, 0.8, 0.7, 0.35, 0.7), 0.0)
    druck = b1.lowpass(b1.rauschen(rng, int(T_HIT * SR)), 140, 3)
    nk = len(druck)
    b1.add(x, [d * (0.05 + (i / nk) ** 2) for i, d in enumerate(druck)], 0.0, 2.0)
    risse = (0.09, 0.27, 0.43, 0.57) if v == "a" else (0.14, 0.2, 0.38, 0.52, 0.63)
    for k, tr in enumerate(risse):
        b1.add(x, ereignis(rng, rng.uniform(0.6, 1.3), 1.0), tr + rng.uniform(-0.02, 0.02), 0.45 + 0.1 * k)
    # 0,70 s: zwei grosse Stuecke kurz nacheinander
    if v == "a":
        b1.add(x, ereignis(rng, 2.9, 1.2), T_HIT, 3.4)
        b1.add(x, ereignis(rng, 2.3, 1.2), T_HIT + 0.055, 1.8)
        # kurzes dichtes Geroell
        b1.add(x, geroell(rng, 0.68, 150, 28, -1.3, 1.0, 1.2, 0.45, 0.5), T_HIT + 0.03)
        b1.add(x, griess(rng, 0.8, 450, 3000, 0.08, abfall(int(0.8 * SR), 0.4)), T_HIT)
    else:
        b1.add(x, ereignis(rng, 2.7, 1.2), T_HIT, 3.4)
        b1.add(x, ereignis(rng, 2.1, 1.2), T_HIT + 0.09, 1.7)
        # laengeres, unregelmaessiges Nachstuerzen mit Pausen und zwei spaeten schweren Stuecken
        b1.add(x, geroell(rng, 0.95, 70, 14, -1.0, 1.05, 1.2, 0.55, 0.5), T_HIT + 0.04)
        b1.add(x, ereignis(rng, 1.6, 1.0), T_HIT + 0.33, 1.0)
        b1.add(x, ereignis(rng, 1.3, 1.0), T_HIT + 0.58, 0.8)
        b1.add(x, griess(rng, 1.0, 400, 2800, 0.07, abfall(int(1.0 * SR), 0.3)), T_HIT)
    x = reflexionen(x, TAPS, 0.0)
    return fertig(x, 0.5, gesamt)


def main():
    ziel = Path(sys.argv[1]) if len(sys.argv) > 1 else HERE
    ziel.mkdir(parents=True, exist_ok=True)
    global P
    d = {}
    # je Klang drei Fassungen, auf gleiche mittlere Lautstaerke gebracht
    bau = {}
    for stufe in (1, 2):
        P = PROFILE["ab"]
        a, b = treffer(62000 + stufe, stufe, "a"), treffer(62100 + stufe, stufe, "b")
        P = PROFILE["c"]
        c = treffer(62000 + stufe, stufe, "a")
        bau[f"treffer-stufe{stufe}"] = (a, b, c)
    P = PROFILE["ab"]
    a, b = bruch(62201, "a"), bruch(62202, "b")
    P = PROFILE["c"]
    c = bruch(62201, "a")
    bau["bruch"] = (a, b, c)
    for name, (a, b, c) in bau.items():
        a, b, c = b1.angleichen([a, b, c])
        # Gesamtpegel naeher an Runde 2 (Mittel dBFS: Stufe 1 -21,7; Stufe 2 -19,5; Bruch -21,4), nur nach unten
        ziel_db = ZIEL_MITTEL[name]
        ist_db = 20 * math.log10(b1.rms(a))
        k = 10 ** (min(0.0, ziel_db - ist_db) / 20)
        a, b, c = [[v * k for v in x] for x in (a, b, c)]
        d[f"saeule-{name}-a.wav"], d[f"saeule-{name}-b.wav"], d[f"saeule-{name}-c.wav"] = a, b, c
    for name, x in d.items():
        b1.schreibe(ziel / name, x)
        print(name, f"{len(x) / SR:.3f} s")


if __name__ == "__main__":
    main()
