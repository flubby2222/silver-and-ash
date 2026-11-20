"""Probestueck Fallgitter (Issue #540). Nur ausserhalb des Spiels, kein Einbau.

Alles entsteht aus diesem einen Skript: die Modelle (.glb mit Atlas-Bild), die Probebilder und die Messwerte. Gebaut auf
tools/models/psx.py (Pipeline, Nebel, Koernung) und den Tuerrahmen aus assets/models (nur gelesen, nicht veraendert).

Modelle (Spiel-Achsen: x seitlich, y oben, z vorn; Meter; Ursprung bei beiden: Mitte der Tuerebene auf dem Boden):
  gitter_breit    Gitter fuer Normal- und Verschlossen-Rahmen (lichte Weite 1,79 / 1,73): 1,70 breit, 3,50 hoch, 9 Staebe
  gitter_schmal   Gitter fuer den Boss-Rahmen (Pfostenabstand 0,98): 0,94 breit, 3,50 hoch, 5 Staebe
  fuehrung_normal Sturzring (Stein) im Bogen der Tuerebene, Normal und Verschlossen
  fuehrung_boss   dasselbe fuer den Boss-Rahmen
Das Gitter wird als Ganzes in y verschoben (Ursprung = Unterkante, Mitte); die Fuehrung bleibt stehen.

Aufruf (aus dem Wurzelverzeichnis; TEMP vorher setzen):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/fallgitter-540/fallgitter.py
Umgebungsvariablen: FG_OUT (Ausgabeordner, Vorgabe: dieser Ordner), FG_MODELS_ONLY=1 (nur Modelle, fuer den Hash-Vergleich).
Zwei Laeufe liefern gleiche Dateien (kein Zufall; die Fleckung der Textur nutzt einen festen Seed).
"""

import json
import math
import os
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
sys.path.insert(0, os.path.join(ROOT, "tools", "models"))
import blf  # noqa: E402
import bpy  # noqa: E402
import imbuf  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Matrix  # noqa: E402
from psx import (V, G, Part, rects_from_pixels, save_image, stone_textures, pass_material, write_light,  # noqa: E402
                 setup_render, camera, aim, render_passes, compose, save_png, export_material, export_glb, MATS, runs)

OUT = os.environ.get("FG_OUT", HERE)
MODELS_ONLY = os.environ.get("FG_MODELS_ONLY") == "1"
TMP = os.path.join(tempfile.gettempdir(), "fallgitter540_tmp")
os.makedirs(TMP, exist_ok=True)
TEXTURES = os.path.join(ROOT, "assets", "textures")
MODELS = os.path.join(ROOT, "assets", "models")
WALL_FILES = {"a": "catacomb_wall.png", "b": "catacomb_wall_b.png", "c": "catacomb_wall_c.png"}
BG = np.array((30, 26, 34), float)

# --- Masse ----------------------------------------------------------------------------------------------------------
BAR = 0.035                  # halbe Stabdicke (Stab 0,07 x 0,07, vierkant)
SPIKE = 0.22                 # Laenge der Spitze unten (vierseitige Pyramide)
BAR_TOP = 3.46               # Staebe enden im oberen Querband
HEIGHT = 3.5
BANDS = (0.62, 1.55, 2.48)   # Mitte der Querbaender, je 0,10 hoch
BAND_H = 0.05                # halbe Hoehe
BAND_Z = 0.05                # halbe Tiefe (Stab: 0,035) - das Band umfasst die Staebe
RAIL = (3.38, 3.50)          # oberer Abschluss, liegt im Zustand "zu" hinter dem Bogenscheitel (3,32)
SHAPES = {"breit": (0.85, 9), "schmal": (0.47, 5)}   # halbe Breite, Staebe
LIFT = {"zu": 0.0, "halb": 1.7, "offen": 3.4}


# --- Palette und Texturen --------------------------------------------------------------------------------------------

def row(name):
    with open(os.path.join(ROOT, "assets", "palette", "hauptpalette.json"), encoding="utf-8") as f:
        data = json.load(f)
    return {r["name"]: np.array(r["farben"], int) for r in data["grund"] + data["akzent"]}[name]


def iron_rows(which):
    eisen = row("Eisen")
    if which == "eisen":
        return eisen
    # Vorschlag ausserhalb der Palette: gleiche Helligkeit, Blau fast weg (neutrales Grau)
    lum = eisen @ np.array((0.299, 0.587, 0.114))
    return np.clip(np.round(lum[:, None] * np.array((1.0, 1.0, 1.03))), 0, 255).astype(int)


