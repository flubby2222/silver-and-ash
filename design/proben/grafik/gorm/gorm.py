"""Gorm, the Chained - Probestueck 9 (Boss) fuer Silver & Ash.

Ein massiger, gebueckter Troll von 3,2 m, an Ketten gebunden: eiserner Halsring mit abgerissener Kette am Ruecken,
Handschellen an beiden Handgelenken, in der rechten Hand (+x, wie GormView) eine Keule mit Eisenbaendern, in der
linken die rostige Kette mit Haken. Der Kopf ist die Schwachstelle und muss von unten lesbar sein: blasses, kahles
Gesicht, schwerer Unterkiefer mit hellen Hauern nach oben, tiefe Augen, der dunkle Halsring rahmt ihn ein.

Zwei Texturen 128 x 128 (Rumpf und Kopf / Glieder und Waffen), dazu eine Variante der ersten fuer Phase 2
(Blutspuren aus aufgerissenen Wunden). Ein einzelnes Kettenglied ist ein eigenes Teil, damit der Code die Kette
beliebig lang zeichnen kann (Schwung, Wurf).

Aufruf (aus dem Repository-Wurzelverzeichnis):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/gorm/gorm.py
"""

import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "gemeinsam"))
import bmesh  # noqa: E402
import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402
from psx import (V, G, Part, tube, rects_from_pixels, vnoise, blob, mix, paint_atlas, save_image,  # noqa: E402
                 export_material, pass_material, write_light, raise_fwd, setup_render, camera, aim,
                 render_passes, compose, save_png, side_by_side, coverage, to_game)

OUT_DIR = os.path.dirname(os.path.abspath(__file__))
LEVEL_DIR = os.path.join(os.path.dirname(OUT_DIR), "level")
TEX = 128

# --- Farben -------------------------------------------------------------------------------------------------------

SKIN = np.array((100, 112, 92), float)         # graugruen, etwas entsaettigter als der Platzhalter
SKIN_PALE = np.array((150, 156, 128), float)    # Gesicht und Kopf: heller - die Schwachstelle soll man sehen
SHADOW = np.array((38, 36, 34), float)
HOLLOW = np.array((20, 14, 16), float)
TUSK = np.array((214, 204, 170), float)         # Hauer: das Hellste am Kopf
EYE = np.array((168, 150, 96), float)           # truebe, kleine Augen
RAGS = np.array((70, 58, 52), float)            # wie der Platzhalter
IRON = np.array((70, 72, 80), float)
RIVET = np.array((150, 152, 160), float)
RUST = np.array((120, 66, 40), float)
WOOD = np.array((104, 72, 44), float)
SCAR = np.array((120, 112, 92), float)
BLOOD = np.array((128, 22, 26), float)          # nur Phase 2: Blut (Rot ist fuer Blut und Gefahr reserviert)
WARNING = np.array((200, 30, 30), float)        # der gluehende Haken (Ankuendigung), wie GormView

# --- Atlanten -----------------------------------------------------------------------------------------------------

RECTS_A = {   # Rumpf und Kopf
    "torso": (0, 64, 128, 128), "head": (0, 16, 64, 64), "jaw": (64, 40, 128, 64), "rags": (64, 16, 128, 40),
    "collar": (0, 0, 64, 16), "tusk": (64, 0, 96, 16), "ear": (96, 0, 128, 16),
}
RECTS_B = {   # Glieder und Waffen
    "upper_arm": (0, 80, 64, 128), "forearm": (64, 80, 128, 128), "thigh": (0, 32, 64, 80), "shin": (64, 32, 128, 80),
    "hand": (0, 16, 32, 32), "foot": (32, 16, 64, 32), "club": (64, 0, 128, 32), "iron": (0, 0, 32, 16),
    "chain": (32, 0, 64, 16),
}
UV_A, UV_B = rects_from_pixels(RECTS_A, TEX), rects_from_pixels(RECTS_B, TEX)


def skin(U, V_, seed, pale=0.0, base=SKIN):
    h, w = U.shape
    col = np.broadcast_to(base + (SKIN_PALE - base) * pale, (h, w, 3)).copy()
    col *= (0.86 + 0.2 * vnoise(h, w, 8, 8, seed) + 0.1 * vnoise(h, w, 30, 30, seed + 1))[..., None]
    warts = vnoise(h, w, 40, 40, seed + 2) > 0.8
    col = mix(col, col * 0.75, warts.astype(float))                                      # Warzen, Poren
    top = 1 - np.abs(np.cos(np.pi * U))
    col *= (0.86 + 0.2 * top)[..., None]
    return col


def scars(col, U, V_, seed, color=SCAR, count=6):
    rng = np.random.default_rng(seed)
    for _ in range(count):
        u, v, a = rng.random(), rng.random(), rng.uniform(0, math.pi)
        d = np.abs((U - u) * math.sin(a) - (V_ - v) * math.cos(a))
        along = np.abs((U - u) * math.cos(a) + (V_ - v) * math.sin(a))
        col = mix(col, color, ((d < 0.008) & (along < 0.08)).astype(float) * 0.9)
    return col


