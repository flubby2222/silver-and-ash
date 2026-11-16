"""Gorm, the Chained - Endfassung (Probestueck 9) fuer Silver & Ash.

Der Mensch hat die Eisenmaske gewaehlt. Gegenueber gorm.py / gorm_koepfe.py:
  - Kopf: die Eisenmaske (Gitter mit Zaehnen dahinter, Stachelband, Ring am Kinn, Kette zum Halsring)
  - Kette: die Waffe, jetzt schwer und hell - Glieder 30 x 19 cm statt 16 x 10 cm, blankes Eisen mit Rost nur in
    den Vertiefungen, dreimal um den linken Unterarm gewickelt, in Ruhe bis zum Boden hängend und ein Stueck
    schleifend, beim Schwung bestimmt sie die Silhouette; der Haken ist 1,8-mal so gross
  - Keule: hat im Spiel eine Aufgabe (Hammerschlag, "Keule steckt fest"), bleibt also, tritt aber zurueck:
    dunkleres, aelteres Holz, kein helles Eisen

Aufruf (aus dem Repository-Wurzelverzeichnis):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/gorm/gorm_final.py
"""

import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gorm as g  # noqa: E402
import gorm_koepfe as k  # noqa: E402
import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402
from psx import (V, G, Part, tube, vnoise, blob, mix, paint_atlas, save_image, pass_material,  # noqa: E402
                 write_light, setup_render, camera, aim, render_passes, compose, save_png, side_by_side,
                 export_glb, report_parts, load_png, zoom)

OUT_DIR = os.path.dirname(os.path.abspath(__file__))
EXPORT_ONLY = "--uebergabe" in sys.argv

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
    export_glb(model + [link, hook], os.path.join(directory, prefix + "gorm.glb" if False else "gorm.glb"))
    report_parts("gorm", model + [link, hook])


# --- Bilder -------------------------------------------------------------------------------------------------------

def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    TMP = os.environ.get("TEMP", ".")
    body, body2, limbs = atlases()
    img_a = save_image("a", body, os.path.join(OUT_DIR, "final_textur_rumpf.png"))
    img_a2 = save_image("a2", body2, os.path.join(OUT_DIR, "final_textur_rumpf_phase2.png"))
    img_b = save_image("b", limbs, os.path.join(OUT_DIR, "final_textur_glieder.png"))
    glow = np.clip(limbs * 0.45 + g.WARNING * 0.55, 0, 255)
    img_glow = save_image("glow", glow, os.path.join(TMP, "gorm_glow2.png"))
    pa, pa2, pb = pass_material("pa", img_a), pass_material("pa2", img_a2), pass_material("pb", img_b)
    g.GLOW_MAT.append(pass_material("pglow", img_glow))
    coll, model, link, hook = build_model(pa, pb)
    for o in model:
        o.data.materials[0] = pa if o.name in ("torso", "head", "jaw") else pb
    link.hide_render = hook.hide_render = True
    print("Dreiecke:", sum(len(o.data.polygons) for o in model + [link, hook]),
          {o.name: len(o.data.polygons) for o in model + [link, hook]})
    cam = camera()
    chain_coll = bpy.data.collections.new("Kette")
    bpy.context.scene.collection.children.link(chain_coll)

    def setup(pose, root, phase2):
        mats = g.pose_gorm(model, pose, root)
        for o in model:
            if o.name in ("torso", "head", "jaw"):
                o.data.materials[0] = pa2 if phase2 else pa
        for o in list(chain_coll.objects):
            bpy.data.objects.remove(o)
        fa = next(o for o in model if o.name == "forearm_nx")
        wr = V(-g.WRIST.x, g.WRIST.y - g.SHOULDER_DROP, g.WRIST.z)
        el = V(-g.ELBOW.x, g.ELBOW.y - g.SHOULDER_DROP, g.ELBOW.z)
        hand = mats["forearm_nx"] @ (G(wr + (wr - el).normalized() * 0.22) - G(Vector(list(fa["pivot"]))))
        r3 = root.to_3x3()
        floor_z = root.translation.z + 0.1
        if pose in ("ruhe", "phase2_ruhe"):
            down = Vector((hand.x, hand.y, floor_z))
            drag = down + r3 @ G(V(-0.5, 0, 1.0)).normalized() * 1.1
            extra = chain_poly(link, hook, [hand, down, drag], chain_coll)
        elif pose == "schwung_an":
            out = r3 @ G(V(-0.8, 0, 0.6)).normalized()           # waagerecht ueber dem Kopf, vorn links hinaus
            extra = chain_poly(link, hook, [hand, hand + out * 5.0], chain_coll)
        elif pose == "schwung":
            target = root @ G(V(-5.6, 1.3, 5.6))
            extra = chain_poly(link, hook, [hand, target], chain_coll)
        elif pose == "haken_an":
            down = Vector((hand.x, hand.y, hand.z - 0.9))
            extra = chain_poly(link, hook, [hand, down], chain_coll, glow=True)
        else:
            extra = []
        # Kinnring-Kette der Maske zum Halsring.
        chin = mats["head"] @ (G(V(0, 2.6, 0.86)) - G(g.NECK))
        ring = mats["torso"] @ (G(V(0, 2.5, 0.7)) - G(g.HIP))
        extra += chain_poly(link, hook, [chin, ring], chain_coll, scale=0.5, with_hook=False)
        if not phase2:
            collar = mats["torso"] @ (G(V(0, 2.42, -0.12)) - G(g.HIP))
            back = mats["torso"].to_3x3() @ G(V(0, -0.8, -0.35)).normalized()
            extra += chain_poly(link, hook, [collar, collar + back * 0.9], chain_coll, scale=0.6, with_hook=False)
        for o in model + extra:
            write_light(o)
        return extra

    room = g.hall(g.level_mats())
    setup_render(854, 480)
    cam.data.angle_y = math.radians(70)
    at = V(0, 0, -3.5)
    shots = [("ruhe", 4, 2.2, False), ("ruhe", 10, 1.7, False), ("schwung_an", 6, 2.2, False),
             ("schwung", 6, 1.8, False), ("haken_an", 6, 2.0, False), ("phase2_ruhe", 4, 2.2, True)]
    for pose, d, look, phase2 in shots:
        eye = V(0, 1.6, at.z + d)
        extra = setup(pose, Matrix.Translation(G(at)), phase2)
        aim(cam, G(eye), G(V(0, look, at.z)))
        img = compose(render_passes(f"final_{pose}_{d}", visible=model + extra + room), "spiel")
        save_png(img, os.path.join(OUT_DIR, f"final_{d}m_{pose}.png"))
        if pose == "ruhe" and d == 10:
            new10 = img
    # Alt (erste Fassung mit Hauern) gegen neu aus 10 m, Ausschnitt dreifach.
    old10 = load_png(os.path.join(OUT_DIR, "spiel_10m_ruhe.png"))
    rows, cols = (150, 330), (337, 517)
    save_png(side_by_side([zoom(old10, rows, cols, 3), zoom(new10, rows, cols, 3)]),
             os.path.join(OUT_DIR, "final_vergleich_alt_neu_10m.png"))


if __name__ == "__main__":
    if EXPORT_ONLY:
        out = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
                           "assets", "models")
        export_to(out, "")
    else:
        main()
