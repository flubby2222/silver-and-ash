"""Probestueck Deckenbogen / Gurtbogen (Issue #537, Baustein aus der Formenlehre Gotik #281, Frage #247).

Ein spitzbogiger Gurtbogen quer ueber den 2 m breiten, 3,5 m hohen Gang auf zwei tragenden Wandpfeilern. Alles entsteht aus
diesem einen Skript: die Modelle (.glb je Wandvariante a, b, c), der Atlas, alle Probebilder. Die Pfeiler sind die Fassung
"gang_traeger" aus design/proben/grafik/wandpfeiler-536/wandpfeiler.py (nur gelesen und ausgefuehrt, nicht veraendert):
gleiches Profil, gleiche Welt-UV (eine 64-px-Kachel je Meter), gleiche um LIFT Stufen aufgehellte Wandkachel.

Spiel-Achsen (x seitlich, y oben, z vorn; Meter). Ursprung des Modells: Mitte des Gangs auf dem Boden, in der Ebene des Bogens.
Der Gang verlaeuft entlang z. Waende bei x = -1 und x = +1, Decke bei y = 3,5.

Bogen (Herleitung in docs/design/formenlehre-gotik.md): gedrueckter Spitzbogen aus zwei Kreisbogen, Mittelpunkte auf der
Kaempferlinie (y = 2,0) im Abstand 0,625 neben der Achse, innen Radius 1,5 (lichte Weite 1,75, Scheitel 3,364), aussen
Radius 1,625 (Scheitel 3,5), Ring 1/8 m dick, 0,5 m tief (bis 1/16 vor dem Rand des Kapitells). Sechs Keilsteine je Haelfte,
radiale Fugen alle 11 Grad; der oberste Stein (Schlussstein) ist durchgehend ueber den Scheitel. Fuss und Schlussstein
weiten sich aussen auf Radius 1,75 auf: die Zaehne (1/8 m) stecken in Wand und Decke, so endet der Ring nirgends als nackte
Kante auf der Flaeche.

Aufruf (aus dem Repository-Wurzelverzeichnis):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/deckenbogen-537/deckenbogen.py
Zwei Laeufe liefern gleiche Dateien (kein Zufall).
"""

import math
import os
import sys
import tempfile

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
PILLAR_PY = os.path.join(ROOT, "design", "proben", "grafik", "wandpfeiler-536", "wandpfeiler.py")

# --- Pfeiler-Skript laden (ohne sein main) ---------------------------------------------------------------------------
_src = open(PILLAR_PY, encoding="utf-8").read().rstrip()
assert _src.endswith("\nmain()"), "wandpfeiler.py endet nicht mehr mit main()"
W = {"__file__": PILLAR_PY, "__name__": "wandpfeiler_ns"}
exec(compile(_src[:-len("main()")], PILLAR_PY, "exec"), W)

bpy = W["bpy"]
bmesh = __import__("bmesh")
Matrix = W["Matrix"]
V, G, Part = W["V"], W["G"], W["Part"]
rects_from_pixels = W["rects_from_pixels"]
OUT = HERE
TMP = os.path.join(tempfile.gettempdir(), "deckenbogen537_tmp")
os.makedirs(TMP, exist_ok=True)
W["TMP"] = TMP
WALL_FILES = W["WALL_FILES"]
emit = W["emit"]

# --- Masse -----------------------------------------------------------------------------------------------------------
CX = 0.625              # Mittelpunkt der Bogenhaelfte neben der Achse
Y0 = 2.0                # Kaempferlinie = Oberkante des Kapitells
R_IN = 1.5
R_OUT = 1.625
R_TOOTH = 1.75          # aufgeweiteter Aussenradius an Fuss und Schlussstein
HALF_DEPTH = 0.25       # Bogen 0,5 tief
CHAMFER = 0.0625
STEP_DEG = 11.0
STONES = 6              # Keilsteine je Haelfte
GANG_W, GANG_H = 2.0, 3.5
PLAYER_H, WARDEN_H = 1.8, 2.52

# Profil des Rings (r, z): Laibung mit Fase 1/16, Seitenflaechen, Ruecken
PROFILE = [(R_IN, HALF_DEPTH - CHAMFER), (R_IN + CHAMFER, HALF_DEPTH), (R_OUT, HALF_DEPTH),
           (R_OUT, -HALF_DEPTH), (R_IN + CHAMFER, -HALF_DEPTH), (R_IN, -(HALF_DEPTH - CHAMFER))]