def blood_streaks(col, U, V_, seed, amount):
    """Phase 2: Blut laeuft aus Wunden unter dem Halsring und an der Brust nach unten (v nimmt nach oben zu)."""
    h, w = U.shape
    streak = vnoise(h, w, 2, 48, seed) > 0.62                       # senkrechte Laeufe
    source = V_ > 0.55 - 0.35 * vnoise(h, w, 2, 48, seed + 1)      # beginnen oben, enden verschieden tief
    front = np.abs(U - 0.5) > 0.12                                   # nicht ueber der Wirbelsaeule
    col = mix(col, BLOOD, (streak & source & front).astype(float) * amount)
    wound = (vnoise(h, w, 6, 10, seed + 2) > 0.78) & (V_ > 0.4)
    return mix(col, BLOOD * 0.6, wound.astype(float) * amount)


def paint_torso(U, V_, phase2=False):
    col = skin(U, V_, 10)
    # Bauch und Brust: Speckfalten quer, ueber der Wirbelsaeule ein Grat aus Knochenhoeckern (u = 0,5 oben).
    folds = ((V_ * 7) % 1.0) < 0.08
    col = mix(col, SHADOW, (folds & (np.abs(U - 0.5) > 0.2)).astype(float) * 0.45)
    spine = (np.abs(U - 0.5) < 0.03) & (((V_ * 10) % 1.0) < 0.5)
    col = mix(col, SKIN_PALE * 0.9, spine.astype(float))
    col = scars(col, U, V_, 11, count=10)
    # Striemen von Ketten quer ueber Brust und Ruecken.
    for v0 in (0.62, 0.7):
        band = np.abs(V_ - v0 - 0.1 * np.sin(U * 2 * np.pi)) < 0.012
        col = mix(col, SHADOW * 1.4, band.astype(float) * 0.8)
    if phase2:
        col = blood_streaks(col, U, V_, 12, 0.95)
    return col


def paint_head(U, V_, phase2=False):
    """Kopf: u rundherum (0 hinten, 0,5 vorn = Gesicht), v vom Kinn (0) zum Scheitel (1)."""
    col = skin(U, V_, 20, pale=0.9)
    col = scars(col, U, V_, 21, color=SKIN_PALE * 1.1, count=5)
    # Schwere Stirnwulst mit Schatten darunter, tiefe kleine Augen.
    col *= (1 - 0.3 * blob(U, V_, 0.5, 0.58, 0.22, 0.06))[..., None]
    for sgn in (-1, 1):
        cu = 0.5 + sgn * 0.075
        col = mix(col, HOLLOW, np.clip(blob(U, V_, cu, 0.56, 0.045, 0.06) * 2.5, 0, 1))
        col = mix(col, EYE, (blob(U, V_, cu, 0.56, 0.014, 0.018) > 0.3).astype(float))
    # Breite, flache Nase mit dunklen Loechern; Mund als schiefer dunkler Spalt ueber dem Unterkiefer.
    for sgn in (-1, 1):
        col = mix(col, HOLLOW, (blob(U, V_, 0.5 + sgn * 0.03, 0.4, 0.016, 0.03) > 0.2).astype(float))
    # Mund nach unten gezogen und schief (kein Grinsen, Regel 4), ein Mundwinkel tiefer.
    mouth_v = 0.22 - 0.55 * np.abs(U - 0.5) - 0.06 * (U - 0.5) + 0.02 * vnoise(*U.shape, 2, 20, 23)
    mouth = np.abs(V_ - mouth_v) < 0.03
    col = mix(col, HOLLOW, (mouth & (np.abs(U - 0.5) < 0.15)).astype(float))
    if phase2:
        col = blood_streaks(col, U, V_, 22, 0.6)
    return col


def paint_jaw(U, V_):
    col = skin(U, V_, 30, pale=0.85)
    inside = np.abs(U - 0.5) < 0.22                                  # Oberseite = Mundraum
    return mix(col, HOLLOW, inside.astype(float))


def paint_rags(U, V_):
    h, w = U.shape
    col = np.broadcast_to(RAGS, (h, w, 3)).copy()
    col *= (0.82 + 0.3 * vnoise(h, w, 4, 12, 40))[..., None]
    col *= (0.85 + 0.15 * np.cos(U * 2 * np.pi * 7))[..., None]
    return mix(col, SHADOW, np.clip((vnoise(h, w, 5, 10, 41) - 0.6) * 2, 0, 0.5))


def paint_iron(U, V_, seed=50, rivets=8):
    h, w = U.shape
    col = np.broadcast_to(IRON, (h, w, 3)).copy()
    col *= (0.75 + 0.45 * vnoise(h, w, 10, 20, seed))[..., None]
    col = mix(col, RUST, np.clip((vnoise(h, w, 6, 12, seed + 1) - 0.55) * 2.5, 0, 0.6))
    riv = ((U * rivets) % 1.0)
    col = mix(col, RIVET, ((np.abs(riv - 0.5) < 0.12) & (np.abs(V_ - 0.5) < 0.18)).astype(float))
    return col


def paint_simple(base, var, seed):
    def painter(U, V_):
        h, w = U.shape
        col = np.broadcast_to(base, (h, w, 3)).copy()
        return col * (1 - var / 2 + var * vnoise(h, w, 8, 8, seed))[..., None]
    return painter


