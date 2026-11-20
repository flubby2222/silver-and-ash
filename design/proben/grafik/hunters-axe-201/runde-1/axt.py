"""Probestueck Hunter's Axe (Issue #201), Runde 1: drei Entwuerfe der Jaeger-Axt und das Vergleichsblatt.

Erzeugt aus diesem einen Skript (nichts heruntergeladen, nur Blender-Python und NumPy):
    axt-a-bartaxt.glb      A  einschneidige Bartaxt mit langem Stiel
    axt-b-doppelaxt.glb    B  doppelschneidige schwere Axt mit Pfahlspitze
    axt-c-pfahlaxt.glb     C  Axt mit Haken auf der Rueckseite und Pfahl am Stielende
    blatt.png              Vergleichsblatt (Seite, Dreiviertel, Ich-Sicht, Massstab neben der Keule, Silhouetten)

Aufbau wie die vorhandenen Waffen (tools/models/club.py): ein Knoten "axe" mit Drehpunkt im Griff (Ursprung), Stiel entlang
+z (vorn), Klingenebene = x-z-Ebene, die Schneide zeigt nach -x (zur Bildmitte; rechts im Bild ist -x), Massstab 1,8
eingebacken, Textur 128 x 128 ohne Filterung, nur Farben der Hauptpalette (Reihen Holz, Eisen, Messing/Gold), flach
schattiert. Der Aermel (Hand) wird NICHT modelliert; fuer die Ich-Sicht wird der vorhandene club_hand.glb dazugestellt.

Aufruf (aus dem Repository-Wurzelverzeichnis, TEMP/TMP auf einen Zwischenordner setzen):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/hunters-axe-201/runde-1/axt.py
"""

import json
import math
import os
import sys
import tempfile

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE)))))
sys.path.insert(0, os.path.join(ROOT, "tools", "models"))
import blf  # noqa: E402
import bmesh  # noqa: E402
import bpy  # noqa: E402
import imbuf  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402
from psx import (V, G, FOG_COLOR, Part, tube, rects_from_pixels, vnoise, save_image, export_material,  # noqa: E402
                 pose, bake_scale, export_glb, setup_render, camera, render_passes, compose, coverage, save_png,
                 pass_material, write_light, stone_textures, build_room, MATS, raise_fwd)

MODELS = os.path.join(ROOT, "assets", "models")
TEX = 128
SCALE = 1.8                      # wie alle Waffen: Dateimass = 1,8 x Spielmass
SIDE = -1.0                      # Schneide zeigt nach -x (Bildmitte)

# --- Palette (nur Hauptpalette, assets/palette/hauptpalette.json) ----------------------------------------------------

_PAL = json.load(open(os.path.join(ROOT, "assets", "palette", "hauptpalette.json"), encoding="utf-8"))
_ROWS = {r["name"]: np.array(r["farben"], float) for r in _PAL["akzent"]}
WOOD = _ROWS["Holz"][1:6]              # 52,27,34 ... 167,125,69
LEATHER = _ROWS["Holz"][1:4]           # dunkles Leder
STEEL = _ROWS["Eisen"][1:5]            # geschwaerzter Stahl
DARKSTEEL = _ROWS["Eisen"][0:3]
SILVER = _ROWS["Eisen"][5:8]           # Silber: helle Eisen-Stufen (Art Bible: Silber gehoert dem Jaeger)
GOLD = _ROWS["Messing/Gold"][2:7]      # Beschlaege der Stufe Royal (214, 170, 64 ist die Stufenfarbe)

RECTS = {
    "iron": (0, 0, 96, 48),
    "dark": (96, 0, 128, 48),
    "silver": (0, 48, 64, 72),
    "gold": (64, 48, 128, 72),
    "grip": (0, 72, 64, 96),
    "wood": (0, 96, 128, 128),
}
UV = rects_from_pixels(RECTS, TEX)


def step(ramp, field):
    """Feld 0..1 in harten Stufen auf die Rampe legen: nur Palettenfarben, kein Mischen."""
    i = np.clip((np.asarray(field) * len(ramp)).astype(int), 0, len(ramp) - 1)
    return ramp[i]