OUTER_IDX = (2, 3)
EDGE_KIND = ["I", "S", "I", "S", "I", "I"]      # I: Lauf- und Fasenflaeche, S: Seitenflaeche (Stirn des Rings)
TOOTH_AT = {0, STONES}                          # Ringe mit aufgeweitetem Aussenradius

# --- Atlas -----------------------------------------------------------------------------------------------------------
ATLAS_SIZE = (128, 128)
PIX = {"stone": (0, 0, 64, 64), "pillar": (64, 0, 128, 64)}
SW, SH_I, SH_S = 20, 32, 16
for _j in range(STONES):
    PIX["I%d" % _j] = (SW * _j, 64, SW * _j + SW, 64 + SH_I)
    PIX["S%d" % _j] = (SW * _j, 96, SW * _j + SW, 96 + SH_S)
UV = rects_from_pixels(PIX, ATLAS_SIZE)
W["FRAME_UV"] = UV


def my_atlas(variant):
    """Wand- und Pfeilerkachel wie beim Pfeiler, dazu je Keilstein ein Stueck der aufgehellten Pfeilerkachel mit Fugen."""
    stein = W["stein_row"]()
    tile = W["load_tile"](WALL_FILES[variant])
    lifted = W["pillar_tile"](tile, stein)
    h, w = ATLAS_SIZE[1], ATLAS_SIZE[0]
    out = np.tile(stein[0].astype(float), (h, w, 1))
    out[0:64, 0:64] = tile[::-1]
    out[0:64, 64:128] = lifted[::-1]
    flat = tile.reshape(-1, 3)
    lo = min(int(((stein - p) ** 2).sum(1).argmin()) for p in {tuple(q) for q in flat})
    mortar = stein[min(lo + W["LIFT"] - 2, len(stein) - 1)]   # zwei Stufen dunkler als die Fugen des Pfeilers: lesbar im Dunkel

    def idx_of(p):
        return int(((stein - p) ** 2).sum(1).argmin())

    for j in range(STONES):
        oy, ox = (11 * j + 5) % 64, (17 * j + 9) % 64
        rolled = np.roll(lifted, (-oy, -ox), (0, 1))
        for name, rows, y_off in (("I", SH_I, 0), ("S", SH_S, SH_I)):
            patch = rolled[y_off:y_off + rows, :SW].copy()
            if j % 2 == 1:                               # jeder zweite Stein eine Stufe dunkler: Steine lesen sich einzeln
                patch = np.array([[stein[max(idx_of(p) - 1, 0)] for p in row] for row in patch])
            patch[:, :2] = mortar                        # Fuge 3 px (2 + 1 vom Nachbarn), wie die Fugen der Wand
            if j < STONES - 1:                           # der Schlussstein hat keine Fuge am Scheitel
                patch[:, -1] = mortar
            x0, y0, x1, y1 = PIX["%s%d" % (name, j)]
            out[y0:y1, x0:x1] = patch[::-1]
    return out


W["atlas"] = my_atlas


def palette_check(tex):
    stein = {tuple(int(c) for c in p) for p in W["stein_row"]()}
    cols = {tuple(int(c) for c in p) for p in np.round(tex).reshape(-1, 3)}
    return len(cols), len(cols - stein)


# --- Bogen -----------------------------------------------------------------------------------------------------------

def ring_points(k):
    """Ecken des Profils an Ring k der linken Haelfte: (x, y, z, r). Ring STONES liegt auf der Mittelebene x = 0."""
    pts = []
    for i, (r0, z) in enumerate(PROFILE):
        r = R_TOOTH if (i in OUTER_IDX and k in TOOTH_AT) else r0
        if k < STONES:
            phi = math.radians(STEP_DEG * k)
            x = CX - r * math.cos(phi)
        else:
            phi = math.acos(CX / r)
            x = 0.0
        pts.append((x, Y0 + r * math.sin(phi), z, r))
    return pts