def paint_limb(seed, pale=0.0, knuckles=False):
    def painter(U, V_):
        col = skin(U, V_, seed, pale)
        near = np.exp(-(V_ / 0.12) ** 2) + np.exp(-((1 - V_) / 0.12) ** 2)
        col *= (1 - 0.18 * np.clip(near, 0, 1))[..., None]
        col = scars(col, U, V_, seed + 5, count=4)
        if knuckles:
            col = mix(col, SKIN_PALE, (((U * 4) % 1.0 < 0.35) & (V_ > 0.75)).astype(float) * 0.6)
        return col
    return painter


def paint_club(U, V_):
    h, w = U.shape
    col = np.broadcast_to(WOOD, (h, w, 3)).copy()
    col *= (0.75 + 0.45 * vnoise(h, w, 60, 4, 60))[..., None]                       # Maserung
    col = mix(col, SHADOW, np.clip((vnoise(h, w, 6, 6, 61) - 0.6) * 2, 0, 0.5))      # Flecken, altes Blut braun
    return col


def paint_chain(U, V_):
    col = paint_iron(U, V_, 70, rivets=0)
    return mix(col, RUST * 1.1, np.clip(vnoise(*U.shape, 6, 6, 71) * 1.2 - 0.2, 0, 0.8))


PAINT_A = {"torso": paint_torso, "head": paint_head, "jaw": paint_jaw, "rags": paint_rags,
           "collar": paint_iron, "tusk": paint_simple(TUSK, 0.15, 42), "ear": paint_limb(43, 0.6)}
PAINT_A2 = dict(PAINT_A, torso=lambda U, V_: paint_torso(U, V_, True), head=lambda U, V_: paint_head(U, V_, True))
PAINT_B = {"upper_arm": paint_limb(80), "forearm": paint_limb(81), "thigh": paint_limb(82), "shin": paint_limb(83),
           "hand": paint_limb(84, 0.2, knuckles=True), "foot": paint_limb(85, 0.1), "club": paint_club,
           "iron": lambda U, V_: paint_iron(U, V_, 86, 6), "chain": paint_chain}


# --- Geometrie ----------------------------------------------------------------------------------------------------
# Spiel-Achsen, Ursprung zwischen den Fuessen, Blick nach +z. Keule auf +x (wie GormView), Kette auf -x.

HIP = V(0, 1.15, 0)
NECK = V(0, 2.5, 0.42)
JAW = V(0, 2.7, 0.58)
SHOULDER = V(0.86, 2.3, 0.26)
SHOULDER_DROP = 0.08            # Kettenseite (-x) haengt tiefer: Asymmetrie
ELBOW = V(1.04, 1.46, 0.42)
WRIST = V(1.0, 0.8, 0.52)       # lange Arme: die Faeuste haengen bis zu den Knien
HIP_J = V(0.36, 1.12, 0.0)
KNEE = V(0.44, 0.6, 0.12)
ANKLE = V(0.42, 0.14, -0.02)


def build_torso():
    p = Part("torso", HIP, UV_A)
    n = 16

    def mods(i, k):
        if k == n // 2 and i >= 3:                       # Hoecker der Wirbelsaeule
            return 1.08
        return 1.0

    # Gebueckt: der Rumpf neigt sich nach vorn, Schultern und Nacken weit vor dem Becken, ein Buckel hinten.
    centres = [V(0, 1.05, -0.06), V(0, 1.2, 0.0), V(0, 1.48, 0.1), V(0, 1.76, 0.2), V(0, 2.02, 0.3),
               V(0, 2.24, 0.34), V(0, 2.44, 0.32), V(0, 2.58, 0.46)]
    shapes = [(0.52, 0.3, 0.32), (0.6, 0.38, 0.36), (0.66, 0.5, 0.42), (0.72, 0.5, 0.5), (0.8, 0.44, 0.6),
              (0.86, 0.36, 0.66), (0.62, 0.28, 0.6), (0.3, 0.26, 0.3)]
    p.loft(tube(centres, shapes, n, ref_up=V(0, 0, -1), mods=mods), "torso")
    # Lendenschurz: Huelle mit ausgefranstem Saum.
    top = tube([V(0, 1.3, 0.04), V(0, 1.25, 0.04)], [(0.64, 0.44, 0.4)] * 2, 14, ref_up=V(0, 0, -1))[0]
    rng = np.random.default_rng(5)
    hem = [q + V(0, -0.45 - 0.2 * rng.random(), 0) + (q - V(0, 1.3, 0.04)) * 0.12 for q in top]
    c = V(0, 1.3, 0.04)
    p.loft([top, hem, [c + (h - c) * 0.95 for h in hem], [c + (q - c) * 0.95 for q in top]], "rags", closed=True)
    # Eiserner Halsring mit Nieten: rahmt den Kopf von unten.
    ring_c = [V(0, 2.42, 0.3), V(0, 2.5, 0.33), V(0, 2.58, 0.36)]
    p.loft(tube(ring_c, [(0.36, 0.34, 0.32), (0.4, 0.38, 0.36), (0.36, 0.34, 0.32)], 16, ref_up=V(0, 0, -1)),
           "collar", cap_start=False, cap_end=False)
    return p


