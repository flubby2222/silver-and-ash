"""Probestueck frei stehende Saeule (Issue #539, Baustein aus der Formenlehre Gotik #281, Frage #247).

Alles entsteht aus diesem einen Skript: die Modelle (.glb je Seitenzahl 12/8 und Wandvariante a, b, c), die Atlas-Bilder,
die Probebilder. Gebaut auf tools/models/psx.py; Sockel und Kapitell sind die Profile des Wandpfeilers (Probestueck #536),
ins Runde gedreht. Nur Stein-Reihe der Hauptpalette, Koernung aus der Wandkachel (Stein-Stufen).
Nichts wird ins Spiel uebernommen.

Spiel-Achsen: x seitlich, y oben, z vorn; Meter. Ursprung: Mitte der Saeule auf dem Boden.

Mass (Raum 6 m): Sockel 0 bis 0,5, Schaft 0,5 bis 5,5 (Halbmesser 0,4375, Eckenkreis), Kapitell 5,5 bis 6,0. Sockel und
Kapitell springen 1/16 vor: Eckenhalbmesser 0,5 (Abweichung von der Formenlehre, siehe LIESMICH: dort 1,0 ueber den Flaechen).
Die Ecken der Vielecke liegen auf dem Kreis; Flaechenmitten liegen auf R * cos(180 / n).

Aufruf (aus dem Repository-Wurzelverzeichnis):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/saeule-539/saeule.py
Zwei Laeufe liefern gleiche Dateien (kein Zufall ausser festen Samen).
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
from psx import (V, G, Part, rects_from_pixels, save_image, stone_textures, pass_material,  # noqa: E402
                 write_light, setup_render, camera, aim, render_passes, compose, save_png, export_material, export_glb,
                 MATS)

OUT = os.environ.get("S539_OUT", HERE)
TMP = os.path.join(tempfile.gettempdir(), "saeule539_tmp")
os.makedirs(TMP, exist_ok=True)
TEXTURES = os.path.join(ROOT, "assets", "textures")
MODELS = os.path.join(ROOT, "assets", "models")
WALL_FILES = {"a": "catacomb_wall.png", "b": "catacomb_wall_b.png", "c": "catacomb_wall_c.png"}
BG = np.array((30, 26, 34), float)
S = 1 / 16
R_SHAFT = 0.4375
HEIGHT, CB = 6.0, 5.5                      # Raumhoehe, Kapitell-Unterkante
DRUM = 0.5                                 # Trommelhoehe
ONLY = os.environ.get("S539_ONLY", "")     # nur fuer Versuche: "modelle" = nur Modelle

# Atlas 216 x 384 (Pixel, Zeile 0 = unten). Der Schaftstreifen ist 192 breit (drei Wandkacheln) und rundum periodisch.
ATLAS = (216, 384)
STRIP = 192                                # 3,0 m bei 64 px/m; Umfang 12-Eck 2,718 m (10 % gedehnt)
RECT_PX = {"schaft": (0, 0, STRIP, 320), "sockel": (0, 320, STRIP, 352), "kapitell": (0, 352, STRIP, 384),
           "oben": (192, 8, 204, 16), "unten": (204, 8, 216, 16)}


def _uv_rects():
    w, h = ATLAS
    out = {}
    for k, (x0, y0, x1, y1) in RECT_PX.items():
        if k in ("schaft", "sockel", "kapitell"):      # u rundherum genau 0..1 des Streifens (Naht ohne Versatz)
            out[k] = (x0 / w, (y0 + 0.5) / h, x1 / w, (y1 - 0.5) / h)
        else:
            out[k] = ((x0 + 0.5) / w, (y0 + 0.5) / h, (x1 - 0.5) / w, (y1 - 0.5) / h)
    return out


COL_UV = _uv_rects()

# Frame-Atlas fuer Wand und Wandpfeiler der Szenen (wie im Probestueck #536, nur gelesen/nachgebaut)
FRAME_SIZE = (128, 64)
FRAME_RECTS = {"stone": (0, 0, 64, 64), "pillar": (64, 0, 128, 64)}
FRAME_UV = rects_from_pixels(FRAME_RECTS, FRAME_SIZE)
LIFT = 5
BACK = -0.125


# --- Palette ---------------------------------------------------------------------------------------------------------

def stein_row():
    with open(os.path.join(ROOT, "assets", "palette", "hauptpalette.json"), encoding="utf-8") as f:
        data = json.load(f)
    rows = {r["name"]: np.array(r["farben"], int) for r in data["grund"] + data["akzent"]}
    return rows["Stein"]


STEIN = stein_row()


def load_tile(name):
    im = bpy.data.images.load(os.path.join(TEXTURES, name))
    w, h = im.size
    a = np.array(im.pixels[:], dtype=float).reshape(h, w, 4)[..., :3] * 255.0
    bpy.data.images.remove(im)
    return np.round(a[::-1]).astype(int)


def nearest_idx(tile):
    flat = tile.reshape(-1, 3)
    cols = sorted({tuple(int(c) for c in p) for p in flat})
    idx = {c: int(((STEIN - np.array(c)) ** 2).sum(1).argmin()) for c in cols}
    return np.array([idx[tuple(int(c) for c in p)] for p in flat]).reshape(tile.shape[:2])


def colours_of(tex):
    return len({tuple(p) for p in np.round(tex).reshape(-1, 3).astype(int)})


def only_stein(tex):
    """Jedes Pixel muss genau eine Farbe der Stein-Reihe sein (Pruefung wie tools/palette/snap.py)."""
    have = {tuple(p) for p in np.round(tex).reshape(-1, 3).astype(int)}
    return have <= {tuple(int(c) for c in r) for r in STEIN}


# --- Textur der Saeule -----------------------------------------------------------------------------------------------

SEEDS = {"a": 11, "b": 23, "c": 37}


def _wrap_box(a, r):
    """Gleitender Mittelwert (Kantenlaenge 2r+1), rundum periodisch."""
    out = np.zeros_like(a, float)
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            out += np.roll(np.roll(a, dy, 0), dx, 1)
    return out / (2 * r + 1) ** 2


def grain_of(variant):
    """Steinkoernung der Wand: Wandkachel (64 x 64) in Stein-Stufen, Moertel (deutlich unter dem Mittel, um 1 Pixel
    erweitert) durch Nachbarn ersetzt, grosse Quaderunterschiede per Hochpass entfernt, Rest als ganze Stufen-Abweichung
    (Betrag hoechstens 2). Periodisch in u und v. Es bleibt nur die feine Koernung, kein Quadermuster."""
    idx = nearest_idx(load_tile(WALL_FILES[variant])).astype(float)
    mortar = idx <= np.median(idx) - 3
    mortar = mortar | np.roll(mortar, 1, 0) | np.roll(mortar, -1, 0) | np.roll(mortar, 1, 1) | np.roll(mortar, -1, 1)
    work = np.where(mortar, np.nan, idx)
    for _ in range(12):
        if not np.isnan(work).any():
            break
        pad = np.pad(work, 1, mode="wrap")
        nb = np.stack([pad[1 + dy:65 + dy, 1 + dx:65 + dx] for dy in (-1, 0, 1) for dx in (-1, 0, 1)])
        with np.errstate(all="ignore"):
            fill = np.nanmean(nb, axis=0)
        work = np.where(np.isnan(work), fill, work)
    work = np.where(np.isnan(work), np.nanmean(work), work)
    dev = work - _wrap_box(work, 4)
    return np.clip(np.round(dev * 1.5), -2, 2).astype(int)


def column_atlas(variant):
    """Atlas als Feld (384, ATLAS[0], 3), Zeile 0 unten. Alle Farben aus der Stein-Reihe.
    Schaft: zehn Trommeln zu 0,5 m (32 px) mit waagerechter Fuge (2 px dunkel, 1 px Schatten), keine senkrechten Fugen.
    Steinflaeche mit der Koernung der Wandkachel (Stufen base +-2, nie unter 9: Fuge 2 und Schatten 6 bleiben getrennt),
    je Trommel um einen festen Versatz verschoben, rundum periodisch (Streifen = drei Kacheln)."""
    rng = np.random.default_rng(SEEDS[variant])
    wall_mean = nearest_idx(load_tile(WALL_FILES[variant])).mean()
    base = int(min(max(round(wall_mean) + 4, 9), 11))
    dev = grain_of(variant)
    reps = STRIP // 64
    idx = np.zeros((ATLAS[1], ATLAS[0]), int) + 2

    def grain(rows, y0, dx, dy):
        out = np.zeros((rows, STRIP), int)
        for r in range(rows):
            out[r] = np.tile(np.roll(dev[(y0 + r + dy) % 64], dx), reps)
        return out

    sh = np.zeros((320, STRIP), int)
    for d in range(10):
        sh[d * 32:(d + 1) * 32] = base + grain(32, 0, int(rng.integers(0, 64)), int(rng.integers(0, 64)))
    for d in range(10):
        r0 = d * 32
        sh[r0 + 31, :] = np.clip(sh[r0 + 31, :] + 1, 0, 14)       # Oberkante der Trommel: Fase, heller
        sh[r0 + 2, :] = 6                                          # Schatten ueber der Fuge
        sh[r0:r0 + 2, :] = 2                                       # Fuge, 2 px
    for _ in range(14):                                            # kleine Ausbrueche
        cx, cy = int(rng.integers(0, STRIP)), int(rng.integers(0, 310))
        w, h = int(rng.integers(2, 4)), int(rng.integers(1, 3))
        for dx in range(w):
            for dy in range(h):
                if (cy + 4 + dy) % 32 > 3:
                    sh[(cy + 4 + dy) % 320, (cx + dx) % STRIP] = max(base - 3, 7)
    for _ in range(5):                                             # Risse
        cx, cy, ln = int(rng.integers(0, STRIP)), int(rng.integers(0, 300)), int(rng.integers(5, 11))
        for k in range(ln):
            y = cy + 4 + k
            if y % 32 > 3 and y < 320:
                sh[y, (cx + (k // 4)) % STRIP] = 7
    idx[0:320, 0:STRIP] = sh

    def band(values, rows_y0, shift):
        g = grain(32, rows_y0, shift, rows_y0)
        out = np.zeros((32, STRIP), int)
        for r in range(32):
            lo, kind = values[r]
            out[r] = np.clip(lo + g[r], 0, 14) if kind else lo
        return out

    # Sockel: Bodenfuge, Steinflaeche base+1, Fase oben
    so = [(4, 0)] * 3 + [(7, 0)] + [(base + 1, 1)] * 23 + [(8, 0)] + [(13, 1)] * 4
    idx[320:352, 0:STRIP] = band(so, 0, 17)
    # Kapitell von unten (1/16 = 4 px): Fase 0-3, senkrecht 4-7, Kehle 8-11, Wulst 12-19, Fase 20-23, Platte 24-31
    ka = ([(8, 0)] * 4 + [(base + 1, 1)] * 3 + [(6, 0)] + [(6, 0)] * 4 + [(12, 1)] * 4 + [(7, 0)] * 4 + [(13, 1)] * 4
          + [(base + 1, 1)] * 7 + [(12, 0)])
    assert len(ka) == 32
    idx[352:384, 0:STRIP] = band(ka, 32, 41)

    idx[8:16, STRIP:STRIP + 12] = 12     # Deckflaeche
    idx[8:16, STRIP + 12:STRIP + 24] = 3  # Standflaeche
    # Pruefungen: Fugen und Steinflaeche bleiben getrennte Stufen; nicht heller als der Wandpfeiler
    stone = sh[(np.arange(320) % 32) > 3]
    assert (stone <= 7).mean() < 0.03, (stone <= 7).mean()
    assert (sh[(np.arange(320) % 32) < 2] == 2).all() and (sh[(np.arange(320) % 32) == 2] == 6).all()
    fa = nearest_idx(frame_atlas(variant)[0:64, 64:128][::-1])
    assert idx[0:320, 0:STRIP][(np.arange(320) % 32 > 3)].mean() <= fa.mean(), (idx[0:320, 0:STRIP].mean(), fa.mean())
    tex = STEIN[np.clip(idx, 0, 14)].astype(float)
    return tex, base


def frame_atlas(variant):
    """Wand- und Pfeilerkachel (Frame, 128 x 64) wie im Probestueck #536; nur fuer die Szenen."""
    tile = load_tile(WALL_FILES[variant])
    flat = tile.reshape(-1, 3)
    colours = sorted({tuple(int(c) for c in p) for p in flat})
    idx = {c: int(((STEIN - np.array(c)) ** 2).sum(1).argmin()) for c in colours}
    lo, hi, top = min(idx.values()), max(idx.values()), len(STEIN) - 1
    new_lo = min(max(lo + LIFT, 0), top)
    span = max(hi - lo, 1)
    mapped = {c: int(round(new_lo + (i - lo) * (top - new_lo) / span)) for c, i in idx.items()}
    lifted = np.array([STEIN[mapped[tuple(int(c) for c in p)]] for p in flat]).reshape(tile.shape)
    out = np.zeros((64, 128, 3))
    out[0:64, 0:64] = tile[::-1]
    out[0:64, 64:128] = lifted[::-1]
    return out


