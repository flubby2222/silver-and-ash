"""Probestueck Brunnen (Issue #438): drei Entwuerfe, je Zustand bereit / verbraucht.

Alles entsteht aus diesem einen Skript: Geometrie (in Spiel-Achsen: x seitlich, y oben, z vorn, Meter), Farben nur aus
assets/palette/hauptpalette.json (Reihen nach Flaechenneigung in harten Stufen, keine Textur), Modelle (.glb), Bilder, Blatt.

Aufruf (aus dem Repository-Wurzelverzeichnis; absolute Pfade, ein Ausgabeordner als erstes Argument nach --):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/brunnen-438/brunnen.py -- <ausgabeordner>
Ohne Argument schreibt es in den Ordner des Skripts. Zwei Laeufe liefern bitgleiche Dateien (nichts Zufaelliges).
"""

import json
import math
import os
import sys
import tempfile
import zlib

import blf
import bpy
import imbuf
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
OUT = HERE
if "--" in sys.argv and len(sys.argv) > sys.argv.index("--") + 1:
    OUT = os.path.abspath(sys.argv[sys.argv.index("--") + 1])
os.makedirs(OUT, exist_ok=True)

PAL = json.load(open(os.path.join(ROOT, "assets", "palette", "hauptpalette.json"), encoding="utf-8"))
RAMPS = {r["name"]: r["farben"] for r in PAL["grund"] + PAL["akzent"]}
FOG = np.array((16, 11, 20), float)
LIGHT = np.array((-0.35, 0.85, 0.4))
LIGHT /= np.linalg.norm(LIGHT)
STEPS = {"Stein": 3.0}                      # Stufen je Ramp zwischen Unterseite und Oberseite (Stein feiner, die anderen 8-stufig groeber)
Z_WALL = 18.0                               # Rueckwand des Probengangs


# ---------------------------------------------------------------- Geometrie (reines Python)

class M:
    """Ein Modell: Liste konvexer Polygone (Spiel-Achsen) mit Farb-Etikett (Reihe, Grundstufe, Streuung)."""

    def __init__(self):
        self.faces = []

    def face(self, pts, outward, tag):
        p = np.array(pts, float)
        n = np.zeros(3)
        for i in range(len(p)):
            a, b = p[i], p[(i + 1) % len(p)]
            n += np.array(((a[1] - b[1]) * (a[2] + b[2]), (a[2] - b[2]) * (a[0] + b[0]), (a[0] - b[0]) * (a[1] + b[1])))
        if np.dot(n, outward) < 0:
            p = p[::-1]
        self.faces.append((p, tag))

    def tris(self):
        return sum(len(p) - 2 for p, _ in self.faces)

    def bounds(self):
        a = np.concatenate([p for p, _ in self.faces])
        return a.min(0), a.max(0)

    def extend(self, other, off=(0, 0, 0)):
        for p, t in other.faces:
            self.faces.append((p + np.array(off, float), t))
        return self


def box(m, x0, x1, y0, y1, z0, z1, tag):
    c = np.array(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2))
    X, Y, Z = (x0, x1), (y0, y1), (z0, z1)
    for xs, side in ((x0, -1), (x1, 1)):
        m.face([(xs, y, z) for y, z in ((y0, z0), (y1, z0), (y1, z1), (y0, z1))], (side, 0, 0), tag)
    for ys, side in ((y0, -1), (y1, 1)):
        m.face([(x, ys, z) for x, z in ((x0, z0), (x1, z0), (x1, z1), (x0, z1))], (0, side, 0), tag)
    for zs, side in ((z0, -1), (z1, 1)):
        m.face([(x, y, zs) for x, y in ((x0, y0), (x1, y0), (x1, y1), (x0, y1))], (0, 0, side), tag)


def ngon(n, r, rot, cx, cz, y):
    return [(cx + r * math.cos(rot + 2 * math.pi * i / n), y, cz + r * math.sin(rot + 2 * math.pi * i / n)) for i in range(n)]


