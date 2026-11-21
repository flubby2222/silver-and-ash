"""Probestueck Wandnische mit echter Tiefe (Issue #538), dazu Regal-Fassung und drei Fuellungen (Flaschen, Kerzen, Schaedel).

Alles entsteht aus diesem einen Skript: die Modelle (.glb), die Atlanten, alle Bilder. Die Vorlagen (Wandpfeiler #536,
Tuerrahmen) werden nur gelesen und ausgefuehrt, nicht veraendert. Gleiche Welt-UV (64 px je Meter), gleiche um LIFT Stufen
aufgehellte Wandkachel fuer den Gewaendestein, nur Farben der Hauptpalette.

Spiel-Achsen (x seitlich, y oben, z vorn; Meter). Ursprung: Mitte der Nische auf dem Boden, Wandflaeche z = 0.

DER BAUSTEIN ("Wandstueck"): Eine Platte 2 m breit (Zellbreite) und 3 m hoch, die die Wandflaeche der Zelle von Boden bis 3 m
ersetzt. Sie ist hinter der Wandebene D + 1/16 m dick (D = Tiefe der Nische: 0,15 oder 0,3 m) und enthaelt eine echte Tasche:
Vorderflaeche mit Oeffnung, Laibung, Rueckwand der Tasche. Dazu kommen als eigene Teile im selben Modell das Gewaende (Ring um die
Oeffnung), die Sohlbank und, in der Regal-Fassung, das Bord. Die Zelle bleibt fuer Bewegung und Schuesse gesperrt (2 m Block).

Aufruf (aus dem Repository-Wurzelverzeichnis; TEMP/TMP auf einen eigenen Ordner setzen):
    blender --background --python design/proben/grafik/wandnische-tief-538/wandnische_tief.py
Umgebungsvariablen: W538T_ONLY (Komma-Liste der Schritte), W538T_OUT (anderer Ausgabeordner, fuer den Zwei-Laeufe-Vergleich).
Kein Zufall: zwei Laeufe liefern gleiche Dateien.
"""

import math
import os
import tempfile

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
PILLAR_PY = os.path.join(ROOT, "design", "proben", "grafik", "wandpfeiler-536", "wandpfeiler.py")
FLAT_GLB = os.path.join(HERE, "vorlage-flach", "wandnische_spitz_gang_a.glb")

_src = open(PILLAR_PY, encoding="utf-8").read().rstrip()
assert _src.endswith("\nmain()"), "wandpfeiler.py endet nicht mehr mit main()"
W = {"__file__": PILLAR_PY, "__name__": "wandpfeiler_ns"}
exec(compile(_src[:-len("main()")], PILLAR_PY, "exec"), W)

bpy = W["bpy"]
bmesh = __import__("bmesh")
Matrix = W["Matrix"]
V, G, Part = W["V"], W["G"], W["Part"]
rects_from_pixels = W["rects_from_pixels"]
OUT = os.environ.get("W538T_OUT") or HERE
TMP = os.path.join(tempfile.gettempdir(), "wandnische538t_tmp")
os.makedirs(TMP, exist_ok=True)
W["TMP"] = TMP
WALL_FILES = W["WALL_FILES"]
LIFT = W["LIFT"]
S = 0.0625

# --- Masse ------------------------------------------------------------------------------------------------------------
HS = 3.0                  # Hoehe des Bausteins; darueber laeuft die gewoehnliche Wandflaeche weiter (Naht auf der Meterlinie)
WB = 0.125                # Breite des Gewaendebands um die Oeffnung
HW = 0.5 + WB             # halbe Breite des Gewaendes aussen
Y_BOT = 0.4375            # Fuss der Gewaendepfosten: 1/16 in der Sohlbank
Y_SIL0, Y_SIL1 = 0.25, 0.5   # Sohlbank, Oberseite = Sohle
FLOOR_T = 0.0625          # Dicke der Platte hinter der Taschenrueckwand
BORD_Y0, BORD_Y1 = 0.875, 1.0
FORM = dict(R=0.8125, CXA=0.3125, YK=1.25, steps=[0.0, 16.5, 33.0, 49.5])   # gedrueckter Spitzbogen, Scheitel 2,0 m
NB = 7
PLAYER_H = 1.8
TIEFEN = {"t15": 0.15, "t30": 0.30}
RAHMEN = os.environ.get("W538T_RAHMEN", "vor")      # "buendig" oder "vor" (Gewaende springt 1/16 vor)


def cfg_of(key, rahmen=None, regal=False):
    D = TIEFEN[key]
    pr = S if (rahmen or RAHMEN) == "vor" else 0.0
    return dict(key=key, D=D, pr=pr, zs=pr + S, regal=regal)


# --- Atlas ------------------------------------------------------------------------------------------------------------
ATLAS_SIZE = (256, 240)
BW, BH = 18, 44
SW_, SH_ = 96, 64
PIX = {"stone": (0, 0, 64, 64), "pillar": (64, 0, 128, 64), "plate": (128, 0, 192, 96),
       "SG": (0, 64, 96, 128), "BD": (0, 128, 96, 192)}
for _j in range(NB):
    PIX["G%d" % _j] = (BW * _j, 192, BW * _j + BW, 192 + BH)
UV = rects_from_pixels(PIX, ATLAS_SIZE)
W["FRAME_UV"] = UV


def stein():
    return W["stein_row"]()


def tile_idx(rgb, st):
    flat = rgb.reshape(-1, 3)
    lut = {c: int(((st - np.array(c)) ** 2).sum(1).argmin()) for c in {tuple(int(x) for x in p) for p in flat}}
    return np.array([lut[tuple(int(x) for x in p)] for p in flat]).reshape(rgb.shape[:2])


def clean_stone(lift_i, mask):
    out, m = lift_i.copy(), mask.copy()
    for k in range(1, 14):
        take = m & ~np.roll(mask, -k, 1)
        out[take] = np.roll(lift_i, -k, 1)[take]
        m &= ~take
    out[m] = int(np.median(lift_i))
    return out


