#!/usr/bin/env python3
"""Hoerprobe grosses Tor, Runde 2 (#549).

Urteil des Menschen zu Runde 1: Schliessen "Grollen beim oeffnen. Grollen, dann
schlag beim schliessen." Oeffnen "Neu machen. Soll sich wie ein schleifen auf
geroell anhoeren." Deutung: Kein Holzknarren mehr (passt nicht zum Steinportal).
Beide Bewegungen tragen ein tiefes Grollen; das Oeffnen ist koerniges, rollendes
Schleifen auf Steinschutt; das Schliessen ist Grollen im Anlauf, dann der Schlag.

Nutzt die Bausteine aus bauen.py. Zeiten sind Parameter wie in Runde 1:
  python bauen_r2.py --anschlag 78 --oeffnen 132 --anlauf 30 --auslauf 40 [--ziel ORDNER]
Dateien:
  r2-tor-schliessen-1-grollen-kurzer-schlag.wav
  r2-tor-schliessen-2-starkes-grollen-langer-nachklang.wav
  r2-tor-schliessen-3-grollen-mit-schutt-rieseln.wav
  r2-tor-oeffnen-1-geroell-fein.wav
  r2-tor-oeffnen-2-geroell-grob.wav
  r2-tor-oeffnen-3-geroell-mit-starkem-grollen.wav
"""
import argparse
import math
import random
from pathlib import Path

import bauen as B

SR, TAU, n, tp = B.SR, B.TAU, B.n, B.tp


def geroell(v, rng, ab, bis, dichte_max, f_lo, f_hi, d_lo, d_hi, pegel, q=1.4):
    """Koerniges Schleifen auf Schutt: kurze Rauschkoerner (Bandpass f_lo..f_hi, Dauer d_lo..d_hi s),
    Dichte und Tonlage steigen mit dem Tempo v. Je Korn ein eigener Pegel und eine eigene Tonlage."""
    L = len(v)
    out = [0.0] * L
    phase = rng.random()
    for i in range(ab, min(bis, L)):
        s = v[i]
        if s <= 0.02:
            continue
        phase += dichte_max * (0.2 + 0.8 * s) / SR
        if phase < 1.0:
            continue
        phase -= 1.0
        f = (f_lo + (f_hi - f_lo) * rng.random() ** 1.5) * (0.8 + 0.4 * s)
        d = n(rng.uniform(d_lo, d_hi))
        r = B.biquad(B.rauschen(d, rng), "band", f, q)
        g = pegel * (0.25 + 0.75 * s) * rng.uniform(0.3, 1.0)
        for j in range(d):
            if i + j >= L:
                break
            out[i + j] += r[j] * math.sin(math.pi * j / d) * g
    return out


def kollern(v, rng, ab, bis, rate_max, pegel):
    """Einzelne schwere Brocken, die kollern: dumpfe Stoesse 60..150 Hz, Rate steigt mit dem Tempo."""
    L = len(v)
    out = [0.0] * L
    phase = rng.random()
    for i in range(ab, min(bis, L)):
        s = v[i]
        if s <= 0.05:
            continue
        phase += rate_max * s / SR
        if phase < 1.0:
            continue
        phase -= 1.0
        f = rng.uniform(60.0, 150.0)
        g = pegel * (0.4 + 0.6 * s) * rng.uniform(0.4, 1.0)
        for j in range(n(0.16)):
            if i + j >= L:
                break
            t = j / SR
            out[i + j] += math.sin(TAU * f * t) * math.exp(-28.0 * t) * g * min(1.0, j / 50.0)
    return out


def summe(*teile):
    L = len(teile[0])
    return [sum(t[i] for t in teile) for i in range(L)]


def schliessen_r2(var, anschlag_tick):
    rng = random.Random(549_300 + var)
    schwanz = {1: 0.9, 2: 2.8, 3: 2.4}[var]
    L = tp(anschlag_tick + 32) + n(schwanz)
    p = tp(anschlag_tick)
    dry = [0.0] * L
    send = [0.0] * L
    v = B.tempo_schliessen(L, anschlag_tick)
    groll_pegel = {1: 4.0, 2: 5.0, 3: 4.0}[var]
    groll = B.rollen(v, rng, 0, p, groll_pegel)
    # zweite, tiefere Schicht: Beben 30..90 Hz, wiegt langsam
    lang = B.glatt(L, rng, 4.0)
    tief = B.biquad(B.biquad(B.rauschen(L, rng), "hoch", 28.0), "tief", 90.0)
    tief = B.biquad(tief, "tief", 90.0)
    for i in range(p):
        tief[i] *= (v[i] ** 1.3) * (0.5 + 0.5 * lang[i]) * groll_pegel * 1.6
    for i in range(p, L):
        tief[i] = 0.0
    grit = geroell(v, rng, 0, p, {1: 110.0, 2: 160.0, 3: 110.0}[var], 200.0, 700.0, 0.008, 0.025,
                   {1: 0.30, 2: 0.45, 3: 0.30}[var])
    bew = summe(groll, tief, grit)
    vor = n(0.025)
    for i in range(vor):
        bew[p - vor + i] *= 1.0 - 0.9 * (i / vor)
    send_an = {1: 0.10, 2: 0.55, 3: 0.30}[var]
    B.addiere(dry, 0, bew, 1.0)
    B.addiere(send, 0, bew, send_an)
    laut_schlag = {1: 1.0, 2: 1.0, 3: 1.0}[var]
    for tick, laut, kurz in ((anschlag_tick, laut_schlag, False),
                             (anschlag_tick + 14, 0.20, True),
                             (anschlag_tick + 22, 0.06, True)):
        s = B.kern_schlag(rng, laut, kurz)
        B.addiere(dry, tp(tick), s, 1.0)
        B.addiere(send, tp(tick), s, send_an * 0.9)
    if var == 2:
        g = B.bodengrollen(rng, 0.5, 2.0)
        B.addiere(dry, p, g, 1.0)
        B.addiere(send, p, g, 0.25)
    if var == 3:
        # Schutt rieselt nach dem Schlag aus dem Portal: feine Koerner, Dichte faellt mit e^(-2,2 t)
        vr = [0.0] * L
        for i in range(p, L):
            vr[i] = math.exp(-2.2 * (i - p) / SR)
        r = geroell(vr, rng, p, L, 150.0, 250.0, 800.0, 0.006, 0.02, 0.30)
        B.addiere(dry, 0, r, 1.0)
        B.addiere(send, 0, r, send_an)
    rt, damp = {1: (0.45, 0.55), 2: (2.6, 0.45), 3: (1.0, 0.5)}[var]
    nach = B.hall(send, rt, damp, L, 1.0)
    m = [dry[i] + nach[i] for i in range(L)]
    m = B.veredeln(m)
    B.abschluss(m, 0.6 if var != 1 else 0.35)
    k = 10 ** ((B.PEGEL_SCHLIESSEN_SPITZE_DB - B.spitze_db(m)) / 20.0)
    return [s * k for s in m]


