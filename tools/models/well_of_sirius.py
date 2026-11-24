"""Brunnen "Sirius' Relief" (Issue #438, Name seit #716; Dateinamen bleiben well_of_sirius_*): Dreischaler aus Stein, bereit (mit Wasser) und verbraucht (trocken).

Erzeuger fuer das Spiel. Uebergabe aus dem Probestueck design/proben/grafik/brunnen-438/brunnen3.py (Endfassung, vom Menschen
am 2026-11-19 abgenommen: Form A2, Textur und Moosmenge von A1). Bereinigt: nur diese Fassung, keine Probebilder, kein Blatt.
Geometrie, Textur und Seed sind unveraendert (die Textur ist bitgleich mit textur_f.png des Probestuecks); in den glb
unterscheiden sich nur die Namen von Material und Bild. Zwei Laeufe liefern bitgleiche Dateien.

Aufruf (aus dem Repository-Wurzelverzeichnis), danach python tools/models/checksums.py:
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/models/well_of_sirius.py

Alles per Skript, nichts heruntergeladen. Spiel-Achsen (x seitlich, y oben, z vorn), Meter. Ursprung: Mitte am Boden.
Sechseckig, 1,70 x 2,30 x 1,96 m (x, y, z); Sockel-Umkreis 0,98 m. Erzeugt nach assets/models/:
  well_of_sirius_bereit.glb      Teile body, column, water_basin, water_bowl1, water_bowl2, falls (372 Dreiecke)
  well_of_sirius_verbraucht.glb  Teile body, column (264 Dreiecke): trockenes Becken mit Brocken und stumpfen Muenzen
  well_of_sirius.png             der Atlas (64 x 64, 28 Farben, nur Palette), in beiden glb eingebettet
Alle Teile fest, ohne Eltern, Drehpunkt im Ursprung.

Wasser-Animation: Die Wasser-Teile (water_*) zeigen das Atlas-Feld water0, die Faelle (falls) fall0. Die Bilder 1 bis 3 liegen
im Atlas jeweils rechts daneben (water: 16 px = 1/4 der Breite je Bild, fall: 8 px = 1/8 je Bild). Das Spiel verschiebt die
u-Koordinate dieser Teile alle 150 ms um ein Bild (WellOfSiriusLook im Core); das Funkeln steckt in den Wasserbildern.
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
import json  # noqa: E402
from psx import V, Part, rects_from_pixels, vnoise, save_image, export_material, export_glb, report_parts, MATS  # noqa: E402

TOOLS = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(TOOLS))
OUT_DIR = os.path.join(ROOT, "assets", "models")
PALETTE = os.path.join(ROOT, "assets", "palette", "hauptpalette.json")

with open(PALETTE, encoding="utf-8") as _f:
    _PAL = json.load(_f)
RAMPS = {r["name"]: r["farben"] for r in _PAL["grund"] + _PAL["akzent"]}
TAB, IDS = [], {}


def pc(ramp, i):
    """Index einer Palettenfarbe in der Farbtabelle der Textur (Reihe, Stufe)."""
    key = (ramp, i)
    if key not in IDS:
        IDS[key] = len(TAB)
        TAB.append(RAMPS[ramp][i])
    return IDS[key]


def st(i):
    return pc("Stein", int(min(max(i, 2), 13)))


def mo(i):
    return pc("Haut/Knochen", i)          # Moos: das einzige Gruen der Palette (vom Menschen gebilligt)


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
MOSS = 0.40                                         # wenig Moos (Fugen, Sockel, Schalenrand)
SEED = 11


def rects_frame0():
    """Die UV-Rechtecke der Datei: Wasser und Faelle zeigen Bild 0 (das Spiel verschiebt sie)."""
    r = dict(UVR)
    r["water"] = r["waterside"] = UVR["water0"]
    r["fall"] = UVR["fall0"]
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
            lx = (x - off) % 16
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


def atlas():
    """Atlas als Feld [y][x][rgb], Zeile 0 unten; nur Palettenfarben, hoechstens 32."""
    m, s = MOSS, SEED
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
    assert len(used) <= 32, len(used)
    return tab[ids], len(used)


# ---------------------------------------------------------------- Geometrie

def ring_pts(n, r, y, rot):
    return [(r * math.cos(math.radians(rot) + 2 * math.pi * k / n), y, r * math.sin(math.radians(rot) + 2 * math.pi * k / n))
            for k in range(n)]


def lathe(part, start, segs, n, rot=0.0):
    """Geschlossener Drehkoerper um die senkrechte Achse. start (r, y); segs: (r, y, Rechteck) je Abschnitt; r = 0 an den
    Enden gibt einen Faecher (Pol). Jede Seitenflaeche zeigt das ganze Rechteck; 'dry' wird von oben projiziert."""
    import bmesh
    from psx import G
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


def parts(state, R):
    """Dreischaler: sechseckig, dreistufiger Sockel, zwei Schalen uebereinander, Steinspitze."""
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
    result = [body, col]
    if wet:
        result.append(water_slab("water_basin", R, n, 0.60, 0.34, 0.64, rot))
        result.append(water_slab("water_bowl1", R, n, 0.42, 1.14, 1.185, rot))
        result.append(water_slab("water_bowl2", R, n, 0.27, 1.64, 1.675, rot))
        f = Part("falls", (0, 0, 0), R)
        for sx in (1, -1):
            stream(f, sx * 0.48, 0.0, "x", 0.60, 1.19)
            stream(f, sx * 0.33, 0.0, "x", 1.17, 1.67)
        result.append(f)
    return result


def build(state, tex):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    coll = bpy.data.collections.new("Brunnen")
    bpy.context.scene.collection.children.link(coll)
    image = save_image("well_of_sirius", tex, os.path.join(OUT_DIR, "well_of_sirius.png"))
    objs = [p.build(coll, export_material("m_well_of_sirius", image), smooth=False) for p in parts(state, rects_frame0())]
    name = "well_of_sirius_%s" % state
    export_glb(objs, os.path.join(OUT_DIR, name + ".glb"))
    report_parts(name, objs)


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    tex, colours = atlas()
    for state in ("bereit", "verbraucht"):
        build(state, tex)
    print(f"TEXTUR well_of_sirius 64x64 Farben {colours}")


main()
