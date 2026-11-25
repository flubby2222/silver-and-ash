"""Baut die vom Menschen gewaehlten Klaenge der Peitsche (Issue 786, Hoerprobe aus Issue 202) nach assets/audio/.

Das Skript kopiert die Synthese nicht, es laedt sie aus dem Probestueck design/proben/audio/whip-202/runde-1/bauen.py
und baut die fuenf gewaehlten Fassungen mit denselben Seeds, Pegeln und Parametern:

    Schlag:        schlag-c-langes-zischen.wav          -> peitsche-schlag.wav           ("C Langes Zischen")
    Treffer:       treffer-fleisch-b-striemen.wav       -> peitsche-treffer-fleisch.wav  ("B Striemen")
    Harter Treffer treffer-hart-c-stein-rieselt.wav     -> peitsche-treffer-hart.wav     ("C Stein rieselt")
    Aufladen:      aufladen-b-wirbeln.wav               -> peitsche-aufladen.wav         ("B Wirbeln")
    Heranziehen:   heranziehen-c-lederruck.wav          -> peitsche-heranziehen.wav      ("C Lederruck")

Die Probe bringt jede Kategorie auf ihre Spitze und das Aufladen danach auf gleiche mittlere Lautstaerke; darum baut
das Skript je Kategorie alle drei Fassungen und nimmt die gewaehlte. Jede Datei muss Byte fuer Byte die Pruefsumme
haben, die wav.sha256 der Probe fuer die abgenommene Fassung nennt; sonst bricht das Skript ab und schreibt nichts.
Nur Standardbibliothek, Zufall nur mit festen Seeds (in der Probe). WAV, 16 Bit, mono, 44,1 kHz.

    python tools/audio/peitschenklang.py           # schreibt assets/audio/peitsche-*.wav
    python tools/audio/peitschenklang.py --pruefen # baut neu und vergleicht mit assets/audio

Danach: python tools/models/checksums.py (schreibt audio.sha256 neu).
"""
import hashlib
import importlib.util
import os
import sys
import tempfile

WURZEL = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PROBE = os.path.join(WURZEL, "design", "proben", "audio", "whip-202", "runde-1")
ZIEL = os.path.join(WURZEL, "assets", "audio")

# (Kategorie, Fassung) -> Dateiname im Spiel (Urteile des Menschen in Issue 202)
GEWAEHLT = {
    ("schlag", "c-langes-zischen"): "peitsche-schlag.wav",
    ("treffer-fleisch", "b-striemen"): "peitsche-treffer-fleisch.wav",
    ("treffer-hart", "c-stein-rieselt"): "peitsche-treffer-hart.wav",
    ("aufladen", "b-wirbeln"): "peitsche-aufladen.wav",
    ("heranziehen", "c-lederruck"): "peitsche-heranziehen.wav",
}


def sha256(daten):
    return hashlib.sha256(daten).hexdigest()


def abgenommen():
    """Die Pruefsummen der Probe aus wav.sha256: {Dateiname: sha256}."""
    summen = {}
    with open(os.path.join(PROBE, "wav.sha256"), encoding="utf-8") as f:
        for zeile in f:
            if zeile.strip():
                wert, name = zeile.split(maxsplit=1)
                summen[name.strip().lstrip("*")] = wert
    return summen


def probe_laden():
    pfad = os.path.join(PROBE, "bauen.py")
    spec = importlib.util.spec_from_file_location("peitsche_runde_1", pfad)
    modul = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(modul)
    return modul


def fassung(modul, kategorie, name):
    """Die Abtastwerte der Fassung, genau wie main() der Probe sie baut."""
    for kuerzel, _, spitze, gleich, varianten in modul.KATEGORIEN:
        if kuerzel == kategorie:
            for (vname, _), x in zip(varianten, modul.baue_kategorie(spitze, gleich, varianten)):
                if vname == name:
                    return x
    raise KeyError((kategorie, name))


def bauen():
    """Baut die fuenf gewaehlten Fassungen und liefert {Dateiname im Spiel: (Datei der Probe, WAV-Bytes)}."""
    modul = probe_laden()
    ergebnis = {}
    with tempfile.TemporaryDirectory() as ordner:
        for (kategorie, name), spiel in GEWAEHLT.items():
            probe = "%s-%s.wav" % (kategorie, name)
            pfad = os.path.join(ordner, probe)
            modul.schreibe(pfad, fassung(modul, kategorie, name))
            with open(pfad, "rb") as f:
                ergebnis[spiel] = (probe, f.read())
    return ergebnis


def main():
    summen = abgenommen()
    neu = bauen()
    abweichend = [probe for probe, daten in neu.values() if sha256(daten) != summen.get(probe)]
    for spiel, (probe, daten) in neu.items():
        print(f"{probe} -> {spiel}: {sha256(daten)}")
    if abweichend:
        print("FEHLER: weicht von wav.sha256 der Probe ab:", abweichend, file=sys.stderr)
        return 1
    print("Alle fuenf Byte-gleich zur abgenommenen Probe (wav.sha256).")
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
