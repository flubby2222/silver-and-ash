"""Skillpunkt-Klang fuer das Spiel (Issue 375): die Endfassung 'Schlag mit Chor' aus der Hoerprobe (Issue 372).

Das Skript kopiert die Synthese nicht, es laedt sie aus dem Probestueck
design/proben/audio/skillpunkt/runde2/bauen.py (schlag_chor_final, normieren, schreiben) und schreibt
assets/audio/skillpunkt.wav. Danach vergleicht es die Datei Byte fuer Byte mit der abgenommenen Vorlage
3-schlag-chor-final.wav und bricht ab, wenn sie abweicht. Nur Standardbibliothek, kein Zufall.

Aufruf (aus dem Repository-Stamm), danach die Pruefsummen neu schreiben:
    python tools/audio/skillpunkt.py
    python tools/models/checksums.py
"""
import hashlib
import importlib.util
import os
import sys

STAMM = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
PROBE = os.path.join(STAMM, "design", "proben", "audio", "skillpunkt", "runde2")
VORLAGE = os.path.join(PROBE, "3-schlag-chor-final.wav")
ZIEL = os.path.join(STAMM, "assets", "audio", "skillpunkt.wav")


def probe_laden():
    spec = importlib.util.spec_from_file_location("skillpunkt_runde2_bauen", os.path.join(PROBE, "bauen.py"))
    modul = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(modul)
    return modul


def sha256(pfad):
    with open(pfad, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def main():
    bauen = probe_laden()
    bauen.schreiben(ZIEL, bauen.normieren(bauen.schlag_chor_final()))
    erzeugt, vorlage = sha256(ZIEL), sha256(VORLAGE)
    print(f"{os.path.relpath(ZIEL, STAMM)}: {erzeugt}")
    print(f"Vorlage {os.path.relpath(VORLAGE, STAMM)}: {vorlage}")
    if erzeugt != vorlage:
        print("FEHLER: Die erzeugte Datei weicht von der abgenommenen Vorlage ab.", file=sys.stderr)
        return 1
    print("Byte-gleich zur Vorlage.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
