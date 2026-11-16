"""Gorm, the Chained - drei Gesichter zur Auswahl (Probestueck 9, Kopf-Runde) fuer Silver & Ash.

Urteil des Menschen: "Der Koerper ist ok, das Gesicht soll nochmal ueberarbeitet werden." Der Koerper kommt
unveraendert aus gorm.py; hier werden nur Kopf, Unterkiefer und die Kopftextur ersetzt - ohne Hauer und ohne
spitze Ohren:
    a) mensch - ein entstellter, vernarbter Mensch: schwerer Schaedel, eingefallene Augen, zugenaehter Mund
    b) eisen  - Gefangener in Eisen: angeschmiedeter Helm mit Sehschlitzen und Gitter-Maulkorb, durch den man
                die Zaehne sieht, Bolzen an den Schlaefen, Kette vom Kinnring zum Halsring
    c) sack   - Vorschlag des Designers: ein grober Kerkersack ueber dem Kopf, am Hals mit dem Eisenring
                zugezogen, gerissene Augenloecher, unten durchnaesst; hell, also als Ziel gut zu sehen

Aufruf (aus dem Repository-Wurzelverzeichnis):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/gorm/gorm_koepfe.py
"""

import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gorm as g  # noqa: E402
import psx  # noqa: E402
import bpy  # noqa: E402
from bpy_extras.object_utils import world_to_camera_view  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402
from psx import (V, G, Part, tube, vnoise, blob, mix, paint_atlas, save_image, pass_material,  # noqa: E402
                 write_light, setup_render, camera, aim, render_passes, compose, save_png, side_by_side)

OUT_DIR = os.path.dirname(os.path.abspath(__file__))
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


def project(cam, point):
    bpy.context.view_layer.update()
    scene = bpy.context.scene
    co = world_to_camera_view(scene, cam, point)
    return int(co.x * scene.render.resolution_x), int(co.y * scene.render.resolution_y)


