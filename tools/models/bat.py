"""Vampirfledermaus

Erzeuger fuer das Spiel (Uebergabe aus dem Probestueck fledermaus, Branch claude/design-probe-ghul-45a8c4).
Erzeugt nur Modell und Textur nach assets/models/, keine Probebilder; die Bilder entstehen im Probestueck-Skript.

Aufruf (aus dem Repository-Wurzelverzeichnis):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/models/bat.py

Herkunft: alles per Skript, nichts heruntergeladen. Teile, Drehpunkte und Haltungen: design/uebergabe/README.md.
"""


import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bmesh  # noqa: E402
import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402
from psx import export_glb, report_parts  # noqa: E402
from psx import (V, G, Part, tube, rects_from_pixels, vnoise, blob, mix, paint_atlas, save_image,  # noqa: E402
                 export_material, stone_textures, build_room, pass_material, write_light, raise_fwd,
                 setup_render, camera, aim, render_passes, compose, save_png, zoom, side_by_side, coverage, runs)

# Modelle und Texturen landen in assets/models (vom Skript in tools/models aus zwei Ebenen hoch).
OUT_DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
                       "assets", "models")
TEX = 128

# --- Farben -------------------------------------------------------------------------------------------------------

FUR_DARK = np.array((58, 48, 50), float)        # Ruecken: dunkel (sieht man selten)
FUR_BELLY = np.array((168, 166, 152), float)    # Bauch: fahl, aschgrau - von unten sichtbar
MEMBRANE = np.array((188, 190, 176), float)     # Flughaut unten: hell und kalt-grau; nach unten zeigende Flaechen
                                                # bekommen im Spiel nur 55 % Licht, deshalb so hell gemalt
MEMBRANE_TOP = np.array((66, 58, 56), float)    # Flughaut oben: dunkles Leder
BONE = np.array((54, 46, 48), float)            # Fingerknochen und Adern: dunkel, das Muster von unten
SKIN = np.array((120, 112, 104), float)         # Gesicht, Ohren innen
HOLLOW = np.array((20, 14, 18), float)          # Maul, Augen
TOOTH = np.array((222, 218, 196), float)        # Fangzaehne: das Hellste am Tier
CLAW = np.array((176, 168, 146), float)

RECTS = {
    "membrane": (0, 64, 64, 128),       # Unterseite der Flughaut (planar, ganze Fluegelflaeche)
    "membrane_top": (64, 64, 128, 128),
    "body": (0, 32, 64, 64),
    "head": (64, 32, 128, 64),
    "bone": (0, 16, 32, 32),
    "ear": (32, 16, 64, 32),
    "jaw": (64, 16, 96, 32),
    "tooth": (96, 16, 112, 32),
    "claw": (112, 16, 128, 32),
}
UV = rects_from_pixels(RECTS, TEX)

# Fluegel (rechte Seite, Spiel-Achsen relativ zur Rumpfmitte; links gespiegelt). y = Hoehe, z = vorn.
SHOULDER = (0.06, 0.02, 0.05)
WRIST = (0.30, 0.04, 0.11)
TIPS = [(0.66, 0.02, 0.12), (0.63, 0.0, -0.08), (0.49, 0.0, -0.22), (0.31, -0.01, -0.19)]   # Finger 2..5
SCALLOPS = [(0.55, 0.01, 0.02), (0.52, 0.0, -0.12), (0.39, -0.01, -0.15)]                   # Bucht dazwischen
TRAIL = [(0.17, -0.02, -0.15), (0.05, -0.01, -0.12)]                                        # Hinterkante zum Rumpf
WING_BOX = (0.0, -0.24, 0.70, 0.16)    # x0, z0, x1, z1 der ganzen Fluegelflaeche fuer die UV
THICK = 0.006

HOVER_CENTRE = V(0, 2.4, 0)            # Rumpfmitte: Schwebehoehe 2,2 m + halbe Koerperhoehe (BodySizes.Bat)


# --- Textur -------------------------------------------------------------------------------------------------------

def wing_uv(p):
    x0, z0, x1, z1 = WING_BOX
    return ((p[0] - x0) / (x1 - x0), (p[2] - z0) / (z1 - z0))


def segment_dist(U, V_, a, b):
    ax, ay = a
    bx, by = b
    dx, dy = bx - ax, by - ay
    t = np.clip(((U - ax) * dx + (V_ - ay) * dy) / (dx * dx + dy * dy), 0, 1)
    return np.hypot(U - (ax + t * dx), V_ - (ay + t * dy))


