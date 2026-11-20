"""Probestueck Skelett mit Armbrust (Issue #334): drei Fassungen A/B/C, Probenblatt und Bilder.

Alles entsteht aus diesem Skript: Geometrie, Textur-Atlas (nur Farben der Hauptpalette), glb-Modelle, Bilder.
Nichts wird heruntergeladen; das Spiel wird nicht angefasst.

Aufruf (aus dem Repository-Wurzelverzeichnis, ohne Argumente):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/armbrust-skelett-334/armbrust.py

Ausgabe in diesem Ordner: blatt.png, spiel-10m.png, spiel-ankuendigung-4m.png, ansichten-<A|B|C>.png,
modelle/armbrust_skelett_<A|B|C>.glb, texturen/atlas_<ruhe|angriff>.png, masse.txt.
Koordinaten: Spiel-Achsen (x seitlich, y oben, z vorn = Blickrichtung der Figur), Meter, Ursprung zwischen den Fuessen.
"""

import json
import math
import os
import sys
import tempfile

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
sys.path.insert(0, os.path.join(ROOT, "tools", "models"))
import blf  # noqa: E402
import bpy  # noqa: E402
import imbuf  # noqa: E402
from mathutils import Matrix  # noqa: E402
from psx import (V, G, FOG_COLOR, Part, tube, rects_from_pixels, vnoise, save_image, export_material,  # noqa: E402
                 pass_material, setup_render, camera, aim, render_passes, compose, save_png, write_light,
                 stone_textures, build_room, export_glb, to_game, MATS, coverage)

MODELS = os.path.join(ROOT, "assets", "models")
VARIANTS = ("A", "B", "C")
NAMES = {"A": "Wächter", "B": "Kapuzenschütze", "C": "Schildträger"}

PAL = {r["name"]: np.array(r["farben"], float)
       for k in ("grund", "akzent")
       for r in json.load(open(os.path.join(ROOT, "assets", "palette", "hauptpalette.json"), encoding="utf-8"))[k]}
ST, HK, SF, HZ, EI, GO, BR = (PAL["Stein"], PAL["Haut/Knochen"], PAL["Stoff"], PAL["Holz"], PAL["Eisen"],
                              PAL["Messing/Gold"], PAL["Blutrot"])

# --- Textur-Atlas 128 x 128 (y von unten) ----------------------------------------------------------------------------

RECTS_PX = {"skull": (0, 80, 64, 128), "bone": (64, 96, 96, 128), "bone_dark": (96, 96, 128, 128),
            "cloth": (0, 40, 32, 80), "cloth_dark": (32, 40, 64, 80), "iron": (64, 40, 96, 80),
            "wood": (96, 40, 128, 80), "leather": (0, 0, 32, 40)}
UV = rects_from_pixels(RECTS_PX, 128)


def pick(n, levels, colors):
    """Wert 0..1 -> harte Stufen aus Palettenfarben (kein Verlauf)."""
    out = np.zeros(n.shape + (3,))
    idx = np.zeros(n.shape, int)
    for k, t in enumerate(levels):
        idx[n >= t] = k + 1
    for k, c in enumerate(colors):
        out[idx == k] = c
    return out


def p_bone(U, V_):
    h, w = U.shape
    col = pick(vnoise(h, w, 4, 4, 11), (0.3, 0.62), (HK[5], HK[6], HK[7]))
    col[np.abs(U - 0.5) < 0.1] = HK[7]                     # Licht von oben (Ecke sides/2 oben)
    col[(U < 0.12) | (U > 0.88)] = HK[4]                   # Unterseite im Schatten
    col[(V_ < 0.1) | (V_ > 0.9)] = HK[4]                   # Gelenkenden
    return col


def p_bone_dark(U, V_):
    h, w = U.shape
    col = pick(vnoise(h, w, 4, 4, 12), (0.35, 0.7), (HK[3], HK[4], HK[5]))
    col[np.abs(U - 0.5) < 0.1] = HK[5]
    return col


