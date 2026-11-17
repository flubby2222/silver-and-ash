"""Schneidet aus den fertigen musik-*.ogg die ersten 28 s als WAV zum Vorhoeren (nur ffmpeg).

Aufruf: python musik_vorhoeren.py <ordner mit den OGG-Dateien> <Zielordner> [name ...]
Ohne Namen: katakomben, boss, boss-phase2, haendler. Ergebnis: <Zielordner>/musik-<name>-anfang.wav, 16 Bit, 22 050 Hz, stereo
(die PS1-Kette schneidet ohnehin alles ueber 11 kHz ab), die letzten 1,5 s ausgeblendet. Das ist keine Schleife und kein Spiel-Asset.
"""
import subprocess
import sys
from pathlib import Path


def main():
    src, dst = Path(sys.argv[1]), Path(sys.argv[2])
    names = sys.argv[3:] or ["katakomben", "boss", "boss-phase2", "haendler"]
    dst.mkdir(parents=True, exist_ok=True)
    for n in names:
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(src / f"musik-{n}.ogg"), "-t", "28", "-af",
                        "afade=t=out:st=26.5:d=1.5", "-ar", "22050", "-c:a", "pcm_s16le", "-map_metadata", "-1",
                        "-fflags", "+bitexact", "-flags:a", "+bitexact", str(dst / f"musik-{n}-anfang.wav")], check=True)
        print(n)


if __name__ == "__main__":
    main()
