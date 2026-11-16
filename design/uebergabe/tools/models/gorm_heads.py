"""Gorm: die drei Gesichter (gewaehlt: Eisenmaske).

Bausteine fuer das Spiel (Uebergabe aus Probestueck 9, Branch claude/design-probe-ghul-45a8c4).
Kein Bildcode. Aufruf nur ueber tools/models/gorm.py. Herkunft: alles per Skript, nichts heruntergeladen.
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gorm_base as g  # noqa: E402
import psx  # noqa: E402
import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402
from psx import (V, G, Part, tube, vnoise, blob, mix, paint_atlas, save_image, pass_material,  # noqa: E402
                 write_light, setup_render, camera, aim, render_passes, compose, save_png, side_by_side)

OUT_DIR = None
IRON_M = np.array((112, 112, 120), float)       # Helm: mittleres Eisengrau, heller als der Halsring
SACK = np.array((146, 136, 112), float)         # grobes Leinen, fahl
WET = np.array((76, 64, 54), float)


# --- Koepfe -------------------------------------------------------------------------------------------------------

def head_loft(part, rows, dent, centre=V(0, 2.86, 0.62), roll=-6, n=14):
    up = V(math.sin(math.radians(roll)), math.cos(math.radians(roll)), 0)
    fwd = V(0, 0, 1)
    side = up.cross(fwd).normalized()
    rings = []
    for i, (h, w, front, back) in enumerate(rows):
        c = centre + up * h
        ring = []
        for k in range(n):
            th = 2 * math.pi * k / n
            depth = -math.cos(th)
            f = dent(i, k)
            ring.append(c + side * (w * math.sin(th) * f) + fwd * (depth * (front if depth > 0 else back) * f))
        rings.append(ring)
    part.loft(rings, "head")


def head_mensch():
    head = Part("head", g.NECK, g.UV_A, parent="torso")
    rows = [(-0.17, 0.2, 0.2, 0.15), (-0.09, 0.25, 0.25, 0.21), (0.0, 0.27, 0.25, 0.25), (0.07, 0.28, 0.27, 0.27),
            (0.15, 0.26, 0.21, 0.27), (0.23, 0.17, 0.1, 0.17)]

    def dent(i, k):
        if i == 2 and k in (6, 8):                  # tief eingefallene Augen
            return 0.82
        if i == 1 and k in (5, 9):                  # hohle Wangen
            return 0.9
        if i == 3 and k in (6, 7, 8):               # schwere Stirn
            return 1.06
        return 1.0

    head_loft(head, rows, dent)
    # Ein kleines, zerfetztes Ohr rechts; links fehlt es (abgerissen).
    head.box((0.255, 2.82, 0.5), (0.29, 2.93, 0.6), "ear")
    jaw = Part("jaw", g.JAW, g.UV_A, parent="head")
    jaw.loft(tube([V(0, 2.69, 0.5), V(0, 2.67, 0.66), V(0, 2.67, 0.78)],
                  [(0.21, 0.08, 0.05), (0.19, 0.08, 0.05), (0.13, 0.07, 0.045)], 10), "jaw")
    return [head, jaw]


def head_eisen():
    head = Part("head", g.NECK, g.UV_A, parent="torso")
    rows = [(-0.17, 0.22, 0.22, 0.17), (-0.09, 0.27, 0.27, 0.23), (0.0, 0.28, 0.27, 0.26), (0.08, 0.29, 0.28, 0.28),
            (0.16, 0.27, 0.23, 0.27), (0.24, 0.2, 0.14, 0.2)]
    head_loft(head, rows, lambda i, k: 1.0, roll=-3)
    # Stirnplatte und Bolzen an den Schlaefen, durch den Helm geschlagen.
    head.box((-0.24, 2.98, 0.82), (0.24, 3.04, 0.9), "collar")
    for s in (-1, 1):
        head.spike((s * 0.28, 2.88, 0.62), (s * 0.4, 2.88, 0.62), 0.04, "collar")
    # Gitter-Maulkorb vor dem Mund: senkrechte Staebe, ein Querband, ein Ring am Kinn.
    for x in (-0.14, -0.07, 0.0, 0.07, 0.14):
        head.box((x - 0.013, 2.6, 0.86), (x + 0.013, 2.84, 0.9), "collar")
    head.box((-0.17, 2.7, 0.86), (0.17, 2.73, 0.91), "collar")
    jaw = Part("jaw", g.JAW, g.UV_A, parent="head")
    jaw.loft(tube([V(0, 2.69, 0.52), V(0, 2.67, 0.68), V(0, 2.67, 0.8)],
                  [(0.22, 0.08, 0.05), (0.2, 0.08, 0.05), (0.14, 0.07, 0.045)], 10), "jaw")
    return [head, jaw]


def head_sack():
    head = Part("head", g.NECK, g.UV_A, parent="torso")
    # Der Sack ist groesser und runder als der Schaedel, faellt bis auf den Halsring und hat oben einen Zipfel.
    rows = [(-0.3, 0.3, 0.24, 0.26), (-0.18, 0.3, 0.29, 0.27), (-0.06, 0.31, 0.3, 0.29), (0.05, 0.31, 0.3, 0.3),
            (0.15, 0.28, 0.25, 0.28), (0.24, 0.2, 0.16, 0.2), (0.31, 0.06, 0.05, 0.08)]

    def dent(i, k):
        if i == 3 and k in (6, 8):                  # Stoff sinkt in die Augenhoehlen
            return 0.93
        if i in (1, 2) and k % 3 == 0:              # grobe Falten
            return 1.04
        return 1.0

    head_loft(head, rows, dent, centre=V(0, 2.84, 0.6), roll=-8)
    return [head]


# --- Kopftexturen (Bereich "head": u rundherum, 0 hinten, 0,5 vorn; v vom Kinn zum Scheitel) ----------------------

def paint_head_mensch(U, V_, phase2=False):
    col = g.skin(U, V_, 120, pale=0.95)
    col = g.scars(col, U, V_, 121, color=g.SKIN_PALE * 1.12, count=9)
    col *= (1 - 0.35 * blob(U, V_, 0.5, 0.58, 0.22, 0.07))[..., None]              # Schatten unter der Stirn
    for sgn, cv in ((-1, 0.56), (1, 0.545)):                                         # ungleich tiefe Augen
        cu = 0.5 + sgn * 0.075
        col = mix(col, g.HOLLOW, np.clip(blob(U, V_, cu, cv, 0.05, 0.07) * 2.6, 0, 1))
        col = mix(col, g.EYE * 0.9, (blob(U, V_, cu, cv, 0.012, 0.016) > 0.4).astype(float))
    col = mix(col, g.HOLLOW, (blob(U, V_, 0.5, 0.42, 0.03, 0.025) > 0.2).astype(float))   # platte, gebrochene Nase
    # Zugenaehter Mund: dunkle, nach unten gezogene Naht mit hellen Stichen quer darueber.
    line_v = 0.24 - 0.35 * np.abs(U - 0.5) - 0.04 * (U - 0.5)
    mouth = (np.abs(V_ - line_v) < 0.02) & (np.abs(U - 0.5) < 0.14)
    col = mix(col, g.HOLLOW, mouth.astype(float))
    stitch = (np.abs(V_ - line_v) < 0.06) & ((((U - 0.36) / 0.28) * 7 % 1.0) < 0.22) & (np.abs(U - 0.5) < 0.14)
    col = mix(col, g.TUSK * 0.85, stitch.astype(float))
    if phase2:                                                                      # Naehte aufgerissen
        torn = (np.abs(V_ - line_v + 0.03) < 0.035 + 0.02 * vnoise(*U.shape, 3, 12, 123)) & (np.abs(U - 0.5) < 0.12)
        col = mix(col, g.BLOOD * 0.6, torn.astype(float) * 0.85)            # dunkel und ausgefranst, kein Lippenrot
        col = g.blood_streaks(col, U, V_, 122, 0.6)
    return col


def paint_head_eisen(U, V_, phase2=False):
    h, w = U.shape
    col = np.broadcast_to(IRON_M, (h, w, 3)).copy()
    col *= (0.8 + 0.35 * vnoise(h, w, 8, 16, 130))[..., None]
    col = mix(col, g.RUST, np.clip((vnoise(h, w, 6, 12, 131) - 0.5) * 2.2, 0, 0.7))
    # Plattenstoesse und Nieten (hell, von unten gut zu sehen).
    seams = (((U * 6) % 1.0) < 0.04) | (np.abs(V_ - 0.75) < 0.012)
    col = mix(col, g.SHADOW, seams.astype(float) * 0.7)
    riv = ((((U * 12) % 1.0) - 0.5) ** 2 + ((V_ - 0.78) * 6) ** 2) < 0.03
    col = mix(col, g.RIVET * 1.2, riv.astype(float))
    # Sehschlitze: waagerecht, dunkel, darin die Augen.
    for sgn in (-1, 1):
        cu = 0.5 + sgn * 0.08
        slit = (np.abs(V_ - 0.56) < 0.025) & (np.abs(U - cu) < 0.055)
        col = mix(col, g.HOLLOW, slit.astype(float))
        col = mix(col, g.EYE, (blob(U, V_, cu, 0.56, 0.012, 0.014) > 0.3).astype(float))
    # Unter dem Gitter: dunkler Mundraum mit hellen, schiefen Zaehnen.
    mouth = (np.abs(U - 0.5) < 0.13) & (V_ < 0.3) & (V_ > 0.08)
    col = mix(col, g.HOLLOW, mouth.astype(float))
    teeth = mouth & (V_ > 0.15) & (V_ < 0.24) & ((((U - 0.37) / 0.26 * 8) % 1.0) < 0.6)
    col = mix(col, g.TUSK, teeth.astype(float))
    if phase2:                                                                      # Blut quillt unter dem Helm hervor
        col = g.blood_streaks(col, U, V_, 132, 0.7)
    return col


def paint_head_sack(U, V_, phase2=False):
    h, w = U.shape
    col = np.broadcast_to(SACK, (h, w, 3)).copy()
    weave = (((U * 64) % 1.0) < 0.5) ^ (((V_ * 48) % 1.0) < 0.5)                   # grobes Gewebe
    col *= (0.92 + 0.08 * weave)[..., None]
    col *= (0.8 + 0.3 * vnoise(h, w, 6, 10, 140))[..., None]
    col = mix(col, WET, np.clip((vnoise(h, w, 4, 8, 141) - 0.6) * 2, 0, 0.4))      # Flecken
    col = mix(col, g.SHADOW, (((U * 7) % 1.0) < 0.05).astype(float) * 0.35)         # Falten
    seam = (np.abs(U - 0.5) < 0.012) & (V_ > 0.62) & (((V_ * 30) % 1.0) < 0.5)      # Naht ueber den Scheitel
    col = mix(col, g.SHADOW, seam.astype(float))
    # Gerissene Augenloecher: ungleich, ausgefranst, darin die Augen.
    for sgn, cv, r in ((-1, 0.6, 0.06), (1, 0.585, 0.05)):
        cu = 0.5 + sgn * 0.085
        rag = blob(U, V_, cu, cv, r, r * 1.1) + 0.25 * vnoise(h, w, 20, 20, 142) > 0.55
        col = mix(col, g.HOLLOW, rag.astype(float))
        col = mix(col, g.EYE, (blob(U, V_, cu, cv, 0.013, 0.016) > 0.3).astype(float))
    # Unten vor dem Mund durchnaesst: dunkler Fleck, der Atem und Speichel zeigt.
    col = mix(col, WET * 0.8, np.clip(blob(U, V_, 0.5, 0.3, 0.13, 0.12) * 1.6, 0, 0.85))
    if phase2:                                                                      # Blut tritt durch den Stoff
        soak = np.clip(blob(U, V_, 0.5, 0.28, 0.12, 0.14) * 2 - 0.3, 0, 1) * (0.5 + 0.5 * vnoise(*U.shape, 8, 8, 144))
        col = mix(col, g.BLOOD * 0.55, soak)                               # durchgesickert, dunkel und fleckig
        col = g.blood_streaks(col, U, V_, 143, 0.5)
    return col


def paint_jaw_closed(U, V_):
    return g.skin(U, V_, 150, pale=0.85)


VARIANTS = [
    ("mensch", head_mensch, paint_head_mensch, paint_jaw_closed),
    ("eisen", head_eisen, paint_head_eisen, g.paint_jaw),
    ("sack", head_sack, paint_head_sack, paint_jaw_closed),
]
