"""Baut die vom Menschen gewaehlten Klaenge der Bossraum-Tuer (Issue 569, Hoerprobe Runde 2 aus Issue 549) nach
assets/audio/.

Das Skript kopiert die Synthese nicht, es laedt sie aus dem Probestueck
design/proben/audio/bossraum-tuer-549/bauen_r2.py (das seinerseits bauen.py daneben nutzt) und baut die zwei gewaehlten
Fassungen mit den Zeiten der Probe (Anschlag bei Tick 78, Oeffnen 132 Ticks, Anlauf 30, Auslauf 40):

    Schliessen: r2-tor-schliessen-2-starkes-grollen-langer-nachklang.wav -> bosstuer-schliessen.wav
    Oeffnen:    r2-tor-oeffnen-1-geroell-fein.wav                        -> bosstuer-oeffnen.wav

Jede Datei muss Byte fuer Byte die Pruefsumme haben, die wav.sha256 der Probe fuer die abgenommene Fassung nennt;
sonst bricht das Skript ab und schreibt nichts. Nur Standardbibliothek, Zufall nur mit festen Seeds (in der Probe).

Daneben schreibt es die Begleitdatei bosstuer-ticks.json (Issue 668) mit den Ticks, auf die die Klaenge gebaut sind
(Anschlag, Dauer des Oeffnens). Ein Test vergleicht sie mit Balance.Default: Aendert eine Balancing-Runde bossShutTick
oder bossOpenTime, wird er rot, bis die Klaenge mit den neuen Ticks neu gebaut und abgehoert sind. Derselbe Test liest
ANSCHLAG_TICK und OEFFNEN_TICKS aus diesem Skript (Issue 680, Zeilen "NAME = Zahl" am Zeilenanfang, je genau einmal):
Eine von Hand angeglichene Begleitdatei macht ihn nicht gruen. Jede weitere Zuweisung am Zeilenanfang, die einen der
beiden Namen bindet (berechnet, als Tupel oder mit "+="), macht ihn rot (Issue 684): Die Ticks werden nur dort gesetzt.
Fehlt die Begleitdatei oder ist sie kaputt, endet --pruefen mit Exit 1 und einer Meldung; ohne --pruefen schreibt das
Skript sie neu. Steht an ihrer Stelle ein Ordner, enden beide Aufrufe mit Exit 1 und einer Meldung und schreiben nichts.

    python tools/audio/bosstuer_klang.py           # schreibt assets/audio/bosstuer-*.wav und bosstuer-ticks.json
    python tools/audio/bosstuer_klang.py --pruefen # baut neu und vergleicht mit assets/audio

Danach: python tools/models/checksums.py (schreibt audio.sha256 neu).
"""
import hashlib
import importlib.util
import json
import os
import sys
import tempfile

WURZEL = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PROBE = os.path.join(WURZEL, "design", "proben", "audio", "bossraum-tuer-549")
ZIEL = os.path.join(WURZEL, "assets", "audio")

# Zeiten der Probe (Bewegung der Tuer aus Issue 549), unveraendert
ANSCHLAG_TICK = 78
OEFFNEN_TICKS = 132
ANLAUF_TICKS = 30
AUSLAUF_TICKS = 40

# Datei der Probe -> Dateiname im Spiel (Urteil des Menschen zu Runde 2, 2026-11-23)
GEWAEHLT = {
    "r2-tor-schliessen-2-starkes-grollen-langer-nachklang.wav": "bosstuer-schliessen.wav",
    "r2-tor-oeffnen-1-geroell-fein.wav": "bosstuer-oeffnen.wav",
}


# Begleitdatei (Issue 668): die Ticks, auf die die Klaenge gebaut sind, unter den Namen aus assets/balance.json
TICKS = "bosstuer-ticks.json"


def ticks():
    """Inhalt der Begleitdatei: Anschlag-Tick und Dauer des Oeffnens in Ticks, dazu das Skript, das sie schreibt."""
    return {
        "script": "tools/audio/bosstuer_klang.py",
        "bossShutTick": ANSCHLAG_TICK,
        "bossOpenTicks": OEFFNEN_TICKS,
    }


class BegleitdateiFehler(Exception):
    """Die Begleitdatei fehlt oder ist kaputt (Issue 680); die Meldung sagt, was zu tun ist."""


