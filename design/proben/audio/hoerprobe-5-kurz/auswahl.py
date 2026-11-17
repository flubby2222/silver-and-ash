"""Hoerprobe 5 kurz – waehlt aus Hoerprobe 3 und 5 aus und benennt um. Erzeugt keine neuen Klaenge.
Aufruf: python auswahl.py  (nutzt ffmpeg nur, um die zwei Ambiente-OGG in WAV zu wandeln)"""
import shutil
import subprocess
from pathlib import Path

HERE = Path(__file__).resolve().parent
A = HERE.parent
H3, H5 = A / "hoerprobe-3", A / "hoerprobe-5"
KOPIEREN = {
    "1a-schuss.wav": H5 / "pistole-2-metall-plus-koerper.wav",
    "1b-schuss.wav": H5 / "vergleich-pistole-hp3.wav",
    "2a-klinge.wav": H5 / "klinge-1-hieb-rein.wav",
    "2b-klinge.wav": H5 / "vergleich-klinge-hp3-heller.wav",
    "3a-schritte.wav": H5 / "schritt-1-rpg-rein-folge.wav",
    "3b-schritte.wav": H5 / "vergleich-schritt-hp3-folge.wav",
    "5-ghul.wav": H3 / "ghul-ps1.wav",
}
OGG = {
    "4a-tropfen-ambiente.wav": H5 / "ambiente-tropfen-vergleich-hp3-gekuerzt.ogg",
    "4b-tropfen-ambiente.wav": H5 / "ambiente-tropfen-2-blech.ogg",
}
for name, src in KOPIEREN.items():
    shutil.copyfile(src, HERE / name)
for name, src in OGG.items():
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(src), "-c:a", "pcm_s16le", str(HERE / name)], check=True)