def p_skull(angry):
    def paint(U, V_):
        h, w = U.shape
        col = pick(vnoise(h, w, 5, 5, 13), (0.3, 0.65), (HK[5], HK[6], HK[7]))
        col[V_ > 0.78] = HK[7]
        col[(np.abs(U - 0.5) > 0.13) & (np.abs(U - 0.5) < 0.23) & (V_ > 0.12) & (V_ < 0.34)] = HK[4]   # hohle Wangen
        col[(U < 0.2) | (U > 0.8)] = HK[5]                  # Hinterkopf
        col[(V_ < 0.04)] = HK[4]
        for sgn in (-1, 1):
            cu = 0.5 + sgn * 0.085
            sock = ((U - cu) / 0.06) ** 2 + ((V_ - 0.43 - sgn * 0.01) / 0.12) ** 2 < 1
            col[sock] = EI[0]
            pup = ((U - cu) / 0.026) ** 2 + ((V_ - 0.42) / 0.05) ** 2 < 1
            if angry:
                col[pup] = BR[6]                            # Blutrot, hart, ohne Schein (Urteil 22)
                col[pup & (((U - cu) / 0.026) ** 2 + ((V_ - 0.42) / 0.05) ** 2 > 0.45)] = BR[5]
            else:
                col[pup] = HK[3]                            # tote, truebe Augenpunkte
        nose = (V_ > 0.2) & (V_ < 0.33) & (np.abs(U - 0.5) < 0.012 + 0.05 * (0.33 - V_))
        col[nose] = EI[0]
        mouth = (V_ < 0.15) & (np.abs(U - 0.5) < 0.13)
        tooth = ((U * 44) % 1.0 < 0.6) & (V_ > 0.045 + 0.025 * np.sin(U * 90))
        col[mouth] = EI[0]
        col[mouth & tooth] = HK[7]
        return col
    return paint


def p_cloth(U, V_):
    h, w = U.shape
    folds = 0.5 + 0.5 * np.cos(2 * np.pi * U * 8 + 2.5 * vnoise(h, w, 2, 6, 14))
    col = pick(folds, (0.35, 0.7), (SF[3], SF[4], SF[5]))
    col[V_ < 0.1] = SF[2]                                   # Saum dunkler
    return col


def p_cloth_dark(U, V_):
    h, w = U.shape
    return pick(vnoise(h, w, 5, 5, 15), (0.4, 0.75), (SF[2], SF[3], SF[4]))


def p_iron(U, V_):
    h, w = U.shape
    col = pick(vnoise(h, w, 4, 4, 16), (0.4, 0.75), (EI[3], EI[4], EI[5]))
    col[(V_ < 0.08) | (V_ > 0.92)] = EI[6]
    col[(U < 0.06) | (U > 0.94)] = EI[2]
    return col


def p_wood(U, V_):
    h, w = U.shape
    grain = 0.5 + 0.5 * np.sin(2 * np.pi * (U * 5 + 3 * vnoise(h, w, 2, 2, 17)))
    return pick(grain, (0.4, 0.8), (HZ[3], HZ[4], HZ[5]))


def p_leather(U, V_):
    h, w = U.shape
    return pick(vnoise(h, w, 4, 4, 18), (0.45, 0.8), (HZ[1], HZ[2], HZ[3]))


def make_atlas(angry):
    a = np.zeros((128, 128, 3)) + EI[0]
    painters = {"skull": p_skull(angry), "bone": p_bone, "bone_dark": p_bone_dark, "cloth": p_cloth,
                "cloth_dark": p_cloth_dark, "iron": p_iron, "wood": p_wood, "leather": p_leather}
    for name, (x0, y0, x1, y1) in RECTS_PX.items():
        ph, pw = y1 - y0, x1 - x0
        U, V_ = np.meshgrid((np.arange(pw) + 0.5) / pw, (np.arange(ph) + 0.5) / ph)
        a[y0:y1, x0:x1] = painters[name](U, V_)
    return a


# --- Bausteine -----------------------------------------------------------------------------------------------------

def bone(p, pts, rad, rect="bone", sides=6, ref=None):
    """Knochen: Rohr durch pts (Spiel-Achsen), Radien je Punkt (Gelenkenden etwas dicker)."""
    c = [V(*q) for q in pts]
    axis = (c[-1] - c[0]).normalized()
    ref = ref or (V(0, 0, 1) if abs(axis.y) > 0.7 else V(0, 1, 0))
    p.loft(tube(c, [(r, r, r) for r in rad], sides, ref_up=ref), rect)


