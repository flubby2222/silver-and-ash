"""Probestueck #549: grosse Bossraum-Tuer (2 m breit, 6 m hohe Wand) mit Vorraum. Drei Entwuerfe a, b, c.

Aufruf (aus dem Repository-Wurzelverzeichnis; TEMP und TMP vorher auf einen Arbeitsordner setzen):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/bossraum-tuer-549/bossraum_tuer.py -- [schritt ...]
Schritte: modelle, ansichten, blaetter (ohne Angabe alle). Alles per Skript, nichts heruntergeladen.

Spiel-Achsen (x seitlich, y oben, z vorn), Meter. Ein Entwurf besteht aus
  - dem Steinportal, zwei feste Knoten: portal_front (Stein der +z-Seite) und portal_back (der -z-Seite). Das Portal fuellt
    4 m Wandbreite (zwei Zellen) und 6 m Hoehe; die Wandebenen liegen bei z = +-1 (eine Zelle, 2 m tief), die Tuer in z = 0.
    Atlas 128 x 64: links die Wand-Textur der Variante a (bitgleich gelesen), rechts dieselbe Kachel als Werkstein;
  - den Fluegeln (Holz und Eisen des Bosstors), je Fluegel ein Knoten mit dem Drehpunkt an der Scharnierkante.
Alle Flaechen haben Textur-Koordinaten aus der Welt (eine 64-px-Kachel je Meter, an den Metergrenzen geteilt, wie door.py).
Wo ein Teilen an den Metergrenzen eine Kante zerreisst, fuegt heal() die fehlende Ecke ein.
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
import bmesh  # noqa: E402
import bpy  # noqa: E402
import imbuf  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402
import pillar_kit as PK  # noqa: E402
from psx import (V, G, FOG_COLOR, Part, rects_from_pixels, vnoise, blob, mix, paint_atlas, save_image,  # noqa: E402
                 stone_textures, pass_material, write_light, setup_render, camera, aim, render_passes, compose,
                 save_png, export_material, export_glb, report_parts, MATS)

TMP = os.path.join(tempfile.gettempdir(), "bossraum549_tmp")
os.makedirs(TMP, exist_ok=True)
MODELLE = os.path.join(HERE, "modelle")
ANSICHTEN = os.path.join(HERE, "ansichten")
TEXTURES = os.path.join(ROOT, "assets", "textures")
MODELS = os.path.join(ROOT, "assets", "models")
TELEPORTER = os.path.join(ROOT, "design", "proben", "grafik", "teleporter-436", "modelle", "teleporter_C_ruhend.glb")
BG = np.array((30, 26, 34), float)
PAPER = np.array((24, 21, 28), float)
INK = (235, 230, 240)

TOP = 6.0                     # Hoehe der Bossraum-Wand
POST = 2.0                    # Portal fuellt x = -2 .. 2
SOCKEL = 0.5
DOOR_Z = 0.12                 # Tuerflaeche liegt in z = -0.12 .. 0.12 (Dicke 0,24)

# --- Atlanten ------------------------------------------------------------------------------------------------------------

FRAME_SIZE = (128, 64)
FRAME_RECTS = {"stone": (0, 0, 64, 64), "werk": (64, 0, 128, 64)}
FRAME_UV = rects_from_pixels(FRAME_RECTS, FRAME_SIZE)
LEAF_SIZE = (128, 128)
LEAF_RECTS = {"planks": (0, 0, 64, 64), "plate": (64, 0, 128, 64), "iron": (0, 64, 64, 128), "edge": (64, 64, 128, 128)}
LEAF_UV = rects_from_pixels(LEAF_RECTS, LEAF_SIZE)
WOOD = np.array((40, 34, 42), float)       # Holz des Bosstors (door.py PAL boss)
IRON = np.array((112, 108, 126), float)    # Eisen des Bosstors
DARK = np.array((16, 12, 16), float)


def werk_tile(tile, stein, werkstein):
    """Wie door.py (Fassung blau): dieselbe Kachel als Werkstein, Stufen aus der Reihe Werkstein."""
    flat = tile.reshape(-1, 3)
    colours = sorted({tuple(int(c) for c in p) for p in flat})
    idx = {c: int(((stein - np.array(c)) ** 2).sum(1).argmin()) for c in colours}
    top = len(stein) - 1
    shift = max(4, top - max(idx.values()))
    lifted = {c: min(i + shift, top) for c, i in idx.items()}
    first = len(stein) - len(werkstein)
    out = {c: werkstein[max(i - first, 0)] for c, i in lifted.items()}
    return np.array([out[tuple(int(c) for c in p)] for p in flat]).reshape(tile.shape)


def frame_atlas():
    rows = PK.palette_rows()
    stein, werkstein = rows["Stein"], rows["Werkstein"]
    tile = PK.load_tile("catacomb_wall.png")                                  # Wandvariante a
    werk = werk_tile(tile, stein, werkstein)
    w, h = FRAME_SIZE
    atlas = np.zeros((h, w, 3))
    for name, t in (("stone", tile), ("werk", werk)):
        x0, y0, x1, y1 = FRAME_RECTS[name]
        atlas[y0:y1, x0:x1] = t[::-1]
    allowed = {tuple(int(c) for c in p) for p in np.concatenate([stein, werkstein])}
    used = {tuple(int(c) for c in p) for p in atlas.reshape(-1, 3)}
    assert used <= allowed, "Portal-Atlas: Farben ausserhalb der Reihen Stein und Werkstein"
    return atlas


def leaf_painters():
    def planks(U, V_):
        h, w = U.shape
        rng = np.random.default_rng(549)
        n = 3
        idx = np.minimum(np.floor(U * n), n - 1).astype(int)
        f = (U * n) % 1.0
        col = np.broadcast_to(WOOD, (h, w, 3)).copy()
        col *= (0.82 + 0.32 * rng.random(n))[idx][..., None]
        col *= (0.86 + 0.28 * vnoise(h, w, 3, 16, 201))[..., None]
        col *= (0.93 + 0.14 * vnoise(h, w, 20, 3, 301))[..., None]
        for _ in range(3):
            cu, cv = 0.1 + 0.8 * rng.random(), 0.1 + 0.8 * rng.random()
            col = mix(col, WOOD * 0.4, (blob(U, V_, cu, cv, 0.05, 0.02) > 0.3).astype(float) * 0.9)
        col = mix(col, DARK * 1.4, ((f < 0.07) | (f > 0.95)).astype(float) * 0.9)
        return mix(col, WOOD * 1.3, ((f > 0.07) & (f < 0.13)).astype(float) * 0.5)

    def plate(U, V_):
        h, w = U.shape
        col = np.broadcast_to(IRON * 0.8, (h, w, 3)).copy()
        col *= (0.84 + 0.3 * vnoise(h, w, 6, 6, 401))[..., None]
        edge = (np.minimum(np.minimum(U, 1 - U), np.minimum(V_, 1 - V_)) < 0.07)
        col = mix(col, IRON * 0.35, edge.astype(float))
        col = mix(col, IRON * 1.4 + 14, ((np.minimum(U, V_) > 0.07) & (np.minimum(U, V_) < 0.13)).astype(float) * 0.55)
        for cu in (0.15, 0.85):
            for cv in (0.15, 0.5, 0.85):
                d = blob(U, V_, cu, cv, 0.08, 0.08)
                col = mix(col, IRON * 1.7 + 20, (d > 0.4).astype(float))
                col = mix(col, IRON * 0.35, ((d > 0.05) & (d <= 0.4)).astype(float) * 0.7)
        return col

    def iron_band(U, V_):
        h, w = U.shape
        col = np.broadcast_to(IRON, (h, w, 3)).copy()
        col *= (0.8 + 0.4 * np.abs(np.sin(V_ * 3.1)))[..., None]
        col *= (0.9 + 0.2 * vnoise(h, w, 6, 6, 601))[..., None]
        col = mix(col, IRON * 1.35 + 14, ((V_ > 0.78) & (V_ < 0.92)).astype(float) * 0.7)
        for k in range(4):
            d = blob(U, V_, (k + 0.5) / 4, 0.5, 0.09, 0.2)
            col = mix(col, IRON * 1.6 + 20, (d > 0.35).astype(float))
        return col

    def edge(U, V_):
        h, w = U.shape
        col = np.broadcast_to(WOOD * 0.7, (h, w, 3)).copy()
        return col * (0.8 + 0.4 * vnoise(h, w, 4, 4, 701))[..., None]

    return {"planks": planks, "plate": plate, "iron": iron_band, "edge": edge}


def leaf_atlas():
    tex = paint_atlas(LEAF_RECTS, leaf_painters(), LEAF_SIZE, colors=32, seed=7)
    lead = tex[..., 0] - np.maximum(tex[..., 1], tex[..., 2])
    assert lead.max() < 30, "Flaechen mit Rotstich im Fluegel-Atlas"
    return tex


# --- Flaechen und Koerper (alles mit Welt-UV) ---------------------------------------------------------------------------

def W(part, pts, hint, field):
    PK.emit(part, pts, hint, field)


def ext(part, poly, z0, z1, field, back=True, front=True):
    """Konvexes Vieleck (x, y) von z0 bis z1 (z0 < z1) als Koerper; Seiten mit Welt-UV."""
    n = len(poly)
    cx, cy = sum(p[0] for p in poly) / n, sum(p[1] for p in poly) / n
    for a, b in zip(poly, poly[1:] + poly[:1]):
        if math.hypot(a[0] - b[0], a[1] - b[1]) < 1e-7:
            continue
        mx, my = (a[0] + b[0]) / 2 - cx, (a[1] + b[1]) / 2 - cy
        W(part, [(a[0], a[1], z0), (b[0], b[1], z0), (b[0], b[1], z1), (a[0], a[1], z1)], (mx, my, 0), field)
    if front:
        W(part, [(p[0], p[1], z1) for p in poly], (0, 0, 1), field)
    if back:
        W(part, [(p[0], p[1], z0) for p in poly], (0, 0, -1), field)


def ebox(part, lo, hi, field, sides=("+z", "-z", "+x", "-x", "+y", "-y")):
    x0, y0, z0 = lo
    x1, y1, z1 = hi
    d = {"+z": ([(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)], (0, 0, 1)),
         "-z": ([(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0)], (0, 0, -1)),
         "+x": ([(x1, y0, z0), (x1, y0, z1), (x1, y1, z1), (x1, y1, z0)], (1, 0, 0)),
         "-x": ([(x0, y0, z0), (x0, y0, z1), (x0, y1, z1), (x0, y1, z0)], (-1, 0, 0)),
         "+y": ([(x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1)], (0, 1, 0)),
         "-y": ([(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)], (0, -1, 0))}
    for s in sides:
        W(part, d[s][0], d[s][1], field)


def pyramid(part, cx, y0, cz, half, tip, field):
    """Vierseitige Spitze (Fiale) mit Welt-UV: Grundflaeche unten, vier Dreiecke."""
    c = [(cx - half, y0, cz - half), (cx + half, y0, cz - half), (cx + half, y0, cz + half), (cx - half, y0, cz + half)]
    t = (cx, tip, cz)
    W(part, c, (0, -1, 0), field)
    for a, b in zip(c, c[1:] + c[:1]):
        W(part, [a, b, t], ((a[0] + b[0]) / 2 - cx, 0.4, (a[2] + b[2]) / 2 - cz), field)


def heal(part):
    """T-Ecken schliessen: liegt die Ecke einer anderen Flaeche mitten auf einer Kante, wird sie in diese Flaeche eingefuegt
    (Textur-Koordinate entlang der Kante gemischt). Danach teilen sich beide Flaechen die Kante ohne Luecke."""
    bm, uvl = part.bm, part.uv
    seen = {}
    for v in bm.verts:
        seen.setdefault(tuple(round(c * 1e5) for c in v.co), tuple(v.co))
    pts = np.array(list(seen.values()), float)
    todo = []
    for f in list(bm.faces):
        loops = list(f.loops)
        n = len(loops)
        out, changed = [], False
        for i, l in enumerate(loops):
            a, b = np.array(l.vert.co), np.array(loops[(i + 1) % n].vert.co)
            ua, ub = np.array(l[uvl].uv), np.array(loops[(i + 1) % n][uvl].uv)
            out.append((tuple(a), tuple(ua)))
            ab = b - a
            L2 = float(ab @ ab)
            if L2 < 1e-12:
                continue
            t = ((pts - a) @ ab) / L2
            d = np.linalg.norm(pts - (a + np.outer(t, ab)), axis=1)
            sel = np.where((t > 1e-6) & (t < 1 - 1e-6) & (d < 2e-5))[0]
            for j in sel[np.argsort(t[sel])]:
                out.append((tuple(pts[j]), tuple(ua + (ub - ua) * t[j])))
                changed = True
        if changed:
            todo.append((f, out))
    for f, out in todo:
        verts = [bm.verts.new(Vector(c)) for c, _ in out]
        nf = bm.faces.new(verts)
        for loop, (_, uv) in zip(nf.loops, out):
            loop[uvl].uv = uv
        bm.faces.remove(f)
    loose = [v for v in bm.verts if not v.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context="VERTS")
    return len(todo)


def open_edges(part):
    """Offene Kanten (Ecken gleicher Lage zusammengelegt) als Liste von Punktpaaren in Spiel-Achsen."""
    keys, edges = {}, {}
    for f in part.bm.faces:
        ids = [keys.setdefault(tuple(round(c * 1e4) for c in v.co), (len(keys), PK.G_inv(v.co))) for v in f.verts]
        for a, b in zip(ids, ids[1:] + ids[:1]):
            k = (min(a[0], b[0]), max(a[0], b[0]))
            edges.setdefault(k, [0, a[1], b[1]])[0] += 1
    return [(e[1], e[2]) for e in edges.values() if e[0] == 1]


def classify(a, b, y_top):
    """Wo liegt eine offene Kante? Wandebene (|z| = 1), Boden, Oberkante, seitliche Zellgrenze oder die Tuerfuge."""
    def same(i, val):
        return abs(a[i] - val) < 2e-3 and abs(b[i] - val) < 2e-3
    for s in (1, -1):
        if same(2, s * 1.0):
            return "Wandebene"
    if same(1, 0.0):
        return "Boden"
    if same(1, y_top):
        return "Oberkante"
    if same(0, POST) or same(0, -POST):
        return "Seite"
    for s in (1, -1):
        if same(2, s * DOOR_Z):
            return "Tuerfuge"
    return "SONST"


# --- Portale ---------------------------------------------------------------------------------------------------------------

def arc_pts(xc, R, spring, n=8):
    rise = math.sqrt(R * R - xc * xc)
    a_end = math.atan2(rise, -xc)
    return [(xc + R * math.cos(a_end * j / n), spring + R * math.sin(a_end * j / n)) for j in range(n + 1)]


def mirror(sx, sz, x, y, z):
    return (sx * x, y, sz * z)


def plinth(part, sz, x0, x1, y0, y1, depth=0.125, field="werk", top=True, bottom=False):
    """Platte vor der Wandebene (z 1 bis 1 + depth); Seiten x0..x1 positiv, wird fuer beide x-Seiten gespiegelt aufgerufen."""
    lo, hi = sorted((sz * 1.0, sz * (1.0 + depth)))
    sides = ["+z" if sz > 0 else "-z", "+x", "-x"] + (["+y"] if top else []) + (["-y"] if bottom else [])
    ebox(part, (x0, y0, lo), (x1, y1, hi), field, sides)


def pointed_portal(parts, hw_in, xc, spring, widths, zs, z_door, sockel_wide=True):
    """Spitzbogen-Portal mit gestuften Laeufen. Alle Kreise teilen den Mittelpunkt (xc, spring), xc < 0."""
    n = len(widths)
    R_in = hw_in - xc
    Rs = [R_in + sum(widths[k:]) for k in range(n)] + [R_in]
    hws = [R + xc for R in Rs]
    depths = [1.0] + list(zs) + [z_door]
    for sz in (1, -1):
        part = parts[sz]
        a0 = arc_pts(xc, Rs[0], spring)
        for sx in (1, -1):
            # Wandebene: Pfosten und Feld ueber dem Bogen, buendig
            W(part, [mirror(sx, sz, hws[0], 0, 1.0), mirror(sx, sz, POST, 0, 1.0), mirror(sx, sz, POST, spring, 1.0),
                     mirror(sx, sz, hws[0], spring, 1.0)], (0, 0, sz), "stone")
            ring = [(POST, spring)] + a0 + [(0.0, TOP)]
            for p, q in zip(ring, ring[1:]):
                W(part, [mirror(sx, sz, POST, TOP, 1.0), mirror(sx, sz, p[0], p[1], 1.0), mirror(sx, sz, q[0], q[1], 1.0)],
                  (0, 0, sz), "stone")
            for k in range(n):                                    # Lauf k: Bogenring und Pfostenband bei Tiefe zs[k]
                a, b = arc_pts(xc, Rs[k], spring), arc_pts(xc, Rs[k + 1], spring)
                for j in range(len(a) - 1):
                    W(part, [mirror(sx, sz, a[j][0], a[j][1], zs[k]), mirror(sx, sz, a[j + 1][0], a[j + 1][1], zs[k]),
                             mirror(sx, sz, b[j + 1][0], b[j + 1][1], zs[k]), mirror(sx, sz, b[j][0], b[j][1], zs[k])],
                      (0, 0, sz), "werk")
                W(part, [mirror(sx, sz, hws[k + 1], 0, zs[k]), mirror(sx, sz, hws[k], 0, zs[k]),
                         mirror(sx, sz, hws[k], spring, zs[k]), mirror(sx, sz, hws[k + 1], spring, zs[k])], (0, 0, sz), "werk")
            for k in range(n + 1):                                # Leibungen
                a = arc_pts(xc, Rs[k], spring)
                for j in range(len(a) - 1):
                    mx, my = (a[j][0] + a[j + 1][0]) / 2, (a[j][1] + a[j + 1][1]) / 2
                    W(part, [mirror(sx, sz, a[j][0], a[j][1], depths[k]), mirror(sx, sz, a[j + 1][0], a[j + 1][1], depths[k]),
                             mirror(sx, sz, a[j + 1][0], a[j + 1][1], depths[k + 1]),
                             mirror(sx, sz, a[j][0], a[j][1], depths[k + 1])], (sx * (xc - mx), spring - my, 0), "stone")
                x = sx * hws[k]
                W(part, [(x, 0, sz * depths[k]), (x, 0, sz * depths[k + 1]), (x, spring, sz * depths[k + 1]),
                         (x, spring, sz * depths[k])], (-sx, 0, 0), "stone")
        plinth(part, sz, hw_in, POST, 0.0, SOCKEL, top=True)                   # Sockel beider Seiten
        plinth(part, sz, -POST, -hw_in, 0.0, SOCKEL, top=True)
        plinth(part, sz, hw_in, hws[0] + 0.12, spring - 0.125, spring, top=True, bottom=True)   # Kaempfer
        plinth(part, sz, -(hws[0] + 0.12), -hw_in, spring - 0.125, spring, top=True, bottom=True)
    return Rs, hws


def gesims(parts, y0=5.78, depth=0.14):
    for sz in (1, -1):
        lo, hi = sorted((sz * 1.0, sz * (1.0 + depth)))
        ebox(parts[sz], (-POST, y0, lo), (POST, TOP, hi), "werk",
             ["+z" if sz > 0 else "-z", "+x", "-x", "-y"])


def portal_a():
    """a: Dreilauf-Spitzbogen, Strebepfeiler mit Fialen, Gesims. Oeffnung 2,0 m, Scheitel der Oeffnung 5,2 m."""
    parts = {1: Part("portal_front", (0, 0, 0), FRAME_UV), -1: Part("portal_back", (0, 0, 0), FRAME_UV)}
    pointed_portal(parts, 1.0, -1.5, 3.2, [0.2, 0.2, 0.2], [0.85, 0.6, 0.35], DOOR_Z)
    for sz in (1, -1):
        p = parts[sz]
        lo, hi = sorted((sz * 1.0, sz * 1.24))
        side = ["+z" if sz > 0 else "-z", "+y", "-y"]
        for sx in (1, -1):
            x0, x1 = sorted((sx * 1.72, sx * 2.0))
            ebox(p, (x0, SOCKEL, lo), (x1, 5.3, hi), "werk", side + (["-x"] if sx > 0 else ["+x"]))
            c0, c1 = sorted((sx * 1.66, sx * 2.0))
            lo2, hi2 = sorted((sz * 1.0, sz * 1.3))
            ebox(p, (c0, 5.3, lo2), (c1, 5.45, hi2), "werk", side + ["-y"] + (["-x"] if sx > 0 else ["+x"]))
            pyramid(p, sx * 1.86, 5.45, sz * 1.15, 0.12, 5.95, "werk")
        # Pfeilerfuss
    return parts


def portal_c():
    """c: schmaler Zweilauf-Spitzbogen unter einem Wimperg (Giebel mit Krabben und Kreuzblume)."""
    parts = {1: Part("portal_front", (0, 0, 0), FRAME_UV), -1: Part("portal_back", (0, 0, 0), FRAME_UV)}
    pointed_portal(parts, 1.0, -1.0, 3.2, [0.25, 0.2], [0.8, 0.5], DOOR_Z)
    base, apex, wd = (1.9, 3.6), (0.0, 5.9), 0.17
    for sz in (1, -1):
        p = parts[sz]
        z0, z1 = sorted((sz * 1.0, sz * 1.11))
        for sx in (1, -1):
            dx, dy = apex[0] - base[0], apex[1] - base[1]
            ln = math.hypot(dx, dy)
            nx, ny = -dy / ln * wd, dx / ln * wd                  # Senkrechte nach aussen-oben
            if ny < 0:
                nx, ny = -nx, -ny
            poly = [(sx * base[0], base[1] - 0.0), (sx * apex[0], apex[1] - wd * 1.2),
                    (sx * (apex[0] + 0.0), apex[1] - wd * 1.2 + 0.0), (sx * base[0], base[1] - wd * 1.4)]
            top_edge = [(sx * base[0], base[1]), (0.0, apex[1])]
            low_edge = [(sx * base[0], base[1] - wd * 1.5), (0.0, apex[1] - wd * 1.5)]
            poly = [top_edge[0], top_edge[1], low_edge[1], low_edge[0]]
            ext(p, poly, z0, z1, "werk", back=False, front=(sz > 0)) if sz > 0 else ext(p, poly, z0, z1, "werk", back=True, front=False)
            for t in (0.28, 0.52, 0.76):                           # Krabben: kleine Dreiecke auf dem Rand
                bx, by = sx * (base[0] + dx * t * sx if False else base[0] * (1 - t)), base[1] + dy * t
                tri = [(bx, by), (bx + sx * 0.0 + sx * 0.16, by + 0.02), (bx + sx * 0.04, by + 0.3)]
                if sx < 0:
                    tri = [(bx, by), (bx - 0.16, by + 0.02), (bx - 0.04, by + 0.3)]
                if sz > 0:
                    ext(p, tri, z0, z1, "werk", back=False, front=True)
                else:
                    ext(p, tri, z0, z1, "werk", back=True, front=False)
        kreuz = [(0.0, 5.55), (0.13, 5.78), (0.0, 6.0), (-0.13, 5.78)]
        if sz > 0:
            ext(p, kreuz, z0, z1 + 0.04, "werk", back=False, front=True)
        else:
            ext(p, kreuz, z0 - 0.04, z1, "werk", back=True, front=False)
    return parts


def portal_b():
    """b: Sturzportal, rechteckig: zwei Stufen, Sturz mit Konsolen und Schlussblock. Oeffnung 2,0 x 5,0 m."""
    parts = {1: Part("portal_front", (0, 0, 0), FRAME_UV), -1: Part("portal_back", (0, 0, 0), FRAME_UV)}
    hw = [1.44, 1.22, 1.0]                     # Halbbreiten der Rechtecke von aussen nach innen
    tp = [5.44, 5.22, 5.0]                     # Oberkanten
    zs = [0.8, 0.5]
    depths = [1.0] + zs + [DOOR_Z]
    for sz in (1, -1):
        p = parts[sz]
        for sx in (1, -1):
            W(p, [mirror(sx, sz, hw[0], 0, 1.0), mirror(sx, sz, POST, 0, 1.0), mirror(sx, sz, POST, TOP, 1.0),
                  mirror(sx, sz, hw[0], TOP, 1.0)], (0, 0, sz), "stone")
        W(p, [(-hw[0], tp[0], sz * 1.0), (hw[0], tp[0], sz * 1.0), (hw[0], TOP, sz * 1.0), (-hw[0], TOP, sz * 1.0)],
          (0, 0, sz), "stone")
        for k in range(2):                                         # Laufbaender (Werkstein) bei Tiefe zs[k]
            z = sz * zs[k]
            for sx in (1, -1):
                W(p, [mirror(sx, sz, hw[k + 1], 0, zs[k]), mirror(sx, sz, hw[k], 0, zs[k]), mirror(sx, sz, hw[k], tp[k], zs[k]),
                      mirror(sx, sz, hw[k + 1], tp[k + 1], zs[k])], (0, 0, sz), "werk")
            W(p, [(-hw[k + 1], tp[k + 1], z), (hw[k + 1], tp[k + 1], z), (hw[k], tp[k], z), (-hw[k], tp[k], z)], (0, 0, sz), "werk")
        for k in range(3):                                         # Leibungen
            d0, d1 = sz * depths[k], sz * depths[k + 1]
            for sx in (1, -1):
                x = sx * hw[k]
                W(p, [(x, 0, d0), (x, 0, d1), (x, tp[k], d1), (x, tp[k], d0)], (-sx, 0, 0), "stone")
            W(p, [(-hw[k], tp[k], d0), (hw[k], tp[k], d0), (hw[k], tp[k], d1), (-hw[k], tp[k], d1)], (0, -1, 0), "stone")
        plinth(p, sz, hw[2], POST, 0.0, SOCKEL)
        plinth(p, sz, -POST, -hw[2], 0.0, SOCKEL)
        for sx in (1, -1):                                         # Konsolen unter dem Sturz: Stufenkoerper
            lo, hi = sorted((sz * 1.0, sz * 1.22))
            for i, (x_in, x_out, y0) in enumerate(((1.0, 1.44, 4.9), (1.0, 1.66, 5.1))):
                xa, xb = sorted((sx * x_in, sx * x_out))
                sides = ["+z" if sz > 0 else "-z", "-y", "+y"]
                ebox(p, (xa, y0, lo), (xb, y0 + 0.2, hi), "werk", sides + ["+x", "-x"])
        lo, hi = sorted((sz * 1.0, sz * 1.16))                     # Schlussblock
        ebox(p, (-0.55, 5.5, lo), (0.55, 5.9, hi), "werk", ["+z" if sz > 0 else "-z", "+x", "-x", "+y", "-y"])
        lo, hi = sorted((sz * 1.0, sz * 1.12))                     # Gesims
        ebox(p, (-POST, 5.9, lo), (POST, TOP, hi), "werk", ["+z" if sz > 0 else "-z", "+x", "-x", "-y"])
    return parts


# --- Fluegel -----------------------------------------------------------------------------------------------------------------

def arc_leaf(hw, spring, apex, n=8):
    """Rechte Haelfte der Spitzbogenform des Fluegels: (hw, spring) bis (0, apex)."""
    rise = apex - spring
    R = (rise * rise + hw * hw) / (2 * hw)
    cx = hw - R
    a_end = math.atan2(rise, R - hw)
    return [(cx + R * math.cos(a_end * k / n), spring + R * math.sin(a_end * k / n)) for k in range(n + 1)], cx, R


def halfw_at(y, spring, cx, R):
    dy = y - spring
    return cx + math.sqrt(max(R * R - dy * dy, 0.0))


def strap(part, x_hinge, side, y, length, half, z0, z1):
    xe = x_hinge - side * length
    pts = [(x_hinge, y - half), (xe + side * 0.12, y - half * 0.55), (xe, y), (xe + side * 0.12, y + half * 0.55), (x_hinge, y + half)]
    ext(part, pts, z0, z1, "iron")


def faces2(thick, proud):
    return [tuple(sorted((f * (thick / 2 - 0.005), f * (thick / 2 + proud)))) for f in (-1, 1)]


def band(part, p0, p1, wd, z0, z1, field="iron"):
    """Schraeges Band von p0 nach p1 der Breite wd."""
    dx, dy = p1[0] - p0[0], p1[1] - p0[1]
    ln = math.hypot(dx, dy)
    nx, ny = -dy / ln * wd / 2, dx / ln * wd / 2
    ext(part, [(p0[0] - nx, p0[1] - ny), (p1[0] - nx, p1[1] - ny), (p1[0] + nx, p1[1] + ny), (p0[0] + nx, p0[1] + ny)], z0, z1, field)


def leaves_a():
    """Runde 2: vier gleiche Beschlagbaender im gleichen Abstand (unter dem Bogenansatz), feine Trittbleche am Fuss."""
    spring, apex, hwL, thick = 3.2, 5.18, 0.99, 0.24
    out = []
    for side, name in ((1, "leaf_a"), (-1, "leaf_b")):
        part = Part(name, (side * 1.0, 0, 0), LEAF_UV)
        arc, cx, R = arc_leaf(hwL, spring, apex)
        poly = [(side * x, y) for x, y in [(0.0, 0.0), (hwL, 0.0)] + arc]
        t = thick / 2
        ext(part, poly, -t, t, "planks")
        for z0, z1 in faces2(thick, 0.03):
            for y in STRAP_Y:
                strap(part, side * hwL, side, y, 0.8, 0.075, z0, z1)
            xa, xb = sorted((side * 0.12, side * (hwL - 0.12)))
            ext(part, [(xa, 0.05), (xb, 0.05), (xb, 0.42), (xa, 0.42)], z0, z1, "plate")
            xa, xb = sorted((side * 0.0, side * 0.08))
            ext(part, [(xa, 0.5), (xb, 0.5), (xb, 4.3), (xa, 4.3)], z0, z1, "iron")
        out.append(part)
    return out


STRAP_Y = (0.8, 1.5, 2.2, 2.9)      # gleicher Abstand 0,7 m; das oberste Band liegt unter dem Bogenansatz (3,2 m)


def leaves_c():
    spring, apex, hwL, thick = 3.2, 4.91, 0.99, 0.22
    out = []
    for side, name in ((1, "leaf_a"), (-1, "leaf_b")):
        part = Part(name, (side * 1.0, 0, 0), LEAF_UV)
        arc, cx, R = arc_leaf(hwL, spring, apex)
        poly = [(side * x, y) for x, y in [(0.0, 0.0), (hwL, 0.0)] + arc]
        t = thick / 2
        ext(part, poly, -t, t, "planks")
        for z0, z1 in faces2(thick, 0.03):
            for y in (0.4, 3.0):
                xa, xb = sorted((side * 0.0, side * hwL))
                ext(part, [(xa, y - 0.08), (xb, y - 0.08), (xb, y + 0.08), (xa, y + 0.08)], z0, z1, "iron")
            band(part, (side * (hwL - 0.05), 0.45), (side * 0.12, 2.95), 0.15, z0, z1)
            band(part, (side * 0.12, 0.45), (side * (hwL - 0.05), 2.95), 0.15, z0, z1)
            xa, xb = sorted((side * 0.0, side * 0.1))
            ext(part, [(xa, 0.3), (xb, 0.3), (xb, 4.3), (xa, 4.3)], z0, z1, "iron")
            length = min(0.7, halfw_at(3.9, spring, cx, R) - 0.1)
            strap(part, side * hwL, side, 3.9, length, 0.08, z0, z1)
        out.append(part)
    return out


def leaf_b():
    hw, top, thick = 0.99, 4.98, 0.3
    part = Part("leaf", (1.0, 0, 0), LEAF_UV)
    t = thick / 2
    ext(part, [(-hw, 0.0), (hw, 0.0), (hw, top), (-hw, top)], -t, t, "planks")
    for z0, z1 in faces2(thick, 0.035):
        for x0, x1, y0, y1 in ((-hw, hw, 0.0, 0.16), (-hw, hw, top - 0.16, top), (-hw, -hw + 0.16, 0.0, top),
                               (hw - 0.16, hw, 0.0, top), (-0.07, 0.07, 0.0, top)):
            ext(part, [(x0, y0), (x1, y0), (x1, y1), (x0, y1)], z0, z1, "iron")
        for y in (1.0, 2.0, 3.0, 4.0):
            ext(part, [(-hw, y - 0.07), (hw, y - 0.07), (hw, y + 0.07), (-hw, y + 0.07)], z0, z1, "iron")
        strap(part, hw, 1, 0.5, 0.0, 0.0, z0, z1) if False else None
        for y in (0.6, 2.5, 4.4):
            strap(part, hw, 1, y, 0.95, 0.13, z0 if z1 < 0 else z0 + 0.02 if False else z0, z1)
        ext(part, [(-0.78, 1.25), (-0.32, 1.25), (-0.32, 1.75), (-0.78, 1.75)], z0, z1, "plate")
    return [part]


# --- Entwuerfe ---------------------------------------------------------------------------------------------------------------

DESIGNS = {
    "a": ("Dreilauf-Spitzbogen", "zweifluegelig", portal_a, leaves_a, 5.18, 5.95),
    "b": ("Sturzportal", "einfluegelig", portal_b, leaf_b, 4.98, 6.0),
    "c": ("Wimperg-Lanzette", "zweifluegelig", portal_c, leaves_c, 4.91, 6.0),
}


def build_design(key):
    _, _, pf, lf, _, _ = DESIGNS[key]
    parts = pf()
    portal = [parts[1], parts[-1]]
    leaves = lf()
    return portal, leaves


# --- Modelle -------------------------------------------------------------------------------------------------------------------

def write_model(name, tex, parts):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    coll = bpy.data.collections.new("Tuer")
    bpy.context.scene.collection.children.link(coll)
    image = save_image(name, tex, os.path.join(MODELLE, name + ".png"))
    mat = export_material(name, image)
    objs = [p.build(coll, mat, smooth=False) for p in parts]
    export_glb(objs, os.path.join(MODELLE, name + ".glb"))
    report_parts(name, objs)
    tris = sum(len(o.data.polygons) for o in objs)
    print(f"DREIECKE {name} {tris}")
    return tris


def step_modelle():
    os.makedirs(MODELLE, exist_ok=True)
    frame_tex, leaf_tex = frame_atlas(), leaf_atlas()
    print("FARBEN Portal-Atlas", PK.colours_of(frame_tex), "Fluegel-Atlas", PK.colours_of(leaf_tex))
    assert PK.colours_of(leaf_tex) <= 32
    for key in DESIGNS:
        portal, leaves = build_design(key)
        for p in portal + leaves:
            healed = heal(p)
            print(f"HEAL {key} {p.name} {healed}")
        for p in portal:
            ed = open_edges(p)
            kinds = {}
            for a, b in ed:
                kinds[classify(a, b, TOP)] = kinds.get(classify(a, b, TOP), 0) + 1
            print(f"KANTEN {key} {p.name} offen {kinds}")
            for a, b in ed:
                if classify(a, b, TOP) == "SONST":
                    print("SONST", [round(c, 3) for c in a], [round(c, 3) for c in b])
            assert "SONST" not in kinds, f"{key} {p.name}: offene Kanten an unerwarteter Stelle {kinds}"
        for p in leaves:
            o, m = PK.edge_report(p)
            print(f"KANTEN {key} {p.name} offen {o} mehrfach {m}")
        write_model(f"bosstor_gross_{key}_portal", frame_tex, portal)
        write_model(f"bosstor_gross_{key}_fluegel", leaf_tex, leaves)


# --- Szenen und Bilder ---------------------------------------------------------------------------------------------------------

ATLAS = {}


def atlas_images():
    if not ATLAS:
        ATLAS["frame"] = save_image("a_frame", frame_atlas(), os.path.join(TMP, "a_frame.png"))
        ATLAS["leaf"] = save_image("a_leaf", leaf_atlas(), os.path.join(TMP, "a_leaf.png"))
        ATLAS["sil"] = save_image("a_sil", np.tile(np.array((150, 150, 168), float), (8, 8, 1)), os.path.join(TMP, "a_sil.png"))
        ATLAS["floor"] = stone_textures(TMP)
    return ATLAS


def new_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    ATLAS.clear()
    coll = bpy.data.collections.new("Szene")
    bpy.context.scene.collection.children.link(coll)
    return coll


def place(part, coll, mat, offset=(0, 0, 0), rot=0.0):
    heal(part)
    o = part.build(coll, mat, smooth=False)
    o.matrix_world = Matrix.Translation(G(part.pivot + V(*offset))) @ Matrix.Rotation(math.radians(rot), 4, "Z")
    return o


def add_design(coll, key, dz=-1.0, with_leaves=True, only=None, swing=0.0):
    A = atlas_images()
    portal, leaves = build_design(key)
    objs = []
    mf = pass_material("m_frame", A["frame"])
    ml = pass_material("m_leaf", A["leaf"])
    for p in portal:
        if only is None or only == p.name:
            objs.append(place(p, coll, mf, (0, 0, dz)))
    if with_leaves:
        for p in leaves:
            sgn = 1.0 if p.pivot.x > 0 else -1.0      # Fluegel schwingen zur -z-Seite (Bossraum)
            objs.append(place(p, coll, ml, (0, 0, dz), rot=-sgn * swing))
    return objs


def add_wall(coll, x0, x1, h=TOP, z=0.0, facing=1):
    A = atlas_images()
    part = Part("wand", (0, 0, 0), FRAME_UV)
    W(part, [(x0, 0, z), (x1, 0, z), (x1, h, z), (x0, h, z)], (0, 0, facing), "stone")
    return [place(part, coll, pass_material("m_wall", A["frame"]))]


def add_floor(coll, x0=-9, x1=9, z0=-4, z1=16, y=0.0, which="floor"):
    A = atlas_images()
    part = Part(which, (0, 0, 0), {"all": (0.0, 0.0, 1.0, 1.0)})
    verts = [part.bm.verts.new(G(V(x, y, z))) for x, z in ((x0, z0), (x1, z0), (x1, z1), (x0, z1))]
    f = part._face(verts, [(x0, z0), (x1, z0), (x1, z1), (x0, z1)])
    if which == "ceiling":
        f.normal_flip()
    return [part.build(coll, pass_material("m_" + which + str(y) + str(x0), A["floor"][which]), smooth=False)]


def add_boxes(coll, boxes, name="fig"):
    A = atlas_images()
    part = Part(name, (0, 0, 0), {"all": (0.0, 0.0, 1.0, 1.0)})
    for lo, hi in boxes:
        part.box(lo, hi, "all")
    return [part.build(coll, pass_material("m_" + name, A["sil"]), smooth=False)]


def player(x, z=1.6):
    """Spieler-Silhouette 1,8 m: Beine, Rumpf, Arme, Kopf (Kisten)."""
    return [((x - 0.2, 0, z - 0.1), (x - 0.02, 0.85, z + 0.1)), ((x + 0.02, 0, z - 0.1), (x + 0.2, 0.85, z + 0.1)),
            ((x - 0.22, 0.85, z - 0.13), (x + 0.22, 1.5, z + 0.13)), ((x - 0.34, 0.9, z - 0.08), (x - 0.22, 1.5, z + 0.08)),
            ((x + 0.22, 0.9, z - 0.08), (x + 0.34, 1.5, z + 0.08)), ((x - 0.13, 1.5, z - 0.13), (x + 0.13, 1.8, z + 0.13))]


def import_glb(coll, path, offset, scale=1.0, prefix="imp"):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    objs = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    out = []
    for o in objs:
        img = None
        for m in o.data.materials:
            if m and m.node_tree:
                for n in m.node_tree.nodes:
                    if n.type == "TEX_IMAGE":
                        img = n.image
        mw = o.matrix_world.copy()
        o.parent = None
        o.matrix_world = Matrix.Translation(G(V(*offset))) @ Matrix.Scale(scale, 4) @ mw
        o.data.materials.clear()
        o.data.materials.append(pass_material(prefix + "_" + o.name, img))
        for c in list(o.users_collection):
            c.objects.unlink(o)
        coll.objects.link(o)
        out.append(o)
    for o in before ^ set(bpy.data.objects):
        if o.type != "MESH" and o.name in bpy.data.objects:
            bpy.data.objects.remove(o)
    return out


def finish(objs):
    bpy.context.view_layer.update()
    for o in objs:
        o["smooth"] = False
        write_light(o)


def shoot(coll_builder, loc, tgt, fov, size, mode="hell", ortho=None, tag="b549"):
    coll = new_scene()
    objs = coll_builder(coll)
    finish(objs)
    cam = camera()
    setup_render(*size)
    if ortho:
        cam.data.type = "ORTHO"
        cam.data.ortho_scale = ortho
    else:
        cam.data.angle = math.radians(fov)
    aim(cam, G(V(*loc)), G(V(*tgt)))
    p = render_passes(tag)
    if mode == "hell":
        base, light = p["base"][..., :3] * 255, p["light"][..., :1]
        cov = p["base"][..., 3:4] > 0.5
        img = np.where(cov, np.clip(base * light * 1.3, 0, 255), BG)
    else:
        img = compose(p, "spiel")
    return np.round(img[::-1])                                      # Zeile 0 oben


# --- Beschriftung -----------------------------------------------------------------------------------------------------------

def text(img, s, x, y, size=18, color=INK):
    f = blf.load("C:/Windows/Fonts/arial.ttf")
    blf.size(f, size)
    w, h = blf.dimensions(f, s)
    wd, hd = int(w) + 8, int(size * 1.6)
    ib = imbuf.new((wd, hd))
    blf.color(f, 1, 1, 1, 1)
    blf.position(f, 4, int(size * 0.4), 0)
    with blf.bind_imbuf(f, ib):
        blf.draw_buffer(f, s)
    tmp = os.path.join(TMP, "txt.png")
    ib.filepath = tmp
    imbuf.write(ib)
    im = bpy.data.images.load(tmp)
    im.colorspace_settings.name = "Non-Color"
    a = np.array(im.pixels[:], np.float32).reshape(hd, wd, 4)[::-1, :, 3]
    bpy.data.images.remove(im)
    hh, ww = img.shape[:2]
    ye, xe = min(hh, y + hd), min(ww, x + wd)
    if ye <= y or xe <= x:
        return
    mk = (a[:ye - y, :xe - x] > 0.5)[..., None]
    img[y:ye, x:xe] = np.where(mk, np.array(color, float), img[y:ye, x:xe])


def png(img, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    save_png(np.asarray(img, float)[::-1], path)


def hcat(images, gap=8, fill=PAPER):
    h = max(a.shape[0] for a in images)
    pads = [a if a.shape[0] == h else np.concatenate([a, np.tile(BG, (h - a.shape[0], a.shape[1], 1))], 0) for a in images]
    out = [pads[0]]
    for a in pads[1:]:
        out += [np.tile(fill, (h, gap, 1)), a]
    return np.concatenate(out, axis=1)


def vcat(images, gap=8, fill=PAPER):
    w = max(a.shape[1] for a in images)
    pads = [a if a.shape[1] == w else np.concatenate([a, np.tile(BG, (a.shape[0], w - a.shape[1], 1))], 1) for a in images]
    out = [pads[0]]
    for a in pads[1:]:
        out += [np.tile(fill, (gap, w, 1)), a]
    return np.concatenate(out, axis=0)


def titled(img, s, size=20):
    img = img.copy()
    text(img, s, 8, 6, size)
    return img


# --- Ansichten je Entwurf ------------------------------------------------------------------------------------------------------

def view_front(key, size=(480, 720)):
    def b(coll):
        return add_wall(coll, -6, -POST) + add_wall(coll, POST, 6) + add_design(coll, key, dz=-1.0) + add_floor(coll)
    return shoot(b, (0, 3.0, 8), (0, 3.0, 0), 0, size, "hell", ortho=6.6)


def view_side(key, size=(480, 720)):
    def b(coll):
        return add_design(coll, key, dz=0.0) + add_floor(coll, -4, 4, -6, 6)
    return shoot(b, (8, 3.0, 0), (0, 3.0, 0), 0, size, "hell", ortho=6.6)


def view_back(key, size=(480, 720)):
    def b(coll):
        return add_design(coll, key, dz=0.0, only="portal_back") + add_floor(coll, -4, 4, -6, 6)
    return shoot(b, (0, 3.0, -8), (0, 3.0, 0), 0, size, "hell", ortho=6.6)


def view_oblique(key, size=(640, 720)):
    def b(coll):
        return add_wall(coll, -7, -POST) + add_wall(coll, POST, 7) + add_design(coll, key, dz=-1.0) + add_floor(coll)
    return shoot(b, (6.2, 7.4, 8.5), (0, 2.6, 0), 45, size, "hell")


def view_eye(key, size=(854, 480), mode="spiel", dist=6.0):
    def b(coll):
        return add_wall(coll, -8, -POST) + add_wall(coll, POST, 8) + add_design(coll, key, dz=-1.0) + add_floor(coll)
    return shoot(b, (0, 1.6, dist), (0, 1.6 + 0.0, 0), 70, size, mode)


def view_eye_tall(key, size=(500, 720), mode="spiel"):
    """Augenhoehe 1,6 m, 6 m vor der Wand, Blick auf die Tuermitte; hoher Ausschnitt fuer das Blatt."""
    def b(coll):
        return add_wall(coll, -9, -POST) + add_wall(coll, POST, 9) + add_design(coll, key, dz=-1.0) + add_floor(coll)
    return shoot(b, (0, 1.6, 6.0), (0, 2.9, 0), 62, size, mode)


def step_ansichten():
    os.makedirs(ANSICHTEN, exist_ok=True)
    for key, (title, wings, *_r) in DESIGNS.items():
        png(view_front(key), os.path.join(ANSICHTEN, f"{key}_vorn.png"))
        png(view_side(key), os.path.join(ANSICHTEN, f"{key}_seite.png"))
        png(view_back(key), os.path.join(ANSICHTEN, f"{key}_hinten.png"))
        png(view_oblique(key), os.path.join(ANSICHTEN, f"{key}_schraeg-oben.png"))
        png(view_eye(key), os.path.join(ANSICHTEN, f"{key}_spiel.png"))


# --- Blaetter ---------------------------------------------------------------------------------------------------------------------

def step_blaetter():
    os.makedirs(ANSICHTEN, exist_ok=True)
    # 1. Die drei Entwuerfe nebeneinander
    panels = []
    for key, (title, wings, *_r) in DESIGNS.items():
        img = view_eye_tall(key)
        text(img, f"{key}   {title}", 10, 8, 26)
        text(img, f"{wings}, Augenhoehe 1,6 m, 6 m Abstand", 10, 40, 16, (190, 185, 200))
        panels.append(img)
    png(hcat(panels), os.path.join(HERE, "blatt-bossraum-tuer-549.png"))

    # 2. Groessenvergleich: Spieler 1,8 m und Gorm 3,2 m
    panels = []
    for key, (title, wings, *_r) in DESIGNS.items():
        def b(coll, key=key):
            objs = add_wall(coll, -6, -POST) + add_wall(coll, POST, 6) + add_design(coll, key, dz=-1.0) + add_floor(coll)
            objs += add_boxes(coll, player(-1.25, 1.2) + player(-0.5, 1.2)[:0])
            objs += import_glb(coll, os.path.join(MODELS, "gorm.glb"), (0.9, 0.0, 1.4), prefix="gorm")
            return objs
        img = shoot(b, (0, 3.0, 8), (0, 3.0, 0), 0, (520, 760), "hell", ortho=6.8)
        text(img, f"{key}   {title}", 10, 8, 24)
        text(img, "Spieler 1,8 m (links), Gorm 3,2 m (rechts)", 10, 728, 16, (190, 185, 200))
        # Hoehenmarken 2, 4, 6 m am linken Rand
        for m in (2, 4, 6):
            y = int(760 / 2 + (3.0 - m) * 760 / 6.8)
            img[y:y + 1, 480:520] = np.array((200, 200, 80))
            text(img, f"{m} m", 436, y - 22, 14, (200, 200, 80))
        panels.append(img)
    png(hcat(panels), os.path.join(HERE, "groessenvergleich-549.png"))


# --- Vorraum ----------------------------------------------------------------------------------------------------------------------

def quad(coll, pts, hint, name="w"):
    A = atlas_images()
    part = Part(name, (0, 0, 0), FRAME_UV)
    W(part, pts, hint, "stone")
    return [place(part, coll, pass_material("m_" + name, A["frame"]))]


def room(coll, d, key="a", cut=False, ceiling=True, tele=True):
    """Vorraum: Breite 6 m (x -3..3), Tiefe d (z 0..d), Hoehe 6 m. Stirnwand z = 0 mit Portal, Gang (2 x 3,5 m) in der Rueckwand."""
    o = []
    o += add_wall(coll, -3, -POST) + add_wall(coll, POST, 3)
    o += add_design(coll, key, dz=-1.0)
    o += quad(coll, [(-3, 0, 0), (-3, 0, d), (-3, TOP, d), (-3, TOP, 0)], (1, 0, 0), "wl")
    if not cut:
        o += quad(coll, [(3, 0, 0), (3, 0, d), (3, TOP, d), (3, TOP, 0)], (-1, 0, 0), "wr")
        o += quad(coll, [(-3, 0, d), (-1, 0, d), (-1, TOP, d), (-3, TOP, d)], (0, 0, -1), "wb1")
        o += quad(coll, [(1, 0, d), (3, 0, d), (3, TOP, d), (1, TOP, d)], (0, 0, -1), "wb2")
        o += quad(coll, [(-1, 3.5, d), (1, 3.5, d), (1, TOP, d), (-1, TOP, d)], (0, 0, -1), "wb3")
    # Gang: Seitenwaende und Decke, 4 m lang
    for sx in ((-1, 1) if not cut else ()):
        o += quad(coll, [(sx, 0, d), (sx, 0, d + 4), (sx, 3.5, d + 4), (sx, 3.5, d)], (-sx, 0, 0), "g%d" % sx)
    o += add_floor(coll, -3, 3, 0, d, 0.0, "floor")
    if not cut:
        o += add_floor(coll, -1, 1, d, d + 4, 0.0, "floor")
    if ceiling:
        o += add_floor(coll, -3, 3, 0, d, TOP, "ceiling")
    if not cut:
        o += add_floor(coll, -1, 1, d, d + 4, 3.5, "ceiling")
    if tele:
        o += import_glb(coll, TELEPORTER, (0.0, 0.0, 3.0 if d >= 6 else 2.2), prefix="tele")
    return o


def rect(img, x0, y0, x1, y1, col, fill=False, w=2):
    x0, y0, x1, y1 = int(x0), int(y0), int(x1), int(y1)
    c = np.array(col, float)
    if fill:
        img[y0:y1, x0:x1] = c
        return
    img[y0:y0 + w, x0:x1] = c
    img[y1 - w:y1, x0:x1] = c
    img[y0:y1, x0:x0 + w] = c
    img[y0:y1, x1 - w:x1] = c


def plan(d, label, sub, scale=44, size=(560, 600)):
    """Grundriss: Stirnwand oben (z = 0), Gang unten. Masse in Metern."""
    img = np.tile(PAPER, (size[1], size[0], 1))
    ox, oy = size[0] / 2, 110.0
    def X(x):
        return ox + x * scale
    def Z(z):
        return oy + z * scale
    gridc = (44, 40, 52)
    for gx in range(-4, 5):
        img[int(Z(-1)):int(Z(d + 4.5)), int(X(gx))] = gridc
    for gz in range(-1, int(d + 5)):
        img[int(Z(gz)), int(X(-4)):int(X(4))] = gridc
    rect(img, X(-3), Z(0), X(3), Z(d), (150, 146, 162), fill=True)
    rect(img, X(-1), Z(d), X(1), Z(d + 4), (110, 106, 122), fill=True)
    rect(img, X(-3), Z(0), X(3), Z(d), (235, 230, 240), w=4)
    img[int(Z(d)) - 2:int(Z(d)) + 3, int(X(-1)):int(X(1))] = np.array((110, 106, 122))
    rect(img, X(-1), Z(d), X(1), Z(d + 4), (235, 230, 240), w=3)
    rect(img, X(-1), Z(d) - 2, X(1), Z(d) + 3, (110, 106, 122), fill=True)
    rect(img, X(-2), Z(-0.55), X(2), Z(0.0), (190, 160, 90), fill=True)          # Portal 4 m
    tz = 3.0 if d >= 6 else 2.2
    for a in range(0, 360, 6):
        r = 0.88
        xx, zz = int(X(r * math.cos(math.radians(a)))), int(Z(tz + r * math.sin(math.radians(a))))
        img[zz - 1:zz + 2, xx - 1:xx + 2] = np.array((150, 200, 230))
    rect(img, X(-0.25), Z(tz - 0.25), X(0.25), Z(tz + 0.25), (150, 200, 230), fill=True)
    for yy in range(int(Z(d + 3.3)), int(Z(d + 0.6)), -2):                   # Pfeil Gang -> Raum
        img[yy:yy + 1, int(X(0))] = np.array((235, 230, 240))
    for k in range(7):
        img[int(Z(d + 0.6)) + k, int(X(0)) - k:int(X(0)) + k + 1] = np.array((235, 230, 240))
    text(img, label, 12, 8, 24)
    text(img, sub, 12, 40, 15, (190, 185, 200))
    text(img, "Tuer 4 m (Portal)", int(X(2.1)), int(Z(-0.7)), 14, (190, 160, 90))
    text(img, "Teleporter", int(X(1.1)), int(Z(tz)) - 8, 14, (150, 200, 230))
    text(img, "Gang 2 m", int(X(1.2)), int(Z(d + 2)), 14)
    text(img, f"6 m breit, {d:g} m tief, 6 m hoch", int(X(-3)), int(Z(d + 4.4)), 15)
    return img


def step_vorraum():
    os.makedirs(ANSICHTEN, exist_ok=True)
    def bl(coll):
        return room(coll, 6.0, "a")
    game = shoot(bl, (0.0, 1.6, 9.2), (0.0, 2.4, 0.0), 70, (854, 480), "spiel")
    png(game, os.path.join(ANSICHTEN, "vorraum_spiel.png"))
    eye = shoot(lambda c: room(c, 6.0, "a"), (0.0, 1.6, 9.2), (0.0, 2.4, 0.0), 70, (640, 480), "hell")
    top = shoot(lambda c: room(c, 6.0, "a", cut=True, ceiling=False), (10.0, 11.0, 10.0), (0.0, 1.2, 2.2), 50, (640, 480), "hell")
    top2 = shoot(lambda c: room(c, 4.0, "a", cut=True, ceiling=False), (10.0, 11.0, 8.0), (0.0, 1.2, 1.8), 50, (640, 480), "hell")
    info = np.tile(PAPER, (480, 434, 1))
    for k, ln in enumerate(["Vorraum (Empfehlung 3 x 3 Zellen)", "", "Gang: 2 m breit, 3,5 m hoch", "Raum: 6 x 6 m, 6 m hoch",
                            "Stirnwand: Portal 4 m breit,", "  Tuer 2 m x 6 m (Entwurf a)", "Teleporter (Form C): 1,76 m breit,",
                            "  2,65 m hoch, Mitte des Raums", "Oben links: Blick aus dem Gang", "  in Spielaufloesung 854 x 480"]):
        text(info, ln, 16, 20 + k * 30, 17)
    row1 = hcat([titled(game, "Blick aus dem Gang (Spielaufloesung, 70 Grad, Augen 1,6 m)", 18), info])
    row2 = hcat([titled(top, "3 x 3 Zellen (6 x 6 m), von schraeg oben, Waende zur Ansicht offen", 16),
                 titled(top2, "3 x 2 Zellen (6 x 4 m), von schraeg oben", 16)])
    p1 = plan(6.0, "Grundriss 3 x 3 Zellen (empfohlen)", "Gang 3,5 m hoch, Raum 6 m hoch, Raster 1 m", size=(640, 640))
    p2 = plan(4.0, "Grundriss 3 x 2 Zellen", "Teleporter ruecknah an die Tuer gerueckt", size=(640, 640))
    row3 = hcat([p1, p2])
    w = max(row1.shape[1], row2.shape[1], row3.shape[1])
    def padw(a):
        return a if a.shape[1] == w else np.concatenate([a, np.tile(PAPER, (a.shape[0], w - a.shape[1], 1))], 1)
    sheet = vcat([padw(row1), padw(row2), padw(row3)])
    png(sheet, os.path.join(HERE, "vorraum-549.png"))


# --- Runde 2: nur Entwurf a ---------------------------------------------------------------------------------------------

def scene_a(side, swing):
    """side +1: Blick von der Vorraum-Seite (+z), -1: aus dem Bossraum (-z). Waende buendig mit den Wandebenen."""
    def b(coll):
        if side > 0:
            o = add_wall(coll, -9, -POST) + add_wall(coll, POST, 9) + add_design(coll, "a", dz=-1.0, swing=swing)
            return o + add_floor(coll, -9, 9, -10, 10)
        o = add_wall(coll, -9, -POST, z=-1.0, facing=-1) + add_wall(coll, POST, 9, z=-1.0, facing=-1)
        o += add_design(coll, "a", dz=0.0, only="portal_back", swing=swing)
        return o + add_floor(coll, -9, 9, -16, 4)
    return b


def step_r2blatt():
    panels = []
    for side, swing, label, sub in ((1, 0, "a  vorn, zu", "von der Vorraum-Seite"), (-1, 0, "a  hinten, zu", "aus dem Bossraum"),
                                    (1, 90, "a  vorn, offen", "Fluegel schwingen in den Bossraum"),
                                    (-1, 90, "a  hinten, offen", "aus dem Bossraum, Fluegel zeigen zu dir")):
        loc = (0.0, 1.6, 6.0) if side > 0 else (0.0, 1.6, -7.0)
        tgt = (0.0, 2.9, 0.0) if side > 0 else (0.0, 2.9, -1.0)
        img = shoot(scene_a(side, swing), loc, tgt, 62, (400, 640), "spiel")
        text(img, label, 10, 8, 24)
        text(img, sub, 10, 40, 14, (190, 185, 200))
        panels.append(img)

    def b(coll):
        objs = add_wall(coll, -6, -POST) + add_wall(coll, POST, 6) + add_design(coll, "a", dz=-1.0) + add_floor(coll)
        objs += add_boxes(coll, player(-1.45, 1.2))
        objs += import_glb(coll, os.path.join(MODELS, "gorm.glb"), (1.1, 0.0, 1.4), prefix="gorm")
        return objs
    img = shoot(b, (0, 3.0, 8), (0, 3.0, 0), 0, (460, 640), "hell", ortho=6.8)
    for m in (2, 4, 6):
        y = int(640 / 2 + (3.0 - m) * 640 / 6.8)
        img[y:y + 1, 410:460] = np.array((200, 200, 80))
        text(img, f"{m} m", 366, y - 22, 14, (200, 200, 80))
    text(img, "a  Groesse", 10, 8, 24)
    text(img, "Spieler 1,8 m, Gorm 3,2 m", 10, 616, 14, (190, 185, 200))
    panels.append(img)
    png(hcat(panels), os.path.join(HERE, "blatt-bossraum-tuer-549-r2.png"))
    for n, im in zip(("vorn_zu", "hinten_zu", "vorn_offen", "hinten_offen"), panels):
        png(im, os.path.join(ANSICHTEN, f"r2_a_{n}.png"))


TELE_POS = (-1.0, 0.0, 7.5)


def room2(coll, cut=False, ceiling=True, swing=0.0):
    """Vorraum 4 x 5 Zellen: x -4..4 (8 m, 4 Zellen), z 0..10 (10 m, 5 Zellen), 6 m hoch.
    Stirnwand z = 0 (8 m) mit Portal ueber den zwei mittleren Zellen; Eingang mittig in der Wand x = 4 (10 m): Gang z 4..6, 4 m lang."""
    o = add_wall(coll, -4, -POST) + add_wall(coll, POST, 4) + add_design(coll, "a", dz=-1.0, swing=swing)
    o += quad(coll, [(-4, 0, 0), (-4, 0, 10), (-4, TOP, 10), (-4, TOP, 0)], (1, 0, 0), "wl")
    if not cut:
        o += quad(coll, [(-4, 0, 10), (4, 0, 10), (4, TOP, 10), (-4, TOP, 10)], (0, 0, -1), "ws")
        o += quad(coll, [(4, 0, 0), (4, 0, 4), (4, TOP, 4), (4, TOP, 0)], (-1, 0, 0), "we1")
        o += quad(coll, [(4, 0, 6), (4, 0, 10), (4, TOP, 10), (4, TOP, 6)], (-1, 0, 0), "we2")
        o += quad(coll, [(4, 3.5, 4), (4, 3.5, 6), (4, TOP, 6), (4, TOP, 4)], (-1, 0, 0), "we3")
        for z in (4, 6):
            o += quad(coll, [(4, 0, z), (8, 0, z), (8, 3.5, z), (4, 3.5, z)], (0, 0, 1 if z == 4 else -1), "g%d" % z)
        o += add_floor(coll, 4, 8, 4, 6, 0.0, "floor") + add_floor(coll, 4, 8, 4, 6, 3.5, "ceiling")
    o += add_floor(coll, -4, 4, 0, 10, 0.0, "floor")
    if ceiling:
        o += add_floor(coll, -4, 4, 0, 10, TOP, "ceiling")
    o += import_glb(coll, TELEPORTER, TELE_POS, prefix="tele")
    return o


def plan2(size=(640, 640), scale=40):
    img = np.tile(PAPER, (size[1], size[0], 1))
    ox, oy = 200.0, 120.0       # x = 0 liegt bei ox, z = 0 bei oy

    def X(x):
        return ox + x * scale

    def Z(z):
        return oy + z * scale
    rect(img, X(-4), Z(0), X(4), Z(10), (150, 146, 162), fill=True)
    rect(img, X(4), Z(4), X(8), Z(6), (110, 106, 122), fill=True)
    rect(img, X(-2), Z(-0.5), X(2), Z(0.0), (190, 160, 90), fill=True)                 # Portal ueber den zwei mittleren Zellen
    rect(img, X(-1), Z(-0.5), X(1), Z(0.0), (60, 50, 30), fill=True)                   # Oeffnung 2 m
    for gx in range(-4, 5, 2):
        img[int(Z(0)):int(Z(10)), int(X(gx))] = np.array((96, 92, 108))
    for gz in range(0, 11, 2):
        img[int(Z(gz)), int(X(-4)):int(X(4))] = np.array((96, 92, 108))
    rect(img, X(-4), Z(0), X(4), Z(10), (235, 230, 240), w=4)
    rect(img, X(4), Z(4), X(8), Z(6), (235, 230, 240), w=3)
    rect(img, X(4) - 3, Z(4) + 2, X(4) + 4, Z(6) - 1, (110, 106, 122), fill=True)
    tx, tz = TELE_POS[0], TELE_POS[2]
    for a in range(0, 360, 5):
        xx, zz = int(X(tx + 0.88 * math.cos(math.radians(a)))), int(Z(tz + 0.88 * math.sin(math.radians(a))))
        img[zz - 1:zz + 2, xx - 1:xx + 2] = np.array((150, 200, 230))
    rect(img, X(tx - 0.25), Z(tz - 0.25), X(tx + 0.25), Z(tz + 0.25), (150, 200, 230), fill=True)
    for xx in range(int(X(7.6)), int(X(4.8)), -2):                                      # Pfeil: Eingang -> Raum
        img[int(Z(5)), xx] = np.array((235, 230, 240))
    for k in range(7):
        img[int(Z(5)) - k:int(Z(5)) + k + 1, int(X(4.8)) + k] = np.array((235, 230, 240))
    text(img, "Grundriss Vorraum, 4 x 5 Zellen", 12, 8, 24)
    text(img, "Raster: 1 Zelle = 2 m", 12, 40, 15, (190, 185, 200))
    text(img, "Bossraum-Tuer: Portal 4 m = die zwei mittleren Zellen", int(X(-4)), int(Z(-0.5)) - 40, 14, (190, 160, 90))
    text(img, "Mitte der 4er-Seite = Zellgrenze (x = 0)", int(X(-4)), int(Z(-0.5)) - 22, 14, (190, 160, 90))
    text(img, "Eingang", int(X(5.0)), int(Z(4)) - 22, 14)
    text(img, "Gang 2 m x 4 m, 3,5 m hoch", int(X(4.2)), int(Z(6)) + 8, 14)
    text(img, "Teleporter", int(X(tx + 1.0)), int(Z(tz)) - 8, 14, (150, 200, 230))
    text(img, "8 m (4 Zellen)", int(X(-1.3)), int(Z(10)) + 8, 15)
    text(img, "10 m (5 Zellen)", int(X(-4)) - 4, int(Z(10)) + 30, 15)
    return img


def step_r2vorraum():
    ent = shoot(lambda c: room2(c), (4.4, 1.6, 5.0), (-3.0, 1.8, 2.4), 70, (854, 480), "spiel")
    png(ent, os.path.join(ANSICHTEN, "r2_vorraum_vom_eingang.png"))
    tur = shoot(lambda c: room2(c), (3.2, 1.6, 9.5), (0.0, 2.6, 0.0), 70, (640, 480), "spiel")
    top = shoot(lambda c: room2(c, cut=True, ceiling=False), (13.0, 13.0, 14.0), (-0.5, 1.0, 4.0), 48, (640, 480), "hell")
    row1 = hcat([titled(ent, "Blick vom Eingang (Spielaufloesung, 70 Grad, Augen 1,6 m)", 18),
                 titled(top, "von schraeg oben, Waende zur Ansicht offen", 16)])
    row2 = hcat([plan2(), titled(tur, "Blick zur Tuer von der Suedwand", 16)])
    w = max(row1.shape[1], row2.shape[1])

    def padw(a):
        return a if a.shape[1] == w else np.concatenate([a, np.tile(PAPER, (a.shape[0], w - a.shape[1], 1))], 1)
    png(vcat([padw(row1), padw(row2)]), os.path.join(HERE, "vorraum-549-r2.png"))


if __name__ == "__main__":
    steps = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    steps = steps or ["modelle", "ansichten", "blaetter", "vorraum"]
    for s in steps:
        {"modelle": step_modelle, "ansichten": step_ansichten, "blaetter": step_blaetter, "vorraum": step_vorraum, "r2blatt": step_r2blatt, "r2vorraum": step_r2vorraum}[s]()