def prism(m, cx, cz, y0, y1, r0, r1, n, tag, rot=0.0, tag_top=None):
    """Geschlossener Kegelstumpf (r1 = 0: Spitze). Seiten flach, n Ecken, rot in Grad."""
    rot = math.radians(rot)
    low, high = ngon(n, r0, rot, cx, cz, y0), ngon(n, r1, rot, cx, cz, y1)
    for i in range(n):
        j = (i + 1) % n
        mid = (np.array(low[i]) + np.array(low[j])) / 2 - np.array((cx, 0, cz))
        out = (mid[0], 0.0, mid[2])
        if r1 == 0:
            m.face([low[i], low[j], (cx, y1, cz)], out, tag)
        else:
            m.face([low[i], low[j], high[j], high[i]], out, tag)
    m.face(low, (0, -1, 0), tag)
    if r1 > 0:
        m.face(high, (0, 1, 0), tag_top or tag)


def ring(m, cx, cz, y0, y1, r_out, r_in, n, tag, rot=0.0, tag_top=None):
    """Geschlossener Ring (Mauer mit Loch): Aussenwand, Innenwand, Oberseite, Unterseite."""
    rot = math.radians(rot)
    ob, ot = ngon(n, r_out, rot, cx, cz, y0), ngon(n, r_out, rot, cx, cz, y1)
    ib, it = ngon(n, r_in, rot, cx, cz, y0), ngon(n, r_in, rot, cx, cz, y1)
    for i in range(n):
        j = (i + 1) % n
        mid = (np.array(ob[i]) + np.array(ob[j])) / 2 - np.array((cx, 0, cz))
        r = (mid[0], 0.0, mid[2])
        m.face([ob[i], ob[j], ot[j], ot[i]], r, tag)
        m.face([ib[i], ib[j], it[j], it[i]], (-r[0], 0, -r[2]), tag)
        m.face([ot[i], ot[j], it[j], it[i]], (0, 1, 0), tag_top or tag)
        m.face([ob[i], ob[j], ib[j], ib[i]], (0, -1, 0), tag)


def extrude(m, poly, z0, z1, tag):
    """Konvexes Vieleck (x, y) von z0 bis z1 ausgezogen (zum Beispiel Spitzgiebel)."""
    c = np.mean(poly, axis=0)
    m.face([(x, y, z1) for x, y in poly], (0, 0, 1), tag)
    m.face([(x, y, z0) for x, y in poly], (0, 0, -1), tag)
    for i in range(len(poly)):
        a, b = poly[i], poly[(i + 1) % len(poly)]
        mid = ((a[0] + b[0]) / 2 - c[0], (a[1] + b[1]) / 2 - c[1], 0)
        m.face([(a[0], a[1], z0), (b[0], b[1], z0), (b[0], b[1], z1), (a[0], a[1], z1)], mid, tag)


# ---------------------------------------------------------------- Farben

def shade(tag, n, centre):
    """Palettenfarbe einer Flaeche: Grundstufe + Stufe nach Neigung (Licht von oben links) + Streuung je Stein."""
    ramp, base, vary = tag
    colors = RAMPS[ramp]
    k = STEPS.get(ramp, 1.5)
    off = int(round(float(np.dot(n, LIGHT)) * k))
    var = 0
    if vary:
        h = zlib.crc32(("%.2f,%.2f,%.2f" % tuple(np.round(centre, 2))).encode()) % 3
        var = h - 1
    return np.array(colors[int(np.clip(base + off + var, 0, len(colors) - 1))], float)


def S(base, vary=True):        # Stein
    return ("Stein", base, vary)


def W(base):                   # Wasser: Reihe Stoff (kalt), harte Flaechen
    return ("Stoff", base, False)


def I(base):                   # Eisen / Silber
    return ("Eisen", base, False)


def G_(base):                  # Messing/Gold: nur das Funkeln der Beute
    return ("Messing/Gold", base, False)


# ---------------------------------------------------------------- Entwuerfe

