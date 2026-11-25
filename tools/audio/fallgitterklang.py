"""Baut die vom Menschen gewaehlten Klaenge des Fallgitters (Issue 558, Hoerprobe Runde 2 aus Issue 540) nach
assets/audio/.

Das Skript kopiert die Synthese nicht, es laedt sie aus dem Probestueck
design/proben/audio/fallgitter-540/runde-2/bauen.py (das seinerseits ../bauen.py und ../zeitverlauf.csv nutzt) und baut
die zwei gewaehlten Fassungen mit denselben Seeds und Parametern wie die Probe:

    Fallen: fallen-b-rutschen-variante-1.wav -> fallgitter-fallen.wav ("Variante 1 - ohne Kette", 63 Ticks)
    Heben:  heben-rutschen-variante-2.wav    -> fallgitter-heben.wav  ("Variante 2 - glatter", 126 Ticks)

Jede Datei muss Byte fuer Byte die Pruefsumme haben, die runde-2/wav.sha256 fuer die abgenommene Fassung nennt;
sonst bricht das Skript ab und schreibt nichts. Nur Standardbibliothek, Zufall nur mit festen Seeds (in der Probe).

Die Dateien bleiben WAV (16 Bit, mono, 44,1 kHz) wie die der Bossraum-Tuer und der Saeule: so sind sie unveraendert die
Fassung, die der Mensch gehoert hat. Der Pegel steht in den Dateien (Fallen Spitze -3,0 dBFS, Mittel -23,1 dBFS; Heben
Spitze -9,8 dBFS, Mittel -24,0 dBFS), gespielt wird mit SoundBoard.SoundVolume.

    python tools/audio/fallgitterklang.py           # schreibt assets/audio/fallgitter-*.wav
    python tools/audio/fallgitterklang.py --pruefen # baut neu und vergleicht mit assets/audio

Danach: python tools/models/checksums.py (schreibt audio.sha256 neu).
"""
import hashlib
import importlib.util
import os
import sys
import tempfile

WURZEL = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PROBE = os.path.join(WURZEL, "design", "proben", "audio", "fallgitter-540", "runde-2")
ZIEL = os.path.join(WURZEL, "assets", "audio")

# Datei der Probe -> Dateiname im Spiel (Urteil des Menschen zu Runde 2, 2026-11-21)
GEWAEHLT = {
    "fallen-b-rutschen-variante-1.wav": "fallgitter-fallen.wav",
    "heben-rutschen-variante-2.wav": "fallgitter-heben.wav",
}


def sha256(daten):
    return hashlib.sha256(daten).hexdigest()


def abgenommen():
    """Die Pruefsummen der Probe aus runde-2/wav.sha256: {Dateiname: sha256}."""
    summen = {}
    with open(os.path.join(PROBE, "wav.sha256"), encoding="utf-8") as f:
        for zeile in f:
            if zeile.strip():
                wert, name = zeile.split(maxsplit=1)
                summen[name.strip().lstrip("*")] = wert
    return summen


def probe_laden():
    """Laedt bauen.py der Runde 2; dessen 'import bauen' findet bauen.py der Runde 1 im Ordner darueber."""
    sys.path.insert(0, os.path.dirname(PROBE))
    try:
        spec = importlib.util.spec_from_file_location("fallgitter_r2_bauen", os.path.join(PROBE, "bauen.py"))
        modul = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(modul)
    finally:
        sys.path.remove(os.path.dirname(PROBE))
    return modul


def bauen():
    """Baut die zwei gewaehlten Fassungen wie die Probe und liefert {Dateiname im Spiel: (Probe, WAV-Bytes)}."""
    r2 = probe_laden()
    verlauf = r2.B.lade_zeitverlauf()
    fassungen = {
        "fallen-b-rutschen-variante-1.wav": lambda: r2.fallen_b(verlauf["fallen-b"], 1),
        "heben-rutschen-variante-2.wav": lambda: r2.heben(verlauf["heben"], 2),
    }
    ergebnis = {}
    with tempfile.TemporaryDirectory() as ordner:
        for probe, spiel in GEWAEHLT.items():
            pfad = os.path.join(ordner, probe)
            r2.B.schreibe(pfad, fassungen[probe]())
            with open(pfad, "rb") as f:
                ergebnis[spiel] = (probe, f.read())
    return ergebnis


def main():
    summen = abgenommen()
    neu = bauen()
    abweichend = [probe for probe, daten in neu.values() if sha256(daten) != summen[probe]]
    for spiel, (probe, daten) in neu.items():
        print(f"{probe} -> {spiel}: {sha256(daten)}")
    if abweichend:
        print("FEHLER: weicht von runde-2/wav.sha256 ab:", abweichend, file=sys.stderr)
        return 1
    print("Beide Byte-gleich zur abgenommenen Probe (runde-2/wav.sha256).")
    if "--pruefen" in sys.argv:
        schlecht = []
        for spiel, (_, daten) in neu.items():
            pfad = os.path.join(ZIEL, spiel)
            if not os.path.exists(pfad) or open(pfad, "rb").read() != daten:
                schlecht.append(spiel)
        print("bitgleich zu assets/audio:", not schlecht, schlecht)
        return 1 if schlecht else 0
    for spiel, (_, daten) in neu.items():
        with open(os.path.join(ZIEL, spiel), "wb") as f:
            f.write(daten)
        print("geschrieben:", spiel, len(daten), "Bytes")
    return 0


if __name__ == "__main__":
    sys.exit(main())