def ring_profile(c):
    """Querschnitt des Gewaendes (d, z): d = Abstand von der Oeffnungskante nach aussen, z = Lage vor (+) / hinter (-) der Wand.
    Rueckgabe: Punkte, Zahl sichtbarer Kanten."""
    D, pr = c["D"], c["pr"]
    p = [(0.0, -D), (0.0, pr - S), (S, pr), (WB, pr)]
    if pr > 1e-9:
        p.append((WB, 0.0))
    p.append((WB, -D))
    return p, 3 + (1 if pr > 1e-9 else 0)


def ring_s(c):
    p, nvis = ring_profile(c)
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


def sill_geo(c):
    return bar_pts(Y_SIL0, Y_SIL1, c["zs"], -c["D"], S)


def bord_geo(c):
    return bar_pts(BORD_Y0, BORD_Y1, c["pr"] - S, -c["D"], S / 2)


def bar_start(p):
    return bar_s(p)["F"] - 2.0 / 64.0


def edge_dist_stage(X, Y):
    F = FORM
    inside = F["R"] ** 2 - (abs(X) - F["CXA"]) ** 2
    top = F["YK"] + math.sqrt(max(inside, 0.0))
    d = min(0.5 - abs(X), (top - Y) * 0.9)
    return 3 if d < 1 / 16 else 2 if d < 1 / 8 else 1 if d < 3 / 16 else 0


def paint_bar(idx, name, p, clean, st):
    cs = bar_s(p)
    ls = bar_start(p)
    sv = ls + (np.arange(SH_) + 0.5) / 64.0
    dl = np.zeros(SH_, int)
    for lo, hi, d in ((-1e9, cs["E"], 0), (cs["E"], cs["D"], 0), (cs["D"], cs["C"], -1), (cs["C"], cs["B"], -1)):
        dl[(sv >= lo) & (sv < hi)] = d
    q = np.roll(clean, (-23, -7), (0, 1))
    q = np.concatenate([q, q], 1)[:SH_, :SW_].copy()
    q = np.clip(q + dl[:, None], 1, len(st) - 1)
    x0, y0, x1, y1 = PIX[name]
    idx[y0:y1, x0:x1] = q


def my_atlas(variant, c):
    st = stein()
    tile = W["load_tile"](WALL_FILES[variant])
    lifted = W["pillar_tile"](tile, st)
    t_i, l_i = tile_idx(tile, st), tile_idx(lifted, st)
    lo = int(t_i.min())
    mask = t_i <= lo + 2
    clean = clean_stone(l_i, mask)
    mortar = min(lo + LIFT - 2, len(st) - 1)
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
            plate[py, px] = min(max(base - 8 - edge_dist_stage(X, Y), 1), len(st) - 1)
    idx[y0:y1, x0:x1] = plate

    # Gewaendesteine (Laibung stufenweise dunkler nach hinten, Fase und Band hell, Aussenkante eine Stufe dunkler)
    p, nvis = ring_profile(c)
    s = ring_s(c)
    sv = (np.arange(BH) + 0.5) / 64.0
    dl = np.zeros(BH, int)
    L = s[1]
    for r, sr in enumerate(sv):
        if sr < s[1]:
            t = sr / max(L, 1e-9)
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
        q = np.clip(q + dl[:, None], 1, len(st) - 1)
        q[:, :2] = mortar
        if j < NB - 1:
            q[:, -1] = mortar
        end = int(round(s[nvis] * 64.0))
        if end < BH:
            q[max(end - 1, 0):end + 1, :] = mortar       # Fuge zwischen Gewaendeband und Wand
        x0, y0, x1, y1 = PIX["G%d" % j]
        idx[y0:y1, x0:x1] = q
    paint_bar(idx, "SG", sill_geo(c), clean, st)
    paint_bar(idx, "BD", bord_geo(c), clean, st)

    out = st[idx].astype(float)
    out[0:64, 0:64] = tile[::-1]
    out[0:64, 64:128] = lifted[::-1]
    return out


def palette_check(tex, rows):
    allowed = {tuple(int(x) for x in p) for r in rows for p in r}
    cols = {tuple(int(x) for x in p) for p in np.round(tex).reshape(-1, 3)}
    return len(cols), len(cols - allowed)


def all_rows():
    import json
    with open(os.path.join(ROOT, "assets", "palette", "hauptpalette.json"), encoding="utf-8") as f:
        data = json.load(f)
    return {r["name"]: np.array(r["farben"], int) for r in data["grund"] + data["akzent"]}


# --- Geometrie des Bausteins ------------------------------------------------------------------------------------------

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


def build_ring(c):
    part = Part("gewaende", (0, 0, 0), UV)
    weld = Welder(part)
    p, nvis = ring_profile(c)
    s = ring_s(c)
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
    """Flaeche mit gegebenen Spiel-Ecken (Liste) und UVs; Normale zeigt zu hint."""
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


def build_slab(c):
    D = c["D"]
    zb = -(D + FLOOR_T)
    part = Part("platte", (0, 0, 0), UV)
    emit, raw = W["emit"], W["raw"]
    left, loop = outline(WB)
    n = len(left)
    # Vorderflaeche z = 0 mit Oeffnung, in lauter konvexe Stuecke zerlegt (Welt-UV, Teilung an den Metergrenzen)
    ys = [q[1] for q in left]
    xs = [q[0] for q in left]
    emit(part, [(-1, 0, 0), (1, 0, 0), (1, Y_BOT, 0), (HW, Y_BOT, 0), (-HW, Y_BOT, 0), (-1, Y_BOT, 0)], (0, 0, 1), "stone")
    colL = [(-1, Y_BOT, 0)] + [(xs[i], ys[i], 0) for i in range(0, 4)] + [(-HW, HS, 0), (-1, HS, 0)]
    colR = [(1, Y_BOT, 0)] + [(-xs[i], ys[i], 0) for i in range(0, 4)] + [(HW, HS, 0), (1, HS, 0)]
    emit(part, colL, (0, 0, 1), "stone")
    emit(part, colR, (0, 0, 1), "stone")
    for i in range(3, n - 1):
        for sg in (-1, 1):
            a, b = (sg * xs[i], ys[i]), (sg * xs[i + 1], ys[i + 1])
            emit(part, [(a[0], a[1], 0), (b[0], b[1], 0), (b[0], HS, 0), (a[0], HS, 0)], (0, 0, 1), "stone")
    # verdeckte Flaechen: Seiten, Oberseite, Unterseite, Rueckseite (raw fuegt T-Stoesse ein)
    for sx in (-1, 1):
        raw(part, [(sx, 0, 0), (sx, 0, zb), (sx, HS, zb), (sx, HS, 0)], (sx, 0, 0), "stone")
    raw(part, [(-1, HS, 0), (1, HS, 0), (1, HS, zb), (-1, HS, zb)], (0, 1, 0), "stone")
    raw(part, [(-1, 0, 0), (1, 0, 0), (1, 0, zb), (-1, 0, zb)], (0, -1, 0), "stone")
    raw(part, [(-1, 0, zb), (1, 0, zb), (1, HS, zb), (-1, HS, zb)], (0, 0, -1), "stone")
    # Tasche: Waende, Boden der Tasche = Rueckwand (Plattenfeld)
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


