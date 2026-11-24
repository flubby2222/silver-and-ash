"""Tueren (Issue #245, Rahmen #303): Normal, Locked und die Risswand. Das Bosstor baut seit Issue #568 boss_portal.py.

Erzeuger fuer das Spiel. Fluegel und Beschlag: Uebergabe aus den Probestuecken tueren und tueren2 (Branch
claude/design-probe-ghul-45a8c4, Commit 412096d). Rahmen nach der Formenlehre (#281): Uebergabe aus dem Probestueck
tuerrahmen (derselbe Branch, Commit 39886aa, design/proben/grafik/tuerrahmen/rahmen.py). Erzeugt nur Modelle und Texturen nach
assets/models/, keine Probebilder, dazu die Liste der gelesenen Wand-Texturen (tools/models/door_frame_sources.sha256).

Aufruf (aus dem Repository-Wurzelverzeichnis), danach python tools/models/checksums.py:
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/models/door.py

Alles per Skript, nichts heruntergeladen. Spiel-Achsen (x seitlich, y oben, z vorn), Meter. Ursprung: Mitte des Durchgangs
auf dem Boden, Tuerflaeche in der x/y-Ebene, Dicke in z. Durchgang = Zelle des Spiels: 2 m breit, 2 m tief, 3,5 m hoch.

Je Tuerart zwei Sorten Dateien:
  door_<art>.glb               nur bewegliche Teile (leaf_a, leaf_b bzw. leaf, lock), kein Stein; jeder Fluegel traegt seinen
                               Teil vom Kopffeld (Issue #429; bis dahin stand das in einem festen Knoten "portal").
                               Eigener Atlas 128 x 128, 32 Farben.
  door_frame_<art>_<v>.glb     der Steinrahmen fuer die Wandvariante v (a, b, c): Knoten "portal_front" (Stein der +z-Seite)
                               und "portal_back" (Stein der -z-Seite), beide fest. Atlas 128 x 64: links die echte
                               Wand-Textur der Variante (assets/textures/catacomb_wall*.png, bitgleich, nur gelesen), rechts
                               dieselbe Kachel als Werkstein (heller, siehe werk_tile).
Getrennt, weil die Wand-Textur allein 24 Farben hat: in einem Atlas mit Holz, Eisen und Glas blieben nur 8 Farben fuer die
Fluegel. Und weil die beiden Seiten einer Tuer in verschiedene Raeume schauen, zeigt jede Seite ihre eigene Wandvariante
(wie die rissige Wand, cracked_wall.py): das Spiel zeichnet portal_front aus der Datei der Variante der +z-Seite und
portal_back aus der der -z-Seite (DoorFrameLook im Core).

Steinflaechen bekommen ihre Textur-Koordinaten aus der Weltposition wie SurfaceTexturing (eine 64-px-Kachel je Meter, u am
Wandverlauf, v mit der Hoehe) und sind an den Metergrenzen geteilt: die Fugen laufen im Raster der Wand weiter. Aendert sich
eine Wand-Textur oder die Palette, passt der Rahmen nicht mehr; ein Test vergleicht door_frame_sources.sha256 mit
assets/textures/textures.sha256 und schlaegt fehl, bis dieses Skript neu gelaufen ist.

Die Fluegel sind unterhalb des Bogenansatzes rechteckig; darueber traegt jeder seine Haelfte des Kopffelds im Spitzbogen
(die Eisentuer das ganze). Beim Oeffnen schwingt die Spitze dieses Kopffelds in die Bogenleibung hinein: der Stein verdeckt
sie, sichtbar bleibt der Fluegel bis zur Leibung. Die Risswand hat nur den Knoten "cracks". Wie das Spiel die Teile bewegt:
docs/modelle.md (Tueren).
"""
import hashlib
import json
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from psx import (V, G, Part, rects_from_pixels, vnoise, blob, mix, paint_atlas, save_image,  # noqa: E402
                 export_material, export_glb, report_parts)

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), "tools", "palette"))
import snap as palette_snap  # noqa: E402

