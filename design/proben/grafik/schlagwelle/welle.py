"""Gorms Schlag, Welle am Boden (Probestueck, Issue #299): zwei Entwuerfe als kleine Bausteine, die das Spiel zu einem Ring zusammensetzt.

Das Spiel zeichnet heute `Ring(...)`: Kleinteile entlang des Kreises mit dem Radius der Welle (Ring aus Kaesten 0,12 breit, 0,35 hoch, einfarbig
220/210/190); die Trefferzone ist die Scheibe bis zu diesem Radius (getroffen wird, wer niedriger als 0,4 m ueber dem Boden ist). Hier nur das
Aussehen. Lokale Achsen eines Bausteins wie beim Ring: x nach aussen (radial), y oben, z am Kreis entlang; Ursprung = Punkt auf dem Ring am Boden.

  welle_a  "Aufbrechender Stein": Knoten `shard` (Gruppe von drei schraeg nach aussen aufgeworfenen Steinplatten, 0,6 m breit, aus der Wandkachel)
           und `puff` (kleiner Staubhaufen aus Kantenstuecken, Knochen-Reihe), mit einem roten Riss (Blutrot) am Fuss der Platten.
  welle_b  "Druckwelle": Knoten `band`: ein Keil von 1 m Kreislaenge (wird auf die Bogenlaenge skaliert): senkrechter heller Rand (Knochen) vorn,
           dahinter eine nach innen abfallende Flaeche in Blutrot-Stufen (hell am Rand, dunkel innen), 0,86 m tief.

Aufruf: blender --background --python welle.py -- [Kachel-Ordner]   (Standard: Kacheln der Palette A, palette3/neu/A/textures)
"""
import json
import math
import os
import sys

import bpy
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
TEXDIR = ARGS[0] if ARGS else os.path.join(ROOT, "design", "proben", "grafik", "palette3", "neu", "A", "textures")
OUT = os.path.join(HERE, "modelle")
sys.path.insert(0, os.path.join(ROOT, "tools", "models"))
import psx  # noqa: E402

Part, V = psx.Part, psx.V
PAL = json.load(open(os.path.join(ROOT, "assets", "palette", "hauptpalette.json"), encoding="utf-8"))
ROWS = {r["name"]: r["farben"] for r in PAL["grund"] + PAL["akzent"]}
BLUT, KNOCHEN = ROWS["Blutrot"], ROWS["Haut/Knochen"]

RECTS = {"stone": (0, 64, 64, 128), "dust": (64, 96, 96, 128), "crack": (96, 64, 128, 96), "rim": (96, 96, 128, 128),
         "body": (0, 0, 64, 32), "dark": (64, 64, 80, 80)}
UV = psx.rects_from_pixels(RECTS, 128)
BAYER = np.array([[0.0, 0.5], [0.75, 0.25]])


def load_tile():
    im = bpy.data.images.load(os.path.join(TEXDIR, "catacomb_wall.png"))
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
    x0, y0, x1, y1 = RECTS["rim"]
    for r in range(y0, y1):
        for c in range(x0, x1):
            a[r, c] = KNOCHEN[7 if (r - y0) * 2 > (y1 - y0) or BAYER[r % 2, c % 2] > 0.6 else 6]
    x0, y0, x1, y1 = RECTS["body"]
    for r in range(y0, y1):                                      # Spalten: links (innen) dunkel bis rechts (am Rand) hell, 2x2 gerastert
        for c in range(x0, x1):
            t = (c - x0) / (x1 - x0 - 1) * 6.0 + 1.0              # 1 .. 7
            a[r, c] = BLUT[int(min(7, max(1, math.floor(t + BAYER[r % 2, c % 2] - 0.5 + 0.5))))]
    x0, y0, x1, y1 = RECTS["dark"]
    a[y0:y1, x0:x1] = ROWS["Stein"][1]
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


def welle_a():
    shard = Part("shard", (0, 0, 0), UV)
    # drei aufgeworfene Platten, nach aussen geneigt, unterschiedlich hoch und breit (Gruppe 0,6 m breit)
    for z, h, w, tilt, spin in ((-0.2, 0.42, 0.22, -24, 8), (0.02, 0.56, 0.26, -30, -6), (0.22, 0.36, 0.2, -20, 12)):
        rbox(shard, (0.0, h / 2 - 0.02, z), (0.1, h, w), (tilt, spin), "stone")
    shard.box((0.1, 0.0, -0.28), (0.2, 0.02, 0.28), "crack")                  # roter Riss am Fuss (Akzent Blutrot, Gefahr)
    puff = Part("puff", (0, 0, 0), UV)
    for x, y, z, s, r in ((0, 0.1, -0.12, 0.24, 20), (0.06, 0.08, 0.1, 0.2, -35), (-0.08, 0.2, 0.0, 0.2, 55), (0.1, 0.22, -0.02, 0.14, 15)):
        rbox(puff, (x, y, z), (s, s, s), (r, r * 1.3), "dust")
    return [shard, puff]


def welle_b():
    band = Part("band", (0, 0, 0), UV)
    H, X0, X1 = 0.35, 0.06, -0.8
    front = [V(X0, 0, -0.5), V(X0, 0, 0.5), V(X0, H, 0.5), V(X0, H, -0.5)]
    slope = [V(X0, H, -0.5), V(X0, H, 0.5), V(X1, 0.0, 0.5), V(X1, 0.0, -0.5)]
    r0, rv0, r1, rv1 = UV["rim"]
    fv = [band.bm.verts.new(psx.G(p)) for p in (V(X0, 0, -0.5), V(X0, H, -0.5), V(X0, H, 0.5), V(X0, 0, 0.5))]   # Normale +x (nach aussen)
    band._face(fv, [(r0, rv0), (r0, rv1), (r1, rv1), (r1, rv0)])
    # Flaeche: u soll entlang x laufen (Verlauf von innen nach aussen), v am Kreis: eigene UVs
    import bmesh
    u0, v0, u1, v1 = UV["body"]
    verts = [band.bm.verts.new(psx.G(p)) for p in reversed(slope)]
    uvs = list(reversed([(u1, v0), (u1, v1), (u0, v1), (u0, v0)]))
    band._face(verts, uvs)
    return [band]


def build(name, parts):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    coll = bpy.data.collections.new("Welle")
    bpy.context.scene.collection.children.link(coll)
    tex, colours = atlas()
    image = psx.save_image(name, tex, os.path.join(OUT, name + ".png"))
    mat = psx.export_material(name, image)
    objs = [p.build(coll, mat, smooth=False) for p in parts]
    psx.export_glb(objs, os.path.join(OUT, name + ".glb"))
    psx.report_parts(name, objs)
    print(f"TEXTUR {name} 128x128 Farben {colours}")


def main():
    os.makedirs(OUT, exist_ok=True)
    build("welle_a", welle_a())
    build("welle_b", welle_b())


main()
