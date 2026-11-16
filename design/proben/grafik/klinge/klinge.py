"""Silberklinge des Jaegers im Bild - Probestueck 6 fuer Silver & Ash.

Ein gerades, zweischneidiges Jagdschwert (kurz, breite Klinge mit Hohlkehle) aus Silber, in der linken Hand im
Handschuh mit Aermel; PS1-nah mit Textur 128 x 128. Lage und Bewegung wie der Platzhalter in WeaponViews.cs:
Hand bei (-0,11, -0,095, 0,19) im Kameraraum, Klinge 25 Grad nach oben, in Ruhe 20 Grad geneigt; Ausholen beim
Aufladen: Spitze 35 Grad nach links, Hand 3 cm links, 1,5 cm hoch, 4 cm zurueck; Schlag: Spitze 95 Grad nach rechts,
45 Grad geneigt, Hand 6 cm rechts und 4 cm hoch. Wie die Pistole 1,8-mal so gross gebaut wie die Platzhalter-Quader.

Aufruf (aus dem Repository-Wurzelverzeichnis):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/klinge/klinge.py
"""

import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "gemeinsam"))
import bpy  # noqa: E402
from mathutils import Matrix  # noqa: E402
from arm import C, build_arm, paint_glove, paint_sleeve  # noqa: E402
from psx import (V, G, Part, tube, rects_from_pixels, vnoise, blob, mix, paint_atlas, save_image,  # noqa: E402
                 export_material, stone_textures, build_room, pass_material, write_light, pose,
                 setup_render, camera, aim, render_passes, compose, save_png)

OUT_DIR = os.path.dirname(os.path.abspath(__file__))
TEX = 128

SILVER = np.array((206, 210, 222), float)
DARK_SILVER = np.array((132, 136, 150), float)   # wie der Platzhalter (Parierstange)
TARNISH = np.array((92, 92, 104), float)
LEATHER = np.array((70, 48, 34), float)          # Griffwicklung
WIRE = np.array((170, 172, 184), float)

RECTS = {
    "blade": (0, 64, 128, 128),
    "silver": (0, 48, 64, 64),
    "grip": (64, 48, 128, 64),
    "glove": (0, 0, 64, 48),
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
            "glove": paint_glove, "sleeve": paint_sleeve}


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
    # Linke Hand: Punkte fuer die rechte Seite angegeben, s = -1 spiegelt sie.
    build_arm(part, -1,
              grip=[(0, 0, -0.008), (0, 0, -0.024), (0, 0, -0.04), (0, 0, -0.056)],
              wrist=(0.01, -0.016, -0.08), elbow=(0.11, -0.16, -0.27),
              thumb=((0.012, 0.009, -0.04), (0.004, 0.013, -0.012)),
              index=((-0.012, -0.002, -0.014), (-0.006, -0.006, -0.002)),
              fist_ref=V(0, 1, 0))
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
    tex = paint_atlas(RECTS, PAINTERS, TEX, colors=32, seed=9)
    image = save_image("blade", tex, os.path.join(OUT_DIR, "klinge_textur.png"))
    coll = bpy.data.collections.new("Klinge")
    bpy.context.scene.collection.children.link(coll)
    mat = export_material("blade", image)
    sword = build_sword().build(coll, mat, smooth=False)
    hand = build_hand().build(coll, mat, smooth=True)
    model = [sword, hand]
    print("Dreiecke: Klinge", len(sword.data.polygons), "Hand und Aermel", len(hand.data.polygons))

    pose([sword], {})
    bpy.ops.object.select_all(action="DESELECT")
    sword.select_set(True)
    bpy.ops.export_scene.gltf(filepath=os.path.join(OUT_DIR, "klinge.glb"), export_format="GLB",
                              use_selection=True, export_yup=True, export_normals=True,
                              export_vertex_color="NONE", export_materials="EXPORT", export_texcoords=True)

    pmat = pass_material("blade_pass", image)
    for o in model:
        o.data.materials[0] = pmat
    cam = camera()

    # Freie Ansichten (Klinge mit Hand am Ursprung).
    setup_render(640, 640)
    cam.data.angle_y = math.radians(30)
    pose(model, {})
    for o in model:
        write_light(o)
    centre = G(C(0, 0.02, 0.03))
    for name, loc in {"ansicht_links": C(-0.55, 0.05, 0.03), "ansicht_hinten_oben": C(0.08, 0.3, -0.42),
                      "ansicht_oben": C(0.0, 0.6, 0.05)}.items():
        aim(cam, G(loc), centre)
        save_png(compose(render_passes(name, visible=model), "ansicht"), os.path.join(OUT_DIR, name + ".png"))

    # Ich-Sicht im Gang.
    stone = stone_textures(os.path.join(os.path.dirname(OUT_DIR), "gemeinsam"))
    room = build_room(stone)
    setup_render(854, 480)
    cam.data.angle_y = math.radians(70)
    aim(cam, G(EYE), G(EYE + V(0, 0, 1)))
    for state, t, wind in (("ruhe", 0.0, 0.0), ("ausholen", 0.0, 1.0), ("schlag", 1.0, 0.0)):
        pose(model, {}, root=hand_matrix(t, wind))
        for o in model:
            write_light(o)
        passes = render_passes(f"ich_{state}", visible=model + room)
        for mode in ("nebel", "dither"):
            save_png(compose(passes, mode), os.path.join(OUT_DIR, f"ich_sicht_{state}_{mode}.png"))


main()
