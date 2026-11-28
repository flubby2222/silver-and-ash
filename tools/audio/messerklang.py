"""Baut die vom Menschen gewaehlten Klaenge der Wurfmesser (Issue 820, Hoerprobe aus Issue 204) nach assets/audio/.

Uebernahme aus dem Probestueck design/proben/audio/throwing-knives-204/bauen.py (Branch
chore/throwing-knives-hoerprobe-204, Stand 27f1202). Das Probestueck liegt nicht auf main, deshalb steht seine Synthese
hier noch einmal, mit denselben Seeds und Parametern (Urteil des Menschen vom 2026-11-28):

    Wurf:           wurf-a-stoff-schwirren.wav            -> wurfmesser-wurf.wav            ("a Stoff, Schwirren")
    Treffer Gegner: treffer-gegner-a-stich-zittert.wav    -> wurfmesser-treffer-gegner.wav  ("a Stich, zittert")
    Treffer Stein:  treffer-stein-b-abprallen-schaben.wav -> wurfmesser-treffer-stein.wav   ("b Abprallen, Schaben")

Die Bausteine (Filter, Rauschen, Schlag, Hall, Abschluss) laedt das Skript wie die Probe aus der Machart der
Pflockarmbrust, design/proben/audio/stake-crossbow-203/runde-2/bauen.py (liegt auf main, dort derselbe Stand wie auf dem
Branch der Probe). Die Probe bringt a und b einer Kategorie gemeinsam auf dieselbe mittlere Lautstaerke; der Pegel der
gewaehlten Fassung haengt deshalb auch an der nicht gewaehlten. Darum stehen hier alle sechs Fassungen, geschrieben
werden nur die drei gewaehlten.

Jede Datei muss Byte fuer Byte die Pruefsumme der abgenommenen Fassung haben (ABGENOMMEN unten, aus wav.sha256 der
Probe); sonst bricht das Skript ab und schreibt nichts. Nur Standardbibliothek, Zufall nur mit festen Seeds.
WAV, 16 Bit, mono, 44,1 kHz - unveraendert die Fassung, die der Mensch gehoert hat.

    python tools/audio/messerklang.py           # schreibt assets/audio/wurfmesser-*.wav
    python tools/audio/messerklang.py --pruefen # baut neu und vergleicht mit assets/audio

Danach: python tools/models/checksums.py (schreibt audio.sha256 neu).
"""
import hashlib
import importlib.util
import io
import math
import os
import random
import struct
import sys
import wave

WURZEL = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ZIEL = os.path.join(WURZEL, "assets", "audio")
BAUSTEINE = os.path.join(WURZEL, "design", "proben", "audio", "stake-crossbow-203", "runde-2", "bauen.py")

# Dateiname im Spiel -> (Datei der Probe, ihre Pruefsumme aus design/proben/audio/throwing-knives-204/wav.sha256)
ABGENOMMEN = {
    "wurfmesser-wurf.wav": ("wurf-a-stoff-schwirren.wav",
                            "08af69c8aea5fdf5a5c7978ae4405dc277590a4a430f61794c1eecea4908407b"),
    "wurfmesser-treffer-gegner.wav": ("treffer-gegner-a-stich-zittert.wav",
                                      "ca00bed264d4d95b9fe1f3bf02dba6b7132b0e571db3dd3daa73c4adb5ae9357"),
    "wurfmesser-treffer-stein.wav": ("treffer-stein-b-abprallen-schaben.wav",
                                     "186fced66e54cd14de46d7450f3aafe389311d293e8d0d04112a9326b99f8031"),
}

# Zielwerte der Probe aus dem Bestand: (mittlere Lautstaerke dB, Spitzengrenze dBFS)
PEGEL = {"wurf": (-22.7, -6.0), "gegner": (-22.1, -4.5), "stein": (-23.0, -4.5)}

# Dateiname im Spiel -> (Kategorie, Platz der gewaehlten Fassung im Paar a, b)
GEWAEHLT = {
    "wurfmesser-wurf.wav": ("wurf", 0),
    "wurfmesser-treffer-gegner.wav": ("gegner", 0),
    "wurfmesser-treffer-stein.wav": ("stein", 1),
}


def bausteine_laden():
    spec = importlib.util.spec_from_file_location("armbrust_runde_2", BAUSTEINE)
    modul = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(modul)
    return modul


B = bausteine_laden()
SR = B.SR
n, mal, mische, versetze, norm = B.n, B.mal, B.mische, B.versetze, B.norm
biquad, rauschen, klopf, tonstoss, zischen = B.biquad, B.rauschen, B.klopf, B.tonstoss, B.zischen
schwingung, koernung, hall, fertig, rms = B.schwingung, B.koernung, B.hall, B.fertig, B.rms