def build_collar_chain():
    """Abgerissenes Kettenstueck am Halsring hinten (Phase 1). In Phase 2 reisst er es ab: Teil ausblenden."""
    return Part("collar_chain", (0, 2.45, -0.36), UV_B, parent="torso")


def build_head():
    p = Part("head", NECK, UV_A, parent="torso")
    n = 14
    # Stehender Loft vom Oberkiefer zum Scheitel, Gesicht vorn (k = n/2). Breit, flacher Schaedel, schwere Stirn.
    # Leicht zur Kettenseite geneigt (Asymmetrie).
    roll = math.radians(-6)
    up = V(math.sin(roll), math.cos(roll), 0)
    fwd = V(0, 0, 1)
    side = up.cross(fwd).normalized()
    centre = V(0, 2.86, 0.62)
    rows = [(-0.16, 0.24, 0.24, 0.16), (-0.08, 0.28, 0.27, 0.22), (0.0, 0.29, 0.26, 0.25), (0.06, 0.3, 0.3, 0.26),
            (0.14, 0.27, 0.22, 0.27), (0.22, 0.18, 0.12, 0.18)]

    def dent(i, k):
        if i == 2 and k in (6, 8):                       # Augenhoehlen
            return 0.88
        if i == 3 and k in (6, 7, 8):                    # Stirnwulst
            return 1.08
        return 1.0

    rings = []
    for i, (h, w, front, back) in enumerate(rows):
        c = centre + up * h
        ring = []
        for k in range(n):
            th = 2 * math.pi * k / n
            depth = -math.cos(th)
            d = (front if depth > 0 else back) * dent(i, k)
            ring.append(c + side * (w * math.sin(th) * dent(i, k)) + fwd * (depth * d))
        rings.append(ring)
    p.loft(rings, "head")
    for s in (-1, 1):                                    # kleine, angelegte Ohren
        base = [V(s * 0.28, 2.9, 0.58), V(s * 0.28, 2.8, 0.52), V(s * 0.29, 2.86, 0.46)]
        tip = V(s * 0.42, 2.92, 0.44)
        p.convex([[base[0], base[1], tip], [base[1], base[2], tip], [base[2], base[0], tip], list(reversed(base))],
                 "ear")
    return p


def build_jaw():
    p = Part("jaw", JAW, UV_A, parent="head")
    # Vorgeschobener, breiter Unterkiefer.
    p.loft(tube([V(0, 2.68, 0.52), V(0, 2.66, 0.7), V(0, 2.66, 0.86)],
                [(0.25, 0.1, 0.06), (0.24, 0.1, 0.06), (0.17, 0.09, 0.05)], 10), "jaw")
    # Hauer: lang, hell, nach oben vor das Gesicht - von unten das Hellste am Kopf. Der linke ist abgebrochen.
    for s, length in ((-1, 0.1), (1, 0.21)):
        root = V(s * 0.14, 2.7, 0.84)
        p.spike(tuple(root), tuple(root + V(s * 0.03, length, 0.03)), 0.04, "tusk")
    return p


def build_arm(s):
    side = "px" if s > 0 else "nx"
    drop = SHOULDER_DROP if s < 0 else 0.0
    sh = V(s * SHOULDER.x, SHOULDER.y - drop, SHOULDER.z)
    el, wr = V(s * ELBOW.x, ELBOW.y - drop, ELBOW.z), V(s * WRIST.x, WRIST.y - drop, WRIST.z)
    up = Part(f"upper_arm_{side}", sh, UV_B, parent="torso")
    up.loft(tube([sh + V(0, 0.08, 0), sh + (el - sh) * 0.5, el],
                 [(0.27, 0.27, 0.27), (0.21, 0.21, 0.21), (0.22, 0.22, 0.22)], 12, ref_up=V(0, 0, 1)), "upper_arm")
    lo = Part(f"forearm_{side}", el, UV_B, parent=up.name)
    lo.loft(tube([el + (el - sh).normalized() * 0.04, el + (wr - el) * 0.45, wr],
                 [(0.22, 0.22, 0.22), (0.25, 0.25, 0.25), (0.18, 0.18, 0.18)], 12, ref_up=V(0, 0, 1)), "forearm")
    # Handschelle am Handgelenk.
    d = (wr - el).normalized()
    lo.loft(tube([wr - d * 0.08, wr - d * 0.02, wr + d * 0.03],
                 [(0.21, 0.21, 0.21), (0.22, 0.22, 0.22), (0.21, 0.21, 0.21)], 10, ref_up=V(0, 0, 1)), "iron",
            cap_start=False, cap_end=False)
    # Faust: grosser Block mit Knoecheln, Daumen.
    fist = wr + d * 0.16
    lo.loft(tube([wr, fist, fist + d * 0.14], [(0.16, 0.14, 0.16), (0.19, 0.16, 0.18), (0.16, 0.13, 0.15)], 8,
                 ref_up=V(0, 0, 1)), "hand")
    lo.loft(tube([wr + d * 0.05 + V(-s * 0.12, 0, 0.08), wr + d * 0.14 + V(-s * 0.14, 0, 0.14),
                  wr + d * 0.2 + V(-s * 0.1, 0, 0.18)], [(0.06, 0.06, 0.06), (0.055,) * 3, (0.045,) * 3], 6,
                 ref_up=V(0, 1, 0)), "hand")
    return [up, lo]