def skull(p, c, s=1.0):
    """Schaedel wie beim Ghul gebaut (Gesicht vorn bei Ecke n/2), aufrecht, ohne Neigung."""
    n = 10
    rows = [(-0.085, 0.040, 0.070, 0.025), (-0.045, 0.058, 0.078, 0.060), (-0.005, 0.068, 0.074, 0.080),
            (0.035, 0.071, 0.084, 0.086), (0.080, 0.068, 0.066, 0.090), (0.118, 0.046, 0.038, 0.062)]

    def dent(i, k):
        if i == 2 and k in (4, 6):
            return 0.82
        if i == 1 and k in (3, 7):
            return 0.86
        return 1.0

    rings = []
    for i, (h, w, front, back) in enumerate(rows):
        ring = []
        for k in range(n):
            th = 2 * math.pi * k / n
            depth = -math.cos(th)
            d = (front if depth > 0 else back) * dent(i, k) * s
            ring.append(c + V(w * math.sin(th) * dent(i, k) * s, h * s, depth * d))
        rings.append(ring)
    p.loft(rings, "skull")


def legs(p, stride=0.1, knee_out=0.0, from_knee=False):
    for s in (1, -1):
        fz = stride if s > 0 else -stride * 0.6
        hip, knee, ank = (s * 0.10, 0.95, 0.0), (s * (0.11 + knee_out), 0.51, 0.05 + fz * 0.5), (s * 0.11, 0.09, fz)
        if not from_knee:
            bone(p, [hip, tuple((np.array(hip) + np.array(knee)) / 2), knee], (0.042, 0.032, 0.040))
        bone(p, [knee, tuple((np.array(knee) + np.array(ank)) / 2), ank], (0.038, 0.028, 0.034))
        toe = (ank[0], 0.04, ank[2] + 0.17)
        p.loft(tube([V(*ank), V(*ank) + V(0, -0.03, 0.07), V(*toe)], [(0.04, 0.022, 0.022), (0.045, 0.02, 0.02),
                                                                        (0.03, 0.015, 0.015)], 6, ref_up=V(0, 1, 0)), "bone")


def pelvis(p):
    p.loft(tube([V(-0.14, 0.96, 0), V(0, 0.97, 0), V(0.14, 0.96, 0)],
                [(0.06, 0.05, 0.05), (0.07, 0.06, 0.06), (0.06, 0.05, 0.05)], 6, ref_up=V(0, 1, 0)), "bone_dark")
    bone(p, [(0, 0.99, -0.01), (0, 1.25, 0.0), (0, 1.52, 0.035)], (0.032, 0.026, 0.03), "bone_dark")


def ribcage(p, pairs=6):
    for i in range(pairs):
        y = 1.17 + 0.062 * i
        wmax = [0.14, 0.16, 0.168, 0.162, 0.148, 0.12][i]
        for s in (1, -1):
            bone(p, [(0, y, -0.05), (s * wmax, y + 0.006, 0.015), (s * 0.025, y - 0.03, 0.105)], (0.026,) * 3, sides=4)
    bone(p, [(0, 1.16, 0.105), (0, 1.33, 0.108), (0, 1.52, 0.10)], (0.028, 0.03, 0.026), "bone_dark", sides=4)


def shoulders(p, half=0.22, y=1.51):
    bone(p, [(-half, y, 0.03), (0, y + 0.01, 0.035), (half, y, 0.03)], (0.034, 0.028, 0.034), ref=V(0, 1, 0))
    bone(p, [(0, 1.50, 0.03), (0, 1.57, 0.045), (0, 1.63, 0.06)], (0.034, 0.03, 0.03), "bone_dark")


def fist(p, c, size=0.085):
    """Faeustling: geschlossener Klotz, keine Finger (Regel: keine Haende modellieren)."""
    x, y, z = c
    h = size / 2
    p.box((x - h, y - h, z - h * 1.2), (x + h, y + h * 0.9, z + h * 1.2), "bone_dark")


def arm(p, sh, el, wr, r=0.036):
    mid = lambda a, b: tuple((np.array(a) + np.array(b)) / 2)
    bone(p, [sh, mid(sh, el), el], (r * 1.1, r * 0.85, r))
    bone(p, [el, mid(el, wr), wr], (r * 0.95, r * 0.75, r * 0.8))
    fist(p, wr)