def paint_wood(U, V_):
    h, w = U.shape
    f = 0.15 + 0.7 * vnoise(h, w, 2, 24, 201)                       # Maserung laeuft entlang v
    f = np.where(vnoise(h, w, 9, 30, 202) < 0.14, 0.0, f)           # dunkle Fasern
    f = np.where(vnoise(h, w, 4, 6, 203) > 0.86, 0.95, f)           # abgegriffene Stellen
    return step(WOOD, f)


def paint_grip(U, V_):
    h, w = U.shape
    wrap = (U * 2.0 + V_ * 5) % 1.0                                 # schraege Lederwicklung
    f = np.where(wrap > 0.2, 0.15 + 0.3 * vnoise(h, w, 3, 8, 204), 0.95)
    return step(LEATHER, f)


def paint_silver(U, V_):
    h, w = U.shape
    f = 0.35 + 0.55 * vnoise(h, w, 4, 16, 205)
    f = np.where(np.abs(V_ - 0.5) < 0.08, 1.0, f)                   # helle Schneidkante
    return step(SILVER, f)


def paint_gold(U, V_):
    h, w = U.shape
    return step(GOLD, 0.25 + 0.7 * vnoise(h, w, 4, 10, 206))


def paint_dark(U, V_):
    h, w = U.shape
    return step(DARKSTEEL, 0.2 + 0.7 * vnoise(h, w, 5, 5, 207))


def lancet(E, Z, ce, cz, w, hgt, ring=0.22):
    """Maske eines Spitzbogens (Linse) als Ring; E, Z Plattenkoordinaten."""
    t = (Z - cz) / hgt
    inside = np.abs(t) < 1
    half = w * np.clip(1 - t * t, 0.0001, 1)
    f = np.abs(E - ce) / half
    return inside & (f < 1) & (f > 1 - ring)


def iron_painter(bounds, marks):
    """Plattenstahl: dunkle Stufen mit Rauschen; marks = Liste (ce, cz, w, h) von eingeritzten Spitzboegen."""
    e0, e1, z0, z1 = bounds

    def paint(U, V_):
        h, w = U.shape
        E, Z = e0 + U * (e1 - e0), z0 + V_ * (z1 - z0)
        f = 0.34 + 0.26 * vnoise(h, w, 3, 4, 208)
        f = np.where(vnoise(h, w, 7, 7, 209) < 0.08, 0.05, f)        # vereinzelte Pocken
        col = step(STEEL, f)
        for ce, cz, ww, hh in marks:
            col = np.where(lancet(E, Z, ce, cz, ww, hh)[..., None], step(SILVER, 0.3 + 0.2 * f), col)
            col = np.where(lancet(E, Z, ce, cz, ww * 0.3, hh * 0.45, 0.9)[..., None], step(GOLD, 0.7 + 0 * f), col)
        return col
    return paint


def make_atlas(painters):
    atlas = np.zeros((TEX, TEX, 3)) + DARKSTEEL[0]
    for name, (x0, y0, x1, y1) in RECTS.items():
        pw, ph = x1 - x0, y1 - y0
        U, V_ = np.meshgrid((np.arange(pw) + 0.5) / pw, (np.arange(ph) + 0.5) / ph)
        atlas[y0:y1, x0:x1] = painters[name](U, V_)
    return atlas


# --- Geometrie --------------------------------------------------------------------------------------------------------

def vert(part, x, y, z):
    return part.bm.verts.new(G(V(x, y, z) - part.pivot))


def poly_area(p):
    return 0.5 * sum(p[i][0] * p[(i + 1) % len(p)][1] - p[(i + 1) % len(p)][0] * p[i][1] for i in range(len(p)))


def inset(pts, ds):
    """Vieleck (gegen den Uhrzeigersinn) je Ecke um ds[i] nach innen versetzt (Gehrung)."""
    n = len(pts)
    out = []
    for i in range(n):
        p0, p1, p2 = np.array(pts[i - 1]), np.array(pts[i]), np.array(pts[(i + 1) % n])
        e1, e2 = p1 - p0, p2 - p1
        e1, e2 = e1 / np.linalg.norm(e1), e2 / np.linalg.norm(e2)
        n1, n2 = np.array((-e1[1], e1[0])), np.array((-e2[1], e2[0]))
        k = max(1 + float(n1 @ n2), 0.45)
        out.append(tuple(p1 + (n1 + n2) / k * ds[i]))
    return out


