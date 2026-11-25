"""Baut die vom Menschen gewaehlten Klaenge der Jagdaxt (Issue 776, Hoerprobe aus Issue 201) nach assets/audio/.

Das Skript kopiert die Synthese nicht, es laedt sie aus den Probestuecken design/proben/audio/hunters-axe-201/runde-1/
bauen.py und runde-2/bauen.py und baut die vier gewaehlten Fassungen mit denselben Seeds, Pegeln und Parametern:

    Hieb:          runde-1 hieb-c-luftdruck.wav             -> axt-hieb.wav             ("C Luftdruck")
    Treffer:       runde-1 treffer-fleisch-c-nass.wav       -> axt-treffer-fleisch.wav  ("C Nass")
    Harter Treffer runde-1 treffer-hart-b-stiel-prallt.wav  -> axt-treffer-hart.wav     ("B Stiel prallt", 0,63 s)
    Aufladen:      runde-2 aufladen-b-faustschlag.wav       -> axt-aufladen.wav         ("B Faustschlag")

Runde 1 bringt jede Kategorie erst auf ihre Spitze und dann alle drei Fassungen auf dieselbe mittlere Lautstaerke; darum
baut das Skript dort alle drei Fassungen einer Kategorie und nimmt die gewaehlte. Jede Datei muss Byte fuer Byte die
Pruefsumme haben, die wav.sha256 der Runde fuer die abgenommene Fassung nennt; sonst bricht das Skript ab und schreibt
nichts. Nur Standardbibliothek, Zufall nur mit festen Seeds (in der Probe). WAV, 16 Bit, mono, 44,1 kHz.

    python tools/audio/axtklang.py           # schreibt assets/audio/axt-*.wav
    python tools/audio/axtklang.py --pruefen # baut neu und vergleicht mit assets/audio

Danach: python tools/models/checksums.py (schreibt audio.sha256 neu).
"""
import hashlib
import importlib.util
import os
import sys
import tempfile

WURZEL = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PROBE = os.path.join(WURZEL, "design", "proben", "audio", "hunters-axe-201")
ZIEL = os.path.join(WURZEL, "assets", "audio")

# (Runde, Kategorie, Fassung) -> Dateiname im Spiel (Urteile des Menschen in Issue 201)
GEWAEHLT = {
    ("runde-1", "hieb", "c-luftdruck"): "axt-hieb.wav",
    ("runde-1", "treffer-fleisch", "c-nass"): "axt-treffer-fleisch.wav",
    ("runde-1", "treffer-hart", "b-stiel-prallt"): "axt-treffer-hart.wav",
    ("runde-2", "aufladen", "b-faustschlag"): "axt-aufladen.wav",
}


def sha256(daten):
    return hashlib.sha256(daten).hexdigest()


def abgenommen(runde):
    """Die Pruefsummen der Probe aus <runde>/wav.sha256: {Dateiname: sha256}."""
    summen = {}
    with open(os.path.join(PROBE, runde, "wav.sha256"), encoding="utf-8") as f:
        for zeile in f:
            if zeile.strip():
                wert, name = zeile.split(maxsplit=1)
                summen[name.strip().lstrip("*")] = wert
    return summen


def probe_laden(runde):
    pfad = os.path.join(PROBE, runde, "bauen.py")
    spec = importlib.util.spec_from_file_location("axt_" + runde.replace("-", "_"), pfad)
    modul = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(modul)
    return modul


def fassung(runde, modul, kategorie, name):
    """Die Abtastwerte der Fassung, genau wie main() der Probe sie baut."""
    if runde == "runde-1":
        for kuerzel, _, spitze, varianten in modul.KATEGORIEN:
            if kuerzel == kategorie:
                for (vname, _), x in zip(varianten, modul.baue_kategorie(spitze, varianten)):
                    if vname == name:
                        return x
    else:
        for vname, fn in modul.AUFLADEN:
            if kategorie == "aufladen" and vname == name:
                x = fn()
                return modul.mal(x, modul.ZIEL_RMS / modul.rms(x))
    raise KeyError((runde, kategorie, name))


def bauen():
    """Baut die vier gewaehlten Fassungen und liefert {Dateiname im Spiel: (Datei der Probe, Runde, WAV-Bytes)}."""
    module = {runde: probe_laden(runde) for runde in ("runde-1", "runde-2")}
    ergebnis = {}
    with tempfile.TemporaryDirectory() as ordner:
        for (runde, kategorie, name), spiel in GEWAEHLT.items():
            probe = "%s-%s.wav" % (kategorie, name)
            pfad = os.path.join(ordner, probe)
            module[runde].schreibe(pfad, fassung(runde, module[runde], kategorie, name))
            with open(pfad, "rb") as f:
                ergebnis[spiel] = (probe, runde, f.read())
    return ergebnis


def main():
    summen = {runde: abgenommen(runde) for runde in ("runde-1", "runde-2")}
    neu = bauen()
    abweichend = [probe for probe, runde, daten in neu.values() if sha256(daten) != summen[runde][probe]]
    for spiel, (probe, runde, daten) in neu.items():
        print(f"{runde}/{probe} -> {spiel}: {sha256(daten)}")
    if abweichend:
        print("FEHLER: weicht von wav.sha256 der Probe ab:", abweichend, file=sys.stderr)
        return 1
    print("Alle vier Byte-gleich zur abgenommenen Probe (wav.sha256).")
    if "--pruefen" in sys.argv:
        schlecht = []
        for spiel, (_, _, daten) in neu.items():
            pfad = os.path.join(ZIEL, spiel)
            if not os.path.exists(pfad) or open(pfad, "rb").read() != daten:
                schlecht.append(spiel)
        print("bitgleich zu assets/audio:", not schlecht, schlecht)
        return 1 if schlecht else 0
    for spiel, (_, _, daten) in neu.items():
        with open(os.path.join(ZIEL, spiel), "wb") as f:
            f.write(daten)
        print("geschrieben:", spiel, len(daten), "Bytes")
    return 0


if __name__ == "__main__":
    sys.exit(main())
