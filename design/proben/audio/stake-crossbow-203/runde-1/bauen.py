#!/usr/bin/env python3
"""Hörprobe Stake Crossbow (#203), Runde 1: Schuss, Spannen, Einschlag in je
drei Varianten A/B/C, dazu eine Vergleichsdatei hoerprobe.wav.

Nur Python-Standardbibliothek. Alles Synthese mit festem Seed, daher bitgleich
bei jedem Lauf. Aufruf:  python bauen.py [Zielordner]
Ohne Argument landen die Dateien neben dem Skript. Schreibt auch zeitmarken.md.

Rahmen (Audio Bible): trocken, nah, körperlich, kein Knall; keine hohen,
schrillen Töne (Tiefpass 2,8 kHz, Grundtöne unter 300 Hz); mono, 16 Bit,
44 100 Hz; Spitze an den Bestand angelehnt (Schuss -1,5 dBFS, Treffer -4,5,
Nachladen -8). Jede Datei läuft auf Stille aus (letzte 50 ms unter -50 dBFS).
Innerhalb einer Kategorie werden A/B/C auf gleiche mittlere Lautstärke
gebracht (nur nach unten gerechnet, die Spitze bleibt darunter).
"""
import math
import random
import struct
import sys
import wave
from pathlib import Path

SR = 44100
TAU = 2.0 * math.pi


def n(sek):
    return int(round(sek * SR))


# ---------- Filter und Bausteine ----------

def biquad(x, art, f, q=0.707):
    w = TAU * f / SR
    cw, sw = math.cos(w), math.sin(w)
    al = sw / (2.0 * q)
    if art == "tief":
        b0, b1, b2 = (1 - cw) / 2, 1 - cw, (1 - cw) / 2
    elif art == "hoch":
        b0, b1, b2 = (1 + cw) / 2, -(1 + cw), (1 + cw) / 2
    else:  # band, Spitzenverstärkung 1
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


def norm(x):
    s = max(abs(v) for v in x) or 1.0
    return [v / s for v in x]


def mal(x, g):
    return [v * g for v in x]


def versetze(x, sek):
    return [0.0] * n(sek) + x


def mische(*spuren):
    laenge = max(len(s) for s in spuren)
    out = [0.0] * laenge
    for s in spuren:
        for i, v in enumerate(s):
            out[i] += v
    return out


def stufig_band(x, f0, f1, q, stufen=5):
    """Bandpass, dessen Mitte über die Länge von f0 nach f1 fährt (Überblendung
    zwischen festen Bandpässen, daher ohne Knackser)."""
    out = [0.0] * len(x)
    letzt = len(x) - 1 or 1
    for s in range(stufen):
        fc = f0 + (f1 - f0) * s / (stufen - 1)
        y = biquad(x, "band", fc, q)
        for i in range(len(x)):
            w = 1.0 - abs(i / letzt * (stufen - 1) - s)
            if w > 0.0:
                out[i] += y[i] * w
    return out


def zischen(sek, rng, f0, f1, q, abfall, anstieg=0.004, halten=0.0):
    """Gefiltertes Rauschen (Pflock zischt, Holz reibt): Mitte fährt von f0 nach f1."""
    laenge = n(sek)
    x = stufig_band(rauschen(sek, rng), f0, f1, q)
    for i in range(laenge):
        t = i / SR
        if t < anstieg:
            a = 0.5 - 0.5 * math.cos(math.pi * t / anstieg)
        elif t < anstieg + halten:
            a = 1.0
        else:
            a = math.exp(-abfall * (t - anstieg - halten))
        x[i] *= a
    return norm(x)


def klopf(sek, f, abfall, start=1.7, fall=60.0, anstieg=0.0012):
    """Dumpfer Schlag: Sinus, dessen Tonhöhe anfangs höher liegt und schnell fällt."""
    ph = 0.0
    out = []
    for i in range(n(sek)):
        t = i / SR
        fr = f * (1.0 + (start - 1.0) * math.exp(-t * fall))
        ph += TAU * fr / SR
        out.append(math.sin(ph) * math.exp(-abfall * t) * min(1.0, t / anstieg))
    return out