def paint_membrane(U, V_, base, line, seed):
    h, w = U.shape
    col = np.broadcast_to(base, (h, w, 3)).copy()
    col *= (0.86 + 0.24 * vnoise(h, w, 10, 10, seed))[..., None]
    col *= (0.92 + 0.12 * vnoise(h, w, 40, 40, seed + 1))[..., None]               # Faltenhaut
    wr, sh = wing_uv(WRIST), wing_uv(SHOULDER)
    bones = [(sh, wr)] + [(wr, wing_uv(t)) for t in TIPS]
    d = np.min([segment_dist(U, V_, a, b) for a, b in bones], axis=0)
    col = mix(col, line, np.clip(1 - d / 0.018, 0, 1))                              # Knochen
    # Adern: verzweigt, quer zwischen den Fingern.
    veins = np.abs(np.sin(U * 40 + vnoise(h, w, 6, 6, seed + 2) * 8)) < 0.08
    col = mix(col, line, (veins & (vnoise(h, w, 8, 8, seed + 3) > 0.55)).astype(float) * 0.55)
    # Zum Rumpf hin dunkler, an der Hinterkante ein dunkler Saum.
    col *= (0.75 + 0.25 * np.clip(U * 3, 0, 1))[..., None]
    return col


def paint_body(U, V_):
    h, w = U.shape
    top = 1 - np.abs(np.cos(np.pi * U))                                             # 0 Bauch, 1 Ruecken
    col = FUR_BELLY * (1 - top[..., None]) + FUR_DARK * top[..., None]
    col *= (0.8 + 0.35 * vnoise(h, w, 30, 6, 91))[..., None]                       # Fellstraehnen
    col = mix(col, FUR_DARK * 0.7, np.clip((vnoise(h, w, 5, 5, 92) - 0.6) * 2, 0, 0.4))
    return col


def paint_head(U, V_):
    """u rundherum (0 unten = Kehle, 0,5 oben), v von hinten nach vorn (Schnauze)."""
    h, w = U.shape
    top = 1 - np.abs(np.cos(np.pi * U))
    col = FUR_BELLY * (1 - top[..., None]) + FUR_DARK * 1.2 * top[..., None]
    col *= (0.82 + 0.3 * vnoise(h, w, 20, 10, 93))[..., None]
    snout = V_ > 0.62
    col = mix(col, SKIN * 0.9, snout.astype(float) * 0.8)                           # nackte, faltige Schnauze
    col = mix(col, SKIN * 0.6, (snout & (((V_ * 30) % 1.0) < 0.3)).astype(float) * 0.5)
    for sgn in (-1, 1):                                                             # kleine, tiefe Augen
        cu = 0.5 + sgn * 0.13
        col = mix(col, HOLLOW, np.clip(blob(U, V_, cu, 0.58, 0.05, 0.07) * 2.5, 0, 1))
        col = mix(col, TOOTH * 0.8, (blob(U, V_, cu + sgn * 0.01, 0.6, 0.012, 0.015) > 0.3).astype(float))
    col = mix(col, HOLLOW, (blob(U, V_, 0.5, 0.93, 0.06, 0.05) > 0.2).astype(float))  # Nasenloecher-Blatt
    lip = (U < 0.12) | (U > 0.88)                                                   # Unterseite vorn: Oberkiefer
    col = mix(col, HOLLOW, (lip & (V_ > 0.55)).astype(float))
    return col


def paint_simple(base, var, seed, dark_end=0.0):
    def painter(U, V_):
        h, w = U.shape
        col = np.broadcast_to(base, (h, w, 3)).copy()
        col *= (1 - var / 2 + var * vnoise(h, w, 8, 8, seed))[..., None]
        if dark_end:
            col *= (1 - dark_end * np.clip(V_ * 2 - 1, 0, 1))[..., None]
        return col
    return painter


def paint_ear(U, V_):
    h, w = U.shape
    col = np.broadcast_to(SKIN, (h, w, 3)).copy()
    col *= (0.8 + 0.3 * vnoise(h, w, 12, 4, 95))[..., None]
    col = mix(col, BONE, (((V_ * 6) % 1.0) < 0.15).astype(float) * 0.6)             # Rippen der Ohrmuschel
    return col


def paint_jaw(U, V_):
    col = paint_simple(SKIN * 0.8, 0.3, 96)(U, V_)
    inside = np.abs(U - 0.5) < 0.22                                                 # Oberseite = Maulinneres
    return mix(col, HOLLOW, inside.astype(float))


