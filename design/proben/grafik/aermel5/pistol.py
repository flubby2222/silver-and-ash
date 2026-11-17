"""Pistole des Jaegers (Steinschloss, Silberbeschlaege) mit Hand

Erzeuger fuer das Spiel (Uebergabe aus dem Probestueck pistole (Fassung 2), Branch claude/design-probe-ghul-45a8c4).
Erzeugt nur Modell und Textur nach assets/models/, keine Probebilder; die Bilder entstehen im Probestueck-Skript.

Aufruf (aus dem Repository-Wurzelverzeichnis):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/models/pistol.py

Herkunft: alles per Skript, nichts heruntergeladen. Teile, Drehpunkte und Haltungen: design/uebergabe/README.md.
"""


import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "..", "tools", "models"))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from mathutils import Matrix  # noqa: E402
from arm import build_sleeve4, paint_sleeve4 as paint_sleeve, paint_cuff3 as paint_cuff  # noqa: E402
from psx import bake_scale, export_glb, report_parts  # noqa: E402
from psx import (V, G, Part, tube, rects_from_pixels, vnoise, blob, mix, paint_atlas, save_image,  # noqa: E402
                 export_material, stone_textures, build_room, pass_material, write_light, pose, raise_fwd,
                 setup_render, camera, aim, render_passes, compose, save_png, load_png, side_by_side, zoom)

# Modelle und Texturen landen in assets/models (vom Skript in tools/models aus zwei Ebenen hoch).
import arm as _arm  # noqa: E402
_arm.CUFF_V = 0.29
OUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "modelle_" + _arm.DESIGN)
TEX = 128

WOOD = np.array((84, 56, 38), float)          # Nussbaum, wie der Platzhalter
IRON = np.array((58, 56, 64), float)          # gebraeuntes Eisen, wie der Platzhalter
SILVER = np.array((206, 210, 222), float)     # Silber des Jaegers: hell, kuehl, fast weiss
TARNISH = np.array((92, 92, 104), float)      # angelaufenes Silber in Vertiefungen
STEEL = np.array((112, 114, 124), float)      # Hahn und Batterie
DARK = np.array((14, 12, 14), float)
LOCKSTEEL = np.array((58, 56, 64), float)   # Schloss: dunkles Eisen, zwischen Lauf und Schwarz (Fassung 4: heller)
FLINT = np.array((176, 164, 138), float)     # Feuerstein: heller als alles ringsum, damit er als Stein lesbar ist
FLASH = np.array((255, 232, 170), float)      # Muendungsfeuer wie im Platzhalter

RECTS = {
    "wood": (0, 64, 64, 128),
    "iron": (64, 96, 128, 128),
    "silver": (64, 64, 128, 96),
    "bore": (0, 48, 16, 64),
    "steel": (16, 48, 48, 64),
    "flash": (48, 48, 64, 64),
    "lock": (64, 48, 96, 64),
    "flint": (96, 48, 128, 64),
}
UV = rects_from_pixels(RECTS, TEX)

PISTOL_HAND = (0.095, -0.08, 0.18)            # Kameraraum, aus WeaponViews.cs
AIM_DISTANCE = 1000.0                         # Lauf parallel zur Blickachse: er laeuft im Bild auf die Bildmitte zu
CANT = 15.0                                   # Grad: Oberseite zum Auge hin gekippt
SCALE = 1.8                                   # echte Groesse einer Reiterpistole samt Hand (Modell ist halb so gross gebaut)
EYE = V(0, 1.6, -6.0)                          # Kamera im Gang, Blick nach +z


def C(xr, yu, zf):
    """Kameraraum (x rechts, y oben, z vorn) -> Spiel-Achsen relativ zur Kamera mit Blick nach +z.
    Im Spiel ist rechts = -x (WeaponViews.CameraToWorld); hier ohne Spiegelung gebaut."""
    return V(-xr, yu, zf)


# --- Textur -------------------------------------------------------------------------------------------------------