# --- Flaechen fuer den Wandpfeiler der Szenen (Welt-UV, wie #536) -------------------------------------------------------

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


def G_inv(p):
    return V(p[0], p[2], -p[1])


def raw(part, pts, hint, field):
    P = [V(*p) for p in pts]
    flip = (P[1] - P[0]).cross(P[2] - P[0]).dot(V(*hint)) < 0
    others = [G_inv(v.co) for v in part.bm.verts]
    withs = []
    for i, a in enumerate(P):
        b = P[(i + 1) % len(P)]
        withs.append(a)
        ab = b - a
        L2 = ab.dot(ab)
        if L2 < 1e-12:
            continue
        hits = []
        for q in others:
            t = (q - a).dot(ab) / L2
            if 1e-6 < t < 1 - 1e-6 and (a + ab * t - q).length < 1e-6:
                hits.append((t, q))
        for t, q in sorted(hits, key=lambda h: h[0]):
            if not withs or (withs[-1] - q).length > 1e-6:
                withs.append(q)
    P = withs
    if flip:
        P.reverse()
    u0, v0, u1, v1 = part.rects[field]
    verts = [part.bm.verts.new(G(p - part.pivot)) for p in P]
    part._face(verts, [(u0 + (u1 - u0) * 0.5, v0 + (v1 - v0) * 0.5)] * len(verts))


