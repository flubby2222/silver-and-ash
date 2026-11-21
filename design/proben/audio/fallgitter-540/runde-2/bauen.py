#!/usr/bin/env python3
"""Hoerprobe Fallgitter, Runde 2 (#540): Eisen rutscht im Steinschlitz.

Urteil des Menschen zu Runde 1: Fallen "soll eher ein rutschendes Geraeusch
sein", Heben "welcher Hebel? benutzt doch keiner". Deutung: Das Gitter ist
schweres Eisen, das auf Stein gleitet; keine Kette, Winde oder Sperrklinke.

Nutzt die Helfer aus ../bauen.py. Aufruf: python bauen.py [Zielordner]
Fallen: nur Bewegung b (zwei Rucke). Dateien:
  fallen-b-rutschen-variante-1.wav  ganz ohne Kette
  fallen-b-rutschen-variante-2.wav  mit kaum hoerbarem Kettenrest
  heben-rutschen-variante-1.wav     rauer
  heben-rutschen-variante-2.wav     glatter
"""
import math
import random
import sys
from pathlib import Path

HIER = Path(__file__).resolve().parent
sys.path.insert(0, str(HIER.parent))
import bauen as B  # noqa: E402

SR, TAU, n, tp = B.SR, B.TAU, B.n, B.tp


def rutschen(rng, v, ab, bis, var, laut, rumpel, grain_pegel, fall):
    """Eisen gleitet auf Stein. Drei Schichten, alle vom Tempo v (0..1) gefuehrt:
    1. Gleitbett: Rauschen 100 bis 700 Hz, Pegel ~ v
    2. Koernung: kurze Rauschkoerner, Dichte (80 bis 480 je s) und Tonlage
       (Mitte 170 bis 520 Hz) steigen mit v - das ist die Reibung
    3. Rumpeln unter 150 Hz, Pegel ~ v.
    Variante 1 (rauer): mehr Koernung, Bett bis 800 Hz. Variante 2 (glatter):
    weniger Koernung, Bett bis 480 Hz."""
    L = len(v)
    rauh = var == 1
    bett = B.biquad(B.biquad(B.rauschen(L, rng), "hoch", 100.0), "tief", 700.0 if rauh else 480.0)
    rum = B.lp1(B.biquad(B.rauschen(L, rng), "tief", 150.0), 150.0)
    out = [0.0] * L
    for i in range(ab, min(bis, L)):
        s = v[i]
        out[i] = bett[i] * (s ** 0.8) * laut + rum[i] * s * rumpel * 4.0
    # Koernung
    phase = rng.random()
    i = ab
    while i < min(bis, L):
        s = v[i]
        phase += (80.0 + 400.0 * s) / SR
        if phase >= 1.0 and s > 0.02:
            phase -= 1.0
            f = (170.0 + 350.0 * s) * rng.uniform(0.75, 1.3)
            dauer = n(rng.uniform(0.008, 0.022))
            r = B.biquad(B.rauschen(dauer, rng), "band", f, 1.4)
            g = grain_pegel * (0.3 + 0.7 * s) * rng.uniform(0.4, 1.0)
            for j in range(dauer):
                w = math.sin(math.pi * j / dauer)
                if i + j < L:
                    out[i + j] += r[j] * w * g * 3.0
        elif phase >= 1.0:
            phase -= 1.0
        i += 1
    return B.biquad(B.biquad(out, "tief", 1400.0), "tief", 1400.0)


def stocken(buf, hall, pos, rng, laut, ha):
    """Kurzes Stocken: dumpfer Stoss, Eisen haengt fuer einen Moment (kein Klirren)."""
    ns = n(0.22)
    out = [0.0] * ns
    kn = B.lp1(B.lp1(B.rauschen(ns, rng), 700.0), 900.0)
    ph = 0.0
    for i in range(ns):
        t = i / SR
        ph += B.TAU * (52.0 + 30.0 * math.exp(-t * 40.0)) / SR
        out[i] = 0.9 * math.sin(ph + 1.5707963) * math.exp(-22.0 * t) + 1.5 * kn[i] * math.exp(-60.0 * t)
    B.addiere(buf, pos, out, laut)
    B.addiere(hall, pos, out, laut * ha)


