"""Gemeinsame Bausteine der Probestuecke Stake Crossbow (Issue #203), Runde 1.

Wird von armbrust_a.py, armbrust_b.py, armbrust_c.py (je ein Entwurf -> eine glb) und blatt.py (Vergleichsblatt) benutzt.
Alles per Blender-Python und NumPy, nichts heruntergeladen. Farben nur aus assets/palette/hauptpalette.json.

Aufbau der Modelle (wie die vorhandenen Waffen, tools/models/pistol.py und club.py):
  Zwei Knoten, beide ohne Eltern (nicht verschachtelt), Drehpunkt = Ursprung = Griff:
    "crossbow"  Schaft, Bogen (Prod), Sehne, Beschlaege
    "stake"     der eingelegte Holzpflock (eigener Knoten: laesst sich fuer Schuss und Nachladen ausblenden/bewegen)
  Achse +z = vorn (Schussrichtung), +y = oben, x = seitlich; Massstab 1,8 x 0,72 = 1,3 eingebacken (Dateimass = 1,3 x Skriptmass).
  Textur 128 x 128 ohne Filterung, flach schattiert, keine Hand modelliert.
"""

import json
import math
import os
import sys
import tempfile

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE)))))
sys.path.insert(0, os.path.join(ROOT, "tools", "models"))
import blf  # noqa: E402
import bmesh  # noqa: E402
import bpy  # noqa: E402
import imbuf  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402
from psx import (V, G, Part, frames, rects_from_pixels, vnoise, save_image, export_material,  # noqa: E402
                 pose, bake_scale, export_glb)

MODELS = os.path.join(ROOT, "assets", "models")
TEX = 128
SCALE = 1.8
SHRINK = 0.72                    # zusaetzlich: eine Armbrust in voller 1,8-fach-Groesse waere breiter als das Bild (Spannweite 0,8 m)

# --- Palette (nur Hauptpalette) ---------------------------------------------------------------------------------------

_PAL = json.load(open(os.path.join(ROOT, "assets", "palette", "hauptpalette.json"), encoding="utf-8"))
_ROWS = {r["name"]: np.array(r["farben"], float) for r in _PAL["akzent"] + _PAL["grund"]}
WOOD_DARK = _ROWS["Holz"][1:5]
WOOD_LIGHT = _ROWS["Holz"][3:7]
WOOD_PALE = _ROWS["Holz"][4:8]            # Pflock: helles Holz
CHAR = _ROWS["Holz"][0:3]                 # verkohlte Spitze
LEATHER = _ROWS["Holz"][1:4]
STEEL = _ROWS["Eisen"][1:5]
DARKSTEEL = _ROWS["Eisen"][0:3]
SILVER = _ROWS["Eisen"][5:8]
GOLD = _ROWS["Messing/Gold"][2:7]
BONE = _ROWS["Haut/Knochen"][5:8]

RECTS = {
    "iron": (0, 0, 64, 32),
    "dark": (64, 0, 128, 32),
    "silver": (0, 32, 64, 56),
    "gold": (64, 32, 128, 56),
    "wood": (0, 56, 64, 96),
    "grip": (64, 56, 128, 96),
    "bone": (0, 96, 64, 128),
    "stake": (64, 96, 112, 128),
    "cord": (112, 96, 128, 128),
}
UV = rects_from_pixels(RECTS, TEX)


def step(ramp, field):
    """Feld 0..1 in harten Stufen auf die Rampe legen: nur Palettenfarben, kein Mischen, kein Dithering."""
    i = np.clip((np.asarray(field) * len(ramp)).astype(int), 0, len(ramp) - 1)
    return ramp[i]


def wood_painter(ramp, seed):
    def paint(U, V_):
        h, w = U.shape
        f = 0.15 + 0.7 * vnoise(h, w, 2, 24, seed)                      # Maserung laeuft entlang v
        f = np.where(vnoise(h, w, 9, 30, seed + 1) < 0.14, 0.0, f)      # dunkle Fasern
        f = np.where(vnoise(h, w, 4, 6, seed + 2) > 0.86, 0.95, f)      # abgegriffene Stellen
        return step(ramp, f)
    return paint


def leather_painter(U, V_):
    h, w = U.shape
    wrap = (U * 2.0 + V_ * 5) % 1.0                                      # schraege Wicklung
    f = np.where(wrap > 0.2, 0.15 + 0.3 * vnoise(h, w, 3, 8, 304), 0.95)
    return step(LEATHER, f)


def iron_painter(U, V_):
    h, w = U.shape
    f = 0.34 + 0.26 * vnoise(h, w, 3, 4, 305)
    f = np.where(vnoise(h, w, 7, 7, 306) < 0.08, 0.05, f)               # vereinzelte Pocken
    return step(STEEL, f)


