"""Probestueck Gruftwaechter (Issue #335): drei Modellfassungen A/B/C, Probenblatt, Bilder, Pruefungen.

Alles entsteht aus diesem Skript: Geometrie, Textur (nur Farben der Hauptpalette), glb-Modelle, Bilder, Zahlen.
Nichts wird heruntergeladen, das Spiel wird nicht angefasst, unter assets/ wird nur gelesen (Ghul und Gorm fuer den
Groessenvergleich).

Aufruf (aus dem Repository-Wurzelverzeichnis, ohne Argumente):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/gruftwaechter-335/gruftwaechter.py

Ausgabe in diesem Ordner: blatt.png, ansichten-<A|B|C>.png (vorn, Seite, hinten, schraeg oben), spiel-<A|B|C>-4m.png,
spiel-<A|B|C>-ankuendigung-4m.png, vergleich-10m.png, vergleich-10m-dunkel.png, vergleich-vorn.png,
modelle/gruftwaechter_<A|B|C>.glb, texturen/atlas_<ruhend|ankuendigung>.png, masse.txt, pruefung.txt.
Koordinaten: Spiel-Achsen (x seitlich, y oben, z vorn = Vorderseite), Meter, Ursprung zwischen den Fuessen am Boden.
Die Texturfelder tragen y von unten (Zeile 0 = unten), wie die UV-Achse v.
"""

import json
import math
import os
import sys
import tempfile

import bmesh
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
sys.path.insert(0, os.path.join(ROOT, "tools", "models"))
import blf  # noqa: E402
import bpy  # noqa: E402
import imbuf  # noqa: E402
from mathutils import Matrix  # noqa: E402
from psx import (V, G, FOG_COLOR, Part, rects_from_pixels, save_image, export_material, pass_material,  # noqa: E402
                 setup_render, camera, aim, render_passes, compose, save_png, write_light, stone_textures,
                 build_room, export_glb, to_game, MATS)

MODELS = os.path.join(ROOT, "assets", "models")
VARIANTS = ("A", "B", "C")
NAMES = {"A": "Plattenträger", "B": "Glocke", "C": "Joch"}

PAL = {r["name"]: np.array(r["farben"], float)
       for k in ("grund", "akzent")
       for r in json.load(open(os.path.join(ROOT, "assets", "palette", "hauptpalette.json")))[k]}
ST, EI, SF, HK, HO, BR = (PAL["Stein"], PAL["Eisen"], PAL["Stoff"], PAL["Haut/Knochen"], PAL["Holz"], PAL["Blutrot"])
GOLD = PAL["Messing/Gold"][5]

# --- Textur: 128 x 128, 32 x 32 je Feld, nur Palettenfarben ---------------------------------------------------------

FIELDS = {"plate": (0, 0), "dark": (32, 0), "rim": (64, 0), "cloth": (96, 0),
          "bone": (0, 32), "wood": (32, 32), "slab": (64, 32), "hollow": (96, 32),
          "eye": (0, 64), "rope": (32, 64), "robe": (64, 64), "slabback": (96, 64)}
UV = rects_from_pixels({k: (x, y, x + 32, y + 32) for k, (x, y) in FIELDS.items()}, 128)


def field(fn):
    out = np.zeros((32, 32, 3))
    for y in range(32):
        for x in range(32):
            out[y, x] = fn(x, y)
    return out


def f_plate(x, y):
    if y % 8 == 7:
        return EI[4]
    if y % 8 == 0:
        return EI[2]
    if x % 16 == 2 and y % 8 == 4:
        return EI[5]
    return EI[3]


def f_dark(x, y):
    if y % 8 == 7:
        return EI[3]
    if y % 8 == 0:
        return EI[1]
    if (x * 7 + y * 3) % 19 == 0:
        return EI[3]
    return EI[2]


def f_rim(x, y):
    return EI[5] if y % 4 == 3 else EI[3] if y % 4 == 0 else EI[4]


def f_cloth(x, y):
    if y < 3:
        return SF[3]
    if x % 8 in (0, 1):
        return SF[3]
    if x % 8 == 5:
        return SF[5]
    return SF[4]


def f_robe(x, y):
    return SF[2] if x % 8 in (0, 1) else SF[3]


def f_bone(x, y):
    if (x * 5 + y * 3) % 17 == 0:
        return HK[5]
    return HK[3] if (x * 7 + y * 13) % 11 == 0 else HK[4]


def f_wood(x, y):
    if (x * 3 + y * 7) % 23 == 0:
        return HO[4]
    return HO[2] if y % 5 == 0 else HO[3]


def f_rope(x, y):
    return HO[3] if (x + y) % 6 < 3 else HO[2]


def f_slab(x, y):
    edge = min(x, y, 31 - x, 31 - y)
    if edge < 3:
        return EI[4] if (edge == 2 and y > 16) or edge < 2 else EI[3]
    if edge == 3:
        return EI[1]
    on_cross = (14 <= x <= 17 and 5 <= y <= 26) or (8 <= x <= 23 and 15 <= y <= 18)
    grow = (13 <= x <= 18 and 4 <= y <= 27) or (7 <= x <= 24 and 14 <= y <= 19)
    if on_cross:
        return EI[0]
    if grow:
        return EI[1]
    return EI[3] if (x * 11 + y * 5) % 13 == 0 else EI[2]