def fountain_a(state):
    """A Schalenbrunnen: achteckige Schale, Mittelsaeule mit kleiner Schale und Spitze, vier Zinnen am Rand. Freistehend."""
    m = M()
    prism(m, 0, 0, 0.0, 0.12, 0.74, 0.72, 8, S(4), rot=22.5)                       # Sockelstufe
    ring(m, 0, 0, 0.12, 0.80, 0.68, 0.52, 8, S(7), rot=22.5)                        # Beckenwand
    ring(m, 0, 0, 0.80, 0.88, 0.71, 0.49, 8, S(11), rot=22.5)                       # Deckplatte
    prism(m, 0, 0, 0.12, 0.20, 0.52, 0.52, 8, S(2), rot=22.5)                       # Beckenboden
    for k in range(4):                                                              # Zinnen auf jeder zweiten Ecke
        a = math.radians(22.5 + 90 * k)
        prism(m, 0.62 * math.cos(a), 0.62 * math.sin(a), 0.88, 1.16, 0.07, 0.0, 4, S(9), rot=45)
    prism(m, 0, 0, 0.20, 1.20, 0.15, 0.13, 8, S(6), rot=22.5)                       # Saeule
    prism(m, 0, 0, 1.20, 1.38, 0.17, 0.36, 8, S(8), rot=22.5, tag_top=S(8) if state == "bereit" else S(3))   # obere Schale
    prism(m, 0, 0, 1.38, 1.78, 0.10, 0.0, 8, S(10), rot=22.5)                       # Spitze
    if state == "bereit":
        prism(m, 0, 0, 0.20, 0.66, 0.50, 0.50, 8, W(2), rot=22.5, tag_top=W(5))    # Wasser im Becken
        for x, z in ((0.30, 0.15), (-0.28, -0.22), (0.05, -0.38), (-0.20, 0.33)):
            box(m, x - 0.04, x + 0.04, 0.66, 0.675, z - 0.04, z + 0.04, G_(5))       # Funkeln der Beute
        prism(m, 0, 0, 1.38, 1.40, 0.29, 0.29, 8, W(4), rot=22.5, tag_top=W(5))    # Wasser in der oberen Schale
    else:
        for x, z, h in ((0.28, 0.2, 0.05), (-0.30, -0.15, 0.04), (0.05, -0.38, 0.06)):   # Steinbrocken und stumpfe Muenzen
            box(m, x - 0.05, x + 0.05, 0.20, 0.20 + h, z - 0.05, z + 0.05, S(5))
        for x, z in ((-0.10, 0.30), (0.34, -0.20)):
            box(m, x - 0.035, x + 0.035, 0.20, 0.212, z - 0.035, z + 0.035, I(3))
    return m


def fountain_b(state):
    """B Wandbrunnen: Spitzgiebel-Platte mit Maskenstein und Rinne, haelt eine Wand. Wand bei z = 0, Front nach -z."""
    m = M()
    extrude(m, [(-0.78, 0.0), (0.78, 0.0), (0.78, 1.95), (0.0, 2.55), (-0.78, 1.95)], -0.30, 0.0, S(7))       # Platte
    extrude(m, [(-0.52, 0.55), (0.52, 0.55), (0.52, 1.85), (0.0, 2.30), (-0.52, 1.85)], -0.33, -0.30, S(3, False))  # Nische (dunkel)
    extrude(m, [(-0.16, 1.62), (0.16, 1.62), (0.0, 1.38)], -0.40, -0.33, S(10))                              # Maskenstein (Schlussstein)
    box(m, -0.05, 0.05, 1.18, 1.38, -0.62, -0.36, I(3))                                                    # Speier (Eisenrohr)
    box(m, -0.10, 0.10, 1.12, 1.18, -0.62, -0.50, I(2))
    # Rinne: vier Waende und Boden, geschlossen
    box(m, -0.70, 0.70, 0.0, 0.14, -0.72, -0.30, S(4))                                                     # Boden
    box(m, -0.70, 0.70, 0.14, 0.78, -0.72, -0.60, S(8))                                                    # Front
    box(m, -0.70, -0.58, 0.14, 0.78, -0.60, -0.30, S(8))                                                   # links
    box(m, 0.58, 0.70, 0.14, 0.78, -0.60, -0.30, S(8))                                                     # rechts
    box(m, -0.74, 0.74, 0.78, 0.86, -0.76, -0.30, S(11))                                                   # Deckleiste
    if state == "bereit":
        box(m, -0.58, 0.58, 0.14, 0.68, -0.60, -0.30, W(3))                                                # Wasser (Oberseite hell)
        box(m, -0.045, 0.045, 0.68, 1.12, -0.58, -0.50, W(5))                                               # Strahl
        for x in (-0.30, 0.30, 0.0):
            box(m, x - 0.04, x + 0.04, 0.685, 0.695, -0.52 if x == 0 else -0.42, -0.44 if x == 0 else -0.34, G_(5))
    else:
        box(m, -0.58, 0.58, 0.14, 0.20, -0.60, -0.30, S(2))                                                # trockene Rinne
        box(m, -0.05, 0.05, 0.20, 0.26, -0.58, -0.50, S(5))                                                # Brocken
        box(m, -0.10, 0.10, 0.20, 0.205, -0.40, -0.34, I(3))
        box(m, -0.06, 0.06, 0.90, 1.12, -0.35, -0.33, S(1, False))                                         # dunkle Wasserspur an der Nische
    return m


