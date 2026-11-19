"""Probestueck Brunnen, Runde 2 (Issue #438): der Schalenbrunnen in drei Ausfuehrungen A1, A2, A3.

Alles entsteht aus diesem einen Skript: Geometrie (Drehkoerper, Spiel-Achsen: x seitlich, y oben, z vorn, Meter) mit UVs,
eine Textur je Ausfuehrung (64 x 64, hoechstens 32 Farben, nur Farben aus assets/palette/hauptpalette.json, in Palettenstufen
gemalt), Wasser und Wasserfaelle als eigene Teile mit vier Atlas-Bildern (Animation = andere UV-Rechtecke, harte Stufen),
Modelle (.glb), Bilder, GIFs und Probenblatt. Gebaut auf tools/models/psx.py (gleiche Render-Durchgaenge wie die anderen
Probestuecke).

Aufruf (aus dem Repository-Wurzelverzeichnis, ein Ausgabeordner als erstes Argument nach --):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/brunnen-438/brunnen2.py -- <ausgabeordner>
Ohne Argument schreibt es in den Ordner des Skripts. Zwei Laeufe liefern bitgleiche Dateien (alles mit festem Seed).
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
from mathutils import Matrix  # noqa: E402
from psx import (V, G, FOG_COLOR, Part, rects_from_pixels, vnoise, save_image, export_material, stone_textures,  # noqa: E402
                 build_room, pass_material, write_light, setup_render, camera, aim, render_passes, compose, save_png,
                 export_glb, MATS)

OUT = HERE
if "--" in sys.argv and len(sys.argv) > sys.argv.index("--") + 1:
    OUT = os.path.abspath(sys.argv[sys.argv.index("--") + 1])
os.makedirs(OUT, exist_ok=True)
TMP = os.path.join(tempfile.gettempdir(), "brunnen2_tmp")
os.makedirs(TMP, exist_ok=True)

PAL = json.load(open(os.path.join(ROOT, "assets", "palette", "hauptpalette.json"), encoding="utf-8"))
RAMPS = {r["name"]: r["farben"] for r in PAL["grund"] + PAL["akzent"]}
TAB, IDS = [], {}


def pc(ramp, i):
    """Index einer Palettenfarbe in der Farbtabelle der Texturen (Reihe, Stufe)."""
    key = (ramp, i)
    if key not in IDS:
        IDS[key] = len(TAB)
        TAB.append(RAMPS[ramp][i])
    return IDS[key]


def st(i):
    return pc("Stein", int(min(max(i, 2), 13)))


def mo(i):
    return pc("Haut/Knochen", i)          # Moos: das einzige Gruen der Palette (Frage an den Menschen)


def wa(i):
    return pc("Stoff", i)                 # Wasser


def go(i):
    return pc("Messing/Gold", i)          # Funkeln der Beute


def ei(i):
    return pc("Eisen", i)


# ---------------------------------------------------------------- Textur (64 x 64, Zeile 0 unten)

RECTS = {
    "wall": (0, 48, 32, 64), "top": (32, 48, 64, 64), "plinth": (0, 32, 32, 48), "col": (32, 32, 48, 48),
    "stat": (48, 32, 64, 48), "iron": (0, 16, 16, 32), "dry": (16, 16, 32, 32),
    "fall0": (32, 16, 40, 32), "fall1": (40, 16, 48, 32), "fall2": (48, 16, 56, 32), "fall3": (56, 16, 64, 32),
    "water0": (0, 0, 16, 16), "water1": (16, 0, 32, 16), "water2": (32, 0, 48, 16), "water3": (48, 0, 64, 16),
}
UVR = rects_from_pixels(RECTS, 64)
PLANAR = {"dry"}


def rects_for(frame):
    r = dict(UVR)
    r["water"] = r["waterside"] = UVR["water%d" % frame]
    r["fall"] = UVR["fall%d" % frame]
    return r


def moss_field(h, w, seed, amount, bias):
    """Wo Moos waechst: weiches Rauschen mit Zufall gegen eine Schwelle; bias (h, w) erhoeht die Schwelle."""
    nz = vnoise(h, w, max(2, h // 4), max(2, w // 4), seed)
    rnd = np.random.default_rng(seed + 99).random((h, w))
    return (0.65 * nz + 0.35 * rnd) < amount * bias * 0.9


def moss_ids(h, w, seed):
    """Moosfarben in harten Stufen: Haut/Knochen 1 (Schatten), 2 (Grund), 3 (Licht)."""
    n = vnoise(h, w, max(2, h // 2), max(2, w // 2), seed + 7)
    out = np.full((h, w), mo(2))
    out[n < 0.33] = mo(1)
    out[n > 0.72] = mo(3)
    return out


def gravity(h, w, strength=1.0):
    y = np.arange(h)[:, None] / (h - 1)
    return np.clip(1.25 - 1.5 * y, 0, 1.3) * np.ones((1, w)) * strength


def add_moss(img, amount, seed, bias):
    if amount <= 0:
        return img
    m = moss_field(img.shape[0], img.shape[1], seed, amount, bias)
    img[m] = moss_ids(img.shape[0], img.shape[1], seed)[m]
    return img


def p_wall(moss, seed):
    rng = np.random.default_rng(seed)
    h, w = 16, 32
    img = np.zeros((h, w), int)
    tones = {}
    for y in range(h):
        c, ly = y // 8, y % 8
        off = 0 if c == 0 else 8
        for x in range(w):
            lx = (x - off) % 16
            bx = ((x - off) % w) // 16
            t = tones.setdefault((c, bx), int(rng.choice([7, 8, 9, 10])))
            r1, r2 = rng.random(), rng.random()
            if lx == 0 or ly == 0:
                v = 2
            elif ly == 7:
                v = t + 2
            elif lx == 1:
                v = t + 1
            elif ly == 1:
                v = t - 2
            else:
                v = t + (1 if r1 < 0.15 else (-1 if r2 < 0.15 else 0))
            img[y, x] = st(v)
    joint = img == st(2)
    near = joint | np.roll(joint, 1, 0) | np.roll(joint, -1, 1)
    return add_moss(img, moss, seed, gravity(h, w) + 0.35 * near)


def p_top(moss, seed):
    rng = np.random.default_rng(seed + 1)
    h, w = 16, 32
    img = np.full((h, w), st(10))
    for y in range(h):
        for x in range(w):
            r = rng.random()
            d = min(x, w - 1 - x, y, h - 1 - y)
            v = 12 if d == 0 else (9 if d == 1 else 10 + (1 if r < 0.12 else (-1 if r > 0.88 else 0)))
            img[y, x] = st(v)
    for _ in range(2):                                   # Risse
        x, y = int(rng.integers(6, 26)), int(rng.integers(4, 12))
        for _ in range(6):
            img[y, x] = st(5)
            x += int(rng.integers(-1, 2)) + 1
            y += int(rng.integers(-1, 2))
            x, y = min(max(x, 2), w - 3), min(max(y, 2), h - 3)
    yy, xx = np.mgrid[0:h, 0:w]
    d = np.minimum(np.minimum(xx, w - 1 - xx), np.minimum(yy, h - 1 - yy))
    return add_moss(img, moss, seed + 1, np.clip(1.2 - 0.35 * d, 0, 1.2))


def p_plinth(moss, seed):
    rng = np.random.default_rng(seed + 2)
    h, w = 16, 32
    img = np.zeros((h, w), int)
    tones = {}
    for y in range(h):
        c, ly = y // 8, y % 8
        off = 0 if c == 0 else 11
        for x in range(w):
            lx = (x - off) % 22 if False else (x - off) % 16
            bx = ((x - off) % w) // 16
            t = tones.setdefault((c, bx), int(rng.choice([5, 6, 7])))
            v = 2 if (lx == 0 or ly == 0) else (t + 1 if ly == 7 else (t - 1 if ly == 1 else t + (1 if rng.random() < 0.12 else 0)))
            img[y, x] = st(v)
    return add_moss(img, moss, seed + 2, gravity(h, w, 1.25))


def p_col(moss, seed):
    rng = np.random.default_rng(seed + 3)
    h, w = 16, 16
    shade = [8, 9, 9, 7]
    img = np.zeros((h, w), int)
    for y in range(h):
        for x in range(w):
            v = shade[x % 4] + (1 if y == h - 1 else 0) + (1 if rng.random() < 0.1 else 0)
            img[y, x] = st(v)
    for x in (3, 9, 12):                                 # dunkle Wasserspuren
        y0 = int(rng.integers(4, 12))
        img[y0:y0 + 5, x] = st(5)
    return add_moss(img, moss, seed + 3, gravity(h, w, 0.9))


def p_stat(moss, seed):
    rng = np.random.default_rng(seed + 4)
    h, w = 16, 16
    img = np.full((h, w), st(11))
    for y in range(h):
        for x in range(w):
            if x in (2, 6, 10, 14):
                v = 9
            elif x in (3, 7, 11, 15):
                v = 13
            else:
                v = 11 + (1 if rng.random() < 0.15 else 0)
            img[y, x] = st(v)
    img[h - 1, :] = st(12)
    return add_moss(img, moss, seed + 4, gravity(h, w, 0.7))


def p_iron(seed):
    h, w = 16, 16
    img = np.full((h, w), ei(3))
    img[:, 0] = img[:, w - 1] = ei(2)
    img[0, :] = img[h - 1, :] = ei(2)
    img[1:h - 1, 1] = ei(4)
    img[h - 2, 1:w - 1] = ei(4)
    for x, y in ((4, 4), (11, 4), (4, 11), (11, 11)):
        img[y, x] = ei(5)
    return img


def p_dry(moss, seed):
    rng = np.random.default_rng(seed + 5)
    h, w = 16, 16
    img = np.full((h, w), st(3))
    for y in range(h):
        for x in range(w):
            r = rng.random()
            if r < 0.18:
                img[y, x] = st(2)
            elif r > 0.9:
                img[y, x] = st(4)
    img[2:6, 1:6] = st(2)                                 # Schlamm
    for x, y in ((9, 8), (12, 3), (4, 11)):               # Brocken
        img[y:y + 2, x:x + 3] = st(6)
        img[y + 1, x:x + 3] = st(5)
    for x, y in ((7, 12), (11, 6), (2, 8)):               # stumpfe Muenzen
        img[y, x] = ei(3)
        img[y, x + 1] = ei(2)
    yy, xx = np.mgrid[0:h, 0:w]
    d = np.minimum(np.minimum(xx, w - 1 - xx), np.minimum(yy, h - 1 - yy))
    return add_moss(img, moss * 0.6, seed + 5, np.clip(1.1 - 0.3 * d, 0, 1.1))


def p_fall(k, seed):
    """Wasserfall 8 x 16: senkrechte Straehnen; Muster mit Periode 16 in y, je Bild um 4 Zeilen nach unten verschoben."""
    rng = np.random.default_rng(seed + 6)
    pat = np.zeros((16, 8), int)
    for x in range(8):
        base = int(rng.integers(0, 16))
        for seg in range(2):
            s0 = (base + seg * 8) % 16
            for j in range(int(rng.integers(3, 6))):
                pat[(s0 + j) % 16, x] = 1
        pat[(base + 2) % 16, x] = 2
    img = np.zeros((16, 8), int)
    for y in range(16):
        for x in range(8):
            v = pat[(y + 4 * k) % 16, x]
            c = wa(4) if v == 0 else (wa(6) if v == 1 else wa(7))
            if x in (0, 7):
                c = wa(3) if v == 0 else wa(5)
            img[y, x] = c
    return img


SPARK = ((3, 4), (11, 9), (7, 13), (13, 2), (6, 7))


def p_water(k, seed):
    """Wasser 16 x 16: zwei Diagonalwellen (Periode 16), je Bild um ein Viertel Phase; dazu goldenes Funkeln der Beute."""
    h = w = 16
    img = np.zeros((h, w), int)
    for y in range(h):
        for x in range(w):
            a = math.sin(2 * math.pi * ((x + y) / 16 + k / 4))
            b = math.sin(2 * math.pi * ((x - 2 * y) / 16 - k / 4))
            s = a + 0.6 * b
            img[y, x] = wa(6) if s > 1.3 else (wa(5) if s > 0.8 else (wa(4) if s > 0.0 else (wa(3) if s > -0.9 else wa(2))))
    for g, (x, y) in enumerate(SPARK):
        ph = (k + g) % 4
        if ph == 0:
            img[y, x] = go(5)
        elif ph == 1:
            img[y, x] = go(7)
            img[y, x + 1] = go(5)
        elif ph == 2:
            img[y, x] = go(6)
    return img


MOSS = {"A1": 0.40, "A2": 1.00, "A3": 0.0}
SEEDS = {"A1": 11, "A2": 22, "A3": 33}


def atlas(key):
    """Atlas als Feld [y][x][rgb], Zeile 0 unten; nur Palettenfarben, hoechstens 32."""
    m, s = MOSS[key], SEEDS[key]
    pain = {"wall": p_wall(m, s), "top": p_top(m, s), "plinth": p_plinth(m, s), "col": p_col(m, s),
            "stat": p_stat(m * 0.5, s), "iron": p_iron(s), "dry": p_dry(m, s)}
    for k in range(4):
        pain["fall%d" % k] = p_fall(k, s)
        pain["water%d" % k] = p_water(k, s)
    ids = np.zeros((64, 64), int)
    for name, (x0, y0, x1, y1) in RECTS.items():
        ids[y0:y1, x0:x1] = pain[name]
    tab = np.array(TAB, float)
    used = np.unique(ids)
    assert len(used) <= 32, (key, len(used))
    return tab[ids], len(used), ids


ATLAS, NCOL = {}, {}
for _k in ("A1", "A2", "A3"):
    ATLAS[_k], NCOL[_k], _ = atlas(_k)


# ---------------------------------------------------------------- Geometrie

def ring_pts(n, r, y, rot):
    return [(r * math.cos(math.radians(rot) + 2 * math.pi * k / n), y, r * math.sin(math.radians(rot) + 2 * math.pi * k / n))
            for k in range(n)]


def lathe(part, start, segs, n, rot=0.0):
    """Geschlossener Drehkoerper um die senkrechte Achse. start (r, y); segs: (r, y, Rechteck) je Abschnitt; r = 0 an den
    Enden gibt einen Faecher (Pol). Jede Seitenflaeche zeigt das ganze Rechteck; 'dry' wird von oben projiziert."""
    pts = [start] + [(s[0], s[1]) for s in segs]
    rp = max(p[0] for p in pts)
    cache = {}

    def pos(r, y, k):
        if r < 1e-6:
            return V(0.0, y, 0.0)
        a = math.radians(rot) + 2 * math.pi * k / n
        return V(r * math.cos(a), y, r * math.sin(a))

    def vert(r, y, k):
        key = (round(y, 5),) if r < 1e-6 else (k % n, round(r, 5), round(y, 5))
        if key not in cache:
            cache[key] = part.bm.verts.new(G(pos(r, y, k) - part.pivot))
        return cache[key]

    for i, seg in enumerate(segs):
        (r0, y0), (r1, y1), rect = pts[i], pts[i + 1], seg[2]
        u0, v0, u1, v1 = part.rects[rect]
        for k in range(n):
            k2 = k + 1
            if r0 < 1e-6:
                cs = [(r0, y0, k), (r1, y1, k), (r1, y1, k2)]
                uv = [((u0 + u1) / 2, v0), (u0, v1), (u1, v1)]
            elif r1 < 1e-6:
                cs = [(r0, y0, k), (r0, y0, k2), (r1, y1, k)]
                uv = [(u0, v0), (u1, v0), ((u0 + u1) / 2, v1)]
            else:
                cs = [(r0, y0, k), (r0, y0, k2), (r1, y1, k2), (r1, y1, k)]
                uv = [(u0, v0), (u1, v0), (u1, v1), (u0, v1)]
            if rect in PLANAR:
                uv = []
                for r, y, kk in cs:
                    p = pos(r, y, kk)
                    uv.append((u0 + (u1 - u0) * (0.5 + p.x / (2 * rp)), v0 + (v1 - v0) * (0.5 + p.z / (2 * rp))))
            part._face([vert(*c) for c in cs], uv)
    bmesh.ops.recalc_face_normals(part.bm, faces=list(part.bm.faces))


def water_slab(name, R, n, r, y0, y1, rot):
    p = Part(name, (0, 0, 0), R)
    p.prism(ring_pts(n, r, y0, rot), (0, y1 - y0, 0), lambda nn: "water" if nn.y > 0.9 else "waterside")
    return p


def stream(part, cx, cz, along, y0, y1, half=0.03, thick=0.025):
    """Wasserfall: duenner Quader; along = 'x' (Dicke in x) oder 'z'."""
    if along == "x":
        part.box((cx - thick, y0, cz - half), (cx + thick, y1, cz + half), "fall")
    else:
        part.box((cx - half, y0, cz - thick), (cx + half, y1, cz + thick), "fall")


def parts_a1(state, R):
    """A1 Zweischaler: achteckiges Becken, schlanke Saeule, eine obere Schale, Eisenspitze; wenig Moos."""
    n, rot = 8, 22.5
    wet = state == "bereit"
    body = Part("body", (0, 0, 0), R)
    lathe(body, (0, 0), [(0.92, 0, "plinth"), (0.92, 0.12, "plinth"), (0.84, 0.12, "top"), (0.78, 0.78, "wall"),
                         (0.86, 0.86, "wall"), (0.62, 0.88, "top"), (0.60, 0.30, "wall"), (0.0, 0.30, "dry")], n, rot)
    col = Part("column", (0, 0, 0), R)
    lathe(col, (0, 0.30), [(0.17, 0.30, "col"), (0.14, 1.18, "col"), (0.46, 1.34, "wall"), (0.46, 1.42, "wall"),
                           (0.40, 1.43, "top"), (0.38, 1.36, "wall"), (0.11, 1.36, "dry"), (0.09, 1.62, "col"),
                           (0.0, 1.90, "iron")], n, rot)
    parts = [body, col]
    if wet:
        parts.append(water_slab("water_basin", R, n, 0.60, 0.34, 0.66, rot))
        parts.append(water_slab("water_bowl", R, n, 0.37, 1.36, 1.405, rot))
        f = Part("falls", (0, 0, 0), R)
        for sx, sz in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            stream(f, sx * 0.455, sz * 0.455, "x" if sx else "z", 0.62, 1.40)
        parts.append(f)
    return parts


def parts_a2(state, R):
    """A2 Dreischaler: sechseckig, dreistufiger Sockel, zwei Schalen uebereinander, Steinspitze; stark bewachsen."""
    n, rot = 6, 30.0
    wet = state == "bereit"
    body = Part("body", (0, 0, 0), R)
    lathe(body, (0, 0), [(0.98, 0, "plinth"), (0.98, 0.10, "plinth"), (0.90, 0.10, "top"), (0.90, 0.22, "plinth"),
                         (0.82, 0.22, "top"), (0.80, 0.72, "wall"), (0.62, 0.74, "top"), (0.60, 0.30, "wall"),
                         (0.0, 0.30, "dry")], n, rot)
    col = Part("column", (0, 0, 0), R)
    lathe(col, (0, 0.30), [(0.18, 0.30, "col"), (0.14, 0.95, "col"), (0.52, 1.12, "wall"), (0.52, 1.20, "wall"),
                           (0.46, 1.21, "top"), (0.43, 1.14, "wall"), (0.13, 1.14, "dry"), (0.10, 1.50, "col"),
                           (0.34, 1.62, "wall"), (0.34, 1.68, "wall"), (0.29, 1.69, "top"), (0.27, 1.64, "wall"),
                           (0.09, 1.64, "dry"), (0.07, 1.95, "col"), (0.0, 2.30, "col")], n, rot)
    parts = [body, col]
    if wet:
        parts.append(water_slab("water_basin", R, n, 0.60, 0.34, 0.64, rot))
        parts.append(water_slab("water_bowl1", R, n, 0.42, 1.14, 1.185, rot))
        parts.append(water_slab("water_bowl2", R, n, 0.27, 1.64, 1.675, rot))
        f = Part("falls", (0, 0, 0), R)
        for sx in (1, -1):
            stream(f, sx * 0.48, 0.0, "x", 0.60, 1.19)
            stream(f, sx * 0.33, 0.0, "x", 1.17, 1.67)
        parts.append(f)
    return parts


def parts_a3(state, R):
    """A3 Figur mit Speiern: breites flaches Becken, Sockel mit vier Eisen-Speiern, Kapuzenfigur ohne Haende; kein Moos."""
    n, rot = 8, 22.5
    wet = state == "bereit"
    body = Part("body", (0, 0, 0), R)
    lathe(body, (0, 0), [(0.98, 0, "plinth"), (0.98, 0.10, "plinth"), (0.92, 0.10, "top"), (0.86, 0.56, "wall"),
                         (0.90, 0.62, "wall"), (0.66, 0.64, "top"), (0.64, 0.26, "wall"), (0.0, 0.26, "dry")], n, rot)
    ped = Part("pedestal", (0, 0, 0), R)
    lathe(ped, (0, 0.26), [(0.30, 0.26, "col"), (0.26, 0.86, "col"), (0.34, 0.90, "wall"), (0.34, 0.96, "wall"),
                           (0.0, 0.96, "top")], n, rot)
    fig = Part("figure", (0, 0, 0), R)
    lathe(fig, (0, 0.95), [(0.24, 0.95, "stat"), (0.20, 1.30, "stat"), (0.32, 1.60, "stat"), (0.31, 1.68, "stat"),
                           (0.12, 1.74, "stat"), (0.18, 1.80, "stat"), (0.17, 2.02, "stat"), (0.0, 2.22, "stat")], 6, 30.0)
    spouts = Part("spouts", (0, 0, 0), R)
    for sx, sz in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        lo = [min(sx * 0.20, sx * 0.38) if sx else -0.05, 0.70, min(sz * 0.20, sz * 0.38) if sz else -0.05]
        hi = [max(sx * 0.20, sx * 0.38) if sx else 0.05, 0.80, max(sz * 0.20, sz * 0.38) if sz else 0.05]
        spouts.box(tuple(lo), tuple(hi), "iron")
    parts = [body, ped, fig, spouts]
    if wet:
        parts.append(water_slab("water_basin", R, n, 0.62, 0.30, 0.52, rot))
        f = Part("falls", (0, 0, 0), R)
        for sx, sz in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            stream(f, sx * 0.355, sz * 0.355, "x" if sx else "z", 0.46, 0.72, half=0.025, thick=0.02)
        parts.append(f)
    return parts


VARIANTS = {
    "A1": ("Zweischaler", parts_a1, "achteckig, eine Schale, Eisenspitze, wenig Moos"),
    "A2": ("Dreischaler", parts_a2, "sechseckig, zwei Schalen, stark bewachsen"),
    "A3": ("Figurenbrunnen", parts_a3, "Kapuzenfigur ohne Hände, vier Speier, kein Moos"),
}


# ---------------------------------------------------------------- Szene und Bilder

def new_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    coll = bpy.data.collections.new("Brunnen")
    bpy.context.scene.collection.children.link(coll)
    return coll


def add_fountain(coll, key, state, frame, offset=(0.0, 0.0, 0.0), export=False):
    image = save_image("atlas_" + key, ATLAS[key], os.path.join(OUT, "textur_%s.png" % key.lower()))
    objs = [p.build(coll, export_material("m_" + key, image), smooth=False) for p in VARIANTS[key][1](state, rects_for(frame))]
    if export:
        return objs, image
    for o in objs:
        o.data.materials.clear()
        o.data.materials.append(pass_material("p_" + key + o.name, image))
        o.matrix_world = Matrix.Translation(G(V(*offset)))
    bpy.context.view_layer.update()
    for o in objs:
        write_light(o)
    return objs, image


def add_stone_slab(coll, box):
    stone = stone_textures(TMP)
    p = Part("slab", (0, 0, 0), {"all": (0.0, 0.0, 1.0, 1.0)})
    p.box(box[0], box[1], "all")
    o = p.build(coll, pass_material("slab", stone["floor"]), smooth=False)
    bpy.context.view_layer.update()
    write_light(o)
    return o


BG = np.array((74.0, 66.0, 82.0))


def shot(cam, loc, target, fov, size, mode, ortho=None):
    setup_render(*size)
    if ortho:
        cam.data.type = "ORTHO"
        cam.data.ortho_scale = ortho   # gilt fuer die Bildhoehe (sensor_fit VERTICAL)
    else:
        cam.data.angle = math.radians(fov)
    aim(cam, loc, target)
    p = render_passes("b2")
    if mode == "hell":                                  # helle Ansicht: Grundfarbe mal Flaechenlicht, ohne Laterne und Nebel
        base, light = p["base"][..., :3] * 255, p["light"][..., :1]
        cov = p["base"][..., 3:4] > 0.5
        img = np.where(cov, np.clip(base * light * 1.3, 0, 255), BG)
    else:
        img = compose(p, "spiel")
    return np.round(img[::-1])                           # Zeile 0 oben


def view_dirs():
    return (("vorn", (0, 0.12, -1)), ("Seite", (1, 0.12, 0)), ("hinten", (0, 0.12, 1)), ("schräg oben", (-0.7, 0.75, -0.8)))


def height_of(key):
    return {"A1": 1.90, "A2": 2.30, "A3": 2.22}[key]


def bright_view(key, state, d, size, frame=0, dist=4.6, fov=34):
    coll = new_scene()
    add_fountain(coll, key, state, frame)
    add_stone_slab(coll, ((-1.0, -0.10, -1.0), (1.0, 0.0, 1.0)))
    cam = camera()
    tgt = V(0, height_of(key) * 0.47, 0)
    dd = V(*d).normalized()
    return shot(cam, G(tgt + dd * dist), G(tgt), fov, size, "hell")


def game_view(key, state, dist, frame=0, size=(854, 480)):
    coll = new_scene()
    add_fountain(coll, key, state, frame)
    stone = stone_textures(TMP)
    build_room(stone)
    cam = camera()
    return shot(cam, G(V(0, 1.6, -dist)), G(V(0, 1.6, -dist + 10)), 70, size, "spiel")


# ---------------------------------------------------------------- GIF (selbst geschrieben, nichts Fremdes)

def kmeans_palette(px, k, seed=5):
    rng = np.random.default_rng(seed)
    sample = px[rng.choice(len(px), min(len(px), 6000), replace=False)].astype(float)
    cen = sample[rng.choice(len(sample), k, replace=False)].copy()
    for _ in range(10):
        idx = ((sample[:, None, :] - cen[None, :, :]) ** 2).sum(-1).argmin(1)
        for c in range(k):
            sel = sample[idx == c]
            if len(sel):
                cen[c] = sel.mean(0)
    return np.round(cen).astype(np.uint8)


def lzw(indices):
    clear, eoi = 256, 257
    out = bytearray()
    acc = nb = 0
    size, nxt = 9, 258
    table = {}

    def flush_code(code, size, acc, nb):
        acc |= code << nb
        nb += size
        while nb >= 8:
            out.append(acc & 255)
            acc >>= 8
            nb -= 8
        return acc, nb

    acc, nb = flush_code(clear, size, acc, nb)
    prefix = int(indices[0])
    for b in indices[1:]:
        b = int(b)
        key = (prefix << 8) | b
        code = table.get(key)
        if code is not None:
            prefix = code
            continue
        acc, nb = flush_code(prefix, size, acc, nb)
        table[key] = nxt
        nxt += 1
        if nxt > (1 << size) and size < 12:
            size += 1
        if nxt >= 4095:
            acc, nb = flush_code(clear, size, acc, nb)
            table.clear()
            size, nxt = 9, 258
        prefix = b
    acc, nb = flush_code(prefix, size, acc, nb)
    acc, nb = flush_code(eoi, size, acc, nb)
    if nb:
        out.append(acc & 255)
    return bytes(out)


def write_gif(frames, path, delay_cs):
    """frames: Liste (h, w, 3) uint8, Zeile 0 oben. Eine globale Farbtabelle (genau, wenn es hoechstens 255 Farben sind)."""
    h, w = frames[0].shape[:2]
    flat = np.concatenate([f.reshape(-1, 3) for f in frames]).astype(np.uint8)
    keys = (flat[:, 0].astype(np.int64) << 16) | (flat[:, 1].astype(np.int64) << 8) | flat[:, 2]
    uniq = np.unique(keys)
    if len(uniq) <= 256:
        pal = np.stack([(uniq >> 16) & 255, (uniq >> 8) & 255, uniq & 255], 1).astype(np.uint8)
        idxs = [np.searchsorted(uniq, (f[..., 0].astype(np.int64) << 16 | f[..., 1].astype(np.int64) << 8 | f[..., 2]).ravel()) for f in frames]
    else:
        pal = kmeans_palette(flat, 256)
        idxs = []
        for f in frames:
            px = f.reshape(-1, 3).astype(np.int32)
            res = np.zeros(len(px), np.int64)
            for s in range(0, len(px), 20000):
                chunk = px[s:s + 20000]
                res[s:s + 20000] = ((chunk[:, None, :] - pal[None, :, :].astype(np.int32)) ** 2).sum(-1).argmin(1)
            idxs.append(res)
    table = np.zeros((256, 3), np.uint8)
    table[:len(pal)] = pal
    out = bytearray(b"GIF89a")
    out += w.to_bytes(2, "little") + h.to_bytes(2, "little") + bytes((0xF7, 0, 0)) + table.tobytes()
    out += b"\x21\xff\x0bNETSCAPE2.0\x03\x01\x00\x00\x00"
    for ix in idxs:
        out += b"\x21\xf9\x04" + bytes((0x04,)) + delay_cs.to_bytes(2, "little") + b"\x00\x00"
        out += b"\x2c" + (0).to_bytes(2, "little") * 2 + w.to_bytes(2, "little") + h.to_bytes(2, "little") + b"\x00"
        data = lzw(ix)
        out.append(8)
        for s in range(0, len(data), 255):
            blk = data[s:s + 255]
            out.append(len(blk))
            out += blk
        out.append(0)
    out.append(0x3B)
    open(path, "wb").write(bytes(out))


# ---------------------------------------------------------------- Beschriftung

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
    save_png(np.asarray(img, float)[::-1], path)


def fit(img, tw, th):
    xi = (np.arange(tw) * img.shape[1] / tw).astype(int)
    yi = (np.arange(th) * img.shape[0] / th).astype(int)
    return img[yi][:, xi]


# ---------------------------------------------------------------- Massstab

FLAT = {"cloth": (0, 0, 2, 2), "skin": (2, 0, 4, 2), "wood": (4, 0, 6, 2), "ra": (0, 2, 2, 4), "rb": (2, 2, 4, 4)}


def flat_atlas():
    arr = np.zeros((8, 8, 3))
    cols = {"cloth": RAMPS["Stoff"][3], "skin": RAMPS["Haut/Knochen"][6], "wood": RAMPS["Holz"][3],
            "ra": RAMPS["Stein"][9], "rb": RAMPS["Stein"][5]}
    for k, (x0, y0, x1, y1) in FLAT.items():
        arr[y0:y1, x0:x1] = cols[k]
    return arr


def scale_image(state, w_=1736, h_=520, px=170.0):
    coll = new_scene()
    fl = rects_from_pixels(FLAT, 8)
    image = save_image("flat", flat_atlas(), os.path.join(TMP, "flat.png"))
    mat = pass_material("flat", image)

    def put(part, x):
        o = part.build(coll, mat, smooth=False)
        o.matrix_world = Matrix.Translation(G(V(-x, 0, 0)))
        return o

    objs = []
    p = Part("ruler", (0, 0, 0), fl)
    k, y = 0, 0.0
    while y < 2.5 - 1e-6:
        p.box((-0.04, y, -0.04), (0.04, y + 0.5, 0.04), "ra" if k % 2 == 0 else "rb")
        y, k = y + 0.5, k + 1
    objs.append(put(p, 0.25))
    p = Part("mannequin", (0, 0, 0), fl)
    for sx in (-1, 1):
        p.box((sx * 0.20 - 0.09, 0.0, -0.10), (sx * 0.20 + 0.09, 0.85, 0.10), "cloth")
        p.box((sx * 0.33 - 0.06, 0.85, -0.07), (sx * 0.33 + 0.06, 1.42, 0.07), "cloth")
    p.box((-0.28, 0.85, -0.13), (0.28, 1.48, 0.13), "cloth")
    p.box((-0.10, 1.48, -0.10), (0.10, 1.80, 0.10), "skin")
    objs.append(put(p, 1.0))
    p = Part("chest", (0, 0, 0), fl)
    p.box((-0.5, 0.0, -0.35), (0.5, 0.455, 0.35), "wood")
    p.box((-0.5, 0.455, -0.35), (0.5, 0.70, 0.35), "wood")
    objs.append(put(p, 2.1))
    xs = {"A1": 3.7, "A2": 5.8, "A3": 7.9}
    for key in VARIANTS:
        o, _ = add_fountain(coll, key, state, 0, offset=(-xs[key], 0.0, 0.0))
        objs += o
    for o in objs:
        o.matrix_world = o.matrix_world
    bpy.context.view_layer.update()
    for o in objs:
        write_light(o)
    add_floor = Part("floor", (0, 0, 0), {"all": (0.0, 0.0, 1.0, 1.0)})
    stone = stone_textures(TMP)
    add_floor.box((-11.0, -0.12, -1.0), (1.0, 0.0, 1.0), "all")
    fo = add_floor.build(coll, pass_material("floor", stone["floor"]), smooth=False)
    bpy.context.view_layer.update()
    write_light(fo)
    cam = camera()
    cx, cy = 5.1, 1.45
    img = shot(cam, G(V(-cx, cy, -20)), G(V(-cx, cy, 0)), 0, (w_, h_), "hell", ortho=h_ / px)
    return img, px, cx, cy


# ---------------------------------------------------------------- Hauptablauf

def main():
    info = {}
    # Modelle (Bild 0 der Wasseranimation), Dreiecke, Masse, Texturen
    for key, (name, fn, sub) in VARIANTS.items():
        for state in ("bereit", "verbraucht"):
            coll = new_scene()
            objs, _ = add_fountain(coll, key, state, 0, export=True)
            export_glb(objs, os.path.join(OUT, "brunnen_%s_%s.glb" % (key.lower(), state)))
            tri = sum(len(o.data.polygons) for o in objs)
            lo = np.array([min(v.co[i] for o in objs for v in o.data.vertices) for i in range(3)])
            hi = np.array([max(v.co[i] for o in objs for v in o.data.vertices) for i in range(3)])
            # Blender-Achsen (x, -z, y) -> Spiel-Achsen
            size = (hi[0] - lo[0], hi[2] - lo[2], hi[1] - lo[1])
            info[(key, state)] = (tri, size)
            print("%s %s: %d Dreiecke, Mass %.2f x %.2f x %.2f m (x, y, z), %d Farben" % (key, state, tri, *size, NCOL[key]))
    # Texturen vergroessert (Beleg)
    sheet = np.zeros((6 * 64 + 60, 3 * 6 * 64 + 40, 3)) + BG
    for c, key in enumerate(VARIANTS):
        big = np.repeat(np.repeat(ATLAS[key][::-1], 6, axis=0), 6, axis=1)
        sheet[50:50 + 384, 10 + c * 394:10 + c * 394 + 384] = big
        text(sheet, "%s: Textur 64 x 64, %d Farben" % (key, NCOL[key]), 10 + c * 394, 6, 22)
    png(sheet, os.path.join(OUT, "texturen.png"))
    # Ansichten je Ausfuehrung: vier Blickwinkel, bereit (oben) und verbraucht (unten)
    t = 380
    for key in VARIANTS:
        sh = np.zeros((2 * (t + 34) + 44, 4 * t + 3 * 6, 3)) + BG
        text(sh, "%s %s – vier Ansichten, bereit (oben) und verbraucht (unten); Platte = Zelle 2 x 2 m" % (key, VARIANTS[key][0]), 8, 4, 22)
        for r, state in enumerate(("bereit", "verbraucht")):
            for c, (vn, d) in enumerate(view_dirs()):
                img = bright_view(key, state, d, (t, t))
                x0, y0 = c * (t + 6), 44 + r * (t + 34)
                text(sh, "%s – %s" % (state, vn), x0 + 4, y0, 20)
                sh[y0 + 32:y0 + 32 + t, x0:x0 + t] = img
        png(sh, os.path.join(OUT, "ansichten_%s.png" % key.lower()))
    # Spielbedingungen
    game, hell = {}, {}
    for key in VARIANTS:
        for state in ("bereit", "verbraucht"):
            for d in (2, 4):
                img = game_view(key, state, d)
                png(img, os.path.join(OUT, "spiel_%s_%s_%dm.png" % (key.lower(), state, d)))
                game[(key, state, d)] = img
            hell[(key, state)] = bright_view(key, state, (-0.55, 0.28, -0.8), (560, 450), dist=4.2)
    # Wasser-Animation als GIF: vier Atlas-Bilder, 150 ms, saubere Schleife
    for key in VARIANTS:
        fr = [bright_view(key, "bereit", (-0.55, 0.28, -0.8), (480, 540), frame=k, dist=4.0).astype(np.uint8) for k in range(4)]
        write_gif(fr, os.path.join(OUT, "wasser_%s.gif" % key.lower()), 15)
        fs = [game_view(key, "bereit", 3.0, frame=k)[:, 187:667].astype(np.uint8) for k in range(4)]
        write_gif(fs, os.path.join(OUT, "wasser_%s_spiel.gif" % key.lower()), 15)
    scale_img, px, cx, cy = scale_image("bereit")
    png(scale_img, os.path.join(OUT, "massstab2.png"))
    blatt(game, hell, scale_img, px, cx, cy, info)


def blatt(game, hell, scale_img, px, cx, cy, info):
    tw, th, g = 560, 315, 14
    colw = tw + g
    width = 3 * colw + g
    bh = 450
    sheet_h = 120 + 90 + 2 * (bh + 34) + 3 * (th + 34) + 60 + 400 + 60 + 520 + 260
    sh = np.zeros((sheet_h, width, 3)) + BG
    text(sh, "Silver & Ash – Brunnen, Runde 2 (Issue #438): der Schalenbrunnen in drei Ausführungen", 18, 8, 40)
    text(sh, "Mit Textur (64 x 64, höchstens 32 Farben, nur Palette), Wasser animiert (vier Bilder), goldenes Funkeln der Beute, Moos in drei Mengen", 18, 62, 22, (210, 205, 220))
    text(sh, "Bilder dieser Zeilen: helle Ansicht ohne Laterne und Nebel (zum Beurteilen); die Spielbilder darunter mit Laterne, Nebel und Körnung", 18, 88, 22, (210, 205, 220))
    y = 130
    names = {"A1": "A1  Zweischaler", "A2": "A2  Dreischaler", "A3": "A3  Figurenbrunnen"}
    moss = {"A1": "Moos: wenig", "A2": "Moos: stark", "A3": "Moos: keines"}
    for c, key in enumerate(VARIANTS):
        x0 = g + c * colw
        text(sh, names[key], x0, y - 10, 44, (240, 232, 210))
        tri, size = info[(key, "bereit")]
        text(sh, VARIANTS[key][2], x0, y + 44, 20, (200, 195, 210))
        text(sh, "%.2f x %.2f x %.2f m, %d Dreiecke (leer: %d)" % (*size, tri, info[(key, "verbraucht")][0]), x0, y + 68, 20, (200, 195, 210))
    y += 100
    for label, state in (("bereit (Wasser fließt, Beute funkelt)", "bereit"), ("verbraucht (Höchstzahl an Würfen erreicht, trocken)", "verbraucht")):
        for c, key in enumerate(VARIANTS):
            x0 = g + c * colw
            text(sh, label, x0, y, 22)
            sh[y + 34:y + 34 + bh, x0:x0 + tw] = hell[(key, state)]
        y += bh + 34
    for label, state, d in (("bereit, aus 4 m im Spiel", "bereit", 4), ("verbraucht, aus 4 m im Spiel", "verbraucht", 4), ("bereit, aus 2 m im Spiel", "bereit", 2)):
        for c, key in enumerate(VARIANTS):
            x0 = g + c * colw
            text(sh, label, x0, y, 22)
            sh[y + 34:y + 34 + th, x0:x0 + tw] = fit(game[(key, state, d)], tw, th)
        y += th + 34
    y += 10
    text(sh, "Die Texturen, sechsfach vergrößert (Moos = Haut/Knochen 1 bis 3, Wasser = Stoff, Funkeln = Messing/Gold)", 18, y, 26)
    y += 40
    for c, key in enumerate(VARIANTS):
        big = np.repeat(np.repeat(ATLAS[key][::-1], 5, axis=0), 5, axis=1)
        x0 = g + c * colw + (tw - 320) // 2
        sh[y:y + 320, x0:x0 + 320] = big
    y += 340
    text(sh, "Maßstab: Meterstab (0,5-m-Teilung), Figur 1,8 m, Kiste (Worn) aus dem Spiel, dann A1, A2, A3 (bereit)", 18, y, 26)
    y += 40
    sh[y:y + 520, 0:scale_img.shape[1]] = scale_img[:, :width]
    for pos, lab in ((0.25, "Stab 2,5 m"), (1.0, "Figur 1,8 m"), (2.1, "Kiste 0,7 m"), (3.7, "A1 1,90 m"), (5.8, "A2 2,30 m"), (7.9, "A3 2,22 m")):
        sx = int((pos - cx) * px + scale_img.shape[1] / 2) - 55
        text(sh, lab, max(0, sx + 20), y + 488, 20, (240, 235, 245))
    y += 540
    text(sh, RECOMMEND[0], 18, y, 34, (240, 232, 210))
    for i, line in enumerate(RECOMMEND[1:]):
        text(sh, line, 18, y + 52 + i * 32, 22, (225, 220, 232))
    png(sh[:y + 52 + 32 * len(RECOMMEND[1:]) + 40], os.path.join(OUT, "blatt2.png"))


RECOMMEND = (
    "Empfehlung: A2 Dreischaler",
    "Die gestuften Schalen geben aus 4 m im Nebel die unverwechselbarste Silhouette, und das Wasser blitzt auf jeder Stufe; das kräftige Moos erzählt Alter.",
    "Will man weniger Moos, ist A1 die ruhige Wahl (schlichter, wenig Moos). A3 ist am schmucklosesten und liest sich ohne Moos als Denkmal.",
    "Nur eine Einschätzung des Grafikdesigners; Form und Moosmenge entscheidet der Mensch.",
)

main()