def pf_profile(cb):
    pts = [(0.0, S), (0.4375, S), (0.5, 0.0), (cb, 0.0), (cb + S, S), (cb + 2 * S, S), (cb + 2 * S, 0.0),
           (cb + 3 * S, 0.0), (cb + 4 * S, S), (cb + 5 * S, 0.0), (cb + 6 * S, S), (cb + 8 * S, S)]
    out = []
    for p in pts:
        if not out or out[-1] != p:
            out.append(p)
    return out


def build_wall_pillar(x0=0.0, depth=0.25, cb=CB):
    """Wandpfeiler Raum (Probestueck #536), nur als Nachbar in den Szenen."""
    part = Part("pfeiler", (0, 0, 0), FRAME_UV)
    rings = [(y, 0.25 + o, depth + o) for y, o in pf_profile(cb)]
    xs = lambda hx, s: x0 + s * hx                                  # noqa: E731
    for (y0, h0, f0), (y1, h1, f1) in zip(rings, rings[1:]):
        dx, dy, dz = h1 - h0, y1 - y0, f1 - f0
        for s in (1, -1):
            emit(part, [(xs(h0, s), y0, BACK), (xs(h0, s), y0, f0), (xs(h1, s), y1, f1), (xs(h1, s), y1, BACK)],
                 (s * dy, -dx, 0), "pillar")
        emit(part, [(xs(h0, -1), y0, f0), (xs(h0, 1), y0, f0), (xs(h1, 1), y1, f1), (xs(h1, -1), y1, f1)],
             (0, -dz, dy), "pillar")
    y, h, f = rings[-1]
    emit(part, [(xs(h, -1), y, BACK), (xs(h, 1), y, BACK), (xs(h, 1), y, f), (xs(h, -1), y, f)], (0, 1, 0), "pillar")
    right = [(xs(h, 1), y, BACK) for y, h, f in rings]
    left = [(xs(h, -1), y, BACK) for y, h, f in reversed(rings)]
    raw(part, right + left, (0, 0, -1), "pillar")
    y, h, f = rings[0]
    raw(part, [(xs(h, -1), y, BACK), (xs(h, 1), y, BACK), (xs(h, 1), y, f), (xs(h, -1), y, f)], (0, -1, 0), "pillar")
    return part


