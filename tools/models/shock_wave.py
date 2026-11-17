"""Gorms Schlag, Welle am Boden (Issue #299): Fassung A "aufbrechender Stein", Bausteine, die das Spiel zu einem Ring setzt.

Erzeuger fuer das Spiel (Uebergabe aus dem Probestueck, Branch claude/design-probe-ghul-45a8c4, Commit 222f95f:
design/proben/grafik/schlagwelle/welle.py). Der Mensch hat Fassung A gewaehlt; nur sie wird erzeugt (Fassung B, die
Druckwelle, nicht). Geometrie, Farben und Zufallszahlen sind die des Probestuecks; der Stein kommt aus der Wand-Textur
des Spiels (assets/textures/catacomb_wall.png, Variante a, nur gelesen) statt aus der Probe-Palette.

Erzeugt nur Modell und Textur nach assets/models/ (shock_wave.glb und shock_wave.png), keine Probebilder.

Aufruf (aus dem Repository-Wurzelverzeichnis), danach python tools/models/checksums.py:
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/models/shock_wave.py

Lokale Achsen eines Bausteins wie beim alten Ring aus Kaesten: x nach aussen (radial), y oben, z am Kreis entlang;
Ursprung = Punkt auf dem Ring am Boden, Drehpunkt 0;0;0. Das Spiel setzt die Bausteine so, dass ihr aeusserster Punkt
genau auf dem Radius der Trefferzone liegt (ShockwaveLook im Core).
  shard  Gruppe von drei schraeg nach aussen aufgeworfenen Steinplatten (0,6 m am Kreis entlang, 0,36 bis 0,56 m hoch)
         aus der Wandkachel, mit einem roten Riss (Blutrot, Akzent fuer die Gefahr) am Fuss
  puff   kleiner Staubhaufen aus Kantenstuecken (hellste Stufen der Stein-Reihe), hinter dem Rand
Textur 128 x 128, Farben aus der Wandkachel und den Reihen Stein und Blutrot von assets/palette/hauptpalette.json.
"""
import json
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
import psx  # noqa: E402

TOOLS = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(TOOLS))
OUT_DIR = os.path.join(ROOT, "assets", "models")
WALL = os.path.join(ROOT, "assets", "textures", "catacomb_wall.png")
NAME = "shock_wave"

Part, V = psx.Part, psx.V
PAL = json.load(open(os.path.join(ROOT, "assets", "palette", "hauptpalette.json"), encoding="utf-8"))
ROWS = {r["name"]: r["farben"] for r in PAL["grund"] + PAL["akzent"]}
BLUT = ROWS["Blutrot"]

# Pixel-Rechtecke (x0, y0, x1, y1) im Atlas 128 x 128, wie im Probestueck.
RECTS = {"stone": (0, 64, 64, 128), "dust": (64, 96, 96, 128), "crack": (96, 64, 128, 96)}
UV = psx.rects_from_pixels(RECTS, 128)


def load_tile():
    im = bpy.data.images.load(WALL)
    w, h = im.size
    return np.round(np.array(im.pixels[:]).reshape(h, w, 4)[::-1, :, :3] * 255).astype(int)


def atlas():
    a = np.zeros((128, 128, 3))
    x0, y0, x1, y1 = RECTS["stone"]
    a[y0:y1, x0:x1] = load_tile()[::-1]
    rng = np.random.default_rng(5)
    x0, y0, x1, y1 = RECTS["dust"]
    pick = rng.choice(3, (y1 - y0, x1 - x0), p=[0.3, 0.45, 0.25])
    a[y0:y1, x0:x1] = np.array([ROWS["Stein"][11], ROWS["Stein"][12], ROWS["Stein"][14]])[pick]
    x0, y0, x1, y1 = RECTS["crack"]
    for r in range(y0, y1):
        for c in range(x0, x1):
            a[r, c] = BLUT[3 + ((r + c // 2) % 3 == 0) + ((r * 3 + c) % 7 == 0)]
    return a, len({tuple(p) for p in a.reshape(-1, 3).astype(int)})


def rbox(part, centre, size, rot, rect):
    """Quader mit Mittelpunkt, Groesse (x, y, z) und Drehung um z (Neigung nach aussen) und y in Grad."""
    cz, sz = math.cos(math.radians(rot[0])), math.sin(math.radians(rot[0]))
    cy, sy = math.cos(math.radians(rot[1])), math.sin(math.radians(rot[1]))
    pts = []
    for dx in (-0.5, 0.5):
        for dy in (-0.5, 0.5):
            for dz in (-0.5, 0.5):
                x, y, z = dx * size[0], dy * size[1], dz * size[2]
                x, y = x * cz - y * sz, x * sz + y * cz
                x, z = x * cy + z * sy, -x * sy + z * cy
                pts.append(V(centre[0] + x, centre[1] + y, centre[2] + z))
    idx = {(a, b, c): pts[a * 4 + b * 2 + c] for a in (0, 1) for b in (0, 1) for c in (0, 1)}
    quads = [[idx[0, 0, 0], idx[0, 0, 1], idx[0, 1, 1], idx[0, 1, 0]], [idx[1, 0, 0], idx[1, 1, 0], idx[1, 1, 1], idx[1, 0, 1]],
             [idx[0, 0, 0], idx[1, 0, 0], idx[1, 0, 1], idx[0, 0, 1]], [idx[0, 1, 0], idx[0, 1, 1], idx[1, 1, 1], idx[1, 1, 0]],
             [idx[0, 0, 0], idx[0, 1, 0], idx[1, 1, 0], idx[1, 0, 0]], [idx[0, 0, 1], idx[1, 0, 1], idx[1, 1, 1], idx[0, 1, 1]]]
    part.convex(quads, rect)


def parts():
    shard = Part("shard", (0, 0, 0), UV)
    # drei aufgeworfene Platten, nach aussen geneigt, unterschiedlich hoch und breit (Gruppe 0,6 m breit)
    for z, h, w, tilt, spin in ((-0.2, 0.42, 0.22, -24, 8), (0.02, 0.56, 0.26, -30, -6), (0.22, 0.36, 0.2, -20, 12)):
        rbox(shard, (0.0, h / 2 - 0.02, z), (0.1, h, w), (tilt, spin), "stone")
    shard.box((0.1, 0.0, -0.28), (0.2, 0.02, 0.28), "crack")                  # roter Riss am Fuss (Akzent Blutrot, Gefahr)
    puff = Part("puff", (0, 0, 0), UV)
    for x, y, z, s, r in ((0, 0.1, -0.12, 0.24, 20), (0.06, 0.08, 0.1, 0.2, -35), (-0.08, 0.2, 0.0, 0.2, 55), (0.1, 0.22, -0.02, 0.14, 15)):
        rbox(puff, (x, y, z), (s, s, s), (r, r * 1.3), "dust")
    return [shard, puff]


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    coll = bpy.data.collections.new("Welle")
    bpy.context.scene.collection.children.link(coll)
    tex, colours = atlas()
    image = psx.save_image(NAME, tex, os.path.join(OUT_DIR, NAME + ".png"))
    mat = psx.export_material(NAME, image)
    objs = [p.build(coll, mat, smooth=False) for p in parts()]
    psx.export_glb(objs, os.path.join(OUT_DIR, NAME + ".glb"))
    psx.report_parts(NAME, objs)
    print(f"TEXTUR {NAME} 128x128 Farben {colours}")


main()