def load_tile(name):
    im = bpy.data.images.load(os.path.join(TEXTURES, name))
    w, h = im.size
    a = np.array(im.pixels[:], dtype=float).reshape(h, w, 4)[..., :3] * 255.0
    bpy.data.images.remove(im)
    return np.round(a[::-1]).astype(int)


G_SIZE = (64, 64)
G_RECTS = {"bar": (0, 0, 32, 64), "band": (32, 0, 64, 64)}
G_UV = rects_from_pixels(G_RECTS, G_SIZE)


def gitter_atlas(which="eisen"):
    """64 x 64: links der Stab (u quer ueber die Breite: Lichtkante links, dunkel rechts), rechts das Band (v quer: oben hell)."""
    iron = iron_rows(which).astype(float)
    rng = np.random.default_rng(540)
    bar = np.tile(iron[4], (64, 32, 1))
    bar[:, 0:4] = iron[5]
    bar[:, 26:30] = iron[3]
    bar[:, 30:32] = iron[2]
    speck = rng.random((64, 32)) < 0.07
    speck[:, :4] = speck[:, 26:] = False
    bar[speck] = iron[3]
    band = np.tile(iron[4], (64, 32, 1))
    band[56:64] = iron[5]          # Zeile 0 unten: oben hell
    band[14:26] = iron[3]
    band[0:14] = iron[2]
    speck = rng.random((64, 32)) < 0.06
    speck[56:] = False
    band[speck] = iron[3]
    out = np.zeros((64, 64, 3))
    out[0:64, 0:32] = bar
    out[0:64, 32:64] = band
    return out


F_RECTS = {"stone": (0, 0, 64, 64)}
F_UV = rects_from_pixels(F_RECTS, (64, 64))


def fuehrung_atlas(variant):
    return load_tile(WALL_FILES[variant])[::-1].astype(float)


def colours_of(tex):
    return len({tuple(p) for p in np.round(tex).reshape(-1, 3).astype(int)})


def check_palette(tex, rows):
    """Jede Farbe des Atlas muss in den genannten Reihen der Hauptpalette stehen."""
    allowed = {tuple(int(c) for c in p) for r in rows for p in r}
    used = {tuple(int(c) for c in p) for p in np.round(tex).reshape(-1, 3)}
    assert used <= allowed, sorted(used - allowed)


# --- Gitter ----------------------------------------------------------------------------------------------------------

def bar(part, cx, off):
    """Ein Stab: vierkantiger Schaft mit Pyramidenspitze unten, als ein einziger konvexer Koerper (14 Dreiecke)."""
    ox, oy, oz = off
    y0, y1 = SPIKE, BAR_TOP
    cs = [(cx - BAR, -BAR), (cx + BAR, -BAR), (cx + BAR, BAR), (cx - BAR, BAR)]
    P = lambda x, y, z: V(x + ox, y + oy, z + oz)                         # noqa: E731
    tip = P(cx, 0.0, 0.0)
    polys = []
    for (xa, za), (xb, zb) in zip(cs, cs[1:] + cs[:1]):
        polys.append([P(xa, y0, za), P(xb, y0, zb), P(xb, y1, zb), P(xa, y1, za)])
        polys.append([P(xa, y0, za), P(xb, y0, zb), tip])
    polys.append([P(x, y1, z) for x, z in cs])
    part.convex(polys, "bar")


def build_gitter(shape, off=(0.0, 0.0, 0.0)):
    hw, n = SHAPES[shape]
    part = Part("gitter_" + shape, off, G_UV)
    span = hw - BAR
    for i in range(n):
        bar(part, -span + 2 * span * i / (n - 1), off)
    ox, oy, oz = off
    for y in BANDS:
        part.box((-(hw + 0.005) + ox, y - BAND_H + oy, -BAND_Z + oz), ((hw + 0.005) + ox, y + BAND_H + oy, BAND_Z + oz), "band")
    part.box((-(hw + 0.005) + ox, RAIL[0] + oy, -BAND_Z + oz), ((hw + 0.005) + ox, RAIL[1] + oy, BAND_Z + oz), "band")
    return part


# --- Fuehrung (Sturzring) --------------------------------------------------------------------------------------------

# frame: (Kreismittelpunkt x, R innen, R aussen, Ansatz, halbe Tiefe) nach tools/models/door.py (stufenportal, innerster Lauf)
RINGS = {"normal": (1.0 - 1.5625, 1.4375, 1.5625, 2.0, 0.07),
         "boss": (1.0 - 1.75, 1.375, 1.5, 1.875, 0.1)}


def arc_pts(xc, R, spring, n=6):
    rise = math.sqrt(R * R - xc * xc)
    a_end = math.atan2(rise, -xc)
    return [(xc + R * math.cos(a_end * j / n), spring + R * math.sin(a_end * j / n)) for j in range(n + 1)]