TOOLS = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(TOOLS))
OUT_DIR = os.path.join(ROOT, "assets", "models")
TEXTURES = os.path.join(ROOT, "assets", "textures")
PALETTE = os.path.join(ROOT, "assets", "palette", "hauptpalette.json")
SOURCES = os.path.join(TOOLS, "door_frame_sources.sha256")
TEX = 128
H = 3.5                                # Zellhoehe

RECTS = {
    "planks": (0, 0, 40, 128),         # Bretter (1 m x 3,5 m)
    "plate": (40, 0, 72, 32),          # genietete Eisenplatte
    "iron": (72, 0, 104, 32),          # Baender, Beschlag
    "edge": (104, 0, 128, 32),         # Kanten
    "lock": (40, 32, 88, 64),          # Schlossplatte
    "dark": (88, 32, 104, 64),         # dunkel
    "glass": (104, 32, 128, 64),       # leuchtendes Glas
}
UV = rects_from_pixels(RECTS, TEX)

# Rahmen-Atlas: links die Wandkachel, rechts der Werkstein (je 64 x 64 = 1 m)
FRAME_SIZE = (128, 64)
FRAME_RECTS = {"stone": (0, 0, 64, 64), "werk": (64, 0, 128, 64)}
FRAME_UV = rects_from_pixels(FRAME_RECTS, FRAME_SIZE)
WALL_FILES = {"a": "catacomb_wall.png", "b": "catacomb_wall_b.png", "c": "catacomb_wall_c.png"}

PAL = {
    "normal": {"wood": (96, 80, 66), "iron": (112, 116, 130)},
    "locked": {"wood": (54, 56, 66), "iron": (104, 108, 122)},
}
BRASS = np.array((158, 126, 66), float)
DARK = np.array((16, 12, 16), float)
GLOW = np.array((226, 92, 52), float)


# --- Texturen -----------------------------------------------------------------------------------------------------