def tonstoss(sek, rng, f, q, abfall):
    """Kurzer Anschlag aus Rauschen (Holz knackt): fester Bandpass, sehr kurz."""
    x = biquad(rauschen(sek, rng), "band", f, q)
    for i in range(len(x)):
        t = i / SR
        x[i] *= math.exp(-abfall * t) * min(1.0, t / 0.0008)
    return norm(x)


def schwingung(sek, f, abfall, teile, fall=30.0, start=1.04, tief=1400.0):
    """Gespannte Sehne oder Stahlbogen: Teiltöne (Verhältnis, Pegel, Zerfallsfaktor),
    Tonhöhe sinkt anfangs leicht (Spannung lässt nach). Phase wird aufsummiert."""
    ph = 0.0
    out = []
    for i in range(n(sek)):
        t = i / SR
        ph += TAU * f * (1.0 + (start - 1.0) * math.exp(-t * fall)) / SR
        s = 0.0
        for ver, pegel, zf in teile:
            s += pegel * math.sin(ver * ph) * math.exp(-abfall * zf * t)
        out.append(s * min(1.0, t / 0.0015))
    return biquad(out, "tief", tief)


def hall(x, nachlauf=0.12, nass=0.3):
    """Kurzer kalter Hall aus vier Kammfiltern, nur so lang wie nötig."""
    laenge = len(x) + n(nachlauf)
    x = x + [0.0] * (laenge - len(x))
    summe = [0.0] * laenge
    for verz, g in ((0.0297, 0.5), (0.0371, 0.45), (0.0411, 0.4), (0.0437, 0.35)):
        d = n(verz)
        y = list(x)
        for i in range(d, laenge):
            y[i] += g * y[i - d]
        for i in range(laenge):
            summe[i] += y[i] * 0.25
    ged = biquad(summe, "tief", 1600.0)
    return [a * 0.85 + b * nass for a, b in zip(x, ged)]


def abschluss(x, aus=0.15, ein=0.002):
    """Hochpass 30 Hz (kein Gleichanteil), Tiefpass 2,8 kHz (keine Höhen), Kosinus-Auslauf
    über die letzten `aus` Sekunden, kurzes Einblenden. Pegel stellt später `pegel` ein."""
    x = biquad(x, "hoch", 30.0)
    x = biquad(x, "tief", 2800.0)
    x = list(x)
    m = min(n(aus), len(x))
    for i in range(m):
        x[-1 - i] *= math.sin(0.5 * math.pi * i / m) ** 2
    for i in range(min(n(ein), len(x))):
        x[i] *= i / n(ein)
    return x


def pegel(x, spitze_db):
    s = max(abs(v) for v in x) or 1.0
    return mal(x, (10 ** (spitze_db / 20.0)) / s)


def rms(x):
    return math.sqrt(sum(v * v for v in x) / len(x))