def build_bord(c):
    return build_bar("bord", bord_geo(c), "BD", 0.5 + 1.0 / 32.0)


def niche_set(c):
    parts = [build_ring(c), build_slab(c), build_bar("sohlbank", sill_geo(c), "SG", HW)]
    if c["regal"]:
        parts.append(build_bord(c))
    return parts


def assert_raster(part):
    """Fugenraster: jede Eckpunkt-UV der Vorderflaeche der Platte entspricht der Weltlage (Kachel 1 m)."""
    u0, v0, u1, v1 = UV["stone"]
    uv = part.bm.loops.layers.uv.active
    checked = 0
    for f in part.bm.faces:
        if all(abs(G_game(v.co)[2]) < 1e-6 for v in f.verts):
            for loop in f.loops:
                x, y, _ = G_game(loop.vert.co)
                uu, vv = (loop[uv].uv[0] - u0) / (u1 - u0), (loop[uv].uv[1] - v0) / (v1 - v0)
                assert abs((x - uu) - round(x - uu)) < 1e-4 and abs((y - vv) - round(y - vv)) < 1e-4, (x, y, uu, vv)
                checked += 1
    assert checked > 20, checked
    return checked


def G_game(co):
    return (co.x, co.z, -co.y)


# --- Fuellungen -------------------------------------------------------------------------------------------------------
# Eigene Atlanten (Farbfelder 8 x 8 und Scheiben), nur Farben der Hauptpalette. Ursprung der Fuellung: Mitte, Standflaeche y = 0,
# Rueckwand der Tasche z = 0 (die Fuellung wird nach +z aufgebaut).

R = all_rows()
HK, ST, HZ, GD = R["Haut/Knochen"], R["Stoff"], R["Holz"], R["Messing/Gold"]


def swatch_atlas(colors, extra=None, size=(96, 48)):
    arr = np.zeros((size[1], size[0], 3)) + np.array(R["Stein"][0], float)     # ungenutzte Flaeche: dunkelste Steinstufe
    pix = {}
    for i, (name, rgb) in enumerate(colors.items()):
        x0, y0 = (i % 8) * 8, (i // 8) * 8
        arr[y0:y0 + 8, x0:x0 + 8] = np.array(rgb, float)
        pix[name] = (x0, y0, x0 + 8, y0 + 8)
    if extra:
        for name, (rect, fn) in extra.items():
            x0, y0, x1, y1 = rect
            arr[y0:y1, x0:x1] = fn(x1 - x0, y1 - y0)
            pix[name] = rect
    return arr, pix


def fpart(name, pix, size=(96, 48)):
    return Part(name, (0, 0, 0), rects_from_pixels(pix, size))


class Fill:
    """Sammelt geschlossene Koerper mit eigener UV-Belegung in einem Teil."""

    def __init__(self, part, pix, size=(96, 48)):
        self.part, self.pix, self.size = part, pix, size

    def swatch_uv(self, name):
        u0, v0, u1, v1 = self.part.rects[name]
        return ((u0 + u1) / 2, (v0 + v1) / 2)

    def solid(self, polys, centre, face_style, T):
        """polys: Vielecke (lokale V-Punkte). face_style(normal_lokal, mitte_lokal) -> ('swatch', name) oder ('planar', name, fn)."""
        for poly in polys:
            P = list(poly)
            n = V(0, 0, 0)
            for i in range(len(P)):
                a, b = P[i], P[(i + 1) % len(P)]
                n += V((a.y - b.y) * (a.z + b.z), (a.z - b.z) * (a.x + b.x), (a.x - b.x) * (a.y + b.y))
            mid = sum(P, V(0, 0, 0)) / len(P)
            if n.length < 1e-12:
                continue
            if n.dot(mid - centre) < 0:
                P.reverse()
                n = -n
            n = n.normalized()
            style = face_style(n, mid)
            if style[0] == "swatch":
                uv = [self.swatch_uv(style[1])] * len(P)
            else:
                rect = self.part.rects[style[1]]
                uv = [style[2](p, rect) for p in P]
            verts = [self.part.bm.verts.new(G(T(p))) for p in P]
            self.part._face(verts, uv)


def ring_solid(rings, caps=True):
    """Ringe (Listen gleicher Laenge) -> Vielecke: Baender als Vierecke, zwei Kappen."""
    n = len(rings[0])
    polys = []
    for a, b in zip(rings, rings[1:]):
        for k in range(n):
            polys.append([a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]])
    if caps:
        polys.append(list(rings[0]))
        polys.append(list(rings[-1]))
    return polys


def make_T(dx, dy, dz, yaw=0.0):
    c, s = math.cos(yaw), math.sin(yaw)
    return lambda p: V(p.x * c + p.z * s + dx, p.y, -p.x * s + p.z * c + dz)


def hexring(cx, cy, r, z, k0=0):
    """Sechseck in der xy-Ebene (Ecken links/rechts, flache Seite oben)."""
    return [V(cx + r * math.cos(math.radians(60 * k)), cy + r * math.sin(math.radians(60 * k)), z) for k in range(6)]


# Flaschen ------------------------------------------------------------------------------------------------------------------

GLAS = {
    "gruen": dict(dark=HK[0], mid=HK[1], light=HK[2], hi=HK[3]),
    "blau": dict(dark=ST[0], mid=ST[2], light=ST[4], hi=ST[5]),
}


