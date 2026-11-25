#!/usr/bin/env python3
"""Hoerprobe Fallgitter der Arena (#540): Fallen a, Fallen b, Heben, je zwei Varianten.

Nur Python-Standardbibliothek, alles Synthese mit festem Seed, daher bitgleich
bei jedem Lauf. Aufruf:  python bauen.py [Zielordner]
Ohne Argument landen die WAV-Dateien neben dem Skript.

Zeitbasis: zeitverlauf.csv des Bewegungsentwurfs (Branch
feat/fallgitter-bewegung-probe-540, design/proben/animation/fallgitter-540/),
Kopie im selben Ordner. 60 Ticks je Sekunde; Tick t liegt auf Sample
round(t * 44100 / 60) = t * 735. Die Datei beginnt im Tick 0 (Ausloeser).

Klangbild: schweres Eisen in Stein. Alles unter etwa 2 kHz (Tiefpass), keine
hellen Toene. Fallen: Kette rasselt mit der Fallgeschwindigkeit, Eisen
schleift im Fuehrungsschlitz (Pegel folgt der Geschwindigkeit aus der CSV),
Aufschlag mit kurzem Nachhall. Heben: Winde und Kette gleichmaessig, am Ende
ein dumpfer Riegel. Mono, 16 Bit, 44 100 Hz, Spitze -3 dBFS.
"""
import csv
import math
import random
import sys
import wave
from pathlib import Path

SR = 44100
TAU = 2.0 * math.pi
TICK = SR // 60  # 735 Samples je Tick (44100 / 60 geht auf)
SPITZE_DB = -3.0
HIER = Path(__file__).resolve().parent


def n(sek):
    return int(round(sek * SR))


def tp(tick):
    """Samplestelle eines Ticks."""
    return int(tick) * TICK


# ---------- Filter ----------

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


def lp1(x, fc):
    a = 1.0 - math.exp(-TAU * fc / SR)
    y = 0.0
    out = []
    for s in x:
        y += a * (s - y)
        out.append(y)
    return out


def rauschen(laenge, rng):
    return [rng.uniform(-1.0, 1.0) for _ in range(laenge)]


def addiere(buf, pos, seg, gain=1.0):
    for i, s in enumerate(seg):
        j = pos + i
        if 0 <= j < len(buf):
            buf[j] += s * gain


# ---------- Zeitverlauf aus der CSV ----------

def lade_zeitverlauf():
    daten = {}
    with open(HIER / "zeitverlauf.csv", newline="", encoding="utf-8") as f:
        for z in csv.DictReader(f, delimiter=";"):
            daten.setdefault(z["bewegung"], []).append(float(z["unterkante_m"]))
    return daten


def geschwindigkeit(hoehen, laenge, vorzeichen, vmax, bis_tick=None):
    """Pro Sample die Geschwindigkeit (0..1) aus der Hoehe je Tick.
    vorzeichen = +1 fuer Fallen (Hoehe sinkt), -1 fuer Heben. Stufen je Tick,
    danach 5 ms geglaettet. Vor Tick 'bis_tick' (sonst ganze Bewegung)."""
    v = [0.0] * laenge
    letzte = len(hoehen) - 1 if bis_tick is None else bis_tick
    for k in range(letzte):
        d = vorzeichen * (hoehen[k] - hoehen[k + 1])
        d = max(0.0, d) / vmax
        for i in range(tp(k), min(tp(k + 1), laenge)):
            v[i] = d
    return lp1(v, 90.0)


# ---------- Bausteine ----------

def klink(rng, f, abfall=75.0, dauer=0.09, rauh=0.5):
    """Ein Kettenglied schlaegt an: zwei unharmonische Teiltoene und ein
    dumpfes Rauschkorn. Alles unter etwa 1,8 kHz."""
    ns = n(dauer)
    ro = lp1([rng.uniform(-1, 1) for _ in range(ns)], 1800.0)
    out = []
    for i in range(ns):
        t = i / SR
        s = math.sin(TAU * f * t) * math.exp(-abfall * t)
        s += 0.55 * math.sin(TAU * f * 1.58 * t) * math.exp(-(abfall + 25) * t)
        s += rauh * ro[i] * math.exp(-320.0 * t)
        out.append(s)
    return out