def _cross(a, b, c):
    return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])


def _segments_cross(a, b, c, d):
    return _cross(a, b, c) * _cross(a, b, d) < 0 and _cross(c, d, a) * _cross(c, d, b) < 0


def _inside(p, poly):
    x, y, c = p[0], p[1], False
    for i in range(len(poly)):
        (x1, y1), (x2, y2) = poly[i], poly[(i + 1) % len(poly)]
        if (y1 > y) != (y2 > y) and x < (x2 - x1) * (y - y1) / (y2 - y1) + x1:
            c = not c
    return c


def check_core(outline, core):
    """Der innere Rand muss einfach (ohne Selbstschnitt) und im Umriss liegen, sonst waere die Platte verdreht."""
    n = len(core)
    for i in range(n):
        for j in range(i + 2, n):
            if i == 0 and j == n - 1:
                continue
            assert not _segments_cross(core[i], core[(i + 1) % n], core[j], core[(j + 1) % n]), ("Kern kreuzt sich", i, j)
        assert _inside(core[i], outline), ("Kernpunkt ausserhalb", i)
    assert poly_area(core) > 0


def plate(part, pts, edge, te, tc, d, bounds):
    """Klingenplatte: Umriss pts (e, z) gegen den Uhrzeigersinn, edge[i] = Segment i->i+1 ist Schneide (Silber).
    Querschnitt: dicker Kern (+-tc), zur Aussenkante auf +-te verjuengt. Rundum geschlossen (Kappen, Fasen, Rand)."""
    assert poly_area(pts) > 0
    n = len(pts)
    core = inset(pts, d)
    check_core(pts, core)
    e0, e1, z0, z1 = bounds

    def uv(rect, e, z):
        u0, v0, u1, v1 = part.rects[rect]
        return (u0 + (u1 - u0) * (e - e0) / (e1 - e0), v0 + (v1 - v0) * (z - z0) / (z1 - z0))

    ot = [vert(part, SIDE * p[0], te[i], p[1]) for i, p in enumerate(pts)]
    ob = [vert(part, SIDE * p[0], -te[i], p[1]) for i, p in enumerate(pts)]
    ct = [vert(part, SIDE * p[0], tc, p[1]) for p in core]
    cb = [vert(part, SIDE * p[0], -tc, p[1]) for p in core]
    faces = []

    def quad(vs, ps, rect):
        faces.append(part._face(vs, [uv(rect, *p) for p in ps]))

    for i in range(n):
        j = (i + 1) % n
        rect = "silver" if edge[i] else "iron"
        quad((ot[i], ot[j], ct[j], ct[i]), (pts[i], pts[j], core[j], core[i]), rect)
        quad((ob[j], ob[i], cb[i], cb[j]), (pts[j], pts[i], core[i], core[j]), rect)
        quad((ot[i], ob[i], ob[j], ot[j]), (pts[i], pts[i], pts[j], pts[j]), "silver" if edge[i] else "dark")
    top = part._face(ct, [uv("iron", *p) for p in core])
    bot = part._face(list(reversed(cb)), [uv("iron", *p) for p in reversed(core)])
    bmesh.ops.triangulate(part.bm, faces=[top, bot], quad_method="BEAUTY", ngon_method="EAR_CLIP")
    bmesh.ops.recalc_face_normals(part.bm, faces=list(part.bm.faces))


def haft(part, z0, z1, r=0.0105, swell=0.0):
    """Stiel entlang z: 8 Seiten, geschlossen."""
    zs = [z0, z0 + 0.02, (z0 + z1) / 2, z1 - 0.01, z1]
    rs = [r * 0.82, r, r + swell, r, r * 0.9]
    part.loft(tube([V(0, 0, z) for z in zs], [(x, x, x) for x in rs], 8), "wood")


def grip(part, z0, z1):
    zs = [z0, (z0 + z1) / 2, z1]
    part.loft(tube([V(0, 0, z) for z in zs], [(0.0112, 0.0112, 0.0112)] * 3, 8), "grip")