def face(part, pts, hint):
    """Eine ebene Flaeche mit Welt-UV (eine 64-px-Kachel je Meter), nicht an Metergrenzen geteilt: die Kachel wird auf die
    Zelle der kleinsten Ecke festgelegt und die Ecken darueber hinaus werden auf den Rand gedrueckt (die Flaechen sind kleiner
    als 0,25 m). So entstehen keine T-Knoten und das Netz bleibt geschlossen."""
    P = [V(*p) for p in pts]
    n = (P[1] - P[0]).cross(P[2] - P[0])
    if n.dot(V(*hint)) < 0:
        P.reverse()
        n = -n
    n = n.normalized()
    ax = max(range(3), key=lambda i: abs(n[i]))
    if ax == 2:
        pu, pv = (lambda p: p.x if n.z > 0 else -p.x), (lambda p: p.y)
    elif ax == 0:
        pu, pv = (lambda p: -p.z if n.x > 0 else p.z), (lambda p: p.y)
    else:
        pu, pv = (lambda p: p.x if n.y > 0 else -p.x), (lambda p: p.z)
    us, vs = [pu(p) for p in P], [pv(p) for p in P]
    i0, j0 = math.floor(min(us) + 1e-9), math.floor(min(vs) + 1e-9)
    u0, v0, u1, v1 = part.rects["stone"]
    verts = [part.bm.verts.new(G(p - part.pivot)) for p in P]
    uvs = [(u0 + (u1 - u0) * min(max(u - i0, 0), 1), v0 + (v1 - v0) * min(max(v - j0, 0), 1)) for u, v in zip(us, vs)]
    part._face(verts, uvs)


def build_fuehrung(kind, off=(0.0, 0.0, 0.0)):
    """Bogenring (Spitzbogen aus zwei Kreisen wie der innerste Lauf des Rahmens), 1/8 breit, so tief wie die Tuerebene
    (z = +-hz). Innenflaeche setzt die Leibung des Rahmens ueber die Tuerebene fort; rundum geschlossen."""
    xc, r_in, r_out, spring, hz = RINGS[kind]
    ox, oy, oz = off
    part = Part("fuehrung_" + kind, off, F_UV)
    ri, ro = arc_pts(xc, r_in, spring), arc_pts(xc, r_out, spring)
    seq_i = ri + [(-x, y) for x, y in reversed(ri[:-1])]          # rechter Ansatz -> Scheitel -> linker Ansatz
    seq_o = ro + [(-x, y) for x, y in reversed(ro[:-1])]
    cen = [(xc, spring)] * 6 + [(-xc, spring)] * 6                  # Kreismitte je Segment (rechte, linke Haelfte)
    w = lambda x, y, z: (x + ox, y + oy, z + oz)                    # noqa: E731
    for k in range(len(seq_i) - 1):
        (xa, ya), (xb, yb), (xc2, yc2), (xd, yd) = seq_i[k], seq_i[k + 1], seq_o[k + 1], seq_o[k]
        mx, my = (xa + xb) / 2, (ya + yb) / 2
        to_c = (cen[k][0] - mx, cen[k][1] - my, 0)                   # zum Kreismittelpunkt = in die Oeffnung
        face(part, [w(xa, ya, -hz), w(xb, yb, -hz), w(xb, yb, hz), w(xa, ya, hz)], to_c)                       # innen
        face(part, [w(xd, yd, -hz), w(xc2, yc2, -hz), w(xc2, yc2, hz), w(xd, yd, hz)], (-to_c[0], -to_c[1], 0))   # aussen
        face(part, [w(xa, ya, hz), w(xb, yb, hz), w(xc2, yc2, hz), w(xd, yd, hz)], (0, 0, 1))                  # vorn
        face(part, [w(xa, ya, -hz), w(xb, yb, -hz), w(xc2, yc2, -hz), w(xd, yd, -hz)], (0, 0, -1))              # hinten
    for k in (0, len(seq_i) - 1):                                    # Enden auf der Ansatzlinie
        (xa, ya), (xd, yd) = seq_i[k], seq_o[k]
        face(part, [w(xa, ya, -hz), w(xd, yd, -hz), w(xd, yd, hz), w(xa, ya, hz)], (0, -1, 0))
    return part


# --- Pruefung ---------------------------------------------------------------------------------------------------------

