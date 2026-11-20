#!/usr/bin/env python3
"""Hoerprobe Brunnen "Well of Sirius" (#438): Wurf, Gluecksfall, Wasser-Schleife.

Nur Python-Standardbibliothek. Alles Synthese mit festem Seed, daher bitgleich
bei jedem Lauf. Aufruf:  python bauen.py [Zielordner]
Ohne Argument landen die Dateien neben dem Skript.

Rahmen: dunkel, keine hohen Toene (Grundtoene unter 900 Hz, Tiefpaesse bei
0,9 bis 2,4 kHz). Mono, 16 Bit, 44 100 Hz. Einzelklaenge Spitze -6 dBFS
(zwischen den Geraeuschen aus Paket 1 mit -7 bis -8 und dem Skillpunkt mit -3),
Schleifen Spitze -18 dBFS. Einzelklaenge laufen ueber Kosinus auf null aus
(Audio-Bible, Regel 7); Schleifen werden kreisfoermig gefiltert und sind an der
Naht stetig.
"""
import math
import random
import struct
import sys
import wave
from pathlib import Path

SR = 44100
TAU = 2.0 * math.pi
SPITZE_EINZEL_DB = -6.0
SPITZE_SCHLEIFE_DB = -18.0


def n(sek):
    return int(round(sek * SR))


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


def zirkulaer(x, *filter_):
    """Filtert eine Schleife so, dass Ende und Anfang stetig ineinander laufen:
    Signal dreimal hintereinander, filtern, mittleres Drittel nehmen."""
    laenge = len(x)
    y = x * 3
    for art, f, *q in filter_:
        y = biquad(y, art, f, *(q or [0.707]))
    return y[laenge:2 * laenge]


# ---------- Bausteine ----------

def kosinus_aus(x, sek):
    """Weiches Ausblenden (Kosinus-Quadrat) ueber die letzten `sek` Sekunden."""
    x = list(x)
    m = min(n(sek), len(x))
    for i in range(m):
        x[-1 - i] *= math.sin(0.5 * math.pi * i / m) ** 2
    return x


def mische(*spuren):
    laenge = max(len(s) for s in spuren)
    out = [0.0] * laenge
    for s in spuren:
        for i, v in enumerate(s):
            out[i] += v
    return out


def versetze(spur, sek):
    return [0.0] * n(sek) + spur


def skaliere(spur, g):
    return [v * g for v in spur]


def blubb(sek, f0, steig=0.9, abfall=22.0, anstieg=0.004):
    """Wasserblase: Sinus, dessen Tonhoehe waehrend des Ausklingens steigt."""
    laenge = n(sek)
    ph = 0.0
    out = []
    for i in range(laenge):
        t = i / SR
        f = f0 * (1.0 + steig * (1.0 - math.exp(-t * 30.0)))
        ph += TAU * f / SR
        a = math.exp(-abfall * t) * min(1.0, t / anstieg)
        out.append(math.sin(ph) * a)
    return out


def teiltoene(sek, f, teile, abfall, anstieg=0.001, tief=2400.0):
    """Metallisch-dumpfer Klang aus unharmonischen Teiltoenen (Verhaeltnis, Pegel, Abfall-Faktor)."""
    laenge = n(sek)
    out = [0.0] * laenge
    for ver, pegel, af in teile:
        fr = f * ver
        if fr > tief:
            continue
        for i in range(laenge):
            t = i / SR
            out[i] += pegel * math.sin(TAU * fr * t) * math.exp(-abfall * af * t) * min(1.0, t / anstieg)
    return biquad(out, "tief", tief)


def schlag(sek, f0, f1, abfall):
    """Tiefer Puls: Sinus mit fallender Tonhoehe."""
    laenge = n(sek)
    ph = 0.0
    out = []
    for i in range(laenge):
        t = i / SR
        f = f1 + (f0 - f1) * math.exp(-t * 14.0)
        ph += TAU * f / SR
        out.append(math.sin(ph) * math.exp(-abfall * t) * min(1.0, i / n(0.004)))
    return out


def rauschband(sek, rng, art_filter, huellkurve):
    roh = [rng.uniform(-1.0, 1.0) for _ in range(n(sek))]
    for art, f, *q in art_filter:
        roh = biquad(roh, art, f, *(q or [0.707]))
    return [v * huellkurve(i / SR) for i, v in enumerate(roh)]


