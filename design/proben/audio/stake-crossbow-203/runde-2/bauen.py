#!/usr/bin/env python3
"""Hörprobe Stake Crossbow (#203), Runde 2: Schuss, Treffer Fleisch (Durchbohren),
Treffer hart (Stein/Holz) und Nachladen in je drei deutlich verschiedenen
Fassungen A/B/C. Jede Fassung ist eine eigene WAV-Datei.

Nur Python-Standardbibliothek, fester Seed je Stück: zwei Läufe ergeben bitgleiche
Dateien (siehe wav.sha256). Aufruf:  python bauen.py [Zielordner]

Rahmen (Audio Bible): mono, 16 Bit, 44 100 Hz; Tiefpass 2,8 kHz (nichts Hohes,
Schrilles oder Glockiges); Grundtöne unter 300 Hz; Klang endet auf Stille.
Pegel wie bei Whip/Axt/Crossbow-Runde-1: A/B/C einer Kategorie auf gleiche mittlere
Lautstärke (nur nach unten gerechnet), Spitze höchstens -1,5 dBFS.

Besonderheiten dieser Runde:
- Treffer in Fleisch: kurz (unter 0,4 s), Hauptenergie in den ersten 30 ms, damit
  eine Dreierfolge im Abstand von 40 ms nicht übersteuert. Der Pegel wird so gesetzt,
  dass die Summe dreier gleichzeitig laufender Treffer (auch aus gemischten Fassungen)
  höchstens -1,5 dBFS erreicht.
- Nachladen ist eine Funktion der Dauer T (s): Ereignisse liegen bei festen Bruchteilen
  von T, einzelne Schläge (Griff, Einrasten, Pflock einlegen) behalten ihre Länge,
  gestreckt werden nur Zug und Ratsche. Hauptdatei T = 2,5 s (wie reloadTime des
  Gewehrs), Proben für 1,5 s und 3,5 s liegen in dehnung/.
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
    else:
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
    """Bandpass, dessen Mitte über die Länge von f0 nach f1 fährt."""
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
    """Gefiltertes Rauschen, Mitte fährt von f0 nach f1, Hüllkurve: Anstieg, Halten, Abfall."""
    x = stufig_band(rauschen(sek, rng), f0, f1, q)
    for i in range(len(x)):
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
    """Dumpfer Schlag: Sinus, Tonhöhe anfangs höher und schnell fallend."""
    ph = 0.0
    out = []
    for i in range(n(sek)):
        t = i / SR
        fr = f * (1.0 + (start - 1.0) * math.exp(-t * fall))
        ph += TAU * fr / SR
        out.append(math.sin(ph) * math.exp(-abfall * t) * min(1.0, t / anstieg))
    return out


def tonstoss(sek, rng, f, q, abfall):
    """Kurzer Anschlag aus Rauschen: fester Bandpass, sehr kurz."""
    x = biquad(rauschen(sek, rng), "band", f, q)
    for i in range(len(x)):
        t = i / SR
        x[i] *= math.exp(-abfall * t) * min(1.0, t / 0.0008)
    return norm(x)


def schwingung(sek, f, abfall, teile, fall=30.0, start=1.04, tief=1400.0):
    """Gespannte Sehne: Teiltöne (Verhältnis, Pegel, Zerfallsfaktor), Tonhöhe sinkt leicht."""
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


def koernung(x, rng, rate, tiefe):
    """Rauhigkeit: Pegel wird mit zufälligem Verlauf (Stufen mit Rate Hz, geglättet) moduliert."""
    schritt = max(1, int(SR / rate))
    werte = [rng.uniform(1.0 - tiefe, 1.0) for _ in range(len(x) // schritt + 2)]
    out = []
    for i, v in enumerate(x):
        k = i // schritt
        a = (i % schritt) / schritt
        g = werte[k] * (1 - a) + werte[k + 1] * a
        out.append(v * g)
    return out


def hall(x, nachlauf=0.12, nass=0.3):
    """Kurzer kalter Hall aus vier Kammfiltern."""
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
    """Hochpass 30 Hz, Tiefpass 2,8 kHz, Kosinus-Auslauf, kurzes Einblenden."""
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


def spitze_db(x):
    return 20 * math.log10(max(abs(v) for v in x) + 1e-12)


def schreibe(pfad, x):
    daten = b"".join(struct.pack("<h", max(-32768, min(32767, int(round(v * 32767))))) for v in x)
    with wave.open(str(pfad), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(daten)


# ---------- Schuss: Sehne schnellt, schwerer Pflock fliegt los ----------

def schuss_a():
    """Sehne und Zischen: tiefes Hanfseil schnellt (78 Hz), dumpfer Schlag, danach ein
    fallendes Rauschband (Pflock fliegt weg, wird dumpfer)."""
    rng = random.Random(2031)
    sehne = schwingung(0.35, 78.0, 13.0, [(1, 1.0, 1.0), (2, 0.5, 1.7), (3, 0.22, 2.6)], start=1.10, fall=40.0)
    schlag = klopf(0.18, 62.0, 28.0, start=1.8, fall=70.0)
    flug = versetze(zischen(0.42, rng, 1500.0, 320.0, 1.2, 7.0, anstieg=0.012), 0.012)
    return hall(mische(mal(sehne, 0.8), mal(schlag, 0.9), mal(flug, 0.42)), 0.12, 0.28)


def schuss_b():
    """Dumpfer Stoß: nur Druck (42 Hz) und ein kurzes Holzklacken, keine Sehne, kein Zischen.
    Kürzeste, tiefste Fassung."""
    rng = random.Random(2032)
    druck = klopf(0.26, 42.0, 15.0, start=2.3, fall=42.0)
    holz = tonstoss(0.06, rng, 190.0, 1.0, 85.0)
    rest = schwingung(0.09, 64.0, 40.0, [(1, 1.0, 1.0), (2, 0.4, 1.5)], start=1.05, fall=60.0)
    return hall(mische(mal(druck, 1.0), mal(holz, 0.55), mal(rest, 0.25)), 0.10, 0.22)


def schuss_c():
    """Schiene schleift: Schlag, dann gleitet der Pflock rauh über die Holzschiene
    (körniges Rauschband 450 auf 900 Hz), zuletzt ein kurzes Tock beim Verlassen."""
    rng = random.Random(2033)
    schlag = klopf(0.16, 70.0, 30.0, start=1.7, fall=65.0)
    gleit = zischen(0.24, rng, 450.0, 900.0, 2.0, 5.0, anstieg=0.01, halten=0.08)
    gleit = koernung(gleit, rng, 260.0, 0.65)
    tock = tonstoss(0.05, rng, 260.0, 1.1, 90.0)
    return hall(mische(mal(schlag, 0.95), versetze(mal(gleit, 0.6), 0.015), versetze(mal(tock, 0.5), 0.27)), 0.12, 0.26)


# ---------- Treffer in Fleisch: kurz, Energie in den ersten 30 ms ----------

def fleisch_a():
    """Stich: knapper Anschlag (520 Hz), runder Schlag 70 auf 48 Hz, feuchtes Nachgleiten."""
    rng = random.Random(2041)
    stich = tonstoss(0.04, rng, 520.0, 0.9, 120.0)
    schlag = klopf(0.24, 70.0, 17.0, start=1.9, fall=55.0)
    nass = versetze(zischen(0.17, rng, 650.0, 240.0, 1.0, 16.0, anstieg=0.006), 0.02)
    return mische(mal(stich, 0.7), mal(schlag, 1.0), mal(nass, 0.35))


def fleisch_b():
    """Knirschen: tiefer Körper (55 Hz) und viele kleine Körner (Knochen, Sehnen) in
    den ersten 0,11 s, am Ende ein kurzes Dumpfes, wenn der Pflock austritt."""
    rng = random.Random(2042)
    koerper = klopf(0.22, 55.0, 20.0, start=1.8, fall=50.0)
    koerner = [0.0] * n(0.2)
    for _ in range(9):
        t = rng.uniform(0.0, 0.11)
        f = rng.uniform(280.0, 800.0)
        a = rng.uniform(0.3, 1.0) * (1.0 - t * 4.0)
        k = mal(tonstoss(rng.uniform(0.02, 0.035), rng, f, 1.2, 160.0), a)
        for i, v in enumerate(k):
            koerner[n(t) + i] += v
    austritt = versetze(mal(klopf(0.1, 92.0, 34.0, start=1.4, fall=60.0), 0.4), 0.14)
    return mische(mal(koerper, 0.95), mal(koerner, 0.55), austritt)


def fleisch_c():
    """Reißen: rauhes Rauschband (230 steigt auf 520 Hz, Pegel zittert mit 65 Hz) wie
    Stoff oder Haut, die reißt, unter einem kurzen Schlag (60 Hz)."""
    rng = random.Random(2043)
    riss = stufig_band(rauschen(0.3, rng), 230.0, 520.0, 0.9)
    laenge = len(riss)
    for i in range(laenge):
        t = i / SR
        h = min(1.0, t / 0.012) * math.exp(-10.0 * t)
        riss[i] *= h * (0.55 + 0.45 * math.sin(TAU * 65.0 * t + 0.7))
    riss = norm(riss)
    schlag = klopf(0.2, 60.0, 20.0, start=1.8, fall=55.0)
    return mische(mal(schlag, 0.9), mal(riss, 0.65))


# ---------- Treffer hart: Pflock in Stein oder Holz ----------

def hart_a():
    """Holz splittert: kurzes Knacken (1,1 kHz), Körper 130 Hz, danach sechs kleine
    Splitter (Rauschkörner 700 bis 1500 Hz), leiser werdend."""
    rng = random.Random(2051)
    knack = tonstoss(0.05, rng, 1100.0, 1.5, 90.0)
    koerper = klopf(0.2, 130.0, 22.0, start=1.5, fall=60.0)
    splitter = [0.0] * n(0.3)
    for k in range(6):
        t = 0.04 + 0.035 * k + rng.uniform(0.0, 0.02)
        g = tonstoss(rng.uniform(0.015, 0.03), rng, rng.uniform(700.0, 1500.0), 1.3, 150.0)
        for i, v in enumerate(g):
            if n(t) + i < len(splitter):
                splitter[n(t) + i] += v * (0.55 - 0.07 * k)
    return hall(mische(mal(knack, 0.8), mal(koerper, 0.95), mal(splitter, 0.7)), 0.1, 0.25)


def hart_b():
    """Stein bricht: breiter Geräuschstoß ohne Tonhöhe (900 Hz, weit), tiefer Schlag
    (55 Hz), danach drei fallende Brocken. Kein Ton, nur Körnung."""
    rng = random.Random(2052)
    stoss = biquad(rauschen(0.12, rng), "band", 900.0, 0.5)
    for i in range(len(stoss)):
        t = i / SR
        stoss[i] *= math.exp(-60.0 * t) * min(1.0, t / 0.0008)
    stoss = norm(stoss)
    masse = klopf(0.2, 55.0, 24.0, start=1.6, fall=70.0)
    brocken = [0.0] * n(0.3)
    for t, f, a in ((0.09, 700.0, 0.45), (0.15, 520.0, 0.3), (0.215, 880.0, 0.18)):
        g = mal(tonstoss(0.03, rng, f, 0.9, 110.0), a)
        for i, v in enumerate(g):
            brocken[n(t) + i] += v
    return hall(mische(mal(stoss, 0.9), mal(masse, 0.85), brocken), 0.14, 0.3)


def hart_c():
    """Abschaben: Pflock prallt an Stein ab und kratzt: kurzer harter Anschlag mit Schlag
    (90 Hz), dann körniges Schaben (1,3 kHz fällt auf 500 Hz) mit 140 Hz Körnung."""
    rng = random.Random(2053)
    anschlag = tonstoss(0.04, rng, 800.0, 0.7, 110.0)
    schlag = klopf(0.16, 90.0, 26.0, start=1.6, fall=70.0)
    schaben = zischen(0.2, rng, 1300.0, 500.0, 2.0, 9.0, anstieg=0.01, halten=0.03)
    schaben = koernung(schaben, rng, 140.0, 0.7)
    return hall(mische(mal(anschlag, 0.85), mal(schlag, 0.9), versetze(mal(schaben, 0.5), 0.02)), 0.1, 0.25)


# ---------- Nachladen: Funktion der Dauer T ----------

def n_griff(rng):
    """Hand packt zu, Fuß im Bügel: weiches Tock und kurzes Reiben."""
    return mische(mal(klopf(0.1, 100.0, 35.0, start=1.4, fall=60.0), 0.5), mal(tonstoss(0.06, rng, 240.0, 1.0, 70.0), 0.35))


def n_einrasten(rng):
    """Schwerer Klack: Eisen auf Holz, tiefer Schlag und kurzer Anschlag (420 Hz)."""
    return mische(mal(klopf(0.14, 140.0, 28.0, start=1.4, fall=60.0), 0.9), mal(tonstoss(0.05, rng, 420.0, 1.1, 85.0), 0.8))


def n_einlegen(rng):
    """Pflock gleitet in die Rille (kurzes Schaben, 500 bis 300 Hz), dann ein Tock."""
    gleit = mal(koernung(zischen(0.13, rng, 500.0, 300.0, 1.6, 8.0, anstieg=0.02, halten=0.05), rng, 200.0, 0.5), 0.45)
    return mische(gleit, versetze(mal(klopf(0.1, 120.0, 40.0, start=1.3, fall=70.0), 0.6), 0.14),
                  versetze(mal(tonstoss(0.04, rng, 300.0, 1.2, 100.0), 0.5), 0.14))


def zug(dauer, rng, f0, f1, ton0, ton1):
    """Ein gespannter Zug: rauhes Knarren (Band f0 auf f1) und ein tiefer Ton, der
    mit der Spannung steigt (ton0 auf ton1). Pegel wächst bis zum Ende, endet abrupt
    (das Einrasten folgt)."""
    knarr = koernung(stufig_band(rauschen(dauer, rng), f0, f1, 3.0), rng, 22.0, 0.75)
    ph = 0.0
    ton = []
    for i in range(n(dauer)):
        u = i / max(1, n(dauer) - 1)
        ph += TAU * (ton0 + (ton1 - ton0) * u) / SR
        ton.append(math.sin(ph) + 0.4 * math.sin(2 * ph))
    ton = biquad(ton, "tief", 400.0)
    out = []
    for i in range(len(knarr)):
        u = i / max(1, len(knarr) - 1)
        h = (0.15 + 0.85 * u ** 1.4) * min(1.0, i / n(0.03))
        out.append((knarr[i] * 0.55 + ton[i] * 0.5) * h)
    # sanftes Ende (Zug lässt los), kurz
    m = min(n(0.02), len(out))
    for i in range(m):
        out[-1 - i] *= i / m
    return norm(out)


def nachladen(variante, T):
    """T = Gesamtdauer des Nachladens (Datei etwa T lang). Griff, Einrasten und Einlegen
    behalten ihre Länge; Zug oder Ratsche füllen die Zeit dazwischen (T - 0,7 s)."""
    rng = random.Random({"a": 2061, "b": 2062, "c": 2063}[variante])
    t_griff = 0.0
    t_einl = T - 0.4          # Pflock einlegen; danach läuft der Klang aus, Datei etwa T lang
    t_rast = t_einl - 0.2     # Einrasten
    t_zug0 = 0.1              # Zug beginnt gleich nach dem Griff; nur dieser Teil wird gestreckt
    spann = t_rast - t_zug0
    teile = [versetze(mal(n_griff(rng), 0.9), t_griff)]
    if variante == "a":
        # Kurbel: Ratsche, Klickdichte bleibt (etwa 10 je Sekunde), Abstände werden enger
        zahl = max(6, int(round(spann * 9.0)))
        zeiten = []
        for k in range(zahl):
            u = k / (zahl - 1)
            zeiten.append(t_zug0 + spann * (u ** 0.8) * 0.97)
        for k, t in enumerate(zeiten):
            u = k / (zahl - 1)
            f = 240.0 + 280.0 * u
            klick = mal(tonstoss(0.035, rng, f, 1.3, 120.0), 0.35 + 0.65 * u)
            teile.append(versetze(klick, t))
        brumm = zug(spann, rng, 120.0, 240.0, 55.0, 95.0)
        teile.append(versetze(mal(brumm, 0.22), t_zug0))
    elif variante == "b":
        # Seilzug: ein einziger langer Zug, Seil knarrt und ächzt, keine Klicks
        teile.append(versetze(mal(zug(spann, rng, 130.0, 320.0, 48.0, 105.0), 0.9), t_zug0))
    else:
        # zwei Züge mit leisem Rasten dazwischen
        halb = spann * 0.42
        pause = spann * 0.16
        teile.append(versetze(mal(zug(halb, rng, 140.0, 260.0, 52.0, 80.0), 0.75), t_zug0))
        teile.append(versetze(mal(tonstoss(0.04, rng, 330.0, 1.2, 100.0), 0.4), t_zug0 + halb + pause * 0.4))
        teile.append(versetze(mal(zug(halb, rng, 200.0, 360.0, 70.0, 110.0), 0.95), t_zug0 + halb + pause))
    teile.append(versetze(n_einrasten(rng), t_rast))
    teile.append(versetze(n_einlegen(rng), t_einl))
    x = mische(*teile)
    return hall(x, 0.15, 0.22)


# ---------- Fassung, Pegel, Ausgabe ----------

def fertig(x, aus=0.12, auslauf=0.0):
    """Abschluss mit `auslauf` Sekunden Stille hinten, damit der Kosinus-Auslauf erst
    greift, wenn der Klang schon abgeklungen ist (Regel 7: letzte 50 ms höchstens -50 dBFS)."""
    return abschluss(x + [0.0] * n(auslauf), aus=aus)


def gleiche_lautstaerke(stuecke, spitze_ziel):
    """Alle auf das Mittel (RMS) des leisesten bringen, Spitze höchstens spitze_ziel."""
    pegel_rms = []
    for x in stuecke:
        pegel_rms.append(rms(x) / (max(abs(v) for v in x)))  # RMS bei Spitze 1
    # gemeinsames Ziel: RMS, das alle bei Spitze <= spitze_ziel erreichen
    g_spitze = 10 ** (spitze_ziel / 20.0)
    ziel = min(p * g_spitze for p in pegel_rms)
    return [mal(x, ziel / rms(x)) for x in stuecke]


def summe_dreier(stuecke, abstand=0.04):
    """Höchster Spitzenwert der Mischungen aus je drei Treffern im Abstand von 40 ms
    (alle 27 Kombinationen der Fassungen)."""
    top = 0.0
    for a in stuecke:
        for b in stuecke:
            for c in stuecke:
                m = mische(a, versetze(b, abstand), versetze(c, 2 * abstand))
                top = max(top, max(abs(v) for v in m))
    return top


def main():
    ziel = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parent
    ziel.mkdir(parents=True, exist_ok=True)
    (ziel / "dehnung").mkdir(exist_ok=True)

    # Schuss
    s = [fertig(f(), 0.18) for f in (schuss_a, schuss_b, schuss_c)]
    s = gleiche_lautstaerke(s, -1.5)
    for name, x in zip(("schuss-a-sehne-zischen", "schuss-b-dumpfer-stoss", "schuss-c-schiene-schleift"), s):
        schreibe(ziel / (name + ".wav"), x)

    # Fleisch: Dreierfolge-Grenze -1,5 dBFS über alle 27 Kombinationen
    fl = [fertig(f(), 0.14, 0.12) for f in (fleisch_a, fleisch_b, fleisch_c)]
    fl = gleiche_lautstaerke(fl, -4.5)
    top = summe_dreier(fl)
    grenze = 10 ** (-1.5 / 20.0)
    if top > grenze:
        fl = [mal(x, grenze / top) for x in fl]
    namen_fl = ("treffer-fleisch-a-stich", "treffer-fleisch-b-knirschen", "treffer-fleisch-c-reissen")
    for name, x in zip(namen_fl, fl):
        schreibe(ziel / (name + ".wav"), x)
    for k, (name, x) in enumerate(zip("abc", fl)):
        schreibe(ziel / ("dreierfolge-fleisch-%s.wav" % name),
                 mische(x, versetze(x, 0.04), versetze(x, 0.08)))

    # Hart
    h = [fertig(f(), 0.14) for f in (hart_a, hart_b, hart_c)]
    h = gleiche_lautstaerke(h, -4.5)
    for name, x in zip(("treffer-hart-a-holz-splittert", "treffer-hart-b-stein-bricht", "treffer-hart-c-schabt-ab"), h):
        schreibe(ziel / (name + ".wav"), x)

    # Nachladen, T = 2,5 s; Dehnung 1,5 s und 3,5 s
    namen_n = {"a": "nachladen-a-kurbel", "b": "nachladen-b-seilzug", "c": "nachladen-c-zwei-zuege"}
    for T, ordner, tag in ((2.5, ziel, ""), (1.5, ziel / "dehnung", "-1.5s"), (3.5, ziel / "dehnung", "-3.5s")):
        nl = [fertig(nachladen(v, T), 0.15) for v in "abc"]
        nl = gleiche_lautstaerke(nl, -8.0)
        for v, x in zip("abc", nl):
            schreibe(ordner / (namen_n[v] + tag + ".wav"), x)


if __name__ == "__main__":
    main()
