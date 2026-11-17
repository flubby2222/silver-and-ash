"""Tuerrahmen als erster Baustein (Probestueck, Teil von Issue #247; Regeln: design/proben/grafik/formenlehre/formenlehre.md).

Neu gegenueber tools/models/door.py (nur der Rahmen, die Fluegel bleiben): Knoten "portal" mit
  * Stein aus der WAND-KACHEL der Wandvariante (a, b, c): jede Steinflaeche bekommt ihre Textur-Koordinaten aus der Weltposition
    (eine 64-px-Kachel je Meter, wie SurfaceTexturing) und wird an den Metergrenzen in Stuecke geteilt; die Fugen laufen im
    Raster der Wand weiter. "werk" = dieselbe Kachel, in der Stein-Rampe der Palette A zwei Stufen heller.
  * Bogenlaeufe aus denselben Kreismittelpunkten (gleiche Breite 0,125), alle Masse im 1/16-Raster,
  * Kaempferplatte und Sockel vor der Wand (Fase an der Aussenkante), Pfosten aus Stein, Eisen nur als Beschlag.
Alle Teile und Namen wie bisher: portal, leaf_a, leaf_b / portal, leaf, lock (DoorPose bleibt unveraendert).

Aufruf: blender --background --python rahmen.py -- [Wandkachel-Ordner]   (Standard: Kacheln der Palette A aus palette3/neu/A/textures)
Schreibt modelle/door_{normal,locked,boss,bosshoch}_{a,b,c}.glb und .png. Das Skript aendert nichts in tools/ oder assets/.
"""
import json
import math
import os
import sys

import bmesh
import bpy
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
TEXDIR = ARGS[0] if ARGS else os.path.join(ROOT, "design", "proben", "grafik", "palette3", "neu", "A", "textures")
OUT = os.path.join(HERE, "modelle")
WERK_STEPS = int(os.environ.get("WERK_STEPS", "4"))

# --- Tuerskript aus main laden (ohne seinen main-Aufruf): Fluegel, Beschlag, Maler ----------------------------------------
_door = os.path.join(ROOT, "tools", "models", "door.py")
_src = open(_door, encoding="utf-8").read().replace("\nmain()\n", "\n").replace("__file__", repr(_door))
ns = {"__name__": "door_main"}
exec(compile(_src, "door.py", "exec"), ns)
Part, V, G = ns["Part"], ns["V"], None
sys.path.insert(0, os.path.join(ROOT, "tools", "models"))
import psx  # noqa: E402

G = psx.G

# --- Atlas 128 x 192: alte Felder bleiben, "stone" = echte Wandkachel, "werk" = hellere Kachel --------------------------
W, H = 128, 192
RECTS = {
    "planks": (0, 0, 40, 128), "plate": (40, 0, 72, 32), "iron": (72, 0, 104, 32), "edge": (104, 0, 128, 32),
    "lock": (40, 32, 88, 64), "dark": (88, 32, 104, 64), "glass": (104, 32, 128, 64),
    "stone": (40, 64, 104, 128), "werk": (0, 128, 64, 192),
}
UV = psx.rects_from_pixels(RECTS, (W, H))
ns["UV"] = UV                       # die Fluegel-Funktionen aus door.py lesen UV aus ihren Globals
_strap = ns["strap"]


def _short_strap(part, x_hinge, side, y, length, half, z0, z1, rect="iron"):
    """Beschlagband hoechstens bis 2 cm vor die Mitte (der Boss-Fluegel sitzt nach innen versetzt)."""
    _strap(part, x_hinge, side, y, min(length, abs(x_hinge) - 0.02), half, z0, z1, rect)


ns["strap"] = _short_strap

RAMP = np.array(json.load(open(os.path.join(ROOT, "assets", "palette", "hauptpalette.json"), encoding="utf-8"))["grund"][0]["farben"], int)
FILES = {"a": "catacomb_wall.png", "b": "catacomb_wall_b.png", "c": "catacomb_wall_c.png"}