def rasseln(buf, hall, rng, v, ab, bis, rate0, rate1, gain0, gain1, fband, abfall, hallanteil=0.0):
    """Kettenglieder im Bereich [ab, bis) Samples; Rate und Pegel folgen der
    Geschwindigkeit v (0..1)."""
    phase = rng.random()
    for i in range(ab, min(bis, len(buf))):
        s = v[i]
        phase += (rate0 + (rate1 - rate0) * s) / SR
        if phase >= 1.0:
            phase -= 1.0
            g = (gain0 + (gain1 - gain0) * s) * rng.uniform(0.55, 1.0)
            seg = klink(rng, rng.uniform(*fband), abfall * rng.uniform(0.8, 1.25))
            addiere(buf, i, seg, g)
            if hallanteil:
                addiere(hall, i, seg, g * hallanteil)


def schleifen(rng, v, ab, bis, band, var, rumpel, laut):
    """Eisen im Fuehrungsschlitz. Pegel folgt v. Variante 1: rauhes
    Schleifen (unregelmaessig); Variante 2: Ruckeln (Haften und Gleiten,
    Takt folgt der Geschwindigkeit)."""
    laenge = len(v)
    r = rauschen(laenge, rng)
    r = biquad(biquad(r, "hoch", band[0]), "tief", band[1])
    rum = lp1(biquad(rauschen(laenge, rng), "tief", 150.0), 150.0)
    out = [0.0] * laenge
    if var == 1:
        korn = lp1(rauschen(laenge, rng), 28.0)
        for i in range(ab, min(bis, laenge)):
            s = v[i]
            m = 0.45 + 2.2 * korn[i]
            m = max(0.15, min(1.5, m))
            out[i] = (r[i] * m * s ** 0.75 * laut) + rum[i] * s * rumpel * 4.0
    else:
        ph = 0.0
        for i in range(ab, min(bis, laenge)):
            s = v[i]
            ph += (14.0 + 70.0 * s) / SR
            ph -= math.floor(ph)
            m = 0.25 + 0.75 * (1.0 - ph) ** 1.6
            out[i] = (r[i] * m * s ** 0.75 * laut) + rum[i] * s * rumpel * 4.0
    return out


class Hall:
    """Kurzer, kalter Gruftnachhall: vier Kammfilter mit Daempfung."""

    def __init__(self, nachhall, daempfung):
        self.verz = [n(x) for x in (0.0297, 0.0371, 0.0411, 0.0437)]
        self.g = [10 ** (-3.0 * (d / SR) / nachhall) for d in self.verz]
        self.dae = daempfung

    def bearbeite(self, x):
        summe = [0.0] * len(x)
        for d, g in zip(self.verz, self.g):
            puffer = [0.0] * d
            lpz = 0.0
            pos = 0
            for i in range(len(x)):
                y = puffer[pos]
                lpz += self.dae * (y - lpz)
                puffer[pos] = x[i] + g * lpz
                pos = (pos + 1) % d
                summe[i] += y * 0.25
        return summe


def aufschlag(buf, hall, pos, rng, laut, var, hallanteil, klein=False):
    """Schweres Eisen trifft Stein. Der Einsatz liegt genau auf Sample 'pos'.
    Variante 1: dumpfer Schlag, kaum Klang. Variante 2: tiefer Eisenklang
    (unharmonische Teiltoene 170 bis 550 Hz) und weniger Wumms."""
    ns = n(0.55 if not klein else 0.28)
    out = [0.0] * ns
    # Schlag: Sinus mit abfallender Tonhoehe, Phase aufsummiert
    ph = 0.0
    zerf = (11.0 if var == 1 else 15.0) * (1.8 if klein else 1.0)
    f_ende, f_anf = (44.0, 100.0) if var == 1 else (52.0, 105.0)
    if klein:
        f_ende, f_anf = f_ende * 1.5, f_anf * 1.4
    gt = 1.0 if var == 1 else 0.7
    for i in range(ns):
        t = i / SR
        f = f_ende + (f_anf - f_ende) * math.exp(-t * 30.0)
        ph += TAU * f / SR
        out[i] += gt * math.sin(ph + 1.5707963) * math.exp(-zerf * t)
    # Knall: Rauschen, Tiefpass
    kn = lp1(lp1(rauschen(ns, rng), 1700.0), 2600.0)
    gk = 0.9 if var == 1 else 0.6
    for i in range(ns):
        t = i / SR
        out[i] += gk * kn[i] * 2.2 * math.exp(-(70.0 if var == 1 else 55.0) * (1.6 if klein else 1.0) * t)
    # Klang
    if var == 1:
        teile = ((146.0, 0.30, 17.0), (305.0, 0.14, 24.0))
    else:
        teile = ((172.0, 0.55, 6.5), (263.0, 0.42, 8.0), (389.0, 0.30, 9.5), (544.0, 0.20, 12.0))
    for f, g, ab in teile:
        if klein:
            f, ab = f * 1.35, ab * 1.8
        for i in range(ns):
            t = i / SR
            out[i] += g * math.sin(TAU * f * t) * math.exp(-ab * t)
    addiere(buf, pos, out, laut)
    addiere(hall, pos, out, laut * hallanteil)