def painters(kind):
    P = PAL[kind]
    wood, iron = (np.array(P[k], float) for k in ("wood", "iron"))
    seed = list(PAL).index(kind) * 11

    def planks(U, V_):
        h, w = U.shape
        rng = np.random.default_rng(100 + seed)
        n = 3
        idx = np.minimum(np.floor(U * n), n - 1).astype(int)
        f = (U * n) % 1.0
        col = np.broadcast_to(wood, (h, w, 3)).copy()
        col *= (0.82 + 0.32 * rng.random(n))[idx][..., None]
        col *= (0.86 + 0.28 * vnoise(h, w, 3, 16, 200 + seed))[..., None]
        col *= (0.93 + 0.14 * vnoise(h, w, 20, 3, 300 + seed))[..., None]
        for k in range(4):
            cu, cv = 0.1 + 0.8 * rng.random(), 0.1 + 0.8 * rng.random()
            col = mix(col, wood * 0.4, (blob(U, V_, cu, cv, 0.05, 0.02) > 0.3).astype(float) * 0.9)
        col = mix(col, DARK * 1.4, ((f < 0.07) | (f > 0.95)).astype(float) * 0.9)
        col = mix(col, wood * 1.3, ((f > 0.07) & (f < 0.13)).astype(float) * 0.5)
        for k, vv in enumerate((0.31, 0.64, 0.82)):
            sel = (idx == k) & (np.abs(V_ - vv) < 0.006)
            col = mix(col, DARK * 1.4, sel.astype(float))
        return col

    def plate(U, V_):
        h, w = U.shape
        col = np.broadcast_to(iron * 0.8, (h, w, 3)).copy()
        col *= (0.84 + 0.3 * vnoise(h, w, 6, 6, 400 + seed))[..., None]
        edge = (np.minimum(np.minimum(U, 1 - U), np.minimum(V_, 1 - V_)) < 0.07)
        col = mix(col, iron * 0.35, edge.astype(float))
        col = mix(col, iron * 1.4 + 14, ((np.minimum(U, V_) > 0.07) & (np.minimum(U, V_) < 0.13)).astype(float) * 0.55)
        for cu in (0.15, 0.85):
            for cv in (0.15, 0.5, 0.85):
                d = blob(U, V_, cu, cv, 0.08, 0.08)
                col = mix(col, iron * 1.7 + 20, (d > 0.4).astype(float))
                col = mix(col, iron * 0.35, ((d > 0.05) & (d <= 0.4)).astype(float) * 0.7)
        col = mix(col, np.array((92, 62, 46), float), np.clip((vnoise(h, w, 5, 5, 500 + seed) - 0.64) * 3, 0, 0.45))
        return col

    def iron_band(U, V_):
        h, w = U.shape
        col = np.broadcast_to(iron, (h, w, 3)).copy()
        col *= (0.8 + 0.4 * np.abs(np.sin(V_ * 3.1)))[..., None]
        col *= (0.9 + 0.2 * vnoise(h, w, 6, 6, 600 + seed))[..., None]
        col = mix(col, iron * 1.35 + 14, ((V_ > 0.78) & (V_ < 0.92)).astype(float) * 0.7)          # helle Kante: Glanz oben
        for k in range(4):
            d = blob(U, V_, (k + 0.5) / 4, 0.5, 0.09, 0.2)
            col = mix(col, iron * 1.6 + 20, (d > 0.35).astype(float))
        return col

    def edge(U, V_):
        h, w = U.shape
        base = wood * 0.7 if kind == "normal" else iron * 0.6
        col = np.broadcast_to(base, (h, w, 3)).copy()
        return col * (0.8 + 0.4 * vnoise(h, w, 4, 4, 700 + seed))[..., None]

    def glass(U, V_):
        h, w = U.shape
        col = np.broadcast_to(GLOW, (h, w, 3)).copy() * (0.85 + 0.25 * vnoise(h, w, 4, 3, 900 + seed))[..., None]
        col = mix(col, np.array((250, 200, 120), float), np.clip(blob(U, V_, 0.5, 0.5, 0.6, 0.7), 0, 1) * 0.55)   # Kern heller
        return mix(col, DARK, (((U * 3) % 1.0 < 0.07) | (np.abs(V_ - 0.5) < 0.04)).astype(float) * 0.85)       # Bleiruten

    def lock(U, V_):
        h, w = U.shape
        col = np.broadcast_to(BRASS, (h, w, 3)).copy() * (0.82 + 0.32 * vnoise(h, w, 5, 8, 1000))[..., None]
        col = mix(col, BRASS * 0.45, (np.minimum(np.minimum(U, 1 - U), np.minimum(V_, 1 - V_)) < 0.07).astype(float))
        col = mix(col, DARK, (blob(U, V_, 0.5, 0.62, 0.07, 0.12) > 0.3).astype(float))
        col = mix(col, DARK, ((np.abs(U - 0.5) < 0.025) & (V_ > 0.15) & (V_ < 0.6)).astype(float))
        for cu in (0.12, 0.88):
            col = mix(col, BRASS * 1.4, (blob(U, V_, cu, 0.5, 0.05, 0.12) > 0.4).astype(float))
        return col

    def dark(U, V_):
        return np.broadcast_to(DARK, U.shape + (3,)).copy()

    return {"planks": planks, "plate": plate, "iron": iron_band, "edge": edge, "lock": lock, "dark": dark, "glass": glass}


def stein_row():
    """Stein (15 Stufen) aus assets/palette/hauptpalette.json. Den Werkstein liest nur noch boss_portal.py (Issue #570)."""
    with open(PALETTE, encoding="utf-8") as f:
        data = json.load(f)
    rows = {r["name"]: np.array(r["farben"], int) for r in data["grund"]}
    return rows["Stein"]


def load_tile(name):
    """Wand-Kachel 64 x 64, Zeile 0 oben (wie das Bild aussieht), ganze Zahlen 0..255."""
    im = bpy.data.images.load(os.path.join(TEXTURES, name))
    w, h = im.size
    a = np.array(im.pixels[:], dtype=float).reshape(h, w, 4)[..., :3] * 255.0
    return np.round(a[::-1]).astype(int)