PAINTERS = {
    "membrane": lambda U, V_: paint_membrane(U, V_, MEMBRANE, BONE, 81),
    "membrane_top": lambda U, V_: paint_membrane(U, V_, MEMBRANE_TOP, BONE * 0.6, 85),
    "body": paint_body, "head": paint_head, "ear": paint_ear, "jaw": paint_jaw,
    "bone": paint_simple(BONE, 0.4, 97, dark_end=0.3),
    "tooth": paint_simple(TOOTH, 0.12, 98), "claw": paint_simple(CLAW, 0.2, 99),
}


# --- Geometrie ----------------------------------------------------------------------------------------------------

def membrane(part, outline, s):
    """Flughaut als duenne, geschlossene Platte: Facher von outline[0] aus, oben und unten, mit Rand.
    outline in Spiel-Achsen (rechte Seite), s spiegelt x. UV planar ueber die ganze Fluegelflaeche."""
    pts = [V(s * p[0], p[1], p[2]) for p in outline]
    up = V(0, THICK / 2, 0)
    bm, uvl = part.bm, part.uv
    top = [bm.verts.new(G(p + up - part.pivot)) for p in pts]
    bot = [bm.verts.new(G(p - up - part.pivot)) for p in pts]
    uvs = [wing_uv((abs(p.x), p.y, p.z)) for p in pts]
    n = len(pts)
    faces = []

    def face(verts, coords, rect):
        f = bm.faces.new(verts)
        u0, v0, u1, v1 = UV[rect]
        for loop, (u, v) in zip(f.loops, coords):
            loop[uvl].uv = (u0 + (u1 - u0) * u, v0 + (v1 - v0) * v)
        faces.append(f)
        return f

    for k in range(1, n - 1):
        face((top[0], top[k], top[k + 1]), (uvs[0], uvs[k], uvs[k + 1]), "membrane_top")
        face((bot[0], bot[k + 1], bot[k]), (uvs[0], uvs[k + 1], uvs[k]), "membrane")
    for k in range(n):
        j = (k + 1) % n
        face((top[k], bot[k], bot[j], top[j]), (uvs[k], uvs[k], uvs[j], uvs[j]), "membrane")
    bmesh.ops.recalc_face_normals(bm, faces=faces)
    # Sicherstellen: die Unterseite zeigt nach unten (sonst sind die Bereiche vertauscht).
    for f in faces[1::2][: n - 2]:
        pass


def bone(part, a, b, r0, r1, s, sides=4):
    pa, pb = V(s * a[0], a[1], a[2]), V(s * b[0], b[1], b[2])
    part.loft(tube([pa, pa + (pb - pa) * 0.5, pb], [(r0, r0, r0), ((r0 + r1) / 2,) * 3, (r1, r1, r1)], sides,
                   ref_up=V(0, 1, 0)), "bone")


def build_wing(s):
    side = "px" if s > 0 else "nx"
    inner = Part(f"wing_in_{side}", V(s * SHOULDER[0], SHOULDER[1], SHOULDER[2]), UV, parent="body")
    membrane(inner, [SHOULDER, WRIST, TIPS[3], TRAIL[0], TRAIL[1]], s)
    bone(inner, SHOULDER, WRIST, 0.014, 0.011, s)
    outer = Part(f"wing_out_{side}", V(s * WRIST[0], WRIST[1], WRIST[2]), UV, parent=inner.name)
    membrane(outer, [WRIST, TIPS[0], SCALLOPS[0], TIPS[1], SCALLOPS[1], TIPS[2], SCALLOPS[2], TIPS[3]], s)
    for tip in TIPS:
        bone(outer, WRIST, tip, 0.008, 0.004, s)
    # Daumenkralle am Handgelenk, nach vorn.
    w = V(s * WRIST[0], WRIST[1], WRIST[2])
    outer.spike(tuple(w), tuple(w + V(s * 0.01, 0.0, 0.05)), 0.008, "claw")
    return [inner, outer]


def build_body():
    part = Part("body", (0, 0, 0), UV)
    centres = [V(0, -0.01, -0.17), V(0, 0.0, -0.11), V(0, 0.01, -0.03), V(0, 0.015, 0.04), V(0, 0.02, 0.09)]
    shapes = [(0.025, 0.02, 0.02), (0.055, 0.055, 0.045), (0.075, 0.075, 0.06), (0.07, 0.07, 0.06), (0.045, 0.045, 0.04)]
    part.loft(tube(centres, shapes, 10), "body")
    # Hinterbeine mit Krallenfuessen, haengend - von unten gut zu sehen.
    for s in (-1, 1):
        hip, knee, foot = V(s * 0.04, -0.03, -0.12), V(s * 0.08, -0.09, -0.17), V(s * 0.07, -0.13, -0.15)
        part.loft(tube([hip, knee, foot], [(0.012, 0.012, 0.012), (0.009, 0.009, 0.009), (0.008, 0.008, 0.008)], 4,
                       ref_up=V(0, 0, 1)), "bone")
        for dx in (-0.012, 0.0, 0.012):
            part.spike(tuple(foot), tuple(foot + V(s * dx, -0.035, 0.02)), 0.005, "claw", sides=3)
    return part