def f_slabback(x, y):
    return EI[1] if (x // 4 + y // 4) % 2 == 0 else EI[2]


def f_hollow(x, y):
    return EI[1] if (x * 5 + y * 9) % 29 == 0 else EI[0]


def f_eye_rest(x, y):
    return EI[1]


def f_eye_warn(x, y):
    return BR[6] if 10 <= y < 22 else BR[5]


def make_atlas(state):
    a = np.zeros((128, 128, 3)) + EI[0]
    fns = {"plate": f_plate, "dark": f_dark, "rim": f_rim, "cloth": f_cloth, "bone": f_bone, "wood": f_wood,
           "slab": f_slab, "hollow": f_hollow, "rope": f_rope, "robe": f_robe, "slabback": f_slabback,
           "eye": f_eye_warn if state == "ankuendigung" else f_eye_rest}
    for name, fn in fns.items():
        x0, y0 = FIELDS[name]
        a[y0:y0 + 32, x0:x0 + 32] = field(fn)
    return a


# --- Bausteine ------------------------------------------------------------------------------------------------------

def ngon(cx, y, cz, rx, rz, n=12, rot=15):
    r0 = math.radians(rot)
    return [(cx + rx * math.cos(r0 + k * 2 * math.pi / n), y, cz + rz * math.sin(r0 + k * 2 * math.pi / n))
            for k in range(n)]


def drum(p, cx, y, cz, rx, rz, h, rect, n=12, rot=15):
    """Stehende Trommel (n-Eck, h hoch). Kappen: rect kann eine Funktion der Normalen sein."""
    p.prism(ngon(cx, y, cz, rx, rz, n, rot), (0, h, 0), rect)


def bar(p, a, b, w0, w1, d0, d1, rect, ref=(0, 0, 1)):
    """Vierkantstab von a nach b; w = halbe Breite entlang 'Seite' (Achse x ref), d = halbe Tiefe (entlang 'Oben')."""
    a, b = V(*a), V(*b)
    axis = (b - a).normalized()
    side = axis.cross(V(*ref))
    if side.length < 1e-3:
        side = axis.cross(V(1, 0, 0))
    side.normalize()
    up = side.cross(axis).normalized()
    ra = [a + side * w0 + up * d0, a - side * w0 + up * d0, a - side * w0 - up * d0, a + side * w0 - up * d0]
    rb = [b + side * w1 + up * d1, b - side * w1 + up * d1, b - side * w1 - up * d1, b + side * w1 - up * d1]
    polys = [[ra[i], ra[(i + 1) % 4], rb[(i + 1) % 4], rb[i]] for i in range(4)] + [ra, rb]
    p.convex(polys, rect)


def frustum(p, y0, hw0, hd0, y1, hw1, hd1, rect, cx=0.0, cz0=0.0, cz1=0.0):
    """Abgestumpfte Kiste: unten y0 (Halbbreite hw0, Halbtiefe hd0, Mitte z cz0), oben y1 (hw1, hd1, cz1)."""
    lo = [V(cx + sx * hw0, y0, cz0 + sz * hd0) for sx, sz in ((1, 1), (-1, 1), (-1, -1), (1, -1))]
    hi = [V(cx + sx * hw1, y1, cz1 + sz * hd1) for sx, sz in ((1, 1), (-1, 1), (-1, -1), (1, -1))]
    polys = [[lo[i], lo[(i + 1) % 4], hi[(i + 1) % 4], hi[i]] for i in range(4)] + [lo, hi]
    p.convex(polys, rect)


def ring(y, rx, rz, cx=0.0, cz=0.0, n=14, rot=0.0):
    r0 = math.radians(rot)
    return [V(cx + rx * math.cos(r0 + 2 * math.pi * k / n), y, cz + rz * math.sin(r0 + 2 * math.pi * k / n))
            for k in range(n)]


def dome(p, c, rx, ry, rz, rect, n=14, angles=(0, 32, 62, 82)):
    """Flache Kuppel (halbes Ei) ueber dem Mittelpunkt c; Boden flach, Spitze oben."""
    rings = []
    for deg in angles:
        t = math.radians(deg)
        rings.append(ring(c[1] + ry * math.sin(t), rx * math.cos(t), rz * math.cos(t), c[0], c[2], n, 18))
    p.loft(rings, rect)


def band(p, y, rx, rz, h, rect, n=14, cx=0.0, cz=0.0):
    """Reif um einen Koerper: geschlossene Scheibe, 3 Ringe, h hoch."""
    p.loft([ring(y, rx, rz, cx, cz, n, 18), ring(y + h / 2, rx, rz, cx, cz, n, 18), ring(y + h, rx, rz, cx, cz, n, 18)],
           rect)


def eyes_part(pivot, z, y, dx=0.055, w=0.04, h=0.032):
    e = Part("eyes", pivot, UV)
    for s in (-1, 1):
        e.box((s * dx - w / 2, y, z), (s * dx + w / 2, y + h, z + 0.008), "eye")
    return e


def front_back(front, back, other="plate"):
    """Flaechenfunktion fuer Platten: vorn andere Flaeche als hinten und Rand."""
    return lambda n: front if n.z > 0.8 else back if n.z < -0.8 else other


# --- Fassung A: Plattenträger --------------------------------------------------------------------------------------

def build_A():
    parts = []
    for s in (-1, 1):
        leg = Part("leg_l" if s < 0 else "leg_r", (s * 0.24, 1.1, 0.0), UV)
        leg.box((s * 0.24 - 0.15, 0.0, -0.16), (s * 0.24 + 0.15, 0.2, 0.46), "plate")                     # Stiefel
        leg.box((s * 0.24 - 0.12, 0.2, 0.12), (s * 0.24 + 0.12, 0.32, 0.4), "dark")                       # Spann
        bar(leg, (s * 0.24, 0.18, -0.02), (s * 0.24, 0.66, 0.0), 0.14, 0.13, 0.14, 0.13, "plate")        # Beinschiene
        drum(leg, s * 0.24, 0.62, 0.1, 0.14, 0.12, 0.1, "rim")                                           # Kniescheibe
        bar(leg, (s * 0.25, 0.7, 0.0), (s * 0.25, 1.16, 0.0), 0.17, 0.2, 0.17, 0.2, "dark")              # Oberschenkel
        leg.box((s * 0.25 - 0.1, 0.34, -0.2), (s * 0.25 + 0.1, 0.62, -0.12), "plate")                    # Wadenschutz hinten
        parts.append(leg)

    torso = Part("torso", (0, 1.15, 0), UV)
    frustum(torso, 1.0, 0.44, 0.28, 1.28, 0.38, 0.25, "cloth")                                            # Hüftrock unten
    frustum(torso, 0.82, 0.5, 0.31, 1.06, 0.44, 0.28, "cloth")
    frustum(torso, 1.2, 0.36, 0.25, 1.9, 0.56, 0.32, "dark", cz1=0.02)                                   # Rumpf
    frustum(torso, 1.4, 0.41, 0.28, 1.86, 0.53, 0.325, "plate", cz0=0.015, cz1=0.03)                     # Brustpanzer
    torso.box((-0.4, 1.2, -0.28), (0.4, 1.3, 0.28), "rim")                                                # Gürtel
    torso.box((-0.07, 1.2, 0.26), (0.07, 1.31, 0.3), "rim")                                               # Schnalle
    bar(torso, (0, 1.82, -0.31), (0, 0.78, -0.36), 0.42, 0.36, 0.05, 0.04, "robe")                        # Umhang hinten
    drum(torso, 0, 1.88, 0.0, 0.22, 0.2, 0.12, "rim")                                                     # Halsberge
    for k, (dx, y, r) in enumerate(((0.64, 1.86, 0.28), (0.68, 1.78, 0.32), (0.72, 1.7, 0.35))):          # rechts: drei Schienen
        drum(torso, dx, y, 0.0, r, r * 0.95, 0.09, "plate" if k != 1 else "rim")
    drum(torso, -0.6, 1.87, 0.0, 0.24, 0.22, 0.09, "plate")                                               # links kleiner
    drum(torso, -0.62, 1.8, 0.0, 0.27, 0.25, 0.08, "rim")
    parts.append(torso)

    head = Part("head", (0, 1.95, 0), UV)
    drum(head, 0, 1.92, 0.0, 0.18, 0.18, 0.34, "plate")                                                   # Eimerhelm
    drum(head, 0, 2.26, 0.0, 0.205, 0.205, 0.05, "rim")
    head.box((-0.02, 2.3, -0.15), (0.04, 2.4, 0.1), "rim")                                                # Kamm, leicht aus der Mitte
    head.box((0.18, 2.14, -0.04), (0.25, 2.27, 0.05), "dark")                                             # abgebrochenes Horn
    head.box((-0.12, 2.06, 0.14), (0.12, 2.14, 0.175), "hollow")                                          # Sehschlitz
    for x in (-0.07, 0.0, 0.07):
        head.box((x - 0.015, 1.96, 0.15), (x + 0.015, 2.03, 0.172), "hollow")                             # Atemlöcher
    parts.append(head)
    parts.append(eyes_part((0, 1.95, 0), 0.172, 2.075))

    arm_r = Part("arm_r", (0.6, 1.78, 0.0), UV)
    bar(arm_r, (0.62, 1.76, 0.0), (0.7, 1.4, 0.12), 0.11, 0.1, 0.11, 0.1, "plate")
    bar(arm_r, (0.7, 1.4, 0.12), (0.66, 1.08, 0.28), 0.1, 0.12, 0.1, 0.12, "dark")
    arm_r.box((0.53, 0.94, 0.15), (0.79, 1.16, 0.41), "plate")                                            # Panzerfaust
    arm_r.box((0.5, 1.12, 0.12), (0.82, 1.2, 0.44), "rim")                                                # Stulpe
    parts.append(arm_r)

    maul = Part("maul", (0.66, 1.05, 0.28), UV)
    bar(maul, (0.66, 0.3, 0.28), (0.66, 1.3, 0.28), 0.045, 0.045, 0.045, 0.045, "wood")
    maul.box((0.6, 1.28, 0.22), (0.72, 1.36, 0.34), "rim")
    maul.box((0.4, 0.02, 0.12), (0.92, 0.46, 0.44), "dark")                                               # schwerer Kopf am Boden
    maul.box((0.38, 0.2, 0.1), (0.94, 0.27, 0.46), "rim")
    maul.box((0.55, 0.44, 0.2), (0.77, 0.52, 0.36), "plate")
    parts.append(maul)

    arm_l = Part("arm_l", (-0.58, 1.78, 0.0), UV)
    bar(arm_l, (-0.6, 1.76, 0.0), (-0.66, 1.44, 0.14), 0.11, 0.1, 0.11, 0.1, "plate")
    bar(arm_l, (-0.66, 1.44, 0.14), (-0.52, 1.04, 0.34), 0.1, 0.12, 0.1, 0.12, "dark")
    arm_l.box((-0.64, 0.92, 0.24), (-0.4, 1.14, 0.46), "plate")
    parts.append(arm_l)

    slab = Part("slab", (-0.52, 1.04, 0.34), UV)
    pts = [(-1.06, 0.2), (0.04, 0.2), (0.04, 1.46), (-0.26, 1.74), (-0.76, 1.74), (-1.06, 1.46)]
    slab.prism([(x, y, 0.46) for x, y in pts], (0, 0, 0.18), front_back("slab", "slabback", "rim"))
    slab.box((-0.7, 0.98, 0.36), (-0.3, 1.06, 0.47), "wood")                                              # Griff hinten
    for k in range(5):                                                                                    # Nieten am Rand der Platte
        slab.box((-1.0 + k * 0.2, 0.22, 0.44), (-0.92 + k * 0.2, 0.3, 0.485), "rim")
    for k in range(3):                                                                                    # Zacken auf dem Maulkopf
        frustum(maul, 0.46, 0.05, 0.05, 0.62, 0.0, 0.0, "plate", cx=0.5 + k * 0.17, cz0=0.28, cz1=0.28) if False else None
        frustum(maul, 0.46, 0.05, 0.05, 0.6, 0.012, 0.012, "plate", cx=0.5 + k * 0.17, cz0=0.28, cz1=0.28)
    for s in (-1, 1):                                                                                     # Hüftlaschen
        for k in range(2):
            torso.box((s * (0.2 + k * 0.17) - 0.07, 0.8, 0.27), (s * (0.2 + k * 0.17) + 0.07, 1.04, 0.31), "plate")
    parts.append(slab)
    return parts


# --- Fassung B: Glocke ----------------------------------------------------------------------------------------------

def build_B():
    parts = []
    body = Part("body", (0, 0.7, 0), UV)
    skirt = [ring(0.005, 0.82, 0.64), ring(0.04, 0.82, 0.64), ring(0.5, 0.68, 0.53), ring(0.98, 0.53, 0.43),
             ring(1.36, 0.45, 0.37)]
    body.loft(skirt, "cloth")
    band(body, 0.04, 0.86, 0.68, 0.14, "rim")                                                             # Saumreif
    band(body, 0.62, 0.69, 0.56, 0.08, "rim")
    band(body, 1.14, 0.52, 0.42, 0.08, "rim")
    for s in (-1, 1):
        body.box((s * 0.24 - 0.14, 0.0, 0.62), (s * 0.24 + 0.14, 0.17, 0.98), "plate")                    # Stiefelspitzen vorn
    frustum(body, 1.3, 0.44, 0.31, 1.97, 0.62, 0.35, "dark", cz1=0.03)                                   # Brustkorb, sehr breite Schultern
    frustum(body, 1.46, 0.5, 0.33, 1.9, 0.58, 0.36, "plate", cz0=0.02, cz1=0.04)                         # Brustplatte
    bar(body, (0, 1.4, 0.35), (0, 0.18, 0.67), 0.2, 0.26, 0.03, 0.03, "robe")                            # Latz vorn
    drum(body, 0, 1.94, 0.0, 0.25, 0.22, 0.1, "rim")                                                      # Halsberge
    dome(body, (0.76, 1.9, 0.0), 0.4, 0.3, 0.36, "plate")                                                 # rechte Schulterkuppel, groß
    dome(body, (-0.72, 1.9, 0.0), 0.33, 0.26, 0.3, "plate")                                               # linke kleiner
    band(body, 1.86, 0.37, 0.33, 0.06, "rim", cx=-0.72)
    band(body, 1.86, 0.43, 0.39, 0.06, "rim", cx=0.76)
    parts.append(body)

    head = Part("head", (0, 2.0, 0), UV)
    head.loft([ring(1.98, 0.18, 0.18), ring(2.2, 0.18, 0.18), ring(2.4, 0.12, 0.12), ring(2.6, 0.05, 0.05)], "plate")
    head.loft([ring(2.2, 0.2, 0.2), ring(2.24, 0.2, 0.2), ring(2.28, 0.2, 0.2)], "rim")                  # Helmreif
    head.box((-0.02, 2.03, 0.15), (0.02, 2.2, 0.185), "hollow")                                           # T-Schlitz senkrecht
    head.box((-0.11, 2.13, 0.145), (0.11, 2.18, 0.185), "hollow")                                         # T-Schlitz waagerecht
    head.box((0.13, 2.0, 0.08), (0.2, 2.1, 0.16), "dark")                                                 # Beule, schief
    parts.append(head)
    parts.append(eyes_part((0, 2.0, 0), 0.185, 2.138, dx=0.062, w=0.036, h=0.03))

    for s in (-1, 1):
        arm = Part("arm_l" if s < 0 else "arm_r", (s * 0.66, 1.85, 0.0), UV)
        bar(arm, (s * 0.68, 1.82, 0.0), (s * 0.6, 1.5, 0.4), 0.12, 0.11, 0.12, 0.11, "plate")
        bar(arm, (s * 0.6, 1.5, 0.4), (s * 0.1, 1.46, 0.78), 0.1, 0.13, 0.1, 0.13, "dark")
        parts.append(arm)

    sword = Part("sword", (0, 1.5, 0.78), UV)
    blade = [(-0.19, 1.34), (0.19, 1.34), (0.19, 0.4), (0.0, 0.0), (-0.19, 0.4)]
    sword.prism([(x, y, 0.745) for x, y in blade], (0, 0, 0.07), "plate")
    sword.box((-0.025, 0.44, 0.805), (0.025, 1.3, 0.82), "dark")                                          # Hohlkehle
    sword.box((-0.45, 1.32, 0.72), (0.45, 1.42, 0.84), "rim")                                             # Parierstange
    bar(sword, (0, 1.4, 0.78), (0, 1.74, 0.78), 0.05, 0.05, 0.05, 0.05, "wood")
    drum(sword, 0, 1.72, 0.78, 0.085, 0.085, 0.07, "rim")
    sword.box((-0.1, 1.42, 0.69), (0.1, 1.54, 0.87), "plate")                                             # untere Faust
    sword.box((-0.09, 1.56, 0.7), (0.09, 1.68, 0.86), "dark")                                             # obere Faust
    parts.append(sword)
    return parts


# --- Fassung C: Joch ------------------------------------------------------------------------------------------------

def build_C():
    parts = []
    for s in (-1, 1):
        leg = Part("leg_l" if s < 0 else "leg_r", (s * 0.3, 1.0, 0.0), UV)
        leg.box((s * 0.3 - 0.17, 0.0, -0.14), (s * 0.3 + 0.17, 0.2, 0.5), "plate")
        bar(leg, (s * 0.3, 0.18, 0.0), (s * 0.3, 0.7, 0.0), 0.15, 0.14, 0.15, 0.14, "dark")
        drum(leg, s * 0.3, 0.66, 0.1, 0.16, 0.14, 0.11, "rim")
        bar(leg, (s * 0.3, 0.72, 0.0), (s * 0.3, 1.18, 0.0), 0.19, 0.22, 0.19, 0.22, "plate")
        parts.append(leg)

    torso = Part("torso", (0, 1.2, 0), UV)
    frustum(torso, 0.84, 0.52, 0.34, 1.28, 0.44, 0.3, "robe")                                             # Rock
    frustum(torso, 1.0, 0.54, 0.35, 1.1, 0.5, 0.33, "rim")                                                # Rocksaum
    frustum(torso, 1.2, 0.4, 0.28, 1.84, 0.6, 0.36, "plate", cz1=0.06)                                   # Rumpf, nach vorn geneigt
    frustum(torso, 1.5, 0.5, 0.31, 1.78, 0.57, 0.34, "dark", cz0=0.03, cz1=0.07)
    torso.box((-0.46, 1.22, -0.3), (0.46, 1.32, 0.32), "rim")
    bar(torso, (0, 1.78, -0.32), (0, 1.0, -0.4), 0.36, 0.44, 0.04, 0.04, "robe")
    drum(torso, 0, 1.82, 0.04, 0.24, 0.22, 0.1, "rim")
    drum(torso, 0.6, 1.76, 0.04, 0.2, 0.2, 0.08, "plate")                                                 # kleine Schulterkappen
    drum(torso, -0.6, 1.78, 0.04, 0.22, 0.22, 0.08, "plate")
    for s in (-1, 1):                                                                                     # Beinplatten, Nieten
        for k in range(3):
            frustum(torso, 0.9 + k * 0.1, 0.05, 0.03, 0.96 + k * 0.1, 0.03, 0.02, "rim", cx=s * (0.2 + k * 0.1), cz0=0.34, cz1=0.35)
    for k in range(4):                                                                                    # Zacken am Rocksaum
        frustum(torso, 0.84, 0.07, 0.05, 0.96, 0.01, 0.01, "rim", cx=-0.3 + k * 0.2, cz0=0.36, cz1=0.38)
    for s in (-1, 1):
        for k in range(2):
            torso.box((s * (0.12 + k * 0.2) - 0.08, 1.4, 0.3), (s * (0.12 + k * 0.2) + 0.08, 1.7, 0.34), "rim")
    parts.append(torso)

    head = Part("head", (0, 1.9, 0.04), UV)
    drum(head, 0, 1.88, 0.05, 0.2, 0.2, 0.4, "plate")                                                     # Eimerhelm, hoch und breit
    drum(head, 0, 2.26, 0.05, 0.225, 0.225, 0.045, "rim")
    head.box((-0.025, 1.92, 0.22), (0.025, 2.26, 0.26), "rim")                                            # senkrechte Kante vorn
    head.box((-0.14, 2.04, 0.2), (0.14, 2.12, 0.235), "hollow")                                           # Sehschlitz
    head.box((-0.2, 1.98, -0.1), (-0.14, 2.2, 0.0), "dark")                                               # Ohrplatte links
    parts.append(head)
    parts.append(eyes_part((0, 1.9, 0.04), 0.235, 2.07, dx=0.075, w=0.045, h=0.032))

    yoke = Part("yoke", (0, 1.87, -0.12), UV)
    yoke.box((-1.04, 1.76, -0.22), (1.04, 1.96, -0.03), "wood")                                           # Tragbalken
    for s in (-1, 1):
        yoke.box((s * 1.0 - 0.08 + (0.0), 1.7, -0.27), (s * 1.0 + 0.08 + 0.1 * s, 2.02, 0.02), "plate")  # Eisenkappe am Ende
        for x in (0.56, 0.82):
            yoke.box((s * x - 0.03, 1.74, -0.24), (s * x + 0.03, 1.98, -0.01), "rim")                     # Reifen
    for s, r, drop in ((-1, 0.25, 0.86), (1, 0.19, 1.0)):                                                 # Gewichte, links größer
        x = s * 1.08
        bar(yoke, (x, 1.75, -0.125), (x, 1.34, -0.125), 0.03, 0.03, 0.03, 0.03, "rope")
        drum(yoke, x, drop, -0.125, r, r, 1.36 - drop, "dark")
        drum(yoke, x, 1.34, -0.125, r * 0.6, r * 0.6, 0.07, "rim")
        drum(yoke, x, drop - 0.06, -0.125, r * 0.8, r * 0.8, 0.07, "plate")
    parts.append(yoke)

    for s in (-1, 1):
        arm = Part("arm_l" if s < 0 else "arm_r", (s * 0.56, 1.72, 0.02), UV)
        bar(arm, (s * 0.56, 1.7, 0.04), (s * 0.88, 1.62, 0.12), 0.11, 0.1, 0.11, 0.1, "plate", ref=(0, 1, 0))
        bar(arm, (s * 0.88, 1.62, 0.12), (s * 0.95, 1.84, -0.1), 0.1, 0.11, 0.1, 0.11, "dark", ref=(0, 1, 0))
        arm.box((s * 0.95 - 0.11, 1.78, -0.25), (s * 0.95 + 0.11, 1.98, -0.02), "plate")                 # Panzerfaust am Balken
        parts.append(arm)
    return parts


BUILD = {"A": build_A, "B": build_B, "C": build_C}


# --- Szene ----------------------------------------------------------------------------------------------------------

HOME = {}


def put_in_scene(parts, mat, tag, smooth=False):
    coll = bpy.data.collections.new(tag)
    bpy.context.scene.collection.children.link(coll)
    objs = [p.build(coll, mat, smooth=smooth) for p in parts]
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


def closed_report(objs):
    """Je Teil: offene Kanten (genau eine Flaeche), Kanten mit mehr als zwei Flaechen, Rauminhalt (muss > 0 sein)."""
    out = {}
    for o in objs:
        bm = bmesh.new()
        bm.from_mesh(o.data)
        open_e = sum(1 for e in bm.edges if len(e.link_faces) == 1)
        multi = sum(1 for e in bm.edges if len(e.link_faces) > 2)
        vol = sum(f.calc_area() * 0 for f in bm.faces)
        vol = 0.0
        for f in bm.faces:
            a, b, c = (v.co for v in f.verts)
            vol += a.dot(b.cross(c)) / 6.0
        out[o.name] = (len(o.data.polygons), open_e, multi, vol)
        bm.free()
    return out


def import_model(name):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(MODELS, name))
    objs = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    cache = {}
    for o in objs:
        image = next(n.image for n in o.active_material.node_tree.nodes if n.type == "TEX_IMAGE")
        if image.name not in cache:
            cache[image.name] = pass_material("cmp_" + name + "_" + image.name, image)
        o.data.materials.clear()
        o.data.materials.append(cache[image.name])
        o["smooth"] = True
        HOME[o.name] = o.matrix_world.copy()
    return objs


