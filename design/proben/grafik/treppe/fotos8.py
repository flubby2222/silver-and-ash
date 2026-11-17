"""Probefotos der Treppen im Spiel (Debug-Build, Szene stairs-view aus patch_lokal7.py), wie tuerrahmen/fotos7.py: Debug-Build einmal bauen
(dotnet build, patch_lokal7.py angewendet), dann die Exe direkt aufrufen; Modelle, Wandkacheln und Pruefsummen-Listen werden nur im
Ausgabeordner bin/Debug getauscht, nie in assets/. Die Wandkacheln sind die der Palette A (palette3/neu/A/textures).
Aufruf (Wurzelverzeichnis): python design/proben/grafik/treppe/fotos8.py [Namenspraefix]
"""
import hashlib
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
BIN = os.path.join(ROOT, "src", "SilverAndAsh.Game", "bin", "Debug", "net10.0")
OUT = os.path.join(HERE, "fotos")
ONLY = sys.argv[1] if len(sys.argv) > 1 else None
TEX_A = os.path.join(ROOT, "design", "proben", "grafik", "palette3", "neu", "A", "textures")


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


def cases():
    for design in ("heute", "spiral", "abgang"):
        for var in "ab":
            for dist in (4, 10):
                yield f"{design}_{var}_{dist}m", design, var, {"SA_STAIRS_DIST": str(dist)}
        yield f"{design}_c_4m", design, "c", {"SA_STAIRS_DIST": "4"}
        yield f"{design}_a_nah", design, "a", {"SA_CAM": "0.8,2.6,0,1.4,0"}
        yield f"{design}_a_oben", design, "a", {"SA_CAM": "0.0,1.9,0,0.2,-0.2"}
        yield f"{design}_a_seite", design, "a", {"SA_CAM": "5.5,0.6,0,1.4,0"}
        yield f"{design}_gang_4m", design, "a", {"SA_STAIRS_DIST": "4", "SA_HALL_H": "4.2"}


def main():
    os.makedirs(OUT, exist_ok=True)
    n = 0
    for name, design, var, env in cases():
        if ONLY and not name.startswith(ONLY):
            continue
        model = f"stairs_{design}_{var}"
        setup([model] if design != "heute" else [])
        tmp = os.path.join(OUT, "_tmp")
        shutil.rmtree(tmp, ignore_errors=True)
        e = dict(os.environ, SA_FORCE_WALL=var, **env)
        if design != "heute":
            e["SA_STAIRS"] = model
        subprocess.run([os.path.join(BIN, "SilverAndAsh.Game.exe"), "--photo", "stairs-view", "--out", tmp], cwd=BIN, env=e,
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        src = os.path.join(tmp, "stairs-view.png")
        if os.path.exists(src):
            shutil.move(src, os.path.join(OUT, name + ".png"))
            n += 1
        else:
            print("FEHLER", name)
        shutil.rmtree(tmp, ignore_errors=True)
    print("Bilder:", n)


main()