def crop(arr, centre, w, h, f):
    cx, cy = centre
    x0 = int(np.clip(cx - w // 2, 0, arr.shape[1] - w))
    y0 = int(np.clip(cy - h // 2, 0, arr.shape[0] - h))
    c = arr[y0:y0 + h, x0:x0 + w]
    return np.repeat(np.repeat(c, f, axis=0), f, axis=1)


def render_variant(name, build_head_fn, head_painter, jaw_painter):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    g.GLOW_MAT.clear()
    psx.MATS.clear()                       # Verweise auf Materialien der vorigen Szene sind nach dem Zuruecksetzen ungueltig
    pa_paint = dict(g.PAINT_A, head=head_painter, jaw=jaw_painter)
    pa2_paint = dict(pa_paint, head=lambda U, V_: head_painter(U, V_, True),
                     torso=lambda U, V_: g.paint_torso(U, V_, True))
    img_a = save_image(f"a_{name}", paint_atlas(g.RECTS_A, pa_paint, g.TEX, colors=32, seed=2),
                       os.path.join(OUT_DIR, f"kopf_{name}_textur_rumpf.png"))
    img_a2 = save_image(f"a2_{name}", paint_atlas(g.RECTS_A, pa2_paint, g.TEX, colors=32, seed=2),
                        os.path.join(OUT_DIR, f"kopf_{name}_textur_rumpf_phase2.png"))
    img_b = save_image("b", paint_atlas(g.RECTS_B, g.PAINT_B, g.TEX, colors=32, seed=3),
                       os.path.join(os.environ.get("TEMP", "."), "gorm_b.png"))
    coll = bpy.data.collections.new("Gorm")
    bpy.context.scene.collection.children.link(coll)
    pa, pa2, pb = pass_material("pa", img_a), pass_material("pa2", img_a2), pass_material("pb", img_b)
    parts_a = [g.build_torso()] + build_head_fn()
    parts_b = g.build_arm(1) + g.build_arm(-1) + g.build_leg(1) + g.build_leg(-1) + [g.build_club()]
    model = [p.build(coll, pa) for p in parts_a] + [p.build(coll, pb) for p in parts_b]
    for o in model:
        if o.name == "club":
            o["smooth"] = False
    link = g.build_link().build(coll, pb, smooth=False)
    hook = g.build_hook().build(coll, pb, smooth=False)
    link.hide_render = hook.hide_render = True
    heads = [o for o in model if o.name in ("head", "jaw")]
    tris = sum(len(o.data.polygons) for o in heads)
    chain_coll = bpy.data.collections.new("Kette")
    bpy.context.scene.collection.children.link(chain_coll)
    cam = camera()

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
        extra = g.chain_objects(link, (hand, hand + Vector((0, 0, -0.9))), chain_coll, hook)
        if name == "eisen":                                          # Kette vom Kinnring zum Halsring
            chin = mats["head"] @ (G(V(0, 2.6, 0.86)) - G(g.NECK))
            ring = mats["torso"] @ (G(V(0, 2.5, 0.7)) - G(g.HIP))
            extra += g.chain_objects(link, (chin, ring), chain_coll)
        if not phase2:
            collar = mats["torso"] @ (G(V(0, 2.42, -0.12)) - G(g.HIP))
            back = mats["torso"].to_3x3() @ G(V(0, -0.8, -0.35)).normalized()
            extra += g.chain_objects(link, (collar, collar + back * 0.9), chain_coll)
        for o in model + extra:
            write_light(o)
        return extra, mats

    out = {}
    # Kopf gross: von vorn und von schraeg unten (ohne Nebel).
    setup_render(480, 480)
    cam.data.angle_y = math.radians(30)
    extra, mats = setup("ruhe", Matrix(), False)
    centre = G(V(0, 2.84, 0.62))
    for label, loc in (("vorn", V(0, 2.84, 2.6)), ("unten", V(0.8, 1.6, 2.4))):
        aim(cam, G(loc), centre)
        img = compose(render_passes(f"{name}_{label}", visible=model + extra), "ansicht")
        save_png(img, os.path.join(OUT_DIR, f"kopf_{name}_{label}.png"))

    # Im Bossraum: Ruhe und Phase 2 aus 4 m und 10 m.
    room = g.hall(g.level_mats())
    setup_render(854, 480)
    cam.data.angle_y = math.radians(70)
    at = V(0, 0, -3.5)
    for pose, phase2, tag in (("ruhe", False, "ruhe"), ("phase2_ruhe", True, "phase2")):
        for d, look in ((4, 2.2), (10, 1.7)):
            eye = V(0, 1.6, at.z + d)
            root = Matrix.Translation(G(at))
            extra, mats = setup(pose, root, phase2)
            aim(cam, G(eye), G(V(0, look, at.z)))
            img = compose(render_passes(f"{name}_{tag}_{d}", visible=model + extra + room), "spiel")
            save_png(img, os.path.join(OUT_DIR, f"kopf_{name}_{tag}_{d}m.png"))
            head_px = project(cam, mats["head"] @ (G(V(0, 2.84, 0.62)) - G(g.NECK)))
            body_px = project(cam, G(at + V(0, 1.6, 0)))
            out[(tag, d)] = (img, head_px, body_px)
    print("Kopf-Dreiecke", name, tris)
    return out, tris


def main():
    results = {}
    tris = {}
    for name, build_head_fn, head_painter, jaw_painter in VARIANTS:
        results[name], tris[name] = render_variant(name, build_head_fn, head_painter, jaw_painter)
    # Alle drei Koepfe nebeneinander: aus 4 m (Kopf vergroessert) und aus 10 m (ganzer Gorm vergroessert).
    for tag in ("ruhe", "phase2"):
        heads4 = [crop(results[n][(tag, 4)][0], results[n][(tag, 4)][1], 160, 120, 3) for n, *_ in VARIANTS]
        save_png(side_by_side(heads4), os.path.join(OUT_DIR, f"vergleich_koepfe_4m_{tag}.png"))
        whole10 = [crop(results[n][(tag, 10)][0], results[n][(tag, 10)][2], 180, 170, 3) for n, *_ in VARIANTS]
        save_png(side_by_side(whole10), os.path.join(OUT_DIR, f"vergleich_koepfe_10m_{tag}.png"))
    print("Dreiecke Kopf und Kiefer:", tris)


main()