def schreibe(pfad, x):
    daten = b"".join(struct.pack("<h", max(-32768, min(32767, int(round(v * 32767))))) for v in x)
    with wave.open(str(pfad), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(daten)


# ---------- Schuss: Sehne schnellt, Holz/Stahl schlägt an, Pflock zischt ab ----------

SEHNE = ((1.0, 1.0, 1.0), (2.0, 0.5, 1.4), (3.0, 0.3, 1.8), (4.0, 0.18, 2.2), (5.0, 0.1, 2.6))
STAHL = ((1.0, 1.0, 1.0), (2.32, 0.55, 1.5), (4.1, 0.3, 2.2), (6.6, 0.15, 3.0))


def schuss_a():
    """A, Sehnenschlag: tiefe Sehne (92 Hz, harmonische Teiltöne) schnellt mit
    hölzernem Anschlag, dahinter kurzes Zischen des Pflocks (1,4 kHz, fällt auf 0,9 kHz)."""
    rng = random.Random(2031)
    sehne = norm(schwingung(0.45, 92.0, 8.0, SEHNE, tief=1100.0))
    holz = norm(mische(klopf(0.14, 210.0, 38.0), mal(tonstoss(0.02, rng, 700.0, 1.2, 160.0), 0.5)))
    zisch = zischen(0.25, rng, 1400.0, 900.0, 0.9, 13.0, anstieg=0.006)
    m = mische(mal(sehne, 0.85), mal(holz, 0.9), versetze(mal(zisch, 0.5), 0.004))
    return abschluss(hall(m, 0.08), aus=0.2)


def schuss_b():
    """B, Holzschlag: trocken und schwer, der Armschlag aufs Holz (150 Hz) trägt alles,
    kaum Sehnenton (70 Hz, kurz), nur ein knappes tiefes Zischen (800 Hz)."""
    rng = random.Random(2032)
    holz = norm(mische(klopf(0.2, 150.0, 26.0, start=1.9), mal(norm(biquad(rauschen(0.03, rng), "tief", 500.0)), 0.7)))
    sehne = norm(schwingung(0.3, 70.0, 16.0, SEHNE[:3], tief=500.0))
    zisch = zischen(0.2, rng, 800.0, 600.0, 0.8, 20.0, anstieg=0.008)
    m = mische(mal(holz, 1.0), mal(sehne, 0.3), versetze(mal(zisch, 0.35), 0.01))
    return abschluss(hall(m, 0.08, 0.2), aus=0.15)


def schuss_c():
    """C, Stahlbogen: der Stahlbogen schwingt metallisch nach (118 Hz mit unharmonischen
    Teiltönen, bis 780 Hz), harter Anschlag (260 Hz), längeres Zischen (2 kHz -> 1,2 kHz)."""
    rng = random.Random(2033)
    bogen = norm(schwingung(0.48, 118.0, 6.0, STAHL, fall=20.0, start=1.02, tief=2200.0))
    anschlag = norm(mische(klopf(0.1, 260.0, 55.0), mal(tonstoss(0.015, rng, 1200.0, 1.5, 220.0), 0.6)))
    zisch = zischen(0.3, rng, 2000.0, 1200.0, 1.0, 9.0, anstieg=0.012)
    m = mische(mal(bogen, 0.75), mal(anschlag, 0.8), versetze(mal(zisch, 0.55), 0.006))
    return abschluss(hall(m, 0.08), aus=0.2)


# ---------- Spannen und Nachladen (vorläufige Länge, Nachladezeit grob 1 bis 1,5 s) ----------

def tick(rng, f, laut=1.0):
    """Ein Ratschenklick: kurzer Holzton plus Rauschkorn."""
    k = norm(mische(klopf(0.05, f, 90.0, start=1.5), mal(tonstoss(0.015, rng, 900.0, 1.5, 250.0), 0.7)))
    return mal(k, laut)


def rast(rng, schwer):
    """Sehne rastet ein: dumpfer Klack (Holz) mit kurzem Stahlring (schwer = lauter, tiefer)."""
    f = 150.0 if schwer else 200.0
    holz = klopf(0.12, f, 40.0, start=1.6)
    stahl = mal(schwingung(0.12, 430.0, 30.0, ((1.0, 1.0, 1.0), (2.76, 0.4, 1.6)), fall=10.0, start=1.0, tief=2000.0), 0.35)
    k = norm(mische(holz, stahl, mal(tonstoss(0.012, rng, 600.0, 1.5, 300.0), 0.5)))
    return mal(k, 1.0 if schwer else 0.8)


def einlegen(rng, f=240.0):
    """Pflock wird eingelegt: kurzes Gleiten über Holz, dann ein weicher Tock."""
    gleit = zischen(0.12, rng, 450.0, 600.0, 1.2, 14.0, anstieg=0.03)
    tock = klopf(0.1, f, 50.0, start=1.4)
    return mische(mal(gleit, 0.25), versetze(mal(norm(tock), 0.6), 0.09))


def knarren(sek, rng, f0, f1, tief, welle):
    """Anschwellendes Knarren: Rauschband, Pegel schwillt an und fällt, mit langsamer
    Rauigkeit (`welle` Hz, aus Rauschen geglättet, keine Vibrato-Phase)."""
    x = stufig_band(rauschen(sek, rng), f0, f1, 2.0)
    glatt = biquad(rauschen(sek, rng), "tief", welle)
    g = max(abs(v) for v in glatt) or 1.0
    laenge = len(x)
    for i in range(laenge):
        t = i / laenge
        huell = math.sin(math.pi * t ** 0.8) ** 1.5
        x[i] *= huell * (0.6 + 0.4 * glatt[i] / g)
    return norm(biquad(x, "tief", tief))


def spannen_a():
    """A, Kurbel (1,35 s): Ratsche mit 11 Klicks, die Abstände werden enger (78 -> 54 ms)
    und die Tonhöhe steigt (300 -> 480 Hz, Sehne spannt sich); dann schwerer Einrast-Klack,
    dann Pflock einlegen."""
    rng = random.Random(2034)
    spur = [0.0] * n(1.35)
    t = 0.04
    for k in range(11):
        f = 300.0 + 180.0 * k / 10.0
        c = tick(rng, f, 0.55 + 0.25 * k / 10.0)
        s0 = n(t)
        for i, v in enumerate(c):
            spur[s0 + i] += v
        t += 0.078 - 0.024 * k / 10.0
    r = rast(rng, True)
    s0 = n(t + 0.06)
    for i, v in enumerate(r):
        spur[s0 + i] += v
    e = einlegen(rng)
    s1 = n(t + 0.06 + 0.27)
    for i, v in enumerate(e):
        spur[s1 + i] += v * 0.9
    return abschluss(hall(spur, 0.1, 0.25), aus=0.2)


def spannen_b():
    """B, Hebel (1,1 s): ein langer Zug, Holz knarrt (220 -> 380 Hz) und die Sehne
    ächzt tief (70 -> 110 Hz, wächst an); Einrasten, Pflock einlegen."""
    rng = random.Random(2035)
    kn = knarren(0.72, rng, 220.0, 380.0, 900.0, 18.0)
    ph = 0.0
    laenge = n(0.72)
    achz = []
    for i in range(laenge):
        t = i / laenge
        ph += TAU * (70.0 + 40.0 * t) / SR
        s = sum(math.sin(k * ph) / k for k in range(1, 5))
        achz.append(s * math.sin(math.pi * t ** 0.9) ** 2)
    achz = norm(biquad(achz, "tief", 450.0))
    zug = mische(mal(kn, 0.7), mal(achz, 0.6))
    spur = mische(zug, versetze(mal(rast(rng, False), 1.1), 0.7), versetze(mal(einlegen(rng, 220.0), 0.9), 0.88))
    return abschluss(hall(spur, 0.1, 0.25), aus=0.2)


def spannen_c():
    """C, Zwei Stufen (1,25 s): zwei kurze Züge (je 0,28 s Knarren), nach dem ersten ein
    leises Rasten, nach dem zweiten ein schwerer Einrast-Klack; Pflock einlegen mit Schaben."""
    rng = random.Random(2036)
    z1 = knarren(0.28, rng, 260.0, 340.0, 800.0, 20.0)
    z2 = knarren(0.28, rng, 300.0, 420.0, 800.0, 20.0)
    r1 = mal(rast(rng, False), 0.6)
    r2 = rast(rng, True)
    schab = zischen(0.16, rng, 500.0, 350.0, 1.0, 9.0, anstieg=0.05)
    spur = mische(
        mal(z1, 0.7), versetze(r1, 0.26),
        versetze(mal(z2, 0.8), 0.42), versetze(r2, 0.68),
        versetze(mal(schab, 0.28), 0.88), versetze(mal(norm(klopf(0.1, 230.0, 50.0, start=1.4)), 0.6), 1.0),
    )
    return abschluss(hall(spur, 0.1, 0.25), aus=0.2)


# ---------- Einschlag ----------

def fleisch_a():
    """A, dumpfer Treffer (0,4 s): tiefer Schlag 110 -> 48 Hz, dazu nasses, tief gefiltertes Rauschen (350 Hz)."""
    rng = random.Random(2037)
    schlag = norm(klopf(0.4, 52.0, 16.0, start=2.1, fall=14.0, anstieg=0.002))
    nass = norm(biquad(biquad(rauschen(0.25, rng), "tief", 350.0), "tief", 350.0))
    nass = [v * math.exp(-24.0 * i / SR) for i, v in enumerate(nass)]
    return abschluss(hall(mische(schlag, mal(norm(nass), 0.7)), 0.06, 0.15), aus=0.18)


def fleisch_b():
    """B, Treffer mit Knochen (0,45 s): tiefer Schlag 85 -> 40 Hz, ein kurzes hölzernes Knacken
    (450 Hz, 12 ms) und ein leises Nachrauschen darunter."""
    rng = random.Random(2038)
    schlag = norm(klopf(0.45, 40.0, 12.0, start=2.1, fall=14.0, anstieg=0.002))
    knack = tonstoss(0.02, rng, 450.0, 1.0, 220.0)
    nach = norm(biquad(rauschen(0.3, rng), "tief", 250.0))
    nach = [v * math.exp(-15.0 * i / SR) for i, v in enumerate(nach)]
    return abschluss(hall(mische(schlag, mal(knack, 0.55), mal(nach, 0.35)), 0.06, 0.15), aus=0.2)


def fleisch_c():
    """C, Treffer nass (0,42 s): Rauschband fährt 500 -> 250 Hz mit grobem Schmatzen
    (Pegel springt unregelmäßig, 55 Hz Körnung), darunter ein runder Schlag (70 Hz)."""
    rng = random.Random(2039)
    schlag = norm(klopf(0.42, 70.0, 20.0, start=1.8, fall=25.0, anstieg=0.002))
    sch = stufig_band(rauschen(0.2, rng), 500.0, 250.0, 1.2)
    koern = biquad(rauschen(0.2, rng), "tief", 55.0)
    g = max(abs(v) for v in koern) or 1.0
    sch = [sch[i] * (0.4 + 0.6 * abs(koern[i]) / g) * math.exp(-16.0 * i / SR) * min(1.0, i / n(0.004)) for i in range(len(sch))]
    return abschluss(hall(mische(mal(schlag, 0.9), mal(norm(sch), 0.65)), 0.06, 0.15), aus=0.18)


def hart_a():
    """A, Holz steckt (0,5 s): kurzer Tock (190 Hz), danach zittert der Pflock im Holz aus
    (135 Hz, fällt gleichmäßig ab, kein Vibrato)."""
    rng = random.Random(2040)
    tock = norm(mische(klopf(0.1, 190.0, 45.0), mal(tonstoss(0.015, rng, 600.0, 1.2, 250.0), 0.6)))
    zitt = norm(schwingung(0.5, 135.0, 9.0, ((1.0, 1.0, 1.0), (2.0, 0.35, 1.6), (3.0, 0.15, 2.2)), fall=5.0, start=1.01, tief=1000.0))
    return abschluss(hall(mische(tock, mal(zitt, 0.45)), 0.06, 0.2), aus=0.2)


def hart_b():
    """B, Stein, Pflock prallt ab (0,45 s): hartes Klack (1,1 kHz Rauschen + 320 Hz Körper),
    danach ein kurzes Wegschleifen (Rauschband fällt 900 -> 450 Hz)."""
    rng = random.Random(2041)
    klack = norm(mische(klopf(0.08, 320.0, 60.0, start=1.4), mal(tonstoss(0.02, rng, 1100.0, 2.0, 200.0), 0.9)))
    schleif = zischen(0.22, rng, 900.0, 450.0, 1.2, 14.0, anstieg=0.012)
    return abschluss(hall(mische(klack, versetze(mal(schleif, 0.3), 0.025)), 0.1, 0.25), aus=0.2)


def hart_c():
    """C, Holz splittert (0,4 s): dumpfer Schlag (120 Hz), kurzes Reißen
    (Rauschband fährt 1,5 kHz -> 600 Hz, 90 ms), kein Zittern."""
    rng = random.Random(2042)
    schlag = norm(klopf(0.2, 120.0, 32.0, start=1.6))
    reiss = zischen(0.14, rng, 1500.0, 600.0, 1.5, 24.0, anstieg=0.006)
    return abschluss(hall(mische(schlag, versetze(mal(reiss, 0.5), 0.004)), 0.06, 0.15), aus=0.17)


# ---------- Kategorien, Pegel, Vergleichsdatei ----------

KATEGORIEN = [
    ("schuss", "Schuss", -1.5, [("a-sehnenschlag", schuss_a), ("b-holzschlag", schuss_b), ("c-stahlbogen", schuss_c)]),
    ("spannen", "Spannen und Nachladen", -8.0, [("a-kurbel", spannen_a), ("b-hebel", spannen_b), ("c-zwei-stufen", spannen_c)]),
    ("einschlag-fleisch", "Einschlag in Fleisch", -4.5, [("a-dumpf", fleisch_a), ("b-knochen", fleisch_b), ("c-nass", fleisch_c)]),
    ("einschlag-hart", "Einschlag in Stein oder Holz", -4.5, [("a-holz-steckt", hart_a), ("b-stein-prallt-ab", hart_b), ("c-holz-splittert", hart_c)]),
]

PAUSE_VARIANTE = 0.8
PAUSE_KATEGORIE = 1.6


def baue_kategorie(spitze_db, varianten):
    """Spitze auf Ziel, danach A/B/C auf gleiche mittlere Lautstärke (nach unten)."""
    roh = [pegel(fn(), spitze_db) for _, fn in varianten]
    ziel = min(rms(x) for x in roh)
    return [mal(x, ziel / rms(x)) for x in roh]


def main():
    ziel = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parent
    ziel.mkdir(parents=True, exist_ok=True)
    teile = {}
    for kuerzel, _, spitze, varianten in KATEGORIEN:
        for (name, _), x in zip(varianten, baue_kategorie(spitze, varianten)):
            datei = "%s-%s.wav" % (kuerzel, name)
            schreibe(ziel / datei, x)
            teile[(kuerzel, name[0])] = (datei, x)
            print(datei)

    # Im Zusammenhang: Spannen -> Schuss -> Einschlag (Fleisch), je Variante A/B/C
    zus = {}
    for buchst in "abc":
        s = teile[("spannen", buchst)][1]
        k = teile[("schuss", buchst)][1]
        f = teile[("einschlag-fleisch", buchst)][1]
        spur = list(s) + [0.0] * n(0.6) + list(k)
        spur += [0.0] * n(0.22) + list(f)
        datei = "zusammenhang-%s.wav" % buchst
        schreibe(ziel / datei, spur)
        zus[buchst] = (datei, spur)
        print(datei)

    # Vergleichsdatei: alle Kategorien A, B, C nacheinander, danach der Zusammenhang
    gesamt = []
    marken = []

    def anhaengen(titel, x, pause):
        marken.append((len(gesamt) / SR, len(x) / SR, titel))
        gesamt.extend(x)
        gesamt.extend([0.0] * n(pause))

    for kuerzel, titel, _, varianten in KATEGORIEN:
        for j, (name, _) in enumerate(varianten):
            x = teile[(kuerzel, name[0])][1]
            anhaengen("%s %s" % (titel, name[0].upper()), x, PAUSE_KATEGORIE if j == 2 else PAUSE_VARIANTE)
    for j, buchst in enumerate("abc"):
        anhaengen("Zusammenhang %s (Spannen, Schuss, Einschlag Fleisch)" % buchst.upper(), zus[buchst][1],
                  PAUSE_VARIANTE if j < 2 else 0.5)
    schreibe(ziel / "hoerprobe.wav", gesamt)
    zeilen = ["| Start s | Länge s | Inhalt |", "|---|---|---|"]
    for start, laenge, titel in marken:
        zeilen.append("| %.2f | %.2f | %s |" % (start, laenge, titel))
    (ziel / "zeitmarken.md").write_text("\n".join(zeilen) + "\n", encoding="utf-8")
    print("hoerprobe.wav %.2f s" % (len(gesamt) / SR))


if __name__ == "__main__":
    main()