def collar(part, zc, half, r, rect="gold"):
    zs = [zc - half, zc, zc + half]
    part.loft(tube([V(0, 0, z) for z in zs], [(r * 0.86, r * 0.86, r * 0.86), (r, r, r), (r * 0.86, r * 0.86, r * 0.86)], 8), rect)


def pommel(part, zc, kind="cone"):
    if kind == "ball":
        zs = [zc - 0.022, zc - 0.014, zc - 0.002, zc + 0.012]
        rs = [0.006, 0.0165, 0.0175, 0.011]
        part.loft(tube([V(0, 0, z) for z in zs], [(r, r, r) for r in rs], 8), "gold")
    else:
        zs = [zc - 0.026, zc - 0.012, zc + 0.004]
        rs = [0.002, 0.011, 0.0125]
        part.loft(tube([V(0, 0, z) for z in zs], [(r, r, r) for r in rs], 8), "gold")


def langets(part, ztop, length, width=0.0045):
    """Zwei goldene Schaftfedern an den Seiten des Stiels unter dem Kopf."""
    for s in (-1, 1):
        part.box((s * 0.0105 - 0.0016, -width, ztop - length), (s * 0.0105 + 0.0016, width, ztop), "gold")


def rivets(part, positions, y):
    for e, z in positions:
        for s in (-1, 1):
            part.loft(tube([V(SIDE * e, s * (y - 0.0012), z), V(SIDE * e, s * (y + 0.0030), z)], [(0.0042, 0.0042, 0.0042)] * 2, 6,
                           ref_up=V(0, 0, 1)), "silver")


def finial(part, z0, tip, rect="silver", half=0.0105):
    part.spike((0, 0, z0), (0, 0, tip), half, rect, sides=4)


def bounds_of(pts, pad=0.002):
    es, zs = [p[0] for p in pts], [p[1] for p in pts]
    return (min(es) - pad, max(es) + pad, min(zs) - pad, max(zs) + pad)


# --- Entwurf A: einschneidige Bartaxt mit langem Stiel ---------------------------------------------------------------------

A_PTS = [(-0.020, 0.214), (0.010, 0.205), (0.030, 0.198), (0.052, 0.184), (0.068, 0.164), (0.082, 0.196), (0.090, 0.232),
         (0.092, 0.270), (0.086, 0.306), (0.074, 0.336), (0.050, 0.324), (0.026, 0.318), (0.002, 0.320), (-0.016, 0.314),
         (-0.026, 0.300), (-0.026, 0.228)]
A_EDGE = [i in (4, 5, 6, 7, 8) for i in range(len(A_PTS))]
A_D = [0.010, 0.012, 0.012, 0.008, 0.008, 0.014, 0.020, 0.020, 0.018, 0.010, 0.010, 0.010, 0.010, 0.010, 0.010, 0.010]


def build_a():
    part = Part("axe", (0, 0, 0), UV)
    b = bounds_of(A_PTS)
    haft(part, -0.060, 0.342, swell=0.0008)
    grip(part, -0.052, 0.052)
    pommel(part, -0.060, "cone")
    plate(part, A_PTS, A_EDGE, [0.0035 if not e else 0.0016 for e in A_EDGE], 0.0120, A_D, b)
    collar(part, 0.205, 0.008, 0.0140)
    collar(part, 0.330, 0.007, 0.0135)
    langets(part, 0.204, 0.085)
    rivets(part, [(0.000, 0.300), (0.000, 0.232)], 0.0120)
    finial(part, 0.334, 0.368, "gold")
    return part, iron_painter(b, [(0.046, 0.256, 0.034, 0.062)])


# --- Entwurf B: doppelschneidige schwere Axt -----------------------------------------------------------------------------

_BR = [(0.014, 0.172), (0.036, 0.168), (0.060, 0.152), (0.082, 0.128), (0.096, 0.166), (0.103, 0.210), (0.100, 0.254),
       (0.088, 0.290), (0.080, 0.318), (0.058, 0.286), (0.034, 0.268), (0.014, 0.262)]
B_PTS = _BR + [(-e, z) for e, z in reversed(_BR)]
# Segmente: rechts 3..7 (Spitze unten bis Spitze oben), links gespiegelt (Index 12 + k)
B_EDGE = [False] * len(B_PTS)
for _i in range(3, 8):
    B_EDGE[_i] = True