def ruck(buf, hall, pos, rng, laut, var, hallanteil):
    """Kette faengt das Gitter (Fallen b): kurzer Lastschlag, Glieder
    klirren gleichzeitig an."""
    ns = n(0.40)
    out = [0.0] * ns
    ph = 0.0
    for i in range(ns):
        t = i / SR
        f = 58.0 + 40.0 * math.exp(-t * 35.0)
        ph += TAU * f / SR
        out[i] += 0.8 * math.sin(ph + 1.5707963) * math.exp(-16.0 * t)
    kn = lp1(lp1(rauschen(ns, rng), 1400.0), 2200.0)
    for i in range(ns):
        t = i / SR
        out[i] += 0.8 * kn[i] * 2.2 * math.exp(-85.0 * t)
    teile = ((236.0, 0.30, 20.0),) if var == 1 else ((205.0, 0.40, 9.0), (318.0, 0.30, 11.0), (470.0, 0.18, 14.0))
    for f, g, ab in teile:
        for i in range(ns):
            t = i / SR
            out[i] += g * math.sin(TAU * f * t) * math.exp(-ab * t)
    # Kettenglieder klirren im Ruck an
    for k in range(9):
        off = int(abs(rng.gauss(0.0, 0.028)) * SR)
        seg = klink(rng, rng.uniform(210.0, 520.0), 70.0 + 10 * rng.random())
        addiere(out, off, seg, 0.30 * (1.0 - k / 11.0))
    addiere(buf, pos, out, laut)
    addiere(hall, pos, out, laut * hallanteil)


def loesen(buf, hall, pos, rng, laut, var, hallanteil):
    """Riegel loest sich, Kette lockert sich: kurzes Klacken."""
    ns = n(0.20)
    out = [0.0] * ns
    kn = lp1(lp1(rauschen(ns, rng), 1000.0), 1500.0)
    for i in range(ns):
        t = i / SR
        out[i] += 1.4 * kn[i] * math.exp(-60.0 * t)
        out[i] += 0.6 * math.sin(TAU * 118.0 * t) * math.exp(-34.0 * t)
        out[i] += 0.22 * math.sin(TAU * (210.0 if var == 1 else 187.0) * t) * math.exp(-26.0 * t)
    seg = klink(rng, 330.0 if var == 1 else 290.0, 60.0)
    addiere(out, n(0.035), seg, 0.35)
    addiere(buf, pos, out, laut)
    addiere(hall, pos, out, laut * hallanteil)


def riegel(buf, hall, pos, rng, laut, var, hallanteil):
    """Einrasten oben: dumpfer Schlag unter 120 Hz, nichts Helles.
    Variante 2 mit einem Hauch Luft (tiefes Rauschen) dahinter."""
    ns = n(0.45)
    out = [0.0] * ns
    ph = 0.0
    for i in range(ns):
        t = i / SR
        f = 56.0 + 44.0 * math.exp(-t * 38.0)
        ph += TAU * f / SR
        out[i] += math.sin(ph + 1.5707963) * math.exp(-(20.0 if var == 1 else 15.0) * t)
    kn = lp1(lp1(rauschen(ns, rng), 650.0), 900.0)
    for i in range(ns):
        t = i / SR
        out[i] += 1.6 * kn[i] * math.exp(-48.0 * t)
    if var == 2:
        luft = lp1(lp1(rauschen(ns, rng), 420.0), 420.0)
        for i in range(ns):
            t = i / SR
            a = (1 - math.exp(-t / 0.012)) * math.exp(-9.0 * t)
            out[i] += 2.4 * luft[i] * a
    addiere(buf, pos, out, laut)
    addiere(hall, pos, out, laut * hallanteil)


# ---------- Mischung ----------