def fountain_c(state):
    m = M()
    ring(m, 0, 0, 0.0, 0.72, 0.64, 0.40, 8, S(6), rot=22.5, tag_top=S(10))
    prism(m, 0, 0, 0.0, 0.14, 0.52, 0.52, 8, S(0, False), rot=22.5)
    for sx in (-1, 1):
        box(m, sx * 0.62 - 0.07, sx * 0.62 + 0.07, 0.72, 1.95, -0.07, 0.07, S(8))
    # Dach: Dreiecksprofil in der z-y-Ebene, entlang x gezogen: als um 90 Grad gedrehte Giebelform gebaut
    ridge = M()
    extrude(ridge, [(-0.50, 0.0), (0.50, 0.0), (0.0, 0.34)], -0.80, 0.80, S(5))
    for p, t in ridge.faces:                                                                                 # (x, y, z) -> (z, y + 1,95, x)
        m.faces.append((np.array([(q[2], q[1] + 1.95, q[0]) for q in p])[::-1], t))
    box(m, -0.55, 0.55, 1.50, 1.58, -0.045, 0.045, I(3))                                                    # Winden-Achse (Eisen)
    box(m, -0.09, 0.09, 1.43, 1.65, -0.09, 0.09, I(4))                                                      # Trommel
    if state == "bereit":
        prism(m, 0, 0, 0.14, 0.50, 0.40, 0.40, 8, W(1), rot=22.5, tag_top=W(3))                             # dunkles Wasser im Schacht
        for x, z in ((0.12, 0.10), (-0.14, -0.08)):
            box(m, x - 0.04, x + 0.04, 0.50, 0.515, z - 0.04, z + 0.04, G_(5))
    else:
        prism(m, 0, 0, 0.62, 0.70, 0.46, 0.46, 8, I(2), rot=22.5, tag_top=I(3))                             # Deckel
        box(m, -0.40, 0.40, 0.70, 0.76, -0.04, 0.04, I(4))                                                  # Riegelbalken
        box(m, -0.04, 0.04, 0.70, 0.76, -0.40, 0.40, I(4))
        prism(m, 0, 0, 0.76, 0.82, 0.07, 0.07, 8, I(5))                                                     # Knauf
    return m


FOUNTAINS = {"A": ("Schalenbrunnen", fountain_a), "B": ("Wandbrunnen", fountain_b), "C": ("Schachtbrunnen", fountain_c)}
# Platz im Zellenraster (Mitte der Zelle, 2 x 2 m); B steht an der Wand und wird so verschoben, dass seine Masse mittig liegt
CELL_SHIFT = {"A": (0, 0, 0), "B": (0, 0, 0.38), "C": (0, 0, 0)}


def mannequin():
    """Massfigur 1,8 m (Spielergroesse): Kasten-Mensch in Stoff, Kopf Haut. Nur Massstab."""
    m = M()
    for sx in (-1, 1):
        box(m, sx * 0.20 - 0.09, sx * 0.20 + 0.09, 0.0, 0.85, -0.10, 0.10, ("Stoff", 2, False))
        box(m, sx * 0.33 - 0.06, sx * 0.33 + 0.06, 0.85, 1.42, -0.07, 0.07, ("Stoff", 3, False))
    box(m, -0.28, 0.28, 0.85, 1.48, -0.13, 0.13, ("Stoff", 4, False))
    box(m, -0.10, 0.10, 1.48, 1.80, -0.10, 0.10, ("Haut/Knochen", 4, False))
    return m


def chest():
    """Kiste aus dem Spiel (Worn): 1,0 x 0,7 x 0,7 m, Holz 3 (110, 76, 46)."""
    m = M()
    box(m, -0.5, 0.5, 0.0, 0.455, -0.35, 0.35, ("Holz", 3, False))
    box(m, -0.5, 0.5, 0.455, 0.70, -0.35, 0.35, ("Holz", 3, False))
    return m