def band_kurve(x, freqs, q):
    """Bandpass, dessen Mitte ueber die Laenge der Reihe nach durch `freqs` faehrt."""
    out = [0.0] * len(x)
    letzt = len(x) - 1 or 1
    stufen = len(freqs)
    for s, fc in enumerate(freqs):
        y = biquad(x, "band", fc, q)
        for i in range(len(x)):
            w = 1.0 - abs(i / letzt * (stufen - 1) - s)
            if w > 0.0:
                out[i] += y[i] * w
    return out


def huelle(x, f):
    return [v * f(i / max(1, len(x) - 1), i / SR) for i, v in enumerate(x)]


# ---------- Wurf ----------

def wurf_a():
    """Gewaehlt. Stoff-Schwirren: breites Rauschband (500 steigt auf 1,5 kHz und faellt auf 800 Hz), an- und
    abschwellend (0,34 s), darunter ein koerniger Luftzug um 250 Hz."""
    rng = random.Random(2041)
    schwirr = band_kurve(rauschen(0.34, rng), [500.0, 800.0, 1200.0, 1500.0, 1200.0, 800.0], 1.2)
    schwirr = norm(huelle(schwirr, lambda u, t: math.sin(math.pi * u ** 0.7) ** 2))
    zug = biquad(rauschen(0.34, rng), "band", 250.0, 1.0)
    zug = koernung(zug, rng, 90.0, 0.6)
    zug = norm(huelle(zug, lambda u, t: math.sin(math.pi * u ** 0.8) ** 2))
    return hall(mische(mal(schwirr, 0.9), mal(zug, 0.35)), 0.06, 0.15)


def wurf_b():
    """Nicht gewaehlt, nur fuer den gemeinsamen Pegel. Klinge zischt: schmaleres Band in 0,2 s, vorn ein Abwurf-Stoss."""
    rng = random.Random(2042)
    zisch = band_kurve(rauschen(0.2, rng), [900.0, 1500.0, 2200.0, 1700.0, 1000.0], 3.0)
    zisch = norm(huelle(zisch, lambda u, t: min(1.0, t / 0.02) * math.exp(-9.0 * max(0.0, t - 0.02))))
    abwurf = klopf(0.08, 85.0, 45.0, start=1.5, fall=70.0)
    return hall(mische(mal(zisch, 0.9), mal(abwurf, 0.35)), 0.06, 0.15)


# ---------- Treffer im Gegner ----------

def gegner_a():
    """Gewaehlt. Stich mit Nachzittern: Anschlag (650 Hz), Schlag 95 Hz, kurzes Nachgleiten, dazu das Zittern des
    steckenden Messers (210 Hz mit Oberton 2,3-fach, 0,16 s)."""
    rng = random.Random(2051)
    stich = tonstoss(0.03, rng, 650.0, 0.9, 140.0)
    schlag = klopf(0.16, 95.0, 24.0, start=1.7, fall=60.0)
    nass = versetze(zischen(0.1, rng, 500.0, 250.0, 1.0, 20.0, anstieg=0.005), 0.015)
    zittern = schwingung(0.16, 210.0, 22.0, [(1, 1.0, 1.0), (2.3, 0.3, 2.0)], start=1.0, fall=30.0, tief=1000.0)
    return mische(mal(stich, 0.7), mal(schlag, 1.0), mal(nass, 0.3), versetze(mal(zittern, 0.3), 0.012))


def gegner_b():
    """Nicht gewaehlt, nur fuer den gemeinsamen Pegel. Dumpfer Einschlag: tiefer Koerper (66 Hz), fuenf kleine Koerner."""
    rng = random.Random(2052)
    koerper = klopf(0.18, 66.0, 20.0, start=1.9, fall=55.0)
    koerner = [0.0] * n(0.12)
    for _ in range(5):
        t = rng.uniform(0.0, 0.07)
        k = mal(tonstoss(rng.uniform(0.015, 0.03), rng, rng.uniform(280.0, 700.0), 1.1, 170.0), rng.uniform(0.35, 1.0))
        for i, v in enumerate(k):
            if n(t) + i < len(koerner):
                koerner[n(t) + i] += v
    return mische(mal(koerper, 1.0), mal(koerner, 0.55))


# ---------- Treffer an Stein ----------

def metallspur(sek, teile, abfall):
    """Kurzes unharmonisches Klirren aus drei sehr schnell abklingenden Teiltoenen."""
    out = [0.0] * n(sek)
    for f, a in teile:
        for i in range(len(out)):
            t = i / SR
            out[i] += a * math.sin(B.TAU * f * t) * math.exp(-abfall * t)
    return norm(out)