def abschluss(dry, wet, spitze_db=SPITZE_DB):
    """Trocken + Hall, Tiefpass, weiches Auslaufen auf null, Spitze auf -3 dBFS."""
    n_ = len(dry)
    m = [dry[i] + wet[i] for i in range(n_)]
    m = biquad(m, "hoch", 28.0)
    m = biquad(biquad(m, "tief", 2400.0), "tief", 2400.0)
    aus = n(0.30)
    for i in range(aus):
        w = 0.5 + 0.5 * math.cos(math.pi * i / aus)
        m[n_ - aus + i] *= w
    sp = max(abs(s) for s in m)
    k = (10 ** (spitze_db / 20.0)) / sp
    return [s * k for s in m]


def schreibe(pfad, daten):
    with wave.open(str(pfad), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(b"".join(int(round(max(-1.0, min(1.0, s)) * 32767)).to_bytes(2, "little", signed=True) for s in daten))


# ---------- Die Klaenge ----------

def fallen_a(h, var):
    rng = random.Random(540_100 + var)
    dauer = 40 / 60.0 + 0.70
    laenge = n(dauer)
    dry = [0.0] * laenge
    send = [0.0] * laenge
    v = geschwindigkeit(h, laenge, +1, 0.28, bis_tick=24)
    fb = (180.0, 520.0) if var == 1 else (240.0, 700.0)
    loesen(dry, send, tp(0), rng, 0.45, var, 0.3)
    sch = schleifen(rng, v, tp(1), tp(24), (110.0, 800.0) if var == 1 else (90.0, 520.0), var, 0.55 if var == 1 else 0.40, 0.55)
    addiere(dry, 0, sch)
    addiere(send, 0, sch, 0.25)
    rasseln(dry, send, rng, v, tp(0), tp(25), 14.0, 70.0, 0.10, 0.34, fb, 75.0 if var == 1 else 55.0, 0.3)
    # Glieder fallen nach dem Aufschlag zu Haufen
    for k, (tick, g) in enumerate(((25.0, 0.18), (27.5, 0.13), (30.0, 0.09), (33.0, 0.06))):
        addiere(dry, tp(tick), klink(rng, rng.uniform(*fb), 80.0), g)
    ha = 0.55 if var == 1 else 0.85
    aufschlag(dry, send, tp(24), rng, 1.0, var, ha)
    aufschlag(dry, send, tp(35), rng, 0.17, var, ha, klein=True)
    aufschlag(dry, send, tp(40), rng, 0.05, var, ha, klein=True)
    nach = Hall(0.55 if var == 1 else 0.85, 0.38).bearbeite(send)
    return abschluss(dry, nach)


def fallen_b(h, var):
    rng = random.Random(540_200 + var)
    dauer = 63 / 60.0 + 0.70
    laenge = n(dauer)
    dry = [0.0] * laenge
    send = [0.0] * laenge
    # Geschwindigkeit: Ruck 1 bis Tick 12, Ruck 2 ab Tick 26. Dazwischen nur
    # das Schwingen der Kette (Hoehe kaum veraendert, Geschwindigkeit klein).
    v = geschwindigkeit(h, laenge, +1, 0.28, bis_tick=47)
    fb = (180.0, 520.0) if var == 1 else (240.0, 700.0)
    band = (110.0, 800.0) if var == 1 else (90.0, 520.0)
    ha = 0.55 if var == 1 else 0.85
    loesen(dry, send, tp(0), rng, 0.45, var, 0.3)
    sch = schleifen(rng, v, tp(1), tp(47), band, var, 0.55 if var == 1 else 0.40, 0.55)
    addiere(dry, 0, sch)
    addiere(send, 0, sch, 0.25)
    rasseln(dry, send, rng, v, tp(0), tp(47), 14.0, 70.0, 0.10, 0.34, fb, 75.0 if var == 1 else 55.0, 0.3)
    # Ruck 1: die Kette faengt das Gitter
    ruck(dry, send, tp(12), rng, 0.62, var, ha)
    # Schwingen: wenige, leiser werdende Glieder
    for tick, g in ((13.5, 0.22), (16.0, 0.16), (18.5, 0.13), (21.5, 0.09), (24.0, 0.06)):
        addiere(dry, tp(tick), klink(rng, rng.uniform(*fb), 65.0), g)
    addiere(send, tp(13.5), klink(rng, 300.0, 65.0), 0.1)
    # zweites Loesen: Kette reisst durch, Glieder jagen hinterher
    loesen(dry, send, tp(26), rng, 0.60, var, 0.4)
    for k, (tick, g) in enumerate(((47.0 + 1.0, 0.18), (50.0, 0.13), (53.0, 0.09), (56.0, 0.06))):
        addiere(dry, tp(tick), klink(rng, rng.uniform(*fb), 80.0), g)
    aufschlag(dry, send, tp(47), rng, 1.0, var, ha)
    aufschlag(dry, send, tp(58), rng, 0.17, var, ha, klein=True)
    aufschlag(dry, send, tp(63), rng, 0.05, var, ha, klein=True)
    nach = Hall(0.55 if var == 1 else 0.85, 0.38).bearbeite(send)
    return abschluss(dry, nach)


def heben(h, var):
    rng = random.Random(540_300 + var)
    dauer = 126 / 60.0 + 0.80
    laenge = n(dauer)
    dry = [0.0] * laenge
    send = [0.0] * laenge
    v = geschwindigkeit(h, laenge, -1, 0.0298, bis_tick=126)
    # Winde und Kette: Startklacken, Kette strafft sich (Tick 0 bis 6)
    loesen(dry, send, tp(0), rng, 0.35, var, 0.3)
    straff = lp1(biquad(rauschen(tp(6), rng), "band", 380.0, 1.2), 900.0)
    for i in range(len(straff)):
        w = math.sin(math.pi * i / len(straff)) ** 1.5
        dry[i + tp(0)] += straff[i] * w * 0.9
    addiere(dry, tp(5), klink(rng, 255.0, 40.0), 0.25)
    # Rumpeln der Trommel, Pegel folgt der Geschwindigkeit
    rum = lp1(biquad(rauschen(laenge, rng), "tief", 170.0), 170.0)
    for i in range(laenge):
        dry[i] += rum[i] * v[i] * 5.5
    if var == 1:
        # Sperrklinke: Takt folgt der Geschwindigkeit (ein Zaehnchen je 0,07 m Hub)
        ph = 0.0
        zaehler = 0
        for i in range(tp(6), tp(126)):
            ph += (v[i] * 0.0298 * 60.0 / 0.07) / SR  # Zaehne je Sekunde
            if ph >= 1.0:
                ph -= 1.0
                zaehler += 1
                ns = n(0.06)
                kn = lp1(lp1(rauschen(ns, rng), 900.0), 1300.0)
                seg = [(1.1 * kn[j] * math.exp(-130.0 * j / SR)
                        + 0.7 * math.sin(TAU * 165.0 * j / SR) * math.exp(-90.0 * j / SR)) for j in range(ns)]
                addiere(dry, i, seg, 0.42 * rng.uniform(0.92, 1.0))
        # Kette liegt dazwischen, selten
        rasseln(dry, send, rng, v, tp(6), tp(126), 3.0, 9.0, 0.05, 0.13, (190.0, 460.0), 65.0, 0.2)
    else:
        # Kette ueber die Trommel: dichter, gleichmaessiger Glieder-Lauf, kein Klinken
        ph = 0.0
        for i in range(tp(6), tp(126)):
            ph += (v[i] * 34.0) / SR
            if ph >= 1.0:
                ph -= 1.0
                seg = klink(rng, rng.uniform(200.0, 480.0), 85.0, dauer=0.06, rauh=0.3)
                addiere(dry, i, seg, 0.24 * rng.uniform(0.6, 1.0))
        # Reibung: tiefes, knarrendes Band
        kn = rauschen(laenge, rng)
        kn = lp1(biquad(biquad(kn, "hoch", 80.0), "tief", 420.0), 420.0)
        for i in range(laenge):
            dry[i] += kn[i] * v[i] * 2.2
    # Einrasten im Tick 126
    ha = 0.5 if var == 1 else 0.7
    # Dauerteil vor dem Riegel sauber beenden (20 ms Ausblendung vor Tick 126)
    aus = n(0.02)
    p126 = tp(126)
    for i in range(aus):
        dry[p126 - aus + i] *= 0.5 + 0.5 * math.cos(math.pi * i / aus)
    riegel(dry, send, p126, rng, 1.5, var, ha)
    nach = Hall(0.5 if var == 1 else 0.75, 0.40).bearbeite(send)
    return abschluss(dry, nach)


def main():
    ziel = Path(sys.argv[1]) if len(sys.argv) > 1 else HIER
    ziel.mkdir(parents=True, exist_ok=True)
    daten = lade_zeitverlauf()
    for var in (1, 2):
        schreibe(ziel / f"fallen-a-variante-{var}.wav", fallen_a(daten["fallen-a"], var))
        schreibe(ziel / f"fallen-b-variante-{var}.wav", fallen_b(daten["fallen-b"], var))
        schreibe(ziel / f"heben-variante-{var}.wav", heben(daten["heben"], var))


if __name__ == "__main__":
    main()