for _i in range(12 + 4, 12 + 9):
    B_EDGE[_i] = True
B_D = ([0.012, 0.014, 0.012, 0.008, 0.016, 0.024, 0.022, 0.014, 0.008, 0.014, 0.016, 0.012]
       + [0.012, 0.016, 0.014, 0.008, 0.014, 0.022, 0.024, 0.016, 0.008, 0.012, 0.014, 0.012])


def build_b():
    part = Part("axe", (0, 0, 0), UV)
    b = bounds_of(B_PTS)
    haft(part, -0.060, 0.272, r=0.0112, swell=0.0014)
    grip(part, -0.052, 0.052)
    pommel(part, -0.060, "ball")
    te = [0.0045 if not B_EDGE[i] else 0.0018 for i in range(len(B_PTS))]
    plate(part, B_PTS, B_EDGE, te, 0.0150, B_D, b)
    collar(part, 0.158, 0.008, 0.0150)
    collar(part, 0.276, 0.007, 0.0150)
    langets(part, 0.160, 0.075, width=0.005)
    rivets(part, [(0.0, 0.246), (0.0, 0.196)], 0.0150)
    finial(part, 0.274, 0.358, "silver", half=0.0125)
    return part, iron_painter(b, [(0.062, 0.222, 0.030, 0.060), (-0.062, 0.222, 0.030, 0.060)])


# --- Entwurf C: Haken auf der Rueckseite und Pfahl am Stielende --------------------------------------------------------------

C_PTS = [(-0.020, 0.228), (0.012, 0.214), (0.044, 0.204), (0.070, 0.192), (0.082, 0.226), (0.087, 0.256), (0.082, 0.286),
         (0.068, 0.308), (0.042, 0.294), (0.014, 0.288), (-0.012, 0.284), (-0.022, 0.270), (-0.022, 0.244)]
C_EDGE = [i in (3, 4, 5, 6) for i in range(len(C_PTS))]
C_D = [0.010, 0.012, 0.012, 0.008, 0.016, 0.018, 0.016, 0.008, 0.010, 0.010, 0.010, 0.010, 0.010]
HOOK = [(-0.008, 0.254), (-0.036, 0.260), (-0.062, 0.254), (-0.082, 0.238), (-0.092, 0.216), (-0.092, 0.192)]
HOOK_W = [0.0125, 0.0108, 0.0090, 0.0068, 0.0042, 0.0010]
HOOK_T = [0.0100, 0.0088, 0.0072, 0.0052, 0.0034, 0.0010]


def build_c():
    part = Part("axe", (0, 0, 0), UV)
    b = bounds_of(C_PTS)
    haft(part, -0.060, 0.300, swell=0.0008)
    grip(part, -0.052, 0.052)
    pommel(part, -0.060, "cone")
    plate(part, C_PTS, C_EDGE, [0.0035 if not e else 0.0016 for e in C_EDGE], 0.0115, C_D, b)
    # Haken: gebogene Roehre, flach (Ebene x-z), spitz auslaufend
    part.loft(tube([V(SIDE * -e, 0, z) for e, z in HOOK], [(w, t, t) for w, t in zip(HOOK_W, HOOK_T)], 6, ref_up=V(0, 1, 0)), "silver")
    collar(part, 0.200, 0.008, 0.0140)
    collar(part, 0.296, 0.007, 0.0140)
    langets(part, 0.198, 0.080)
    rivets(part, [(0.020, 0.250)], 0.0115)
    # Pfahlspitze am Stielende (Silber)
    part.loft(tube([V(0, 0, z) for z in (0.296, 0.314, 0.352, 0.384, 0.398)],
                   [(r, r, r) for r in (0.0100, 0.0105, 0.0070, 0.0030, 0.0006)], 4), "silver")
    return part, iron_painter(b, [(0.046, 0.250, 0.026, 0.040)])


DESIGNS = [
    ("a", "A Bartaxt", "axt-a-bartaxt.glb", build_a),
    ("b", "B Doppelaxt", "axt-b-doppelaxt.glb", build_b),
    ("c", "C Pfahlaxt", "axt-c-pfahlaxt.glb", build_c),
]