def edge_report(part):
    """Kanten je Netz (Ecken gleicher Lage zusammengelegt): offene Kanten und Kanten mit mehr als zwei Flaechen."""
    keys, edges = {}, {}
    for f in part.bm.faces:
        ids = [keys.setdefault(tuple(round(c * 1e5) for c in v.co), len(keys)) for v in f.verts]
        for a, b in zip(ids, ids[1:] + ids[:1]):
            k = (min(a, b), max(a, b))
            edges[k] = edges.get(k, 0) + 1
    return sum(1 for n in edges.values() if n == 1), sum(1 for n in edges.values() if n > 2)


def triangles(part):
    return sum(len(f.verts) - 2 for f in part.bm.faces)


def export_models():
    rows = []
    d = os.path.join(OUT, "modelle")
    os.makedirs(d, exist_ok=True)

    def one(name, tex, rects_rows, make):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        coll = bpy.data.collections.new("Modell")
        bpy.context.scene.collection.children.link(coll)
        assert colours_of(tex) <= 40, colours_of(tex)
        check_palette(tex, rects_rows)
        image = save_image(name, tex, os.path.join(d, name + ".png"))
        part = make()
        o_edges, multi = edge_report(part)
        tris = triangles(part)
        lo = [min(v.co[i] for v in part.bm.verts) for i in range(3)]
        hi = [max(v.co[i] for v in part.bm.verts) for i in range(3)]
        obj = part.build(coll, export_material("m", image), smooth=False)
        export_glb([obj], os.path.join(d, name + ".glb"))
        rows.append((name, tris, o_edges, multi, colours_of(tex), lo, hi))

    for shape in SHAPES:
        one("gitter_" + shape, gitter_atlas(), [row("Eisen")], lambda s=shape: build_gitter(s))
    for kind in RINGS:
        for v in WALL_FILES:
            one("fuehrung_%s_%s" % (kind, v), fuehrung_atlas(v), [row("Stein")], lambda k=kind: build_fuehrung(k))
    for r in rows:
        # Blender-Achsen (x, -z_spiel, y_spiel) in der Ausgabe zurueckgerechnet
        print("MODELL %s: %d Dreiecke, offene Kanten %d, mehrfach %d, Farben %d, x %.3f..%.3f, y %.3f..%.3f, z %.3f..%.3f" % (
            r[0], r[1], r[2], r[3], r[4], r[5][0], r[6][0], r[5][2], r[6][2], -r[6][1], -r[5][1]))
    return rows


# --- Szenen ----------------------------------------------------------------------------------------------------------

IMG = {}


def new_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    IMG.clear()
    coll = bpy.data.collections.new("Szene")
    bpy.context.scene.collection.children.link(coll)
    return coll


def img(key, make):
    if key not in IMG:
        IMG[key] = save_image("t_" + key, make(), os.path.join(TMP, "t_%s.png" % key))
    return IMG[key]


def emit(part, pts, hint, field):
    """Flaeche mit Welt-UV, an den Metergrenzen geteilt (aus door.py, fuer Waende der Szene)."""
    P = [V(*p) for p in pts]
    n = None
    for i in range(1, len(P) - 1):
        c = (P[i] - P[0]).cross(P[i + 1] - P[0])
        if c.length > 1e-9:
            n = c
            break
    if n is None:
        return
    if n.dot(V(*hint)) < 0:
        P.reverse()
        n = -n
    n = n.normalized()
    ax = max(range(3), key=lambda i: abs(round(n[i], 6)))
    if ax == 2:
        pu, pv = (lambda p: p.x if n.z > 0 else -p.x), (lambda p: p.y)
    elif ax == 0:
        pu, pv = (lambda p: -p.z if n.x > 0 else p.z), (lambda p: p.y)
    else:
        pu, pv = (lambda p: p.x if n.y > 0 else -p.x), (lambda p: p.z)
    poly = [(pu(p), pv(p), p) for p in P]
    i0, i1 = math.floor(min(q[0] for q in poly) + 1e-9), math.ceil(max(q[0] for q in poly) - 1e-9)
    j0, j1 = math.floor(min(q[1] for q in poly) + 1e-9), math.ceil(max(q[1] for q in poly) - 1e-9)
    u0, v0, u1, v1 = part.rects[field]

    def clip(pl, axis, bound, keep_ge):
        out = []
        for i, cur in enumerate(pl):
            prev = pl[i - 1]
            c_in = cur[axis] >= bound - 1e-9 if keep_ge else cur[axis] <= bound + 1e-9
            p_in = prev[axis] >= bound - 1e-9 if keep_ge else prev[axis] <= bound + 1e-9
            if c_in != p_in:
                t = (bound - prev[axis]) / (cur[axis] - prev[axis])
                out.append((prev[0] + t * (cur[0] - prev[0]), prev[1] + t * (cur[1] - prev[1]),
                            prev[2] + (cur[2] - prev[2]) * t))
            if c_in:
                out.append(cur)
        return out

    for i in range(i0, max(i1, i0 + 1)):
        for j in range(j0, max(j1, j0 + 1)):
            cell = clip(clip(clip(clip(poly, 0, i, True), 0, i + 1, False), 1, j, True), 1, j + 1, False)
            cell = [q for k, q in enumerate(cell) if k == 0 or abs(q[0] - cell[k - 1][0]) + abs(q[1] - cell[k - 1][1]) > 1e-7]
            if len(cell) >= 2 and abs(cell[0][0] - cell[-1][0]) + abs(cell[0][1] - cell[-1][1]) < 1e-7:
                cell = cell[:-1]
            if len(cell) < 3:
                continue
            area = sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(cell, cell[1:] + cell[:1])) / 2
            if abs(area) < 1e-6:
                continue
            verts = [part.bm.verts.new(G(q[2] - part.pivot)) for q in cell]
            uvs = [(u0 + (u1 - u0) * min(max(q[0] - i, 0), 1), v0 + (v1 - v0) * min(max(q[1] - j, 0), 1)) for q in cell]
            part._face(verts, uvs)


