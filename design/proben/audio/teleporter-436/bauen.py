#!/usr/bin/env python3
"""Hörprobe Teleporter (#476): Freischalten und Reise, je drei Varianten A/B/C.

Nur Python-Standardbibliothek. Alles Synthese mit festem Seed, daher
bitgleich bei jedem Lauf. Aufruf:  python bauen.py [Zielordner]
Ohne Argument landen die Dateien neben dem Skript.

Rahmen: dunkles Horn, Kristall erwacht mit Glimmen; keine hohen Töne
(Grundtöne unter 600 Hz, Tiefpass bei etwa 2,5 kHz). Mono, 16 Bit, 44 100 Hz,
Spitze -3 dBFS (wie der Skillpunkt-Klang im Spiel).
"""
import math
import random
import struct
import sys
import wave
from pathlib import Path

SR = 44100
SPITZE_DB = -3.0
TAU = 2.0 * math.pi


def n(sek):
    return int(round(sek * SR))


def leer(sek):
    return [0.0] * n(sek)


# ---------- Filter ----------

def biquad(x, art, f, q=0.707):
    w = TAU * f / SR
    cw, sw = math.cos(w), math.sin(w)
    al = sw / (2.0 * q)
    if art == "tief":
        b0, b1, b2 = (1 - cw) / 2, 1 - cw, (1 - cw) / 2
    elif art == "hoch":
        b0, b1, b2 = (1 + cw) / 2, -(1 + cw), (1 + cw) / 2
    else:  # band
        b0, b1, b2 = al, 0.0, -al
    a0, a1, a2 = 1 + al, -2 * cw, 1 - al
    b0, b1, b2, a1, a2 = b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0
    x1 = x2 = y1 = y2 = 0.0
    out = []
    for s in x:
        y = b0 * s + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1, y2, y1 = x1, s, y1, y
        out.append(y)
    return out


def rauschen(sek, rng):
    return [rng.uniform(-1.0, 1.0) for _ in range(n(sek))]


# ---------- Bausteine ----------

def huellkurve(laenge, anstieg, abfall_rate, halten=0.0):
    """Anstieg (s), optional Halten (s), danach exponentieller Abfall je Sekunde."""
    out = []
    for i in range(laenge):
        t = i / SR
        if t < anstieg:
            a = 0.5 - 0.5 * math.cos(math.pi * t / anstieg)
        elif t < anstieg + halten:
            a = 1.0
        else:
            a = math.exp(-abfall_rate * (t - anstieg - halten))
        out.append(a)
    return out


def horn(sek, f0, f1=None, anstieg=0.12, halten=0.2, abfall=5.0, oberton=7, tief=900.0):
    """Dunkles Horn: Sägezahn-ähnliche Teiltöne, Tiefpass, Tonhöhe darf gleiten.
    Phase wird aufsummiert (kein Vibrato, keine Schwebung)."""
    f1 = f0 if f1 is None else f1
    laenge = n(sek)
    env = huellkurve(laenge, anstieg, abfall, halten)
    ph = 0.0
    out = []
    for i in range(laenge):
        t = i / laenge
        f = f0 + (f1 - f0) * t
        ph += TAU * f / SR
        s = 0.0
        for k in range(1, oberton + 1):
            s += math.sin(k * ph) / k
        out.append(s * env[i])
    return biquad(biquad(out, "tief", tief), "tief", tief)


def kristall(sek, f, abfall=3.0, anstieg=0.02, teile=((1.0, 1.0), (2.76, 0.35), (5.4, 0.12)), tief=2200.0):
    """Glasig-dunkler Klang aus unharmonischen Teiltönen (Verhältnis, Pegel)."""
    laenge = n(sek)
    env = huellkurve(laenge, anstieg, abfall)
    out = [0.0] * laenge
    for ver, pegel in teile:
        fr = f * ver
        if fr > 2400.0:
            continue
        for i in range(laenge):
            out[i] += pegel * math.sin(TAU * fr * i / SR) * env[i] * math.exp(-0.6 * ver * i / SR)
    return biquad(out, "tief", tief)