def closed_report(part):
    """Vor dem Bauen: jede Kante des Netzes gehoert zu genau zwei Flaechen (rundum geschlossen)."""
    bm = part.bm
    open_e = sum(1 for e in bm.edges if len(e.link_faces) == 1)
    multi = sum(1 for e in bm.edges if len(e.link_faces) > 2)
    loose = sum(1 for e in bm.edges if len(e.link_faces) == 0)
    return len(bm.edges), open_e, multi, loose


def export_design(key, label, fname, builder):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    part, iron = builder()
    painters = {"wood": paint_wood, "grip": paint_grip, "silver": paint_silver, "gold": paint_gold, "dark": paint_dark, "iron": iron}
    edges, open_e, multi, loose = closed_report(part)
    print(f"PRUEFUNG {label}: Kanten {edges}, offene Kanten {open_e}, Kanten mit mehr als zwei Flaechen {multi}, lose Kanten {loose}")
    tex = make_atlas(painters)
    colors = len({tuple(c) for c in tex.reshape(-1, 3)})
    image = save_image("axe_" + key, tex, os.path.join(tempfile.gettempdir(), f"axe_{key}.png"))
    coll = bpy.data.collections.new("Axt")
    bpy.context.scene.collection.children.link(coll)
    obj = part.build(coll, export_material("axe_" + key, image), smooth=False)
    tris = len(obj.data.polygons)
    pose([obj], {}, root=Matrix.Scale(SCALE, 4))
    bake_scale([obj])
    export_glb([obj], os.path.join(HERE, fname))
    print(f"MODELL {label}: {tris} Dreiecke, {colors} Farben, Textur {TEX}x{TEX}")
    return tris


# --- Blatt -----------------------------------------------------------------------------------------------------------------

EYE = V(0, 1.6, -6.0)
HAND = (0.11, -0.095, 0.19)           # Spielachsen wie bei der Keule (+x = links im Bild)
TILE, UPSCALE = 200, 2
HOME = {}


def load(name, tag):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(MODELS if name.startswith(("club", "blade")) else HERE, name))
    objs = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    for o in objs:
        image = next(n.image for n in o.active_material.node_tree.nodes if n.type == "TEX_IMAGE")
        o.data.materials.clear()
        o.data.materials.append(pass_material(f"{tag}_{o.name}", image))
        o["smooth"] = not o.name.startswith(("club", "sword", "axe"))
        o["tag"] = tag
        HOME[o.name] = o.matrix_world.copy()
    return objs


def place(objs, matrix):
    for o in objs:
        o.matrix_world = matrix @ HOME[o.name]
    bpy.context.view_layer.update()
    for o in objs:
        write_light(o)


def hand_matrix():
    return Matrix.Translation(G(EYE + V(*HAND))) @ Matrix.Rotation(math.radians(-20), 4, "Y")


def held_matrix():
    """Haltung der Waffe in der Hand: der Stiel knickt am Griff 35 Grad nach oben und 10 Grad nach aussen ab (wie die Keule,
    Art Bible Regel 23); der Aermel bleibt stehen."""
    return hand_matrix() @ Matrix.Rotation(math.radians(10), 4, "Z") @ raise_fwd(35)


def aim_up(cam, loc, target, up):
    fwd = (target - loc).normalized()
    zc = -fwd
    xc = up.cross(zc).normalized()
    yc = zc.cross(xc)
    m = Matrix(((xc.x, yc.x, zc.x), (xc.y, yc.y, zc.y), (xc.z, yc.z, zc.z)))
    cam.location = loc
    cam.rotation_euler = m.to_euler()


def render_tile(visible, cam, loc, target, fov, size, mode, up=None):
    setup_render(*size)
    cam.data.angle = math.radians(fov)
    aim_up(cam, loc, target, up if up is not None else G(V(0, 1, 0)))
    return compose(render_passes("blatt", visible), mode)[::-1]


def render_mask(visible, cam, loc, target, fov, size):
    setup_render(*size)
    cam.data.angle = math.radians(fov)
    aim_up(cam, loc, target, G(V(0, 1, 0)))
    return coverage(render_passes("maske", visible))[::-1]


