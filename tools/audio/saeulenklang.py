"""Baut die vom Menschen gewaehlten Saeulen-Klaenge (Issue 605, Hoerprobe Runde 4) nach assets/audio/.

Das Skript kopiert die Synthese nicht, es laedt sie aus dem Probestueck
design/proben/audio/saeule-klang-605/r4/bauen.py (das seinerseits ../bauen.py aus Runde 1 nutzt), baut alle neun
Fassungen der Runde wie die Probe in einen leeren Ordner und nimmt davon die drei gewaehlten:

    Treffer Stufe 1: saeule-treffer-stufe1-c.wav -> saeule-treffer-stufe1.wav (die dumpfe Fassung)
    Treffer Stufe 2: saeule-treffer-stufe2-b.wav -> saeule-treffer-stufe2.wav (bewusst die hellere Fassung)
    Bruch:           saeule-bruch-c.wav          -> saeule-bruch.wav          (die dumpfe Fassung)

Jede Datei muss Byte fuer Byte die Pruefsumme haben, die r4/wav.sha256 fuer die abgenommene Fassung nennt; sonst
bricht das Skript ab und schreibt nichts. Nur Standardbibliothek, Zufall nur mit festen Seeds.

    python tools/audio/saeulenklang.py           # schreibt assets/audio/saeule-*.wav
    python tools/audio/saeulenklang.py --pruefen # baut neu und vergleicht mit assets/audio

Danach: python tools/models/checksums.py (schreibt audio.sha256 neu).
"""
import hashlib
import importlib.util
import os
import sys
import tempfile

WURZEL = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PROBE = os.path.join(WURZEL, "design", "proben", "audio", "saeule-klang-605", "r4")
ZIEL = os.path.join(WURZEL, "assets", "audio")

# Datei der Probe -> Dateiname im Spiel (Urteil des Menschen zu Runde 4, 2026-11-23)
GEWAEHLT = {
    "saeule-treffer-stufe1-c.wav": "saeule-treffer-stufe1.wav",
    "saeule-treffer-stufe2-b.wav": "saeule-treffer-stufe2.wav",
    "saeule-bruch-c.wav": "saeule-bruch.wav",
}


def sha256(daten):
    return hashlib.sha256(daten).hexdigest()


def abgenommen():
    """Die Pruefsummen der Probe aus r4/wav.sha256: {Dateiname: sha256}."""
    summen = {}
    with open(os.path.join(PROBE, "wav.sha256"), encoding="utf-8") as f:
        for zeile in f:
            if zeile.strip():
                wert, name = zeile.split(maxsplit=1)
                summen[name.strip().lstrip("*")] = wert
    return summen


def bauen():
    """Baut die Runde wie die Probe und liefert {Dateiname im Spiel: WAV-Bytes} der drei gewaehlten Fassungen."""
    spec = importlib.util.spec_from_file_location("saeule_r4_bauen", os.path.join(PROBE, "bauen.py"))
    modul = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(modul)
    with tempfile.TemporaryDirectory() as ordner:
        alt = sys.argv
        sys.argv = [alt[0], ordner]
        try:
            modul.main()
        finally:
            sys.argv = alt
        ergebnis = {}
        for probe, spiel in GEWAEHLT.items():
            with open(os.path.join(ordner, probe), "rb") as f:
                ergebnis[spiel] = (probe, f.read())
    return ergebnis


def main():
    summen = abgenommen()
    neu = bauen()
    abweichend = [probe for probe, daten in neu.values() if sha256(daten) != summen[probe]]
    for spiel, (probe, daten) in neu.items():
        print(f"{probe} -> {spiel}: {sha256(daten)}")
    if abweichend:
        print("FEHLER: weicht von r4/wav.sha256 ab:", abweichend, file=sys.stderr)
        return 1
    print("Alle drei Byte-gleich zur abgenommenen Probe (r4/wav.sha256).")
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