def fallen_b(h, var):
    rng = random.Random(540_400 + var)
    laenge = n(63 / 60.0 + 0.70)
    dry = [0.0] * laenge
    send = [0.0] * laenge
    v = B.geschwindigkeit(h, laenge, +1, 0.28, bis_tick=47)
    sl = rutschen(rng, v, tp(0), tp(47), 1, 0.55, 0.55, 0.9, True)
    B.addiere(dry, 0, sl)
    B.addiere(send, 0, sl, 0.25)
    if var == 2:
        # Kettenrest: sehr leise, tief, nur waehrend das Eisen laeuft
        B.rasseln(dry, send, rng, v, tp(0), tp(47), 6.0, 30.0, 0.012, 0.045, (170.0, 380.0), 70.0, 0.2)
    stocken(dry, send, tp(12), rng, 0.55, 0.5)
    stocken(dry, send, tp(26), rng, 0.30, 0.5)
    ha = 0.55
    B.aufschlag(dry, send, tp(47), rng, 1.0, 1, ha)
    B.aufschlag(dry, send, tp(58), rng, 0.17, 1, ha, klein=True)
    B.aufschlag(dry, send, tp(63), rng, 0.05, 1, ha, klein=True)
    nach = B.Hall(0.55, 0.38).bearbeite(send)
    return B.abschluss(dry, nach)


def mittel_db(x):
    return 10 * math.log10(sum(s * s for s in x) / len(x) + 1e-20)


def heben(h, var):
    rng = random.Random(540_500 + var)
    laenge = n(126 / 60.0 + 0.80)
    dry = [0.0] * laenge
    send = [0.0] * laenge
    v = B.geschwindigkeit(h, laenge, -1, 0.0298, bis_tick=126)
    if var == 1:
        sl = rutschen(rng, v, tp(0), tp(126), 1, 0.55, 0.55, 0.9, False)
    else:
        sl = rutschen(rng, v, tp(0), tp(126), 2, 0.60, 0.65, 0.35, False)
    B.addiere(dry, 0, sl)
    B.addiere(send, 0, sl, 0.15)
    # Rutschen endet mit dem Tempo; leises dumpfes Anschlagen im Tick 126
    aus = n(0.02)
    p = tp(126)
    for i in range(aus):
        dry[p - aus + i] *= 0.5 + 0.5 * math.cos(math.pi * i / aus)
    ns = n(0.30)
    kn = B.lp1(B.lp1(B.rauschen(ns, rng), 600.0), 800.0)
    out = []
    ph = 0.0
    for i in range(ns):
        t = i / SR
        ph += B.TAU * (50.0 + 30.0 * math.exp(-t * 40.0)) / SR
        out.append(math.sin(ph + 1.5707963) * math.exp(-24.0 * t) + 1.3 * kn[i] * math.exp(-55.0 * t))
    B.addiere(dry, p, out, 0.55)
    B.addiere(send, p, out, 0.55 * 0.4)
    nach = B.Hall(0.5, 0.40).bearbeite(send)
    m = [dry[i] + nach[i] for i in range(laenge)]
    m = B.biquad(m, "hoch", 28.0)
    m = B.biquad(B.biquad(m, "tief", 2400.0), "tief", 2400.0)
    aus = n(0.30)
    for i in range(aus):
        m[laenge - aus + i] *= 0.5 + 0.5 * math.cos(math.pi * i / aus)
    k = 10 ** ((-24.0 - mittel_db(m)) / 20.0)
    m = [s * k for s in m]
    return m


def main():
    ziel = Path(sys.argv[1]) if len(sys.argv) > 1 else HIER
    ziel.mkdir(parents=True, exist_ok=True)
    d = B.lade_zeitverlauf()
    for var in (1, 2):
        B.schreibe(ziel / f"fallen-b-rutschen-variante-{var}.wav", fallen_b(d["fallen-b"], var))
        B.schreibe(ziel / f"heben-rutschen-variante-{var}.wav", heben(d["heben"], var))


if __name__ == "__main__":
    main()