GRIP = V(WRIST.x, WRIST.y - 0.24, WRIST.z + 0.06)       # Mitte der rechten Faust


def build_club():
    p = Part("club", GRIP, UV_B, parent="forearm_px")
    # Keule haengt in der Faust, Kopf nach unten vorn: Griff 0,5 m, Kopf 0,8 m.
    axis = V(0.05, -0.3, 0.95).normalized()       # flach nach vorn unten: die Spitze bleibt ueber dem Boden
    a = GRIP - axis * 0.25
    b = GRIP + axis * 0.3
    tip = GRIP + axis * 1.15
    p.loft(tube([a, GRIP, b], [(0.06, 0.06, 0.06), (0.065,) * 3, (0.07,) * 3], 8, ref_up=V(1, 0, 0)), "club")
    p.loft(tube([b, b + axis * 0.15, b + axis * 0.5, tip],
                [(0.12, 0.12, 0.12), (0.2, 0.2, 0.2), (0.24, 0.24, 0.24), (0.21, 0.21, 0.21)], 10,
                ref_up=V(1, 0, 0)), "club")
    for t in (0.22, 0.52, 0.8):                          # Eisenbaender
        c = b + axis * t
        p.loft(tube([c - axis * 0.03, c + axis * 0.03], [(0.255, 0.255, 0.255)] * 2, 10, ref_up=V(1, 0, 0)),
               "iron", cap_start=False, cap_end=False)
    return p


def link_rings(centre, axis_a, axis_b, length=0.16, width=0.1, thick=0.022, steps=10):
    """Ein Kettenglied als geschlossener Ring: Ellipse in der Ebene (axis_a, axis_b), quadratischer Querschnitt."""
    pts = [centre + axis_a * (length / 2 * math.cos(2 * math.pi * i / steps))
           + axis_b * (width / 2 * math.sin(2 * math.pi * i / steps)) for i in range(steps)]
    normal = axis_a.cross(axis_b).normalized()
    rings = []
    for i, p in enumerate(pts):
        t = (pts[(i + 1) % steps] - pts[i - 1]).normalized()
        out = t.cross(normal).normalized()
        rings.append([p + out * (thick * math.cos(a)) + normal * (thick * math.sin(a))
                      for a in (math.pi / 4, 3 * math.pi / 4, 5 * math.pi / 4, 7 * math.pi / 4)])
    return rings


def build_link():
    """Ein Kettenglied (Drehpunkt = Mitte, laengs entlang y). Der Code reiht es auf, jedes zweite um 90 Grad gedreht."""
    p = Part("chain_link", (0, 0, 0), UV_B)
    p.loft(link_rings(V(0, 0, 0), V(0, 1, 0), V(1, 0, 0)), "chain", closed=True)
    return p


def build_hook():
    """Der Haken (Drehpunkt = Oese oben): ein gekruemmter, spitzer Eisenhaken."""
    p = Part("hook", (0, 0, 0), UV_B)
    p.loft(link_rings(V(0, -0.02, 0), V(0, 1, 0), V(1, 0, 0), 0.1, 0.08, 0.02, 8), "iron", closed=True)
    pts = [V(0, -0.06, 0), V(0, -0.25, 0), V(0, -0.4, 0.06), V(0, -0.42, 0.18), V(0, -0.33, 0.26)]
    p.loft(tube(pts, [(0.035, 0.035, 0.035), (0.04, 0.04, 0.04), (0.035, 0.035, 0.035), (0.028,) * 3, (0.006,) * 3],
                6, ref_up=V(1, 0, 0)), "iron")
    return p


def build_leg(s):
    side = "px" if s > 0 else "nx"
    hip, knee, ankle = V(s * HIP_J.x, HIP_J.y, HIP_J.z), V(s * KNEE.x, KNEE.y, KNEE.z), V(s * ANKLE.x, ANKLE.y, ANKLE.z)
    th = Part(f"thigh_{side}", hip, UV_B, parent="torso")
    th.loft(tube([hip + V(0, 0.08, 0), hip + (knee - hip) * 0.5, knee], [(0.3, 0.3, 0.3), (0.27,) * 3, (0.24,) * 3],
                 12, ref_up=V(0, 0, 1)), "thigh")
    sh = Part(f"shin_{side}", knee, UV_B, parent=th.name)
    sh.loft(tube([knee + V(0, 0.03, 0), knee + (ankle - knee) * 0.5, ankle], [(0.23,) * 3, (0.22,) * 3, (0.17,) * 3],
                 12, ref_up=V(0, 0, 1)), "shin")
    sh.loft(tube([ankle + V(0, 0.02, -0.12), ankle + V(0, -0.04, 0.08), ankle + V(0, -0.1, 0.3)],
                 [(0.18, 0.1, 0.12), (0.22, 0.08, 0.1), (0.2, 0.04, 0.06)], 8, ref_up=V(0, 1, 0)), "foot")
    return [th, sh]


# --- Posen --------------------------------------------------------------------------------------------------------

def abduct(s, deg):
    """Arm seitlich abspreizen (um die Laengsachse des Koerpers): positiv = Hand nach aussen/oben."""
    return Matrix.Rotation(math.radians(deg) * s, 4, Vector((0, -1, 0)))