def load_tile(variant):
    im = bpy.data.images.load(os.path.join(TEXDIR, FILES[variant]))
    w, h = im.size
    a = np.array(im.pixels[:]).reshape(h, w, 4)[::-1, :, :3] * 255          # oben zuerst
    return np.round(a).astype(int)


def werk_of(tile):
    """Dieselbe Kachel, in der Stein-Rampe so weit heller, dass ihre hellste Farbe auf die hellste Stufe der Rampe faellt
    (mindestens WERK_STEPS Stufen): gleiche Fugen, gleiche Verhaeltnisse, nur heller. Heller als die Rampe geht nicht."""
    flat = tile.reshape(-1, 3)
    idx = {}
    for c in {tuple(p) for p in flat}:
        idx[c] = int(((RAMP - np.array(c)) ** 2).sum(1).argmin())
    shift = max(WERK_STEPS, len(RAMP) - 1 - max(idx.values()))
    print("WERK Verschiebung", shift, "Stufen, Kachel von", min(idx.values()), "bis", max(idx.values()))
    out = np.array([RAMP[min(idx[tuple(p)] + shift, len(RAMP) - 1)] for p in flat])
    return out.reshape(tile.shape)


def paint_atlas(kind, variant, hell=False):
    tile = load_tile(variant)
    werk = werk_of(tile)
    if hell:
        # nur zur Ansicht: Werkstein heller als die Stein-Rampe (x 1,45, deutlich Richtung Grau) - braucht eine Palette-Entscheidung
        grey = werk.mean(axis=2, keepdims=True)
        werk = np.clip(np.round((0.45 * werk + 0.55 * grey) * 1.45), 0, 255).astype(int)
    fixed = {tuple(c) for c in tile.reshape(-1, 3)} | {tuple(c) for c in werk.reshape(-1, 3)}
    free = 32 - len(fixed)
    assert free >= 12, f"zu wenig freie Farben: {free}"
    pnt = ns["painters"](kind)
    paint = 4
    big = np.zeros((H * paint, W * paint, 3))
    mask = np.zeros((H, W), bool)
    for name, (x0, y0, x1, y1) in RECTS.items():
        if name in ("stone", "werk"):
            continue
        ph, pw = (y1 - y0) * paint, (x1 - x0) * paint
        U, V_ = np.meshgrid((np.arange(pw) + 0.5) / pw, (np.arange(ph) + 0.5) / ph)
        big[y0 * paint:y1 * paint, x0 * paint:x1 * paint] = pnt[name](U, V_)
        mask[y0:y1, x0:x1] = True
    small = np.clip(big.reshape(H, paint, W, paint, 3).mean(axis=(1, 3)), 0, 255)
    atlas = np.zeros((H, W, 3))
    atlas[mask] = psx.quantize(small[mask], free, 7)
    for name, t in (("stone", tile), ("werk", werk)):
        x0, y0, x1, y1 = RECTS[name]
        atlas[y0:y1, x0:x1] = t[::-1]                                       # Feld: Zeile 0 unten
    fill = tuple(tile[0, 0])
    unused = np.ones((H, W), bool)
    for x0, y0, x1, y1 in RECTS.values():
        unused[y0:y1, x0:x1] = False
    atlas[unused] = fill
    return atlas, len({tuple(p) for p in atlas.reshape(-1, 3).astype(int)})


# --- Steinflaechen mit Welt-UV ---------------------------------------------------------------------------------------------

def _clip(poly, axis, bound, keep_ge):
    """poly: Liste (u, v, P); beschneidet an u (axis 0) oder v (axis 1) = bound."""
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
    """Konvexe ebene Flaeche (Spiel-Achsen, Datei-Koordinaten) mit Normale nach `hint`; Textur-Koordinaten aus der Welt
    (u am Wandverlauf, v mit der Hoehe, eine Kachel je Meter), an den Metergrenzen geteilt."""
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