def bottle_atlas(tint):
    g = GLAS[tint]

    def disc(w, h):
        out = np.zeros((h, w, 3))
        for py in range(h):
            for px in range(w):
                rho = math.hypot(px - (w - 1) / 2, py - (h - 1) / 2) / (w / 2)
                col = g["dark"] if rho < 0.34 else g["mid"] if rho < 0.64 else g["light"] if rho < 0.86 else g["mid"]
                out[py, px] = col
        out[h - 4, 4:6] = g["hi"]
        return out

    colors = {"dark": g["dark"], "mid": g["mid"], "light": g["light"], "hi": g["hi"]}
    return swatch_atlas(colors, {"disc": ((0, 16, 16, 32), disc)})


def build_flaschen(tint):
    arr, pix = bottle_atlas(tint)
    part = fpart("flaschen_" + tint, pix)
    fl = Fill(part, pix)
    r, L, Ls, rn = 0.0675, 0.15, 0.07, 0.028
    dx, dy = 0.125, 0.108
    layers = [(5, 0), (4, 1), (3, 2)]
    zfront = 0.25
    k = 0
    for n_in, row in layers:
        for i in range(n_in):
            x = (i - (n_in - 1) / 2) * dx
            y = r * 0.866 + row * dy
            zf = zfront - (0.012 if (i + row) % 2 else 0.0)
            T = make_T(0, 0, 0)
            zb, zs, ze = zf, zf - L, zf - L - Ls
            rings = [hexring(x, y, r, zb), hexring(x, y, r, zs), hexring(x, y, rn, ze)]
            centre = V(x, y, (zb + ze) / 2)

            def style(n, mid, x=x, y=y, zb=zb):
                if n.z > 0.9:
                    def f(p, rect, x=x, y=y):
                        u0, v0, u1, v1 = rect
                        return (u0 + (u1 - u0) * (0.5 + (p.x - x) / (2 * r)), v0 + (v1 - v0) * (0.5 + (p.y - y) / (2 * r)))
                    return ("planar", "disc", f)
                if n.y > 0.8:
                    return ("swatch", "light")
                if n.y > 0.3:
                    return ("swatch", "mid")
                if n.y > -0.3:
                    return ("swatch", "mid" if n.z > -0.3 else "dark")
                return ("swatch", "dark")
            fl.solid(ring_solid(rings), centre, style, T)
            k += 1
    assert k == 12
    return part, arr


# Kerzen --------------------------------------------------------------------------------------------------------------------

def candle_atlas():
    colors = {"wax_hi": HK[7], "wax_lt": HK[6], "wax_mid": HK[5], "wax_dk": HK[3], "wick": HZ[0],
              "fl_core": GD[7], "fl_mid": GD[6], "fl_edge": GD[5]}
    return swatch_atlas(colors)


KERZEN = [(-0.2, 0.10, 0.27, 0.034), (-0.08, 0.07, 0.16, 0.030), (0.03, 0.12, 0.23, 0.032), (0.14, 0.08, 0.09, 0.034),
          (0.24, 0.13, 0.06, 0.030)]
TOPS = [0.0, -0.012, -0.022, -0.010, 0.0, 0.007]       # heruntergebrannt: schiefe Oberkante
LUMPS = [(-0.02, 0.22, 0.026), (0.2, 0.04, 0.022)]     # Wachsreste auf der Standflaeche


def build_kerzen(lit):
    arr, pix = candle_atlas()
    part = fpart("kerzen_" + ("brennend" if lit else "aus"), pix)
    fl = Fill(part, pix)

    def wax_style(n, mid):
        if n.y > 0.8:
            return ("swatch", "wax_hi")
        if n.y > 0.2:
            return ("swatch", "wax_lt")
        if n.y > -0.3:
            return ("swatch", "wax_mid" if n.x + n.z * 0.5 > -0.2 else "wax_dk")
        return ("swatch", "wax_dk")

    def ring6(cx, cz, r, y, tops=None):
        return [V(cx + r * math.cos(math.radians(60 * k + 30)), y + (tops[k] if tops else 0.0),
                  cz + r * math.sin(math.radians(60 * k + 30))) for k in range(6)]

    for i, (x, z, h, r) in enumerate(KERZEN):
        rings = [ring6(x, z, r * 1.8, 0.0), ring6(x, z, r * 1.3, 0.016), ring6(x, z, r, 0.034), ring6(x, z, r, h, TOPS)]
        fl.solid(ring_solid(rings), V(x, h / 2, z), wax_style, make_T(0, 0, 0))
        top = h + TOPS[0]
        wick = [ring6(x + 0.002, z, 0.004, h - 0.01), ring6(x + 0.002, z, 0.004, h + 0.014)]
        fl.solid(ring_solid(wick), V(x, h, z), lambda n, m: ("swatch", "wick"), make_T(0, 0, 0))
        if lit:
            yb, yt, yp = h + 0.012, h + 0.058, h + 0.012 - 0.0
            wd = 0.0105
            c = V(x + 0.002, yb + 0.012, z)
            ring = [V(c.x + wd, c.y, c.z), V(c.x, c.y, c.z + wd), V(c.x - wd, c.y, c.z), V(c.x, c.y, c.z - wd)]
            tip, base = V(c.x, yt, c.z), V(c.x, yb, c.z)
            polys = [[ring[k], ring[(k + 1) % 4], tip] for k in range(4)] + [[ring[(k + 1) % 4], ring[k], base] for k in range(4)]
            fl.solid(polys, c, lambda n, m: ("swatch", "fl_core" if (n.z > 0.2 and n.y > 0) else "fl_mid" if n.z > 0.2 else "fl_edge"),
                     make_T(0, 0, 0))
    for (x, z, r) in LUMPS:
        rings = [ring6(x, z, r * 1.5, 0.0), ring6(x, z, r, 0.012), ring6(x, z, r * 0.8, 0.02)]
        fl.solid(ring_solid(rings), V(x, 0.01, z), wax_style, make_T(0, 0, 0))
    return part, arr


# Schaedel ------------------------------------------------------------------------------------------------------------------