POSES = {
    "neutral": {},                                     # so steht er in der Datei
    # Teil: (heben, abspreizen)
    "ruhe": {"jaw": (-6, 0), "forearm_px": (14, 0), "forearm_nx": (10, 0)},
    "schwung_an": {"upper_arm_nx": (160, 25), "forearm_nx": (20, 0), "head": (8, 0), "jaw": (-20, 0),
                   "torso": (6, 0), "forearm_px": (14, 0)},
    "schwung": {"upper_arm_nx": (15, 85), "forearm_nx": (0, 0), "torso": (-6, 0), "jaw": (-25, 0),
                "upper_arm_px": (-10, -15), "forearm_px": (14, 0)},
    "haken_an": {"upper_arm_nx": (-40, 45), "forearm_nx": (60, 0), "torso": (4, 0), "head": (-4, 0), "jaw": (-12, 0),
                 "forearm_px": (14, 0)},
    "phase2_bruellen": {"torso": (16, 0), "head": (38, 0), "jaw": (-45, 0), "upper_arm_px": (150, 30),
                        "upper_arm_nx": (150, 30), "forearm_px": (20, 0), "forearm_nx": (20, 0)},
    "phase2_ruhe": {"torso": (-8, 0), "head": (-6, 0), "jaw": (-22, 0), "upper_arm_px": (8, 18),
                    "upper_arm_nx": (8, 18), "forearm_px": (24, 0), "forearm_nx": (24, 0)},
}


def pose_gorm(objs, name, root):
    by = {o.name: o for o in objs}
    mats = {}

    def local(o):
        r, a = POSES[name].get(o.name, (0, 0))
        s = 1 if o.name.endswith("px") else -1
        return abduct(s, a) @ raise_fwd(r)

    def world(n):
        if n in mats:
            return mats[n]
        o = by[n]
        pivot = Vector(list(o["pivot"]))
        parent = o["parent"]
        if parent and parent in by:
            m = world(parent) @ Matrix.Translation(G(pivot) - G(Vector(list(by[parent]["pivot"])))) @ local(o)
        else:
            m = root @ Matrix.Translation(G(pivot)) @ local(o)
        mats[n] = m
        return m

    for o in objs:
        o.matrix_world = world(o.name)
    bpy.context.view_layer.update()
    return mats


# --- Kette als Reihe von Gliedern ---------------------------------------------------------------------------------

def chain_objects(link, path, coll, hook=None, glow=False):
    """Glieder entlang einer Linie (Blender-Punkte), jedes zweite um 90 Grad gedreht; optional der Haken am Ende."""
    objs = []
    start, end = path
    d = end - start
    count = max(2, int(d.length / 0.13))
    q = d.normalized().to_track_quat("Z", "X")       # Blender-z des Gliedes (Spiel-y) entlang der Kette
    for i in range(count):
        o = link.copy()
        o.data = link.data.copy()
        coll.objects.link(o)
        spin = Matrix.Rotation(math.pi / 2 * (i % 2), 4, "Z")
        o.matrix_world = Matrix.Translation(start + d * ((i + 0.5) / count)) @ q.to_matrix().to_4x4() @ spin
        objs.append(o)
    if hook is not None:
        h = hook.copy()
        h.data = hook.data.copy()
        coll.objects.link(h)
        h.matrix_world = Matrix.Translation(end) @ q.to_matrix().to_4x4() @ Matrix.Rotation(math.pi, 4, "X")
        if glow:
            h.data.materials[0] = GLOW_MAT[0]
        objs.append(h)
    bpy.context.view_layer.update()
    return objs


GLOW_MAT = []


# --- Raum (Bossraum, Decke 6 m, vier Saeulen, Level-Texturen Fassung 2) ------------------------------------------

def quad_grid(coll, name, mat, corner, du, dv, lu, lv, centre, step=0.5):
    nu, nv = int(round(lu / step)), int(round(lv / step))
    bm = bmesh.new()
    uv = bm.loops.layers.uv.verify()
    verts = [[bm.verts.new(corner + du * (i * step) + dv * (j * step)) for j in range(nv + 1)] for i in range(nu + 1)]
    for i in range(nu):
        for j in range(nv):
            f = bm.faces.new((verts[i][j], verts[i + 1][j], verts[i + 1][j + 1], verts[i][j + 1]))
            for loop, (a, b) in zip(f.loops, ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))):
                loop[uv].uv = (a * step, b * step)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    obj["smooth"] = False
    coll.objects.link(obj)
    poly = mesh.polygons[0]
    if centre is not None and poly.normal.dot(centre - poly.center) < 0:
        mesh.flip_normals()
    mesh.materials.append(mat)
    return obj


