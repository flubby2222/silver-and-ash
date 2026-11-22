"""Tiefe Wandnische (Issue #553, Teil 1): spitzbogige Nische, 0,3 m tief in die Wand gesetzt, mit und ohne Steinbord.

Erzeuger fuer das Spiel. Uebergabe aus dem abgenommenen Probestueck design/proben/grafik/wandnische-tief-538/wandnische_tief.py
(Urteil: Tiefe 0,3 m, Gewaende springt 1/16 vor, Bord "beides" - die Fassung mit Bord und die ohne wechseln sich ab). Gleiche
Geometrie und derselbe Atlas wie die Fassungen "t30" und "regal_t30" dort, ohne die Fuellungen (Flaschen, Kerzen, Schaedel;
Teil 2). Die Teile heissen frame (Gewaende), wall (Wandstueck mit Tasche), sill (Sohlbank) und shelf (Bord). Der Gewaendestein
ist die Wandkachel um 5 Stufen heller, linear in den oberen Rest gestaucht, wie bei der flachen Nische (wall_niche.niche_lift).

Aufruf (aus dem Repository-Wurzelverzeichnis), danach python tools/models/checksums.py:
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/models/wall_niche_deep.py

Alles per Skript, nichts heruntergeladen. Spiel-Achsen (x seitlich, y oben, z vorn), Meter. Ursprung: Mitte der Nische auf
dem Boden, Wandflaeche z = 0.

DER BAUSTEIN: Das Wandstueck ist 2 m breit (eine Zellwand) und 3 m hoch und ersetzt die Wandflaeche der Zelle vom Boden bis
3 m - das Spiel laesst dort ein Loch in der Wand (WallCutout, WallNicheLook.Cutout). Es reicht D + 1/16 hinter die Wandebene
und enthaelt eine echte Tasche: Vorderflaeche mit Oeffnung in Welt-UV (eine Kachel je Meter, wie die Wand), Laibung, Rueckwand
der Tasche (Wandkachel 8 Stufen dunkler, am Rand harte Zusatzstufen). Davor das Gewaende (Ring um die Oeffnung, 1/16 vor der
Wand), die Sohlbank und in der Fassung mit Bord ein Steinbord in der Tasche. Die Zelle dahinter bleibt fest.

Masse: Oeffnung 1,0 breit, gedrueckter Spitzbogen R = 13/16, Kaempfer 1,25, Scheitel 2,0; Gewaendeband 1/8; Sohlbank 0,25
bis 0,5; Bord 0,875 bis 1,0.

Dateien: wall_niche_deep_<v>.glb und wall_niche_shelf_<v>.glb mit .png, v die Wandvariante a, b, c. Jedes Netz rundum
geschlossen. Atlas 256 x 240. Die Mitte der Nische steht auf der Mitte einer Zellwand (ganze Meterzahl), dann liegen
Vorderflaeche und Rueckwand in Weltphase der Wand. Aendert sich eine Wand-Textur oder die Palette, passt die Nische nicht
mehr; ein Test vergleicht wall_niche_deep_sources.sha256 mit assets/textures/textures.sha256. Zwei Laeufe liefern gleiche
Dateien (kein Zufall).
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bmesh  # noqa: E402
import bpy  # noqa: E402
from psx import V, G, Part, rects_from_pixels, save_image, export_material, export_glb, report_parts  # noqa: E402
from pillar_kit import (OUT_DIR, TOOLS, WALL_FILES, colours_of, edge_report, emit, load_tile, raw,  # noqa: E402
                        stein_row, steps_of, write_sources)

SOURCES = os.path.join(TOOLS, "wall_niche_deep_sources.sha256")
NICHE_LIFT = 5            # Stein-Stufen heller als die Wand, wie die flache Nische
S = 0.0625

# --- Masse (wie im Probestueck) ---------------------------------------------------------------------------------------
D = 0.30                  # Tiefe der Tasche hinter der Wandebene
PR = S                    # Vorsprung des Gewaendes vor der Wand
ZS = PR + S               # Vorderkante der Sohlbank
HS = 3.0                  # Hoehe des Wandstuecks; darueber laeuft die gewoehnliche Wandflaeche weiter
HALF = 1.0                # halbe Breite des Wandstuecks (eine Zellwand)
WB = 0.125                # Breite des Gewaendebands um die Oeffnung
HW = 0.5 + WB             # halbe Breite des Gewaendes aussen
Y_BOT = 0.4375            # Fuss der Gewaendepfosten: 1/16 in der Sohlbank
Y_SIL0, Y_SIL1 = 0.25, 0.5
FLOOR_T = 0.0625          # Dicke des Wandstuecks hinter der Taschenrueckwand
SHELF_Y0, SHELF_Y1 = 0.875, 1.0
SHELF_HALF = 0.5 + 1.0 / 32.0
FORM = dict(R=0.8125, CXA=0.3125, YK=1.25, steps=[0.0, 16.5, 33.0, 49.5])   # gedrueckter Spitzbogen, Scheitel 2,0 m
NB = 7
FORMS = {"deep": ("frame", "wall", "sill"), "shelf": ("frame", "wall", "sill", "shelf")}
TRIANGLES = {"deep": 322, "shelf": 342}

# --- Atlas -----------------------------------------------------------------------------------------------------------
ATLAS_SIZE = (256, 240)
BW, BH = 18, 44
SW_, SH_ = 96, 64
PIX = {"stone": (0, 0, 64, 64), "pillar": (64, 0, 128, 64), "plate": (128, 0, 192, 96),
       "SG": (0, 64, 96, 128), "BD": (0, 128, 96, 192)}
for _j in range(NB):
    PIX["G%d" % _j] = (BW * _j, 192, BW * _j + BW, 192 + BH)
UV = rects_from_pixels(PIX, ATLAS_SIZE)


def tile_idx(rgb, stein):
    return steps_of(rgb, stein).reshape(rgb.shape[:2])


def niche_lift(tile, stein):
    """Kachel um NICHE_LIFT Stufen heller, die Stufen oben linear zusammengedraengt (wie wall_niche.py)."""
    flat = tile.reshape(-1, 3)
    colours = sorted({tuple(int(c) for c in p) for p in flat})
    idx = {c: int(((stein - np.array(c)) ** 2).sum(1).argmin()) for c in colours}
    lo, hi, top = min(idx.values()), max(idx.values()), len(stein) - 1
    new_lo = min(max(lo + NICHE_LIFT, 0), top)
    span = max(hi - lo, 1)
    mapped = {c: int(round(new_lo + (i - lo) * (top - new_lo) / span)) for c, i in idx.items()}
    return np.array([stein[mapped[tuple(int(c) for c in p)]] for p in flat]).reshape(tile.shape)


def clean_stone(lift_i, mask):
    """Gewaendestein ohne die Fugen der Wandkachel: Fugenpixel durch den naechsten Steinpixel rechts daneben ersetzt."""
    out, m = lift_i.copy(), mask.copy()
    for k in range(1, 14):
        take = m & ~np.roll(mask, -k, 1)
        out[take] = np.roll(lift_i, -k, 1)[take]
        m &= ~take
    out[m] = int(np.median(lift_i))
    return out


def ring_profile():
    """Querschnitt des Gewaendes (d, z): d = Abstand von der Oeffnungskante nach aussen, z = vor (+) / hinter (-) der Wand.
    Rueckgabe: Punkte, Zahl sichtbarer Kanten."""
    return [(0.0, -D), (0.0, PR - S), (S, PR), (WB, PR), (WB, 0.0), (WB, -D)], 4


def ring_s():
    p, nvis = ring_profile()
    s = [0.0] * len(p)
    for i in range(1, nvis + 1):
        s[i] = s[i - 1] + math.hypot(p[i][0] - p[i - 1][0], p[i][1] - p[i - 1][1])
    for i in range(nvis + 1, len(p)):
        s[i] = s[nvis]
    return s


def bar_pts(y0, y1, zf, zb, ch):
    return {"A": (zb, y0), "B": (zf - ch, y0), "C": (zf, y0 + ch), "D": (zf, y1 - ch), "E": (zf - ch, y1), "F": (zb, y1)}


def bar_s(p):
    d = lambda a, b: math.hypot(p[a][0] - p[b][0], p[a][1] - p[b][1])   # noqa: E731
    sD = d("E", "D")
    sC = sD + d("D", "C")
    sB = sC + d("C", "B")
    return {"F": -(p["E"][0] - p["F"][0]), "E": 0.0, "D": sD, "C": sC, "B": sB, "A": sB}


def sill_geo():
    return bar_pts(Y_SIL0, Y_SIL1, ZS, -D, S)


def shelf_geo():
    return bar_pts(SHELF_Y0, SHELF_Y1, PR - S, -D, S / 2)


def bar_start(p):
    return bar_s(p)["F"] - 2.0 / 64.0


def edge_dist_stage(X, Y):
    """Zusatz-Dunkelstufe der Taschenrueckwand nach dem Abstand zum Rand der Oeffnung (0 bis 3)."""
    F = FORM
    inside = F["R"] ** 2 - (abs(X) - F["CXA"]) ** 2
    top = F["YK"] + math.sqrt(max(inside, 0.0))
    d = min(0.5 - abs(X), (top - Y) * 0.9)
    return 3 if d < 1 / 16 else 2 if d < 1 / 8 else 1 if d < 3 / 16 else 0


def paint_bar(idx, name, p, clean, stein):
    cs = bar_s(p)
    sv = bar_start(p) + (np.arange(SH_) + 0.5) / 64.0
    dl = np.zeros(SH_, int)
    for lo, hi, d in ((-1e9, cs["E"], 0), (cs["E"], cs["D"], 0), (cs["D"], cs["C"], -1), (cs["C"], cs["B"], -1)):
        dl[(sv >= lo) & (sv < hi)] = d
    q = np.roll(clean, (-23, -7), (0, 1))
    q = np.concatenate([q, q], 1)[:SH_, :SW_].copy()
    q = np.clip(q + dl[:, None], 1, len(stein) - 1)
    x0, y0, x1, y1 = PIX[name]
    idx[y0:y1, x0:x1] = q


def atlas(variant, stein):
    """Atlas der Variante: Wandkachel, aufgehellter Stein, Taschenrueckwand, Gewaendesteine, Sohlbank, Bord."""
    tile = load_tile(WALL_FILES[variant])
    lifted = niche_lift(tile, stein)
    t_i, l_i = tile_idx(tile, stein), tile_idx(lifted, stein)
    lo = int(t_i.min())
    mask = t_i <= lo + 2
    clean = clean_stone(l_i, mask)
    mortar = min(lo + NICHE_LIFT - 2, len(stein) - 1)
    h, w = ATLAS_SIZE[1], ATLAS_SIZE[0]
    idx = np.full((h, w), mortar, int)

    # Taschenrueckwand: Wandkachel in Weltphase, 8 Stufen dunkler, am Rand harte Zusatzstufen (Tiefe ueber Palettenstufen)
    x0, y0, x1, y1 = PIX["plate"]
    tb = t_i[::-1]
    plate = np.zeros((y1 - y0, x1 - x0), int)
    for py in range(y1 - y0):
        for px in range(x1 - x0):
            X, Y = -0.5 + (px + 0.5) / 64.0, 0.5 + (py + 0.5) / 64.0
            base = tb[int(math.floor(Y * 64)) % 64, int(math.floor(X * 64)) % 64]
            plate[py, px] = min(max(base - 8 - edge_dist_stage(X, Y), 1), len(stein) - 1)
    idx[y0:y1, x0:x1] = plate

    # Gewaendesteine: Laibung nach hinten stufenweise dunkler, Fase und Band hell, Aussenkante eine Stufe dunkler
    _, nvis = ring_profile()
    s = ring_s()
    sv = (np.arange(BH) + 0.5) / 64.0
    dl = np.zeros(BH, int)
    for r, sr in enumerate(sv):
        if sr < s[1]:
            t = sr / max(s[1], 1e-9)
            dl[r] = -3 if t < 0.34 else -2 if t < 0.67 else -1
        elif sr < s[3]:
            dl[r] = 0
        elif sr < s[nvis]:
            dl[r] = -1
    for j in range(NB):
        oy, ox = (11 * j + 5) % 64, (17 * j + 9) % 64
        q = np.roll(clean, (-oy, -ox), (0, 1))[:BH, :BW].copy()
        if j % 2 == 1:
            q = q - 1
        q = np.clip(q + dl[:, None], 1, len(stein) - 1)
        q[:, :2] = mortar
        if j < NB - 1:
            q[:, -1] = mortar
        end = int(round(s[nvis] * 64.0))
        if end < BH:
            q[max(end - 1, 0):end + 1, :] = mortar       # Fuge zwischen Gewaendeband und Wand
        x0, y0, x1, y1 = PIX["G%d" % j]
        idx[y0:y1, x0:x1] = q
    paint_bar(idx, "SG", sill_geo(), clean, stein)
    paint_bar(idx, "BD", shelf_geo(), clean, stein)

    out = stein[idx].astype(float)
    out[0:64, 0:64] = tile[::-1]
    out[0:64, 64:128] = lifted[::-1]
    return out


def check_palette(name, tex, stein):
    row = {tuple(int(c) for c in p) for p in stein}
    cols = {tuple(int(c) for c in p) for p in np.round(tex).reshape(-1, 3)}
    assert not cols - row, f"{name}: {len(cols - row)} Farben ausserhalb der Reihe Stein"


# --- Geometrie -------------------------------------------------------------------------------------------------------

def stations():
    F = FORM
    st = [("post", Y_BOT)]
    y = 0.75
    while y < F["YK"] - 1e-9:
        st.append(("post", y))
        y += 0.25
    st.append(("post", F["YK"]))
    for a in F["steps"][1:]:
        st.append(("arc", math.radians(a)))
    st.append(("apex", None))
    assert len(st) == NB + 1
    return st


def pos(s, d, z):
    """Punkt der linken Haelfte (x <= 0) an Station s, Abstand d von der Oeffnungskante, Lage z vor der Wand."""
    F = FORM
    kind, val = s
    if kind == "post":
        return (-(F["R"] - F["CXA"] + d), val, z)
    r = F["R"] + d
    if kind == "arc":
        return (F["CXA"] - r * math.cos(val), F["YK"] + r * math.sin(val), z)
    phi = math.acos(F["CXA"] / r)
    return (0.0, F["YK"] + r * math.sin(phi), z)


def uvp(rect, u, v):
    u0, v0, u1, v1 = UV[rect]
    return (u0 + (u1 - u0) * u, v0 + (v1 - v0) * v)


class Welder:
    def __init__(self, part):
        self.part, self.verts = part, {}

    def __call__(self, p):
        key = tuple(int(round(c * 1e5)) for c in p)
        if key not in self.verts:
            self.verts[key] = self.part.bm.verts.new(G(V(*p)))
        return self.verts[key]


def build_frame():
    part = Part("frame", (0, 0, 0), UV)
    weld = Welder(part)
    p, nvis = ring_profile()
    s = ring_s()
    n = len(p)
    span = BH / 64.0
    vv = [si / span for si in s]
    assert s[nvis] <= span, "Gewaendeprofil laenger als das Steinfeld"
    ev = [(vv[i], vv[i + 1]) if i < nvis else (vv[nvis], vv[nvis]) for i in range(n)]
    st = stations()
    rings = [[pos(q, d, z) for d, z in p] for q in st]
    for sgn in (-1, 1):
        for k in range(NB):
            rect = "G%d" % k
            a, b = rings[k], rings[k + 1]
            for e in range(n):
                g = (e + 1) % n
                vs = [weld((q[0] if sgn < 0 else -q[0], q[1], q[2])) for q in (a[e], a[g], b[g], b[e])]
                v0, v1 = ev[e] if e < n - 1 else (vv[nvis], vv[nvis])
                part._face(vs, [uvp(rect, 0.0, v0), uvp(rect, 0.0, v1), uvp(rect, 1.0, v1), uvp(rect, 1.0, v0)])
        cap = rings[0]
        part._face([weld((q[0] if sgn < 0 else -q[0], q[1], q[2])) for q in cap], [uvp("G0", 0.5, 0.5)] * n)
    bmesh.ops.recalc_face_normals(part.bm, faces=part.bm.faces[:])
    return part


def build_bar(name, geo, rect, x_half):
    part = Part(name, (0, 0, 0), UV)
    cs = bar_s(geo)
    order = ["A", "B", "C", "D", "E", "F"]
    ls = bar_start(geo)
    span = SH_ / 64.0
    vv = {k: (cs[k] - ls) / span for k in order}
    assert 0.0 <= min(vv.values()) and max(vv.values()) <= 1.0, (name, vv)
    cols = {}
    for x in (-x_half, x_half):
        cols[x] = [part.bm.verts.new(G(V(x, geo[k][1], geo[k][0]))) for k in order]
    xl, xr = -x_half, x_half
    for i, k in enumerate(order):
        j = (i + 1) % 6
        va, vb = vv[k], vv[order[j]]
        if k == "A":
            va = vb
        if k == "F":
            vb = va
        part._face([cols[xl][i], cols[xr][i], cols[xr][j], cols[xl][j]],
                   [uvp(rect, 0, va), uvp(rect, 1, va), uvp(rect, 1, vb), uvp(rect, 0, vb)])
    for x, u in ((xl, 0.0), (xr, 1.0)):
        part._face(list(cols[x]), [uvp(rect, u, vv[k]) for k in order])
    bmesh.ops.recalc_face_normals(part.bm, faces=part.bm.faces[:])
    return part


def oriented_face(part, pts, uvs, hint):
    """Flaeche mit gegebenen Spiel-Ecken und UVs; Normale zeigt zu hint."""
    P = [V(*p) for p in pts]
    n = V(0, 0, 0)
    for i in range(len(P)):
        a, b = P[i], P[(i + 1) % len(P)]
        n += V((a.y - b.y) * (a.z + b.z), (a.z - b.z) * (a.x + b.x), (a.x - b.x) * (a.y + b.y))
    uvs = list(uvs)
    if n.dot(V(*hint)) < 0:
        P.reverse()
        uvs.reverse()
    verts = [part.bm.verts.new(G(p - part.pivot)) for p in P]
    part._face(verts, uvs)


def outline(left_d):
    st = stations()
    left = [pos(q, left_d, 0.0)[:2] for q in st]
    right = [(-x, y) for x, y in reversed(left[:-1])]
    return left, left + right


def build_wall():
    """Das Wandstueck: Vorderflaeche mit Oeffnung (Welt-UV), verdeckte Seiten und Rueckseite, Laibung und Taschenrueckwand."""
    zb = -(D + FLOOR_T)
    part = Part("wall", (0, 0, 0), UV)
    left, loop = outline(WB)
    n = len(left)
    ys = [q[1] for q in left]
    xs = [q[0] for q in left]
    # Vorderflaeche z = 0 mit Oeffnung, in lauter konvexe Stuecke zerlegt (Teilung an den Metergrenzen in emit)
    emit(part, [(-HALF, 0, 0), (HALF, 0, 0), (HALF, Y_BOT, 0), (HW, Y_BOT, 0), (-HW, Y_BOT, 0), (-HALF, Y_BOT, 0)], (0, 0, 1),
         "stone")
    col_l = [(-HALF, Y_BOT, 0)] + [(xs[i], ys[i], 0) for i in range(0, 4)] + [(-HW, HS, 0), (-HALF, HS, 0)]
    col_r = [(HALF, Y_BOT, 0)] + [(-xs[i], ys[i], 0) for i in range(0, 4)] + [(HW, HS, 0), (HALF, HS, 0)]
    emit(part, col_l, (0, 0, 1), "stone")
    emit(part, col_r, (0, 0, 1), "stone")
    for i in range(3, n - 1):
        for sg in (-1, 1):
            a, b = (sg * xs[i], ys[i]), (sg * xs[i + 1], ys[i + 1])
            emit(part, [(a[0], a[1], 0), (b[0], b[1], 0), (b[0], HS, 0), (a[0], HS, 0)], (0, 0, 1), "stone")
    # verdeckte Flaechen: Seiten, Oberseite, Unterseite, Rueckseite (raw fuegt T-Stoesse ein)
    for sx in (-HALF, HALF):
        raw(part, [(sx, 0, 0), (sx, 0, zb), (sx, HS, zb), (sx, HS, 0)], (sx, 0, 0), "stone")
    raw(part, [(-HALF, HS, 0), (HALF, HS, 0), (HALF, HS, zb), (-HALF, HS, zb)], (0, 1, 0), "stone")
    raw(part, [(-HALF, 0, 0), (HALF, 0, 0), (HALF, 0, zb), (-HALF, 0, zb)], (0, -1, 0), "stone")
    raw(part, [(-HALF, 0, zb), (HALF, 0, zb), (HALF, HS, zb), (-HALF, HS, zb)], (0, 0, -1), "stone")
    # Tasche: Laibung und Rueckwand (Plattenfeld)
    cx = sum(q[0] for q in loop) / len(loop)
    cy = sum(q[1] for q in loop) / len(loop)
    for i in range(len(loop)):
        a, b = loop[i], loop[(i + 1) % len(loop)]
        mx, my = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
        raw(part, [(a[0], a[1], 0), (a[0], a[1], -D), (b[0], b[1], -D), (b[0], b[1], 0)], (cx - mx, cy - my, 0), "stone")
    floor_uv = [uvp("plate", min(max(x + 0.5, 0.0), 1.0), min(max((y - 0.5) / 1.5, 0.0), 1.0)) for x, y in loop]
    oriented_face(part, [(x, y, -D) for x, y in loop], floor_uv, (0, 0, 1))
    bmesh.ops.recalc_face_normals(part.bm, faces=part.bm.faces[:])
    return part


def build_parts(form):
    parts = [build_frame(), build_wall(), build_bar("sill", sill_geo(), "SG", HW)]
    if form == "shelf":
        parts.append(build_bar("shelf", shelf_geo(), "BD", SHELF_HALF))
    return parts


def game(co):
    return (co.x, co.z, -co.y)


def assert_raster(part):
    """Fugenraster: jede Eckpunkt-UV der Vorderflaeche entspricht der Weltlage (Kachel 1 m), wenn die Mitte auf einem Meter steht."""
    u0, v0, u1, v1 = UV["stone"]
    uv = part.bm.loops.layers.uv.active
    checked = 0
    for f in part.bm.faces:
        if all(abs(game(v.co)[2]) < 1e-6 for v in f.verts):
            for loop in f.loops:
                x, y, _ = game(loop.vert.co)
                uu, vv = (loop[uv].uv[0] - u0) / (u1 - u0), (loop[uv].uv[1] - v0) / (v1 - v0)
                assert abs((x - uu) - round(x - uu)) < 1e-4 and abs((y - vv) - round(y - vv)) < 1e-4, (x, y, uu, vv)
                checked += 1
    assert checked > 20, checked
    return checked


def bounds(parts):
    pts = [game(v.co) for p in parts for v in p.bm.verts]
    return [min(q[i] for q in pts) for i in range(3)], [max(q[i] for q in pts) for i in range(3)]


def triangles(part):
    return sum(len(f.verts) - 2 for f in part.bm.faces)


# --- Ausgabe ------------------------------------------------------------------------------------------------------

def write(form, variant, stein):
    name = f"wall_niche_{form}_{variant}"
    bpy.ops.wm.read_factory_settings(use_empty=True)
    coll = bpy.data.collections.new("Niche")
    bpy.context.scene.collection.children.link(coll)
    tex = atlas(variant, stein)
    check_palette(name, tex, stein)
    image = save_image(name, tex, os.path.join(OUT_DIR, name + ".png"))
    material = export_material(name, image)
    parts = build_parts(form)
    lo, hi = bounds(parts)
    assert abs(lo[0] + HALF) < 1e-6 and abs(hi[0] - HALF) < 1e-6, f"{name}: Breite {lo[0]:.4f} bis {hi[0]:.4f}"
    assert abs(lo[1]) < 1e-6 and abs(hi[1] - HS) < 1e-6, f"{name}: Hoehe {lo[1]:.4f} bis {hi[1]:.4f}"
    raster = assert_raster(next(p for p in parts if p.name == "wall"))
    objs, total = [], 0
    for part in parts:
        open_edges, multi = edge_report(part)
        assert open_edges == 0 and multi == 0, f"{name} {part.name}: {open_edges} offene Kanten, {multi} mehrfach"
        total += triangles(part)
        objs.append(part.build(coll, material, smooth=False))
    assert tuple(o.name for o in objs) == FORMS[form]
    assert total == TRIANGLES[form], f"{name}: {total} Dreiecke statt {TRIANGLES[form]} wie im Probestueck"
    export_glb(objs, os.path.join(OUT_DIR, name + ".glb"))
    report_parts(name, objs)
    print(f"TEXTUR {name} {ATLAS_SIZE[0]}x{ATLAS_SIZE[1]} Farben {colours_of(tex)}, {total} Dreiecke, "
          f"z {lo[2]:.4f} bis {hi[2]:.4f}, Raster {raster}")


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    stein = stein_row()
    for form in FORMS:
        for variant in WALL_FILES:
            write(form, variant, stein)
    write_sources(SOURCES, "tools/models/wall_niche_deep.py", "deep wall niches")


main()