def skull_atlas():
    bone = HK[5]
    dark = HK[0]

    def face(w, h):
        out = np.zeros((h, w, 3)) + np.array(bone, float)
        out[13:18, 8:13] = dark
        out[13:18, 19:24] = dark
        out[9:12, 15:17] = dark
        out[8, 11:21] = HK[4]
        out[18:20, 7:14] = HK[4]
        out[18:20, 18:25] = HK[4]
        return out

    def jaw(w, h):
        out = np.zeros((h, w, 3)) + np.array(HK[4], float)
        for px in range(w):
            if (px // 2) % 2 == 0:
                out[4:8, px] = HK[7]
            else:
                out[4:8, px] = HK[1]
        out[0:4] = HK[4]
        return out

    colors = {"bone_hi": HK[6], "bone": HK[5], "bone_mid": HK[4], "bone_dk": HK[3], "socket": HK[0]}
    return swatch_atlas(colors, {"face": ((0, 16, 32, 48), face), "jaw": ((40, 16, 72, 24), jaw)})


SK_X0, SK_X1 = -0.08, 0.08


def skull_into(fl, dx, dy, dz, yaw, scale=1.0):
    def planar_face(p, rect):
        u0, v0, u1, v1 = rect
        return (u0 + (u1 - u0) * min(max((p.x / scale - SK_X0) / (SK_X1 - SK_X0), 0.0), 1.0),
                v0 + (v1 - v0) * min(max((p.y / scale - 0.02) / 0.16, 0.0), 1.0))

    def planar_jaw(p, rect):
        u0, v0, u1, v1 = rect
        return (u0 + (u1 - u0) * min(max((p.x / scale + 0.045) / 0.09, 0.0), 1.0),
                v0 + (v1 - v0) * min(max(p.y / scale / 0.045, 0.0), 1.0))

    def cran_style(n, mid):
        if n.z > 0.5 and n.y < 0.8:
            return ("planar", "face", planar_face)
        if n.y > 0.7:
            return ("swatch", "bone_hi")
        if n.y < -0.5:
            return ("swatch", "bone_dk")
        return ("swatch", "bone" if n.x < 0.3 else "bone_mid")

    def jaw_style(n, mid):
        if n.z > 0.7:
            return ("planar", "jaw", planar_jaw)
        if n.y > 0.7:
            return ("swatch", "bone_mid")
        return ("swatch", "bone_dk")

    k = scale
    nseg = 8
    spec = [(0.035, 0.040, 0.050, -0.005), (0.065, 0.070, 0.080, -0.008), (0.105, 0.075, 0.085, -0.010),
            (0.138, 0.058, 0.066, -0.012), (0.152, 0.030, 0.036, -0.012)]
    rings = []
    for (y, rx, rz, cz) in spec:
        rings.append([V(k * rx * math.cos(math.pi / nseg + 2 * math.pi * i / nseg), k * y,
                        k * (cz + rz * math.sin(math.pi / nseg + 2 * math.pi * i / nseg))) for i in range(nseg)])
    T = make_T(dx, dy, dz, yaw)
    fl.solid(ring_solid(rings), V(0, k * 0.1, k * -0.008), cran_style, T)
    jr = [[V(k * sx * 0.045, k * 0.0, k * sz * 0.04) for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1))]]
    jr.append([V(k * sx * 0.036, k * 0.045, k * sz * 0.038) for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1))])
    # Kiefer: Fuehrung der Zaehne nach vorn (+z), Unterseite auf der Standflaeche
    fl.solid(ring_solid(jr), V(0, k * 0.022, 0), jaw_style, T)


def build_schaedel():
    arr, pix = skull_atlas()
    part = fpart("schaedel", pix)
    fl = Fill(part, pix)
    skull_into(fl, -0.15, 0.0, 0.12, math.radians(14))
    skull_into(fl, 0.15, 0.0, 0.14, math.radians(-22))
    skull_into(fl, 0.0, 0.128, 0.10, math.radians(4))
    return part, arr


FUELLUNGEN = {
    "flaschen_gruen": lambda: build_flaschen("gruen"),
    "flaschen_blau": lambda: build_flaschen("blau"),
    "kerzen_brennend": lambda: build_kerzen(True),
    "kerzen_aus": lambda: build_kerzen(False),
    "schaedel": build_schaedel,
}


# --- Szene ------------------------------------------------------------------------------------------------------------

def place(part, side, zpos, x0):
    """side 0: Wand in der Ebene z = 0 (Raum); side -1 / +1: Gangwand bei x = side, Nische an der Stelle zpos (Spiel-z)."""
    if side == 0:
        part.bm.transform(Matrix.Translation(G(V(x0, 0.0, 0.0))))
        return
    rot = Matrix.Rotation(math.radians(90 if side < 0 else -90), 4, "Z")
    part.bm.transform(Matrix.Translation(G(V(side * 1.0, 0.0, zpos))) @ rot @ Matrix.Translation(G(V(x0, 0.0, 0.0))))


def shift(part, dx, dy, dz):
    part.bm.transform(Matrix.Translation(G(V(dx, dy, dz))))


def game_bbox(parts):
    lo, hi = [9e9] * 3, [-9e9] * 3
    for p in parts:
        for v in p.bm.verts:
            g = G_game(v.co)
            for i in range(3):
                lo[i], hi[i] = min(lo[i], g[i]), max(hi[i], g[i])
    return lo, hi


def niche_image(c, v):
    key = "niche_%s_%s_%s_%s" % (v, c["key"], c["pr"], c["regal"])
    if key not in W["ATLAS_IMG"]:
        W["ATLAS_IMG"][key] = W["save_image"](key, my_atlas(v, c), os.path.join(TMP, key + ".png"))
    return W["ATLAS_IMG"][key]


def add_niche(coll, c, v, side=0, zpos=0.0, x0=0.0):
    out = []
    for p in niche_set(c):
        place(p, side, zpos, x0)
        name = "n_%s_%g_%s_%s_%g_%g" % (c["key"], c["pr"], c["regal"], p.name, side, zpos)
        out.append(p.build(coll, W["pass_material"](name, niche_image(c, v)), smooth=False))
    return out


