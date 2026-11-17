"""Tueren und rissige Wand fuer Runde 3 (Blender-Python): Der Stein der Tueren (Feld "stone": die Bogenfelder, Pfosten) ist **die echte
neue Wand-Kachel** der jeweiligen Fassung (A oder B), also dieselbe Rampe und dieselben Fugen wie die Wand. Rest der Tuer wie in main
(tools/models/door.py, unveraendert gelesen, nur Ausgabepfad und der Stein-Schritt ersetzt). Die rissige Wand wird mit
tools/models/cracked_wall.py (nur Pfade ersetzt) auf den neuen Wand-Texturen neu erzeugt.
Aufruf: blender --background --python tueren_neu3.py"""
import os

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
TOOLS = os.path.join(ROOT, "tools", "models")

door_src = open(os.path.join(TOOLS, "door.py"), encoding="utf-8").read()
door_src = door_src.replace("\nmain()\n", "\n").replace("__file__", repr(os.path.join(TOOLS, "door.py")))
cw_src0 = open(os.path.join(TOOLS, "cracked_wall.py"), encoding="utf-8").read()


def read(path):
    im = bpy.data.images.load(path)
    w, h = im.size
    a = np.array(im.pixels[:], dtype=float).reshape(h, w, 4)[..., :3] * 255
    bpy.data.images.remove(im)
    return np.round(a[::-1])                                   # Zeile 0 oben


for fass in ("A", "B"):
    base = os.path.join(HERE, "neu", fass)
    os.makedirs(os.path.join(base, "models"), exist_ok=True)
    g = {"__name__": "door"}
    import bpy  # noqa: E402
    exec(compile(door_src, "door.py", "exec"), g)
    g["OUT_DIR"] = os.path.join(base, "models")
    tile = read(os.path.join(base, "textures", "catacomb_wall.png"))            # 64 x 64, Zeile 0 oben
    orig = g["paint_atlas"]

    def wrapped(rects, painters_, size, paint=4, colors=32, seed=1, _orig=orig, _tile=tile):
        p2 = dict(painters_)
        flat = _tile.reshape(-1, 3).mean(axis=0)
        p2["stone"] = lambda U, V_: np.broadcast_to(flat, U.shape + (3,)).copy()
        arr = _orig(rects, p2, size, paint=paint, colors=18, seed=seed)
        x0, y0, x1, y1 = rects["stone"]
        arr[y0:y1, x0:x1] = _tile[::-1]                                          # Zeile 0 unten
        return arr
    g["paint_atlas"] = wrapped
    for kind in g["PAL"]:
        g["build"](kind, g["RECTS"], g["painters"](kind), g["parts_for"](kind))
    # rissige Wand
    cw = cw_src0.replace("\nmain()\n", "\n").replace("__file__", repr(os.path.join(TOOLS, "cracked_wall.py")))
    cw = cw.replace('OUT_DIR = os.path.join(ROOT, "assets", "models")', f"OUT_DIR = {os.path.join(base, 'models')!r}")
    cw = cw.replace('TEXTURES = os.path.join(ROOT, "assets", "textures")', f"TEXTURES = {os.path.join(base, 'textures')!r}")
    cw = cw.replace('SOURCES = os.path.join(TOOLS, "cracked_wall_sources.sha256")', f"SOURCES = {os.path.join(base, 'cracked_wall_sources.sha256')!r}")
    g2 = {"__name__": "cw"}
    exec(compile(cw, "cracked_wall.py", "exec"), g2)
    g2["main"]()