def quad(part, a, b, c, d, hint, field):
    emit(part, [a, b, c, d], hint, field)


def arc_pts(xc, R, spring, n=6):
    """Rechte Haelfte eines Spitzbogens (Kreismittelpunkt (xc, spring), xc < 0) vom Ansatz bis zum Scheitel x = 0."""
    rise = math.sqrt(R * R - xc * xc)
    a_end = math.atan2(rise, -xc)
    return [(xc + R * math.cos(a_end * j / n), spring + R * math.sin(a_end * j / n)) for j in range(n + 1)]


def to3(side_x, side_z, x, y, z):
    return (side_x * x, y, side_z * z)


def arch_ring(part, xc, R_out, R_in, spring, z, side_z, field, n=6):
    """Bogenring zwischen den Radien R_out und R_in (gleiche Mitte) in der Ebene |z| = z, beide Haelften."""
    a, b = arc_pts(xc, R_out, spring, n), arc_pts(xc, R_in, spring, n)
    for sx in (1, -1):
        for j in range(n):
            quad(part, to3(sx, side_z, a[j][0], a[j][1], z), to3(sx, side_z, a[j + 1][0], a[j + 1][1], z),
                 to3(sx, side_z, b[j + 1][0], b[j + 1][1], z), to3(sx, side_z, b[j][0], b[j][1], z), (0, 0, side_z), field)


def arc_reveal(part, xc, R, spring, z_from, z_to, side_z, field, n=6):
    """Leibung des Bogens vom Radius R zwischen den Tiefen z_from und z_to (Normale ins Innere)."""
    a = arc_pts(xc, R, spring, n)
    for sx in (1, -1):
        for j in range(n):
            mx, my = (a[j][0] + a[j + 1][0]) / 2, (a[j][1] + a[j + 1][1]) / 2
            hint = (sx * (xc - mx), spring - my, 0)
            quad(part, to3(sx, side_z, a[j][0], a[j][1], z_from), to3(sx, side_z, a[j + 1][0], a[j + 1][1], z_from),
                 to3(sx, side_z, a[j + 1][0], a[j + 1][1], z_to), to3(sx, side_z, a[j][0], a[j][1], z_to), hint, field)


def spandrel_caps(part, xc, R, spring, top, side_z, z, field, n=6):
    """Bogenfeld: Ecken ueber dem Bogen bis zur Zellkante x = +-1 und zur Decke, buendig in der Wandebene |z| = z."""
    a = arc_pts(xc, R, spring, n)
    ring = [(a[0][0], spring)] + a + [(0.0, top)]
    ring = [p for k, p in enumerate(ring) if k == 0 or abs(p[0] - ring[k - 1][0]) + abs(p[1] - ring[k - 1][1]) > 1e-7]
    corner = (a[0][0], top)
    for sx in (1, -1):
        for p, q in zip(ring, ring[1:]):
            emit(part, [to3(sx, side_z, corner[0], corner[1], z), to3(sx, side_z, p[0], p[1], z), to3(sx, side_z, q[0], q[1], z)],
                 (0, 0, side_z), field)


def plan_prism(part, pts, y0, y1, skip, field, top=True, bottom=False):
    """Prisma aus einem konvexen Grundriss (x, z), Hoehe y0..y1; Seitenkanten in `skip` entfallen (liegen an der Wand)."""
    cx, cz = sum(p[0] for p in pts) / len(pts), sum(p[1] for p in pts) / len(pts)
    for i, (a, b) in enumerate(zip(pts, pts[1:] + pts[:1])):
        if i in skip:
            continue
        mx, mz = (a[0] + b[0]) / 2 - cx, (a[1] + b[1]) / 2 - cz
        quad(part, (a[0], y0, a[1]), (b[0], y0, b[1]), (b[0], y1, b[1]), (a[0], y1, a[1]), (mx, 0, mz), field)
    if top:
        emit(part, [(p[0], y1, p[1]) for p in pts], (0, 1, 0), field)
    if bottom:
        emit(part, [(p[0], y0, p[1]) for p in pts], (0, -1, 0), field)