def fill_image(kind):
    key = "fill_" + kind
    if key not in W["ATLAS_IMG"]:
        part, arr = FUELLUNGEN[kind]()
        part.bm.free()
        W["ATLAS_IMG"][key] = W["save_image"](key, arr, os.path.join(TMP, key + ".png"))
    return W["ATLAS_IMG"][key]


def add_fill(coll, kind, c, ys, side=0, zpos=0.0, x0=0.0, dx=0.0):
    part, _ = FUELLUNGEN[kind]()
    shift(part, dx, ys, -c["D"])
    place(part, side, zpos, x0)
    return [part.build(coll, W["pass_material"]("f_%s_%g_%g_%g" % (kind, ys, side, zpos), fill_image(kind)), smooth=False)]


def import_glb(coll, path, matrix, prefix):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    objs = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    for o in objs:
        img = None
        for m in o.data.materials:
            for n in m.node_tree.nodes:
                if n.type == "TEX_IMAGE":
                    img = n.image
        o.data.materials.clear()
        o.data.materials.append(W["pass_material"](prefix + o.name, img))
        o.parent = None
        o.matrix_world = matrix
        coll.objects.link(o)
        for cc in list(o.users_collection):
            if cc is not coll:
                cc.objects.unlink(o)
    return objs


def add_flat(coll, side=-1, zpos=0.0):
    """Das bisherige, vorgesetzte Modell (Vorgaengerprobe), nur zum Vergleich."""
    rot = Matrix.Rotation(math.radians(90 if side < 0 else -90), 4, "Z")
    return import_glb(coll, FLAT_GLB, Matrix.Translation(G(V(side * 1.0, 0.0, zpos))) @ rot, "flat_")


def add_gang(coll, v, z0, z1, gap=None):
    """Gang 2 m breit, 3,5 m hoch. gap = (za, zb): linke Wand ueber 0..3 m ausgespart (dort sitzt der Baustein)."""
    part = Part("gang", (0, 0, 0), UV)
    emit = W["emit"]
    x = 1.0
    if gap is None:
        emit(part, [(-x, 0, z0), (-x, 0, z1), (-x, 3.5, z1), (-x, 3.5, z0)], (1, 0, 0), "stone")
    else:
        za, zb = gap
        emit(part, [(-x, 0, z0), (-x, 0, za), (-x, 3.5, za), (-x, 3.5, z0)], (1, 0, 0), "stone")
        emit(part, [(-x, 0, zb), (-x, 0, z1), (-x, 3.5, z1), (-x, 3.5, zb)], (1, 0, 0), "stone")
        emit(part, [(-x, HS, za), (-x, HS, zb), (-x, 3.5, zb), (-x, 3.5, za)], (1, 0, 0), "stone")
    emit(part, [(x, 0, z0), (x, 0, z1), (x, 3.5, z1), (x, 3.5, z0)], (-1, 0, 0), "stone")
    emit(part, [(-x, 3.5, z0), (x, 3.5, z0), (x, 3.5, z1), (-x, 3.5, z1)], (0, -1, 0), "stone")
    return [part.build(coll, W["pass_material"]("g_%s_%g_%g_%s" % (v, z0, z1, gap), niche_image(cfg_of("t30"), v)), smooth=False)]


def add_wall_gap(coll, v, x0, x1, h, gaps):
    """Wand z = 0 von x0 bis x1. gaps = [(xa, xb, oben)]: dort fehlt die Wand; oben=True: oberhalb 3 m laeuft sie weiter."""
    part = Part("wand", (0, 0, 0), UV)
    emit = W["emit"]
    x = x0
    for xa, xb, oben in sorted(gaps):
        if xa > x:
            emit(part, [(x, 0, 0), (xa, 0, 0), (xa, h, 0), (x, h, 0)], (0, 0, 1), "stone")
        if oben:
            emit(part, [(xa, HS, 0), (xb, HS, 0), (xb, h, 0), (xa, h, 0)], (0, 0, 1), "stone")
        x = xb
    if x1 > x:
        emit(part, [(x, 0, 0), (x1, 0, 0), (x1, h, 0), (x, h, 0)], (0, 0, 1), "stone")
    return [part.build(coll, W["pass_material"]("wg_%s_%g_%g_%g" % (v, x0, x1, h), niche_image(cfg_of("t30"), v)), smooth=False)]


def add_door_x(coll, v, x, z=0.0):
    return import_glb(coll, os.path.join(W["MODELS"], "door_frame_normal_%s.glb" % v),
                      Matrix.Translation(G(V(x, 0, z - 1))), "d_")


def add_figure(coll, x, z, h, width, rgb):
    part = Part("figur", (0, 0, 0), {"all": (0.0, 0.0, 1.0, 1.0)})
    part.box(V(x - width / 2, 0, z - width / 2), V(x + width / 2, h, z + width / 2), "all")
    tag = "fig_%g_%g_%d" % (x, h, rgb[0])
    img = W["save_image"](tag, np.tile(np.array(rgb, np.float32), (4, 4, 1)), os.path.join(TMP, tag + ".png"))
    return [part.build(coll, W["pass_material"](tag, img), smooth=False)]


def build_scene(items):
    coll = W["new_scene"]()
    objs = []
    for it in items:
        k = it[0]
        if k == "niche":
            objs += add_niche(coll, *it[1:])
        elif k == "fill":
            objs += add_fill(coll, *it[1:])
        elif k == "flat":
            objs += add_flat(coll, *it[1:])
        elif k == "wall":
            objs += W["add_wall"](coll, *it[1:])
        elif k == "wallgap":
            objs += add_wall_gap(coll, *it[1:])
        elif k == "floor":
            objs += W["add_floor"](coll, *it[1:])
        elif k == "gang":
            objs += add_gang(coll, *it[1:])
        elif k == "pillar":
            objs += W["add_pillar"](coll, *it[1:])
        elif k == "door":
            objs += add_door_x(coll, *it[1:])
        elif k == "fig":
            objs += add_figure(coll, *it[1:])
    W["finish"](objs)
    return objs


W["build_scene"] = build_scene
shot, text, png, hcat, titled = W["shot"], W["text"], W["png"], W["hcat"], W["titled"]
BG = W["BG"]
RED = (200, 90, 70)