def werk_tile(tile, stein):
    """Dieselbe Kachel als Werkstein: gleiche Fugen und Verhaeltnisse, nur heller. Jede Farbe auf die naechste Stein-Stufe,
    dann so weit hinauf, dass die hellste Farbe der Kachel auf die hellste Stufe faellt (mindestens 4 Stufen); Normal und
    Verschlossen bleiben in der Stein-Reihe (oberste Stufen). Das Bosstor (Reihe Werkstein) baut boss_portal.py."""
    flat = tile.reshape(-1, 3)
    colours = sorted({tuple(int(c) for c in p) for p in flat})
    idx = {c: int(((stein - np.array(c)) ** 2).sum(1).argmin()) for c in colours}
    top = len(stein) - 1
    shift = max(4, top - max(idx.values()))
    out = {c: stein[min(i + shift, top)] for c, i in idx.items()}
    return np.array([out[tuple(int(c) for c in p)] for p in flat]).reshape(tile.shape)


def frame_atlas(variant, stein):
    tile = load_tile(WALL_FILES[variant])
    werk = werk_tile(tile, stein)
    w, h = FRAME_SIZE
    atlas = np.zeros((h, w, 3))
    for name, t in (("stone", tile), ("werk", werk)):
        x0, y0, x1, y1 = FRAME_RECTS[name]
        atlas[y0:y1, x0:x1] = t[::-1]                                       # Feld: Zeile 0 unten
    return atlas


# --- Geometrie-Bausteine ------------------------------------------------------------------------------------------

def arc(hw, spring, apex, n=6):
    """Rechte Haelfte eines Spitzbogens von (hw, spring) bis zum Scheitel (0, apex): zwei Kreise mit den Mittelpunkten auf der
    Ansatzlinie, Radius R = (Stich^2 + hw^2) / (2 hw) (gleichseitig bei Stich = 1,73 hw)."""
    rise = apex - spring
    R = (rise * rise + hw * hw) / (2 * hw)
    cx = hw - R
    a_end = math.atan2(rise, R - hw)
    return [(cx + R * math.cos(a_end * k / n), spring + R * math.sin(a_end * k / n)) for k in range(n + 1)]


def extrude(part, pts, z0, z1, rect):
    part.prism([(x, y, z0) for x, y in pts], (0, 0, z1 - z0), rect)


def head_panel(part, hw, spring, apex, z0, z1, rect, sides=(1, -1), n=6):
    """Kopffeld im Bogen (konvex), in zwei Haelften (rechts +1, links -1), damit die Textur nicht ueber 2 m gestreckt wird.
    `sides`: welche Haelften an dieses Teil kommen (jede Haelfte schwingt mit dem Fluegel ihrer Seite, Issue #429)."""
    right = arc(hw, spring, apex, n)
    for s in sides:
        extrude(part, [(0, spring)] + [(s * x, y) for x, y in right], z0, z1, rect)


def half_poly(pts, side):
    """Konvexes Vieleck (x, y), beschnitten an x = 0; bleibt die Seite side * x >= 0."""
    out = []
    for i, b in enumerate(pts):
        a = pts[i - 1]
        a_in, b_in = side * a[0] >= 0, side * b[0] >= 0
        if a_in != b_in:
            t = a[0] / (a[0] - b[0])
            out.append((0.0, a[1] + t * (b[1] - a[1])))
        if b_in:
            out.append(b)
    return out


def strap(part, x_hinge, side, y, length, half, z0, z1, rect="iron"):
    """Beschlagband vom Scharnier aus, spitz zulaufend; hoechstens bis 2 cm vor die Mitte (sonst stossen die Baender zweier
    Fluegel oder die beiden Baender im Kopffeld aneinander)."""
    length = min(length, abs(x_hinge) - 0.02)
    xe = x_hinge - side * length
    pts = [(x_hinge, y - half), (xe + side * 0.12, y - half * 0.55), (xe, y), (xe + side * 0.12, y + half * 0.55), (x_hinge, y + half)]
    part.prism([(x, y_, z0) for x, y_ in pts], (0, 0, z1 - z0), rect)


def ring_pull(part, cx, cy, z, radius, width, depth, rect="iron", segs=8):
    r0, r1 = radius - width / 2, radius + width / 2
    for k in range(segs):
        a0, a1 = 2 * math.pi * k / segs, 2 * math.pi * (k + 1) / segs
        pts = [(cx + r0 * math.cos(a0), cy + r0 * math.sin(a0), z), (cx + r1 * math.cos(a0), cy + r1 * math.sin(a0), z),
               (cx + r1 * math.cos(a1), cy + r1 * math.sin(a1), z), (cx + r0 * math.cos(a1), cy + r0 * math.sin(a1), z)]
        part.prism(pts, (0, 0, depth), rect)


