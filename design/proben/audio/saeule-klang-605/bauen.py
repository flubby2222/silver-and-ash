#!/usr/bin/env python3
"""Saeule-Klang (#605): Treffer Stufe 1 und 2, Bruch. Nur Python-Standardbibliothek.

Aufruf: python bauen.py [Zielordner]   (Standard: Ordner dieses Skripts)
Alles wird aus Rauschen und Sinus mit festem Seed gebaut, zwei Laeufe sind bitgleich.
"""
import math
import random
import struct
import sys
import wave
from pathlib import Path

SR = 44100
HERE = Path(__file__).resolve().parent
TICK = 1.0 / 60.0
BRUCH_AUFSCHLAG = 42 * TICK  # 0,70 s: Stuecke schlagen auf


# ---------- Grundbausteine ----------
def zeros(sek):
    return [0.0] * int(sek * SR)


def rauschen(rng, n):
    return [rng.uniform(-1, 1) for _ in range(n)]


def lowpass(x, fc, pole=2):
    """Einpol-Tiefpass, mehrfach hintereinander (je Pol 6 dB/Okt.)."""
    a = 1.0 - math.exp(-2 * math.pi * fc / SR)
    for _ in range(pole):
        y, s = [], 0.0
        for v in x:
            s += a * (v - s)
            y.append(s)
        x = y
    return x


def highpass(x, fc):
    lp = lowpass(x, fc, 1)
    return [v - l for v, l in zip(x, lp)]


def band(x, lo, hi):
    return highpass(lowpass(x, hi, 2), lo)


def add(ziel, quelle, start_sek, gain=1.0):
    i0 = int(start_sek * SR)
    for i, v in enumerate(quelle):
        j = i0 + i
        if 0 <= j < len(ziel):
            ziel[j] += v * gain


def huellkurve_exp(n, attack_s, tau_s):
    out = []
    na = max(1, int(attack_s * SR))
    for i in range(n):
        t = i / SR
        a = min(1.0, i / na)
        a = 0.5 - 0.5 * math.cos(math.pi * a)
        out.append(a * math.exp(-t / tau_s))
    return out


def mul(x, h):
    return [a * b for a, b in zip(x, h)]


def sinus_fall(n, f0, f1, tau_f, tau_a, attack_s=0.002):
    """Sinus mit fallender Tonhoehe (Phase aufsummiert) und exponentieller Huelle."""
    out, ph = [], 0.0
    na = max(1, int(attack_s * SR))
    for i in range(n):
        t = i / SR
        f = f1 + (f0 - f1) * math.exp(-t / tau_f)
        ph += 2 * math.pi * f / SR
        a = min(1.0, i / na)
        out.append(math.sin(ph) * math.exp(-t / tau_a) * (0.5 - 0.5 * math.cos(math.pi * a)))
    return out


def modus(n, f, tau, attack_s=0.001):
    """Gedaempfter Sinus: dumpfe Steinresonanz."""
    na = max(1, int(attack_s * SR))
    w = 2 * math.pi * f / SR
    return [math.sin(w * i) * math.exp(-i / SR / tau) * min(1.0, i / na) for i in range(n)]


def schlag(rng, dauer, f_bass, f_rausch, tau_bass, tau_rausch, modi, gain_rausch=1.0):
    """Ein dumpfer Steinschlag: Bass-Sinus, tiefgefiltertes Rauschen, ein paar Resonanzen."""
    n = int(dauer * SR)
    bass = sinus_fall(n, f_bass * 1.8, f_bass, 0.03, tau_bass)
    r = lowpass(rauschen(rng, n), f_rausch, 3)
    r = mul(r, huellkurve_exp(n, 0.001, tau_rausch))
    out = [b * 1.0 + c * gain_rausch * 3.0 for b, c in zip(bass, r)]
    for f, tau, g in modi:
        m = modus(n, f, tau)
        out = [o + v * g for o, v in zip(out, m)]
    return out


def broeckeln(rng, dauer, anfang_dichte, ende_dichte, fc=900, gain=0.25, tief=True):
    """Kleine Steinchen: einzelne kurze, dumpfe Kornstoesse, Dichte nimmt ab."""
    n = int(dauer * SR)
    out = [0.0] * n
    t = 0.0
    while t < dauer:
        p = t / dauer
        dichte = anfang_dichte + (ende_dichte - anfang_dichte) * p
        t += rng.expovariate(dichte)
        if t >= dauer:
            break
        kl = int(rng.uniform(0.006, 0.020) * SR)
        f = rng.uniform(fc * 0.4, fc) if tief else rng.uniform(fc * 0.6, fc * 1.2)
        k = lowpass(rauschen(rng, kl), f, 2)
        h = huellkurve_exp(kl, 0.0008, rng.uniform(0.003, 0.007))
        g = gain * rng.uniform(0.3, 1.0) * (1.0 - 0.6 * p)
        add(out, mul(k, h), t, g)
    return out