def glimmen(sek, rng, mitte=(330.0, 700.0), dichte=14.0, laut=1.0, form=None):
    """Glimmen: viele leise, kurze Sinus-Körnchen (Tonhöhen aus einem
    Moll-Pentatonik-Raster, damit nichts schwebt), über die Zeit geformt."""
    raster = [f for f in (293.7, 349.2, 392.0, 440.0, 523.3, 587.3, 698.5) if mitte[0] <= f <= mitte[1]]
    laenge = n(sek)
    out = [0.0] * laenge
    anzahl = int(dichte * sek)
    for _ in range(anzahl):
        start = rng.random()
        if form is not None:
            start = form(start)
        f = rng.choice(raster)
        dauer = rng.uniform(0.06, 0.16)
        pegel = rng.uniform(0.3, 1.0) * laut
        s0 = int(start * (laenge - n(dauer) - 1))
        for i in range(n(dauer)):
            t = i / n(dauer)
            out[s0 + i] += pegel * math.sin(TAU * f * i / SR) * math.sin(math.pi * t) ** 2
    return out


def hall(x, nachlauf=0.35):
    """Kurzer kalter Hall aus vier Kammfiltern, nur so lang wie nötig."""
    laenge = len(x) + n(nachlauf)
    x = x + [0.0] * (laenge - len(x))
    summe = [0.0] * laenge
    for verz, g in ((0.0297, 0.55), (0.0371, 0.5), (0.0411, 0.45), (0.0437, 0.4)):
        d = n(verz)
        y = list(x)
        for i in range(d, laenge):
            y[i] += g * y[i - d]
        for i in range(laenge):
            summe[i] += y[i] * 0.25
    gedaempft = biquad(summe, "tief", 1800.0)
    return [a * 0.8 + b * 0.35 for a, b in zip(x, gedaempft)]


def mische(*spuren):
    laenge = max(len(s) for s in spuren)
    out = [0.0] * laenge
    for s in spuren:
        for i, v in enumerate(s):
            out[i] += v
    return out


def versetze(spur, sek):
    return [0.0] * n(sek) + spur


def schlag(sek, f0, f1, abfall):
    """Tiefer Puls: Sinus mit fallender Tonhöhe."""
    laenge = n(sek)
    ph = 0.0
    out = []
    for i in range(laenge):
        t = i / SR
        f = f1 + (f0 - f1) * math.exp(-t * 14.0)
        ph += TAU * f / SR
        out.append(math.sin(ph) * math.exp(-abfall * t) * min(1.0, i / n(0.004)))
    return out


def sog(sek, rng, von, bis, hoch=True, tief=1400.0, q=1.2):
    """Gefiltertes Rauschen mit Mittenfrequenz-Fahrt (blockweise), Pegel steigt oder fällt."""
    laenge = n(sek)
    roh = rauschen(sek, rng)
    block = 512
    out = []
    for b0 in range(0, laenge, block):
        t = b0 / laenge
        f = von + (bis - von) * t
        out.extend(biquad(roh[b0:b0 + block + 64], "band", f, q)[:block])
    out = out[:laenge]
    for i in range(laenge):
        t = i / laenge
        a = math.sin(math.pi * 0.5 * t) ** 2 if hoch else math.cos(math.pi * 0.5 * t) ** 2
        out[i] *= a
    return biquad(out, "tief", tief)


# ---------- Abschluss ----------

def abschluss(x, ein=0.004, aus=0.03):
    x = list(x)
    # Gleichanteil und Tiefenrumpeln unter 30 Hz weg, sonst knackt der Anfang
    x = biquad(x, "hoch", 30.0)
    spitze = max(abs(v) for v in x) or 1.0
    g = (10 ** (SPITZE_DB / 20.0)) / spitze
    x = [v * g for v in x]
    for i in range(min(n(ein), len(x))):
        x[i] *= i / n(ein)
    for i in range(min(n(aus), len(x))):
        x[-1 - i] *= i / n(aus)
    return x