def paint_wood(U, V_):
    h, w = U.shape
    col = np.broadcast_to(WOOD, (h, w, 3)).copy()
    grain = vnoise(h, w, 60, 5, 1) * 0.6 + vnoise(h, w, 12, 3, 2) * 0.4          # Maserung entlang des Schafts
    col *= (0.8 + 0.42 * grain)[..., None]
    top = 1 - np.abs(np.cos(np.pi * U))
    col *= (0.8 + 0.3 * top)[..., None]                                            # abgegriffen oben heller
    col = mix(col, WOOD * 1.5, np.clip((vnoise(h, w, 8, 8, 3) - 0.7) * 3, 0, 0.5))
    rng = np.random.default_rng(4)
    for _ in range(14):                                                            # Kratzer
        u, v, a = rng.random(), rng.random(), rng.uniform(0, math.pi)
        t = np.linspace(-0.04, 0.04, 30)
        for du, dv in zip(t * math.cos(a), t * math.sin(a) * 2):
            col = mix(col, WOOD * 1.6, (blob(U, V_, u + du, v + dv, 0.006, 0.006) > 0.2).astype(float))
    # Silberdraht-Einlage: Ranke auf beiden Flanken.
    for cu in (0.25, 0.75):
        curve = cu + 0.05 * np.sin(V_ * 18)
        col = mix(col, SILVER * 0.9, ((np.abs(U - curve) < 0.008) & (V_ > 0.25) & (V_ < 0.75)).astype(float))
    return col


def paint_iron(U, V_):
    h, w = U.shape
    col = np.broadcast_to(IRON, (h, w, 3)).copy()
    col *= (0.8 + 0.4 * vnoise(h, w, 40, 16, 11))[..., None]                       # Narben, Rost
    col = mix(col, np.array((80, 58, 44.0)), np.clip((vnoise(h, w, 10, 6, 12) - 0.6) * 2.5, 0, 0.5))
    flat = ((U * 8) % 1.0)
    col *= (1 + 0.18 * (flat > 0.85))[..., None]                                   # Kanten der acht Flaechen
    top = np.abs(U - 0.5) < 0.07                                                   # Silberlinien oben
    col = mix(col, SILVER, (top & (np.abs(((U - 0.5) / 0.07)) > 0.6)).astype(float) * 0.9)
    col = mix(col, SILVER, ((V_ < 0.12) & (((V_ * 40) % 1.0) < 0.5)).astype(float) * 0.8)   # Ring am Pulversack
    return col


def paint_silver(U, V_):
    h, w = U.shape
    col = np.broadcast_to(SILVER, (h, w, 3)).copy()
    col = mix(col, TARNISH, np.clip(vnoise(h, w, 6, 6, 21) * 1.2 - 0.3, 0, 0.8))   # angelaufen
    col *= (0.85 + 0.25 * np.abs(np.sin(U * 9 + V_ * 3)))[..., None]              # Glanz
    col = mix(col, SILVER * 1.15, ((np.abs(V_ - 0.5 - 0.1 * np.sin(U * 12)) < 0.03)).astype(float) * 0.7)
    return col


def paint_steel(U, V_):
    h, w = U.shape
    col = np.broadcast_to(STEEL, (h, w, 3)).copy()
    col *= (0.75 + 0.45 * vnoise(h, w, 8, 8, 31))[..., None]
    return col


def paint_lock(U, V_):
    """Hahn, Pfanne, Batterie: dunkel gebraeuntes Eisen, nur Rauschen - keine Linien."""
    h, w = U.shape
    col = np.broadcast_to(LOCKSTEEL, (h, w, 3)).copy()
    return col * (0.8 + 0.4 * vnoise(h, w, 6, 6, 41))[..., None]


def paint_flint(U, V_):
    """Feuerstein: helles, graubeiges Gestein mit weichen Flecken. Absichtlich ohne Linien und Kanten."""
    h, w = U.shape
    col = np.broadcast_to(FLINT, (h, w, 3)).copy()
    return col * (0.82 + 0.3 * vnoise(h, w, 4, 4, 42))[..., None]


PAINTERS = {"lock": paint_lock, "flint": paint_flint, "wood": paint_wood, "iron": paint_iron, "silver": paint_silver, "steel": paint_steel,
            "bore": lambda U, V_: np.broadcast_to(DARK, U.shape + (3,)).copy(),
            "flash": lambda U, V_: mix(np.broadcast_to(FLASH, U.shape + (3,)).copy(), np.array((255, 255, 240.0)),
                                       blob(U, V_, 0.5, 0.5, 0.6, 0.6))}


# --- Geometrie ----------------------------------------------------------------------------------------------------

def build_stock():
    part = Part("stock", (0, 0, 0), UV)
    # Griff: vom Schloss nach unten hinten, Kolben verdickt (Vogelkopf).
    grip = [C(0, 0.004, 0.024), C(0, -0.012, 0.008), C(0, -0.032, -0.006), C(0, -0.052, -0.016), C(0, -0.066, -0.020)]
    part.loft(tube(grip, [(0.011, 0.012, 0.012), (0.010, 0.012, 0.012), (0.011, 0.014, 0.013),
                          (0.013, 0.017, 0.015), (0.0135, 0.018, 0.016)], 8, ref_up=V(0, 0, 1)), "wood")
    # Schaft unter dem Lauf bis kurz vor die Muendung.
    # Letzter Ring dicht am vorletzten: die Kappe bleibt stumpf statt spitz.
    fore = [C(0, -0.002, 0.012), C(0, -0.004, 0.06), C(0, -0.005, 0.123), C(0, -0.005, 0.125)]
    part.loft(tube(fore, [(0.0105, 0.010, 0.006), (0.009, 0.009, 0.005), (0.0075, 0.007, 0.004),
                          (0.0068, 0.0064, 0.0036)], 8), "wood")
    return part