def hall(x, nachlauf_s=0.25, mix=0.22):
    """Kurzer kalter Raum: vier Kammfilter (11 bis 41 ms), dunkel gedaempft."""
    n = len(x) + int(nachlauf_s * SR)
    x = x + [0.0] * (n - len(x))
    summe = [0.0] * n
    for ms, fb in ((11.3, 0.62), (19.7, 0.58), (29.9, 0.55), (41.1, 0.5)):
        d = int(ms / 1000 * SR)
        buf = [0.0] * n
        lp = 0.0
        for i in range(n):
            ein = x[i] + (buf[i - d] if i >= d else 0.0) * fb
            lp += 0.35 * (ein - lp)  # Daempfung im Rueckweg
            buf[i] = lp if i >= d else ein
            summe[i] += buf[i]
    return [a + b * mix * 0.5 for a, b in zip(x, summe)]


def ausblenden(x, sek):
    n = int(sek * SR)
    for i in range(n):
        j = len(x) - 1 - i
        x[j] *= 0.5 - 0.5 * math.cos(math.pi * i / n)
    return x


def einblenden(x, sek=0.002):
    n = int(sek * SR)
    for i in range(min(n, len(x))):
        x[i] *= 0.5 - 0.5 * math.cos(math.pi * i / n)
    return x


def spitze(x):
    return max(abs(v) for v in x) or 1.0


def rms(x):
    return math.sqrt(sum(v * v for v in x) / len(x))


def fertig(x, spitze_db=-3.0, dauer=None, fade=0.12):
    """Auf Zielspitze, Ausblenden, Laenge."""
    x = list(x)
    if dauer:
        n = int(dauer * SR)
        x = x[:n] + [0.0] * max(0, n - len(x))
    ausblenden(x, fade)
    einblenden(x)
    g = 10 ** (spitze_db / 20) / spitze(x)
    return [v * g for v in x]


