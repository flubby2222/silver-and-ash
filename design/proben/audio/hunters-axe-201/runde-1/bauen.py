#!/usr/bin/env python3
"""Hörprobe Hunter's Axe (#201), Runde 1: Hieb, Aufladen, Treffer in Fleisch und
Treffer auf Stein/Holz in je drei Fassungen A/B/C, dazu je Fassung eine Datei
"zusammenhang" (Aufladen, Hieb, Treffer in Folge).

JEDE Fassung ist eine eigene WAV-Datei mit sprechendem Namen (keine Sammeldatei).
Nur Python-Standardbibliothek. Alles Synthese mit festem Seed, daher bitgleich
bei jedem Lauf. Aufruf:  python bauen.py [Zielordner]
Ohne Argument landen die Dateien neben dem Skript.

Rahmen (Audio Bible): trocken, nah, körperlich; keine hohen, schrillen Töne
(Tiefpass 2,8 kHz, alle Grundtöne unter 400 Hz); mono, 16 Bit, 44 100 Hz.
Schwerer als Klinge und Keule im Bestand: tiefere Lage (Schläge 40 bis 70 Hz),
längerer Schwung (0,75 s statt 0,5 s), Treffer mit Stiel-Nachklang. Spitzen:
Hieb -9, Aufladen -10, Treffer -4,5 dBFS (Bestand: Schwung -9, Treffer -4,5; die mittlere Lautstärke liegt höher, weil die Axt dicht in der Tiefe liegt).
Innerhalb einer Kategorie werden A/B/C auf gleiche mittlere Lautstärke gebracht
(nur nach unten gerechnet). Jede Datei läuft auf Stille aus (letzte 50 ms unter
-50 dBFS). Die Teile bis "Abschluss" stammen aus der Armbrust-Probe (#203).
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


# ---------- Bausteine der Axt ----------

STAHL = ((1.0, 1.0, 1.0), (2.32, 0.55, 1.5), (4.1, 0.3, 2.2), (6.6, 0.15, 3.0))
EISEN = ((1.0, 1.0, 1.0), (2.76, 0.5, 1.6), (5.4, 0.25, 2.4))


def schwung_huell(u, gipfel):
    """Weicher Schwung: steigt mit sin^2 bis zum Gipfel, fällt mit cos^2 auf null."""
    if u < gipfel:
        return math.sin(0.5 * math.pi * u / gipfel) ** 2
    return math.cos(0.5 * math.pi * (u - gipfel) / (1.0 - gipfel)) ** 2


def luftband(sek, rng, f0, f1, q, gipfel, stufen=6):
    """Rauschband, dessen Mitte von f0 nach f1 fährt, mit weichem Schwung-Pegel."""
    x = stufig_band(rauschen(sek, rng), f0, f1, q, stufen)
    laenge = len(x)
    return norm([x[i] * schwung_huell(i / laenge, gipfel) for i in range(laenge)])


def tiefton(sek, f0, f1, gipfel, teile=1, ein=1.0):
    """Tiefer Ton (Phase aufsummiert, kein Vibrato), Frequenz fährt linear f0 -> f1,
    Pegel mit Schwung-Hüllkurve; `teile` Obertöne mit 1/k."""
    laenge = n(sek)
    ph = 0.0
    out = []
    for i in range(laenge):
        u = i / laenge
        ph += TAU * (f0 + (f1 - f0) * u) / SR
        s = sum(math.sin(k * ph) / k for k in range(1, teile + 1))
        out.append(s * schwung_huell(u, gipfel) ** ein)
    return norm(out)


def zu(x, ziel):
    """Spur auf genau `ziel` Samples bringen (kürzen oder mit Stille auffüllen)."""
    return x[:ziel] + [0.0] * max(0, ziel - len(x))


# ---------- 1. Hieb durch die Luft (0,75 s, Gipfel bei 60 %) ----------

def hieb_a():
    """A, tiefes Rauschen (0,75 s): breites Luftband fährt von 520 auf 190 Hz, darunter ein
    Druckton 70 -> 48 Hz; beides schwillt langsam an und läuft weich aus. Kein Ton, nur Luft."""
    rng = random.Random(2011)
    luft = luftband(0.75, rng, 520.0, 190.0, 0.8, 0.6)
    druck = tiefton(0.75, 70.0, 48.0, 0.62, teile=2)
    return abschluss(mische(mal(luft, 0.9), mal(druck, 0.6)), aus=0.2)


def hieb_b():
    """B, Eisenkopf (0,8 s): der Eisenkopf summt metallisch mit (95 Hz, unharmonische Teiltöne
    bis 520 Hz, Tonhöhe fällt nach dem Gipfel wie beim Vorbeiziehen), dazu schmales Luftband
    (350 -> 250 Hz). Der Ton schwillt mit dem Schwung an und läuft ohne Ausklang aus."""
    rng = random.Random(2012)
    laenge = n(0.8)
    ph = 0.0
    kopf = []
    for i in range(laenge):
        u = i / laenge
        f = 95.0 * (1.0 + 0.1 * (1.0 - u) ** 2 - 0.12 * max(0.0, u - 0.55))
        ph += TAU * f / SR
        s = sum(p * math.sin(v * ph) * math.exp(-0.6 * zf * u) for v, p, zf in EISEN)
        kopf.append(s * schwung_huell(u, 0.55))
    kopf = norm(biquad(kopf, "tief", 900.0))
    luft = luftband(0.8, rng, 350.0, 250.0, 1.6, 0.55)
    return abschluss(mische(mal(kopf, 0.85), mal(luft, 0.6)), aus=0.22)


def hieb_c():
    """C, Luftdruck (0,6 s): kürzer und gedrungener, fast nur Tiefe. Druckwelle 48 -> 34 Hz
    mit Obertönen bis 150 Hz, dazu gedämpftes Rauschen unter 450 Hz; Gipfel früh (45 %),
    danach ein langer, weicher Abfall."""
    rng = random.Random(2013)
    druck = tiefton(0.6, 48.0, 34.0, 0.45, teile=3)
    luft = biquad(luftband(0.6, rng, 400.0, 220.0, 0.7, 0.45), "tief", 450.0)
    return abschluss(mische(mal(druck, 1.0), mal(norm(luft), 0.55)), aus=0.2)


# ---------- 2. Aufladen (Spannung 1,0 s, dann Einrasten; alles unter 400 Hz) ----------

def spann_huell(i, laenge, hoch=1.6):
    """Wächst bis zum Ende, ohne Sprung (Potenz von u)."""
    return (i / laenge) ** hoch


def klack_eisen(rng, f, schwer=1.0):
    """Einrasten: dumpfer Eisenschlag (f Hz) mit kurzem tiefem Metallring (2,3facher Teilton)."""
    schlag = klopf(0.14, f, 38.0, start=1.6)
    ring = mal(schwingung(0.3, f * 1.9, 11.0, STAHL[:3], fall=10.0, start=1.0, tief=1100.0), 0.45)
    korn = mal(tonstoss(0.014, rng, 500.0, 1.4, 260.0), 0.5)
    return mal(norm(mische(schlag, ring, korn)), schwer)


def aufladen_a():
    """A, Knarzen (1,45 s): Holz und Leder knarzen unter Zug (Rauschband 150 -> 320 Hz, rauh durch
    14-Hz-Körnung), darunter ein Brummen 62 -> 96 Hz; beides wächst bis 1,0 s und endet im
    Einrasten (Eisenschlag 150 Hz mit tiefem Ring)."""
    rng = random.Random(2021)
    zug = 1.0
    laenge = n(zug)
    kn = stufig_band(rauschen(zug, rng), 150.0, 320.0, 2.0)
    kor = biquad(rauschen(zug, rng), "tief", 14.0)
    g = max(abs(v) for v in kor) or 1.0
    kn = norm([kn[i] * spann_huell(i, laenge, 1.4) * (0.6 + 0.4 * kor[i] / g) for i in range(laenge)])
    brumm = tiefton(zug, 62.0, 96.0, 0.999, teile=4, ein=1.0)
    brumm = [brumm[i] * spann_huell(i, laenge, 1.3) for i in range(laenge)]
    # Zug fällt in den letzten 25 ms weich ab, das Einrasten setzt bei 1,0 s ein
    m = n(0.025)
    zugspur = mische(mal(kn, 0.8), mal(norm(brumm), 0.7))
    for i in range(m):
        zugspur[laenge - 1 - i] *= math.sin(0.5 * math.pi * i / m) ** 2
    spur = mische(zugspur, versetze(mal(klack_eisen(rng, 150.0), 1.3), zug))
    return abschluss(hall(spur, 0.1, 0.25), aus=0.25)


def aufladen_b():
    """B, Brummen (1,5 s): ein einziger, glatter Dröhnton, der steigt (48 -> 105 Hz, sechs Obertöne
    nach 1/k) und mit dem Quadrat der Zeit lauter wird; kein Rauschen. Einrasten: tiefer
    Glockenschlag (72 Hz mit fallender Tonhöhe) plus Eisenring."""
    rng = random.Random(2022)
    zug = 1.0
    laenge = n(zug)
    ph = 0.0
    dr = []
    for i in range(laenge):
        u = i / laenge
        ph += TAU * (48.0 + 57.0 * u ** 1.4) / SR
        dr.append(sum(math.sin(k * ph) / k for k in range(1, 7)) * u ** 2.0)
    dr = norm(biquad(dr, "tief", 650.0))
    m = n(0.025)
    for i in range(m):
        dr[laenge - 1 - i] *= math.sin(0.5 * math.pi * i / m) ** 2
    schlag = klopf(0.5, 72.0, 9.0, start=1.5, fall=18.0)
    ring = mal(schwingung(0.5, 190.0, 8.0, EISEN, fall=8.0, start=1.0, tief=1000.0), 0.5)
    rast = norm(mische(schlag, ring, mal(tonstoss(0.012, rng, 450.0, 1.2, 300.0), 0.4)))
    spur = mische(mal(dr, 0.9), versetze(mal(rast, 1.2), zug))
    return abschluss(hall(spur, 0.1, 0.25), aus=0.3)


def aufladen_c():
    """C, Ratsche (1,4 s): ein Puls aus dumpfen Holzschlägen, der schneller wird (Abstand 150 ms
    -> 42 ms) und höher (120 -> 260 Hz) und lauter; dazu ein leises Brummen 70 -> 100 Hz.
    Die Pulse verschmelzen zur Dauer, dann rastet ein schwerer Eisenschlag (140 Hz) ein."""
    rng = random.Random(2023)
    zug = 1.0
    spur = [0.0] * n(zug + 0.45)
    t = 0.02
    while t < zug - 0.03:
        u = t / zug
        f = 120.0 + 140.0 * u
        puls = norm(mische(klopf(0.06, f, 80.0, start=1.4), mal(tonstoss(0.014, rng, 520.0, 1.4, 260.0), 0.6)))
        s0 = n(t)
        g = 0.3 + 0.7 * u
        for i, v in enumerate(puls):
            spur[s0 + i] += v * g
        t += 0.15 - 0.108 * u
    brumm = tiefton(zug, 70.0, 100.0, 0.999, teile=3)
    laenge = n(zug)
    for i in range(laenge):
        spur[i] += brumm[i] * 0.35 * spann_huell(i, laenge, 1.2)
    for i, v in enumerate(klack_eisen(rng, 140.0, 1.3)):
        spur[n(zug) + i] += v
    return abschluss(hall(spur, 0.1, 0.25), aus=0.25)


# ---------- 3. Treffer in Fleisch (0,5 bis 0,6 s, wuchtig) ----------

def fleisch_a():
    """A, dumpf (0,5 s): ein sehr tiefer Schlag 90 -> 40 Hz (der ganze Körper der Axt),
    dazu nasses, tief gefiltertes Rauschen (320 Hz, doppelt) und ein kurzes Knirschen
    der Schneide (700 Hz, 12 ms)."""
    rng = random.Random(2031)
    schlag = norm(klopf(0.5, 40.0, 11.0, start=2.2, fall=12.0, anstieg=0.002))
    nass = norm(biquad(biquad(rauschen(0.3, rng), "tief", 320.0), "tief", 320.0))
    nass = [v * math.exp(-20.0 * i / SR) for i, v in enumerate(nass)]
    schneide = tonstoss(0.02, rng, 700.0, 1.0, 200.0)
    return abschluss(hall(mische(schlag, mal(norm(nass), 0.7), mal(schneide, 0.35)), 0.06, 0.15), aus=0.22)


def fleisch_b():
    """B, Knochen spaltet (0,6 s): tiefer Schlag (46 Hz), ein hölzernes Knacken (380 Hz, 25 ms,
    der Knochen bricht), danach dröhnt der Stiel kurz nach (125 Hz, fällt gleichmäßig)."""
    rng = random.Random(2032)
    schlag = norm(klopf(0.5, 46.0, 10.0, start=2.0, fall=14.0, anstieg=0.002))
    knack = norm(mische(tonstoss(0.03, rng, 380.0, 1.0, 130.0), mal(tonstoss(0.02, rng, 900.0, 1.2, 220.0), 0.4)))
    stiel = norm(schwingung(0.45, 125.0, 9.0, ((1.0, 1.0, 1.0), (2.0, 0.3, 1.6)), fall=6.0, start=1.01, tief=700.0))
    return abschluss(hall(mische(schlag, mal(knack, 0.7), versetze(mal(stiel, 0.3), 0.012)), 0.06, 0.15), aus=0.25)


def fleisch_c():
    """C, nass (0,5 s): Rauschband fährt 480 -> 220 Hz mit grobem Schmatzen (Pegel springt
    unregelmäßig, 45 Hz Körnung), darunter ein runder Schlag (55 Hz) und ein zweiter,
    langsamer Schwall Rauschen (250 Hz), der das Nasse nachziehen lässt."""
    rng = random.Random(2033)
    schlag = norm(klopf(0.5, 55.0, 14.0, start=2.0, fall=18.0, anstieg=0.002))
    sch = stufig_band(rauschen(0.25, rng), 480.0, 220.0, 1.2)
    koern = biquad(rauschen(0.25, rng), "tief", 45.0)
    g = max(abs(v) for v in koern) or 1.0
    sch = [sch[i] * (0.35 + 0.65 * abs(koern[i]) / g) * math.exp(-13.0 * i / SR) * min(1.0, i / n(0.004))
           for i in range(len(sch))]
    schwall = norm(biquad(rauschen(0.4, rng), "tief", 250.0))
    schwall = [v * math.sin(math.pi * min(1.0, i / n(0.4))) ** 2 * 0.5 for i, v in enumerate(schwall)]
    return abschluss(hall(mische(mal(schlag, 0.9), mal(norm(sch), 0.65), versetze(schwall, 0.05)), 0.06, 0.15), aus=0.22)


# ---------- 4. Treffer auf Stein oder Holz (Fehlschlag an der Wand, 0,5 bis 0,65 s) ----------

def hart_a():
    """A, Funkenschlag (0,6 s): Eisen schlägt auf Stein: harter Klang (210 Hz, unharmonische Teiltöne
    bis 1,4 kHz, kurz) mit tiefem Schlag darunter (80 Hz); danach rieselt eine Handvoll winziger
    Rauschkörner (1,1 bis 2,2 kHz, leise) über 0,2 s ab (Funken)."""
    rng = random.Random(2041)
    klang = norm(schwingung(0.3, 210.0, 16.0, STAHL, fall=14.0, start=1.0, tief=2200.0))
    schlag = norm(mische(klopf(0.18, 80.0, 28.0, start=1.8), mal(tonstoss(0.012, rng, 1300.0, 1.5, 230.0), 0.7)))
    funken = [0.0] * n(0.3)
    for _ in range(14):
        t = rng.uniform(0.01, 0.2)
        k = norm(tonstoss(0.008, rng, rng.uniform(1100.0, 2200.0), 2.0, 300.0))
        gain = (1.0 - t / 0.25) * rng.uniform(0.4, 1.0)
        for i, v in enumerate(k):
            funken[n(t) + i] += v * gain
    return abschluss(hall(mische(mal(klang, 0.7), mal(schlag, 1.0), versetze(mal(funken, 0.22), 0.004)), 0.1, 0.22), aus=0.22)


def hart_b():
    """B, Stiel prallt (0,65 s): die Axt trifft Holz oder Balken: dumpfer, breiter Schlag (100 Hz),
    kurzes Splittern (Rauschband 1,2 -> 500 Hz, 60 ms) und der Holzstiel dröhnt lange nach
    (150 Hz, mit Obertönen, fällt gleichmäßig, kein Vibrato)."""
    rng = random.Random(2042)
    schlag = norm(mische(klopf(0.25, 100.0, 24.0, start=1.7), mal(tonstoss(0.03, rng, 500.0, 1.0, 140.0), 0.6)))
    splitter = zischen(0.1, rng, 1200.0, 500.0, 1.4, 30.0, anstieg=0.004)
    stiel = norm(schwingung(0.55, 150.0, 7.0, ((1.0, 1.0, 1.0), (2.0, 0.4, 1.5), (3.0, 0.2, 2.0)), fall=5.0, start=1.01, tief=900.0))
    return abschluss(hall(mische(schlag, versetze(mal(splitter, 0.4), 0.004), mal(stiel, 0.5)), 0.08, 0.2), aus=0.25)


def hart_c():
    """C, Stein bricht (0,6 s): tiefer Schlag (60 Hz) mit hartem Anschlag, danach bröckelt es:
    gedämpftes Rauschen unter 1 kHz, dessen Pegel in groben Körnern (35 Hz, unregelmäßig)
    abfällt, ohne Ton."""
    rng = random.Random(2043)
    schlag = norm(mische(klopf(0.3, 60.0, 17.0, start=2.0, fall=16.0), mal(tonstoss(0.012, rng, 800.0, 1.2, 220.0), 0.8)))
    laenge = n(0.45)
    br = biquad(biquad(rauschen(0.45, rng), "tief", 1000.0), "hoch", 120.0)
    koern = biquad(rauschen(0.45, rng), "tief", 35.0)
    g = max(abs(v) for v in koern) or 1.0
    br = norm([br[i] * (0.15 + 0.85 * abs(koern[i]) / g) * math.exp(-9.0 * i / SR) * min(1.0, i / n(0.006))
               for i in range(laenge)])
    return abschluss(hall(mische(mal(schlag, 1.0), versetze(mal(br, 0.5), 0.01)), 0.08, 0.2), aus=0.25)


# ---------- Kategorien, Pegel, Dateien ----------

KATEGORIEN = [
    ("hieb", "Hieb durch die Luft", -9.0,
     [("a-tiefes-rauschen", hieb_a), ("b-eisenkopf", hieb_b), ("c-luftdruck", hieb_c)]),
    ("aufladen", "Aufladen", -10.0,
     [("a-knarzen", aufladen_a), ("b-brummen", aufladen_b), ("c-ratsche", aufladen_c)]),
    ("treffer-fleisch", "Treffer in Fleisch", -4.5,
     [("a-dumpf", fleisch_a), ("b-knochen", fleisch_b), ("c-nass", fleisch_c)]),
    ("treffer-hart", "Treffer auf Stein oder Holz", -4.5,
     [("a-funkenschlag", hart_a), ("b-stiel-prallt", hart_b), ("c-stein-broeckelt", hart_c)]),
]

# Zusammenhang: Aufladen, Pause, Hieb; der Treffer setzt auf dem Gipfel des Hiebs ein
PAUSE_NACH_AUFLADEN = 0.35
TREFFER_NACH_HIEBSTART = 0.45
SPITZE_ZUSAMMENHANG = -1.5
ZUSAMMENHANG_NAMEN = {"a": "tiefes-rauschen, knarzen, dumpf", "b": "eisenkopf, brummen, knochen", "c": "luftdruck, ratsche, nass"}


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
            teile[(kuerzel, name[0])] = x
            print(datei)
    for b in "abc":
        auf = teile[("aufladen", b)]
        hieb = teile[("hieb", b)]
        treffer = teile[("treffer-fleisch", b)]
        t_hieb = len(auf) / SR + PAUSE_NACH_AUFLADEN
        spur = list(auf) + [0.0] * n(PAUSE_NACH_AUFLADEN) + list(hieb)
        s0 = n(t_hieb + TREFFER_NACH_HIEBSTART)
        spur += [0.0] * max(0, s0 + len(treffer) - len(spur))
        for i, v in enumerate(treffer):
            spur[s0 + i] += v
        s = max(abs(v) for v in spur)
        lim = 10 ** (SPITZE_ZUSAMMENHANG / 20.0)
        if s > lim:
            spur = mal(spur, lim / s)
        datei = "zusammenhang-%s.wav" % b
        schreibe(ziel / datei, spur)
        print(datei)


if __name__ == "__main__":
    main()