# --- Die Saeule ------------------------------------------------------------------------------------------------------

def profile():
    """(y, o, Streifen): o = Vorsprung vor dem Schaft (0 oder 1/16); Streifen = Atlas-Feld des Abschnitts darunter.
    Sockel wie der Wandpfeiler (Fase 1/16 oben), Kapitell: Fase, Platte, Kehle, Wulst (Raute), Fase, Platte."""
    return [(0.0, S), (0.4375, S), (0.5, 0.0), (CB, 0.0),
            (CB + S, S), (CB + 2 * S, S), (CB + 2 * S, 0.0), (CB + 3 * S, 0.0),
            (CB + 4 * S, S), (CB + 5 * S, 0.0), (CB + 6 * S, S), (CB + 8 * S, S)]


def field_of(y0, y1):
    ym = (y0 + y1) / 2
    return "sockel" if ym < 0.5 else ("schaft" if ym < CB else "kapitell")


def v_of(field, y):
    """Atlas-v in Meter -> UV (v entlang der Hoehe; Schaft ab y = 0,5, Kapitell ab CB, Sockel ab 0)."""
    u0, v0, u1, v1 = COL_UV[field]
    y_start = {"sockel": 0.0, "schaft": 0.5, "kapitell": CB}[field]
    span = {"sockel": 0.5, "schaft": CB - 0.5, "kapitell": 0.5}[field]
    return v0 + (v1 - v0) * (y - y_start) / span


def ring_pts(n, R, y, x0=0.0, z0=0.0):
    return [(x0 + R * math.cos(2 * math.pi * k / n), y, z0 + R * math.sin(2 * math.pi * k / n)) for k in range(n)]