NECK = V(0, 0.03, 0.09)
JAW_HINGE = V(0, 0.0, 0.13)


def build_head():
    part = Part("head", NECK, UV, parent="body")
    centres = [V(0, 0.03, 0.08), V(0, 0.04, 0.13), V(0, 0.03, 0.18), V(0, 0.015, 0.225), V(0, 0.01, 0.245)]
    shapes = [(0.045, 0.04, 0.04), (0.06, 0.05, 0.055), (0.05, 0.04, 0.045), (0.032, 0.025, 0.03), (0.022, 0.018, 0.022)]
    part.loft(tube(centres, shapes, 10), "head")
    # Grosse spitze Ohren, nach aussen und hinten gestellt: der Umriss.
    for s in (-1, 1):
        base = [V(s * 0.025, 0.08, 0.15), V(s * 0.06, 0.06, 0.15), V(s * 0.055, 0.07, 0.11)]
        tip = V(s * 0.11, 0.2, 0.09)
        polys = [[base[0], base[1], tip], [base[1], base[2], tip], [base[2], base[0], tip], list(reversed(base))]
        part.convex(polys, "ear")
    # Obere Fangzaehne: lang, weiss, nach unten - von unten das Helle am Kopf.
    for s in (-1, 1):
        root = V(s * 0.016, 0.0, 0.225)
        part.spike(tuple(root), tuple(root + V(0, -0.055, -0.006)), 0.008, "tooth", sides=3)
    for s in (-1, 1):                                                                # kleinere Reihe dahinter
        root = V(s * 0.03, 0.005, 0.19)
        part.spike(tuple(root), tuple(root + V(0, -0.025, 0)), 0.005, "tooth", sides=3)
    return part


def build_jaw():
    part = Part("jaw", JAW_HINGE, UV, parent="head")
    part.loft(tube([V(0, -0.005, 0.13), V(0, -0.012, 0.18), V(0, -0.015, 0.22)],
                   [(0.04, 0.012, 0.008), (0.032, 0.012, 0.008), (0.02, 0.01, 0.006)], 6), "jaw")
    for s in (-1, 1):
        root = V(s * 0.014, -0.008, 0.21)
        part.spike(tuple(root), tuple(root + V(0, 0.03, 0.004)), 0.006, "tooth", sides=3)
    return part


# --- Posen --------------------------------------------------------------------------------------------------------

def flap(s, deg):
    """Fluegel um die Laengsachse heben (positiv = hoch), s = Seite."""
    return Matrix.Rotation(math.radians(deg) * s, 4, Vector((0, -1, 0)))


def sweep(s, deg):
    """Fluegel nach hinten schwenken (um die Hochachse)."""
    return Matrix.Rotation(math.radians(deg) * s, 4, Vector((0, 0, 1)))


POSES = {
    "neutral": (0, 0, 0, 0, 0, 0, 0),                # so steht es in der Datei
    # Name: (Rumpf nicken, Kopf heben, Kiefer, innen heben, aussen heben, innen schwenken, aussen schwenken)
    "schweben": (15, 0, -12, 8, 4, 0, 0),               # Mitte des Fluegelschlags, Bauch zum Spieler
    "schweben_oben": (15, 0, -10, 40, 20, 0, 0),
    "schweben_unten": (15, 0, -10, -35, -15, 0, 0),
    "kreischen": (12, 30, -55, 65, 25, -15, -20),      # hoch aufgerichtet, Fluegel weit oben, Maul weit auf
    "sturzflug": (-30, -5, -45, -12, -5, 32, 38),       # Kopf voran, Fluegel halb nach hinten angelegt (Pfeilform)
}


def pose_bat(objs, name, root):
    body_p, head_p, jaw_p, in_up, out_up, in_sw, out_sw = POSES[name]
    by = {o.name: o for o in objs}
    mats = {}

    def local(o):
        n = o.name
        s = 1 if n.endswith("px") else -1
        if n == "body":
            return raise_fwd(body_p)
        if n == "head":
            return raise_fwd(head_p)
        if n == "jaw":
            return raise_fwd(jaw_p)
        if n.startswith("wing_in"):
            return sweep(s, in_sw) @ flap(s, in_up)
        if n.startswith("wing_out"):
            return sweep(s, out_sw) @ flap(s, out_up)
        return Matrix()

    def world(n):
        if n in mats:
            return mats[n]
        o = by[n]
        pivot = Vector(list(o["pivot"]))
        parent = o["parent"]
        if parent:
            m = world(parent) @ Matrix.Translation(G(pivot) - G(Vector(list(by[parent]["pivot"])))) @ local(o)
        else:
            m = root @ Matrix.Translation(G(pivot)) @ local(o)
        mats[n] = m
        return m

    for o in objs:
        o.matrix_world = world(o.name)
    bpy.context.view_layer.update()


