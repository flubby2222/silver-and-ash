"""Treppe zur naechsten Ebene (Probestueck, Issue #300): zwei Entwuerfe, je Wandvariante a, b, c.

  spiral  Wendeltreppe aus Stein um eine achteckige Spindel, 12 Stufen (4,0 m hoch wie heute), oben ein Werkstein-Kapitell und eine Lampe.
  abgang  Abgangsportal: freistehende Bogenwand (3,5 m, 0,375 dick) mit spitzem Durchgang, den eine dunkle Flaeche fuellt, davor auf
          beiden Seiten drei Stufen zur Schwelle; Werkstein nur als Bogenring und Gesims, unten an der Schwelle glimmt es.

Beide stehen innerhalb des heutigen Kastens der Treppe (+-1,2 m, 4 m hoch), Ursprung = Mitte am Boden. Knoten: "stairs" (Stein, fest),
"glow" (Lampe bzw. Glimmen; das Spiel kann ihn getoent/pulsend zeichnen). Stein aus der Wandkachel (Waende) und der Bodenkachel
(Trittflaechen) der Variante, Textur-Koordinaten aus der Weltposition (eine 64-px-Kachel je Meter, an Metergrenzen geteilt, wie bei den
Tuerrahmen); der Mittelpunkt liegt auf einer Zellmitte (ungerade Meter), also bleibt das Raster erhalten.
Werkstein = die vier Stufen der Reihe "Werkstein" (Palette, PR Tuerrahmen), nur fuer Kapitell bzw. Deckel; Lampe = Messing/Gold.

Aufruf: blender --background --python treppe.py -- [Kachel-Ordner]   (Standard: Kacheln der Palette A, palette3/neu/A/textures)
"""
import json
import math
import os
import sys

import bpy
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
TEXDIR = ARGS[0] if ARGS else os.path.join(ROOT, "design", "proben", "grafik", "palette3", "neu", "A", "textures")
OUT = os.path.join(HERE, "modelle")
sys.path.insert(0, os.path.join(ROOT, "tools", "models"))
import psx  # noqa: E402

Part, V, G = psx.Part, psx.V, psx.G
PAL = json.load(open(os.path.join(ROOT, "assets", "palette", "hauptpalette.json"), encoding="utf-8"))
ROWS = {r["name"]: r["farben"] for r in PAL["grund"] + PAL["akzent"]}
STEIN = ROWS["Stein"]
WERKSTEIN = [(171, 159, 164), (183, 171, 176), (195, 182, 188), (209, 193, 200)]      # Reihe "Werkstein" (Palette mit PR Tuerrahmen)
MESSING = ROWS["Messing/Gold"]

RECTS = {"stone": (0, 64, 64, 128), "floor": (64, 64, 128, 128), "werk": (0, 0, 64, 64), "dark1": (64, 0, 80, 16),
         "dark2": (80, 0, 96, 16), "deep": (96, 0, 112, 16), "glow": (64, 16, 128, 48)}
W = H = 128
UV = psx.rects_from_pixels(RECTS, (W, H))
WALL = {"a": "catacomb_wall.png", "b": "catacomb_wall_b.png", "c": "catacomb_wall_c.png"}
FLOOR = {"a": "catacomb_floor.png", "b": "catacomb_floor_b.png", "c": "catacomb_floor_c.png"}


def load(name):
    im = bpy.data.images.load(os.path.join(TEXDIR, name))
    w, h = im.size
    return np.round(np.array(im.pixels[:]).reshape(h, w, 4)[::-1, :, :3] * 255).astype(int)      # oben zuerst


def werk_of(tile):
    """Dieselbe Kachel in den vier Stufen der Reihe Werkstein (nach der Helligkeit der Farben in Viertel geteilt)."""
    lum = tile @ np.array([0.3, 0.59, 0.11])
    uniq = sorted({tuple(c) for c in tile.reshape(-1, 3)}, key=lambda c: np.dot(c, [0.3, 0.59, 0.11]))
    rank = {c: min(3, int(4 * i / len(uniq))) for i, c in enumerate(uniq)}
    return np.array([WERKSTEIN[rank[tuple(c)]] for c in tile.reshape(-1, 3)]).reshape(tile.shape)