def build_arch(zpos=0.0, name="bogen"):
    """Ein geschlossenes Netz: zwei gespiegelte Haelften aus Keilsteinen, am Scheitel ohne Naht verbunden."""
    part = Part(name, (0, 0, 0), UV)
    verts = {}

    def vert(p):
        key = tuple(int(round(c * 1e5)) for c in p)
        if key not in verts:
            verts[key] = part.bm.verts.new(G(V(*p)))
        return verts[key]

    def uvp(rect, u, v):
        u0, v0, u1, v1 = UV[rect]
        return (u0 + (u1 - u0) * u, v0 + (v1 - v0) * v)

    def vcoord(kind, r, z):
        return (z + HALF_DEPTH) / (2 * HALF_DEPTH) if kind == "I" else (r - R_IN) / (R_TOOTH - R_IN)

    for sgn in (-1, 1):
        def pos(p):
            return vert((p[0] * (-sgn), p[1], p[2] + zpos))

        for k in range(STONES):
            a, b = ring_points(k), ring_points(k + 1)
            for e in range(6):
                f = (e + 1) % 6
                kind = EDGE_KIND[e]
                rect = "%s%d" % (kind, k)
                corners = [(a[e], 0.0), (a[f], 0.0), (b[f], 1.0), (b[e], 1.0)]
                vs = [pos(c) for c, _ in corners]
                uvs = [uvp(rect, u, vcoord(kind, c[3], c[2])) for c, u in corners]
                part._face(vs, uvs)
        cap = ring_points(0)                                           # Fuss: Flaeche auf dem Kapitell
        mid = uvp("I0", 0.5, 0.5)
        part._face([pos(c) for c in cap], [mid] * 6)
    bmesh.ops.recalc_face_normals(part.bm, faces=part.bm.faces[:])
    return part


def build_pillar_side(variant, side, zpos=0.0):
    """Pfeilerfassung gang_traeger, gedreht an die Gangwand (side -1 links, +1 rechts), Wandflaeche bei x = side * 1."""
    part, _ = W["build_pillar"]("gang_traeger", variant, 0.0)
    part.name = "pfeiler_l" if side < 0 else "pfeiler_r"
    rot = Matrix.Rotation(math.radians(90 if side < 0 else -90), 4, "Z")
    part.bm.transform(Matrix.Translation((side * GANG_W / 2, -zpos, 0.0)) @ rot)
    return part


def arch_set(variant, zpos=0.0, sides=(-1, 1)):
    parts = [build_arch(zpos)]
    parts += [build_pillar_side(variant, s, zpos) for s in sides]
    return parts


def clear_height(x):
    """Lichte Hoehe unter dem Bogenring an der Stelle x (Laibung, Radius 1,5)."""
    cx = CX if x <= 0 else -CX                          # Mittelpunkt der gegenueberliegenden Haelfte
    return Y0 + math.sqrt(R_IN ** 2 - (x - cx) ** 2)


# --- Szene -----------------------------------------------------------------------------------------------------------

def add_gang(coll, v, z0, z1, walls="LR", ceiling=True):
    part = Part("gang", (0, 0, 0), UV)
    x = GANG_W / 2
    if "L" in walls:
        emit(part, [(-x, 0, z0), (-x, 0, z1), (-x, GANG_H, z1), (-x, GANG_H, z0)], (1, 0, 0), "stone")
    if "R" in walls:
        emit(part, [(x, 0, z0), (x, 0, z1), (x, GANG_H, z1), (x, GANG_H, z0)], (-1, 0, 0), "stone")
    if ceiling:
        emit(part, [(-x, GANG_H, z0), (x, GANG_H, z0), (x, GANG_H, z1), (-x, GANG_H, z1)], (0, -1, 0), "stone")
    return [part.build(coll, W["pass_material"]("g_%s_%g_%g" % (v, z0, z1), W["atlas_img"](v)), smooth=False)]


def add_door2(coll, v, z):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(W["MODELS"], "door_frame_normal_%s.glb" % v))
    objs = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    for o in objs:
        img = None
        for m in o.data.materials:
            for n in m.node_tree.nodes:
                if n.type == "TEX_IMAGE":
                    img = n.image
        o.data.materials.clear()
        o.data.materials.append(W["pass_material"]("d_" + o.name, img))
        o.parent = None
        o.matrix_world = Matrix.Translation(G(V(0, 0, z - 1)))
        coll.objects.link(o)
        for c in list(o.users_collection):
            if c is not coll:
                c.objects.unlink(o)
    return objs


def add_figure(coll, x, z, h, width, rgb):
    part = Part("figur", (0, 0, 0), {"all": (0.0, 0.0, 1.0, 1.0)})
    part.box((x - width / 2, 0, z - width / 2), (x + width / 2, h, z + width / 2), "all")
    tag = "fig_%g_%g_%d" % (x, h, rgb[0])
    img = W["save_image"](tag, np.tile(np.array(rgb, float), (4, 4, 1)), os.path.join(TMP, tag + ".png"))
    return [part.build(coll, W["pass_material"](tag, img), smooth=False)]


def build_scene(items):
    coll = W["new_scene"]()
    objs = []
    for it in items:
        k = it[0]
        if k == "gang":
            objs += add_gang(coll, *it[1:])
        elif k == "floor":
            objs += W["add_floor"](coll, *it[1:])
        elif k == "arch":
            v, z = it[1], it[2]
            sides = it[3] if len(it) > 3 else (-1, 1)
            for p in arch_set(v, z, sides):
                objs.append(p.build(coll, W["pass_material"]("a_%s_%s_%g" % (v, p.name, z), W["atlas_img"](v)), smooth=False))
        elif k == "door":
            objs += add_door2(coll, it[1], it[2])
        elif k == "fig":
            objs += add_figure(coll, *it[1:])
    W["finish"](objs)
    return objs