def vcat(images, gap=6):
    w = max(a.shape[1] for a in images)
    pads = [a if a.shape[1] == w else np.concatenate([a, np.tile(BG, (a.shape[0], w - a.shape[1], 1))], 1) for a in images]
    out = [pads[0]]
    for a in pads[1:]:
        out += [np.tile(BG * 0.5, (gap, w, 1)), a]
    return np.concatenate(out, axis=0)


# --- Modelle ----------------------------------------------------------------------------------------------------------

R_MAIN = list(R.values())


def model_jobs():
    jobs = []
    for key in TIEFEN:
        for v in WALL_FILES:
            jobs.append(("wandnische_%s_%s" % (key, v), cfg_of(key), v))
    for v in WALL_FILES:
        jobs.append(("wandnische_regal_t30_%s" % v, cfg_of("t30", regal=True), v))
    return jobs


def export_models():
    rows = []
    d = os.path.join(OUT, "modelle")
    os.makedirs(d, exist_ok=True)
    for tag, c, v in model_jobs():
        bpy.ops.wm.read_factory_settings(use_empty=True)
        coll = bpy.data.collections.new("Wandnische")
        bpy.context.scene.collection.children.link(coll)
        tex = my_atlas(v, c)
        ncol, bad = palette_check(tex, [stein()])
        assert bad == 0, "Farben ausserhalb der Reihe Stein: %d" % bad
        image = W["save_image"](tag, tex, os.path.join(d, tag + ".png"))
        mat = W["export_material"]("m", image)
        parts = niche_set(c)
        lo, hi = game_bbox(parts)
        raster = assert_raster([p for p in parts if p.name == "platte"][0])
        objs, info = [], []
        for part in parts:
            o_edges, multi = W["edge_report"](part)
            assert o_edges == 0 and multi == 0, (tag, part.name, o_edges, multi)
            info.append((part.name, W["triangles"](part)))
            objs.append(part.build(coll, mat, smooth=False))
        W["export_glb"](objs, os.path.join(d, tag + ".glb"))
        rows.append((tag, info, ncol, lo, hi, raster))
    for tag, info, ncol, lo, hi, raster in rows:
        print("MODELL %s: %s, gesamt %d Dreiecke, Farben %d, y %.3f bis %.3f, x %.3f bis %.3f, z %.4f bis %.4f, Raster %d" % (
            tag, "; ".join("%s %d" % i for i in info), sum(i[1] for i in info), ncol, lo[1], hi[1], lo[0], hi[0], lo[2], hi[2], raster))
    return rows


def export_fills():
    d = os.path.join(OUT, "modelle")
    os.makedirs(d, exist_ok=True)
    for kind in FUELLUNGEN:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        coll = bpy.data.collections.new("Fuellung")
        bpy.context.scene.collection.children.link(coll)
        part, arr = FUELLUNGEN[kind]()
        ncol, bad = palette_check(arr, R_MAIN)
        if bad:
            ok = {tuple(int(x) for x in p) for r in R_MAIN for p in r}
            print("FREMD", kind, sorted({tuple(int(x) for x in p) for p in np.round(arr).reshape(-1, 3)} - ok))
        assert bad == 0, (kind, "Farben ausserhalb der Hauptpalette", bad)
        tag = "fuellung_" + kind
        image = W["save_image"](tag, arr, os.path.join(d, tag + ".png"))
        mat = W["export_material"]("m", image)
        lo, hi = game_bbox([part])
        o_edges, multi = W["edge_report"](part)
        assert o_edges == 0 and multi == 0, (kind, o_edges, multi)
        tris = W["triangles"](part)
        W["export_glb"]([part.build(coll, mat, smooth=False)], os.path.join(d, tag + ".glb"))
        print("FUELLUNG %s: %d Dreiecke, Farben %d, x %.3f bis %.3f, y 0 bis %.3f, z %.3f bis %.3f" % (
            tag, tris, ncol, lo[0], hi[0], hi[1], lo[2], hi[2]))


# --- Ansichten --------------------------------------------------------------------------------------------------------
# Gangwand links bei x = -1 (Spiel-Achsen); Nische bei z = 0, Blick auf die Wand.
CAMS = {
    "frontal": ((0.95, 1.55, 0.0), (-1.0, 1.45, 0.0), 76),
    "schraeg": ((0.45, 1.6, 1.55), (-1.0, 1.4, 0.0), 70),
    "streif": ((-0.7, 1.6, 5.2), (-1.0, 1.55, 0.0), 70),
    "nah_front": ((0.45, 1.3, 0.0), (-1.0, 1.2, 0.0), 62),
    "nah_schraeg": ((0.1, 1.5, 1.2), (-1.0, 1.15, 0.0), 60),
    "gang_figur": ((0.8, 1.6, 3.6), (-1.0, 1.1, 0.4), 62),
}
FIG = ("fig", -0.4, -1.6, PLAYER_H, 0.45, RED)


def gang_scene(v, c, extras=(), flat=False, fig=True):
    base = [("floor", -1, 1, -9, 9)]
    if flat:
        base += [("gang", v, -9.0, 9.0, None), ("flat", -1, 0.0)]
    else:
        base += [("gang", v, -9.0, 9.0, (-1.0, 1.0)), ("niche", c, v, -1, 0.0, 0.0)]
    items = base + list(extras)
    if fig:
        items.append(FIG)
    return items


def cell(items, cam, mode="spiel", size=(560, 315)):
    loc, tgt, fov = CAMS[cam]
    return shot(items, loc, tgt, fov, size, mode)


def sheet_tiefe(v="a"):
    cols = [("flach (bisher)", gang_scene(v, None, flat=True)),
            ("Tiefe 0,15 m", gang_scene(v, cfg_of("t15"))),
            ("Tiefe 0,3 m", gang_scene(v, cfg_of("t30")))]
    rows = []
    for cam, label in (("frontal", "frontal"), ("schraeg", "schraeg 45 Grad"), ("streif", "Streiflicht")):
        rows.append(hcat([titled(cell(it, cam, size=(430, 300)), "%s - %s" % (name, label)) for name, it in cols]))
    return vcat(rows)


