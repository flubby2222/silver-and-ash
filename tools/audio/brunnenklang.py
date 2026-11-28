"""Baut die vom Menschen gewaehlten Klaenge des Brunnens "Sirius' Relief" (Issue 815, Hoerprobe aus Issue 438) nach
assets/audio/.

Uebernahme aus dem Probestueck design/proben/audio/brunnen-438/bauen.py (Branch feat/brunnen-hoerprobe-438, Stand
e7473d81). Das Probestueck liegt nicht auf main, deshalb steht die Synthese hier noch einmal, aber nur fuer die drei
gewaehlten Fassungen, mit denselben Seeds und Parametern wie die Probe (Urteil des Menschen vom 2026-11-20):

    Wurf:   wurf-a-muenze-plumpst.wav   -> brunnen-wurf.wav   ("Muenze plumpst", 0,80 s)
    Glueck: glueck-a-funkeln-steigt.wav -> brunnen-glueck.wav ("Funkeln steigt", 2,70 s)
    Wasser: wasser-c-gurgeln.wav        -> brunnen-wasser.wav ("Gurgeln", Schleife von 12 s)

Fuer "kein Glueck" gibt es keinen Klang. Jede Datei muss Byte fuer Byte die Pruefsumme der abgenommenen Fassung haben
(ABGENOMMEN unten, aus wav.sha256 der Probe); sonst bricht das Skript ab und schreibt nichts. Nur Standardbibliothek,
Zufall nur mit festen Seeds.

Die Dateien bleiben WAV (16 Bit, mono, 44,1 kHz): so sind sie unveraendert die Fassung, die der Mensch gehoert hat.
Der Pegel steht in den Dateien (Wurf und Glueck Spitze -6,0 dBFS; Wasser Spitze -18,0 dBFS, Mittel -36,2 dBFS). Das
Wasser liegt im Mittenband (300 bis 3000 Hz), das die Audio-Bible fuer Ambiente frei haelt: es ist deshalb leise und
nur im Nahbereich zu hoeren (well.waterRange und well.waterVolume in assets/balance.json).

    python tools/audio/brunnenklang.py           # schreibt assets/audio/brunnen-*.wav
    python tools/audio/brunnenklang.py --pruefen # baut neu und vergleicht mit assets/audio

Danach: python tools/models/checksums.py (schreibt audio.sha256 neu).
"""
import hashlib
import io
import math
import os
import random
import struct
import sys
import wave

WURZEL = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ZIEL = os.path.join(WURZEL, "assets", "audio")

SR = 44100
TAU = 2.0 * math.pi
SPITZE_EINZEL_DB = -6.0
SPITZE_SCHLEIFE_DB = -18.0
SCHLEIFE = 12.0

# Dateiname im Spiel -> (Datei der Probe, ihre Pruefsumme aus design/proben/audio/brunnen-438/wav.sha256)
ABGENOMMEN = {
    "brunnen-wurf.wav": ("wurf-a-muenze-plumpst.wav",
                         "49060bdce87e7b8e766a31f7662700e28efc4bcced282011fdb879b1ef85cf95"),
    "brunnen-glueck.wav": ("glueck-a-funkeln-steigt.wav",
                           "4d1f76faf0720857fc88f00ee00f9243226a3b4d900b2326ad06334dbb98988d"),
    "brunnen-wasser.wav": ("wasser-c-gurgeln.wav",
                           "f6208cc73618af2bcaf1a2a3c41dc2a0417e238fc2b82353f4cc7766131a413b"),
}


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


def wav_bytes(x):
    """Die Abtastwerte als WAV-Datei (16 Bit, mono, 44,1 kHz) im Speicher."""
    daten = b"".join(struct.pack("<h", max(-32768, min(32767, int(round(v * 32767))))) for v in x)
    puffer = io.BytesIO()
    with wave.open(puffer, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(daten)
    return puffer.getvalue()


# ---------- Die drei gewaehlten Fassungen ----------

def wurf():
    """Wurf A, Muenze plumpst: ein kurzes dumpfes Metall-Tick (Teiltoene 430 bis 1,5 kHz),
    dazu eine tiefe Wasserblase, die kurz danach aufsteigt. Zwei Schichten, kurzer Hall."""
    tick = teiltoene(0.5, 430.0, ((1.0, 1.0, 1.0), (2.13, 0.5, 1.4), (3.4, 0.25, 2.0)), abfall=14.0, tief=1800.0)
    bl = blubb(0.45, 150.0, steig=1.0, abfall=13.0)
    m = mische(tick, versetze(skaliere(bl, 1.3), 0.04))
    return abschluss(hall(m, 0.3), aus=0.28)


def glueck():
    """Glueck A, Funkeln steigt: viele leise Sinus-Koernchen (Moll-Pentatonik 294 bis 880 Hz),
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


def wasser():
    """Wasser C, Gurgeln: dichtes Blubbern (etwa 28 kurze Blasen je Sekunde, 90 bis 320 Hz) ueber einem
    leisen dunklen Bett; wirkt wie Wasser, das in eine Schale laeuft. Keine einzelne Blase sticht heraus.
    Eine Schleife: das Bett ist kreisfoermig gefiltert, die Blasen laufen ueber die Naht herum."""
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


FASSUNGEN = {
    "brunnen-wurf.wav": wurf,
    "brunnen-glueck.wav": glueck,
    "brunnen-wasser.wav": wasser,
}


def sha256(daten):
    return hashlib.sha256(daten).hexdigest()


def bauen():
    """Baut die drei Fassungen und liefert {Dateiname im Spiel: WAV-Bytes}."""
    return {spiel: wav_bytes(fassung()) for spiel, fassung in FASSUNGEN.items()}


def main():
    neu = bauen()
    abweichend = [spiel for spiel, daten in neu.items() if sha256(daten) != ABGENOMMEN[spiel][1]]
    for spiel, daten in neu.items():
        print(f"{ABGENOMMEN[spiel][0]} -> {spiel}: {sha256(daten)}")
    if abweichend:
        print("FEHLER: weicht von der abgenommenen Probe (wav.sha256) ab:", abweichend, file=sys.stderr)
        return 1
    print("Alle drei Byte-gleich zur abgenommenen Probe (wav.sha256 aus design/proben/audio/brunnen-438).")
    if "--pruefen" in sys.argv:
        schlecht = []
        for spiel, daten in neu.items():
            pfad = os.path.join(ZIEL, spiel)
            if not os.path.exists(pfad) or open(pfad, "rb").read() != daten:
                schlecht.append(spiel)
        print("bitgleich zu assets/audio:", not schlecht, schlecht)
        return 1 if schlecht else 0
    for spiel, daten in neu.items():
        with open(os.path.join(ZIEL, spiel), "wb") as f:
            f.write(daten)
        print("geschrieben:", spiel, len(daten), "Bytes")
    return 0


if __name__ == "__main__":
    sys.exit(main())
