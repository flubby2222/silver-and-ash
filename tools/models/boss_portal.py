"""Bossraum-Portal (Issue #568): grosses Spitzbogen-Portal ueber zwei Zellen und seine zwei Fluegel.

Erzeuger fuer das Spiel. Uebergabe aus dem Probestueck #549 (Branch feat/bossraum-tuer-probe-549,
design/proben/grafik/bossraum-tuer-549/bossraum_tuer.py), Entwurf a "Dreilauf-Spitzbogen" in der Fassung der Runde 3:
hohe Platte unten (0,87 m), drei gleiche Beschlaege im gleichen Abstand. Erzeugt nur Modelle und Texturen nach
assets/models/, keine Probebilder, dazu die Liste der gelesenen Wand-Texturen (tools/models/boss_portal_sources.sha256).

Aufruf (aus dem Repository-Wurzelverzeichnis), danach python tools/models/check_closed.py und tools/models/checksums.py:
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/models/boss_portal.py

Alles per Skript, nichts heruntergeladen. Spiel-Achsen (x seitlich, y oben, z vorn), Meter. Ursprung: Mitte der Tuer auf
dem Boden, also auf der Grenze der beiden Tuerzellen; der Durchgang laeuft entlang z, die Wandebenen liegen bei z = +-1.
Das Portal fuellt 4 m Wandbreite (x = -2 .. 2) und 6 m Hoehe (die Raumhoehe); die lichte Oeffnung ist 2 m breit und
5,2 m hoch.

  door_boss.glb             die beiden Fluegel leaf_a (Drehpunkt x = +1) und leaf_b (Drehpunkt x = -1), Atlas 128 x 128,
                            hoechstens 32 Farben, Holz und Eisen des Bosstors.
  door_frame_boss_<v>.glb   das Steinportal fuer die Wandvariante v (a, b, c): portal_front (Stein der +z-Seite) und
                            portal_back (Stein der -z-Seite). Atlas 128 x 64: links die echte Wand-Textur der Variante
                            (bitgleich gelesen), rechts dieselbe Kachel als Werkstein (Fassung "blau" wie door.py).

Steinflaechen tragen Textur-Koordinaten aus der Welt (eine 64-px-Kachel je Meter, an den Metergrenzen geteilt). Jeder
Koerper ist rundum geschlossen (Art-Bible, Regel 21): das Portal bekommt Kappen an der Tuerebene (z = 0), an den Seiten,
oben und unten; Sockel, Kaempfer, Strebepfeiler und Fialen sind eigene geschlossene Koerper, die das Portal schneiden.
Wo das Teilen an den Metergrenzen eine Kante zerreisst, fuegt heal() die fehlende Ecke ein; das Skript bricht ab, wenn
danach noch eine Kante offen ist.
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bmesh  # noqa: E402
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402
import pillar_kit as PK  # noqa: E402
from psx import Part, rects_from_pixels, vnoise, blob, mix, paint_atlas, save_image, export_material, export_glb, \
    report_parts  # noqa: E402

TOOLS = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(TOOLS))
OUT_DIR = os.path.join(ROOT, "assets", "models")
SOURCES = os.path.join(TOOLS, "boss_portal_sources.sha256")

TOP = 6.0                     # Raumhoehe = Oberkante des Portals
POST = 2.0                    # Portal fuellt x = -2 .. 2
SOCKEL = 0.5
DOOR_Z = 0.12                 # Fluegel: z = -0.12 .. 0.12 (Dicke 0,24)
BACK_Z = 0.0                  # Kappe jeder Portalseite in der Tuerebene: die beiden Haelften stossen ohne Spalt aneinander

STRAP_Y = (1.1, 1.95, 2.8)    # drei gleiche Beschlaege, Abstand 0,85 m; der oberste liegt unter dem Bogenansatz (3,2 m)
PLATE = (0.08, 0.95)          # hohe Platte unten, 0,87 m (Urteil des Menschen zu Runde 3)

# --- Atlanten ------------------------------------------------------------------------------------------------------------

FRAME_SIZE = (128, 64)
FRAME_RECTS = {"stone": (0, 0, 64, 64), "werk": (64, 0, 128, 64)}
FRAME_UV = rects_from_pixels(FRAME_RECTS, FRAME_SIZE)
LEAF_SIZE = (128, 128)
LEAF_RECTS = {"planks": (0, 0, 64, 64), "plate": (64, 0, 128, 64), "iron": (0, 64, 64, 128), "edge": (64, 64, 128, 128)}
LEAF_UV = rects_from_pixels(LEAF_RECTS, LEAF_SIZE)
WOOD = np.array((40, 34, 42), float)       # Holz des Bosstors
IRON = np.array((112, 108, 126), float)    # Eisen des Bosstors
DARK = np.array((16, 12, 16), float)


def werk_tile(tile, stein, werkstein):
    """Dieselbe Kachel als Werkstein (wie door.py, Fassung blau): jede Farbe auf die naechste Stein-Stufe, so weit hinauf,
    dass die hellste auf die hellste Stufe faellt (mindestens 4), dann die Stufen aus der Reihe Werkstein."""
    flat = tile.reshape(-1, 3)
    colours = sorted({tuple(int(c) for c in p) for p in flat})
    idx = {c: int(((stein - np.array(c)) ** 2).sum(1).argmin()) for c in colours}
    top = len(stein) - 1
    shift = max(4, top - max(idx.values()))
    lifted = {c: min(i + shift, top) for c, i in idx.items()}
    first = len(stein) - len(werkstein)
    out = {c: werkstein[max(i - first, 0)] for c, i in lifted.items()}
    return np.array([out[tuple(int(c) for c in p)] for p in flat]).reshape(tile.shape)


def frame_atlas(variant):
    rows = PK.palette_rows()
    stein, werkstein = rows["Stein"], rows["Werkstein"]
    tile = PK.load_tile(PK.WALL_FILES[variant])
    werk = werk_tile(tile, stein, werkstein)
    w, h = FRAME_SIZE
    atlas = np.zeros((h, w, 3))
    for name, t in (("stone", tile), ("werk", werk)):
        x0, y0, x1, y1 = FRAME_RECTS[name]
        atlas[y0:y1, x0:x1] = t[::-1]                                       # Feld: Zeile 0 unten
    allowed = {tuple(int(c) for c in p) for p in np.concatenate([stein, werkstein])}
    used = {tuple(int(c) for c in p) for p in atlas.reshape(-1, 3)}
    assert used <= allowed, f"Portal-Atlas {variant}: Farben ausserhalb der Reihen Stein und Werkstein"
    assert PK.colours_of(atlas) <= 32, f"Portal-Atlas {variant}: {PK.colours_of(atlas)} Farben (hoechstens 32)"
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
    assert PK.colours_of(tex) <= 32, f"Fluegel-Atlas: {PK.colours_of(tex)} Farben (hoechstens 32)"
    return tex


# --- Flaechen und Koerper (alles mit Welt-UV) ---------------------------------------------------------------------------

def W(part, pts, hint, field):
    PK.emit(part, pts, hint, field)


def ext(part, poly, z0, z1, field):
    """Konvexes Vieleck (x, y) von z0 bis z1 (z0 < z1) als geschlossener Koerper."""
    n = len(poly)
    cx, cy = sum(p[0] for p in poly) / n, sum(p[1] for p in poly) / n
    for a, b in zip(poly, poly[1:] + poly[:1]):
        if math.hypot(a[0] - b[0], a[1] - b[1]) < 1e-7:
            continue
        mx, my = (a[0] + b[0]) / 2 - cx, (a[1] + b[1]) / 2 - cy
        W(part, [(a[0], a[1], z0), (b[0], b[1], z0), (b[0], b[1], z1), (a[0], a[1], z1)], (mx, my, 0), field)
    W(part, [(p[0], p[1], z1) for p in poly], (0, 0, 1), field)
    W(part, [(p[0], p[1], z0) for p in poly], (0, 0, -1), field)


def ebox(part, lo, hi, field):
    """Quader mit allen sechs Seiten."""
    x0, y0, z0 = lo
    x1, y1, z1 = hi
    for pts, hint in (([(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)], (0, 0, 1)),
                      ([(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0)], (0, 0, -1)),
                      ([(x1, y0, z0), (x1, y0, z1), (x1, y1, z1), (x1, y1, z0)], (1, 0, 0)),
                      ([(x0, y0, z0), (x0, y0, z1), (x0, y1, z1), (x0, y1, z0)], (-1, 0, 0)),
                      ([(x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1)], (0, 1, 0)),
                      ([(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)], (0, -1, 0))):
        W(part, pts, hint, field)


def pyramid(part, cx, y0, cz, half, tip, field):
    """Vierseitige Spitze (Fiale): Grundflaeche unten, vier Dreiecke."""
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
        for i, lp in enumerate(loops):
            a, b = np.array(lp.vert.co), np.array(loops[(i + 1) % n].vert.co)
            ua, ub = np.array(lp[uvl].uv), np.array(loops[(i + 1) % n][uvl].uv)
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
    # Ecken, die das Teilen und Einfuegen fast an dieselbe Stelle gelegt hat (Abstand unter 1/50 mm), zusammenlegen:
    # sonst bliebe eine Kante der Laenge null, die keine zweite Flaeche hat.
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=2e-5)
    loose = [v for v in bm.verts if not v.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context="VERTS")
    return len(todo)


# --- Portal ----------------------------------------------------------------------------------------------------------------

def arc_pts(xc, R, spring, n=8):
    rise = math.sqrt(R * R - xc * xc)
    a_end = math.atan2(rise, -xc)
    return [(xc + R * math.cos(a_end * j / n), spring + R * math.sin(a_end * j / n)) for j in range(n + 1)]


def mirror(sx, sz, x, y, z):
    return (sx * x, y, sz * z)


def zbox(part, sz, x0, x1, y0, y1, z_out, field="werk"):
    """Koerper vor der Wandebene der Seite sz (z von 1 bis z_out), x0..x1 positiv; fuer beide x-Seiten gespiegelt."""
    lo, hi = sorted((sz * 1.0, sz * z_out))
    for sx in (1, -1):
        a, b = sorted((sx * x0, sx * x1))
        ebox(part, (a, y0, lo), (b, y1, hi), field)


def pointed_portal(part, sz, hw_in, xc, spring, widths, zs):
    """Eine Seite (sz) des Spitzbogen-Portals mit gestuften Laeufen, als geschlossener Koerper zwischen Wandebene (|z| = 1)
    und Tuerebene (z = BACK_Z). Alle Kreise teilen den Mittelpunkt (xc, spring), xc < 0."""
    n = len(widths)
    R_in = hw_in - xc
    Rs = [R_in + sum(widths[k:]) for k in range(n)] + [R_in]
    hws = [R + xc for R in Rs]
    depths = [1.0] + list(zs) + [BACK_Z]
    a0 = arc_pts(xc, Rs[0], spring)
    a_in = arc_pts(xc, R_in, spring)
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
        # Kappen: Tuerebene (Pfosten und Feld ueber der inneren Leibung), Seite, Boden je Lauf
        W(part, [mirror(sx, sz, hw_in, 0, BACK_Z), mirror(sx, sz, POST, 0, BACK_Z), mirror(sx, sz, POST, spring, BACK_Z),
                 mirror(sx, sz, hw_in, spring, BACK_Z)], (0, 0, -sz), "stone")
        ring = [(POST, spring)] + a_in + [(0.0, TOP)]
        for p, q in zip(ring, ring[1:]):
            W(part, [mirror(sx, sz, POST, TOP, BACK_Z), mirror(sx, sz, p[0], p[1], BACK_Z),
                     mirror(sx, sz, q[0], q[1], BACK_Z)], (0, 0, -sz), "stone")
        W(part, [mirror(sx, sz, POST, 0, BACK_Z), mirror(sx, sz, POST, 0, 1.0), mirror(sx, sz, POST, TOP, 1.0),
                 mirror(sx, sz, POST, TOP, BACK_Z)], (sx, 0, 0), "stone")
        for k in range(n + 1):
            W(part, [mirror(sx, sz, hws[k], 0, depths[k]), mirror(sx, sz, POST, 0, depths[k]),
                     mirror(sx, sz, POST, 0, depths[k + 1]), mirror(sx, sz, hws[k], 0, depths[k + 1])], (0, -1, 0), "stone")
    lo, hi = sorted((sz * BACK_Z, sz * 1.0))                  # Kappe oben (Decke des Raums)
    W(part, [(-POST, TOP, lo), (POST, TOP, lo), (POST, TOP, hi), (-POST, TOP, hi)], (0, 1, 0), "stone")
    zbox(part, sz, hw_in, POST, 0.0, SOCKEL, 1.125)                           # Sockel
    zbox(part, sz, hw_in, hws[0] + 0.12, spring - 0.125, spring, 1.125)       # Kaempfer
    return Rs, hws


def portal():
    """Dreilauf-Spitzbogen, Strebepfeiler mit Fialen. Oeffnung 2,0 m, Scheitel der Oeffnung 5,2 m."""
    parts = []
    for sz, name in ((1, "portal_front"), (-1, "portal_back")):
        part = Part(name, (0, 0, 0), FRAME_UV)
        pointed_portal(part, sz, 1.0, -1.5, 3.2, [0.2, 0.2, 0.2], [0.85, 0.6, 0.35])
        zbox(part, sz, 1.72, POST, SOCKEL, 5.3, 1.24)                          # Strebepfeiler
        zbox(part, sz, 1.66, POST, 5.3, 5.45, 1.3)                             # Deckplatte
        for sx in (1, -1):
            pyramid(part, sx * 1.86, 5.45, sz * 1.15, 0.12, 5.95, "werk")      # Fiale
        parts.append(part)
    return parts


# --- Fluegel -----------------------------------------------------------------------------------------------------------------

def arc_leaf(hw, spring, apex, n=8):
    """Rechte Haelfte der Spitzbogenform des Fluegels: (hw, spring) bis (0, apex)."""
    rise = apex - spring
    R = (rise * rise + hw * hw) / (2 * hw)
    cx = hw - R
    a_end = math.atan2(rise, R - hw)
    return [(cx + R * math.cos(a_end * k / n), spring + R * math.sin(a_end * k / n)) for k in range(n + 1)]


def strap(part, x_hinge, side, y, length, half, z0, z1):
    xe = x_hinge - side * length
    pts = [(x_hinge, y - half), (xe + side * 0.12, y - half * 0.55), (xe, y), (xe + side * 0.12, y + half * 0.55),
           (x_hinge, y + half)]
    ext(part, pts, z0, z1, "iron")


def faces2(thick, proud):
    return [tuple(sorted((f * (thick / 2 - 0.005), f * (thick / 2 + proud)))) for f in (-1, 1)]


def leaves():
    """Zwei Fluegel im Spitzbogen; je Seite drei gleiche Beschlaege, die hohe Platte unten und die Mittelleiste."""
    spring, apex, hwL, thick = 3.2, 5.18, 0.99, 2 * DOOR_Z
    out = []
    for side, name in ((1, "leaf_a"), (-1, "leaf_b")):
        part = Part(name, (side * 1.0, 0, 0), LEAF_UV)
        poly = [(side * x, y) for x, y in [(0.0, 0.0), (hwL, 0.0)] + arc_leaf(hwL, spring, apex)]
        ext(part, poly, -thick / 2, thick / 2, "planks")
        for z0, z1 in faces2(thick, 0.03):
            for y in STRAP_Y:
                strap(part, side * hwL, side, y, 0.8, 0.075, z0, z1)
            xa, xb = sorted((side * 0.12, side * (hwL - 0.12)))
            ext(part, [(xa, PLATE[0]), (xb, PLATE[0]), (xb, PLATE[1]), (xa, PLATE[1])], z0, z1, "plate")
            xa, xb = sorted((side * 0.0, side * 0.08))
            ext(part, [(xa, 0.5), (xb, 0.5), (xb, 4.3), (xa, 4.3)], z0, z1, "iron")
        out.append(part)
    return out


# --- Ausgabe --------------------------------------------------------------------------------------------------------------

def open_edges(part):
    """Offene Kanten (Ecken gleicher Lage zusammengelegt) als Punktpaare in Spiel-Achsen, zum Nachsehen."""
    keys, edges = {}, {}
    for f in part.bm.faces:
        ids = [keys.setdefault(tuple(round(c * 1e5) for c in v.co), (len(keys), PK.G_inv(v.co))) for v in f.verts]
        for a, b in zip(ids, ids[1:] + ids[:1]):
            k = (min(a[0], b[0]), max(a[0], b[0]))
            edges.setdefault(k, [0, a[1], b[1]])[0] += 1
    return [(e[1], e[2]) for e in edges.values() if e[0] == 1]


def closed(parts, label):
    for p in parts:
        healed = heal(p)
        opened, multi = PK.edge_report(p)
        print(f"KANTEN {label} {p.name}: geheilt {healed}, offen {opened}, mehrfach {multi}")
        for a, b in open_edges(p):
            print("  offen", [round(c, 5) for c in a], [round(c, 5) for c in b])
        assert opened == 0, f"{label} {p.name}: {opened} offene Kanten"


def write(name, tex, parts):
    """Ein Modell mit eigener Textur nach assets/models/<name>.glb und .png."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    coll = bpy.data.collections.new("Tuer")
    bpy.context.scene.collection.children.link(coll)
    image = save_image(name, tex, os.path.join(OUT_DIR, name + ".png"))
    mat = export_material(name, image)
    objs = [p.build(coll, mat, smooth=False) for p in parts]
    export_glb(objs, os.path.join(OUT_DIR, name + ".glb"))
    report_parts(name, objs)
    h, w = tex.shape[:2]
    print(f"TEXTUR {name} {w}x{h} Farben {PK.colours_of(tex)}")


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    for variant in PK.WALL_FILES:
        tex = frame_atlas(variant)                    # vor dem Bauen: load_tile setzt Blender nicht zurueck, write schon
        parts = portal()
        closed(parts, f"door_frame_boss_{variant}")
        write(f"door_frame_boss_{variant}", tex, parts)
    tex = leaf_atlas()
    parts = leaves()
    closed(parts, "door_boss")
    write("door_boss", tex, parts)
    PK.write_sources(SOURCES, "tools/models/boss_portal.py", "boss portal")


main()
