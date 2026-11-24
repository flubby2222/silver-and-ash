"""Keule des Jaegers (Stufe Worn) mit Aermel

Erzeuger fuer das Spiel, gleicher Aufbau wie blade.py (Issue #403): ein Knoten "club" mit Drehpunkt am Griff,
Massstab 1,8 eingebacken, Textur 128 x 128 ohne Filterung. Der Aermel (club_hand.glb) ist der der Klinge
(arm.build_hand_parts), nur an die Keule gehaengt; es entsteht keine neue Hand.
Erzeugt nur Modell und Textur nach assets/models/, keine Probebilder (die Bilder: design/proben/grafik/keule-403/blatt.py).

Aufruf (aus dem Repository-Wurzelverzeichnis):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/models/club.py

Masse wie der Platzhalter in WeaponViews.cs (DrawBlade, Zweig Club): Griff 0,1 m lang und 0,022 m dick, Kopf 0,11 m lang
und 0,038 m dick, gerade auf dem Griff (seit #414; vorher 25 Grad geneigt), ab z = 0,04 im Griff steckend (Spielmass; die Datei ist 1,8-fach, das Spiel zeichnet sie halb).
Herkunft: alles per Skript, nichts heruntergeladen.
"""

import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from mathutils import Matrix  # noqa: E402
import arm as _arm  # noqa: E402
from arm import C  # noqa: E402
from psx import bake_scale, export_glb, report_parts  # noqa: E402
from psx import (V, Part, tube, rects_from_pixels, vnoise, blob, mix, paint_atlas, save_image,  # noqa: E402
                 export_material, pose)

OUT_DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
                       "assets", "models")
TEX = 128
SCALE = 1.8

WOOD = np.array((110, 76, 46), float)        # Holz, Stufe 3 (Farbe der Stufe Worn)
WOOD_LIGHT = np.array((156, 118, 76), float)  # abgegriffene Stellen
WOOD_DARK = np.array((58, 38, 26), float)     # Kerben, Hirnholz
IRON = np.array((62, 66, 78), float)
IRON_LIGHT = np.array((118, 122, 136), float)
RUST = np.array((112, 66, 42), float)
LEATHER = np.array((66, 46, 34), float)
STITCH = np.array((120, 104, 82), float)

# Felder wie bei der Klinge: glove, cuff, sleeve unveraendert (der Aermel kommt aus arm.py).
RECTS = {
    "glove": (0, 16, 64, 48),
    "wood": (0, 64, 128, 128),
    "iron": (0, 48, 64, 64),
    "grip": (64, 48, 128, 64),
    "cuff": (0, 0, 64, 16),
    "sleeve": (64, 0, 128, 48),
}
UV = rects_from_pixels(RECTS, TEX)

# Kerben als (u, v, Halbbreite u, Halbbreite v) im Holzfeld; u rundherum, v vom Griff zum Ende.
NICKS = [(0.06, 0.38, 0.020, 0.040), (0.32, 0.55, 0.018, 0.030), (0.58, 0.28, 0.022, 0.035),
         (0.81, 0.62, 0.016, 0.045), (0.44, 0.80, 0.018, 0.028), (0.17, 0.90, 0.020, 0.030)]


def paint_wood(U, V_):
    """u rundherum (8 Seiten), v vom Griff zum Ende. Maserung laeuft entlang v, abgegriffen, mit Kerben."""
    h, w = U.shape
    col = np.broadcast_to(WOOD, (h, w, 3)).copy()
    col *= (0.80 + 0.40 * vnoise(h, w, 3, 40, 91))[..., None]                              # Maserung
    col = mix(col, WOOD_LIGHT, np.clip((vnoise(h, w, 5, 8, 92) - 0.55) * 2.2, 0, 0.7))    # abgegriffen
    col = mix(col, WOOD_DARK, np.clip((0.32 - vnoise(h, w, 4, 16, 93)) * 3, 0, 0.55))     # dunkle Fasern
    for cu, cv, ru, rv in NICKS:
        nick = (blob(U, V_, cu, cv, ru, rv) > 0.25).astype(float)
        col = mix(col, WOOD_DARK, nick * 0.9)
    return col


def paint_iron(U, V_):
    h, w = U.shape
    col = np.broadcast_to(IRON, (h, w, 3)).copy()
    col *= (0.82 + 0.30 * vnoise(h, w, 8, 8, 94))[..., None]
    col = mix(col, IRON_LIGHT, (np.abs(V_ - 0.5) < 0.09).astype(float) * 0.55)             # helle Kante in der Mitte
    col = mix(col, RUST, np.clip((vnoise(h, w, 6, 10, 95) - 0.66) * 3, 0, 0.7))            # Rost
    rivet = np.minimum.reduce([blob(U, V_, c, 0.5, 0.05, 0.22) for c in (0.125, 0.375, 0.625, 0.875)])
    col = mix(col, IRON_LIGHT, (rivet > 0.4).astype(float) * 0.8)                          # Nietkoepfe
    return col