def hall(mats, half=8.0, height=6.0):
    coll = bpy.data.collections.new("Halle")
    bpy.context.scene.collection.children.link(coll)
    X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
    c = Vector((0, 0, height / 2))
    objs = [quad_grid(coll, "boden", mats["floor"], Vector((-half, -half, 0)), X, Y, 2 * half, 2 * half, c),
            quad_grid(coll, "decke", mats["ceiling"], Vector((-half, -half, height)), X, Y, 2 * half, 2 * half, c)]
    for name, corner, du in (("w1", Vector((-half, -half, 0)), X), ("w2", Vector((half, -half, 0)), Y),
                             ("w3", Vector((-half, half, 0)), X), ("w4", Vector((-half, -half, 0)), Y)):
        objs.append(quad_grid(coll, name, mats["wall"], corner, du, Z, 2 * half, height, c))
    for px, py in ((-4, -4), (4, -4), (-4, 4), (4, 4)):                       # vier Saeulen wie great-hall
        h = 0.6
        for k, (corner, du) in enumerate(((Vector((px - h, py - h, 0)), X), (Vector((px + h, py - h, 0)), Y),
                                         (Vector((px - h, py + h, 0)), X), (Vector((px - h, py - h, 0)), Y))):
            o = quad_grid(coll, f"s{px}{py}{k}", mats["wall"], corner, du, Z, 2 * h, height, None)
            poly = o.data.polygons[0]
            if poly.normal.dot(poly.center - Vector((px, py, poly.center.z))) < 0:
                o.data.flip_normals()
            objs.append(o)
    for o in objs:
        write_light(o)
    return objs


def level_mats():
    mats = {}
    for key in ("wall", "floor", "ceiling"):
        img = bpy.data.images.load(os.path.join(LEVEL_DIR, f"catacomb_{key}_v2.png"), check_existing=False)
        img.name = f"lvl_{key}"
        mats[key] = pass_material("lvl_" + key, img)
    return mats


# --- Messung ------------------------------------------------------------------------------------------------------