# --- Blatt-Werkzeuge ------------------------------------------------------------------------------------------------

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
    tmp = os.path.join(tempfile.gettempdir(), "gruftwaechter_txt.png")
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


def render_p(visible, cam, loc, target, fov, size):
    setup_render(*size)
    cam.data.angle = math.radians(fov)
    aim(cam, loc, target)
    return render_passes("gw", visible)


def render(visible, cam, loc, target, fov, size, mode):
    return compose(render_p(visible, cam, loc, target, fov, size), mode)[::-1]


def blit(sheet, tile, x, y):
    sheet[y:y + tile.shape[0], x:x + tile.shape[1]] = tile


def up(arr, k):
    return np.repeat(np.repeat(arr, k, axis=0), k, axis=1)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    os.makedirs(os.path.join(HERE, "modelle"), exist_ok=True)
    os.makedirs(os.path.join(HERE, "texturen"), exist_ok=True)
    atlas = {s: make_atlas(s) for s in ("ruhend", "ankuendigung")}
    uniq = {tuple(c) for a in atlas.values() for c in a.reshape(-1, 3)}
    pal_all = {tuple(c) for k in PAL for c in PAL[k]}
    assert all(c in pal_all for c in uniq), "Farbe ausserhalb der Palette"
    assert len({tuple(c) for c in atlas["ruhend"].reshape(-1, 3)}) <= 32
    assert len({tuple(c) for c in atlas["ankuendigung"].reshape(-1, 3)}) <= 32
    images = {s: save_image("atlas_" + s, atlas[s], os.path.join(HERE, "texturen", f"atlas_{s}.png")) for s in atlas}

    # 1. Modelle (glb), Zahlen, Pruefung "geschlossen"
    stats, lines = {}, []
    for v in VARIANTS:
        mat = export_material(f"gw_{v}", images["ruhend"])
        objs = put_in_scene(BUILD[v](), mat, f"exp_{v}")
        export_glb(objs, os.path.join(HERE, "modelle", f"gruftwaechter_{v}.glb"))
        lo, hi = extent(objs)
        stats[v] = (tri_count(objs), lo, hi)
        rep = closed_report(objs)
        lines.append(f"{v} {NAMES[v]}: Teile {len(objs)}, Dreiecke {sum(r[0] for r in rep.values())}, offene Kanten "
                     f"{sum(r[1] for r in rep.values())}, Kanten mit mehr als zwei Flaechen {sum(r[2] for r in rep.values())}, "
                     f"kleinster Rauminhalt {min(r[3] for r in rep.values()):.6f} m3 (muss > 0 sein)")
        for name, r in rep.items():
            lines.append(f"    {name}: {r[0]} Dreiecke, offen {r[1]}, mehrfach {r[2]}, Raum {r[3]:.4f}")
        for o in objs:
            bpy.data.objects.remove(o, do_unlink=True)
    with open(os.path.join(HERE, "pruefung.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write("Pruefung je Teil im Skript (Kante mit genau einer Flaeche = offen):\n" + "\n".join(lines) + "\n")
    with open(os.path.join(HERE, "masse.txt"), "w", encoding="utf-8", newline="\n") as f:
        for v in VARIANTS:
            t, lo, hi = stats[v]
            f.write(f"{v} {NAMES[v]}: Dreiecke {sum(t.values())} {dict(t)}, x {lo[0]:.2f}..{hi[0]:.2f}, "
                    f"y {lo[1]:.2f}..{hi[1]:.2f}, z {lo[2]:.2f}..{hi[2]:.2f}\n")

    # 2. Szenen zum Rendern
    pm = {s: pass_material("pm_" + s, images[s]) for s in atlas}
    sets = {(v, s): put_in_scene(BUILD[v](), pm[s], f"r_{v}_{s}") for v in VARIANTS for s in atlas}
    for objs in sets.values():
        place(objs)
    ghoul, gorm = import_model("ghoul.glb"), import_model("gorm.glb")
    place(ghoul)
    place(gorm)
    gh_lo, gh_hi = extent(ghoul)
    go_lo, go_hi = extent(gorm)
    cam = camera()
    stone = stone_textures(tempfile.mkdtemp(prefix="gw_stein_"))
    room = build_room(stone)
    hall = build_room(stone, half=14.0, height=3.5, back=-14.0, front=24.0)

    # 2a. Ansichten je Fassung (ohne Nebel): vorn, Seite, hinten, schraeg oben
    dirs = (("vorn", V(0, 0.08, 1)), ("Seite", V(1, 0.08, 0)), ("hinten", V(0, 0.08, -1)),
            ("schräg oben", V(0.6, 0.9, 0.7)))
    tiles = {}
    for v in VARIANTS:
        sh = np.zeros((40 + 440, 4 * 440, 3)) + FOG_COLOR
        text(sh, f"{v}  {NAMES[v]}: vorn, Seite, hinten, schräg oben (ohne Nebel)", 8, 4, 26, bold=True)
        for k, (nm, d) in enumerate(dirs):
            t = render(sets[(v, "ruhend")], cam, G(V(0, 1.2, 0) + d.normalized() * 7.2), G(V(0, 1.2, 0)), 30, (440, 440), "ansicht")
            blit(sh, t, k * 440, 40)
            text(sh, nm, k * 440 + 6, 44, 26)
            tiles[(v, nm)] = t[:, 20:420]
        save_png(sh[::-1], os.path.join(HERE, f"ansichten-{v}.png"))

    # 2b. Spielbilder 854 x 480, 70 Grad, Augen 1,6 m: Ruhe und Ankuendigung aus 4 m
    game = {}
    for v in VARIANTS:
        for s, tag in (("ruhend", ""), ("ankuendigung", "-ankuendigung")):
            img = render(list(sets[(v, s)]) + room, cam, G(V(0, 1.6, 4)), G(V(0, 1.6, 0)), 70, (854, 480), "spiel")
            game[(v, s)] = img
            save_png(img[::-1], os.path.join(HERE, f"spiel-{v}{tag}-4m.png"))

    # 2c. Groessenvergleich: Ghul, A, B, C, Gorm in einer Reihe
    order = (("Ghul", ghoul, -6.6), ("A", sets[("A", "ruhend")], -3.3), ("B", sets[("B", "ruhend")], 0.0),
             ("C", sets[("C", "ruhend")], 3.3), ("Gorm", gorm, 6.6))
    for name, objs, x in order:
        if name in ("Ghul", "Gorm"):
            for o in objs:
                o.matrix_world = Matrix.Translation(G(V(x, 0, 0))) @ HOME[o.name]
        else:
            place(objs, (x, 0, 0))
    bpy.context.view_layer.update()
    for _, objs, _ in order:
        for o in objs:
            write_light(o)
    line_objs = [o for _, objs, _ in order for o in objs]
    p10 = render_p(line_objs + hall, cam, G(V(0, 1.6, 10)), G(V(0, 1.6, 0)), 70, (854, 480))
    strips = {}
    for tag, scale in (("", 1.0), ("-dunkel", 0.5)):
        q = dict(p10)
        q["light"] = p10["light"] * scale
        img = compose(q, "spiel")[::-1]
        save_png(img[::-1], os.path.join(HERE, f"vergleich-10m{tag}.png"))
        strips[tag] = img[170:310, 160:700]
    SWd, SHd, cx, fov, dist, cy = 1260, 420, 0.0, 22, 15.0, 1.75
    strip = render(line_objs, cam, G(V(cx, cy, dist)), G(V(cx, cy, 0)), fov, (SWd, SHd), "ansicht")
    save_png(strip[::-1], os.path.join(HERE, "vergleich-vorn.png"))
    hw = 2 * dist * math.tan(math.radians(fov / 2))
    ww = hw * SWd / SHd
    px = lambda x: int((x - cx) / ww * SWd + SWd / 2)
    py = lambda y: int(SHd / 2 - (y - cy) / hw * SHd)
    for yy, lab in ((1.0, "1 m"), (2.0, "2 m"), (3.0, "3 m")):
        row = py(yy)
        mask = np.all(np.abs(strip[row] - FOG_COLOR) < 3, axis=1) & ((np.arange(SWd) // 14) % 2 == 0)
        strip[row - 1:row + 1, mask] = (110, 96, 120)
        text(strip, lab, 6, row - 34, 24, (160, 146, 170))
    for name, _, x in order:
        text(strip, name, px(x) - 14 * len(name), py(0) + 6, 34, bold=True)

    # 3. Blatt
    CW, GAP, M = 400, 30, 30
    W = M * 2 + 3 * CW + 2 * GAP
    Hh = 4300
    sheet = np.zeros((Hh, W, 3)) + FOG_COLOR
    text(sheet, "Gruftwächter (Issue 335): Fassungen A, B, C", M, 18, 44, bold=True)
    text(sheet, "gleiche Haltung, gleiches Licht; Textur nur aus der Hauptpalette; Rot nur in den Augen der Ankündigung", M, 76, 24, (190, 180, 200))
    y = 120
    rows = ("vorn", "Seite", "schräg oben")
    for k, v in enumerate(VARIANTS):
        x0 = M + k * (CW + GAP)
        text(sheet, v, x0, y - 4, 70, GOLD, True)
        text(sheet, NAMES[v], x0 + 70, y + 10, 42, bold=True)
    y += 90
    for r in rows:
        for k, v in enumerate(VARIANTS):
            blit(sheet, tiles[(v, r)], M + k * (CW + GAP), y + 34)
            text(sheet, r, M + k * (CW + GAP) + 6, y, 26)
        y += 34 + 440 + 6
    facts = {}
    for v in VARIANTS:
        t, lo, hi = stats[v]
        facts[v] = (f"{hi[0] - lo[0]:.1f} × {hi[2] - lo[2]:.1f} × {hi[1]:.2f} m (B×T×H)".replace(" (B×T×H)", ""), f"{sum(t.values())} Dreiecke")
    for k, v in enumerate(VARIANTS):
        x0 = M + k * (CW + GAP)
        text(sheet, facts[v][0], x0, y, 24)
        text(sheet, facts[v][1] + " (Ghul 1024, Gorm 2268)", x0, y + 34, 22)
    y += 90
    text(sheet, "ANKÜNDIGUNG aus 4 m: Augen leuchten rot (nur Augen, harte Stufen, kein Schein)", M, y, 30, bold=True)
    y += 44
    for k, v in enumerate(VARIANTS):
        crop = game[(v, "ankuendigung")][60:320, 227:627]
        blit(sheet, crop, M + k * (CW + GAP), y)
    y += 260 + 20
    text(sheet, "GRÖSSENVERGLEICH von vorn (ohne Nebel), Linien bei 1, 2, 3 m: Ghul, A, B, C, Gorm", M, y, 30, bold=True)
    y += 44
    blit(sheet, strip, (W - SWd) // 2, y)
    y += SHd + 24
    for tag, title in (("", "AUS 10 m im Spiel (Nebel, Körnung): Ghul, A, B, C, Gorm"),
                       ("-dunkel", "AUS 10 m, DUNKEL (Licht halbiert): Ghul, A, B, C, Gorm")):
        text(sheet, title, M, y, 30, bold=True)
        y += 44
        big = up(strips[tag], 2)
        blit(sheet, big, (W - big.shape[1]) // 2, y)
        for name, _, x in order:
            text(sheet, name, (W - big.shape[1]) // 2 + 2 * (int(427 + 34.276 * x) - 160) - 14 * len(name), y + big.shape[0] + 2, 26)
        y += big.shape[0] + 50
    reco = RECO
    for line, col, bold, size in reco:
        text(sheet, line, M, y, size, col, bold)
        y += size + 18
    save_png(sheet[:y + 20][::-1] if False else sheet[::-1][:, :, :][Hh - (y + 20):], os.path.join(HERE, "blatt.png"))


RECO = [
    ("Empfehlung: A, Rückfall B (siehe notizen.md)", GOLD, True, 40),
]

main()