def faces(t, proud):
    """z-Bereiche der beiden Seiten eines Bauteils der Dicke t, `proud` ueber der Flaeche."""
    return [tuple(sorted((f * (t / 2 - 0.005), f * (t / 2 + proud)))) for f in (-1, 1)]


def leaf_rect(name, side, x_j, thick, top, kind, inner=0.005, fittings_top=None):
    """Rechteckiger Fluegel bis `top` (dem Bogenansatz: die Bretter laufen ohne Fuge ins Kopffeld, Issue #543); Scharnierkante
    bei side * (x_j - thick/2 - 0.035); der Fluegel schwingt dort um y. Der Steinpfosten (im Rahmen) fuellt den Rest bis x_j.
    Beschlag wie in Runde 1, hellere Baender; das oberste Band sitzt 0,25 unter `fittings_top` (ohne Angabe: `top`)."""
    proud = 0.03
    px = side * (x_j - thick / 2 - proud - 0.005)
    part = Part(name, (px, 0, 0), UV)
    t = thick / 2
    lo, hi = sorted((px, side * inner))
    part.box((lo, 0, -t), (hi, top, t), lambda n: "planks" if abs(n.z) > 0.9 else "edge")
    for z0, z1 in faces(thick, 0.025):
        for y in (0.45, 1.0, (top if fittings_top is None else fittings_top) - 0.25):
            strap(part, px, side, y, 0.8, 0.075, z0, z1)
        if kind == "normal":
            rx = px - side * 0.7
            ring_pull(part, rx, 1.15, z0, 0.1, 0.03, z1 - z0)
            part.box((rx - 0.045, 1.25, z0), (rx + 0.045, 1.32, z1), "iron")
    return part


# --- Steinflaechen mit Welt-UV (Rahmen) ---------------------------------------------------------------------------

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
    u0, v0, u1, v1 = part.rects[field]
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


def plate(part, x_in, x_out, y0, y1, sx, sz, field, top=True, bottom=False, back=0.9375):
    """Kaempfer- oder Sockelplatte im Pfostenband, buendig mit der Wandebene (z von `back` bis 1). Sie fuellt nur die Stufe
    zwischen Wandebene und Band; nichts steht vor der Wand (Issue #715: bis dahin ragte sie 1/8 m vor und hing mit ihrer
    Innenkante frei in der Oeffnung). Offen bleiben Vorderseite, Innenkante, Ober- bzw. Unterseite; Rueck- und Aussenseite
    liegen an Band und Wand."""
    pts = [(x_out, 1.0), (x_in, 1.0), (x_in, back), (x_out, back)]
    pts = [(sx * x, sz * z) for x, z in pts]
    plan_prism(part, pts, y0, y1, {2, 3}, field, top, bottom)


def box_faces(part, lo, hi, field, sides):
    """Quader (Datei-Koordinaten) mit den genannten Seiten: '+z', '-z', '+x', '-x', '+y', '-y'."""
    x0, y0, z0 = lo
    x1, y1, z1 = hi
    d = {"+z": ([(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)], (0, 0, 1)),
         "-z": ([(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0)], (0, 0, -1)),
         "+x": ([(x1, y0, z0), (x1, y0, z1), (x1, y1, z1), (x1, y1, z0)], (1, 0, 0)),
         "-x": ([(x0, y0, z0), (x0, y0, z1), (x0, y1, z1), (x0, y1, z0)], (-1, 0, 0)),
         "+y": ([(x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1)], (0, 1, 0)),
         "-y": ([(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)], (0, -1, 0))}
    for f in sides:
        emit(part, d[f][0], d[f][1], field)


SOCKEL = 0.5