def dark_painter(U, V_):
    h, w = U.shape
    return step(DARKSTEEL, 0.2 + 0.7 * vnoise(h, w, 5, 5, 307))


def silver_painter(U, V_):
    h, w = U.shape
    f = 0.35 + 0.55 * vnoise(h, w, 4, 16, 308)
    f = np.where(np.abs(V_ - 0.5) < 0.1, 1.0, f)                         # helle Kante
    return step(SILVER, f)


def gold_painter(U, V_):
    h, w = U.shape
    return step(GOLD, 0.25 + 0.7 * vnoise(h, w, 4, 10, 309))


def bone_painter(U, V_):
    h, w = U.shape
    f = 0.35 + 0.55 * vnoise(h, w, 3, 6, 310)
    f = np.where(vnoise(h, w, 8, 14, 311) < 0.1, 0.0, f)                 # feine Risse
    return step(BONE, f)


def stake_painter(U, V_):
    h, w = U.shape
    f = 0.2 + 0.7 * vnoise(h, w, 2, 12, 312)
    f = np.where(vnoise(h, w, 7, 20, 313) < 0.12, 0.0, f)
    return step(WOOD_PALE, f)


def cord_painter(U, V_):
    h, w = U.shape
    return step(WOOD_PALE, 0.55 + 0.4 * vnoise(h, w, 3, 3, 314))


def char_painter(U, V_):
    h, w = U.shape
    return step(CHAR, 0.2 + 0.7 * vnoise(h, w, 3, 8, 315))


def make_atlas(painters):
    atlas = np.zeros((TEX, TEX, 3)) + DARKSTEEL[0]
    for name, (x0, y0, x1, y1) in RECTS.items():
        pw, ph = x1 - x0, y1 - y0
        U, V_ = np.meshgrid((np.arange(pw) + 0.5) / pw, (np.arange(ph) + 0.5) / ph)
        atlas[y0:y1, x0:x1] = painters[name](U, V_)
    return atlas


# --- Geometrie --------------------------------------------------------------------------------------------------------

def circle(n, phase=0.0):
    return [(math.cos(phase + 2 * math.pi * k / n), math.sin(phase + 2 * math.pi * k / n)) for k in range(n)]


PROF_RECT = [(-1, -1), (1, -1), (1, 1), (-1, 1)]


def sweep(part, centres, sizes, rect, profile=None, up=V(0, 1, 0)):
    """Geschlossener Koerper um eine Mittellinie: je Mittelpunkt ein Ring (Vieleck), Kappen als ebene Vielecke.
    sizes[i] = (halbe Breite, halbe Hoehe) in Waffenmass; profile: Liste (a, b) im Einheitsquadrat (Standard: Rechteck)."""
    profile = profile or PROF_RECT
    n = len(profile)
    u0, v0, u1, v1 = part.rects[rect]
    rings = []
    for (axis, side, upv), c, (w, h) in zip(frames([V(*p) for p in centres], up), [V(*p) for p in centres], sizes):
        rings.append([part.bm.verts.new(G(c + side * (w * a) + upv * (h * b) - part.pivot)) for a, b in profile])
    count = len(rings)
    faces = []
    for i in range(count - 1):
        for k in range(n):
            q = (rings[i][k], rings[i][(k + 1) % n], rings[i + 1][(k + 1) % n], rings[i + 1][k])
            uvs = ((u0 + (u1 - u0) * k / n, v0 + (v1 - v0) * i / (count - 1)),
                   (u0 + (u1 - u0) * (k + 1) / n, v0 + (v1 - v0) * i / (count - 1)),
                   (u0 + (u1 - u0) * (k + 1) / n, v0 + (v1 - v0) * (i + 1) / (count - 1)),
                   (u0 + (u1 - u0) * k / n, v0 + (v1 - v0) * (i + 1) / (count - 1)))
            faces.append(part._face(q, uvs))
    for ring in (rings[0], list(reversed(rings[-1]))):
        uvs = [(u0 + (u1 - u0) * (0.5 + 0.4 * a), v0 + (v1 - v0) * (0.5 + 0.4 * b)) for a, b in profile]
        faces.append(part._face(ring, uvs if ring is rings[0] else list(reversed(uvs))))
    bmesh.ops.recalc_face_normals(part.bm, faces=faces)
    return faces


def beam(part, p0, p1, w, h, rect, w1=None, h1=None, up=V(0, 1, 0)):
    """Gerader Balken (Pyramidenstumpf mit Rechteck) von p0 nach p1."""
    return sweep(part, [p0, p1], [(w, h), (w if w1 is None else w1, h if h1 is None else h1)], rect, PROF_RECT, up)


