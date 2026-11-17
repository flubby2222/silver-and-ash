"""Filmt Gorms Schlag mit der heutigen Welle (Kasten-Ring) und den zwei Entwuerfen (Debug-Build, patch_lokal8.py angewendet und gebaut).
Wie tuerrahmen/fotos7.py: Exe direkt, Modelle, Wandkacheln (Palette A) und Pruefsummen nur im bin/Debug-Ordner getauscht, nie in assets/.
Je Szene (gorm-slam 8 m, gorm-slam-far 14 m) und Entwurf ein Uebersichtsbild per tools/clip.ps1 -Sheet nach <Entwurf>_<Szene>.png.
Aufruf (Wurzelverzeichnis): python design/proben/grafik/schlagwelle/filme9.py [Praefix]
"""
import hashlib
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
BIN = os.path.join(ROOT, "src", "SilverAndAsh.Game", "bin", "Debug", "net10.0")
TEX_A = os.path.join(ROOT, "design", "proben", "grafik", "palette3", "neu", "A", "textures")
ONLY = sys.argv[1] if len(sys.argv) > 1 else ""


def sha_list(directory, list_name, endings):
    lines = []
    for name in sorted(os.listdir(directory)):
        if name.endswith(endings):
            data = open(os.path.join(directory, name), "rb").read()
            lines.append(f"{hashlib.sha256(data).hexdigest()} {len(data)} {name}")
    with open(os.path.join(directory, list_name), "w", encoding="ascii", newline="\n") as f:
        f.write("# sha256 size name - local probe\n" + "\n".join(lines) + "\n")


def setup(models):
    d = os.path.join(BIN, "assets", "models")
    shutil.rmtree(d, ignore_errors=True)
    shutil.copytree(os.path.join(ROOT, "assets", "models"), d)
    for m in models:
        shutil.copy(os.path.join(HERE, "modelle", m + ".glb"), os.path.join(d, m + ".glb"))
    sha_list(d, "models.sha256", (".glb", ".png"))
    t = os.path.join(BIN, "assets", "textures")
    for f in os.listdir(TEX_A):
        shutil.copy(os.path.join(TEX_A, f), os.path.join(t, f))
    sha_list(t, "textures.sha256", (".png",))


def main():
    clips = os.path.join(os.environ["LOCALAPPDATA"], "SilverAndAsh", "clips")
    for design in ("heute", "welle_a", "welle_b"):
        for scene in ("gorm-slam", "gorm-slam-far"):
            tag = f"{design}_{scene}"
            if not tag.startswith(ONLY):
                continue
            setup([design] if design != "heute" else [])
            folder = os.path.join(clips, scene)
            shutil.rmtree(folder, ignore_errors=True)
            env = dict(os.environ, SA_FORCE_WALL="a")
            if design != "heute":
                env["SA_WAVE"] = design
            subprocess.run([os.path.join(BIN, "SilverAndAsh.Game.exe"), "--clip", scene], cwd=BIN, env=env,
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            subprocess.run(["powershell", "-ExecutionPolicy", "Bypass", "-File", os.path.join(ROOT, "tools", "clip.ps1"), folder, "-Sheet"],
                           cwd=ROOT, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            sheet = folder + "-sheet.png"
            if os.path.exists(sheet):
                shutil.copy(sheet, os.path.join(HERE, f"{tag}.png"))
            else:
                print("FEHLER", tag)
    setup([])


main()