def plate(part, x_in, x_out, y0, y1, sx, sz, field, top=True, bottom=False, depth=0.125, chamfer_in=False):
    """Kaempfer- oder Sockelplatte vor der Wand (z von 1 bis 1 + depth), Fase 1/16 an den freien senkrechten Kanten."""
    c = 0.0625
    pts = [(x_out, 1.0), (x_out - c, 1.0 + depth), (x_in + c, 1.0 + depth), (x_in, 1.0 + depth - c), (x_in, 1.0)]
    pts = [(sx * x, sz * z) for x, z in pts]
    plan_prism(part, pts, y0, y1, {4}, field, top, bottom)


def box_faces(part, lo, hi, field, faces):
    """Quader (Datei-Koordinaten) mit den genannten Seiten: '+z', '-z', '+x', '-x', '+y', '-y'."""
    x0, y0, z0 = lo
    x1, y1, z1 = hi
    d = {"+z": ([(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)], (0, 0, 1)),
         "-z": ([(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0)], (0, 0, -1)),
         "+x": ([(x1, y0, z0), (x1, y0, z1), (x1, y1, z1), (x1, y1, z0)], (1, 0, 0)),
         "-x": ([(x0, y0, z0), (x0, y0, z1), (x0, y1, z1), (x0, y1, z0)], (-1, 0, 0)),
         "+y": ([(x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1)], (0, 1, 0)),
         "-y": ([(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)], (0, -1, 0))}
    for f in faces:
        emit(part, d[f][0], d[f][1], field)


# --- Rahmen: Bogenstufen + Kaempfer + Sockel -----------------------------------------------------------------------------

SOCKEL = 0.5


def stufenportal(parts, hw0, R0, spring, zs, z_door, ring_field, plate_x_in, vertical_reveals, top=3.5, width=0.125):
    """Alles aus Stein: Bogenfeld (buendig mit der Wand), Bogenlaeufe (je Lauf eine Stufe nach hinten), Leibungen, Kaempfer, Sockel."""
    xc = hw0 - R0
    n = len(zs)
    for sz in (1, -1):
        part = parts[0] if sz > 0 else parts[1]                              # Vorderseite (+z) und Rueckseite (-z) je ein Knoten
        spandrel_caps(part, xc, R0, spring, top, sz, 1.0, "stone")
        depths = [1.0] + list(zs) + [z_door]
        for k in range(n):
            ro, ri = R0 - width * k, R0 - width * (k + 1)
            arch_ring(part, xc, ro, ri, spring, zs[k], sz, ring_field)
            for sx in (1, -1):                                              # Pfostenband des Laufs bis zum Sockel
                quad(part, (sx * (hw0 - width * k), SOCKEL, sz * zs[k]), (sx * (hw0 - width * (k + 1)), SOCKEL, sz * zs[k]),
                     (sx * (hw0 - width * (k + 1)), spring, sz * zs[k]), (sx * (hw0 - width * k), spring, sz * zs[k]), (0, 0, sz), ring_field)
        for k in range(n + 1):
            arc_reveal(part, xc, R0 - width * k, spring, depths[k], depths[k + 1], sz, "stone")
            if vertical_reveals and k >= 1:
                for sx in (1, -1):
                    x = sx * (hw0 - width * k)
                    quad(part, (x, SOCKEL, sz * depths[k]), (x, SOCKEL, sz * depths[k + 1]), (x, spring, sz * depths[k + 1]),
                         (x, spring, sz * depths[k]), (-sx, 0, 0), "stone")
        for sx in (1, -1):
            plate(part, plate_x_in, 1.0, spring - 0.125, spring, sx, sz, "werk", top=False, bottom=True)
            plate(part, plate_x_in, 1.0, 0.0, SOCKEL, sx, sz, "werk", top=True, bottom=False)