# --- Platzhalter aus BatViews.cs zum Vergleich ---------------------------------------------------------------------

def build_placeholder(coll):
    """Quader wie PlaceholderBatView (Fluegelschlag oben, beat = 1), Farben einfarbig als winzige Texturen."""
    objs = []

    def solid(name, rgb):
        img = save_image(name, np.broadcast_to(np.array(rgb, float), (4, 4, 3)).copy(),
                         os.path.join(os.environ.get("TEMP", "."), name + ".png"))
        return pass_material("mat_" + name, img)

    fur, wing, eye = solid("ph_fur", (52, 40, 46)), solid("ph_wing", (88, 52, 62)), solid("ph_eye", (220, 40, 40))
    rects = {"x": (0.1, 0.1, 0.9, 0.9)}

    def box(name, size, centre, mat, rot=Matrix()):
        p = Part(name, (0, 0, 0), rects)
        hx, hy, hz = (c / 2 for c in size)
        p.box((-hx, -hy, -hz), (hx, hy, hz), "x")
        o = p.build(coll, mat, smooth=False)
        o["local"] = [list(r) for r in (Matrix.Translation(G(V(*centre))) @ rot)]
        objs.append(o)

    box("ph_body", (0.2, 0.18, 0.24), (0, 0, 0), fur)
    for s in (-1, 1):
        box(f"ph_ear{s}", (0.04, 0.09, 0.04), (s * 0.06, 0.13, 0.05), fur)
        box(f"ph_eye{s}", (0.03, 0.03, 0.01), (s * 0.05, 0.03, 0.125), eye)
        rot = Matrix.Translation(G(V(s * 0.1, 0.02, 0))) @ flap(s, 40) @ Matrix.Translation(G(V(s * 0.2, 0, 0)))
        box(f"ph_wing{s}", (0.4, 0.02, 0.22), (0, 0, 0), wing, rot)
    return objs


def place_placeholder(objs, root):
    for o in objs:
        o.matrix_world = root @ Matrix([list(r) for r in o["local"]])
    bpy.context.view_layer.update()


# --- Bilder -------------------------------------------------------------------------------------------------------

def bat_root(cam_pos):
    """Fledermaus auf Schwebehoehe, zum Spieler gedreht (wie im Spiel: nur um die Hochachse)."""
    to = cam_pos - HOVER_CENTRE
    yaw = math.atan2(to.x, to.z)
    return Matrix.Translation(G(HOVER_CENTRE)) @ Matrix.Rotation(yaw, 4, "Z")


def measure(mask, img, label):
    rows = np.where(mask.any(axis=1))[0]
    cols = np.where(mask.any(axis=0))[0]
    lum = img.mean(axis=2)
    ring = np.zeros_like(mask)
    r0, r1, c0, c1 = max(rows.min() - 6, 0), rows.max() + 7, max(cols.min() - 6, 0), cols.max() + 7
    ring[r0:r1, c0:c1] = True
    ring &= ~mask
    return (f"{label}: {cols.max() - cols.min() + 1} x {rows.max() - rows.min() + 1} Pixel, {int(mask.sum())} Pixel gedeckt, "
            f"mittlere Helligkeit {lum[mask].mean():.0f} gegen Umgebung {lum[ring].mean():.0f} "
            f"(hellste 10 %: {np.percentile(lum[mask], 90):.0f})")


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    os.makedirs(OUT_DIR, exist_ok=True)
    tex = paint_atlas(RECTS, PAINTERS, TEX, colors=32, seed=3)
    image = save_image("bat", tex, os.path.join(OUT_DIR, "bat.png"))
    coll = bpy.data.collections.new("Fledermaus")
    bpy.context.scene.collection.children.link(coll)
    mat = export_material("bat", image)
    parts = [build_body(), build_head(), build_jaw()] + build_wing(1) + build_wing(-1)
    model = [p.build(coll, mat, smooth=not p.name.startswith("wing")) for p in parts]
    pose_bat(model, "neutral", Matrix())
    export_glb(model, os.path.join(OUT_DIR, "bat.glb"))
    report_parts("bat", model)


main()
