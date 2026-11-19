"""Baut die vom Menschen gewaehlten Monsterklaenge (Issue 392) nach assets/audio/.

Die Synthese steht in design/proben/audio/monster/bauen.py (Hoerprobe). Dieses Skript baut dieselben Paare,
nimmt je Klang die gewaehlte Fassung und schreibt sie unter dem alten Dateinamen ins Spiel.

    python tools/audio/monsterklaenge.py           # schreibt assets/audio/*.wav
    python tools/audio/monsterklaenge.py --pruefen # baut neu und vergleicht Byte fuer Byte mit assets/audio
    (optional Klangnamen anhaengen, dann nur diese)

Danach: python tools/models/checksums.py (schreibt audio.sha256 neu).
"""
import importlib.util
import os
import sys

WURZEL = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PROBE = os.path.join(WURZEL, "design", "proben", "audio", "monster", "bauen.py")
ZIEL = os.path.join(WURZEL, "assets", "audio")

# Name -> gewaehlte Fassung (Urteil des Menschen vom 2026-11-19)
GEWAEHLT = {
    "fledermaus-spucken": "a", "fledermaus-flattern": "a", "fledermaus-biss": "b",
    "fledermaus-treffer": "a", "fledermaus-fehlsturz": "a",
    "ghul-ankuendigung": "a", "ghul-entdecken": "a", "ghul-sterben": "a",
    "gorm-bruellen": "b", "gorm-treffer": "a", "gorm-sterben": "b", "gorm-ankuendigung-hammer": "b",
    # Runde 2 (Urteil 2026-11-19)
    "fledermaus-kreischen": "c", "fledermaus-spuck-ankuendigung": "a", "fledermaus-sterben": "b",
}


NUR = [a for a in sys.argv[1:] if not a.startswith("--")]  # optional: nur diese Klaenge bauen


def laden():
    spec = importlib.util.spec_from_file_location("monster_bauen", PROBE)
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


def bauen(m):
    """Liefert {Dateiname im Spiel: WAV-Bytes}."""
    import tempfile
    ergebnis = {}
    with tempfile.TemporaryDirectory() as t:
        for name, alt, _, _, fa, fb, _ in list(m.KLAENGE) + list(m.RUNDE2):
            if name not in GEWAEHLT or (NUR and name not in NUR):
                continue
            paar = m.anpassen_paar(m.fertig(fa()), m.fertig(fb()), alt)
            x = paar[0] if GEWAEHLT[name] in ("a", "c") else paar[1]
            p = os.path.join(t, alt)
            m.schreibe(p, x)
            with open(p, "rb") as f:
                ergebnis[alt] = f.read()
    return ergebnis


def main():
    m = laden()
    neu = bauen(m)
    if "--pruefen" in sys.argv:
        schlecht = [n for n, b in neu.items() if open(os.path.join(ZIEL, n), "rb").read() != b]
        print("bitgleich zu assets/audio:", not schlecht, "(%d Dateien)" % len(neu), schlecht)
        return 1 if schlecht else 0
    for n, b in neu.items():
        with open(os.path.join(ZIEL, n), "wb") as f:
            f.write(b)
        print("geschrieben:", n, len(b), "Bytes")
    return 0


if __name__ == "__main__":
    sys.exit(main())