def add_plane(coll, pts, hint, tag):
    part = Part("flaeche", (0, 0, 0), F_UV)
    emit(part, pts, hint, "stone")
    return part.build(coll, pass_material("w_" + tag, img("wand", lambda: fuehrung_atlas("a"))), smooth=False)


def add_floor(coll, x0=-2.5, x1=2.5, z0=-14, z1=14):
    tex = stone_textures(TMP)["floor"]
    part = Part("boden", (0, 0, 0), {"all": (0.0, 0.0, 1.0, 1.0)})
    verts = [part.bm.verts.new(G(V(x, 0, z))) for x, z in ((x0, z0), (x1, z0), (x1, z1), (x0, z1))]
    part._face(verts, [(x0, z0), (x1, z0), (x1, z1), (x0, z1)])
    return part.build(coll, pass_material("boden", tex), smooth=False)


def add_door(coll, kind):
    """Tuerrahmen aus assets/models (nur gelesen), so verschoben, dass die vordere Wandflaeche auf z = 0 liegt; Tuerebene z = -1."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(MODELS, "door_frame_%s_a.glb" % kind))
    objs = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    for o in objs:
        im = None
        for m in o.data.materials:
            for n in m.node_tree.nodes:
                if n.type == "TEX_IMAGE":
                    im = n.image
        o.data.materials.clear()
        o.data.materials.append(pass_material("d_" + o.name, im))
        o.parent = None
        o.matrix_world = Matrix.Translation(G(V(0, 0, -1)))
        coll.objects.link(o)
        for c in list(o.users_collection):
            if c is not coll:
                c.objects.unlink(o)
    return objs


def add_box(coll, name, lo, hi, which):
    colour = {"figur": row("Stoff")[5], "ziegel": row("Holz")[4]}[which]
    tex = np.tile(colour.astype(float), (8, 8, 1))
    part = Part(name, (0, 0, 0), {"all": (0.0, 0.0, 1.0, 1.0)})
    part.box(lo, hi, "all")
    return part.build(coll, pass_material(name, img("fig_" + which, lambda: tex)), smooth=False)


def build_scene(items):
    coll = new_scene()
    objs, named = [], {}
    n = 0
    for it in items:
        k = it[0]
        if k == "floor":
            objs.append(add_floor(coll))
        elif k == "plane":
            n += 1
            objs.append(add_plane(coll, it[1], it[2], str(n)))
        elif k == "door":
            objs += add_door(coll, it[1])
        elif k == "gitter":
            shape, lift, which = it[1], it[2], it[3]
            part = build_gitter(shape, (0.0, lift, -1.0))
            o = part.build(coll, pass_material("gitter", img("gitter_" + which, lambda w=which: gitter_atlas(w))), smooth=False)
            objs.append(o)
            named["gitter"] = o
        elif k == "fuehrung":
            part = build_fuehrung(it[1], (0.0, 0.0, -1.0))
            objs.append(part.build(coll, pass_material("fuehrung", img("wand", lambda: fuehrung_atlas("a"))), smooth=False))
        elif k == "figur":
            x, z, h = it[1], it[2], it[3]
            objs.append(add_box(coll, "figur", (x - 0.25, 0, z - 0.15), (x + 0.25, h, z + 0.15), "figur"))
    bpy.context.view_layer.update()
    for o in objs:
        o["smooth"] = False
        write_light(o)
    return objs, named


def world(frame="normal", shape="breit", lift=0.0, iron="eisen", extra=(), gitter=True):
    """Raum vor der Tuer (5 m breit), die Tuerzelle (2 m tief, Tuerebene bei z = -1) und der Gang dahinter (2 m breit)."""
    H = 3.5
    items = [("floor",)]
    q = lambda *p: [tuple(c) for c in p]                      # noqa: E731
    items += [("plane", q((-2.5, 0, 0), (-1, 0, 0), (-1, H, 0), (-2.5, H, 0)), (0, 0, 1)),         # Wand vorn links und rechts
              ("plane", q((1, 0, 0), (2.5, 0, 0), (2.5, H, 0), (1, H, 0)), (0, 0, 1)),
              ("plane", q((-2.5, 0, -2), (-1, 0, -2), (-1, H, -2), (-2.5, H, -2)), (0, 0, -1)),    # Wand hinten
              ("plane", q((1, 0, -2), (2.5, 0, -2), (2.5, H, -2), (1, H, -2)), (0, 0, -1)),
              ("plane", q((-1, 0, -2), (-1, 0, 0), (-1, H, 0), (-1, H, -2)), (1, 0, 0)),           # Seiten der Zelle
              ("plane", q((1, 0, -2), (1, 0, 0), (1, H, 0), (1, H, -2)), (-1, 0, 0)),
              ("plane", q((-1, H, -2), (1, H, -2), (1, H, 0), (-1, H, 0)), (0, -1, 0)),            # Decke der Zelle
              ("plane", q((-2.5, 0, 0), (-2.5, 0, 14), (-2.5, H, 14), (-2.5, H, 0)), (1, 0, 0)),   # Raum vorn
              ("plane", q((2.5, 0, 0), (2.5, 0, 14), (2.5, H, 14), (2.5, H, 0)), (-1, 0, 0)),
              ("plane", q((-2.5, H, 0), (2.5, H, 0), (2.5, H, 14), (-2.5, H, 14)), (0, -1, 0)),
              ("plane", q((-1, 0, -14), (-1, 0, -2), (-1, H, -2), (-1, H, -14)), (1, 0, 0)),        # Gang dahinter
              ("plane", q((1, 0, -14), (1, 0, -2), (1, H, -2), (1, H, -14)), (-1, 0, 0)),
              ("plane", q((-1, H, -14), (1, H, -14), (1, H, -2), (-1, H, -2)), (0, -1, 0)),
              ("plane", q((-1, 0, -14), (1, 0, -14), (1, H, -14), (-1, H, -14)), (0, 0, 1)),
              ("door", frame), ("fuehrung", "boss" if frame == "boss" else "normal")]
    if gitter:
        items.append(("gitter", shape, lift, iron))
    return items + list(extra)


def render(items, loc, tgt, fov, size, ortho=None, only=None):
    objs, named = build_scene(items)
    cam = camera()
    setup_render(*size)
    if ortho:
        cam.data.type = "ORTHO"
        cam.data.ortho_scale = ortho
    else:
        cam.data.angle = math.radians(fov)
    aim(cam, G(V(*loc)), G(V(*tgt)))
    visible = [named[n] for n in only] if only else None
    return render_passes("fg540", visible)


def to_image(p, mode):
    if mode == "hell":
        base, light = p["base"][..., :3] * 255, p["light"][..., :1]
        cov = p["base"][..., 3:4] > 0.5
        out = np.where(cov, np.clip(base * light * 1.3, 0, 255), BG)
    else:
        out = compose(p, "spiel")
    return np.round(out[::-1])                                       # Zeile 0 oben


def shot(items, loc, tgt, fov, size, mode="hell", ortho=None):
    return to_image(render(items, loc, tgt, fov, size, ortho), mode)


# --- Beschriftung -----------------------------------------------------------------------------------------------------

def text(im, s, x, y, size=18, color=(235, 230, 240)):
    f = blf.load("C:/Windows/Fonts/arial.ttf")
    blf.size(f, size)
    w, h = blf.dimensions(f, s)
    wd, hd = int(w) + 8, int(size * 1.6)
    ib = imbuf.new((wd, hd))
    blf.color(f, 1, 1, 1, 1)
    blf.position(f, 4, int(size * 0.4), 0)
    with blf.bind_imbuf(f, ib):
        blf.draw_buffer(f, s)
    assert ib.size == (wd, hd), (ib.size, wd, hd)
    tmp = os.path.join(TMP, "txt.png")
    ib.filepath = tmp
    imbuf.write(ib)
    t = bpy.data.images.load(tmp)
    t.colorspace_settings.name = "Non-Color"
    a = np.array(t.pixels[:], np.float32).reshape(hd, wd, 4)[::-1, :, 3]
    bpy.data.images.remove(t)
    hh, ww = im.shape[:2]
    ye, xe = min(hh, y + hd), min(ww, x + wd)
    if ye <= y or xe <= x:
        return
    mk = (a[:ye - y, :xe - x] > 0.5)[..., None]
    im[y:ye, x:xe] = np.where(mk, np.array(color, float), im[y:ye, x:xe])


def png(im, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    save_png(np.asarray(im, float)[::-1], path)


def hcat(images, gap=6):
    h = max(a.shape[0] for a in images)
    pads = [a if a.shape[0] == h else np.concatenate([a, np.tile(BG, (h - a.shape[0], a.shape[1], 1))], 0) for a in images]
    out = [pads[0]]
    for a in pads[1:]:
        out += [np.tile(BG * 0.5, (h, gap, 1)), a]
    return np.concatenate(out, axis=1)


def vcat(images, gap=6):
    w = max(a.shape[1] for a in images)
    pads = [a if a.shape[1] == w else np.concatenate([a, np.tile(BG, (a.shape[0], w - a.shape[1], 1))], 1) for a in images]
    out = [pads[0]]
    for a in pads[1:]:
        out += [np.tile(BG * 0.5, (gap, w, 1)), a]
    return np.concatenate(out, axis=0)


def titled(im, s, size=18):
    im = im.copy()
    text(im, s, 6, 4, size)
    return im


def half(im):
    return im.reshape(im.shape[0] // 2, 2, im.shape[1] // 2, 2, 3).mean(axis=(1, 3))


# --- Ansichten --------------------------------------------------------------------------------------------------------

EYE = 1.6
SPIEL = (854, 480)


def cam_pos(view, dist):
    """Kamera vor der Tuerebene (z = -1): frontal oder schraeg von rechts, Augenhoehe 1,6 m, Blick auf die Tuermitte."""
    z = -1 + dist
    if view == "frontal":
        return (0.0, EYE, z)
    x = 1.3 if dist < 5 else 2.0
    return (x, EYE, z - 0.1)


def view_state(state, view, dist, mode, **kw):
    items = world(lift=LIFT[state], **kw)
    return shot(items, cam_pos(view, dist), (0, EYE, -1), 70, SPIEL, mode)


def view_single(which, size=(360, 640), parts=("gitter",), shape="breit", frame="normal"):
    items = [("floor",)]
    if "gitter" in parts:
        items.append(("gitter", shape, 0.0, "eisen"))
    if "fuehrung" in parts:
        items.append(("fuehrung", "boss" if frame == "boss" else "normal"))
    cy, scale = HEIGHT / 2 - 1.0 + 1.0, 4.0
    if which == "vorn":
        return shot(items, (0, cy, 8), (0, cy, -1), 0, size, "hell", ortho=scale)
    if which == "seite":
        return shot(items, (8, cy, -1), (0, cy, -1), 0, size, "hell", ortho=scale)
    if which == "hinten":
        return shot(items, (0, cy, -10), (0, cy, -1), 0, size, "hell", ortho=scale)
    return shot(items, (2.6, 4.8, 2.4), (0, 1.6, -1), 38, size, "hell")


def view_detail():
    items = world(lift=0.0)
    out = []
    for lbl, loc, tgt, fov in (("Spitzen", (0.75, 0.55, 0.0), (0.35, 0.3, -1.0), 38),
                               ("Querband und Staebe", (0.8, 1.7, 0.1), (0.4, 1.55, -1.0), 38),
                               ("Spitzen von vorn, tief", (0.0, 0.3, 0.3), (0.0, 0.3, -1.0), 50)):
        out.append(titled(shot(items, loc, tgt, fov, (480, 480), "hell"), lbl))
    return hcat(out)


def view_through():
    """Blick durch das geschlossene Gitter in den Gang; im Gang steht eine Figur (Quader 0,5 x 1,8 x 0,3) in 5 m Abstand."""
    extra = [("figur", 0.0, -6.0, 1.8)]
    out = []
    for mode in ("spiel", "hell"):
        out.append(titled(shot(world(extra=extra), (0.0, EYE, 1.5), (0, EYE, -1), 70, SPIEL, mode),
                          "Blick durch das Gitter, 2,5 m davor, " + ("Spielbild" if mode == "spiel" else "hell")))
    return vcat(out)


def view_size():
    out = []
    for lbl, lift, fx, fz in (("zu, Figur 1,8 m daneben", 0.0, 1.75, 0.6), ("halb offen (Unterkante 1,7 m), Figur davor", 1.7, 0.0, -0.45)):
        items = world(lift=lift, extra=[("figur", fx, fz, 1.8)])
        out.append(titled(shot(items, (0, 1.75, 11), (0, 1.75, -1), 0, (640, 480), "hell", ortho=4.2), lbl))
    return hcat(out)


def view_frames():
    out = []
    for frame, shape in (("normal", "breit"), ("locked", "breit"), ("boss", "schmal")):
        out.append(titled(shot(world(frame=frame, shape=shape), (0, 1.75, 11), (0, 1.75, -1), 0, (400, 480), "hell", ortho=4.2),
                          "Rahmen %s, Gitter %s" % (frame, shape)))
    out2 = []
    for frame, shape in (("normal", "breit"), ("locked", "breit"), ("boss", "schmal")):
        out2.append(titled(shot(world(frame=frame, shape=shape, lift=1.7), (1.4, 1.6, 2.2), (0, 1.8, -1), 60, (400, 480), "hell"),
                           "%s, halb offen, schraeg" % frame))
    return vcat([hcat(out), hcat(out2)])


def view_iron():
    rows = []
    for mode in ("spiel", "hell"):
        pair = []
        for which, lbl in (("eisen", "Eisen der Hauptpalette (bläulich)"),
                           ("neutral", "VORSCHLAG, ausserhalb der Palette: neutrales Grau")):
            pair.append(titled(shot(world(iron=which), cam_pos("schraeg", 3.0), (0, EYE, -1), 70, SPIEL, mode),
                               lbl + (", Spielbild" if mode == "spiel" else ", hell")))
        rows.append(hcat(pair))
    return vcat(rows)


def measure(dist):
    """Zahl, Breite und Abstand der Staebe in Pixeln (nur das Gitter gerendert, frontal), Zeilen um 1,1 m Hoehe."""
    p = render(world(), (0, EYE, -1 + dist), (0, EYE, -1), 70, SPIEL, only=["gitter"])
    cov = p["base"][..., 3] > 0.5
    h = cov.shape[0]
    r0 = int(h / 2 - (EYE - 1.1) / (2 * dist * math.tan(math.radians(35))) * h)
    widths, gaps, counts = [], [], []
    for r in range(r0 - 8, r0 + 9):
        rr = runs(cov[r])
        xs = np.flatnonzero(cov[r])
        counts.append(len(rr))
        widths += rr
        inner = ~cov[r][xs[0]:xs[-1] + 1]
        gaps += runs(inner)
    return dict(dist=dist, bars=sorted(set(counts)), width_min=min(widths), width_med=float(np.median(widths)),
                gap_min=min(gaps), gap_med=float(np.median(gaps)))


def main():
    rows = export_models()
    if MODELS_ONLY:
        return
    d = os.path.join(OUT, "zustaende")
    for state in LIFT:
        for view in ("frontal", "schraeg"):
            for dist in (3.0, 8.0):
                for mode in ("spiel", "hell"):
                    name = "%s-%s-%dm-%s.png" % (state, view, int(dist), mode)
                    png(view_state(state, view, dist, mode), os.path.join(d, name))
    for dist in (3, 8):
        for mode in ("spiel", "hell"):
            tiles = []
            for view in ("frontal", "schraeg"):
                tiles.append(hcat([titled(half(view_state(s, view, float(dist), mode)), "%s, %s, %d m" % (s, view, dist), 14)
                                   for s in LIFT]))
            png(vcat(tiles), os.path.join(OUT, "uebersicht-%dm-%s.png" % (dist, mode)))
    png(view_detail(), os.path.join(OUT, "detail-spitzen-querband.png"))
    png(view_through(), os.path.join(OUT, "durchblick-gang.png"))
    png(view_size(), os.path.join(OUT, "groessenvergleich.png"))
    png(view_frames(), os.path.join(OUT, "rahmen-vergleich.png"))
    png(view_iron(), os.path.join(OUT, "vergleich-eisen.png"))
    png(hcat([titled(view_single(w), w) for w in ("vorn", "seite", "hinten", "oben")]), os.path.join(OUT, "einzel-gitter.png"))
    png(hcat([titled(view_single(w, parts=("fuehrung",)), w) for w in ("vorn", "seite", "hinten", "oben")]),
        os.path.join(OUT, "einzel-fuehrung.png"))
    png(hcat([titled(view_single(w, parts=("gitter", "fuehrung")), w) for w in ("vorn", "seite", "hinten", "oben")]),
        os.path.join(OUT, "einzel-zusammen.png"))
    png(hcat([titled(view_single(w, shape="schmal"), w) for w in ("vorn", "seite", "hinten", "oben")]),
        os.path.join(OUT, "einzel-gitter-schmal.png"))
    for dist in (3.0, 8.0):
        print("MESSUNG", measure(dist))


main()
