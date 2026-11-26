"""Baut die vom Menschen gewaehlten Klaenge der Pflockarmbrust (Issue 793, Hoerprobe Runde 2 aus Issue 203) nach assets/audio/.

Das Skript kopiert die Synthese nicht, es laedt sie aus dem Probestueck design/proben/audio/stake-crossbow-203/runde-2/bauen.py
und baut die gewaehlten Fassungen mit denselben Seeds, Pegeln und Parametern:

    Schuss:        schuss-a-sehne-zischen.wav        -> armbrust-schuss.wav           ("A Sehne zischen")
    Treffer:       treffer-fleisch-a-stich.wav       -> armbrust-treffer-fleisch.wav  ("A Stich")
    Harter Treffer treffer-hart-b-stein-bricht.wav   -> armbrust-treffer-hart.wav     ("B Stein bricht")
    Nachladen:     nachladen-b-seilzug.wav           -> armbrust-nachladen.wav        ("B Seilzug")

Schuss, Fleisch und hart sind Byte fuer Byte die abgenommenen Dateien der Probe (Pruefsummen aus wav.sha256 der Probe;
weicht eine ab, bricht das Skript ab und schreibt nichts). Das Nachladen baut das Skript auf die Nachladezeit aus
assets/balance.json (stakeCrossbow.reloadTime): die Probe hat es auf 2,5 s gebaut und nennt `nachladen(variante, T)` zum
Strecken; Griff, Einrasten (T - 0,6 s) und Einlegen (T - 0,4 s) behalten ihre Laenge, nur der Zug dazwischen waechst. Dazu
baut das Skript, wie die Probe, alle drei Fassungen und gleicht sie auf die mittlere Lautstaerke an. Bei T = 2,5 s waere die
Datei Byte-gleich zu nachladen-b-seilzug.wav der Probe (das prueft das Skript mit --pruefen-probe).
Die Haltung des Spiels (StakeCrossbowPose.CockedAt und StakeAt) haengt an diesen 0,6 s und 0,4 s vor dem Ende.
Nur Standardbibliothek, Zufall nur mit festen Seeds (in der Probe). WAV, 16 Bit, mono, 44,1 kHz.

    python tools/audio/armbrustklang.py           # schreibt assets/audio/armbrust-*.wav
    python tools/audio/armbrustklang.py --pruefen # baut neu und vergleicht mit assets/audio

Danach: python tools/models/checksums.py (schreibt audio.sha256 neu).
"""
import hashlib
import importlib.util
import os
import re
import sys
import tempfile
from pathlib import Path

WURZEL = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PROBE = os.path.join(WURZEL, "design", "proben", "audio", "stake-crossbow-203", "runde-2")
ZIEL = os.path.join(WURZEL, "assets", "audio")
BALANCE = os.path.join(WURZEL, "assets", "balance.json")

# Dateiname der Probe -> Dateiname im Spiel (Urteile des Menschen in Issue 203)
UEBERNOMMEN = {
    "schuss-a-sehne-zischen.wav": "armbrust-schuss.wav",
    "treffer-fleisch-a-stich.wav": "armbrust-treffer-fleisch.wav",
    "treffer-hart-b-stein-bricht.wav": "armbrust-treffer-hart.wav",
}
NACHLADEN = "armbrust-nachladen.wav"


def sha256(daten):
    return hashlib.sha256(daten).hexdigest()


def abgenommen():
    summen = {}
    with open(os.path.join(PROBE, "wav.sha256"), encoding="utf-8") as f:
        for zeile in f:
            if zeile.strip():
                wert, name = zeile.split(maxsplit=1)
                summen[name.strip().lstrip("*")] = wert
    return summen


def probe_laden():
    spec = importlib.util.spec_from_file_location("armbrust_runde_2", os.path.join(PROBE, "bauen.py"))
    modul = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(modul)
    return modul


def nachladezeit():
    """stakeCrossbow.reloadTime aus balance.json (die Datei hat //-Kommentare, darum kein JSON-Parser)."""
    with open(BALANCE, encoding="utf-8-sig") as f:
        text = f.read()
    block = re.search(r'"stakeCrossbow"\s*:\s*\{(.*?)\n  \}', text, re.S).group(1)
    return float(re.search(r'"reloadTime"\s*:\s*([0-9.]+)', block).group(1))


def lesen(pfad):
    with open(pfad, "rb") as f:
        return f.read()


def nachladen_bauen(modul, dauer, ordner):
    """Die Fassung B auf die Dauer gebaut; alle drei Fassungen werden auf gleiche Lautstaerke gebracht (wie in der Probe)."""
    stuecke = [modul.fertig(modul.nachladen(v, dauer), 0.15) for v in "abc"]
    stuecke = modul.gleiche_lautstaerke(stuecke, -8.0)
    pfad = os.path.join(ordner, "nachladen-b.wav")
    modul.schreibe(Path(pfad), stuecke[1])
    return lesen(pfad)


def bauen():
    """Liefert {Dateiname im Spiel: WAV-Bytes} und die Abweichungen von wav.sha256 der Probe."""
    modul = probe_laden()
    summen = abgenommen()
    ergebnis, abweichend = {}, []
    with tempfile.TemporaryDirectory() as ordner:
        modul.main.__globals__["sys"].argv = [sys.argv[0], ordner]
        modul.main()
        for probe, spiel in UEBERNOMMEN.items():
            daten = lesen(os.path.join(ordner, probe))
            ergebnis[spiel] = daten
            if sha256(daten) != summen.get(probe):
                abweichend.append(probe)
        if "--pruefen-probe" in sys.argv:
            probe_b = lesen(os.path.join(ordner, "nachladen-b-seilzug.wav"))
            if nachladen_bauen(modul, 2.5, ordner) != probe_b:
                abweichend.append("nachladen-b-seilzug.wav (T = 2,5 s)")
        ergebnis[NACHLADEN] = nachladen_bauen(modul, nachladezeit(), ordner)
    return ergebnis, abweichend


def main():
    neu, abweichend = bauen()
    for spiel, daten in neu.items():
        print(f"{spiel}: {sha256(daten)}")
    if abweichend:
        print("FEHLER: weicht von der abgenommenen Probe ab:", abweichend, file=sys.stderr)
        return 1
    print("Schuss, Fleisch und hart Byte-gleich zur abgenommenen Probe (wav.sha256); Nachladen auf %.2f s gebaut." % nachladezeit())
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