def build_barrel():
    part = Part("barrel", (0, 0, 0), UV, parent="stock")
    pts = [C(0, 0.007, 0.02), C(0, 0.007, 0.09), C(0, 0.007, 0.152)]
    part.loft(tube(pts, [(0.0088, 0.0088, 0.0088), (0.0078, 0.0078, 0.0078), (0.0072, 0.0072, 0.0072)], 8),
              "iron", cap_end=False)
    # Muendung: Silberring und dunkle Bohrung.
    part.loft(tube([C(0, 0.007, 0.147), C(0, 0.007, 0.158)], [(0.0092, 0.0092, 0.0092)] * 2, 8), "silver",
              cap_end=False)
    part.loft(tube([C(0, 0.007, 0.158), C(0, 0.007, 0.1585)], [(0.0045, 0.0045, 0.0045)] * 2, 8), "bore")
    return part


def build_fittings():
    part = Part("fittings", (0, 0, 0), UV, parent="stock")
    # Kolbenkappe aus Silber mit kurzem Sporn.
    part.loft(tube([C(0, -0.063, -0.019), C(0, -0.072, -0.022)], [(0.0145, 0.019, 0.017), (0.012, 0.014, 0.013)], 8,
                   ref_up=V(0, 0, 1)), "silver")
    part.spike((C(0, -0.07, -0.03)), (C(0, -0.076, -0.044)), 0.006, "silver")
    # Seitenplatte links (zur Kamera hin) mit spitzen Enden.
    x = -0.0115
    # Fassung 2: kleiner, sie stand in der Ich-Sicht als heller Fluegel im Bild.
    pts = [C(x, -0.003, -0.004), C(x, 0.003, 0.0), C(x, 0.004, 0.024), C(x, -0.001, 0.032), C(x, -0.005, 0.016)]
    part.prism(pts, (0.0015, 0, 0), "silver")
    # Schlossplatte rechts.
    x = 0.0115
    pts = [C(x, -0.004, -0.002), C(x, 0.004, 0.0), C(x, 0.006, 0.026), C(x, -0.002, 0.032), C(x, -0.005, 0.012)]
    part.prism(pts, (-0.0015, 0, 0), "silver")
    # Abzugsbuegel aus Silber.
    guard = [C(0, -0.008, 0.036), C(0, -0.022, 0.028), C(0, -0.027, 0.014), C(0, -0.024, 0.0), C(0, -0.014, -0.006)]
    part.loft(tube(guard, [(0.0028, 0.0022, 0.0022)] * 5, 4, ref_up=V(1, 0, 0)), "silver")
    # Abzug.
    part.prism([C(-0.002, -0.006, 0.016), C(-0.002, -0.02, 0.012), C(-0.002, -0.017, 0.008), C(-0.002, -0.006, 0.01)],
               (0.004, 0, 0), "steel")
    # Ladestock unter dem Schaft, Silberspitze.
    part.loft(tube([C(0, -0.010, 0.04), C(0, -0.010, 0.042), C(0, -0.010, 0.148), C(0, -0.010, 0.15)],
                   [(0.0022, 0.0022, 0.0022)] * 4, 4), "wood")
    part.loft(tube([C(0, -0.010, 0.15), C(0, -0.010, 0.155), C(0, -0.010, 0.156)], [(0.003, 0.003, 0.003)] * 3, 4),
              "silver")
    # Griffplatte (Schwanzschraube) oben auf dem Griff hinter dem Lauf: Zeichnung fuer den Blick von hinten oben.
    part.box((-0.0045, 0.0125, -0.006), (0.0045, 0.0165, 0.022), "silver")
    # Pfanne und Batterie (Feuerstahl) oben rechts vor dem Hahn.
    part.box((-0.0125, 0.008, 0.016), (-0.004, 0.011, 0.03), "lock")
    part.prism([C(0.006, 0.011, 0.024), C(0.006, 0.011, 0.031), C(0.006, 0.025, 0.029), C(0.006, 0.023, 0.023)],
               (-0.0035, 0, 0), "lock")
    return part