def stufenportal(parts, hw0, R0, spring, zs, z_door, plate_x_in, vertical_reveals, top=H, width=0.125):
    """Alles aus Stein: Bogenfeld (buendig mit der Wand), Bogenlaeufe aus Werkstein (je Lauf eine Stufe nach hinten, alle aus
    denselben Kreismittelpunkten, Ringbreite `width`), Leibungen, Kaempfer- und Sockelplatte aus Werkstein."""
    xc = hw0 - R0
    n = len(zs)
    for sz in (1, -1):
        part = parts[0] if sz > 0 else parts[1]                              # Vorderseite (+z) und Rueckseite (-z) je ein Knoten
        spandrel_caps(part, xc, R0, spring, top, sz, 1.0, "stone")
        depths = [1.0] + list(zs) + [z_door]
        for k in range(n):
            ro, ri = R0 - width * k, R0 - width * (k + 1)
            arch_ring(part, xc, ro, ri, spring, zs[k], sz, "werk")
            for sx in (1, -1):                                              # Pfostenband des Laufs bis zum Sockel
                quad(part, (sx * (hw0 - width * k), SOCKEL, sz * zs[k]), (sx * (hw0 - width * (k + 1)), SOCKEL, sz * zs[k]),
                     (sx * (hw0 - width * (k + 1)), spring, sz * zs[k]), (sx * (hw0 - width * k), spring, sz * zs[k]), (0, 0, sz), "werk")
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


# --- Tuerarten ----------------------------------------------------------------------------------------------------

LEAF_TOP_N = 1.94               # Normal: Bezug fuer das oberste Beschlagband (bis #543 auch die Oberkante des Fluegels)
SPRING_N = 2.0                  # Normal und Verschlossen: Bogenansatz, Lauf R = 1,5625 (Scheitel 3,458), Oeffnung 1,75 m
POST_N = 0.105                  # Pfosten an der Scharnierseite (Normal): Dicke 0,14 / 2 + Beschlag 0,03 + Luft
POST_L = 0.135                  # Locked: Dicke 0,14 / 2 + Platten und Baender 0,06 + Luft


def frame_nodes():
    return Part("portal_front", (0, 0, 0), FRAME_UV), Part("portal_back", (0, 0, 0), FRAME_UV)


def frame_normal(kind):
    """Ein Lauf 0,125 breit, 1/16 hinter der Wandebene; Steinpfosten an der Scharnierseite (hinter dem Pfostenband)."""
    front, back = frame_nodes()
    stufenportal((front, back), 1.0, 1.5625, SPRING_N, [0.9375], 0.07, 0.875, False)
    post = POST_N if kind == "normal" else POST_L
    zp = 0.1 if kind == "normal" else 0.15
    for sx in (1, -1):
        lo, hi = sorted((sx * (1.0 - post), sx * 1.0))
        inner = "+x" if sx < 0 else "-x"
        box_faces(front, (lo, 0, 0), (hi, SPRING_N, zp), "stone", ["+z", inner])
        box_faces(back, (lo, 0, -zp), (hi, SPRING_N, 0), "stone", ["-z", inner])
    return [front, back]


def head_normal(part, kind, sides):
    """Kopffeld (Holz oder Eisenplatten) im Bogen des Laufs: die Haelften `sides` an den Fluegel `part` (Issue #429: bis #303
    stand das Kopffeld fest im Knoten "portal" und blieb beim Oeffnen stehen). Der Fluegel reicht bis an den Bogenansatz;
    Querband und Fuellstueck auf der Fuge sind weg (Issue #543: ein Rest aus der Zeit vor #429, das Urteil des Menschen)."""
    hw = 0.875
    apex = SPRING_N + math.sqrt(1.4375 ** 2 - 0.5625 ** 2)
    head_panel(part, hw, SPRING_N, apex, -0.07, 0.07, "planks" if kind == "normal" else "plate", sides)
    if kind == "normal":                                                                # ein Band im Kopf, spitz zulaufend
        for s in sides:
            for z0, z1 in faces(0.14, 0.025):
                strap(part, s * (hw - 0.01), s, 2.5, 0.9, 0.065, z0, z1)