def crossbow(p, x0=0.045, y0=1.40, butt=0.02, span=0.40, tip=0.14, thick=0.04, long=0.78, string=True):
    """Armbrust, geladen und angelegt, zeigt nach +z: Schaft, Bogen mit nach vorn gebogenen Spitzen, Sehne, Bolzen."""
    h = thick / 2
    p.box((x0 - 0.028, y0 - 0.034, butt), (x0 + 0.028, y0 + 0.02, long), "wood")                    # Schaft
    p.box((x0 - 0.04, y0 - 0.075, butt - 0.005), (x0 + 0.04, y0 + 0.034, butt + 0.09), "wood")      # Kolben
    zb = long - 0.10
    for s in (1, -1):
        a = (x0 + s * 0.02, x0 + s * (span - tip * 0.55))
        p.box((min(a), y0 - h * 0.7, zb), (max(a), y0 + h * 0.7, zb + 0.045), "iron")
        xs0, xs1 = x0 + s * (span - tip * 0.7), x0 + s * (span + tip * 0.35)
        p.prism([(xs0, y0 - h * 0.7, zb - 0.002), (xs0 + s * 0.04, y0 - h * 0.7, zb - 0.002),
                 (xs1, y0 - h * 0.7, zb + tip * 1.4), (xs1 - s * 0.04, y0 - h * 0.7, zb + tip * 1.4)],
                (0, h * 1.4, 0), "iron")
        if string:
            tipx, tipz = xs1 - s * 0.02, zb + tip * 1.4
            bone(p, [(tipx, y0, tipz), ((tipx + x0) / 2, y0, (tipz + butt + 0.34) / 2), (x0, y0 + 0.002, butt + 0.34)],
                 (0.011,) * 3, sides=4)
    # Steigbuegel vorn und Bolzen
    p.box((x0 - 0.05, y0 - 0.06, long - 0.03), (x0 + 0.05, y0 + 0.01, long + 0.035), "iron")
    bone(p, [(x0, y0 + 0.03, butt + 0.30), (x0, y0 + 0.03, long * 0.75), (x0, y0 + 0.03, long + 0.12)], (0.014,) * 3,
         "wood", sides=4)
    p.spike((x0, y0 + 0.03, long + 0.12), (x0, y0 + 0.03, long + 0.22), 0.026, "iron")


def loincloth(p, y_top=0.99, y_bot=0.58, half=0.11, z=0.075):
    p.prism([(-half, y_top, z), (half, y_top, z), (half * 0.8, y_bot + 0.03, z + 0.01), (-half * 0.7, y_bot, z + 0.01)],
            (0, 0, 0.02), "cloth")


# --- Fassungen ------------------------------------------------------------------------------------------------------

def build_A():
    """Waechter: schlank, aufrecht, offener Brustkorb mit Luecken, grosse Armbrust vor der Brust."""
    body, gear, cloth = Part("skelett", (0, 0, 0), UV), Part("armbrust", (0, 0, 0), UV), Part("lendentuch", (0, 0, 0), UV)
    gear.flat = True
    pelvis(body)
    ribcage(body, 6)
    shoulders(body)
    legs(body)
    skull(body, V(0, 1.725, 0.075), 1.05)
    arm(body, (0.21, 1.50, 0.03), (0.27, 1.24, 0.06), (0.10, 1.30, 0.26))
    arm(body, (-0.21, 1.50, 0.03), (-0.21, 1.27, 0.24), (0.03, 1.375, 0.46))
    crossbow(gear, x0=0.055, y0=1.40, butt=0.03, span=0.40, tip=0.15, long=0.80)
    loincloth(cloth)
    return [body, gear, cloth]