HAMMER_PIVOT = C(0.007, 0.010, 0.004)


COCK_K = 0.66                                                                       # Fassung 4: ein Drittel kleiner


def build_cock():
    """Hahn (Fassung 4): wie Fassung 3, aber um ein Drittel kleiner um seinen Drehpunkt, mit kurzem Sporn und in
    hellerem Schlosseisen. Hals als Rohr, zwei Backen, Oberbacke, heller Feuerstein (eigenes Texturfeld)."""
    part = Part("cock", HAMMER_PIVOT, UV, parent="stock")
    k, x0 = COCK_K, 0.0075
    py, pz = 0.010, 0.004

    def P(y, z):
        return C(x0, py + (y - py) * k, pz + (z - pz) * k)

    neck = [P(0.010, 0.004), P(0.017, 0.002), P(0.025, 0.005), P(0.031, 0.012)]
    part.loft(tube(neck, [(0.0034 * k,) + (0.004 * k,) * 2, (0.0034 * k,) + (0.0042 * k,) * 2,
                          (0.0034 * k,) + (0.004 * k,) * 2, (0.0036 * k,) + (0.0042 * k,) * 2], 6,
                   ref_up=V(1, 0, 0)), "lock")
    # Backen
    for sx in (-1, 1):
        y0, y1, z0, z1 = py + (0.029 - py) * k, py + (0.0375 - py) * k, pz + (0.010 - pz) * k, pz + (0.022 - pz) * k
        part.box((-(x0 + sx * 0.0042 * k) - 0.0012 * k, y0, z0), (-(x0 + sx * 0.0042 * k) + 0.0012 * k, y1, z1), "lock")
    y0, y1, z0, z1 = py + (0.0375 - py) * k, py + (0.0395 - py) * k, pz + (0.012 - pz) * k, pz + (0.02 - pz) * k
    part.box((-(x0) - 0.0042 * k, y0, z0), (-(x0) + 0.0042 * k, y1, z1), "lock")      # Oberbacke
    part.spike(P(0.0335, 0.0105), P(0.036, 0.0045), 0.002, "lock")                    # kurzer Sporn nach hinten
    flint = [P(0.0305, 0.016), P(0.036, 0.020), P(0.0345, 0.031), P(0.0295, 0.0295)]
    part.prism(flint, (-0.0034 * k, 0, 0), "flint")
    part.prism(flint, (0.0034 * k, 0, 0), "flint")
    return part


def build_hand():
    part = Part("arm", (0, 0, 0), _arm.UV_ARM, parent="stock")
    _arm.build_design(part, "pistol")
    return part


def build_flash():
    part = Part("flash", (0, 0, 0), UV, parent="stock")
    m = C(0, 0.007, 0.162)
    for d in (C(0, 0, 0.06), C(0.02, 0.016, 0.03), C(-0.02, 0.016, 0.03), C(0.02, -0.016, 0.03),
              C(-0.02, -0.016, 0.03), C(0, 0.03, 0.02), C(0, -0.03, 0.02)):
        part.spike(tuple(m), tuple(m + d), 0.011, "flash")
    return part


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    os.makedirs(OUT_DIR, exist_ok=True)
    tex = paint_atlas(RECTS, PAINTERS, TEX, colors=32, seed=5)
    image = save_image("pistol", tex, os.path.join(OUT_DIR, "pistol.png"))
    coll = bpy.data.collections.new("Pistole")
    bpy.context.scene.collection.children.link(coll)
    mat = export_material("pistol", image)
    model = [build_stock().build(coll, mat, smooth=True), build_barrel().build(coll, mat, smooth=False),
             build_fittings().build(coll, mat, smooth=False), build_cock().build(coll, mat, smooth=False)]
    flash = build_flash().build(coll, mat, smooth=False)
    arm_tex = paint_atlas(_arm.ARM_RECTS, _arm.ARM_PAINTERS, 64, colors=32, seed=11)
    arm_image = save_image("pistol_hand", arm_tex, os.path.join(OUT_DIR, "pistol_hand.png"))
    arm_mat = export_material("pistol_hand", arm_image)
    hand = build_hand().build(coll, arm_mat, smooth=True)
    # In echter Groesse: die Skalierung der Ich-Sicht ist eingebacken (1 Einheit = 1 m).
    pose(model + [flash, hand], {}, root=Matrix.Scale(SCALE, 4))
    bake_scale(model + [flash, hand])
    export_glb(model + [flash], os.path.join(OUT_DIR, "pistol.glb"))
    export_glb([hand], os.path.join(OUT_DIR, "pistol_hand.glb"))
    report_parts("pistol", model + [flash, hand])


main()