def rod(part, p0, p1, r, rect, sides=6, r1=None, up=V(0, 1, 0)):
    """Runder Stab (Vieleck) von p0 nach p1, an den Enden geschlossen."""
    return sweep(part, [p0, p1], [(r, r), (r if r1 is None else r1, r if r1 is None else r1)], rect, circle(sides), up)


def disc_x(part, c, r, half, rect, sides=8):
    """Walze quer (Achse x), Mittelpunkt c, halbe Laenge half."""
    return sweep(part, [(c[0] - half, c[1], c[2]), (c[0] + half, c[1], c[2])], [(r, r)] * 2, rect, circle(sides, math.pi / sides),
                 up=V(0, 1, 0))


def build_stake(part, y, z0, z1, r, tip_len, tip_rect, sides=6, collar=None):
    """Pflock entlang z auf Hoehe y. Koerper helles Holz, vorn eine zugespitzte Spitze (tip_rect)."""
    zt = z1 - tip_len
    sweep(part, [(0, y, z0), (0, y, z0 + 0.008), (0, y, zt + 0.004)], [(r * 0.8, r * 0.8), (r, r), (r, r)], "stake",
          circle(sides, math.pi / sides))
    sweep(part, [(0, y, zt), (0, y, zt + tip_len * 0.55), (0, y, z1)], [(r * 0.98, r * 0.98), (r * 0.55, r * 0.55), (r * 0.08, r * 0.08)],
          tip_rect, circle(sides, math.pi / sides))
    if collar:
        zc, rc, rect = collar
        sweep(part, [(0, y, zc - 0.005), (0, y, zc + 0.005)], [(rc, rc)] * 2, rect, circle(sides, math.pi / sides))


def build_string(part, tips, nock, r=0.0024):
    """Gespannte Sehne: von jeder Wippenspitze zum Pflockende (V-Form)."""
    for t in tips:
        rod(part, t, nock, r, "cord", sides=4)


def closed_report(parts):
    """Vor dem Bauen: jede Kante gehoert zu genau zwei Flaechen? (offen, mehr als zwei, lose, Kanten)"""
    out = [0, 0, 0, 0]
    for p in parts:
        bm = p.bm
        out[0] += sum(1 for e in bm.edges if len(e.link_faces) == 1)
        out[1] += sum(1 for e in bm.edges if len(e.link_faces) > 2)
        out[2] += sum(1 for e in bm.edges if len(e.link_faces) == 0)
        out[3] += len(bm.edges)
    return out


def export_design(key, label, fname, builder, painters):
    """builder() -> (crossbow_part, stake_part). Schreibt <fname> neben das Skript."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    body, stake = builder()
    open_e, multi, loose, edges = closed_report([body, stake])
    print(f"PRUEFUNG {label}: Kanten {edges}, offene Kanten {open_e}, Kanten mit mehr als zwei Flaechen {multi}, lose Kanten {loose}")
    assert open_e == 0 and multi == 0 and loose == 0, "Modell nicht geschlossen"
    tex = make_atlas(painters)
    colors = len({tuple(c) for c in tex.reshape(-1, 3)})
    assert colors <= 32, colors
    image = save_image("armbrust_" + key, tex, os.path.join(tempfile.gettempdir(), f"armbrust_{key}.png"))
    coll = bpy.data.collections.new("Armbrust")
    bpy.context.scene.collection.children.link(coll)
    mat = export_material("armbrust_" + key, image)
    objs = [body.build(coll, mat, smooth=False), stake.build(coll, mat, smooth=False)]
    tris = [len(o.data.polygons) for o in objs]
    pose(objs, {}, root=Matrix.Scale(SCALE * SHRINK, 4))
    bake_scale(objs)
    export_glb(objs, os.path.join(HERE, fname))
    xs = [v.co.x for o in objs for v in o.data.vertices]
    ys = [v.co.y for o in objs for v in o.data.vertices]
    print(f"MODELL {label}: {sum(tris)} Dreiecke (Schaft+Bogen {tris[0]}, Pflock {tris[1]}), {colors} Farben, Textur {TEX}x{TEX}, "
          f"Spannweite {max(xs) - min(xs):.3f} m, Laenge {max(ys) - min(ys):.3f} m (Dateimass)")
    return sum(tris)


def new_parts():
    return Part("crossbow", (0, 0, 0), UV), Part("stake", (0, 0, 0), UV)


def base_painters(**over):
    p = {"iron": iron_painter, "dark": dark_painter, "silver": silver_painter, "gold": gold_painter,
         "wood": wood_painter(WOOD_DARK, 201), "grip": leather_painter, "bone": bone_painter,
         "stake": stake_painter, "cord": cord_painter}
    p.update(over)
    return p