def locked_leaf():
    x_j, thick = 1.0, 0.14
    px = x_j - POST_L
    part = Part("leaf", (px, 0, 0), UV)
    t = thick / 2
    part.box((-0.995, 0, -t), (px, SPRING_N, t), "edge")                 # bis an den Bogenansatz (Issue #543)
    cols, rows, gap = 2, 3, 0.03
    width = px + 0.995
    cw, rh = (width - gap * (cols + 1)) / cols, (SPRING_N - gap * (rows + 1)) / rows
    for z0, z1 in faces(thick, 0.02):
        for i in range(cols):
            for j in range(rows):
                x0 = -0.995 + gap + i * (cw + gap)
                y0 = gap + j * (rh + gap)
                part.box((x0, y0, z0), (x0 + cw, y0 + rh, z1), "plate")
        za, zb = (z1, z1 + 0.04) if z1 > 0 else (z0 - 0.04, z0)
        for y in (0.4, 1.0, 1.55):
            part.box((-0.995, y - 0.09, za), (px, y + 0.09, zb), "iron")
        for x in (-0.65, 0.5):
            part.box((x - 0.07, 0.0, za), (x + 0.07, SPRING_N, zb), "iron")
    return part


def lock_node():
    cx, cy = -0.4, 1.0
    part = Part("lock", (cx, cy, 0), UV, parent="leaf")
    zt = 0.07 + 0.06
    part.box((cx - 0.2, cy - 0.17, -zt - 0.04), (cx + 0.2, cy + 0.17, zt + 0.04), lambda n: "lock" if abs(n.z) > 0.9 else "iron")
    for face in (-1, 1):
        z0, z1 = sorted((face * (zt + 0.04), face * (zt + 0.09)))
        part.box((cx - 0.12, cy + 0.17, z0), (cx - 0.07, cy + 0.3, z1), "iron")
        part.box((cx + 0.07, cy + 0.17, z0), (cx + 0.12, cy + 0.3, z1), "iron")
        part.box((cx - 0.12, cy + 0.3, z0), (cx + 0.12, cy + 0.35, z1), "iron")
    return part


def parts_for(kind):
    """Nichts steht fest (Issue #429): jeder Fluegel traegt seinen Teil des Kopffelds."""
    if kind == "normal":
        a = leaf_rect("leaf_a", +1, 1.0, 0.14, SPRING_N, "normal", fittings_top=LEAF_TOP_N)
        b = leaf_rect("leaf_b", -1, 1.0, 0.14, SPRING_N, "normal", fittings_top=LEAF_TOP_N)
        head_normal(a, "normal", (1,))
        head_normal(b, "normal", (-1,))
        return [a, b]
    if kind == "locked":
        leaf = locked_leaf()
        head_normal(leaf, "locked", (1, -1))
        return [leaf, lock_node()]
    raise ValueError(f"door.py baut keine Tuer der Art {kind}")


# --- Risswand (aus Runde 1 unveraendert: eigener Atlas, alles dunkel) -------------------------------------------------

CRACKED_RECTS = {
    "planks": (0, 0, 44, 128),
    "plate": (44, 0, 108, 64),
    "iron": (44, 64, 76, 96),
    "edge": (76, 64, 108, 96),
    "bone": (108, 0, 128, 32),
    "glow": (108, 32, 128, 64),
    "lock": (44, 96, 108, 128),
    "dark": (108, 64, 128, 128),
}
CRACKED_UV = rects_from_pixels(CRACKED_RECTS, TEX)


def cracked_painters():
    return {name: (lambda U, V_: np.broadcast_to(np.array((28, 24, 30), float), U.shape + (3,)).copy() *
                   (0.8 + 0.4 * vnoise(U.shape[0], U.shape[1], 4, 4, 9))[..., None]) for name in CRACKED_RECTS}


