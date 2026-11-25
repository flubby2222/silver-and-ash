#!/usr/bin/env python3
"""Hörprobe Hunter's Axe (#201), Runde 2: nur das Aufladen des schweren Hiebs.

Urteil zu Runde 1: "Das Glöckchen (Ding) am Ende des Aufladens stört." Das Einrasten
(Eisenschlag, bei B Glockenschlag) wurde als helles "Ding" gehört. Darum endet in
Runde 2 keine Fassung mit einem tonalen, glockigen oder metallischen Ereignis:
  A  ohne Schlussereignis: die Spannung steht am Ende einfach und klingt aus.
  B  dumpfer, tonloser Schlag unter 120 Hz (Faust auf Holz), kein Nachklang.
  C  kurzer Luftstoß und Anziehen von Leder (Rauschen unter 330 Hz), kein Ton.
Der Aufbau davor stammt aus Runde 1: A = Knarzen, B = Brummen, C = Ratsche.
Dazu je Fassung "zusammenhang-<a|b|c>": Aufladen, 0,35 s Pause, gewählter Hieb C
(Runde 1) und Treffer Fleisch C (Runde 1, setzt auf dem Gipfel des Hiebs ein).

Jede Fassung ist eine eigene WAV. Nur Python-Standardbibliothek, fester Seed, daher
bitgleich. Aufruf:  python bauen.py [Zielordner]   (ohne Argument neben dem Skript).
Die Bausteine bis "zu" sind aus Runde 1 übernommen.
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



# ---------- Aufladen, Runde 2 (Spannung 1,0 s; alles unter 400 Hz, kein Schlussklang) ----------

def spann_huell(i, laenge, hoch=1.6):
    """Wächst bis zum Ende, ohne Sprung (Potenz von u)."""
    return (i / laenge) ** hoch


def tief_stoss(sek, rng, grenze, abfall, anstieg=0.003):
    """Tonloser Schlag aus Rauschen, doppelt tiefpassgefiltert (keine Tonhöhe, kein Ring)."""
    x = rauschen(sek, rng)
    x = biquad(biquad(x, "tief", grenze), "tief", grenze)
    x = [v * math.exp(-abfall * i / SR) * min(1.0, i / n(anstieg)) for i, v in enumerate(x)]
    return norm(x)


def knarz_zug(rng, zug):
    """Rauschband 150 -> 320 Hz mit 14-Hz-Körnung, wächst bis zum Ende (aus Runde 1, A)."""
    laenge = n(zug)
    kn = stufig_band(rauschen(zug, rng), 150.0, 320.0, 2.0)
    kor = biquad(rauschen(zug, rng), "tief", 14.0)
    g = max(abs(v) for v in kor) or 1.0
    return norm([kn[i] * spann_huell(i, laenge, 1.4) * (0.6 + 0.4 * kor[i] / g) for i in range(laenge)])


def aufladen_a():
    """A, Spannung steht (1,0 s Zug + 0,45 s Auslauf): Knarzen von Holz und Leder (Band 150 -> 320 Hz,
    rauh durch 14-Hz-Körnung) und Brummen 62 -> 96 Hz wachsen bis 1,0 s, ein durchgehendes Signal.
    Danach bleibt die Tonlage stehen und der Pegel fällt in 0,45 s mit einem Kosinus auf null.
    Kein Schlag, kein Einrasten, kein Ton am Ende."""
    rng = random.Random(2121)
    zug, aus = 1.0, 0.45
    laenge = n(zug + aus)
    lz = n(zug)

    def huell(i, hoch):
        if i < lz:
            return spann_huell(i, lz, hoch)
        return 0.5 + 0.5 * math.cos(math.pi * (i - lz) / (laenge - lz))

    kn = stufig_band(rauschen(zug + aus, rng), 150.0, 320.0, 2.0)
    kor = biquad(rauschen(zug + aus, rng), "tief", 14.0)
    g = max(abs(v) for v in kor) or 1.0
    kn = norm([kn[i] * huell(i, 1.4) * (0.6 + 0.4 * kor[i] / g) for i in range(laenge)])
    ph = 0.0
    brumm = []
    for i in range(laenge):
        ph += TAU * (62.0 + 34.0 * min(1.0, i / lz)) / SR
        brumm.append(sum(math.sin(k * ph) / k for k in range(1, 5)) * huell(i, 1.3))
    spur = mische(mal(kn, 0.8), mal(norm(brumm), 0.7))
    return abschluss(hall(spur, 0.1, 0.25), aus=0.3)


def aufladen_b():
    """B, Brummen + Faustschlag (1,0 s Zug, Schlag, kurzer Hall): glatter Dröhnton steigt 48 -> 105 Hz
    (sechs Obertöne nach 1/k, tiefpass 650 Hz), Pegel mit u^2. Schluss: dumpfer, tonloser Schlag, wie
    eine Faust auf Holz: Rauschen doppelt tiefpassgefiltert bei 110 Hz, Anstieg 3 ms, fällt mit
    exp(-48/s); kein Ring, kein Ton. Der Zug endet 25 ms vor dem Schlag."""
    rng = random.Random(2123)
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
    schlag = tief_stoss(0.2, rng, 110.0, 48.0, anstieg=0.003)
    spur = mische(mal(dr, 0.9), versetze(mal(schlag, 2.6), zug + 0.01))
    return abschluss(hall(spur, 0.1, 0.25), aus=0.25)


def aufladen_c():
    """C, Ratsche + Luftstoß (1,0 s Zug, Schluss 0,3 s): dumpfe Holzschläge werden schneller
    (150 -> 42 ms) und höher (120 -> 260 Hz) und lauter, dazu Brummen 70 -> 100 Hz. Schluss: ein
    kurzer Luftstoß (Rauschen, Band 300 -> 170 Hz, zweifach tiefpass 330 Hz, 8 ms Anstieg, fällt mit
    exp(-24/s)) mit einem trockenen Lederzug (Rauschband 170 Hz, 40 ms): Luft und Leder, kein Ton."""
    rng = random.Random(2124)
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
    luft = stufig_band(rauschen(0.3, rng), 300.0, 170.0, 0.9)
    luft = norm(biquad(biquad(luft, "tief", 330.0), "tief", 330.0))
    luft = [v * math.exp(-24.0 * i / SR) * min(1.0, i / n(0.008)) for i, v in enumerate(luft)]
    leder = tonstoss(0.04, rng, 170.0, 1.0, 90.0)
    for i, v in enumerate(norm(luft)):
        spur[n(zug) + i] += v * 1.1
    for i, v in enumerate(leder):
        spur[n(zug) + n(0.01) + i] += v * 0.5
    return abschluss(hall(spur, 0.1, 0.25), aus=0.25)


# ---------- Pegel, Dateien ----------

AUFLADEN = [("a-spannung-steht", aufladen_a), ("b-faustschlag", aufladen_b), ("c-luftstoss", aufladen_c)]
ZIEL_RMS = 10 ** (-29.1 / 20.0)  # Mittel der drei Aufladen-Fassungen aus Runde 1
PAUSE_NACH_AUFLADEN = 0.35
TREFFER_NACH_HIEBSTART = 0.45
SPITZE_ZUSAMMENHANG = -1.5


def lies(pfad):
    with wave.open(str(pfad)) as w:
        k = w.getnframes()
        d = struct.unpack("<%dh" % k, w.readframes(k))
    return [v / 32768.0 for v in d]


def main():
    hier = Path(__file__).resolve().parent
    ziel = Path(sys.argv[1]) if len(sys.argv) > 1 else hier
    ziel.mkdir(parents=True, exist_ok=True)
    runde1 = hier.parent / "runde-1"
    hieb = lies(runde1 / "hieb-c-luftdruck.wav")
    treffer = lies(runde1 / "treffer-fleisch-c-nass.wav")
    # alle drei auf das mittlere Pegel-Ziel von Runde 1 (-29,1 dB RMS); die Spitzen ergeben sich
    fassungen = [mal(x, ZIEL_RMS / rms(x)) for x in (fn() for _, fn in AUFLADEN)]
    for (name, _), x in zip(AUFLADEN, fassungen):
        datei = "aufladen-%s.wav" % name
        schreibe(ziel / datei, x)
        print(datei)
        spur = list(x) + [0.0] * n(PAUSE_NACH_AUFLADEN)
        t_hieb = len(spur)
        spur += hieb
        s0 = t_hieb + n(TREFFER_NACH_HIEBSTART)
        spur += [0.0] * max(0, s0 + len(treffer) - len(spur))
        for i, v in enumerate(treffer):
            spur[s0 + i] += v
        s = max(abs(v) for v in spur)
        lim = 10 ** (SPITZE_ZUSAMMENHANG / 20.0)
        if s > lim:
            spur = mal(spur, lim / s)
        datei = "zusammenhang-%s.wav" % name[0]
        schreibe(ziel / datei, spur)
        print(datei)


if __name__ == "__main__":
    main()