def build_column(n, x0=0.0, z0=0.0):
    part = Part("saeule_%d" % n, (0, 0, 0), COL_UV)
    prof = profile()
    rings = [(y, R_SHAFT + o) for y, o in prof]
    for (y0, R0), (y1, R1) in zip(rings, rings[1:]):
        field = field_of(y0, y1)
        u0, _, u1, _ = COL_UV[field]
        a, b = ring_pts(n, R0, y0, x0, z0), ring_pts(n, R1, y1, x0, z0)
        dy, dR = y1 - y0, R1 - R0
        for k in range(n):
            k2 = (k + 1) % n
            quad = [a[k], a[k2], b[k2], b[k]]
            cx = sum(p[0] for p in quad) / 4 - x0
            cz = sum(p[2] for p in quad) / 4 - z0
            hint = (dy * cx, -dR * math.hypot(cx, cz), dy * cz)
            Pq = [V(*p) for p in quad]
            c1 = (Pq[1] - Pq[0]).cross(Pq[2] - Pq[0])
            c2 = (Pq[2] - Pq[0]).cross(Pq[3] - Pq[0])
            nrm = c1 + c2
            if nrm.dot(V(*hint)) < 0:
                Pq = [Pq[0], Pq[3], Pq[2], Pq[1]]
                src = [(0, y0), (0, y1), (1, y1), (1, y0)]
            else:
                src = [(0, y0), (1, y0), (1, y1), (0, y1)]
            verts = [part.bm.verts.new(G(p - part.pivot)) for p in Pq]
            uvs = [(u0 + (u1 - u0) * (k + s) / n, v_of(field, yy)) for s, yy in src]
            part._face(verts, uvs)
    # Boden und Deckflaeche: ein Vieleck
    top = ring_pts(n, rings[-1][1], rings[-1][0], x0, z0)
    bot = ring_pts(n, rings[0][1], rings[0][0], x0, z0)
    for pts, hint, field in ((top, 1, "oben"), (bot, -1, "unten")):
        Pq = [V(*p) for p in pts]
        c = (Pq[1] - Pq[0]).cross(Pq[2] - Pq[0])
        if c.y * hint < 0:
            Pq.reverse()
        verts = [part.bm.verts.new(G(p - part.pivot)) for p in Pq]
        u0, v0, u1, v1 = COL_UV[field]
        part._face(verts, [((u0 + u1) / 2, (v0 + v1) / 2)] * len(verts))
    return part


def edge_report(part):
    keys, edges = {}, {}
    for f in part.bm.faces:
        ids = [keys.setdefault(tuple(round(c * 1e5) for c in v.co), len(keys)) for v in f.verts]
        for a, b in zip(ids, ids[1:] + ids[:1]):
            k = (min(a, b), max(a, b))
            edges[k] = edges.get(k, 0) + 1
    return sum(1 for n in edges.values() if n == 1), sum(1 for n in edges.values() if n > 2)


def triangles(part):
    return sum(len(f.verts) - 2 for f in part.bm.faces)


def radii_report(n):
    print("RADIEN %d-Eck (Ecken R / Flaechenmitte R*cos(180/n)):" % n)
    seen = set()
    for y, o in profile():
        R = R_SHAFT + o
        key = (round(y, 4), round(R, 4))
        if key in seen:
            continue
        seen.add(key)
        print("  y=%.4f  max %.4f  min %.4f" % (y, R, R * math.cos(math.pi / n)))


def export_models():
    rows = []
    for n in (12, 8):
        for v in WALL_FILES:
            bpy.ops.wm.read_factory_settings(use_empty=True)
            coll = bpy.data.collections.new("Saeule")
            bpy.context.scene.collection.children.link(coll)
            tex, base = column_atlas(v)
            assert only_stein(tex), "Farbe ausserhalb der Stein-Reihe"
            d = os.path.join(OUT, "modelle")
            os.makedirs(d, exist_ok=True)
            name = "saeule_%d_%s" % (n, v)
            image = save_image(name, tex, os.path.join(d, name + ".png"))
            part = build_column(n)
            o_edges, multi = edge_report(part)
            tris = triangles(part)
            obj = part.build(coll, export_material("m", image), smooth=False)
            export_glb([obj], os.path.join(d, name + ".glb"))
            rows.append((n, v, tris, o_edges, multi, colours_of(tex), base))
    for r in rows:
        print("MODELL %d-Eck %s: %d Dreiecke, offene Kanten %d, mehrfach %d, Farben %d, Basisstufe %d" % r)
    radii_report(12)
    radii_report(8)
    return rows


# --- Szenen ----------------------------------------------------------------------------------------------------------

IMGS = {}


def new_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    IMGS.clear()
    coll = bpy.data.collections.new("Szene")
    bpy.context.scene.collection.children.link(coll)
    return coll


def img_column(v):
    if ("c", v) not in IMGS:
        IMGS[("c", v)] = save_image("sp_" + v, column_atlas(v)[0], os.path.join(TMP, "saeule_%s.png" % v))
    return IMGS[("c", v)]


def img_frame(v):
    if ("f", v) not in IMGS:
        IMGS[("f", v)] = save_image("fr_" + v, frame_atlas(v), os.path.join(TMP, "frame_%s.png" % v))
    return IMGS[("f", v)]


def img_flat(name, rgb):
    if ("x", name) not in IMGS:
        IMGS[("x", name)] = save_image("fl_" + name, np.tile(np.array(rgb, float), (4, 4, 1)),
                                       os.path.join(TMP, "flat_%s.png" % name))
    return IMGS[("x", name)]


def finish(objs):
    bpy.context.view_layer.update()
    for o in objs:
        o["smooth"] = False
        write_light(o)