def ruler(height=2.5):
    m = M()
    k = 0
    y = 0.0
    while y < height - 1e-6:
        box(m, -0.04, 0.04, y, y + 0.5, -0.04, 0.04, S(9 if k % 2 == 0 else 5, False))
        y += 0.5
        k += 1
    return m


# ---------------------------------------------------------------- Raum, Bildfolge

def room():
    """Probengang: 4 m breit (zwei Zellen), 3,5 m hoch, Stein-Rampe, Kacheln 0,5 m (fuer Nebel je Flaeche)."""
    m = M()
    t = 0.5
    xs = np.arange(-2.0, 2.0 + 1e-6, t)
    zs = np.arange(-4.0, Z_WALL + 1e-6, t)
    ys = np.arange(0.0, 3.5 + 1e-6, t)
    for i in range(len(xs) - 1):
        for j in range(len(zs) - 1):
            x0, x1, z0, z1 = xs[i], xs[i + 1], zs[j], zs[j + 1]
            m.face([(x0, 0, z0), (x1, 0, z0), (x1, 0, z1), (x0, 0, z1)], (0, 1, 0), S(3))
            m.face([(x0, 3.5, z0), (x1, 3.5, z0), (x1, 3.5, z1), (x0, 3.5, z1)], (0, -1, 0), S(2))
    for j in range(len(zs) - 1):
        for k in range(len(ys) - 1):
            for xw, side in ((-2.0, 1), (2.0, -1)):
                m.face([(xw, ys[k], zs[j]), (xw, ys[k + 1], zs[j]), (xw, ys[k + 1], zs[j + 1]), (xw, ys[k], zs[j + 1])], (side, 0, 0), S(5))
    for i in range(len(xs) - 1):
        for k in range(len(ys) - 1):
            m.face([(xs[i], ys[k], Z_WALL), (xs[i + 1], ys[k], Z_WALL), (xs[i + 1], ys[k + 1], Z_WALL), (xs[i], ys[k + 1], Z_WALL)], (0, 0, -1), S(5))
    return m


def slab():
    m = M()
    box(m, -1.0, 1.0, -0.10, 0.0, -1.0, 1.0, S(3))
    return m


def to_bl(p):
    """Spiel-Achsen -> Blender (Drehung um x um 90 Grad, bleibt rechtshaendig): (x, y, z) -> (x, -z, y)."""
    return (p[0], -p[2], p[1])


def srgb_lin(c):
    c = np.asarray(c, float) / 255.0
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def build_object(name, parts, cam=None, fog=False):
    """parts: Liste (Modell, Verschiebung). Farbe je Flaeche; bei fog Laterne + Nebel nach Abstand der Flaechenmitte zur Kamera."""
    verts, polys, cols = [], [], []
    for model, off in parts:
        o = np.array(off, float)
        for p, tag in model.faces:
            p = p + o
            n = np.cross(p[1] - p[0], p[2] - p[0])
            n /= np.linalg.norm(n)
            c = p.mean(0)
            col = shade(tag, n, c)
            if fog:
                d = float(np.linalg.norm(c - cam))
                bright = 1 - 0.65 * min(d / 18.2, 1.0)
                f = np.clip((d - 3.25) / (18.2 - 3.25), 0, 1)
                f = f * f * (3 - 2 * f)
                col = col * bright * (1 - f) + FOG * f
            base = len(verts)
            verts.extend(to_bl(q) for q in p)
            polys.append(list(range(base, base + len(p))))
            cols.extend([srgb_lin(col)] * len(p))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], polys)
    mesh.update()
    attr = mesh.color_attributes.new("Col", "FLOAT_COLOR", "CORNER")
    rgba = np.array([[*c, 1.0] for c in cols], np.float32)
    # Schleifen-Reihenfolge entspricht der Reihenfolge der Polygone
    attr.data.foreach_set("color", rgba.ravel())
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def setup(w, h):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.render.resolution_x, sc.render.resolution_y = w, h
    sc.render.resolution_percentage = 100
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGB"
    sc.render.film_transparent = False
    sc.view_settings.view_transform = "Standard"
    sc.view_settings.look = "None"
    sc.display_settings.display_device = "sRGB"
    sc.display.render_aa = "OFF"
    sc.display.shading.light = "FLAT"
    sc.display.shading.color_type = "VERTEX"
    sc.display.shading.show_cavity = False
    sc.display.shading.show_object_outline = False
    sc.display.shading.show_specular_highlight = False
    sc.render.dither_intensity = 0.0
    world = bpy.data.worlds.new("w")
    world.color = tuple(srgb_lin(FOG))
    sc.world = world
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    return sc, cam


