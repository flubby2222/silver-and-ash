"""Zaehlt Farben der Wandkacheln (heute und Palette A) und ordnet sie der Stein-Rampe zu."""
import json, os, sys
import bpy
import numpy as np
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
ramp = np.array(json.load(open(os.path.join(ROOT, "assets", "palette", "hauptpalette.json"), encoding="utf-8"))["grund"][0]["farben"], int)
def load(p):
    im = bpy.data.images.load(p)
    w, h = im.size
    a = np.array(im.pixels[:]).reshape(h, w, 4)[::-1, :, :3] * 255
    return np.round(a).astype(int)
for tag, d in (("heute", os.path.join(ROOT, "assets", "textures")), ("A", os.path.join(ROOT, "design", "proben", "grafik", "palette3", "neu", "A", "textures"))):
    for n in ("catacomb_wall", "catacomb_wall_b", "catacomb_wall_c"):
        a = load(os.path.join(d, n + ".png"))
        cols = {tuple(p) for p in a.reshape(-1, 3)}
        inramp = sum(1 for c in cols if any((ramp == c).all(1)))
        print("KACHEL", tag, n, a.shape, "Farben", len(cols), "davon in Rampe", inramp)