def sheet_rahmen(v="a"):
    cols = [("Rahmen buendig", gang_scene(v, cfg_of("t30", "buendig"))), ("Rahmen 1/16 vor", gang_scene(v, cfg_of("t30", "vor")))]
    rows = [hcat([titled(cell(it, cam, size=(560, 315)), "%s - %s" % (name, cam)) for name, it in cols]) for cam in ("frontal", "schraeg", "streif")]
    return vcat(rows)


def view_single(c, v, which, size=(360, 640)):
    it = [("floor", -3, 3, -3, 3), ("niche", c, v, 0, 0.0, 0.0)]
    cy = 1.4
    if which == "vorn":
        return shot(it, (0, cy, 8), (0, cy, 0), 0, size, "hell", ortho=4.0)
    if which == "seite":
        return shot(it, (8, cy, 0), (0, cy, 0), 0, size, "hell", ortho=4.0)
    if which == "hinten":
        return shot(it, (0, cy, -8), (0, cy, 0), 0, size, "hell", ortho=4.0)
    return shot(it, (2.4, 3.4, 3.0), (0, 1.1, 0), 40, size, "hell")


def einzel(c, v="a"):
    return hcat([titled(view_single(c, v, w), w) for w in ("vorn", "seite", "hinten", "oben")])


def wall_scene(fass, v, c):
    if fass == "raum":
        h = 6.0
        return [("floor", -8, 8, -1, 14), ("wallgap", v, -6, 6, h, [(-4.2, -2.2, False), (-1, 1, True)]), ("door", v, -3.2),
                ("niche", c, v, 0, 0.0, 0.0), ("pillar", "raum", v, 3.2), ("fig", 1.5, 2.3, PLAYER_H, 0.45, RED)]
    h = 3.5
    return [("floor", -8, 8, -1, 14), ("wallgap", v, -5, 5, h, [(-3.6, -1.6, False), (-1, 1, True)]), ("door", v, -2.6),
            ("niche", c, v, 0, 0.0, 0.0), ("pillar", "gang", v, 2.4), ("fig", 1.15, 2.3, PLAYER_H, 0.45, RED)]


def view_raum(c, v="a"):
    it = wall_scene("raum", v, c)
    a = shot(it, (0, 3.1, 12), (0, 3.1, 0), 0, (1000, 740), "hell", ortho=10.2)
    b = shot(it, (4.5, 1.7, 7.5), (0, 1.6, 0), 60, (1000, 600), "spiel")
    return vcat([titled(a, "6-m-Raum, frontal (Tuerrahmen links, Wandpfeiler rechts)"), titled(b, "schraeg, Spielansicht")])


def view_gangwand(c, v="a"):
    it = wall_scene("gang", v, c)
    a = shot(it, (0, 1.75, 12), (0, 1.75, 0), 0, (1000, 560), "hell", ortho=8.0)
    return titled(a, "Gang 3,5 m, frontal")


def fills_for(kind, c, where):
    ys = 0.5 if where == "sohle" else 1.0
    return [("fill", kind, c, ys, -1, 0.0, 0.0)]


def sheet_regal(v="a"):
    c_regal, c_flach = cfg_of("t30", regal=True), cfg_of("t30")
    cols = [("leer", None, None), ("Flaschen", "flaschen_gruen", "sohle"), ("Kerzen", "kerzen_brennend", "bord"),
            ("Schaedel", "schaedel", "bord")]
    rows = []
    for cam, label, c, rg in (("nah_front", "frontal", c_regal, "Regal"), ("nah_schraeg", "schraeg", c_regal, "Regal"),
                              ("nah_front", "frontal", c_flach, "ohne Bord"), ("gang_figur", "im Gang", c_regal, "Regal")):
        r = []
        for name, kind, where in cols:
            ex = fills_for(kind, c, where if rg == "Regal" else "sohle") if kind else []
            r.append(titled(cell(gang_scene(v, c, ex), cam, size=(430, 330)), "%s, %s: %s" % (rg, label, name)))
        rows.append(hcat(r))
    return vcat(rows)


def fill_detail():
    c = cfg_of("t30", regal=True)
    cells = []
    for name, kind, where in (("Flaschen gruen", "flaschen_gruen", "sohle"), ("Flaschen blau", "flaschen_blau", "sohle"),
                              ("Kerzen brennend", "kerzen_brennend", "bord"), ("Kerzen aus", "kerzen_aus", "bord"),
                              ("Schaedel", "schaedel", "bord")):
        it = gang_scene("a", c, fills_for(kind, c, where), fig=False)
        y = 0.7 if where == "sohle" else 1.2
        cells.append(titled(shot(it, (0.2, y, 1.1), (-1.0, y, 0.0), 40, (420, 420), "spiel"), name))
    return hcat(cells)


STEPS = os.environ.get("W538T_ONLY")
STEPS = set(STEPS.split(",")) if STEPS else None


def want(name):
    return STEPS is None or name in STEPS


def main():
    c = cfg_of("t30")
    if want("modelle"):
        export_models()
        export_fills()
    if want("rahmen"):
        png(sheet_rahmen(), os.path.join(OUT, "rahmen-vergleich.png"))
    if want("blatt"):
        png(sheet_tiefe(), os.path.join(OUT, "blatt-nische-tief-538.png"))
    if want("raum"):
        png(view_raum(c), os.path.join(OUT, "raum-empfehlung-538.png"))
        png(view_gangwand(c), os.path.join(OUT, "gang-empfehlung-538.png"))
    if want("regal"):
        png(sheet_regal(), os.path.join(OUT, "blatt-nische-regal-538.png"))
    if want("detail"):
        png(fill_detail(), os.path.join(OUT, "fuellungen-detail.png"))
    if want("einzel"):
        png(einzel(c), os.path.join(OUT, "einzel-t30.png"))
        png(einzel(cfg_of("t15")), os.path.join(OUT, "einzel-t15.png"))
        png(einzel(cfg_of("t30", regal=True)), os.path.join(OUT, "einzel-regal-t30.png"))
    if want("spiel"):
        png(cell(gang_scene("a", c), "schraeg", size=(854, 480)), os.path.join(OUT, "spielaufloesung-t30.png"))


main()