def cracked_node():
    """Risse im Mauerwerk: duenne dunkle Streifen knapp vor beiden Flaechen der Wandzelle (Zelltiefe 2 m, also z = +-1)."""
    part = Part("cracks", (0, 0, 0), CRACKED_UV)
    lines = [  # (x0, y0, x1, y1, breite)
        (0.00, 1.15, 0.30, 1.45, 0.024), (0.30, 1.45, 0.34, 1.85, 0.018), (0.30, 1.45, 0.62, 1.55, 0.016),
        (0.00, 1.15, -0.25, 1.05, 0.022), (-0.25, 1.05, -0.32, 0.65, 0.02), (-0.25, 1.05, -0.55, 1.2, 0.014),
        (0.00, 1.15, 0.06, 0.70, 0.024), (0.06, 0.70, -0.05, 0.30, 0.018), (0.06, 0.70, 0.30, 0.55, 0.014),
        (0.34, 1.85, 0.2, 2.25, 0.012), (-0.32, 0.65, -0.5, 0.3, 0.012),
    ]
    for face in (-1, 1):
        z = face * (1.0 + 0.012)
        for x0, y0, x1, y1, wd in lines:
            dx, dy = x1 - x0, y1 - y0
            ln = math.hypot(dx, dy)
            nx, ny = -dy / ln * wd / 2, dx / ln * wd / 2
            pts = [(x0 - nx, y0 - ny, z), (x1 - nx, y1 - ny, z), (x1 + nx, y1 + ny, z), (x0 + nx, y0 + ny, z)]
            part.prism(pts, (0, 0, face * 0.004), "dark")
    return part


# --- Ausgabe ------------------------------------------------------------------------------------------------------

def colours_of(tex):
    return len({tuple(p) for p in np.round(tex).reshape(-1, 3).astype(int)})


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
    print(f"TEXTUR {name} {w}x{h} Farben {colours_of(tex)}")


def paint_on_stein(pixel_rects, painters_, size, paint=4):
    """Wie psx.paint_atlas, aber ohne k-Means: das in paint-facher Groesse gemalte und verkleinerte Bild wird Pixel fuer
    Pixel auf die naechste Stufe der Reihe Stein der Hauptpalette gesetzt (Issue #280). Feld [y][x][rgb], Zeile 0 unten."""
    w, h = size if isinstance(size, tuple) else (size, size)
    atlas = np.zeros((h * paint, w * paint, 3))
    for name, (x0, y0, x1, y1) in pixel_rects.items():
        ph, pw = (y1 - y0) * paint, (x1 - x0) * paint
        U, V_ = np.meshgrid((np.arange(pw) + 0.5) / pw, (np.arange(ph) + 0.5) / ph)
        atlas[y0 * paint:y1 * paint, x0 * paint:x1 * paint] = painters_[name](U, V_)
    small = atlas.reshape(h, paint, w, paint, 3).mean(axis=(1, 3))
    return palette_snap.snap(np.clip(small, 0, 255), palette_snap.colours("Stein"))


def build(kind, rects, painters_, parts):
    # Fluegel (Holz, Eisen, Messing, Glas): noch die alte Farbtabelle aus k-Means, nicht in der Hauptpalette; die Palette hat
    # kein gedaempftes Holz und kein graues Eisen wie sie (Issue #280, offen). Nur die Risswand liegt auf der Reihe Stein.
    tex = paint_on_stein(rects, painters_, TEX) if kind == "cracked" else paint_atlas(rects, painters_, TEX, colors=32, seed=7)
    write(f"door_{kind}", tex, parts)


def build_frame(kind, variant, stein):
    tex = frame_atlas(variant, stein)
    assert colours_of(tex) <= 32, f"door_frame_{kind}_{variant}: {colours_of(tex)} Farben (hoechstens 32)"
    write(f"door_frame_{kind}_{variant}", tex, frame_normal(kind))


def write_sources():
    """SHA-256 und Groesse jeder gelesenen Wand-Textur (wie cracked_wall_sources.sha256)."""
    lines = ["# sha256 size name - the wall textures tools/models/door.py built the door frames from"]
    for name in sorted(WALL_FILES.values()):
        with open(os.path.join(TEXTURES, name), "rb") as f:
            data = f.read()
        lines.append(f"{hashlib.sha256(data).hexdigest()} {len(data)} {name}")
    with open(SOURCES, "w", encoding="ascii", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print("Quellen:", os.path.relpath(SOURCES, ROOT))


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    stein = stein_row()
    for kind in PAL:
        build(kind, RECTS, painters(kind), parts_for(kind))
        for variant in WALL_FILES:
            build_frame(kind, variant, stein)
    build("cracked", CRACKED_RECTS, cracked_painters(), [cracked_node()])
    write_sources()


main()