def up2(a, f):
    return np.repeat(np.repeat(a, f, axis=0), f, axis=1)


def text(img, s, x, y, size=16):
    """Beschriftung mit der Blender-Schrift; y von oben. Groesse wird geprueft (ein vertauschter Aufruf fraß einmal 20 GB)."""
    assert 8 <= size <= 40 and isinstance(s, str) and 0 <= x < 20000 and 0 <= y < 20000, (s, x, y, size)
    f = blf.load("C:/Windows/Fonts/arial.ttf")
    blf.size(f, size)
    w, h = blf.dimensions(f, s)
    W, H = int(w) + 8, int(size * 1.6)
    ib = imbuf.new((W, H))
    blf.color(f, 1, 1, 1, 1)
    blf.position(f, 4, int(size * 0.4), 0)
    with blf.bind_imbuf(f, ib):
        blf.draw_buffer(f, s)
    tmp = os.path.join(tempfile.gettempdir(), "axt_txt.png")
    ib.filepath = tmp
    imbuf.write(ib)
    im = bpy.data.images.load(tmp)
    im.colorspace_settings.name = "Non-Color"
    a = np.array(im.pixels[:], np.float32).reshape(H, W, 4)[::-1, :, 3]
    bpy.data.images.remove(im)
    hh, ww = img.shape[:2]
    ye, xe = min(hh, y + H), min(ww, x + W)
    m = (a[:ye - y, :xe - x] > 0.5)[..., None]
    img[y:ye, x:xe] = np.where(m, 235.0, img[y:ye, x:xe])


CENTRE = V(0, 0.0, 0.30)       # Mitte der Ansichten (Spielmass der Datei)
UPG = G(V(0, 0, 1))            # Bildoben = Stiel nach vorn


