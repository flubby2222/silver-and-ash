"""Gorm, the Chained (Boss) fuer Silver & Ash.

Erzeuger fuer das Spiel (Uebergabe aus Probestueck 9, Endfassung mit Eisenmaske und schwerer Kette).
Erzeugt nur Modell und Texturen nach assets/models/, keine Probebilder.

Aufruf (aus dem Repository-Wurzelverzeichnis):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/models/gorm.py

Teile, Drehpunkte, Haltungen: design/uebergabe/README.md. Herkunft: alles per Skript, nichts heruntergeladen.
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gorm_base as g  # noqa: E402
import gorm_heads as k  # noqa: E402
import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402
from psx import (V, G, Part, tube, vnoise, blob, mix, paint_atlas, save_image, pass_material,  # noqa: E402
                 write_light, setup_render, camera, aim, render_passes, compose, save_png, side_by_side,
                 export_glb, report_parts)

OUT_DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
                       "assets", "models")

# --- Kette: hell, schwer ------------------------------------------------------------------------------------------

STEEL = np.array((158, 160, 170), float)        # blankes Eisen: das Hellste an Gorms Koerper ausser dem Kopf
LINK_L, LINK_W, LINK_T = 0.30, 0.19, 0.036
SPACING = 0.19
HOOK_SCALE = 1.8


def paint_chain_bright(U, V_):
    h, w = U.shape
    col = np.broadcast_to(STEEL, (h, w, 3)).copy()
    col *= (0.82 + 0.3 * vnoise(h, w, 10, 20, 90))[..., None]
    top = 1 - np.abs(np.cos(np.pi * U))
    col *= (0.75 + 0.4 * top)[..., None]                                      # Kanten oben blank
    rust = np.clip((vnoise(h, w, 5, 10, 91) - 0.62) * 3, 0, 0.7) * (1 - top)
    return mix(col, g.RUST, rust)                                              # Rost nur in den Vertiefungen


def paint_club_dark(U, V_):
    return g.paint_club(U, V_) * 0.62


g.PAINT_B["chain"] = paint_chain_bright
g.PAINT_B["club"] = paint_club_dark


def build_link():
    p = Part("chain_link", (0, 0, 0), g.UV_B)
    p.loft(g.link_rings(V(0, 0, 0), V(0, 1, 0), V(1, 0, 0), LINK_L, LINK_W, LINK_T, 8), "chain", closed=True)
    return p


def build_hook():
    """Haken (Oese oben), in der Datei normal gross; im Bild 1,8-fach."""
    p = Part("hook", (0, 0, 0), g.UV_B)
    p.loft(g.link_rings(V(0, -0.02, 0), V(0, 1, 0), V(1, 0, 0), 0.1, 0.08, 0.02, 8), "chain", closed=True)
    pts = [V(0, -0.06, 0), V(0, -0.25, 0), V(0, -0.4, 0.06), V(0, -0.42, 0.18), V(0, -0.33, 0.26)]
    p.loft(tube(pts, [(0.04, 0.04, 0.04), (0.045, 0.045, 0.045), (0.04, 0.04, 0.04), (0.032,) * 3, (0.006,) * 3],
                6, ref_up=V(1, 0, 0)), "chain")
    return p


def build_wrap(s=-1):
    """Kette dreimal um den linken Unterarm gewickelt: drei dicke Baender. Teil des Unterarms (bewegt sich mit)."""
    side = "nx"
    drop = g.SHOULDER_DROP
    el = V(s * g.ELBOW.x, g.ELBOW.y - drop, g.ELBOW.z)
    wr = V(s * g.WRIST.x, g.WRIST.y - drop, g.WRIST.z)
    d = (wr - el).normalized()
    p = Part(f"wrap_{side}", el, g.UV_B, parent=f"forearm_{side}")
    for t, r in ((0.22, 0.3), (0.42, 0.31), (0.62, 0.3)):
        c = el + (wr - el) * t
        p.loft(tube([c - d * 0.06, c, c + d * 0.06], [(r * 0.97,) * 3, (r,) * 3, (r * 0.97,) * 3], 12,
                    ref_up=V(0, 0, 1)), "chain")
    return p


def chain_poly(link, hook, pts, coll, spacing=SPACING, scale=1.0, with_hook=True, glow=False):
    """Glieder entlang eines Linienzugs (Blender-Punkte), jedes zweite um 90 Grad gedreht, am Ende der Haken."""
    objs = []
    idx = 0
    for a, b in zip(pts, pts[1:]):
        d = b - a
        count = max(1, int(d.length / (spacing * scale)))
        q = d.normalized().to_track_quat("Z", "X").to_matrix().to_4x4()
        for i in range(count):
            o = link.copy()
            o.data = link.data.copy()
            coll.objects.link(o)
            spin = Matrix.Rotation(math.pi / 2 * (idx % 2), 4, "Z")
            o.matrix_world = Matrix.Translation(a + d * ((i + 0.5) / count)) @ q @ spin @ Matrix.Scale(scale, 4)
            objs.append(o)
            idx += 1
    if with_hook:
        d = pts[-1] - pts[-2]
        q = d.normalized().to_track_quat("Z", "X").to_matrix().to_4x4()
        h = hook.copy()
        h.data = hook.data.copy()
        coll.objects.link(h)
        h.matrix_world = Matrix.Translation(pts[-1]) @ q @ Matrix.Rotation(math.pi, 4, "X") @ Matrix.Scale(HOOK_SCALE, 4)
        if glow:
            h.data.materials[0] = g.GLOW_MAT[0]
        objs.append(h)
    bpy.context.view_layer.update()
    return objs


# --- Modell -------------------------------------------------------------------------------------------------------

def build_model(pa, pb):
    coll = bpy.data.collections.new("Gorm")
    bpy.context.scene.collection.children.link(coll)
    parts_a = [g.build_torso()] + k.head_eisen()
    parts_b = g.build_arm(1) + g.build_arm(-1) + g.build_leg(1) + g.build_leg(-1) + [g.build_club(), build_wrap()]
    model = [p.build(coll, pa) for p in parts_a] + [p.build(coll, pb) for p in parts_b]
    for o in model:
        if o.name == "club":
            o["smooth"] = False
    link = build_link().build(coll, pb, smooth=False)
    hook = build_hook().build(coll, pb, smooth=False)
    return coll, model, link, hook


def atlases():
    pa_paint = dict(g.PAINT_A, head=k.paint_head_eisen, jaw=g.paint_jaw)
    pa2_paint = dict(pa_paint, head=lambda U, V_: k.paint_head_eisen(U, V_, True),
                     torso=lambda U, V_: g.paint_torso(U, V_, True))
    return (paint_atlas(g.RECTS_A, pa_paint, g.TEX, colors=32, seed=2),
            paint_atlas(g.RECTS_A, pa2_paint, g.TEX, colors=32, seed=2),
            paint_atlas(g.RECTS_B, g.PAINT_B, g.TEX, colors=32, seed=3))


def export_to(directory, prefix):
    """Modell und Texturen fuer das Spiel: gorm.glb, gorm_body.png, gorm_body_phase2.png, gorm_limbs.png."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    os.makedirs(directory, exist_ok=True)
    body, body2, limbs = atlases()
    img_a = save_image("gorm_body", body, os.path.join(directory, prefix + "body.png"))
    save_image("gorm_body_phase2", body2, os.path.join(directory, prefix + "body_phase2.png"))
    img_b = save_image("gorm_limbs", limbs, os.path.join(directory, prefix + "limbs.png"))
    coll, model, link, hook = build_model(g.export_material("gorm_body", img_a), g.export_material("gorm_limbs", img_b))
    g.pose_gorm(model, "neutral", Matrix())
    export_glb(model + [link, hook], os.path.join(directory, "gorm.glb"))
    report_parts("gorm", model + [link, hook])


export_to(OUT_DIR, "gorm_")