def schreibe(pfad, x):
    daten = b"".join(struct.pack("<h", max(-32768, min(32767, int(round(v * 32767))))) for v in x)
    with wave.open(str(pfad), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(daten)


# ---------- Freischalten: der Kristall erwacht ----------

def freischalten_a():
    """A, Horn mit Glimmen: ein dunkler Hornstoß (98 Hz, wächst an), darüber
    ein Glimmen aus leisen Körnchen, das beim Ausklingen aufgeht."""
    rng = random.Random(4761)
    h = horn(1.3, 98.0, 98.0, anstieg=0.18, halten=0.25, abfall=3.4)
    g = glimmen(1.3, rng, dichte=16.0, laut=0.10, form=lambda s: 0.25 + 0.75 * s)
    return abschluss(hall(mische(h, g), 0.3))


def freischalten_b():
    """B, Kristallton: langsames Erwachen eines glasig-dunklen Tons (220 Hz mit
    unharmonischen Teiltönen), darunter ein leiser Hornton (73 Hz)."""
    h = horn(1.2, 73.4, 73.4, anstieg=0.3, halten=0.1, abfall=4.0)
    k = kristall(1.2, 220.0, abfall=2.6, anstieg=0.28)
    return abschluss(hall(mische([v * 0.45 for v in h], k), 0.35))


def freischalten_c():
    """C, Puls und Funken: ein tiefer Schlag (55 Hz) wie ein erwachendes Herz,
    danach ein kurzes Funkeln aus dichten Körnchen in der Tiefmitte."""
    rng = random.Random(4763)
    s = schlag(0.6, 90.0, 55.0, 6.0)
    g = glimmen(1.0, rng, dichte=34.0, laut=0.16, form=lambda t: t ** 1.6)
    return abschluss(hall(mische(s, versetze(g, 0.12)), 0.25))


# ---------- Reise: Abreise und Ankunft als Paar ----------

def reise_a_ab():
    """A, Horn-Gleiten: das Horn fällt von 196 auf 98 Hz, mit Sog aus Rauschen."""
    rng = random.Random(4764)
    h = horn(0.8, 196.0, 98.0, anstieg=0.25, halten=0.2, abfall=7.0)
    s = sog(0.8, rng, 400.0, 250.0, hoch=True)
    return abschluss(hall(mische(h, [v * 0.35 for v in s]), 0.2))


def reise_a_an():
    """A, Ankunft: kurzer Hornstoß steigt von 98 auf 131 Hz, tiefer Schlag darunter."""
    h = horn(0.8, 98.0, 131.0, anstieg=0.05, halten=0.12, abfall=6.5)
    s = schlag(0.5, 80.0, 49.0, 7.0)
    return abschluss(hall(mische(h, [v * 0.9 for v in s]), 0.3))


def reise_b_ab():
    """B, Kristall-Sog: ein Kristallton (165 Hz) schwillt an, Rauschen zieht
    zusammen (Mittenfrequenz fällt von 1500 auf 500 Hz), Ende abrupt gedämpft."""
    rng = random.Random(4765)
    k = kristall(0.8, 165.0, abfall=-1.6, anstieg=0.5)
    s = sog(0.8, rng, 1500.0, 500.0, hoch=True, tief=1800.0)
    return abschluss(mische(k, [v * 0.3 for v in s]), aus=0.06)


def reise_b_an():
    """B, Ankunft: weicher Kristallanschlag (196 Hz) klingt aus, dumpfer Schlag darunter."""
    k = kristall(0.9, 196.0, abfall=4.0, anstieg=0.006)
    s = schlag(0.4, 75.0, 52.0, 8.0)
    return abschluss(hall(mische(k, [v * 0.7 for v in s]), 0.3))


def reise_c_ab():
    """C, Dröhnen: ein tiefer Ton (61 Hz) schwillt an, darüber ein sanft
    ansteigendes Rauschen-Band; kein Hornton, nur Druck."""
    rng = random.Random(4766)
    h = horn(0.7, 61.0, 61.0, anstieg=0.55, halten=0.05, abfall=14.0, oberton=5, tief=500.0)
    s = sog(0.7, rng, 300.0, 900.0, hoch=True, tief=1200.0)
    return abschluss(mische(h, [v * 0.3 for v in s]), aus=0.05)


def reise_c_an():
    """C, Ankunft: tiefer Schlag (49 Hz) und ein kurzes Funkeln, das sich schnell verläuft."""
    rng = random.Random(4767)
    s = schlag(0.6, 85.0, 49.0, 6.0)
    g = glimmen(0.6, rng, dichte=28.0, laut=0.14, form=lambda t: t ** 0.8)
    return abschluss(hall(mische(s, versetze(g, 0.05)), 0.25))


DATEIEN = {
    "freischalten-a-horn-glimmen.wav": freischalten_a,
    "freischalten-b-kristallton.wav": freischalten_b,
    "freischalten-c-puls-funken.wav": freischalten_c,
    "reise-a-horn-gleiten-abreise.wav": reise_a_ab,
    "reise-a-horn-gleiten-ankunft.wav": reise_a_an,
    "reise-b-kristall-sog-abreise.wav": reise_b_ab,
    "reise-b-kristall-sog-ankunft.wav": reise_b_an,
    "reise-c-droehnen-abreise.wav": reise_c_ab,
    "reise-c-droehnen-ankunft.wav": reise_c_an,
}


def main():
    ziel = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parent
    ziel.mkdir(parents=True, exist_ok=True)
    for name, fn in DATEIEN.items():
        schreibe(ziel / name, fn())
        print(name)


if __name__ == "__main__":
    main()