def add_column(coll, n, v, x, z):
    o = build_column(n, x, z).build(coll, pass_material("c_%d_%s_%g_%g" % (n, v, x, z), img_column(v)), smooth=False)
    return [o]


def add_wall(coll, v, x0, x1, h, z=0.0, y0=0.0):
    part = Part("wand", (0, 0, 0), FRAME_UV)
    emit(part, [(x0, y0, z), (x1, y0, z), (x1, h, z), (x0, h, z)], (0, 0, 1), "stone")
    return [part.build(coll, pass_material("w_%s_%g_%g_%g" % (v, x0, z, y0), img_frame(v)), smooth=False)]


def add_wall_pillar(coll, v, x):
    return [build_wall_pillar(x).build(coll, pass_material("wp_%s_%g" % (v, x), img_frame(v)), smooth=False)]


def add_floor(coll, x0=-12, x1=12, z0=-1, z1=18):
    img = stone_textures(TMP)["floor"]
    part = Part("boden", (0, 0, 0), {"all": (0.0, 0.0, 1.0, 1.0)})
    verts = [part.bm.verts.new(G(V(x, 0, z))) for x, z in ((x0, z0), (x1, z0), (x1, z1), (x0, z1))]
    part._face(verts, [(x0, z0), (x1, z0), (x1, z1), (x0, z1)])
    return [part.build(coll, pass_material("boden", img), smooth=False)]


def add_ceiling(coll, x0=-12, x1=12, z0=-3.5, z1=18):
    img = stone_textures(TMP)["ceiling"]
    part = Part("decke", (0, 0, 0), {"all": (0.0, 0.0, 1.0, 1.0)})
    verts = [part.bm.verts.new(G(V(x, HEIGHT, z))) for x, z in ((x1, z0), (x0, z0), (x0, z1), (x1, z1))]
    part._face(verts, [(x1, z0), (x0, z0), (x0, z1), (x1, z1)])
    return [part.build(coll, pass_material("decke", img), smooth=False)]


def add_figure(coll, x, z):
    """Vergleichsfigur 1,8 m (einfacher Quader, nur fuer den Massstab)."""
    part = Part("figur", (0, 0, 0), {"all": (0.0, 0.0, 1.0, 1.0)})
    part.box((x - 0.25, 0, z - 0.15), (x + 0.25, 1.8, z + 0.15), "all")
    return [part.build(coll, pass_material("figur", img_flat("figur", (60, 75, 116))), smooth=False)]


def add_door(coll, v, x=0.0):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(MODELS, "door_frame_normal_%s.glb" % v))
    objs = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    for o in objs:
        img = None
        for m in o.data.materials:
            for n in m.node_tree.nodes:
                if n.type == "TEX_IMAGE":
                    img = n.image
        o.data.materials.clear()
        o.data.materials.append(pass_material("d_" + o.name, img))
        o.parent = None
        o.matrix_world = Matrix.Translation(G(V(x, 0, -1)))
        coll.objects.link(o)
        for c in list(o.users_collection):
            if c is not coll:
                c.objects.unlink(o)
    return objs


def build_scene(items):
    coll = new_scene()
    objs = []
    for it in items:
        k = it[0]
        if k == "col":
            objs += add_column(coll, it[1], it[2], it[3], it[4])
        elif k == "wall":
            objs += add_wall(coll, it[1], it[2], it[3], it[4], it[5] if len(it) > 5 else 0.0, it[6] if len(it) > 6 else 0.0)
        elif k == "wp":
            objs += add_wall_pillar(coll, it[1], it[2])
        elif k == "floor":
            objs += add_floor(coll)
        elif k == "ceil":
            objs += add_ceiling(coll)
        elif k == "fig":
            objs += add_figure(coll, it[1], it[2])
        elif k == "door":
            objs += add_door(coll, it[1], it[2])
    finish(objs)
    return objs


def shot(items, loc, tgt, fov, size, mode="hell", ortho=None):
    build_scene(items)
    cam = camera()
    setup_render(*size)
    if ortho:
        cam.data.type = "ORTHO"
        cam.data.ortho_scale = ortho
    else:
        cam.data.angle = math.radians(fov)
    aim(cam, G(V(*loc)), G(V(*tgt)))
    p = render_passes("s539")
    if mode == "hell":
        base, light = p["base"][..., :3] * 255, p["light"][..., :1]
        cov = p["base"][..., 3:4] > 0.5
        img = np.where(cov, np.clip(base * light * 1.3, 0, 255), BG)
    else:
        img = compose(p, "spiel")
    return np.round(img[::-1])


# --- Beschriftung ----------------------------------------------------------------------------------------------------

def text(img, s, x, y, size=18, color=(235, 230, 240)):
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


def titled(img, s):
    img = img.copy()
    text(img, s, 6, 4, 18)
    return img