def build_B():
    """Kapuzenschuetze: breite Kutte als Dreieck bis zu den Schienbeinen, Kapuze mit Spitze nach hinten, Gesicht schaut vor."""
    body, gear, cloth = Part("skelett", (0, 0, 0), UV), Part("armbrust", (0, 0, 0), UV), Part("kutte", (0, 0, 0), UV)
    gear.flat = True
    legs(body, stride=0.08, from_knee=True)
    skull(body, V(0, 1.70, 0.075), 1.0)
    bone(body, [(0, 1.50, 0.02), (0, 1.56, 0.04), (0, 1.62, 0.06)], (0.04, 0.034, 0.034), "bone_dark")
    rng = np.random.default_rng(7)
    rows = [(0.30, 0.0, 0.50, 0.40), (0.60, -0.01, 0.37, 0.28), (0.95, -0.02, 0.28, 0.22), (1.25, 0.0, 0.265, 0.21),
            (1.46, 0.02, 0.25, 0.185), (1.57, 0.03, 0.13, 0.11)]
    n = 10
    rings = []
    for i, (y, cz, rx, rz) in enumerate(rows):
        ring = []
        for k in range(n):
            th = 2 * math.pi * k / n
            f = 1.0 + (0.14 * rng.random() - 0.04 if i == 0 else 0.0)
            yy = y + (0.10 * rng.random() if i == 0 else 0.0)
            ring.append(V(rx * f * math.sin(th), yy, cz + rz * f * math.cos(th)))
        rings.append(ring)
    cloth.loft(rings, "cloth")
    # Kapuze: massiver Kopfputz hinter und ueber dem Schaedel, Spitze nach hinten gezogen; das Gesicht steht vor.
    hood = [(1.52, 0.0, 0.16, 0.14), (1.64, -0.04, 0.18, 0.16), (1.77, -0.06, 0.155, 0.14), (1.87, -0.11, 0.10, 0.09),
            (1.95, -0.19, 0.04, 0.05)]
    hrings = []
    for y, cz, rx, rz in hood:
        hrings.append([V(rx * math.sin(2 * math.pi * k / n), y, cz + rz * math.cos(2 * math.pi * k / n)) for k in range(n)])
    cloth.loft(hrings, "cloth_dark")
    for s, (el, wr) in ((1, ((0.30, 1.22, 0.10), (0.10, 1.30, 0.26))), (-1, ((-0.27, 1.25, 0.24), (0.03, 1.375, 0.46)))):
        sh = (s * 0.22, 1.47, 0.02)
        bone(cloth, [sh, tuple((np.array(sh) + np.array(el)) / 2), el], (0.075, 0.07, 0.07), "cloth_dark")
        bone(body, [el, tuple((np.array(el) + np.array(wr)) / 2), wr], (0.034, 0.028, 0.03))
        fist(body, wr)
    crossbow(gear, x0=0.055, y0=1.40, butt=0.12, span=0.31, tip=0.11, long=0.66)
    return [body, gear, cloth]


def build_C():
    """Schildtraeger: breite Hutkrempe aus Eisen, hoher Schild links, Armbrust tief an der Hueftseite angelegt."""
    body, gear, cloth = Part("skelett", (0, 0, 0), UV), Part("ruestung", (0, 0, 0), UV), Part("lendentuch", (0, 0, 0), UV)
    gear.flat = True
    pelvis(body)
    ribcage(body, 5)
    shoulders(body, half=0.24)
    legs(body, stride=0.14, knee_out=0.03)
    skull(body, V(0, 1.70, 0.075), 1.0)
    arm(body, (0.22, 1.50, 0.03), (0.29, 1.24, 0.02), (0.16, 1.17, 0.26))
    arm(body, (-0.22, 1.50, 0.03), (-0.31, 1.27, 0.12), (-0.25, 1.19, 0.285))
    # Eisenhut mit breiter Krempe
    rings = [[V(0.25 * math.sin(2 * math.pi * k / 10), 1.78, 0.02 + 0.25 * math.cos(2 * math.pi * k / 10)) for k in range(10)],
             [V(0.25 * math.sin(2 * math.pi * k / 10), 1.80, 0.02 + 0.25 * math.cos(2 * math.pi * k / 10)) for k in range(10)],
             [V(0.135 * math.sin(2 * math.pi * k / 10), 1.815, 0.02 + 0.135 * math.cos(2 * math.pi * k / 10)) for k in range(10)],
             [V(0.125 * math.sin(2 * math.pi * k / 10), 1.89, 0.02 + 0.125 * math.cos(2 * math.pi * k / 10)) for k in range(10)],
             [V(0.06 * math.sin(2 * math.pi * k / 10), 1.96, 0.02 + 0.06 * math.cos(2 * math.pi * k / 10)) for k in range(10)]]
    gear.loft(rings, "iron")
    # Schild: Pavese-Form, steht vor der linken Seite, Eisenbuckel in der Mitte
    gear.prism([(-0.50, 1.50, 0.30), (-0.10, 1.50, 0.30), (-0.07, 1.10, 0.30), (-0.30, 0.74, 0.30), (-0.53, 1.10, 0.30)],
               (0, 0, 0.045), "wood")
    gear.spike((-0.30, 1.22, 0.345), (-0.30, 1.22, 0.42), 0.06, "iron", sides=6)
    gear.prism([(-0.50, 1.50, 0.296), (-0.10, 1.50, 0.296), (-0.10, 1.465, 0.296), (-0.50, 1.465, 0.296)], (0, 0, 0.056),
               "iron")
    crossbow(gear, x0=0.19, y0=1.19, butt=0.0, span=0.36, tip=0.13, long=0.84)
    loincloth(cloth, half=0.12)
    return [body, gear, cloth]


BUILD = {"A": build_A, "B": build_B, "C": build_C}
HOME = {}