W["build_scene"] = build_scene
shot, text, png, hcat, titled = W["shot"], W["text"], W["png"], W["hcat"], W["titled"]
FOG = "spiel"


# --- Modelle ---------------------------------------------------------------------------------------------------------

def export_models():
    rows = []
    d = os.path.join(OUT, "modelle")
    os.makedirs(d, exist_ok=True)
    for v in WALL_FILES:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        coll = bpy.data.collections.new("Deckenbogen")
        bpy.context.scene.collection.children.link(coll)
        tex = my_atlas(v)
        ncol, bad = palette_check(tex)
        assert bad == 0, "Farben ausserhalb der Reihe Stein: %d" % bad
        image = W["save_image"]("deckenbogen_%s" % v, tex, os.path.join(d, "deckenbogen_%s.png" % v))
        mat = W["export_material"]("m", image)
        objs, info = [], []
        for part in arch_set(v):
            o_edges, multi = W["edge_report"](part)
            info.append((part.name, W["triangles"](part), o_edges, multi))
            objs.append(part.build(coll, mat, smooth=False))
        W["export_glb"](objs, os.path.join(d, "deckenbogen_%s.glb" % v))
        rows.append((v, info, ncol))
    for v, info, ncol in rows:
        print("MODELL %s: %s, Farben %d, gesamt %d Dreiecke" % (
            v, "; ".join("%s %d Dreiecke, offen %d, mehrfach %d" % i for i in info), ncol, sum(i[1] for i in info)))
    return rows


# --- Ansichten -------------------------------------------------------------------------------------------------------

def gang_items(v, arches=(0.0, -4.0, -8.0), door=-12.0, ceiling=True, walls="LR", z0=-13.0, z1=8.0):
    items = [("floor", -1, 1, z0, z1), ("gang", v, z0, z1, walls, ceiling)]
    sides = tuple(s for s, c in ((-1, "L"), (1, "R")) if c in walls)
    items += [("arch", v, z, sides) for z in arches]
    if door is not None:
        items.append(("door", v, door))
    return items


def view_gang(v, mode, size=(854, 480)):
    return shot(gang_items(v), (0, 1.6, 2.5), (0, 1.6, -12), 70, size, mode)


def view_one(v, size=(640, 480)):
    items = gang_items(v, arches=(0.0,), door=None, z0=-6.0, z1=8.0)
    return shot(items, (0.75, 1.6, 3.2), (-0.7, 2.4, -0.5), 62, size, "hell")


def view_outside(v, size=(640, 480)):
    items = gang_items(v, arches=(0.0,), door=None, ceiling=False, walls="L", z0=-6.0, z1=6.0)
    return shot(items, (5.2, 3.9, 5.0), (-0.3, 2.3, 0.0), 50, size, "hell")


def view_kaempfer(v, size=(420, 420)):
    items = gang_items(v, arches=(0.0,), door=None, z0=-4.0, z1=6.0)
    a = shot(items, (0.2, 2.1, 1.7), (-0.85, 2.15, 0.0), 40, size, "hell")
    b = shot(items, (-0.55, 2.3, 8.0), (-0.55, 2.3, 0.0), 0, size, "hell", ortho=1.5)
    c = shot(items, (1.0, 1.2, 1.4), (-0.85, 2.05, 0.0), 40, size, "hell")
    return hcat([titled(a, "Kaempfer schraeg oben"), titled(b, "Kaempfer vorn (Elevation)"), titled(c, "Kaempfer von unten")])


def view_scheitel(v, size=(420, 420)):
    items = gang_items(v, arches=(0.0,), door=None, z0=-4.0, z1=6.0)
    a = shot(items, (0.3, 2.4, 2.2), (0.0, 3.4, 0.0), 45, size, "hell")
    b = shot(items, (0.0, 3.3, 8.0), (0.0, 3.3, 0.0), 0, size, "hell", ortho=1.5)
    c = shot(items, (0.9, 2.2, 3.0), (0.0, 3.45, -0.3), 55, size, "hell")
    return hcat([titled(a, "Scheitel von unten"), titled(b, "Scheitel vorn (Elevation)"), titled(c, "Scheitel Decke")])