def hall(x, nachlauf=0.35):
    """Kurzer kalter Hall aus vier Kammfiltern, nur so lang wie noetig."""
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


def abschluss(x, aus, ein=0.003):
    """Gleichanteil weg, auf Spitze -6 dBFS, kurz einblenden, `aus` Sekunden Kosinus auf null."""
    x = biquad(list(x), "hoch", 30.0)
    x = kosinus_aus(x, aus)
    spitze = max(abs(v) for v in x) or 1.0
    g = (10 ** (SPITZE_EINZEL_DB / 20.0)) / spitze
    x = [v * g for v in x]
    for i in range(min(n(ein), len(x))):
        x[i] *= i / n(ein)
    return x


def schleife_normieren(x):
    spitze = max(abs(v) for v in x) or 1.0
    g = (10 ** (SPITZE_SCHLEIFE_DB / 20.0)) / spitze
    return [v * g for v in x]


def schreibe(pfad, x):
    daten = b"".join(struct.pack("<h", max(-32768, min(32767, int(round(v * 32767))))) for v in x)
    with wave.open(str(pfad), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(daten)


# ---------- (1) Silber faellt ins Wasser (Ereignis: Wurf) ----------

def wurf_a():
    """A, Muenze plumpst: ein kurzes dumpfes Metall-Tick (Teiltoene 430 bis 1,5 kHz),
    dazu eine tiefe Wasserblase, die kurz danach aufsteigt. Zwei Schichten, kurzer Hall."""
    tick = teiltoene(0.5, 430.0, ((1.0, 1.0, 1.0), (2.13, 0.5, 1.4), (3.4, 0.25, 2.0)), abfall=14.0, tief=1800.0)
    bl = blubb(0.45, 150.0, steig=1.0, abfall=13.0)
    m = mische(tick, versetze(skaliere(bl, 1.3), 0.04))
    return abschluss(hall(m, 0.3), aus=0.28)


def wurf_b():
    """B, Platsch: ein weicher Wasserschlag aus Rauschen (Band um 500 Hz) mit tiefem
    Schlag darunter (90 auf 55 Hz). Kein Metall, nur Wasser. Hall etwas laenger."""
    rng = random.Random(4381)
    plat = rauschband(0.5, rng, (("band", 520.0, 0.7), ("tief", 1100.0)),
                      lambda t: min(1.0, t / 0.006) * math.exp(-12.0 * t))
    dumpf = schlag(0.5, 90.0, 55.0, 9.0)
    m = mische(skaliere(plat, 3.0), skaliere(dumpf, 0.9))
    return abschluss(hall(m, 0.5), aus=0.45)


def wurf_c():
    """C, Sinken: ein Metallton, der unter Wasser sinkt (Tonhoehe faellt von 640 auf 300 Hz,
    der Tiefpass schliesst sich von 1,8 kHz auf 300 Hz), am Ende eine kleine Blase. Laenger."""
    dauer = 1.1
    laenge = n(dauer)
    ph = 0.0
    roh = []
    for i in range(laenge):
        t = i / SR
        f = 300.0 + 340.0 * math.exp(-t * 2.2)
        ph += TAU * f / SR
        a = min(1.0, t / 0.006) * math.exp(-3.2 * t)
        roh.append((math.sin(ph) + 0.5 * math.sin(2.13 * ph) + 0.25 * math.sin(3.4 * ph)) * a)
    hell = biquad(roh, "tief", 1200.0)
    mitte = biquad(roh, "tief", 800.0)
    dunkel = biquad(roh, "tief", 300.0)
    ton = []
    for i in range(laenge):
        t = i / laenge
        wh = max(0.0, 1.0 - t * 3.5)
        wm = max(0.0, 1.0 - abs(t - 0.3) * 3.2)
        wd = min(1.0, t * 2.2)
        ton.append(hell[i] * wh + mitte[i] * wm + dunkel[i] * wd)
    bl = blubb(0.35, 120.0, steig=0.8, abfall=15.0)
    m = mische(ton, versetze(skaliere(bl, 0.5), 0.65))
    return abschluss(hall(m, 0.3), aus=0.6)


# ---------- (2) Der Brunnen gibt einen Gegenstand (Glueck) ----------

def glueck_a():
    """A, Funkeln steigt: viele leise Sinus-Koernchen (Moll-Pentatonik 294 bis 880 Hz),
    deren Tonhoehe und Dichte steigen, darunter ein warmer tiefer Ton (147 Hz), der anschwillt."""
    rng = random.Random(4382)
    dauer = 2.4
    laenge = n(dauer)
    raster = [294.0, 349.0, 392.0, 440.0, 523.0, 587.0, 698.0, 784.0, 880.0]
    koernchen = [0.0] * laenge
    for k in range(34):
        t0 = (k / 34.0) ** 1.4 * 1.5
        idx = min(len(raster) - 1, int(t0 / 1.5 * (len(raster) - 1) + rng.uniform(-1.0, 1.5)))
        f = raster[max(0, idx)]
        dauer_k = rng.uniform(0.08, 0.2)
        pegel = rng.uniform(0.4, 1.0)
        s0 = n(t0)
        nk = n(dauer_k)
        for i in range(nk):
            koernchen[s0 + i] += pegel * math.sin(TAU * f * i / SR) * math.sin(math.pi * i / nk) ** 2
    ton = teiltoene(dauer, 147.0, ((1.0, 1.0, 1.0), (2.0, 0.3, 1.2)), abfall=1.6, anstieg=0.5, tief=700.0)
    m = mische(skaliere(koernchen, 0.45), skaliere(ton, 0.8))
    return abschluss(hall(biquad(m, "tief", 1500.0), 0.3), aus=1.2)


def glueck_b():
    """B, Glocke aus der Tiefe: drei tiefe Blasen steigen auf, im selben Atemzug schlaegt
    eine dunkle Glocke an (196 Hz, unharmonische Teiltoene) und klingt lang aus."""
    bl = mische(blubb(0.3, 110.0, 1.0, 14.0),
                versetze(blubb(0.3, 140.0, 1.0, 14.0), 0.07),
                versetze(blubb(0.3, 175.0, 1.0, 14.0), 0.14))
    glocke = teiltoene(2.4, 196.0, ((1.0, 1.0, 1.0), (2.76, 0.4, 1.6), (5.4, 0.14, 2.5)), abfall=2.0, anstieg=0.04, tief=2400.0)
    m = mische(skaliere(bl, 0.6), versetze(glocke, 0.12))
    return abschluss(hall(m, 0.5), aus=1.5)


def glueck_c():
    """C, Aufquellen: Blasen werden schneller und steigen in der Tonhoehe, dann schwillt ein
    weicher tiefer Ton (110 auf 165 Hz) an und klingt aus. Kein Metall, kein Glimmen."""
    rng = random.Random(4383)
    dauer = 2.2
    laenge = n(dauer)
    blasen = [0.0] * laenge
    t = 0.0
    luecke = 0.14
    while t < 1.0:
        f = 110.0 + 260.0 * t
        b = blubb(rng.uniform(0.05, 0.1), f * rng.uniform(0.9, 1.1), 0.9, 40.0)
        s0 = n(t)
        for i, v in enumerate(b):
            blasen[s0 + i] += v * (0.4 + 0.6 * t)
        luecke = max(0.03, luecke * 0.84)
        t += luecke
    anschwellen = []
    ph = 0.0
    for i in range(laenge):
        ts = i / SR
        f = 110.0 + 55.0 * min(1.0, ts / 1.2)
        ph += TAU * f / SR
        a = (0.5 - 0.5 * math.cos(math.pi * min(1.0, ts / 0.95))) * (1.0 if ts < 0.95 else math.exp(-2.4 * (ts - 0.95)))
        anschwellen.append((math.sin(ph) + 0.4 * math.sin(2 * ph) + 0.15 * math.sin(3 * ph)) * a)
    ton = biquad(anschwellen, "tief", 600.0)
    m = mische(skaliere(blasen, 0.9), skaliere(ton, 1.0))
    return abschluss(hall(m, 0.4), aus=1.2)


# ---------- (3) Wasser in der Naehe: Schleifen von 12 s ----------

SCHLEIFE = 12.0


def schleife_a():
    """A, Rieseln: ein stetiges dunkles Rauschen (150 bis 900 Hz), dessen Pegel langsam
    und ungleichmaessig atmet (zwei Schwingungen mit ganzen Perioden je Schleife). Keine Einzelereignisse."""
    rng = random.Random(4384)
    roh = [rng.uniform(-1.0, 1.0) for _ in range(n(SCHLEIFE))]
    y = zirkulaer(roh, ("hoch", 150.0), ("tief", 900.0), ("tief", 900.0))
    laenge = len(y)
    for i in range(laenge):
        t = i / SR / SCHLEIFE
        y[i] *= 1.0 + 0.22 * math.sin(TAU * 5.0 * t + 1.0) + 0.15 * math.sin(TAU * 13.0 * t + 2.5)
    return schleife_normieren(y)


def _tropfen_spur(rng, anzahl, f_von, f_bis, laenge_s, abfall, steig):
    laenge = n(SCHLEIFE)
    out = [0.0] * laenge
    for _ in range(anzahl):
        b = blubb(laenge_s, rng.uniform(f_von, f_bis), steig, abfall)
        s0 = rng.randrange(laenge)
        pegel = rng.uniform(0.35, 1.0)
        for i, v in enumerate(b):
            out[(s0 + i) % laenge] += v * pegel
    return out


def schleife_b():
    """B, Tropfen im Becken: ein sehr leises dunkles Rauschbett (unter 400 Hz) und 15 einzelne
    tiefe Tropfen (150 bis 420 Hz) an festen Stellen, deren Ausklang ueber die Naht herumlaeuft.
    Enthaelt Einzelereignisse; ohne sie siehe Fassung `-ohne-tropfen`."""
    rng = random.Random(4385)
    bett = [rng.uniform(-1.0, 1.0) for _ in range(n(SCHLEIFE))]
    bett = zirkulaer(bett, ("hoch", 80.0), ("tief", 400.0), ("tief", 400.0))
    bett = skaliere(bett, 0.5 / max(abs(v) for v in bett))
    tropfen = _tropfen_spur(rng, 15, 150.0, 420.0, 0.3, 17.0, 0.7)
    return schleife_normieren(mische(skaliere(bett, 0.12), tropfen)), schleife_normieren(bett)


def schleife_c():
    """C, Gurgeln: dichtes Blubbern (etwa 28 kurze Blasen je Sekunde, 90 bis 320 Hz) ueber einem
    leisen dunklen Bett; wirkt wie Wasser, das in eine Schale laeuft. Keine einzelne Blase sticht heraus."""
    rng = random.Random(4386)
    bett = [rng.uniform(-1.0, 1.0) for _ in range(n(SCHLEIFE))]
    bett = zirkulaer(bett, ("hoch", 100.0), ("tief", 600.0), ("tief", 600.0))
    bett = skaliere(bett, 0.5 / max(abs(v) for v in bett))
    laenge = n(SCHLEIFE)
    blasen = [0.0] * laenge
    for _ in range(int(28 * SCHLEIFE)):
        b = blubb(rng.uniform(0.03, 0.09), rng.uniform(90.0, 320.0), 0.9, 45.0)
        s0 = rng.randrange(laenge)
        pegel = rng.uniform(0.2, 0.6)
        for i, v in enumerate(b):
            blasen[(s0 + i) % laenge] += v * pegel
    return schleife_normieren(mische(skaliere(bett, 0.25), blasen))


EINZEL = {
    "wurf-a-muenze-plumpst.wav": wurf_a,
    "wurf-b-platsch.wav": wurf_b,
    "wurf-c-sinken.wav": wurf_c,
    "glueck-a-funkeln-steigt.wav": glueck_a,
    "glueck-b-glocke-aus-der-tiefe.wav": glueck_b,
    "glueck-c-aufquellen.wav": glueck_c,
    "wasser-a-rieseln.wav": schleife_a,
    "wasser-c-gurgeln.wav": schleife_c,
}


def main():
    ziel = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parent
    ziel.mkdir(parents=True, exist_ok=True)
    for name, fn in EINZEL.items():
        schreibe(ziel / name, fn())
        print(name)
    mit, ohne = schleife_b()
    schreibe(ziel / "wasser-b-tropfen-im-becken.wav", mit)
    schreibe(ziel / "wasser-b-tropfen-im-becken-ohne-tropfen.wav", ohne)
    print("wasser-b-tropfen-im-becken.wav")
    print("wasser-b-tropfen-im-becken-ohne-tropfen.wav")


if __name__ == "__main__":
    main()