def zoom_px(img, f):
    return np.repeat(np.repeat(img, f, axis=0), f, axis=1)


# --- Ansichten ---------------------------------------------------------------------------------------------------------

N = 12
SIDES_DEFAULT = int(os.environ.get("S539_N", "12"))


def view_single(which, n=SIDES_DEFAULT, v="a", size=(360, 640)):
    items = [("floor",), ("col", n, v, 0.0, 0.0)]
    cy, scale = HEIGHT / 2, HEIGHT + 0.5
    if which == "vorn":
        return shot(items, (0, cy, 10), (0, cy, 0), 0, size, "hell", ortho=scale)
    if which == "seite":
        return shot(items, (10, cy, 0), (0, cy, 0), 0, size, "hell", ortho=scale)
    if which == "hinten":
        return shot(items, (0, cy, -10), (0, cy, 0), 0, size, "hell", ortho=scale)
    d = 6.9
    return shot(items, (d * 0.7, HEIGHT + d * 0.45, d * 0.8), (0, HEIGHT * 0.45, 0), 38, size, "hell")


def room_items(n, v, back=-3.5):
    return [("floor",), ("ceil",), ("wall", v, -12, 12, HEIGHT, back), ("col", n, v, 0.0, 0.0)]


def view_eye(dist, mode, n=SIDES_DEFAULT, v="a", size=(854, 480), tilt=None):
    tgt = (0, 1.6, 0) if tilt is None else (0, tilt, 0)
    return shot(room_items(n, v), (0.0, 1.6, dist), tgt, 70, size, mode)


def view_up(n=SIDES_DEFAULT, v="a", mode="hell", size=(854, 480)):
    return shot(room_items(n, v), (0.0, 1.6, 3.0), (0, 5.6, 0), 70, size, mode)


def view_detail(n=SIDES_DEFAULT, v="a", size=(420, 420)):
    items = [("floor",), ("col", n, v, 0.0, 0.0)]
    out = []
    for cy, lbl in ((0.4, "Sockel"), (CB + 0.25, "Kapitell")):
        a = shot(items, (1.5, cy + 0.35, 1.6), (0, cy, 0), 40, size, "hell")
        b = shot(items, (6, cy, 0), (0, cy, 0), 0, size, "hell", ortho=1.2)
        out += [titled(a, lbl + " schraeg"), titled(b, lbl + " Profil (Seite)")]
    return hcat(out)


def view_row(n=SIDES_DEFAULT, v="a", mode="hell", size=(854, 480), dist=13.0):
    items = [("floor",), ("ceil",), ("wall", v, -12, 12, HEIGHT, 0.0)]
    items += [("wp", v, x) for x in (-4.0, 0.0, 4.0)]
    items += [("col", n, v, x, 3.0) for x in (-6.0, -2.0, 2.0, 6.0)]
    return shot(items, (1.5, 1.6, dist), (-0.5, 2.6, 0), 70, size, mode)


def view_row_oblique(n=SIDES_DEFAULT, v="a", size=(960, 540)):
    items = [("floor",), ("wall", v, -12, 12, HEIGHT, 0.0)]
    items += [("wp", v, x) for x in (-4.0, 0.0, 4.0)]
    items += [("col", n, v, x, 3.0) for x in (-6.0, -2.0, 2.0, 6.0)]
    return shot(items, (11.0, 2.0, 9.0), (-1.0, 2.6, 1.0), 62, size, "hell")


def view_compare_size(n=SIDES_DEFAULT, v="a", size=(900, 760)):
    items = [("floor",), ("wall", v, -6, -3.4, HEIGHT, 0.0), ("wall", v, -1.4, 6, HEIGHT, 0.0),
             ("wall", v, -3.4, -1.4, HEIGHT, 0.0, 3.5), ("door", v, -2.4), ("col", n, v, 1.6, 1.6), ("fig", 0.4, 1.6)]
    return shot(items, (0.0, 3.0, 14), (0.0, 3.0, 0), 0, size, "hell", ortho=HEIGHT + 0.6)


def view_top(n, v="a", size=(640, 640), scale=1.6):
    items = [("floor",), ("col", n, v, 0.0, 0.0)]
    img = shot(items, (0, 10, 0.01), (0, 0, 0), 0, size, "hell", ortho=scale).copy()
    ppm = size[1] / scale
    cx, cy = size[0] / 2, size[1] / 2
    ys, xs = np.mgrid[0:size[1], 0:size[0]]
    for r, col in ((0.5, (230, 70, 70)), (R_SHAFT, (80, 190, 230))):
        d = np.hypot(xs + 0.5 - cx, ys + 0.5 - cy) / ppm
        img[np.abs(d - r) < 1.1 / ppm] = col
    text(img, "rot: Kreis R 0,5 m   blau: Schaft R 0,4375 m   (%d-Eck)" % n, 6, 4, 16)
    return img