def put_in_scene(parts, mat, tag):
    coll = bpy.data.collections.new(tag)
    bpy.context.scene.collection.children.link(coll)
    objs = [p.build(coll, mat, smooth=not getattr(p, "flat", False)) for p in parts]
    for o in objs:
        HOME[o.name] = o.matrix_world.copy()
    return objs


def place(objs, pos=(0, 0, 0)):
    for o in objs:
        o.matrix_world = Matrix.Translation(G(pos)) @ HOME[o.name]
    bpy.context.view_layer.update()
    for o in objs:
        write_light(o)


def tri_count(objs):
    return {o.name: len(o.data.polygons) for o in objs}


def extent(objs):
    lo, hi = np.full(3, 1e9), np.full(3, -1e9)
    for o in objs:
        for c in o.bound_box:
            g = to_game(o.matrix_world @ V(*c))
            lo, hi = np.minimum(lo, (g.x, g.y, g.z)), np.maximum(hi, (g.x, g.y, g.z))
    return lo, hi


# --- Blatt-Werkzeuge -------------------------------------------------------------------------------------------------

def text(img, s, x, y, size=30, color=(235, 235, 235), bold=False):
    f = blf.load("C:/Windows/Fonts/" + ("arialbd.ttf" if bold else "arial.ttf"))
    blf.size(f, size)
    w, h = blf.dimensions(f, s)
    W, H = int(w) + 8, int(size * 1.6)
    ib = imbuf.new((W, H))
    blf.color(f, 1, 1, 1, 1)
    blf.position(f, 4, int(size * 0.4), 0)
    with blf.bind_imbuf(f, ib):
        blf.draw_buffer(f, s)
    tmp = os.path.join(tempfile.gettempdir(), "armbrust_txt.png")
    ib.filepath = tmp
    imbuf.write(ib)
    im = bpy.data.images.load(tmp)
    im.colorspace_settings.name = "Non-Color"
    a = np.array(im.pixels[:], np.float32).reshape(H, W, 4)[::-1, :, 3]
    bpy.data.images.remove(im)
    hh, ww = img.shape[:2]
    ye, xe = min(hh, y + H), min(ww, x + W)
    m = (a[:ye - y, :xe - x] > 0.5)[..., None]
    img[y:ye, x:xe] = np.where(m, np.array(color, float), img[y:ye, x:xe])


def render(visible, cam, loc, target, fov, size, mode, want_mask=False):
    setup_render(*size)
    cam.data.angle = math.radians(fov)
    aim(cam, loc, target)
    p = render_passes("ar", visible)
    img = compose(p, mode)[::-1]
    return (img, coverage(p)[::-1]) if want_mask else img


def blit(sheet, tile, x, y):
    sheet[y:y + tile.shape[0], x:x + tile.shape[1]] = tile


