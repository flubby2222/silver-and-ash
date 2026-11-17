"""Tueren und rissige Wand fuer die Palettenprobe, Runde 2 (Blender-Python).
- Tueren (door_normal/locked/boss/cracked): die eingebetteten Texturen werden wie in Runde 1 auf die ganzen 64 Farben der Palette
  umgerechnet (nächste Farbe, Lab); sie haben viele gemalte Farbtoene und leiden nur wenig, siehe Notizen.
- Rissige Wand (cracked_wall_a/b/c): NEU erzeugt mit tools/models/cracked_wall.py (lokale Kopie der Pfade) auf den neu gemalten
  Wand-Texturen der Palette, damit die Platte die Wand ohne Naht fortsetzt.
Aufruf: blender --background --python tueren_neu.py -- <palette_key | alle>
"""
import json
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
PALDIR = os.path.join(os.path.dirname(HERE), "palette")

# Hilfsfunktionen aus der Probe von Runde 1 (ohne deren main)
usrc = open(os.path.join(PALDIR, "umrechnen.py"), encoding="utf-8").read()
usrc = usrc.replace("\nmain()\n", "\n").replace("__file__", repr(os.path.join(PALDIR, "umrechnen.py")))
ns = {"__name__": "umr"}
exec(compile(usrc, "umrechnen.py", "exec"), ns)

ARG = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else "alle"
KEYS = [f[len("palette_"):-5] for f in sorted(os.listdir(os.path.join(HERE, "paletten"))) if f.endswith(".json")] if ARG == "alle" else [ARG]
DOORS = ("door_normal", "door_locked", "door_boss", "door_cracked")


def full_palette(key):
    data = json.load(open(os.path.join(HERE, "paletten", f"palette_{key}.json"), encoding="utf-8"))
    return np.array([c for r in data["reihen"] for c in r["farben"]], float)


for key in KEYS:
    base = os.path.join(HERE, "neu", key)
    os.makedirs(os.path.join(base, "models"), exist_ok=True)
    pal = full_palette(key)
    ns["TMP"] = os.path.join(HERE, "_tmp")
    for name in DOORS:
        c = ns["patch_glb"](os.path.join(ROOT, "assets", "models", name + ".glb"), os.path.join(base, "models", name + ".glb"), pal)
        ns["process_png"](os.path.join(ROOT, "assets", "models", name + ".png"), os.path.join(base, "models", name + ".png"), pal)
        print("TUER", key, name, "Farben", c)
    # rissige Wand: tools/models/cracked_wall.py mit anderen Pfaden
    cw = open(os.path.join(ROOT, "tools", "models", "cracked_wall.py"), encoding="utf-8").read()
    cw = cw.replace("\nmain()\n", "\n").replace("__file__", repr(os.path.join(ROOT, "tools", "models", "cracked_wall.py")))
    cw = cw.replace('OUT_DIR = os.path.join(ROOT, "assets", "models")', f"OUT_DIR = {os.path.join(base, 'models')!r}")
    cw = cw.replace('TEXTURES = os.path.join(ROOT, "assets", "textures")', f"TEXTURES = {os.path.join(base, 'textures')!r}")
    cw = cw.replace('SOURCES = os.path.join(TOOLS, "cracked_wall_sources.sha256")', f"SOURCES = {os.path.join(base, 'cracked_wall_sources.sha256')!r}")
    g = {"__name__": "cw"}
    exec(compile(cw, "cracked_wall.py", "exec"), g)
    g["main"]()
import shutil
shutil.rmtree(os.path.join(HERE, "_tmp"), ignore_errors=True)
