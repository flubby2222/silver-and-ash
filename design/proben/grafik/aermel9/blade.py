"""Silberklinge des Jaegers mit Hand

Erzeuger fuer das Spiel (Uebergabe aus dem Probestueck klinge, Branch claude/design-probe-ghul-45a8c4).
Erzeugt nur Modell und Textur nach assets/models/, keine Probebilder; die Bilder entstehen im Probestueck-Skript.

Aufruf (aus dem Repository-Wurzelverzeichnis):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/models/blade.py

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
from arm import C, build_sleeve4, paint_sleeve4 as paint_sleeve, paint_cuff3 as paint_cuff  # noqa: E402
from psx import load_png, side_by_side, zoom  # noqa: E402
from psx import bake_scale, export_glb, report_parts  # noqa: E402
from psx import (V, G, Part, tube, rects_from_pixels, vnoise, blob, mix, paint_atlas, save_image,  # noqa: E402
                 export_material, stone_textures, build_room, pass_material, write_light, pose,
                 setup_render, camera, aim, render_passes, compose, save_png)

# Modelle und Texturen landen in assets/models (vom Skript in tools/models aus zwei Ebenen hoch).
import arm as _arm  # noqa: E402
OUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "modelle_" + _arm.DESIGN)
TEX = 128

SILVER = np.array((206, 210, 222), float)
DARK_SILVER = np.array((132, 136, 150), float)   # wie der Platzhalter (Parierstange)
TARNISH = np.array((92, 92, 104), float)
LEATHER = np.array((70, 48, 34), float)          # Griffwicklung
WIRE = np.array((170, 172, 184), float)

RECTS = {
    "glove": (0, 16, 64, 48),
    "blade": (0, 64, 128, 128),
    "silver": (0, 48, 64, 64),
    "grip": (64, 48, 128, 64),
    "cuff": (0, 0, 64, 16),
    "sleeve": (64, 0, 128, 48),
}
UV = rects_from_pixels(RECTS, TEX)

BLADE_HAND = (-0.11, -0.095, 0.19)               # Kameraraum, aus WeaponViews.cs
SCALE = 1.8
EYE = V(0, 1.6, -6.0)


def paint_blade(U, V_):
    """u rundherum (Ecken: 0 unten/Ruecken, 0,25 Schneide, 0,5 oben, 0,75 Schneide), v vom Heft zur Spitze."""
    h, w = U.shape
    col = np.broadcast_to(SILVER, (h, w, 3)).copy()
    col *= (0.88 + 0.16 * vnoise(h, w, 30, 4, 81))[..., None]
    edge = np.minimum(np.abs(U - 0.25), np.abs(U - 0.75))
    col = mix(col, SILVER * 1.12, (edge < 0.03).astype(float))                    # geschliffene Schneiden hell
    mid = np.minimum.reduce([np.abs(U - c) for c in (0.125, 0.375, 0.625, 0.875)])
    fuller = (mid < 0.035) & (V_ > 0.05) & (V_ < 0.7)
    col = mix(col, TARNISH, fuller.astype(float) * 0.8)                            # Hohlkehle dunkel
    etch = (V_ < 0.28) & (mid > 0.04) & (edge > 0.04)
    vine = np.abs(np.sin(V_ * 60 + np.sin(U * 50) * 2)) < 0.25
    col = mix(col, TARNISH, (etch & vine).astype(float) * 0.7)                     # Aetzung am Heft
    col = mix(col, TARNISH * 0.8, np.clip((vnoise(h, w, 8, 8, 82) - 0.7) * 2, 0, 0.4))   # Flecken
    return col


def paint_silver(U, V_):
    h, w = U.shape
    col = np.broadcast_to(DARK_SILVER, (h, w, 3)).copy()
    col = mix(col, SILVER, np.clip(np.abs(np.sin(U * 9 + V_ * 4)), 0, 1) * 0.5)
    col *= (0.85 + 0.25 * vnoise(h, w, 6, 6, 83))[..., None]
    return col


def paint_grip(U, V_):
    h, w = U.shape
    col = np.broadcast_to(LEATHER, (h, w, 3)).copy()
    wrap = ((U * 2 + V_ * 7) % 1.0)
    col *= (0.75 + 0.4 * (wrap > 0.15))[..., None]                                 # schraege Wicklung
    col = mix(col, WIRE, (wrap < 0.06).astype(float) * 0.9)                        # Silberdraht dazwischen
    col *= (0.85 + 0.25 * vnoise(h, w, 6, 10, 84))[..., None]
    return col


PAINTERS = {"blade": paint_blade, "silver": paint_silver, "grip": paint_grip,
            "cuff": paint_cuff, "sleeve": paint_sleeve}
PAINTERS.update(_arm.design_painters(_arm.DESIGN))


def build_sword():
    part = Part("sword", (0, 0, 0), UV)
    # Griff mit Lederwicklung, Knauf, Parierstange mit eingerollten Enden.
    part.loft(tube([C(0, 0, -0.064), C(0, 0, -0.032), C(0, 0, 0.0)],
                   [(0.0095, 0.0095, 0.0095), (0.0105, 0.0105, 0.0105), (0.0095, 0.0095, 0.0095)], 8), "grip")
    part.loft(tube([C(0, 0, -0.076), C(0, 0, -0.072), C(0, 0, -0.064)],
                   [(0.009, 0.009, 0.009), (0.015, 0.015, 0.015), (0.011, 0.011, 0.011)], 8), "silver")
    part.box((-0.028, -0.005, -0.004), (0.028, 0.006, 0.008), "silver")
    for s in (-1, 1):
        part.spike((s * 0.026, 0.0, 0.002), (s * 0.036, -0.012, 0.012), 0.005, "silver")
    # Klinge: 25 Grad nach oben, Rautenquerschnitt, zur Spitze schmaler.
    up = math.radians(25)
    axis = C(0, math.sin(up), math.cos(up))
    base = C(0, 0.003, 0.009)
    pts = [base + axis * t for t in (0.0, 0.05, 0.11, 0.15)]
    part.loft(tube(pts, [(0.0105, 0.0028, 0.0028), (0.0098, 0.0026, 0.0026), (0.0082, 0.0022, 0.0022),
                         (0.0045, 0.0015, 0.0015)], 4, ref_up=V(0, 1, 0)), "blade")
    return part


def build_hand():
    part = Part("arm", (0, 0, 0), UV, parent="sword")
    _arm.build_design(part, "blade")
    return part


def hand_matrix(t=0.0, wind=0.0):
    """Lage der Hand nach WeaponViews.DrawBlade: t = Schlag (0..1 am Hoehepunkt), wind = Ausholen (0..1)."""
    tilt = 20 + t * 25
    sweep = t * 95 - 35 * wind
    off = C(-0.03 * wind + t * 0.06, 0.015 * wind + t * 0.04, -0.04 * wind)
    roll = Matrix.Rotation(math.radians(-tilt), 4, "Y")       # Oberseite zur Bildmitte (rechts = -x)
    yaw = Matrix.Rotation(math.radians(-sweep), 4, "Z")       # Spitze nach rechts
    return Matrix.Translation(G(EYE + C(*BLADE_HAND) + off)) @ yaw @ roll @ Matrix.Scale(SCALE, 4)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    os.makedirs(OUT_DIR, exist_ok=True)
    tex = paint_atlas(RECTS, PAINTERS, TEX, colors=32, seed=9)
    image = save_image("blade", tex, os.path.join(OUT_DIR, "blade.png"))
    coll = bpy.data.collections.new("Klinge")
    bpy.context.scene.collection.children.link(coll)
    mat = export_material("blade", image)
    sword = build_sword().build(coll, mat, smooth=False)
    hand = build_hand().build(coll, mat, smooth=True)
    pose([sword, hand], {}, root=Matrix.Scale(SCALE, 4))
    bake_scale([sword, hand])
    export_glb([sword], os.path.join(OUT_DIR, "blade.glb"))
    export_glb([hand], os.path.join(OUT_DIR, "blade_hand.glb"))
    report_parts("blade", [sword, hand])


main()