def stein_a():
    """Nicht gewaehlt, nur fuer den gemeinsamen Pegel. Klirren und dreimal Aufspringen."""
    rng = random.Random(2061)
    anschlag = tonstoss(0.03, rng, 1500.0, 0.7, 150.0)
    klirr = metallspur(0.06, [(1100.0, 1.0), (1730.0, 0.7), (2410.0, 0.5)], 110.0)
    schlag = klopf(0.1, 120.0, 35.0, start=1.5, fall=70.0)
    teile = [mal(anschlag, 0.8), mal(klirr, 0.5), mal(schlag, 0.7)]
    for t, f, a in ((0.10, 520.0, 0.7), (0.19, 420.0, 0.4), (0.27, 340.0, 0.2)):
        klack = mische(mal(tonstoss(0.04, rng, f, 1.0, 100.0), a), mal(klopf(0.06, 110.0, 60.0, start=1.3, fall=80.0), a * 0.5))
        teile.append(versetze(klack, t))
    return hall(mische(*teile), 0.1, 0.25)


def stein_b():
    """Gewaehlt. Abprallen und Schaben: Anschlag (Rauschen 1,8 kHz), Schlag (100 Hz), dann koerniges Schaben
    (1,9 kHz faellt auf 800 Hz, 0,14 s), am Ende ein einzelnes Aufklacken."""
    rng = random.Random(2062)
    anschlag = tonstoss(0.03, rng, 1800.0, 0.6, 130.0)
    schlag = klopf(0.14, 100.0, 30.0, start=1.6, fall=65.0)
    schaben = zischen(0.14, rng, 1900.0, 800.0, 2.0, 14.0, anstieg=0.006, halten=0.03)
    schaben = koernung(schaben, rng, 190.0, 0.7)
    klack = mische(mal(klopf(0.08, 80.0, 45.0, start=1.4, fall=70.0), 0.8), mal(tonstoss(0.04, rng, 400.0, 1.0, 100.0), 0.5))
    return hall(mische(mal(anschlag, 0.9), mal(schlag, 0.9), versetze(mal(schaben, 0.5), 0.02),
                       versetze(mal(klack, 0.6), 0.2)), 0.1, 0.25)


# ---------- Pegel und Ausgabe ----------

# Kategorie -> (Fassung a, Fassung b, Auslauf in Sekunden)
PAARE = {
    "wurf": (wurf_a, wurf_b, 0.06),
    "gegner": (gegner_a, gegner_b, 0.12),
    "stein": (stein_a, stein_b, 0.12),
}


def auf_ziel(paar, kategorie):
    """Beide Fassungen auf dieselbe mittlere Lautstaerke (Ziel aus dem Bestand), nur nach unten gerechnet, wenn die
    Spitzengrenze es verlangt."""
    ziel_db, spitze_db = PEGEL[kategorie]
    ziel = 10 ** (ziel_db / 20.0)
    grenze = 10 ** (spitze_db / 20.0)
    for x in paar:
        ziel = min(ziel, rms(x) * grenze / max(abs(v) for v in x))
    return [mal(x, ziel / rms(x)) for x in paar]


def wav_bytes(x):
    """Die Abtastwerte als WAV-Datei (16 Bit, mono, 44,1 kHz) im Speicher, wie `schreibe` der Probe."""
    daten = b"".join(struct.pack("<h", max(-32768, min(32767, int(round(v * 32767))))) for v in x)
    puffer = io.BytesIO()
    with wave.open(puffer, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(daten)
    return puffer.getvalue()


def sha256(daten):
    return hashlib.sha256(daten).hexdigest()


def bauen():
    """Baut die drei gewaehlten Fassungen und liefert {Dateiname im Spiel: WAV-Bytes}."""
    paare = {}
    for kategorie, (a, b, auslauf) in PAARE.items():
        paare[kategorie] = auf_ziel([fertig(f(), 0.1, auslauf) for f in (a, b)], kategorie)
    return {spiel: wav_bytes(paare[kategorie][platz]) for spiel, (kategorie, platz) in GEWAEHLT.items()}


def lesen(pfad):
    with open(pfad, "rb") as f:
        return f.read()


def main():
    neu = bauen()
    abweichend = [spiel for spiel, daten in neu.items() if sha256(daten) != ABGENOMMEN[spiel][1]]
    for spiel, daten in neu.items():
        print(f"{ABGENOMMEN[spiel][0]} -> {spiel}: {sha256(daten)}")
    if abweichend:
        print("FEHLER: weicht von der abgenommenen Probe (wav.sha256) ab:", abweichend, file=sys.stderr)
        return 1
    print("Alle drei Byte-gleich zur abgenommenen Probe (wav.sha256 aus design/proben/audio/throwing-knives-204).")
    if "--pruefen" in sys.argv:
        schlecht = [s for s, d in neu.items() if not os.path.exists(os.path.join(ZIEL, s)) or lesen(os.path.join(ZIEL, s)) != d]
        print("bitgleich zu assets/audio:", not schlecht, schlecht)
        return 1 if schlecht else 0
    for spiel, daten in neu.items():
        with open(os.path.join(ZIEL, spiel), "wb") as f:
            f.write(daten)
        print("geschrieben:", spiel, len(daten), "Bytes")
    return 0


if __name__ == "__main__":
    sys.exit(main())
