#!/usr/bin/env python3
"""Hörprobe Whip (Lederpeitsche, #202), Runde 1: Schlag (Ausholen und Knall), Treffer in
Fleisch, Treffer auf Stein/Holz, Aufladen und Heranziehen in je drei Fassungen A/B/C, dazu je
Fassung eine Datei "zusammenhang" (Schlag, dann Treffer in Fleisch derselben Fassung).

Angriffe laut Issue: Schlag mit langer Reichweite, Heranziehen (Ghule und Fledermäuse),
Aufladen (bricht rissige Wände).

JEDE Fassung ist eine eigene WAV-Datei mit sprechendem Namen (keine Sammeldatei).
Nur Python-Standardbibliothek, fester Seed, daher bitgleich bei jedem Lauf. Aufruf:
    python bauen.py [Zielordner]
Ohne Argument landen die Dateien neben dem Skript.

Rahmen (Audio Bible): trocken, nah, körperlich; mono, 16 Bit, 44 100 Hz. Ein Peitschenknall ist
von Natur aus hell; hier bleibt er kurz (Knallspitze 1 bis 4 ms) und die Energie über 4 kHz wird
begrenzt: jede Datei läuft durch einen Tiefpass 4. Ordnung bei 3,0 kHz (A) bzw. 2,4 kHz
(B, C und alles Dumpfe). Keine tonalen Schlussereignisse (kein "Ding"): Einrasten und Klopfen sind
tiefe Schläge unter 200 Hz oder Rauschen mit niedriger Güte. Jede Datei läuft auf Stille aus
(letzte 50 ms unter -50 dBFS). Teile bis "Abschluss" stammen aus der Axt-Probe (#201).
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


def schwung_huell(u, gipfel):
    """Weicher Schwung: steigt mit sin^2 bis zum Gipfel, fällt mit cos^2 auf null."""
    if u < gipfel:
        return math.sin(0.5 * math.pi * u / gipfel) ** 2
    return math.cos(0.5 * math.pi * (u - gipfel) / (1.0 - gipfel)) ** 2


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


def abschluss(x, aus=0.15, ein=0.002, tief_hz=2400.0):
    """Hochpass 30 Hz (kein Gleichanteil), Tiefpass 4. Ordnung bei `tief_hz` (zwei Biquads: die
    Energie über 4 kHz bleibt klein), Kosinus-Auslauf über die letzten `aus` Sekunden, kurzes
    Einblenden. Den Pegel stellt später `pegel` ein."""
    x = biquad(x, "hoch", 30.0)
    x = biquad(biquad(x, "tief", tief_hz), "tief", tief_hz)
    x = list(x)
    m = min(n(aus), len(x))
    for i in range(m):
        x[-1 - i] *= math.sin(0.5 * math.pi * i / m) ** 2
    for i in range(min(n(ein), len(x))):
        x[i] *= i / n(ein)
    return x


def zu(x, ziel):
    return x[:ziel] + [0.0] * max(0, ziel - len(x))


def setze(spur, x, t, gain=1.0):
    """Mischt x ab Zeit t (s) in spur (verlängert sie bei Bedarf)."""
    s0 = n(t)
    if len(spur) < s0 + len(x):
        spur.extend([0.0] * (s0 + len(x) - len(spur)))
    for i, v in enumerate(x):
        spur[s0 + i] += v * gain
    return spur


# ---------- Bausteine der Peitsche ----------

def knall(rng, band_hz, q, klick_abfall, koerper_abfall, sek=0.1):
    """Peitschenknall: ein winziger Rauschklick (Überschallspitze, Abfall in 1 bis 3 ms) plus ein
    kurzer Körper aus Rauschband (Güte niedrig, also kein Ton), beides ohne Vorlauf."""
    klick = biquad(rauschen(sek, rng), "tief", 3600.0)
    koerper = biquad(rauschen(sek, rng), "band", band_hz, q)
    out = []
    for i in range(n(sek)):
        t = i / SR
        a = min(1.0, t / 0.0002)
        out.append(a * (klick[i] * math.exp(-klick_abfall * t) + 0.8 * koerper[i] * math.exp(-koerper_abfall * t)))
    return norm(out)


def anschwellen(sek, rng, f0, f1, q, hoch, ende_aus=0.006, stufen=6):
    """Ausholen: Rauschband, dessen Mitte von f0 nach f1 fährt und das bis zum Ende (dem Knall)
    mit der Potenz `hoch` von u anschwillt; endet mit kurzem Abfall, damit nichts knackt."""
    x = stufig_band(rauschen(sek, rng), f0, f1, q, stufen)
    laenge = len(x)
    m = n(ende_aus)
    for i in range(laenge):
        a = (i / laenge) ** hoch
        if i > laenge - m:
            a *= math.sin(0.5 * math.pi * (laenge - i) / m) ** 2
        x[i] *= a
    return norm(x)


def koernung(sek, rng, hz):
    """Unregelmäßige Pegelkörnung 0..1 (Rauschen tiefpassgefiltert, Betrag, normiert)."""
    k = [abs(v) for v in biquad(rauschen(sek, rng), "tief", hz)]
    g = max(k) or 1.0
    return [v / g for v in k]


# ---------- 1. Schlag: Ausholen und Knall ----------

def schlag_a():
    """A, scharfer Knall (0,5 s): kurzes Ausholen (0,16 s Rauschband 700 -> 2200 Hz schwillt an),
    dann der Knall bei 0,16 s: Rauschklick (Abfall in 1 ms) plus Körper 2 kHz (50 ms), darunter
    ein kleiner Schlag 150 Hz. Danach nur kurzer kalter Hall. Tiefpass 3,0 kHz."""
    rng = random.Random(3011)
    spur = [0.0] * n(0.5)
    setze(spur, anschwellen(0.16, rng, 700.0, 2200.0, 1.0, 2.0), 0.0, 0.45)
    setze(spur, knall(rng, 2000.0, 1.2, 1200.0, 90.0, 0.12), 0.16, 1.0)
    setze(spur, klopf(0.1, 150.0, 55.0, start=1.5, fall=60.0), 0.16, 0.3)
    return abschluss(hall(spur, 0.1, 0.18), aus=0.15, tief_hz=3000.0)


def schlag_b():
    """B, dumpfer Lederschlag (0,6 s): langsameres Ausholen (0,3 s, tiefes Band 250 -> 600 Hz), der
    Aufschlag ist ein nasser Lederklatsch: Rauschen um 700 Hz (kein Klick) plus Schlag 85 Hz.
    Nichts über 1,3 kHz im Knall. Tiefpass 2,4 kHz."""
    rng = random.Random(3012)
    spur = [0.0] * n(0.6)
    setze(spur, anschwellen(0.3, rng, 250.0, 600.0, 0.8, 1.5), 0.0, 0.6)
    k = biquad(knall(rng, 700.0, 0.9, 500.0, 60.0, 0.15), "tief", 1300.0)
    setze(spur, norm(k), 0.3, 0.9)
    setze(spur, klopf(0.25, 85.0, 22.0, start=1.6, fall=30.0), 0.3, 0.85)
    return abschluss(hall(spur, 0.1, 0.2), aus=0.2)


def schlag_c():
    """C, langes Zischen mit kurzem Knall (0,85 s): das Ausholen ist ein langes Zischen (0,55 s
    Rauschband 900 -> 2800 Hz, wächst fast linear), der Knall bei 0,55 s ist klein und kurz
    (Körper 1,8 kHz, 20 ms), danach klingt ein Rest Zischen (0,15 s) aus. Tiefpass 2,4 kHz."""
    rng = random.Random(3013)
    spur = [0.0] * n(0.85)
    setze(spur, anschwellen(0.55, rng, 900.0, 2800.0, 0.7, 1.2), 0.0, 0.8)
    setze(spur, knall(rng, 1800.0, 1.0, 1500.0, 200.0, 0.08), 0.55, 0.5)
    rest = zischen(0.2, rng, 2200.0, 1200.0, 0.8, 18.0, anstieg=0.006)
    setze(spur, rest, 0.55, 0.3)
    return abschluss(hall(spur, 0.08, 0.15), aus=0.25)


# ---------- 2. Treffer in Fleisch (Lederklatsch, bis etwa 0,4 s) ----------

def fleisch_a():
    """A, Klatsch (0,35 s): trockener Lederklatsch: Rauschstoß um 900 Hz (30 ms) mit Schlag 110 Hz
    darunter und einem kurzen Knacken (Rauschen unter 2,5 kHz, 12 ms)."""
    rng = random.Random(3021)
    spur = []
    setze(spur, klopf(0.3, 110.0, 24.0, start=1.6, fall=30.0), 0.0, 1.0)
    setze(spur, tonstoss(0.03, rng, 900.0, 0.8, 120.0), 0.0, 0.9)
    setze(spur, norm(biquad(zischen(0.015, rng, 1800.0, 1800.0, 0.6, 250.0, anstieg=0.0005), "tief", 2500.0)), 0.0, 0.5)
    return abschluss(hall(spur, 0.05, 0.12), aus=0.15)


def fleisch_b():
    """B, Striemen (0,4 s): kurzes Knacken des Lederendes (1,5 kHz, 20 ms), dann zieht das Leder
    über die Haut: Reibe-Rauschen 1,1 -> 0,35 kHz (0,22 s), darunter ein leiser Schlag 95 Hz."""
    rng = random.Random(3022)
    spur = []
    setze(spur, tonstoss(0.02, rng, 1500.0, 1.0, 350.0), 0.0, 0.8)
    setze(spur, klopf(0.2, 95.0, 28.0, start=1.5, fall=30.0), 0.0, 0.7)
    setze(spur, zischen(0.22, rng, 1100.0, 350.0, 1.0, 11.0, anstieg=0.01), 0.015, 0.5)
    return abschluss(hall(spur, 0.05, 0.12), aus=0.2)


def fleisch_c():
    """C, nass-dumpf (0,4 s): runder Schlag 65 Hz (fällt von 130 Hz), darunter feuchtes Schmatzen
    (Rauschband 400 -> 200 Hz, grobe Pegelkörnung 45 Hz) und ein weicher Stoß um 600 Hz."""
    rng = random.Random(3023)
    spur = []
    setze(spur, klopf(0.35, 65.0, 14.0, start=2.0, fall=16.0, anstieg=0.002), 0.0, 1.0)
    sch = stufig_band(rauschen(0.25, rng), 400.0, 200.0, 1.2)
    kn = koernung(0.25, rng, 45.0)
    sch = [sch[i] * (0.3 + 0.7 * kn[i]) * math.exp(-12.0 * i / SR) * min(1.0, i / n(0.004)) for i in range(len(sch))]
    setze(spur, norm(sch), 0.0, 0.6)
    setze(spur, tonstoss(0.03, rng, 600.0, 0.9, 130.0), 0.0, 0.4)
    return abschluss(hall(spur, 0.05, 0.12), aus=0.15)


# ---------- 3. Treffer auf Stein oder Holz (Fehlschlag an der Wand, bis etwa 0,45 s) ----------

def hart_a():
    """A, Stein knallt (0,3 s): harter Knall des Lederendes auf Stein (Klick, Körper 1,6 kHz, 15 ms)
    und ein trockenes Tock aus Rauschen um 420 Hz (niedrige Güte, kein Ton) mit Schlag 140 Hz."""
    rng = random.Random(3031)
    spur = []
    setze(spur, knall(rng, 1600.0, 1.0, 1000.0, 160.0, 0.06), 0.0, 0.9)
    setze(spur, klopf(0.12, 140.0, 45.0, start=1.4, fall=50.0), 0.0, 0.6)
    setze(spur, tonstoss(0.06, rng, 420.0, 0.7, 70.0), 0.0, 0.5)
    return abschluss(hall(spur, 0.08, 0.2), aus=0.15, tief_hz=3000.0)


def hart_b():
    """B, Holz klopft (0,4 s): Leder auf Holzbalken: dumpfer Klopf 190 Hz (fällt auf 150 Hz, klingt in
    50 ms aus) mit knapper Rauschspitze 1,4 kHz und einem tiefen Schlag 70 Hz darunter."""
    rng = random.Random(3032)
    spur = []
    setze(spur, klopf(0.25, 150.0, 55.0, start=1.3, fall=45.0), 0.0, 1.0)
    setze(spur, knall(rng, 1400.0, 1.0, 900.0, 200.0, 0.06), 0.0, 0.5)
    setze(spur, klopf(0.2, 70.0, 22.0, start=1.6, fall=30.0), 0.0, 0.6)
    return abschluss(hall(spur, 0.08, 0.2), aus=0.2)


def hart_c():
    """C, Stein rieselt (0,45 s): kleiner Knall (Klick und Körper 1,2 kHz), Schlag 70 Hz, danach
    rieselt Staub: Rauschen 300 Hz bis 1,5 kHz in groben Körnern (30 Hz), fällt in 0,3 s ab."""
    rng = random.Random(3033)
    spur = []
    setze(spur, knall(rng, 1200.0, 1.0, 1100.0, 140.0, 0.06), 0.0, 0.8)
    setze(spur, klopf(0.2, 70.0, 20.0, start=1.6, fall=30.0), 0.0, 0.7)
    laenge = n(0.35)
    ries = biquad(biquad(rauschen(0.35, rng), "tief", 1500.0), "hoch", 300.0)
    kn = koernung(0.35, rng, 30.0)
    ries = norm([ries[i] * (0.15 + 0.85 * kn[i]) * math.exp(-10.0 * i / SR) * min(1.0, i / n(0.006)) for i in range(laenge)])
    setze(spur, ries, 0.02, 0.4)
    return abschluss(hall(spur, 0.08, 0.2), aus=0.22)


# ---------- 4. Aufladen (Zug baut sich auf, 1,0 s, alles dunkel) ----------

def spann(i, laenge, hoch):
    return (i / laenge) ** hoch


def aufladen_a():
    """A, Leder knarrt (1,3 s): Rauschband 180 -> 420 Hz (Güte 3) knarrt in groben Körnern (22 Hz) und
    wird bis 1,0 s lauter, darunter ein Brummen 55 -> 85 Hz; zum Schluss zieht es fest: tiefer
    Schlag 60 Hz."""
    rng = random.Random(3041)
    laenge = n(1.0)
    kn = stufig_band(rauschen(1.0, rng), 180.0, 420.0, 3.0)
    ko = koernung(1.0, rng, 22.0)
    kn = norm([kn[i] * spann(i, laenge, 1.3) * (0.2 + 0.8 * ko[i]) for i in range(laenge)])
    brumm = tiefton(1.0, 55.0, 85.0, 0.999, teile=3)
    brumm = norm([brumm[i] * spann(i, laenge, 1.4) for i in range(laenge)])
    m = n(0.02)
    zug = mische(mal(kn, 0.8), mal(brumm, 0.5))
    for i in range(m):
        zug[laenge - 1 - i] *= math.sin(0.5 * math.pi * i / m) ** 2
    spur = list(zug)
    setze(spur, klopf(0.25, 60.0, 16.0, start=1.6, fall=22.0), 1.0, 0.9)
    return abschluss(hall(spur, 0.1, 0.2), aus=0.25)


def aufladen_b():
    """B, Wirbeln (1,2 s): das Leder kreist durch die Luft: Rauschband 300 -> 900 Hz, dessen Pegel
    mit 3 -> 11 Hz pulst (jede Runde ein Puls, schneller werdend), insgesamt lauter werdend;
    es endet ohne Schlag in weichem Abfall (Gipfel bei 0,95 s)."""
    rng = random.Random(3042)
    laenge = n(1.2)
    x = stufig_band(rauschen(1.2, rng), 300.0, 900.0, 1.0)
    ph = 0.0
    out = []
    for i in range(laenge):
        u = i / laenge
        ph += TAU * (3.0 + 8.0 * u) / SR
        puls = 0.45 + 0.55 * (0.5 - 0.5 * math.cos(ph))
        gipfel = 0.95 / 1.2
        h = (u / gipfel) ** 1.5 if u < gipfel else math.cos(0.5 * math.pi * (u - gipfel) / (1.0 - gipfel)) ** 2
        out.append(x[i] * puls * h)
    return abschluss(hall(norm(out), 0.08, 0.15), aus=0.25)


def aufladen_c():
    """C, Spannen (1,3 s): dunkles, gedehntes Reiben (Rauschband 120 -> 260 Hz, Güte 2,5, grobe
    Körnung 14 Hz), ein Brummen 50 -> 80 Hz, dazu drei kurze Lederquietscher (Rauschen um 350 bis
    500 Hz, 50 ms); endet mit einem tiefen Schlag 50 Hz bei 1,05 s."""
    rng = random.Random(3043)
    laenge = n(1.05)
    rb = stufig_band(rauschen(1.05, rng), 120.0, 260.0, 2.5)
    ko = koernung(1.05, rng, 14.0)
    rb = norm([rb[i] * spann(i, laenge, 1.1) * (0.35 + 0.65 * ko[i]) for i in range(laenge)])
    brumm = tiefton(1.05, 50.0, 80.0, 0.999, teile=3)
    brumm = norm([brumm[i] * spann(i, laenge, 1.8) for i in range(laenge)])
    m = n(0.02)
    zug = mische(mal(rb, 0.8), mal(brumm, 0.45))
    for i in range(m):
        zug[laenge - 1 - i] *= math.sin(0.5 * math.pi * i / m) ** 2
    spur = list(zug)
    for t, f in ((0.35, 380.0), (0.62, 450.0), (0.85, 500.0)):
        q = norm(zischen(0.05, rng, f, f * 1.2, 1.5, 55.0, anstieg=0.006))
        setze(spur, q, t, 0.35)
    setze(spur, klopf(0.25, 50.0, 15.0, start=1.6, fall=20.0), 1.05, 1.0)
    return abschluss(hall(spur, 0.1, 0.2), aus=0.25)


# ---------- 5. Heranziehen (der Getroffene wird gezerrt, bis etwa 0,5 s) ----------

def zug_a():
    """A, Ruck (0,35 s): ein kurzes Reißen: Rauschband fällt von 1,5 kHz auf 300 Hz (0,3 s), dazu ein
    dumpfer Schlag 75 Hz (Ruck am Ende der Leine)."""
    rng = random.Random(3051)
    spur = []
    setze(spur, zischen(0.3, rng, 1500.0, 300.0, 0.9, 9.0, anstieg=0.006), 0.0, 0.8)
    setze(spur, klopf(0.3, 75.0, 16.0, start=1.8, fall=22.0), 0.0, 1.0)
    return abschluss(hall(spur, 0.06, 0.12), aus=0.18)


def zug_b():
    """B, Schleifen (0,5 s): der Körper schleift über Stein: Rauschband 500 -> 200 Hz in groben
    Körnern (40 Hz), setzt weich ein (30 ms) und fällt gleichmäßig ab; ein leiser Stoß 60 Hz am
    Anfang."""
    rng = random.Random(3052)
    laenge = n(0.45)
    x = stufig_band(rauschen(0.45, rng), 500.0, 200.0, 0.8)
    ko = koernung(0.45, rng, 40.0)
    x = norm([x[i] * (0.25 + 0.75 * ko[i]) * math.exp(-6.0 * i / SR) * min(1.0, i / n(0.03)) for i in range(laenge)])
    spur = []
    setze(spur, x, 0.0, 1.0)
    setze(spur, klopf(0.15, 60.0, 25.0, start=1.6, fall=30.0), 0.0, 0.5)
    return abschluss(hall(spur, 0.06, 0.12), aus=0.2)


def zug_c():
    """C, Lederruck (0,3 s): die Leine spannt sich: Rauschband 260 -> 520 Hz (Güte 2,5, Körnung
    30 Hz) schwillt in 0,1 s an und fällt ab, darunter ein Schlag 90 Hz; kürzester der drei."""
    rng = random.Random(3053)
    laenge = n(0.22)
    x = stufig_band(rauschen(0.22, rng), 260.0, 520.0, 2.5)
    ko = koernung(0.22, rng, 30.0)
    x = norm([x[i] * (0.3 + 0.7 * ko[i]) * math.sin(math.pi * min(1.0, i / laenge * 1.3)) ** 2 for i in range(laenge)])
    spur = []
    setze(spur, x, 0.0, 0.8)
    setze(spur, klopf(0.2, 90.0, 30.0, start=1.5, fall=40.0), 0.08, 0.9)
    return abschluss(hall(spur, 0.05, 0.12), aus=0.15)


# ---------- Kategorien, Pegel, Dateien ----------

# (Kürzel, Titel, Spitze dBFS, gleiche mittlere Lautstärke?, [(Name, Funktion)])
KATEGORIEN = [
    ("schlag", "Schlag", -6.0, False,
     [("a-scharfer-knall", schlag_a), ("b-dumpfer-lederschlag", schlag_b), ("c-langes-zischen", schlag_c)]),
    ("treffer-fleisch", "Treffer in Fleisch", -4.5, False,
     [("a-klatsch", fleisch_a), ("b-striemen", fleisch_b), ("c-nass-dumpf", fleisch_c)]),
    ("treffer-hart", "Treffer auf Stein oder Holz", -4.5, False,
     [("a-stein-knallt", hart_a), ("b-holz-klopft", hart_b), ("c-stein-rieselt", hart_c)]),
    ("aufladen", "Aufladen", -10.0, True,
     [("a-leder-knarrt", aufladen_a), ("b-wirbeln", aufladen_b), ("c-spannen", aufladen_c)]),
    ("heranziehen", "Heranziehen", -6.0, False,
     [("a-ruck", zug_a), ("b-schleifen", zug_b), ("c-lederruck", zug_c)]),
]

# Zusammenhang: der Knall setzt bei KNALLZEIT ein; der Treffer folgt 20 ms später
KNALLZEIT = {"a": 0.16, "b": 0.30, "c": 0.55}
TREFFER_NACH_KNALL = 0.02
SPITZE_ZUSAMMENHANG = -1.5


def baue_kategorie(spitze_db, gleich_laut, varianten):
    """Spitze auf Ziel; bei `gleich_laut` danach A/B/C auf gleiche mittlere Lautstärke (nach unten)."""
    roh = [pegel(fn(), spitze_db) for _, fn in varianten]
    if not gleich_laut:
        return roh
    ziel = min(rms(x) for x in roh)
    return [mal(x, ziel / rms(x)) for x in roh]


def main():
    ziel = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parent
    ziel.mkdir(parents=True, exist_ok=True)
    teile = {}
    for kuerzel, _, spitze, gleich, varianten in KATEGORIEN:
        for (name, _), x in zip(varianten, baue_kategorie(spitze, gleich, varianten)):
            datei = "%s-%s.wav" % (kuerzel, name)
            schreibe(ziel / datei, x)
            teile[(kuerzel, name[0])] = x
            print(datei)
    for b in "abc":
        spur = list(teile[("schlag", b)])
        setze(spur, teile[("treffer-fleisch", b)], KNALLZEIT[b] + TREFFER_NACH_KNALL)
        s = max(abs(v) for v in spur)
        lim = 10 ** (SPITZE_ZUSAMMENHANG / 20.0)
        if s > lim:
            spur = mal(spur, lim / s)
        datei = "zusammenhang-%s.wav" % b
        schreibe(ziel / datei, spur)
        print(datei)


if __name__ == "__main__":
    main()