def atlas(variant):
    wall, floor = load(WALL[variant]), load(FLOOR[variant])
    a = np.zeros((H, W, 3))
    for name, t in (("stone", wall), ("floor", floor), ("werk", werk_of(wall))):
        x0, y0, x1, y1 = RECTS[name]
        a[y0:y1, x0:x1] = t[::-1]                                                   # Zeile 0 unten
    for name, colour in (("dark1", STEIN[2]), ("dark2", STEIN[1]), ("deep", STEIN[0])):
        x0, y0, x1, y1 = RECTS[name]
        a[y0:y1, x0:x1] = colour
    x0, y0, x1, y1 = RECTS["glow"]
    for row in range(y0, y1):                                                       # Verlauf von dunkel (unten) nach hell (oben)
        a[row, x0:x1] = MESSING[3 + min(4, (row - y0) * 5 // (y1 - y0))]
    return a, len({tuple(p) for p in a.reshape(-1, 3).astype(int)})


# --- Steinflaechen mit Welt-UV (wie rahmen.py der Tuerrahmen) ---------------------------------------------------------------

def _clip(poly, axis, bound, keep_ge):
    out = []
    for i, cur in enumerate(poly):
        prev = poly[i - 1]
        c_in = cur[axis] >= bound - 1e-9 if keep_ge else cur[axis] <= bound + 1e-9
        p_in = prev[axis] >= bound - 1e-9 if keep_ge else prev[axis] <= bound + 1e-9
        if c_in != p_in:
            t = (bound - prev[axis]) / (cur[axis] - prev[axis])
            out.append((prev[0] + t * (cur[0] - prev[0]), prev[1] + t * (cur[1] - prev[1]), prev[2] + (cur[2] - prev[2]) * t))
        if c_in:
            out.append(cur)
    return out


def emit(part, pts, hint, field):
    """Konvexe ebene Flaeche mit Normale nach `hint`; Waende: u am Wandverlauf, v mit der Hoehe; nach oben zeigende Steinflaechen
    nehmen die Bodenkachel (u = x, v = z). Eine Kachel je Meter, an den Metergrenzen geteilt."""
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
    ax = max(range(3), key=lambda i: abs(n[i]))
    if ax == 2:
        pu, pv = (lambda p: p.x if n.z > 0 else -p.x), (lambda p: p.y)
    elif ax == 0:
        pu, pv = (lambda p: -p.z if n.x > 0 else p.z), (lambda p: p.y)
    else:
        pu, pv = (lambda p: p.x if n.y > 0 else -p.x), (lambda p: p.z)
        if n.y > 0 and field == "stone":
            field = "floor"
    poly = [(pu(p), pv(p), p) for p in P]
    i0, i1 = math.floor(min(q[0] for q in poly) + 1e-9), math.ceil(max(q[0] for q in poly) - 1e-9)
    j0, j1 = math.floor(min(q[1] for q in poly) + 1e-9), math.ceil(max(q[1] for q in poly) - 1e-9)
    u0, v0, u1, v1 = UV[field]
    for i in range(i0, max(i1, i0 + 1)):
        for j in range(j0, max(j1, j0 + 1)):
            cell = _clip(_clip(_clip(_clip(poly, 0, i, True), 0, i + 1, False), 1, j, True), 1, j + 1, False)
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


def prism(part, pts, y0, y1, field, skip=(), top=True, bottom=False, top_field=None):
    """Prisma aus einem konvexen Grundriss (x, z); Seitenkanten in `skip` entfallen (innen liegend)."""
    cx, cz = sum(p[0] for p in pts) / len(pts), sum(p[1] for p in pts) / len(pts)
    for i, (a, b) in enumerate(zip(pts, pts[1:] + pts[:1])):
        if i in skip:
            continue
        mx, mz = (a[0] + b[0]) / 2 - cx, (a[1] + b[1]) / 2 - cz
        emit(part, [(a[0], y0, a[1]), (b[0], y0, b[1]), (b[0], y1, b[1]), (a[0], y1, a[1])], (mx, 0, mz), field)
    if top:
        emit(part, [(p[0], y1, p[1]) for p in pts], (0, 1, 0), top_field or field)
    if bottom:
        emit(part, [(p[0], y0, p[1]) for p in pts], (0, -1, 0), field)


def octagon(circ, rot=22.5):
    return [(circ * math.cos(math.radians(rot + 45 * k)), circ * math.sin(math.radians(rot + 45 * k))) for k in range(8)]


# --- Entwurf 1: Wendeltreppe -----------------------------------------------------------------------------------------------

def spiral():
    st, gl = Part("stairs", (0, 0, 0), UV), Part("glow", (0, 0, 0), UV)
    prism(st, octagon(1.1875), 0.0, 0.125, "stone")                                 # Sockel, zwei Stufen
    prism(st, octagon(1.0625), 0.125, 0.25, "stone")
    prism(st, octagon(0.3125), 0.25, 3.5, "stone", top=False)                       # Spindel
    r_in, r_out, rise, n = 0.25, 1.0625, 0.25, 12
    for i in range(n):                                                              # 12 Stufen, 45 Grad je Stufe
        a0, a1 = math.radians(45 * i), math.radians(45 * (i + 1))
        pts = [(r_in * math.cos(a0), r_in * math.sin(a0)), (r_out * math.cos(a0), r_out * math.sin(a0)),
               (r_out * math.cos(a1), r_out * math.sin(a1)), (r_in * math.cos(a1), r_in * math.sin(a1))]
        top = 0.5 + rise * i
        prism(st, pts, top - 0.25, top, "stone", skip=(3,), top=True)
    prism(st, octagon(0.5), 3.5, 3.75, "werk")                                      # Kapitell: Werkstein, nur hier
    prism(gl, octagon(0.3125), 3.75, 4.0, "glow")                                   # Lampe
    return [st, gl]


# --- Entwurf 2: Abgangsportal --------------------------------------------------------------------------------------------------

def arc_pts(xc, R, spring, n=6):
    rise = math.sqrt(R * R - xc * xc)
    a_end = math.atan2(rise, -xc)
    return [(xc + R * math.cos(a_end * j / n), spring + R * math.sin(a_end * j / n)) for j in range(n + 1)]


def fan(part, corner, chain, z, hint_z, field):
    """Faecher von einer Ecke aus ueber eine Punktkette (x, y) in der Ebene z."""
    for p, q in zip(chain, chain[1:]):
        emit(part, [(corner[0], corner[1], z), (p[0], p[1], z), (q[0], q[1], z)], (0, 0, hint_z), field)


def abgang():
    """Freistehende Bogenwand (0,375 dick, 3,5 m hoch) mit spitzem Durchgang, den eine dunkle Flaeche fuellt, davor auf beiden Seiten
    drei Stufen zur Schwelle; Werkstein nur als Bogenring und Gesims. Ausgang gelesen ueber Form (hohes Portal) und dunkle Oeffnung."""
    st, gl = Part("stairs", (0, 0, 0), UV), Part("glow", (0, 0, 0), UV)
    T, HW, HH, SILL, SPRING, R, TOP = 0.1875, 0.75, 1.1875, 0.5625, 1.6875, 1.25, 3.5
    OUTER = 1.1875
    xc = HW - R
    arc = arc_pts(xc, R, SPRING)
    for sz in (1, -1):
        z = sz * T
        for sx in (1, -1):                                                           # Wandflaeche beidseitig: Faecher von der oberen Ecke
            chain = [(sx * OUTER, 0.0), (sx * HW, 0.0), (sx * HW, SILL)] + [(sx * x, y) for x, y in arc] + [(0.0, TOP)]
            fan(st, (sx * OUTER, TOP), chain, z, sz, "stone")
        emit(st, [(-HW, 0, z), (HW, 0, z), (HW, SILL, z), (-HW, SILL, z)], (0, 0, sz), "stone")          # Schwelle (Vorderseite)
        # Leibung des Bogens und der Pfosten (von der Wandflaeche bis zur dunklen Flaeche in der Mitte)
        for sx in (1, -1):
            emit(st, [(sx * HW, SILL, z), (sx * HW, SPRING, z), (sx * HW, SPRING, 0), (sx * HW, SILL, 0)], (-sx, 0, 0), "stone")
            for j in range(len(arc) - 1):
                (x0, y0), (x1, y1) = arc[j], arc[j + 1]
                hint = (sx * (xc - (x0 + x1) / 2), SPRING - (y0 + y1) / 2, 0)
                emit(st, [(sx * x0, y0, z), (sx * x1, y1, z), (sx * x1, y1, 0), (sx * x0, y0, 0)], hint, "stone")
        # Schwelle oben (Bodenkachel), Gesims und Bogenring aus Werkstein (1/16 vor der Wand, Fase aussen)
        emit(st, [(-HW, SILL, 0), (HW, SILL, 0), (HW, SILL, z), (-HW, SILL, z)], (0, 1, 0), "stone")
        c = 0.0625
        ring_out = arc_pts(xc, R + 2 * c, SPRING)
        for sx in (1, -1):
            for j in range(len(arc) - 1):
                a, b = arc[j], arc[j + 1]
                o, p = ring_out[j], ring_out[j + 1]
                emit(st, [(sx * a[0], a[1], z), (sx * b[0], b[1], z), (sx * p[0], p[1], z), (sx * o[0], o[1], z)], (0, 0, sz), "werk")
            emit(st, [(sx * HW, 0.0, z), (sx * (HW + 2 * c), 0.0, z), (sx * (HW + 2 * c), SPRING, z), (sx * HW, SPRING, z)], (0, 0, sz), "werk")
        emit(st, [(-OUTER, TOP - 0.125, z), (OUTER, TOP - 0.125, z), (OUTER, TOP, z), (-OUTER, TOP, z)], (0, 0, sz), "werk")   # Gesims
    for sx in (1, -1):                                                                # Seiten der Wand und Oberseite
        emit(st, [(sx * OUTER, 0, -T), (sx * OUTER, 0, T), (sx * OUTER, TOP, T), (sx * OUTER, TOP, -T)], (sx, 0, 0), "stone")
    emit(st, [(-OUTER, TOP, -T), (OUTER, TOP, -T), (OUTER, TOP, T), (-OUTER, TOP, T)], (0, 1, 0), "werk")
    # dunkle Flaeche in der Oeffnung (beidseitig sichtbar) und Glimmen an der Schwelle
    chain = [(HW, SILL)] + arc + [(-x, y) for x, y in reversed(arc)] + [(-HW, SILL)]
    for sz in (1, -1):
        fan(st, (0.0, SILL), chain, 0.0, sz, "deep")
        emit(gl, [(-0.5, SILL + 0.06, sz * 0.02), (0.5, SILL + 0.06, sz * 0.02), (0.5, SILL + 0.16, sz * 0.02), (-0.5, SILL + 0.16, sz * 0.02)],
             (0, 0, sz), "glow")
    # drei Stufen je Seite (Trittflaechen: Bodenkachel)
    for sz in (1, -1):
        for z0, z1, top in ((T, 0.5, 0.5625), (0.5, 0.8125, 0.375), (0.8125, 1.1875, 0.1875)):
            a, b = sorted((sz * z0, sz * z1))
            box = [(-1.0625, a), (1.0625, a), (1.0625, b), (-1.0625, b)]
            prism(st, box, 0.0, top, "stone", skip=(), top=True)
    return [st, gl]


def build(kind, variant):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    coll = bpy.data.collections.new("Treppe")
    bpy.context.scene.collection.children.link(coll)
    name = f"stairs_{kind}_{variant}"
    tex, colours = atlas(variant)
    image = psx.save_image(name, tex, os.path.join(OUT, name + ".png"))
    mat = psx.export_material(name, image)
    objs = [p.build(coll, mat, smooth=False) for p in (spiral() if kind == "spiral" else abgang())]
    psx.export_glb(objs, os.path.join(OUT, name + ".glb"))
    psx.report_parts(name, objs)
    print(f"TEXTUR {name} {W}x{H} Farben {colours}")


def main():
    os.makedirs(OUT, exist_ok=True)
    for variant in "abc":
        for kind in ("spiral", "abgang"):
            build(kind, variant)


main()