def oeffnen_r2(var, dauer, an, ab):
    rng = random.Random(549_400 + var)
    schwanz = {1: 0.8, 2: 1.0, 3: 2.0}[var]
    L = tp(dauer) + n(schwanz)
    e = tp(dauer)
    dry = [0.0] * L
    send = [0.0] * L
    v = B.tempo_oeffnen(L, dauer, an, ab)
    if var == 1:    # fein: dichte kleine Koerner, wenig Grollen
        g = geroell(v, rng, 0, e, 520.0, 260.0, 900.0, 0.005, 0.014, 0.55, q=1.2)
        bett = B.schleifen(v, rng, 0, e, 1.6)
        groll = B.rollen(v, rng, 0, e, 2.0)
        koll = [0.0] * L
    elif var == 2:  # grob: wenige grosse Brocken, tiefer
        g = geroell(v, rng, 0, e, 80.0, 150.0, 600.0, 0.018, 0.05, 1.1, q=1.0)
        bett = B.schleifen(v, rng, 0, e, 0.8)
        groll = B.rollen(v, rng, 0, e, 2.6)
        koll = kollern(v, rng, 0, e, 7.0, 0.9)
    else:           # mittel, starkes Grollen darunter
        g = geroell(v, rng, 0, e, 230.0, 200.0, 750.0, 0.008, 0.03, 0.7, q=1.1)
        bett = B.schleifen(v, rng, 0, e, 1.1)
        groll = B.rollen(v, rng, 0, e, 6.5)
        koll = kollern(v, rng, 0, e, 4.0, 0.7)
    lang = B.glatt(L, rng, 3.0)
    tief = B.biquad(B.biquad(B.rauschen(L, rng), "hoch", 28.0), "tief", 80.0)
    tief = B.biquad(tief, "tief", 80.0)
    pt = {1: 1.2, 2: 2.0, 3: 6.0}[var]
    for i in range(L):
        tief[i] *= (v[i] ** 1.2) * (0.5 + 0.5 * lang[i]) * pt
    bew = summe(g, bett, groll, tief, koll)
    send_an = {1: 0.12, 2: 0.25, 3: 0.45}[var]
    B.addiere(dry, 0, bew, 1.0)
    B.addiere(send, 0, bew, send_an)
    rt, damp = {1: (0.5, 0.55), 2: (0.8, 0.5), 3: (2.0, 0.45)}[var]
    nach = B.hall(send, rt, damp, L, 1.0)
    m = [dry[i] + nach[i] for i in range(L)]
    m = B.veredeln(m)
    B.abschluss(m, 0.5)
    k = 10 ** ((B.PEGEL_OEFFNEN_MITTEL_DB - B.mittel_db(m)) / 20.0)
    m = [s * k for s in m]
    if B.spitze_db(m) > B.PEGEL_OEFFNEN_SPITZE_MAX_DB:
        k = 10 ** ((B.PEGEL_OEFFNEN_SPITZE_MAX_DB - B.spitze_db(m)) / 20.0)
        m = [s * k for s in m]
    return m


S = {1: "grollen-kurzer-schlag", 2: "starkes-grollen-langer-nachklang", 3: "grollen-mit-schutt-rieseln"}
O = {1: "geroell-fein", 2: "geroell-grob", 3: "geroell-mit-starkem-grollen"}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--anschlag", type=int, default=78)
    ap.add_argument("--oeffnen", type=int, default=132)
    ap.add_argument("--anlauf", type=int, default=30)
    ap.add_argument("--auslauf", type=int, default=40)
    ap.add_argument("--ziel", default=str(Path(__file__).resolve().parent))
    a = ap.parse_args()
    ziel = Path(a.ziel)
    ziel.mkdir(parents=True, exist_ok=True)
    for var in (1, 2, 3):
        B.schreibe(ziel / f"r2-tor-schliessen-{var}-{S[var]}.wav", schliessen_r2(var, a.anschlag))
        B.schreibe(ziel / f"r2-tor-oeffnen-{var}-{O[var]}.wav", oeffnen_r2(var, a.oeffnen, a.anlauf, a.auslauf))


if __name__ == "__main__":
    main()