def aim(cam, loc, target):
    cam.location = to_bl(loc)
    d = Vector(to_bl(target)) - Vector(to_bl(loc))
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()


def grab(sc):
    path = os.path.join(tempfile.gettempdir(), "brunnen_render.png")
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    im = bpy.data.images.load(path)
    im.colorspace_settings.name = "Non-Color"
    w, h = im.size
    a = np.array(im.pixels[:], np.float32).reshape(h, w, 4)[::-1, :, :3] * 255.0
    bpy.data.images.remove(im)
    return np.round(a)


def render(parts, loc, target, size, fov=None, ortho=None, fog=False):
    sc, cam = setup(*size)
    if ortho:
        cam.data.type = "ORTHO"
        cam.data.ortho_scale = ortho
    else:
        cam.data.sensor_fit = "VERTICAL"
        cam.data.angle = math.radians(fov)
    cam.data.clip_start, cam.data.clip_end = 0.05, 80
    aim(cam, loc, target)
    build_object("scene", parts, cam=np.array(loc, float), fog=fog)
    return grab(sc)


BAYER = np.array(((0, 8, 2, 10), (12, 4, 14, 6), (3, 11, 1, 9), (15, 7, 13, 5)), float)


def bayer15(img):
    """15 Bit (5 je Kanal) mit festem 4 x 4-Punktmuster an der Bildschirmposition, wie ADR 0010."""
    h, w = img.shape[:2]
    t = (np.tile(BAYER, (h // 4 + 1, w // 4 + 1))[:h, :w] + 0.5) / 16.0
    q = np.clip(np.floor(img / 255.0 * 31.0 + t[..., None]), 0, 31)
    return np.round(q * 255.0 / 31.0)


def save_png(arr, path):
    """arr: Zeilen von oben, 0..255."""
    h, w = arr.shape[:2]
    im = bpy.data.images.new(os.path.basename(path), w, h)
    im.pixels = np.concatenate([np.clip(arr, 0, 255)[::-1] / 255.0, np.ones((h, w, 1))], axis=2).ravel()
    im.filepath_raw = path
    im.file_format = "PNG"
    im.save()
    bpy.data.images.remove(im)


def text(img, s, x, y, size=18, color=(235, 230, 240)):
    f = blf.load("C:/Windows/Fonts/arial.ttf")
    blf.size(f, size)
    w, h = blf.dimensions(f, s)
    Wd, Hd = int(w) + 8, int(size * 1.6)
    ib = imbuf.new((Wd, Hd))
    blf.color(f, 1, 1, 1, 1)
    blf.position(f, 4, int(size * 0.4), 0)
    with blf.bind_imbuf(f, ib):
        blf.draw_buffer(f, s)
    tmp = os.path.join(tempfile.gettempdir(), "brunnen_txt.png")
    ib.filepath = tmp
    imbuf.write(ib)
    im = bpy.data.images.load(tmp)
    im.colorspace_settings.name = "Non-Color"
    a = np.array(im.pixels[:], np.float32).reshape(Hd, Wd, 4)[::-1, :, 3]
    bpy.data.images.remove(im)
    hh, ww = img.shape[:2]
    ye, xe = min(hh, y + Hd), min(ww, x + Wd)
    if ye <= y or xe <= x:
        return
    mk = (a[:ye - y, :xe - x] > 0.5)[..., None]
    img[y:ye, x:xe] = np.where(mk, np.array(color, float), img[y:ye, x:xe])


def export_glb(model, path):
    sc, _ = setup(64, 64)
    obj = build_object("brunnen", [(model, (0, 0, 0))])
    for o in bpy.data.objects:
        o.select_set(o is obj)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True, export_vertex_color="ACTIVE",
                              export_cameras=False, export_lights=False, export_apply=True)


# ---------------------------------------------------------------- Bilder

def views(model, shift, T=380):
    """Vier Ansichten (vorn, Seite, hinten, schraeg oben) auf der Bodenplatte der 2 x 2-m-Zelle."""
    lo, hi = model.bounds()
    centre = (lo + hi) / 2 + np.array(shift)
    height = hi[1]
    tgt = (0.0, max(0.7, height * 0.45), 0.0)
    out = {}
    for name, d in (("vorn", (0, 0.12, -1)), ("Seite", (1, 0.12, 0)), ("hinten", (0, 0.12, 1)), ("schräg oben", (-0.7, 0.75, -0.8))):
        d = np.array(d, float)
        d /= np.linalg.norm(d)
        loc = np.array(tgt) + d * 4.6
        out[name] = render([(slab(), (0, 0, 0)), (model, shift)], tuple(loc), tgt, (T, T), fov=34)
    return out


def game_view(key, state, dist, w=854, h=480):
    model = FOUNTAINS[key][1](state)
    ref = 17.0 if key != "B" else 17.62
    shiftz = 17.0 if key != "B" else Z_WALL
    loc = (0.0, 1.6, ref - dist)
    return bayer15(render([(room(), (0, 0, 0)), (model, (0, 0, shiftz))], loc, (0.0, 1.6, ref - dist + 10), (w, h), fov=70, fog=True))


def scale_row(state="bereit", W_=1736, H_=560):
    """Frontansicht aller drei mit Massfigur, Kiste und Meterstab, gleicher Massstab (orthogonal, 200 Pixel je Meter)."""
    px = 200.0
    items = [(ruler(), -3.95, "Meterstab"), (mannequin(), -3.25, "Figur 1,8 m"), (chest(), -2.30, "Kiste 1,0 x 0,7 m")]
    for k, x in (("A", -0.95), ("B", 0.95), ("C", 2.95)):
        items.append((FOUNTAINS[k][1](state), x, None))
    parts = [(m, (x, 0, 0)) for m, x, _ in items]
    parts.append((slab_wide(), (0, 0, 0)))
    cx = (W_ / 2) / px - 4.2
    cy = H_ / px / 2 - 0.15
    img = render(parts, (cx, cy, -20), (cx, cy, 0), (W_, H_), ortho=W_ / px)[:, ::-1]   # von -z gesehen liegt +x links: spiegeln
    return img, px, cx, cy


def slab_wide():
    m = M()
    box(m, -5.0, 5.0, -0.12, 0.0, -1.0, 1.0, S(3))
    return m


def main():
    report = []
    # Modelle
    for k, (name, fn) in FOUNTAINS.items():
        for state in ("bereit", "verbraucht"):
            mdl = fn(state)
            export_glb(mdl, os.path.join(OUT, "brunnen_%s_%s.glb" % (k.lower(), state)))
            lo, hi = mdl.bounds()
            report.append((k, name, state, mdl.tris(), hi - lo, lo, hi))
    for r in report:
        print("%s %s %s: %d Dreiecke, Mass %.2f x %.2f x %.2f m (x, y, z)" % (r[0], r[1], r[2], r[3], *r[4]))
    # Ansichten je Entwurf: vier Ansichten, bereit (obere Zeile) und verbraucht (untere Zeile)
    T = 380
    for k, (name, fn) in FOUNTAINS.items():
        sheet = np.zeros((2 * (T + 34) + 44, 4 * T + 3 * 6, 3)) + FOG
        text(sheet, "Entwurf %s – %s: vier Ansichten, bereit (oben) und verbraucht (unten); Platte = Zelle 2 x 2 m" % (k, name), 8, 4, 22)
        for r, state in enumerate(("bereit", "verbraucht")):
            v = views(fn(state), CELL_SHIFT[k], T)
            for c, (vn, img) in enumerate(v.items()):
                x0, y0 = c * (T + 6), 44 + r * (T + 34)
                text(sheet, "%s – %s" % (state, vn), x0 + 4, y0, 20)
                sheet[y0 + 32:y0 + 32 + T, x0:x0 + T] = img
        save_png(sheet, os.path.join(OUT, "ansichten_%s.png" % k.lower()))
    # Spielbedingungen
    game = {}
    for k in FOUNTAINS:
        for state in ("bereit", "verbraucht"):
            for d in (2, 4, 10):
                img = game_view(k, state, d)
                save_png(img, os.path.join(OUT, "spiel_%s_%s_%dm.png" % (k.lower(), state, d)))
                game[(k, state, d)] = img
    # Massstab
    img, px, cx, cy = scale_row("bereit")
    save_png(img, os.path.join(OUT, "massstab.png"))
    # Blatt
    blatt(game, img, px, cx, cy)


def blatt(game, scale_img, px, cx, cy):
    TW, TH, g = 560, 315, 14
    colw = TW + g
    head = 110
    width = 3 * colw + g
    rows = []
    # Zeilen: Titel, Kopf A/B/C, 4 m bereit, 4 m verbraucht, 2 m bereit (nah), 10 m bereit, Massstab, Empfehlung
    sheet_h = head + 80 + (TH + 40) * 4 + 10 + 40 + 560 + 20 + 190
    sh = np.zeros((sheet_h, width, 3)) + FOG
    text(sh, "Silver & Ash – Brunnen (Issue #438): drei Entwürfe, Probe", 18, 8, 44, (235, 230, 240))
    text(sh, "Vorläufige Annahme: zwei Zustände, bereit (Wasser, Beute funkelt) und verbraucht (Höchstzahl an Würfen erreicht)", 18, 62, 24, (190, 185, 200))
    names = {"A": "A  Schalenbrunnen", "B": "B  Wandbrunnen", "C": "C  Schachtbrunnen"}
    sub = {"A": "freistehend, achteckig, 1,4 m Durchmesser", "B": "an der Wand, 1,6 m breit, 0,75 m tief", "C": "freistehend, mit Dach, 1,6 m breit, 1,2 m tief"}
    y = head
    for c, k in enumerate("ABC"):
        x0 = g + c * colw
        text(sh, names[k], x0, y - 18, 40, (240, 232, 210))
        text(sh, sub[k], x0, y + 32, 22, (190, 185, 200))
    y += 80
    for label, state, d in (("bereit, aus 4 m", "bereit", 4), ("verbraucht, aus 4 m", "verbraucht", 4), ("bereit, aus 2 m (so nah steht der Spieler davor)", "bereit", 2), ("bereit, aus 10 m (Nebel, Körnung)", "bereit", 10)):
        for c, k in enumerate("ABC"):
            x0 = g + c * colw
            text(sh, label, x0, y, 22)
            # Verkleinern 854 x 480 -> 560 x 315 (Mittelwert ueber Bloecke, Naeherung durch Auswahl der Pixelspalten)
            xi = (np.arange(TW) * 854 / TW).astype(int)
            yi = (np.arange(TH) * 480 / TH).astype(int)
            sh[y + 34:y + 34 + TH, x0:x0 + TW] = game[(k, state, d)][yi][:, xi]
        y += TH + 40
    y += 10
    text(sh, "Maßstab (bereit): Meterstab mit 0,5-m-Teilung, Figur 1,8 m, Kiste (Worn) aus dem Spiel, dann A, B, C", 18, y, 26)
    sh[y + 40:y + 40 + 560, 0:scale_img.shape[1]] = scale_img[:, :width]
    # Beschriftung im Massstabsbild
    for xx, lab in ((-3.95, "0,5 m je Band"), (-3.25, "Figur 1,8 m"), (-2.30, "Kiste 0,7 m"), (-0.95, "A 1,78 m"), (0.95, "B 2,55 m"), (2.95, "C 2,29 m")):
        sx = int((xx - cx) * px + (scale_img.shape[1] / 2)) - 50
        text(sh, lab, max(0, sx), y + 40 + 6, 20, (235, 230, 240))
    y += 40 + 560 + 20
    text(sh, "Empfehlung: C Schachtbrunnen (Wunschbrunnen)", 18, y, 34, (240, 232, 210))
    for i, line in enumerate(("Klassisches Bild für „Silber hineinwerfen“; das Dach macht aus 4 m und 10 m die größte, klarste Silhouette (2,29 m),",
                              "und der Zustandswechsel ist am deutlichsten: Wasser mit Funkeln gegen ein geschlossenes Eisengitter.",
                              "A ist die Alternative (schönste Nähe, aber kleinere Silhouette); B spart Bodenfläche (0,75 m Tiefe), wenn Zellen knapp sind.")):
        text(sh, line, 18, y + 52 + i * 34, 22, (220, 215, 228))
    save_png(sh, os.path.join(OUT, "blatt.png"))


main()