def view_folge_eye(v, size=(854, 480)):
    return shot(gang_items(v), (0, 1.6, 3.2), (0, 1.9, -12), 70, size, "hell")


def view_folge_oben(v, size=(854, 480)):
    items = gang_items(v, ceiling=False, walls="L", z0=-13.0, z1=4.0)
    return shot(items, (5.5, 4.6, 6.0), (-0.5, 2.0, -5.0), 55, size, "hell")


def view_tuer(v):
    items = gang_items(v, arches=(0.0,), door=-5.0, z0=-5.1, z1=8.0)
    a = shot(items, (0, 1.75, 10), (0, 1.75, 0), 0, (480, 600), "hell", ortho=4.0)
    b = shot(items, (0, 1.6, 3.5), (0, 1.8, -5), 62, (640, 600), "hell")
    return hcat([titled(a, "Bogen und Tuerrahmen ueberlagert (parallel)"), titled(b, "Blick entlang, Tuer am Gangende")])


def view_single(v, which, size=(360, 640)):
    items = [("floor", -2.5, 2.5, -2.5, 2.5), ("arch", v, 0.0)]
    cy = 1.9
    if which == "vorn":
        return shot(items, (0, cy, 8), (0, cy, 0), 0, size, "hell", ortho=4.2)
    if which == "seite":
        return shot(items, (8, cy, 0), (0, cy, 0), 0, size, "hell", ortho=4.2)
    if which == "hinten":
        return shot(items, (0, cy, -8), (0, cy, 0), 0, size, "hell", ortho=4.2)
    return shot(items, (3.4, 5.4, 4.6), (0, 1.9, 0), 38, size, "hell")


def view_front_ortho(v, size=(360, 520)):
    items = [("gang", v, -1, 1, "LR", False), ("arch", v, 0.0)]
    return titled(shot(items, (0, 1.75, 8), (0, 1.75, 0), 0, size, "hell", ortho=3.9), "Wand " + v)


def view_light(v):
    """Lichte Oeffnung gegen Spieler (1,8 m) und Gruftwaechter (Stab bis 2,52 m) in der Gangmitte und 0,35 m neben der Wand."""
    red, blue = (200, 90, 70), (70, 120, 200)
    base = [("gang", v, -1, 1, "LR", False), ("arch", v, 0.0)]
    xs = (-0.65, 0.0, 0.65)
    warden = base + [("fig", x, 0.0, WARDEN_H, 0.45, blue) for x in xs]
    player = base + [("fig", x, 0.0, PLAYER_H, 0.45, red) for x in xs]
    a = shot(warden, (0, 1.75, 8), (0, 1.75, 0), 0, (400, 600), "hell", ortho=4.0)
    b = shot(player, (0, 1.75, 8), (0, 1.75, 0), 0, (400, 600), "hell", ortho=4.0)
    return hcat([titled(a, "Waechter 2,52 m (Stab)"), titled(b, "Spieler 1,8 m")])


def main():
    rows = export_models()
    h0, h35 = clear_height(0.0), clear_height(-(GANG_W / 2 - 0.35))
    print("LICHT Mitte %.3f, 0,35 neben der Wand %.3f, Kaempfer-Weite %.3f" % (h0, h35, 2 * (R_IN - CX)))
    v = "a"
    png(view_gang(v, "spiel"), os.path.join(OUT, "spielaufloesung.png"))
    png(view_gang(v, "hell"), os.path.join(OUT, "auge-hell.png"))
    png(view_one(v), os.path.join(OUT, "schraeg.png"))
    png(view_outside(v), os.path.join(OUT, "schraeg-aussen.png"))
    png(view_kaempfer(v), os.path.join(OUT, "detail-kaempfer.png"))
    png(view_scheitel(v), os.path.join(OUT, "detail-scheitel.png"))
    png(view_folge_eye(v), os.path.join(OUT, "folge-drei.png"))
    png(view_folge_oben(v), os.path.join(OUT, "folge-oben.png"))
    png(view_tuer(v), os.path.join(OUT, "vergleich-tuer.png"))
    png(hcat([titled(view_single(v, w), w) for w in ("vorn", "seite", "hinten", "oben")]), os.path.join(OUT, "einzel.png"))
    png(view_light(v), os.path.join(OUT, "lichte-hoehe.png"))
    png(hcat([view_front_ortho(x) for x in WALL_FILES]), os.path.join(OUT, "blatt-vorn.png"))
    for x in ("b", "c"):
        png(view_gang(x, "spiel"), os.path.join(OUT, "wandvarianten", "spielaufloesung-%s.png" % x))
        png(view_one(x), os.path.join(OUT, "wandvarianten", "schraeg-%s.png" % x))


main()