def hohes_relief(part, field="werk"):
    """Zweite Fassung fuer Boss A im 6-m-Raum: ein hoher Lanzettbogen als Relief (1/16 vor der Wand) auf der +z-Seite um die Tuer."""
    hw0, R0, spring = 1.375, 2.9375, 2.5
    xc = hw0 - R0
    z0, z1, c = 1.0, 1.0625, 0.0625
    a_out, a_mid, a_in = arc_pts(xc, R0, spring), arc_pts(xc, R0 - c, spring), arc_pts(xc, R0 - 2 * c, spring)
    n = len(a_out) - 1
    for sx in (1, -1):
        for j in range(n):
            P = lambda arc, k, z: (sx * arc[k][0], arc[k][1], z)
            ix = sx * (xc - (a_in[j][0] + a_in[j + 1][0]) / 2)
            iy = spring - (a_in[j][1] + a_in[j + 1][1]) / 2
            length = math.hypot(ix, iy)
            quad(part, P(a_out, j, z0), P(a_out, j + 1, z0), P(a_mid, j + 1, z1), P(a_mid, j, z1),
                 (-0.7 * ix / length, -0.7 * iy / length, 0.7), field)
            quad(part, P(a_mid, j, z1), P(a_mid, j + 1, z1), P(a_in, j + 1, z1), P(a_in, j, z1), (0, 0, 1), field)
            quad(part, P(a_in, j, z1), P(a_in, j + 1, z1), P(a_in, j + 1, z0), P(a_in, j, z0), (ix, iy, 0), field)
        # Pfosten: Fase, Front, Leibung (von oben bis zum Sockel)
        xo, xm, xi = hw0, hw0 - c, hw0 - 2 * c
        quad(part, (sx * xo, SOCKEL, z0), (sx * xm, SOCKEL, z1), (sx * xm, spring, z1), (sx * xo, spring, z0), (sx * 0.7, 0, 0.7), field)
        quad(part, (sx * xm, SOCKEL, z1), (sx * xi, SOCKEL, z1), (sx * xi, spring, z1), (sx * xm, spring, z1), (0, 0, 1), field)
        quad(part, (sx * xi, SOCKEL, z1), (sx * xi, SOCKEL, z0), (sx * xi, spring, z0), (sx * xi, spring, z1), (-sx, 0, 0), field)
        plate(part, xi, xo, spring - 0.125, spring, sx, 1, field, top=False, bottom=True)
        plate(part, xi, xo, 0.0, SOCKEL, sx, 1, field, top=True, bottom=False)


# --- Tuerarten ---------------------------------------------------------------------------------------------------------

def portal_nodes():
    """portal: alles ohne Stein (Kopffeld, Beschlag, Glas); portal_front/portal_back: die Steinteile der +z- und der -z-Seite,
    damit das Spiel auf jeder Seite die Wandvariante dieser Seite aufziehen kann (wie bei der rissigen Wand)."""
    return (Part("portal", (0, 0, 0), UV), Part("portal_front", (0, 0, 0), UV, parent="portal"),
            Part("portal_back", (0, 0, 0), UV, parent="portal"))


def portal_normal(kind):
    part, front, back = portal_nodes()
    spring, apex_n = 2.0, 2.0 + math.sqrt(1.4375 ** 2 - 0.5625 ** 2)
    stufenportal((front, back), 1.0, 1.5625, spring, [0.9375], 0.07, "werk", 0.875, False)
    post = 0.105 if kind == "normal" else 0.135                                   # wie in door.py (Scharnierpfosten, versteckt hinter dem Lauf)
    zp = 0.1 if kind == "normal" else 0.15
    for sx in (1, -1):
        lo, hi = sorted((sx * (1.0 - post), sx * 1.0))
        inner = "+x" if sx < 0 else "-x"
        box_faces(front, (lo, 0, 0), (hi, spring, zp), "stone", ["+z", inner])
        box_faces(back, (lo, 0, -zp), (hi, spring, 0), "stone", ["-z", inner])
    hw_n = 0.875
    t = 0.07
    ns["head_panel"](part, hw_n, spring, apex_n, -t, t, "planks" if kind == "normal" else "plate")
    for z0, z1 in ns["faces"](0.14, 0.025):
        part.box((-hw_n, ns["LEAF_TOP_N"], z0), (hw_n, spring + 0.06, z1), "iron")
        if kind == "normal":
            for s in (1, -1):
                ns["strap"](part, s * (hw_n - 0.01), s, 2.5, 0.9, 0.065, z0, z1)
    return [part, front, back]