def blatt(tris):
    stone_dir = os.path.join(tempfile.gettempdir(), "axt_stein")
    os.makedirs(stone_dir, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    axes = {k: load(f, k) for k, _l, f, _b in DESIGNS}
    club = load("club.glb", "club")
    club_hand = load("club_hand.glb", "clubh")
    blade = load("blade.glb", "blade")
    blade_hand = load("blade_hand.glb", "bladeh")
    cam = camera()
    room = build_room(stone_textures(stone_dir))

    def hide_all_but(objs):
        return objs

    # Einzelansichten (haengen nur an den Waffen, ohne Raum)
    tiles, wide = {}, {}
    side_loc = G(CENTRE + V(0, 1, 0) * 1.5)
    quarter_dir = V(-0.8, 0.40, -0.30).normalized()
    quarter_loc = G(CENTRE + quarter_dir * 1.5)
    for k, label, _f, _b in DESIGNS:
        place(axes[k], Matrix())
        tiles[(k, "seite")] = up2(render_tile(axes[k], cam, side_loc, G(CENTRE), 33, (TILE, TILE), "ansicht", UPG), UPSCALE)
        tiles[(k, "schraeg")] = up2(render_tile(axes[k], cam, quarter_loc, G(CENTRE), 33, (TILE, TILE), "ansicht", UPG), UPSCALE)
        # Massstab: Axt und Keule nebeneinander (Keule bei x = +0,30, Axt bei x = -0,06), Blick von oben
        place(axes[k], Matrix.Translation(G(V(-0.07, 0, 0))))
        place(club, Matrix.Translation(G(V(0.40, 0, 0))))
        wide[k] = up2(render_tile(axes[k] + club, cam, G(V(0.17, 1.6, 0.30)), G(V(0.17, 0, 0.30)), 42, (TILE, TILE), "ansicht", UPG), UPSCALE)
    # Ich-Sicht: Raum, Nebel, Koernung, Waffe wie gehalten
    crop = (80, 480, 110, 510)                       # Zeilen von oben, Spalten
    ego, masks, full = {}, {}, {}
    shots = [(k, axes[k], None) for k, *_ in DESIGNS] + [("club", club, club_hand), ("blade", blade, blade_hand)]
    for k, w, h in shots:
        hands = h or []
        keep = list(room) + w + list(hands)
        place(w, held_matrix() if k != "blade" else hand_matrix())
        if h:
            place(h, hand_matrix())
        elif k in axes:
            place(club_hand, hand_matrix())
            keep += club_hand
        shot = render_tile(keep, cam, G(EYE), G(EYE + V(0, 0, 1)), 70, (854, 480), "spiel")
        full[k] = shot
        ego[k] = up2(shot[crop[0]:crop[1], crop[2]:crop[3]], 1)
        # Silhouette der Waffe allein (ohne Aermel)
        masks[k] = render_mask(list(w), cam, G(EYE), G(EYE + V(0, 0, 1)), 70, (854, 480))[crop[0]:crop[1], crop[2]:crop[3]]
    return tiles, wide, ego, masks, full


def main():
    tris = {}
    for key, label, fname, builder in DESIGNS:
        tris[key] = export_design(key, label, fname, builder)
    tiles, wide, ego, masks, full = blatt(tris)
    # Sheet
    gap, label_h, tw = 8, 26, TILE * UPSCALE
    cols, rows = 4, 4
    sheet = np.zeros((label_h + rows * (tw + label_h + gap), cols * (tw + gap), 3)) + FOG_COLOR
    text(sheet, "Silver & Ash - Hunter's Axe (Issue 201), Runde 1: drei Entwuerfe, Seite / Dreiviertel / Ich-Sicht / Massstab neben der Keule",
         6, 2, 16)

    def put(r, c, img, title, span=1):
        x0, y0 = c * (tw + gap), label_h + r * (tw + label_h + gap)
        h, w = img.shape[:2]
        sheet[y0 + label_h:y0 + label_h + min(h, tw), x0:x0 + min(w, span * tw + (span - 1) * gap)] = \
            img[:tw, :span * tw + (span - 1) * gap]
        text(sheet, title, x0 + 4, y0, 16)

    for r, (k, lab, _f, _b) in enumerate(DESIGNS):
        put(r, 0, tiles[(k, "seite")], f"{lab} ({tris[k]} Dreiecke): Seite (Klingenflaeche)")
        put(r, 1, tiles[(k, "schraeg")], f"{lab}: Dreiviertel (Schneidenseite)")
        e = ego[k]
        pad = np.zeros((tw, tw, 3)) + FOG_COLOR
        big = up2(e, 1)
        pad[:min(tw, big.shape[0]), :min(tw, big.shape[1])] = big[:tw, :tw]
        put(r, 2, pad, f"{lab}: Ich-Sicht (Ausschnitt)")
        put(r, 3, wide[k], f"{lab} (rechts) neben der Keule")
    # unterste Zeile: Bestand und Silhouetten
    for c, (k, lab) in enumerate((("club", "Bestand Keule"), ("blade", "Bestand Klinge"))):
        pad = np.zeros((tw, tw, 3)) + FOG_COLOR
        pad[:ego[k].shape[0], :ego[k].shape[1]] = ego[k][:tw, :tw]
        put(3, c, pad, f"{lab}: Ich-Sicht (Ausschnitt)")
    sil = np.zeros((tw, 2 * tw + gap, 3)) + FOG_COLOR
    order = [("a", "A"), ("b", "B"), ("c", "C"), ("club", "Keule"), ("blade", "Klinge")]
    for i, (k, lab) in enumerate(order):
        m = masks[k]
        # auf die Haelfte verkleinern (Spiel aus der Ferne): 2x2 Bloecke, Anteil gedeckt
        h2, w2 = m.shape[0] // 3, m.shape[1] // 3
        small = m[:h2 * 3, :w2 * 3].reshape(h2, 3, w2, 3).mean(axis=(1, 3))[..., None]
        thumb = np.where(small > 0.3, 235.0, 0.0) + FOG_COLOR * 0
        x0 = 2 + i * (w2 + 6)
        sil[40:40 + h2, x0:x0 + w2] = thumb
        text(sil, lab, x0, 8, 16)
    put(3, 2, sil, "Silhouetten in der Ich-Sicht, Drittel-Groesse (A B C Keule Klinge)", span=2)
    save_png(sheet[::-1], os.path.join(HERE, "blatt.png"))
    for k in ("a", "b", "c"):
        save_png(full[k][::-1], os.path.join(tempfile.gettempdir(), f"ich_{k}.png"))


if __name__ == "__main__":
    main()