def up(arr, f):
    return np.repeat(np.repeat(arr, f, axis=0), f, axis=1)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    for d in ("modelle", "texturen"):
        os.makedirs(os.path.join(HERE, d), exist_ok=True)
    atlas = {"ruhe": make_atlas(False), "angriff": make_atlas(True)}
    colours = {k: len({tuple(c) for c in v.reshape(-1, 3).round().astype(int)}) for k, v in atlas.items()}
    images = {k: save_image("atlas_" + k, v, os.path.join(HERE, "texturen", f"atlas_{k}.png")) for k, v in atlas.items()}

    # 1. glb und Dreiecke
    stats = {}
    for v in VARIANTS:
        mat = export_material(f"as_{v}", images["ruhe"])
        objs = put_in_scene(BUILD[v](), mat, f"exp_{v}")
        export_glb(objs, os.path.join(HERE, "modelle", f"armbrust_skelett_{v}.glb"))
        lo, hi = extent(objs)
        stats[v] = (tri_count(objs), lo, hi)
        for o in objs:
            bpy.data.objects.remove(o, do_unlink=True)
    with open(os.path.join(HERE, "masse.txt"), "w", encoding="utf-8", newline="\n") as f:
        for v in VARIANTS:
            t, lo, hi = stats[v]
            f.write(f"{v} {NAMES[v]}: Dreiecke {t} (Summe {sum(t.values())}), x {lo[0]:.2f}..{hi[0]:.2f}, "
                    f"y {lo[1]:.2f}..{hi[1]:.2f}, z {lo[2]:.2f}..{hi[2]:.2f}\n")
        f.write(f"Farben in der Textur: ruhe {colours['ruhe']}, angriff {colours['angriff']} (Grenze 32)\n")

    # 2. Szenen
    pm = {k: pass_material("pm_" + k, images[k]) for k in images}
    sets = {(v, k): put_in_scene(BUILD[v](), pm[k], f"r_{v}_{k}") for v in VARIANTS for k in images}
    cam = camera()
    stone = stone_textures(tempfile.mkdtemp(prefix="as_stein_"))
    room = build_room(stone)
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(MODELS, "ghoul.glb"))
    ghoul = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    for o in ghoul:
        image = next(n.image for n in o.active_material.node_tree.nodes if n.type == "TEX_IMAGE")
        o.data.materials.clear()
        o.data.materials.append(pass_material("ghoul_" + o.name, image))
        o["smooth"] = True
        HOME[o.name] = o.matrix_world.copy()

    # 2a. Ansichten je Fassung (ohne Nebel): vorn, Seite, schraeg
    dirs = (("vorn", V(0, 0.05, 1)), ("Seite", V(1, 0.05, 0)), ("schräg", V(0.7, 0.25, 0.7)))
    TW, TH = 260, 440
    tiles = {}
    for v in VARIANTS:
        place(sets[(v, "ruhe")], (0, 0, 0))
        lo, hi = stats[v][1], stats[v][2]
        c = V(0.0, 0.98, (lo[2] + hi[2]) / 2)
        sh = np.zeros((40 + TH, 3 * TW, 3)) + FOG_COLOR
        text(sh, f"{v} {NAMES[v]}: vorn, Seite, schräg (ohne Nebel)", 8, 4, 26, bold=True)
        for k, (nm, d) in enumerate(dirs):
            t = render(sets[(v, "ruhe")], cam, G(c + d.normalized() * 6.5), G(c), 20, (TW, TH), "ansicht")
            blit(sh, t, k * TW, 40)
            tiles[(v, nm)] = t
        save_png(sh[::-1], os.path.join(HERE, f"ansichten-{v}.png"))
        place(sets[(v, "ruhe")], (50, 0, 0))     # aus dem Bild raeumen
        place(sets[(v, "angriff")], (50, 0, 0))

    # 2b. Spiel: aus 10 m neben dem Ghul, Silhouetten, Ankuendigung aus 4 m
    xs = {"G": -1.8, "A": -0.6, "B": 0.6, "C": 1.8}
    allobjs = list(ghoul)
    place(ghoul, (xs["G"], 0, 0))
    for v in VARIANTS:
        place(sets[(v, "ruhe")], (xs[v], 0, 0))
        allobjs += sets[(v, "ruhe")]
    img10, mask10 = render(allobjs + room, cam, G(V(0, 1.6, 10)), G(V(0, 1.6, 0)), 70, (854, 480), "spiel", True)
    save_png(img10[::-1], os.path.join(HERE, "spiel-10m.png"))
    mask_only = render(allobjs, cam, G(V(0, 1.6, 10)), G(V(0, 1.6, 0)), 70, (854, 480), "spiel", True)[1]
    for o in allobjs:
        place([o], (60, 0, 0)) if False else None
    place(ghoul, (60, 0, 0))
    for v in VARIANTS:
        place(sets[(v, "ruhe")], (60, 0, 0))
    ann = []
    for v, x in zip(VARIANTS, (-1.1, 0.0, 1.1)):
        place(sets[(v, "angriff")], (x, 0, 0))
        ann += sets[(v, "angriff")]
    img4 = render(ann + room, cam, G(V(0, 1.6, 4)), G(V(0, 1.6, 0)), 70, (854, 480), "spiel")
    save_png(img4[::-1], os.path.join(HERE, "spiel-ankuendigung-4m.png"))
    # Silhouette je Figur aus 10 m (Breite in Pixeln, Hoehe) fuer die Notizen
    meas = {}
    for key, (a, b) in {"G": (200, 320), "A": (320, 420), "B": (430, 530), "C": (540, 650)}.items():
        pass
    cols = np.where(mask_only.any(axis=0))[0]
    rowsall = np.where(mask_only.any(axis=1))[0]
    print("MASKE", cols.min(), cols.max(), rowsall.min(), rowsall.max())

    # 2c. Massstab (ohne Nebel): Ghul, A, B, C mit Hoehenlinien
    for o in ann:
        pass
    place(sets[("A", "angriff")], (60, 0, 0))
    place(sets[("B", "angriff")], (60, 0, 0))
    place(sets[("C", "angriff")], (60, 0, 0))
    mx = {"G": -2.1, "A": -0.7, "B": 0.7, "C": 2.1}
    place(ghoul, (mx["G"], 0, 0))
    strip_objs = list(ghoul)
    for v in VARIANTS:
        place(sets[(v, "ruhe")], (mx[v], 0, 0))
        strip_objs += sets[(v, "ruhe")]
    SW, SH, cy, fov, dist = 1500, 600, 1.0, 36, 7.0
    strip = render(strip_objs, cam, G(V(0, cy, dist)), G(V(0, cy, 0)), fov, (SW, SH), "ansicht")
    hw = 2 * dist * math.tan(math.radians(fov / 2))
    py = lambda y: int(SH / 2 - (y - cy) / hw * SH)
    for yy, lab in ((1.0, "1 m"), (1.8, "1,8 m (Spieler)")):
        row = py(yy)
        m = np.all(np.abs(strip[row] - FOG_COLOR) < 3, axis=1) & ((np.arange(SW) // 14) % 2 == 0)
        strip[row - 1:row + 1, m] = (110, 96, 120)
        text(strip, lab, SW - 260, row - 34, 26, (160, 146, 170))
    px = lambda x: int(SW / 2 + x / (hw * SW / SH) * SW)
    for k, lab in (("G", "Ghul"), ("A", "A"), ("B", "B"), ("C", "C")):
        text(strip, lab, px(mx[k]) - 22, SH - 44, 32, bold=True)

    # 3. Blatt
    COLW, GAP, M = 3 * TW, 40, 30
    W = M * 2 + 3 * COLW + 2 * GAP
    z10 = up(img10[190:310, 327:527], 4)                      # 800 x 480
    zm = np.zeros((480, 800, 3)) + FOG_COLOR
    mm = up(mask_only[190:310, 327:527], 4)
    zm[mm] = (225, 220, 205)
    head = 190
    row1 = 40 + TH
    y_below = head + row1 + 190
    Hh = y_below + 44 + 480 + 70 + 44 + SH + 40
    sheet = np.zeros((Hh, W, 3)) + FOG_COLOR
    text(sheet, "Skelett mit Armbrust, Probestück zu Issue 334: drei Fassungen", M, 14, 46, bold=True)
    notes = {
        "A": ("Schlank und aufrecht, offener Brustkorb", "Armbrust breit vor der Brust, beidhändig"),
        "B": ("Breite Kutte, Kapuze mit Spitze nach hinten", "Kompakte Armbrust, nur Gesicht und Schienbeine frei"),
        "C": ("Breite Eisenkrempe, hoher Schild links", "Armbrust tief angelegt, einhändig"),
    }
    for k, v in enumerate(VARIANTS):
        x0 = M + k * (COLW + GAP)
        text(sheet, v, x0, head - 88, 60, GO[5], True)
        text(sheet, NAMES[v], x0 + 60, head - 80, 44, bold=True)
        for j, (nm, _) in enumerate(dirs):
            blit(sheet, tiles[(v, nm)], x0 + j * TW, head + 40 - 40)
            text(sheet, nm, x0 + j * TW + 6, head + 2, 26)
        t, lo, hi = stats[v]
        y = head + row1 + 6
        text(sheet, f"{sum(t.values())} Dreiecke, {hi[1]:.2f} m hoch, {hi[0] - lo[0]:.2f} m breit", x0, y, 31, bold=True)
        for i, line in enumerate(notes[v]):
            text(sheet, line, x0, y + 44 + i * 40, 29)
    y = y_below
    text(sheet, "Aus 10 m im Spiel: Ghul, A, B, C", M, y, 34, bold=True)
    blit(sheet, z10, M, y + 44)
    text(sheet, "Umrisse derselben Szene", M + 830, y, 34, bold=True)
    blit(sheet, zm, M + 830, y + 44)
    text(sheet, "Ankündigung aus 4 m: A, B, C", M + 1660, y, 34, bold=True)
    blit(sheet, img4[60:420, 127:727], M + 1660, y + 44 + 60)
    y += 44 + 480 + 50
    text(sheet, "Größenvergleich ohne Nebel: Ghul (kriechend, 1 m), A, B, C", M, y, 34, bold=True)
    blit(sheet, strip, M, y + 44)
    y += 44 + SH + 20
    save_png(sheet[::-1], os.path.join(HERE, "blatt.png"))
    print("FARBEN", colours)


main()