def portal_boss(hoch):
    part, front, back = portal_nodes()
    spring = 1.875
    stufenportal((front, back), 1.0, 1.75, spring, [0.9375, 0.625, 0.3125], 0.1, "werk", 0.625, True)
    xc = 1.0 - 1.75
    R_n = 1.75 - 0.375
    hw_n = 0.625
    apex_n = spring + math.sqrt(R_n * R_n - xc * xc)
    for sx in (1, -1):
        lo, hi = sorted((sx * 0.49, sx * hw_n))
        inner = "+x" if sx < 0 else "-x"
        box_faces(front, (lo, 0, 0), (hi, spring, 0.14), "stone", ["+z", inner])
        box_faces(back, (lo, 0, -0.14), (hi, spring, 0), "stone", ["-z", inner])
    ns["head_panel"](part, hw_n, spring, apex_n, -0.1, 0.1, "planks")
    for z0, z1 in ns["faces"](0.2, 0.03):
        part.box((-hw_n, spring, z0), (hw_n, spring + 0.0625, z1), "iron")
    cy = 2.375
    for f in (-1, 1):
        z0, z1 = sorted((f * 0.095, f * 0.14))
        pts = [(0.21 * math.cos(2 * math.pi * k / 10), cy + 0.21 * math.sin(2 * math.pi * k / 10)) for k in range(10)]
        ns["extrude"](part, pts, z0, z1, "glass")
    if hoch:
        hohes_relief(front)
    return [part, front, back]


def parts_for(kind, hoch=False):
    if kind == "normal":
        return portal_normal("normal") + [ns["leaf_rect"]("leaf_a", +1, 1.0, 0.14, ns["LEAF_TOP_N"], "normal"),
                                          ns["leaf_rect"]("leaf_b", -1, 1.0, 0.14, ns["LEAF_TOP_N"], "normal")]
    if kind == "locked":
        return portal_normal("locked") + [ns["locked_leaf"](), ns["lock_node"]()]
    return portal_boss(hoch) + [ns["leaf_rect"]("leaf_a", +1, 0.625, 0.2, 1.875, "boss"),
                                ns["leaf_rect"]("leaf_b", -1, 0.625, 0.2, 1.875, "boss")]


def build(kind, variant, hoch=False, hell=False):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    coll = bpy.data.collections.new("Tuer")
    bpy.context.scene.collection.children.link(coll)
    name = f"door_{kind}{'hoch' if hoch else ''}{'hell' if hell else ''}_{variant}"
    atlas, colours = paint_atlas("boss" if kind == "boss" else kind, variant, hell)
    image = psx.save_image(name, atlas, os.path.join(OUT, name + ".png"))
    mat = psx.export_material(name, image)
    objs = [p.build(coll, mat, smooth=False) for p in parts_for(kind, hoch)]
    psx.export_glb(objs, os.path.join(OUT, name + ".glb"))
    psx.report_parts(name, objs)
    print(f"TEXTUR {name} {W}x{H} Farben {colours}")


def main():
    os.makedirs(OUT, exist_ok=True)
    for variant in "abc":
        for kind in ("normal", "locked", "boss"):
            build(kind, variant)
        build("boss", variant, hoch=True)
    for variant in "ab":
        build("boss", variant, hell=True)


main()