def measure(label, model, cam, eye, target, hidden=()):
    aim(cam, G(eye), G(target))
    mask = coverage(render_passes("mess_" + label, visible=[o for o in model if o not in hidden]))
    rows = np.where(mask.any(axis=1))[0]
    cols = np.where(mask.any(axis=0))[0]
    return f"{label}: {cols.max() - cols.min() + 1} x {rows.max() - rows.min() + 1} Pixel, {int(mask.sum())} Pixel gedeckt"


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    img_a = save_image("gorm_body", paint_atlas(RECTS_A, PAINT_A, TEX, colors=32, seed=2),
                       os.path.join(OUT_DIR, "gorm_textur_rumpf.png"))
    img_a2 = save_image("gorm_body_phase2", paint_atlas(RECTS_A, PAINT_A2, TEX, colors=32, seed=2),
                        os.path.join(OUT_DIR, "gorm_textur_rumpf_phase2.png"))
    img_b = save_image("gorm_limbs", paint_atlas(RECTS_B, PAINT_B, TEX, colors=32, seed=3),
                       os.path.join(OUT_DIR, "gorm_textur_glieder.png"))
    glow = np.array(img_b.pixels[:]).reshape(TEX, TEX, 4)[..., :3] * 255 * 0.4 + WARNING * 0.6
    img_glow = save_image("gorm_glow", np.clip(glow, 0, 255), os.path.join(os.environ.get("TEMP", "."), "gorm_glow.png"))

    coll = bpy.data.collections.new("Gorm")
    bpy.context.scene.collection.children.link(coll)
    mat_a, mat_b = export_material("gorm_body", img_a), export_material("gorm_limbs", img_b)
    parts_a = [build_torso(), build_head(), build_jaw()]
    parts_b = build_arm(1) + build_arm(-1) + build_leg(1) + build_leg(-1) + [build_club()]
    model = [p.build(coll, mat_a) for p in parts_a] + [p.build(coll, mat_b) for p in parts_b]
    for o in model:
        if o.name == "club":
            o["smooth"] = False
    link = build_link().build(coll, mat_b, smooth=False)
    hook = build_hook().build(coll, mat_b, smooth=False)
    tris = {o.name: len(o.data.polygons) for o in model + [link, hook]}
    print("Dreiecke:", sum(tris.values()), tris)

    # Export in neutraler Lage (alle Winkel 0); Kettenglied und Haken je am Ursprung.
    pose_gorm(model, "neutral", Matrix())
    bpy.ops.object.select_all(action="DESELECT")
    for o in model + [link, hook]:
        o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=os.path.join(OUT_DIR, "gorm.glb"), export_format="GLB", use_selection=True,
                              export_yup=True, export_normals=True, export_vertex_color="NONE",
                              export_materials="EXPORT", export_texcoords=True)

    pa, pa2, pb = pass_material("p_a", img_a), pass_material("p_a2", img_a2), pass_material("p_b", img_b)
    GLOW_MAT.append(pass_material("p_glow", img_glow))
    for o in model:
        o.data.materials[0] = pa if o.name in ("torso", "head", "jaw") else pb
    link.data.materials[0] = pb
    hook.data.materials[0] = pb
    link.hide_render = hook.hide_render = True
    cam = camera()
    chain_coll = bpy.data.collections.new("Kette")
    bpy.context.scene.collection.children.link(chain_coll)

    def set_phase(phase2):
        for o in model:
            if o.name in ("torso", "head", "jaw"):
                o.data.materials[0] = pa2 if phase2 else pa

    def clear_chain():
        for o in list(chain_coll.objects):
            bpy.data.objects.remove(o)

    def hand_world(mats, s):
        o = next(o for o in model if o.name == ("forearm_px" if s > 0 else "forearm_nx"))
        drop = SHOULDER_DROP if s < 0 else 0.0
        wr = V(s * WRIST.x, WRIST.y - drop, WRIST.z)
        el = V(s * ELBOW.x, ELBOW.y - drop, ELBOW.z)
        local = G(wr + (wr - el).normalized() * 0.22) - G(Vector(list(o["pivot"])))
        return mats[o.name] @ local

    def setup_pose(name, root, phase2=False):
        mats = pose_gorm(model, name, root)
        set_phase(phase2)
        clear_chain()
        hand = hand_world(mats, -1)
        extra = []
        down = Vector((0, 0, -1))
        if name in ("ruhe", "phase2_ruhe", "phase2_bruellen"):
            extra = chain_objects(link, (hand, hand + down * 0.9), chain_coll, hook)
        elif name == "schwung_an":
            # Kette kreist ueber dem Kopf, waagerecht, so weit sie reicht (wie GormView): hier eine Stellung.
            out = root.to_3x3() @ Vector((-0.8, 0.6, 0)).normalized()
            extra = chain_objects(link, (hand, hand + out * 4.0), chain_coll, hook)
        elif name == "schwung":
            # Kette gestreckt auf Brusthoehe, von seiner Linken nach vorn streichend (45 Grad vor links).
            target = root @ G(V(-5.6, 1.3, 5.6))
            extra = chain_objects(link, (hand, target), chain_coll, hook)
        elif name == "haken_an":
            extra = chain_objects(link, (hand, hand + down * 0.55), chain_coll, hook, glow=True)
        if not phase2:
            # Phase 1: am Halsring haengt hinten ein abgerissenes Kettenstueck. In Phase 2 hat er es abgerissen.
            collar = mats["torso"] @ (G(V(0, 2.42, -0.12)) - G(HIP))
            back = mats["torso"].to_3x3() @ G(V(0, -0.8, -0.35)).normalized()
            extra = extra + chain_objects(link, (collar, collar + back * 0.9), chain_coll)
        for o in model + extra:
            write_light(o)
        return extra

    # Vier Ansichten.
    setup_render(640, 640)
    cam.data.angle_y = math.radians(30)
    extra = setup_pose("ruhe", Matrix())
    for name, loc in {"ansicht_vorn": V(0, 1.8, 9.0), "ansicht_seite": V(9.0, 1.8, 0.3),
                      "ansicht_hinten": V(0, 1.8, -9.0), "ansicht_schraeg_oben": V(-5.0, 6.0, 6.0)}.items():
        aim(cam, G(loc), G(V(0, 1.6, 0.2)))
        save_png(compose(render_passes(name, visible=model + extra), "ansicht"), os.path.join(OUT_DIR, name + ".png"))

    # Spielbilder in der Halle: Gorm in der Mitte, Spieler davor.
    room = hall(level_mats())
    setup_render(854, 480)
    cam.data.angle_y = math.radians(70)
    gorm_at = V(0, 0, -3.5)        # hinten in der Halle, damit auch 10 m Abstand drinnen liegen
    report = []
    shots = [("ruhe", 2, 2.9, False), ("ruhe", 4, 2.2, False), ("ruhe", 10, 1.7, False),
             ("schwung_an", 6, 2.2, False), ("schwung", 6, 1.8, False), ("haken_an", 6, 2.0, False),
             ("phase2_bruellen", 6, 2.2, True), ("phase2_ruhe", 10, 1.7, True)]
    results = {}
    for name, d, look, phase2 in shots:
        eye = V(0, 1.6, gorm_at.z + d)
        to = eye - gorm_at
        root = Matrix.Translation(G(gorm_at)) @ Matrix.Rotation(math.atan2(to.x, to.z), 4, "Z")
        extra = setup_pose(name, root, phase2)
        target = V(0, look, gorm_at.z)
        aim(cam, G(eye), G(target))
        img = compose(render_passes(f"{name}_{d}", visible=model + extra + room), "spiel")
        results[(name, d)] = img
        save_png(img, os.path.join(OUT_DIR, f"spiel_{d}m_{name}.png"))
        if name in ("ruhe", "phase2_ruhe") and d == 10:
            report.append(measure(f"{name} aus 10 m", model + extra, cam, eye, target))
        if name == "ruhe" and d == 4:
            report.append(measure("ruhe aus 4 m", model + extra, cam, eye, target))
            head = [o for o in model if o.name in ("head", "jaw")]
            report.append(measure("Kopf aus 4 m", head, cam, eye, target))
    # Phase 1 und 2 aus 10 m nebeneinander (Ausschnitt, dreifach).
    from psx import zoom
    rows, cols = (150, 330), (337, 517)
    save_png(side_by_side([zoom(results[("ruhe", 10)], rows, cols, 3), zoom(results[("phase2_ruhe", 10)], rows, cols, 3)]),
             os.path.join(OUT_DIR, "vergleich_phase1_phase2_10m.png"))
    with open(os.path.join(OUT_DIR, "messung.txt"), "w", encoding="utf-8") as fh:
        fh.write("\n".join(report) + "\n")
    print("\n".join(report))


if __name__ == "__main__":       # auch als Modul nutzbar (gorm_koepfe.py)
    main()