def ordner_statt_begleitdatei():
    """Steht an der Stelle der Begleitdatei ein Ordner (Issue 684), die Meldung dazu, sonst None. Neu schreiben hilft
    dann nicht: Das Skript loescht keinen Ordner, das muss jemand von Hand tun."""
    if not os.path.isdir(os.path.join(ZIEL, TICKS)):
        return None
    return (f"assets/audio/{TICKS} ist ein Ordner, keine Datei. Den Ordner entfernen oder umbenennen, dann "
            "python tools/audio/bosstuer_klang.py laufen lassen (schreibt die Datei neu).")


def ticks_lesen():
    """Die Begleitdatei in assets/audio als dict. Fehlt sie, ist sie ein Ordner oder kein JSON-Objekt:
    BegleitdateiFehler."""
    pfad = os.path.join(ZIEL, TICKS)
    name = f"assets/audio/{TICKS}"
    neu_bauen = "python tools/audio/bosstuer_klang.py schreibt sie neu"
    ordner = ordner_statt_begleitdatei()
    if ordner:
        raise BegleitdateiFehler(ordner)
    if not os.path.exists(pfad):
        raise BegleitdateiFehler(f"{name} fehlt ({neu_bauen}).")
    try:
        with open(pfad, encoding="utf-8") as f:
            inhalt = json.load(f)
    except (OSError, UnicodeDecodeError, ValueError) as fehler:
        raise BegleitdateiFehler(f"{name} ist nicht lesbar: {fehler} ({neu_bauen}).") from None
    if not isinstance(inhalt, dict):
        raise BegleitdateiFehler(f"{name} ist kein JSON-Objekt ({neu_bauen}).")
    return inhalt


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
    """Laedt bauen_r2.py der Probe; dessen 'import bauen' findet bauen.py im selben Ordner."""
    sys.path.insert(0, PROBE)
    try:
        spec = importlib.util.spec_from_file_location("bosstuer_r2_bauen", os.path.join(PROBE, "bauen_r2.py"))
        modul = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(modul)
    finally:
        sys.path.remove(PROBE)
    return modul


def bauen():
    """Baut die zwei gewaehlten Fassungen wie die Probe und liefert {Dateiname im Spiel: (Probe, WAV-Bytes)}."""
    r2 = probe_laden()
    fassungen = {
        "r2-tor-schliessen-2-starkes-grollen-langer-nachklang.wav": lambda: r2.schliessen_r2(2, ANSCHLAG_TICK),
        "r2-tor-oeffnen-1-geroell-fein.wav": lambda: r2.oeffnen_r2(1, OEFFNEN_TICKS, ANLAUF_TICKS, AUSLAUF_TICKS),
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
        print("FEHLER: weicht von wav.sha256 der Probe ab:", abweichend, file=sys.stderr)
        return 1
    print("Beide Byte-gleich zur abgenommenen Probe (wav.sha256).")
    if "--pruefen" in sys.argv:
        schlecht = []
        for spiel, (_, daten) in neu.items():
            pfad = os.path.join(ZIEL, spiel)
            if not os.path.exists(pfad) or open(pfad, "rb").read() != daten:
                schlecht.append(spiel)
        print("bitgleich zu assets/audio:", not schlecht, schlecht)
        # Die Begleitdatei nach Inhalt vergleichen: git schreibt sie unter Windows mit CRLF aus.
        try:
            passt = ticks_lesen() == ticks()
        except BegleitdateiFehler as fehler:
            print("FEHLER:", fehler, file=sys.stderr)
            return 1
        print(f"{TICKS} passt zu den Ticks des Skripts:", passt)
        return 1 if schlecht or not passt else 0
    ordner = ordner_statt_begleitdatei()
    if ordner:
        # Vor dem ersten Schreiben pruefen: sonst stuenden neue WAV-Dateien neben einer Begleitdatei, die fehlt.
        print("FEHLER:", ordner, file=sys.stderr)
        return 1
    for spiel, (_, daten) in neu.items():
        with open(os.path.join(ZIEL, spiel), "wb") as f:
            f.write(daten)
        print("geschrieben:", spiel, len(daten), "Bytes")
    try:
        passt = ticks_lesen() == ticks()
    except BegleitdateiFehler as fehler:
        # Ohne --pruefen ist Schreiben die Abhilfe: eine fehlende oder kaputte Begleitdatei wird neu geschrieben.
        print("Hinweis:", fehler)
        passt = False
    if not passt:
        # Nur bei anderem Inhalt schreiben, sonst zeigte git status nach jedem Lauf andere Zeilenenden.
        with open(os.path.join(ZIEL, TICKS), "w", encoding="utf-8", newline="\n") as f:
            json.dump(ticks(), f, indent=2)
            f.write("\n")
        print("geschrieben:", TICKS)
    return 0


if __name__ == "__main__":
    sys.exit(main())