def schreibe(pfad, x):
    with wave.open(str(pfad), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", max(-32767, min(32767, int(round(v * 32767))))) for v in x))


# ---------- Treffer ----------
def treffer(seed, stufe, variante):
    rng = random.Random(seed)
    if stufe == 1:
        dauer = 0.40
        if variante == "a":  # kurzer, trockener Knall: hoehere Bassnote, wenig Rauschen
            s = schlag(rng, dauer, 95, 1100, 0.07, 0.035, [(170, 0.05, 0.35), (255, 0.035, 0.2)], 0.8)
        else:  # dumpfer, tiefer, etwas laenger, Gewicht im Bass
            s = schlag(rng, dauer, 68, 700, 0.12, 0.055, [(120, 0.09, 0.4), (205, 0.05, 0.2)], 0.7)
        fest = 0.2
    else:
        dauer = 0.55
        if variante == "a":  # schwerer Schlag, tiefer Bass, dahinter Steinchen
            s = schlag(rng, dauer, 55, 800, 0.20, 0.09, [(95, 0.14, 0.45), (160, 0.08, 0.3), (230, 0.05, 0.15)], 0.9)
            br = broeckeln(rng, 0.40, 90, 25, fc=800, gain=0.22)
            add(s, br, 0.07)
        else:  # zwei Gewichte: Hauptschlag, kurz danach ein nachrutschender Brocken, dichteres Broeckeln
            s = schlag(rng, dauer, 50, 600, 0.22, 0.08, [(88, 0.16, 0.5), (140, 0.09, 0.3)], 0.8)
            nach = schlag(rng, 0.25, 62, 500, 0.10, 0.05, [(110, 0.06, 0.3)], 0.6)
            add(s, nach, 0.11, 0.45)
            br = broeckeln(rng, 0.40, 140, 30, fc=700, gain=0.20)
            add(s, br, 0.06)
        fest = 0.28
    s = hall(s, 0.12, 0.18)
    s = lowpass(s, 4200, 1)  # nimmt letzte Schaerfe aus Kornstoessen
    return fertig(s, -3.0, dauer + 0.05, fest)


# ---------- Bruch ----------
def bruch(seed, variante):
    rng = random.Random(seed)
    gesamt = 2.0
    n = int(gesamt * SR)
    x = [0.0] * n
    t_hit = BRUCH_AUFSCHLAG

    # Phase 1 (0 bis 0,70 s): Knicken. Tiefes Knirschen und einzelne Risse, Pegel steigt zum Aufschlag.
    nk = int(t_hit * SR)
    knirsch = lowpass(rauschen(rng, nk), 380 if variante == "a" else 300, 3)
    # unregelmaessig stockende Huelle (Reibung), Gesamtanstieg
    h, ph = [], 0.0
    for i in range(nk):
        p = i / nk
        ph += 2 * math.pi * (7 + 9 * p) / SR
        wob = 0.55 + 0.45 * math.sin(ph) * math.sin(ph * 0.37 + 1.1)
        h.append((0.15 + 0.85 * p ** 1.5) * max(0.05, wob))
    knirsch = mul(knirsch, h)
    add(x, knirsch, 0.0, 1.6)
    # tiefer Zug: Last auf dem Stein (sinus steigt kaum)
    ton = sinus_fall(nk, 52, 46, 0.5, 5.0, 0.15)
    ton = mul(ton, [0.1 + 0.9 * (i / nk) ** 2 for i in range(nk)])
    add(x, ton, 0.0, 0.35)
    # Risse: kurze dumpfe Knacker (Tick 8, 20, 31 bei a; 12, 26, 36 bei b)
    ticks = (8, 20, 31) if variante == "a" else (12, 26, 36)
    for k, tk in enumerate(ticks):
        g = 0.35 + 0.25 * k
        r = schlag(rng, 0.09, 140 + 25 * k, 1200, 0.03, 0.012, [(210, 0.02, 0.25)], 1.2)
        add(x, r, tk * TICK, g)

    # Phase 2: Aufschlag bei 0,70 s
    if variante == "a":
        # ein schwerer Hauptschlag, drei Stuecke dicht dahinter
        s = schlag(rng, 0.9, 42, 650, 0.28, 0.14, [(78, 0.20, 0.55), (130, 0.12, 0.35), (205, 0.07, 0.2)], 1.0)
        add(x, s, t_hit, 1.25)
        for dt, g, f in ((0.035, 0.55, 58), (0.07, 0.4, 72), (0.12, 0.3, 66)):
            st = schlag(rng, 0.35, f, 700, 0.12, 0.06, [(110, 0.07, 0.3)], 0.9)
            add(x, st, t_hit + dt, g)
        riesel = broeckeln(rng, 0.62, 110, 12, fc=750, gain=0.30)
        add(x, riesel, t_hit + 0.10)
    else:
        # zerstreut: mehrere Brocken nacheinander, dichteres Poltern, laengeres Nachrieseln
        for dt, g, f, tau in ((-0.045, 0.45, 70, 0.10), (0.0, 1.0, 46, 0.22), (0.04, 0.5, 54, 0.14),
                              (0.09, 0.4, 64, 0.11), (0.16, 0.25, 76, 0.09)):
            st = schlag(rng, 0.8, f, 600, tau, tau * 0.5, [(f * 1.9, 0.12, 0.45), (f * 3.0, 0.07, 0.25)], 1.0)
            add(x, st, t_hit + dt, g)
        riesel = broeckeln(rng, 0.78, 170, 18, fc=650, gain=0.32)
        add(x, riesel, t_hit + 0.06)
        # dumpfes Rollen: tiefgefiltertes Rauschen ueber etwa 0,5 s
        roll = lowpass(rauschen(rng, int(0.5 * SR)), 260, 3)
        roll = mul(roll, huellkurve_exp(len(roll), 0.05, 0.22))
        add(x, roll, t_hit + 0.05, 0.6)

    x = hall(x, 0.2, 0.2)
    x = lowpass(x, 4200, 1)
    return fertig(x, -3.0, gesamt, 0.45)


# ---------- Lautheit angleichen (nach unten) ----------
def angleichen(paar):
    pegel = [rms(x) for x in paar]
    ziel = min(pegel)
    return [[v * ziel / p for v in x] for x, p in zip(paar, pegel)]


def main():
    ziel = Path(sys.argv[1]) if len(sys.argv) > 1 else HERE
    ziel.mkdir(parents=True, exist_ok=True)
    dateien = {}
    for stufe in (1, 2):
        a, b = angleichen([treffer(60500 + stufe, stufe, "a"), treffer(60600 + stufe, stufe, "b")])
        dateien[f"saeule-treffer-stufe{stufe}-a.wav"] = a
        dateien[f"saeule-treffer-stufe{stufe}-b.wav"] = b
    a, b = angleichen([bruch(60701, "a"), bruch(60702, "b")])
    dateien["saeule-bruch-a.wav"] = a
    dateien["saeule-bruch-b.wav"] = b
    for name, x in dateien.items():
        schreibe(ziel / name, x)
        print(name, f"{len(x) / SR:.3f} s")


if __name__ == "__main__":
    main()