def view_compare_fill(v="a"):
    """Achteck gegen Zwoelfeck, ein Bild: Draufsicht mit Kreis und beide im Spielbild in 8 m."""
    return hcat([view_top(8, v, (420, 420), 1.3), view_top(12, v, (420, 420), 1.3)])


def view_compare_game(v="a", dist=8.0, size=(854, 480)):
    items = [("floor",), ("wall", v, -12, 12, HEIGHT, -3.5), ("col", 8, v, -0.9, 0.0), ("col", 12, v, 0.9, 0.0)]
    return shot(items, (0.0, 1.6, dist), (0, 1.6, 0), 70, size, "spiel")


def view_compare_close(v="a", size=(900, 540)):
    items = [("floor",), ("wall", v, -12, 12, HEIGHT, -3.5), ("col", 8, v, -0.9, 0.0), ("col", 12, v, 0.9, 0.0)]
    return shot(items, (0.0, 3.0, 12), (0, 3.0, 0), 0, size, "hell", ortho=2.6)


def view_naht(n=SIDES_DEFAULT, v="a"):
    """Naht des Streifens (u = 0/1 liegt auf der +x-Ecke): Blick von +x und von der gegenueberliegenden Seite."""
    items = [("floor",), ("col", n, v, 0.0, 0.0)]
    a = shot(items, (10, 2.5, 0), (0, 2.5, 0), 0, (520, 520), "hell", ortho=1.4)
    b = shot(items, (-10, 2.5, 0), (0, 2.5, 0), 0, (520, 520), "hell", ortho=1.4)
    c = shot(items, (0, 2.5, 10), (0, 2.5, 0), 0, (520, 520), "hell", ortho=1.4)
    return hcat([titled(a, "von +x (Naht in der Mitte)"), titled(c, "von vorn"), titled(b, "von -x (Mitte des Streifens)")])


def sheet_texture(v="a"):
    tex, _ = column_atlas(v)
    shaft = tex[0:320, 0:STRIP][::-1]
    two = np.concatenate([shaft, shaft], axis=1)                       # zweimal nebeneinander: Naht in der Mitte
    img = zoom_px(two, 3)
    img[:, STRIP * 3 - 1:STRIP * 3 + 1] = (230, 70, 70)
    img = np.concatenate([np.tile(BG, (30, img.shape[1], 1)), img], 0)
    text(img, "Schaftstreifen 192 x 320 px, zweimal nebeneinander (rote Linie = Naht); Trommelfugen alle 32 px", 6, 4, 16)
    return img


def sheet_atlas(v="a"):
    tex, _ = column_atlas(v)
    return zoom_px(tex[::-1], 2)


def main():
    rows = export_models()
    if ONLY == "modelle":
        return
    for v in WALL_FILES:
        png(hcat([titled(view_single(w, 12, v), "%s (%s)" % (w, v)) for w in ("vorn", "seite", "hinten", "schraeg-oben")]),
            os.path.join(OUT, "einzel-%s.png" % v))
    png(hcat([titled(view_single(w, 8, "a"), "8-Eck %s" % w) for w in ("vorn", "seite", "hinten", "schraeg-oben")]),
        os.path.join(OUT, "einzel-8eck-a.png"))
    for d in (3, 8):
        png(view_eye(d, "spiel"), os.path.join(OUT, "auge-%dm-spiel.png" % d))
        png(view_eye(d, "hell"), os.path.join(OUT, "auge-%dm-hell.png" % d))
    png(view_up(mode="hell"), os.path.join(OUT, "blick-hoch-hell.png"))
    png(view_up(mode="spiel"), os.path.join(OUT, "blick-hoch-spiel.png"))
    png(view_detail(), os.path.join(OUT, "detail-sockel-kapitell.png"))
    png(view_row(mode="spiel"), os.path.join(OUT, "reihe-spiel.png"))
    png(view_row(mode="hell"), os.path.join(OUT, "reihe-hell.png"))
    png(view_row_oblique(), os.path.join(OUT, "reihe-schraeg-hell.png"))
    png(view_compare_size(), os.path.join(OUT, "groesse-tuer-figur.png"))
    png(hcat([view_top(12), view_top(8)]), os.path.join(OUT, "draufsicht-kreis.png"))
    png(view_compare_game(), os.path.join(OUT, "vergleich-8eck-12eck-spiel.png"))
    png(view_compare_close(), os.path.join(OUT, "vergleich-8eck-12eck-hell.png"))
    png(view_naht(), os.path.join(OUT, "naht.png"))
    png(sheet_texture(), os.path.join(OUT, "textur-streifen.png"))
    png(hcat([sheet_atlas(v) for v in WALL_FILES]), os.path.join(OUT, "atlas-a-b-c.png"))
    for v in ("b", "c"):
        png(view_eye(3, "hell", 12, v), os.path.join(OUT, "auge-3m-hell-%s.png" % v))


main()