def paint_grip(U, V_):
    h, w = U.shape
    col = np.broadcast_to(WOOD * 0.8, (h, w, 3)).copy()
    col *= (0.82 + 0.30 * vnoise(h, w, 2, 20, 96))[..., None]
    wrap = (U * 1.0 + V_ * 6) % 1.0
    col = mix(col, LEATHER, (wrap > 0.18).astype(float) * 0.92)                            # schraege Lederwicklung
    col *= (0.8 + 0.35 * vnoise(h, w, 4, 10, 97))[..., None]
    col = mix(col, STITCH, (wrap < 0.07).astype(float) * 0.7)                              # Garn zwischen den Lagen
    return col


PAINTERS = {"wood": paint_wood, "iron": paint_iron, "grip": paint_grip}
PAINTERS.update(_arm.design_painters(_arm.DESIGN))
PAINTERS.setdefault("wood", paint_wood)


# Kopf: gerade Verlaengerung des Griffs (Achse z, Issue #414), beginnt bei z = 0,04 und steckt mit dem ersten Stueck im
# Griff (der bis 0,05 reicht): keine Luecke. Halbmesser nach Laenge t entlang der Achse.
AXIS = C(0, 0, 1)
BASE = C(0, 0, 0.040)
HEAD_LEN = 0.11
HEAD_T = [0.0, 0.02, 0.05, 0.08, 0.10, 0.11]
HEAD_R = [0.0115, 0.0145, 0.0180, 0.0205, 0.0195, 0.0140]
# Eisenbaender: Mitte t, halbe Laenge
BANDS = [(0.030, 0.006), (0.064, 0.007), (0.095, 0.006)]


def head_radius(t):
    return float(np.interp(t, HEAD_T, HEAD_R))


def build_club():
    part = Part("club", (0, 0, 0), UV)
    # Griff mit Lederwicklung, Knauf am Ende (8 Seiten), Holzkopf.
    # Griff und Knauf stecken ganz in der geschlossenen Manschette (Issue #637): Der Knauf reichte bis z = -0,074 und
    # schaute im vollen Schlag unten links aus dem Aermel; jetzt endet er bei -0,056 (Datei: -0,101).
    gz = [-0.040, -0.020, 0.0, 0.050]
    gr = [0.0105, 0.0098, 0.0102, 0.0112]
    part.loft(tube([C(0, 0, z) for z in gz], [(r, r, r) for r in gr], 8), "grip")
    kz = [-0.056, -0.050, -0.044, -0.038]
    kr = [0.0085, 0.0138, 0.0140, 0.0105]
    part.loft(tube([C(0, 0, z) for z in kz], [(r, r, r) for r in kr], 8), "wood")
    centres = [BASE + AXIS * t for t in HEAD_T]
    part.loft(tube(centres, [(r, r, r) for r in HEAD_R], 8, ref_up=V(0, 1, 0)), "wood")
    # Alle Teile sind geschlossene Koerper (Kappen an beiden Enden), sie ueberlappen sich: rundum geschlossen (Regel 21).
    # Drei dunkle Eisenbaender, je mit leichtem Wulst in der Mitte.
    for t0, half in BANDS:
        ts = [t0 - half, t0, t0 + half]
        rs = [head_radius(t) + d for t, d in zip(ts, (0.0022, 0.0034, 0.0022))]
        part.loft(tube([BASE + AXIS * t for t in ts], [(r, r, r) for r in rs], 8, ref_up=V(0, 1, 0)), "iron")
    return part


def build_hand_parts():
    parts = _arm.build_hand_parts("blade", UV)       # gleicher Aermel wie bei der Klinge
    for p in parts:
        p.parent = "club"
    return parts


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    os.makedirs(OUT_DIR, exist_ok=True)
    tex = paint_atlas(RECTS, PAINTERS, TEX, colors=32, seed=11)
    image = save_image("club", tex, os.path.join(OUT_DIR, "club.png"))
    coll = bpy.data.collections.new("Keule")
    bpy.context.scene.collection.children.link(coll)
    mat = export_material("club", image)
    club = build_club().build(coll, mat, smooth=False)
    hands = [part.build(coll, mat, smooth=True) for part in build_hand_parts()]
    pose([club] + hands, {}, root=Matrix.Scale(SCALE, 4))
    bake_scale([club] + hands)
    export_glb([club], os.path.join(OUT_DIR, "club.glb"))
    export_glb(hands, os.path.join(OUT_DIR, "club_hand.glb"))
    report_parts("club", [club] + hands)


if __name__ == "__main__":
    main()
