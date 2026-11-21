"""Silver & Ash - Probestueck Wandzonen und Baender (Issue #282), ausserhalb des Spiels.

Aufruf:  blender --background --python wandzonen.py        (TEMP auf einen Wegwerf-Ordner setzen)

Erzeugt in diesem Ordner: Texturen (Sockel und Abschluss je Anfang/Mitte/Ende, Version a und b),
Ansichten (hell und in Spielbeleuchtung) und das Uebersichtsblatt blatt-282.png.
Alles per Skript; nur Farben der Hauptpalette (Reihe Stein). Zweimal ausgefuehrt gibt es dieselben Bytes.

Aufbau der Baender: Ein Band ist eine Wiederholung mit Periode 128 px (2 m = eine Zelle). Ein Lauf aus n Zellen
ist genau Anfang (die ersten 64 px) + (n-1) x Mitte (128 px, nahtlos wiederholbar) + Ende (die letzten 64 px),
also 128 n px. Anfang und Ende sind die Haelften der Mitte plus ein Abschluss an der Stirnkante.
"""
import hashlib
import json
import math
import os
import struct
import sys
import tempfile

import bpy  # noqa: E402
import blf  # noqa: E402
import imbuf  # noqa: E402
import numpy as np  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "tools", "models"))
import psx  # noqa: E402

TMP = os.path.join(tempfile.gettempdir(), "wandzonen282_tmp")
os.makedirs(TMP, exist_ok=True)
TEX_DIR = os.path.join(HERE, "texturen")
VIEW_DIR = os.path.join(HERE, "ansichten")
os.makedirs(TEX_DIR, exist_ok=True)
os.makedirs(VIEW_DIR, exist_ok=True)

# ------------------------------------------------------------------ Palette (nur Reihe Stein)
PAL = json.load(open(os.path.join(ROOT, "assets", "palette", "hauptpalette.json"), encoding="utf-8"))
STEIN = np.array([r for r in PAL["grund"] if r["name"] == "Stein"][0]["farben"], float)
assert len(STEIN) == 15
STEIN_SET = {tuple(int(v) for v in c) for c in STEIN}
JOINT = 6      # Fuge
SHADOW = 7
FACE = 11      # Grundfarbe der Werksteine (Mittel der Wandtextur etwa 10 bis 11)


def rgb(idx):
    return STEIN[np.clip(idx, 0, 14)].astype(np.uint8)


def hash_noise(shape, seed, prob):
    r = np.random.RandomState(seed)
    return np.where(r.rand(*shape) < prob, np.where(r.rand(*shape) < 0.5, -1, 1), 0)


# ------------------------------------------------------------------ Wandtextur (wie heute im Spiel)
def load_wall():
    img = bpy.data.images.load(os.path.join(ROOT, "assets", "textures", "catacomb_wall.png"), check_existing=False)
    img.colorspace_settings.name = "Non-Color"
    w, h = img.size
    a = np.round(np.array(img.pixels[:]).reshape(h, w, 4)[..., :3] * 255).astype(np.uint8)
    bpy.data.images.remove(img)
    return a[::-1].copy()   # Zeile 0 = oben


WALL = load_wall()
assert {tuple(int(v) for v in p) for p in WALL.reshape(-1, 3)} <= STEIN_SET

# ------------------------------------------------------------------ Baender als Palettenindex-Felder
PERIOD = 128
ROWS = 32
HJ = 16                  # waagerechte Lagerfuge: Zeile 16 (= 0,25 m ueber dem Sockelfuss bzw. unter der Oberkante)
J1 = [0, 56, 96]         # Sockel, untere Schicht (Steine 56, 40, 32)
J2 = [28, 76, 108]       # Sockel, obere Schicht (48, 32, 48), versetzt
F_TOP = [0, 72]          # Abschluss a, obere Schicht (72, 56)
F_LOW = [36, 100]        # Abschluss a, untere Schicht (64, 64)
H_JOINT_ROWS = [HJ]


def course(arr, noise, r0, r1, joints, hjoint_top, bright=0, seed=0):
    """Werksteinschicht ueber die ganze Periode: Stossfugen, Kantenlicht und -schatten, Steinfarbe je Stein."""
    js = sorted(joints)
    xs = np.arange(PERIOD)
    sid = np.searchsorted(js, xs, side="right") - 1          # -1 = letzter Stein (laeuft ueber die Naht)
    nxt = np.array([js[(i + 1) % len(js)] + (PERIOD if i + 1 >= len(js) else 0) for i in range(len(js))])
    for x in xs:
        i = sid[x]
        start = js[i]
        end = nxt[i]
        d_left = (x - start) % PERIOD
        d_right = (end - 1 - (x if x >= start else x + PERIOD)) % PERIOD
        base = FACE - 1 + (hash((i * 7 + seed) % 5) % 3) + bright
        for r in range(r0, r1):
            v = base
            if r == r0 and hjoint_top:
                v = JOINT
            elif r == r0 + 1 and hjoint_top:
                v = base + 2
            elif r == r0 and not hjoint_top:
                v = base + 2
            elif r == r1 - 1:
                v = base - 1
            if d_left == 0:
                v = JOINT
            elif d_left == 1 and v > JOINT + 1:
                v += 1
            elif d_right == 0 and v > JOINT + 1:
                v -= 2
            arr[r, x] = v
    arr[r0:r1] += noise[r0:r1] * (arr[r0:r1] > JOINT + 1)


def schraege(arr, r0, r1, joints):
    ramp = [9, 8, 8, 7, 7, 7, 6, 6]
    for k, r in enumerate(range(r0, r1)):
        arr[r, :] = ramp[k]
        for j in joints:
            arr[r, j % PERIOD] = JOINT


def fase(arr, joints):
    for r, v in enumerate((13, 13, 12, 12)):
        arr[r, :] = v
        for j in joints:
            arr[r, j % PERIOD] = 9


def band_tile(kind, ver, ende=False):
    """Eine Periode (32 x 128) Palettenindizes. ende=True: letzte Schicht ohne Schmalstein (nur fuer die Ende-Haelfte)."""
    arr = np.full((ROWS, PERIOD), FACE, int)
    noise = hash_noise((ROWS, PERIOD), 282 + (0 if kind == "sockel" else 7) + (0 if ver == "a" else 3), 0.10)
    if kind == "sockel":
        j2 = [j for j in J2 if not (ende and j == 108)]
        fase(arr, j2)
        if ver == "a":
            course(arr, noise, 4, 16, j2, False, 0, 3)
        else:
            course(arr, noise, 4, 8, j2, False, 0, 3)
            arr[8, :] = JOINT                       # Kehle
            arr[9, :] = SHADOW
            for r, v in zip(range(10, 16), (12, 13, 13, 12, 10, 8)):   # Wulst
                arr[r, :] = v
                for j in j2:
                    arr[r, j % PERIOD] = v - 3
        course(arr, noise, HJ, 32, J1, True, 0, 11)
    else:
        if ver == "a":
            ft = list(F_TOP)
            course(arr, noise, 0, 16, ft, False, 0, 5)
            fl = [j for j in F_LOW if not (ende and j == 100)]
            course(arr, noise, HJ, 24, fl, True, 0, 9)
            schraege(arr, 24, 32, fl)
        else:
            lip = [0, 72]
            course(arr, noise, 0, 4, lip, False, 0, 5)
            arr[3, :] = np.where(arr[3, :] > JOINT, SHADOW + 1, JOINT)
            xs = np.arange(PERIOD)
            xm = xs % 32
            piers = (xm >= 30) | (xm <= 1)
            for r in range(4, 24):
                for x in xs:
                    if piers[x]:
                        v = FACE - (1 if r == 23 else 0)
                        if xm[x] == 30:
                            v = FACE + 2
                        elif xm[x] == 1:
                            v = FACE - 2
                        arr[r, x] = v
            arr[HJ, piers] = JOINT
            yy, xx = np.mgrid[0:ROWS, 0:PERIOD]
            xl = (xx % 32) + 0.5
            yc = yy + 0.5

            def lens(r):
                return ((xl - (2 + 21)) ** 2 + (yc - 24) ** 2 < r * r) & ((xl - (30 - 21)) ** 2 + (yc - 24) ** 2 < r * r) & (yc < 24)
            inner, outer = lens(21), lens(22.5)
            rim = outer & ~inner
            arr[(rim) & (xl < 16) & (yy >= 4) & (yy < 24)] = 13
            arr[(rim) & (xl >= 16) & (yy >= 4) & (yy < 24)] = 10
            arr[inner & (yy >= 4) & (yy < 24)] = JOINT
            arr[inner & (yy >= 17) & (yy < 24)] = SHADOW
            schraege(arr, 24, 32, [])
    return np.clip(arr, 0, 14)


def cap_anfang(a):
    a = a.copy()
    a[:, 0] = np.where(a[:, 0] > JOINT, np.minimum(a[:, 0] + 2, 14), a[:, 0])
    return a


def cap_ende(a):
    a = a.copy()
    a[:, -1] = np.where(a[:, -1] > JOINT, np.maximum(a[:, -1] - 3, JOINT + 1), a[:, -1])
    return a


def pieces(kind, ver):
    n = band_tile(kind, ver)
    e = band_tile(kind, ver, True)
    anfang = cap_anfang(n[:, 0:64])
    mitte = n[:, 64:128]
    mitte = np.concatenate([n[:, 64:128], n[:, 0:64]], axis=1)     # pattern[64:192] mod 128
    ende = cap_ende(e[:, 64:128])
    return anfang, mitte, ende


PIECES = {(k, v): pieces(k, v) for k in ("sockel", "fries") for v in ("a", "b")}


def run_strip(kind, ver, n):
    a, m, e = PIECES[(kind, ver)]
    parts = [a] + [m] * (n - 1) + [e]
    s = np.concatenate(parts, axis=1)
    assert s.shape[1] == 128 * n
    return s


# ---- Pruefungen der Baender
for (k, v), (a, m, e) in PIECES.items():
    tile = band_tile(k, v)
    three = np.tile(tile, (1, 3))
    assert (three[:, 0:128] == three[:, 128:256]).all() and (three[:, 128:256] == three[:, 256:384]).all(), "Mitte nicht nahtlos"
    assert a.shape == (32, 64) and m.shape == (32, 128) and e.shape == (32, 64)
    # Mitte wiederholt: Naht zwischen zwei Kacheln gleicht einer Stelle mitten in der Kachel (keine Stoerung an der Naht)
    mm = np.concatenate([m, m], axis=1)
    assert (mm[:, 64:192] == np.concatenate([m[:, 64:128], m[:, 0:64]], axis=1)).all()
    for n in (1, 2, 5):
        s = run_strip(k, v, n)
        assert s.shape[1] == 128 * n
        assert (s[:, :64] == a).all() and (s[:, -64:] == e).all()
        if n > 1:
            assert (s[:, 64:64 + 128 * (n - 1)] == np.tile(m, (1, n - 1))).all()
    # Anfang und Ende unterscheiden sich von der Mitte-Haelfte nur durch Abschluss und Schmalsteinregel
    assert (a[:, 1:] == tile[:, 1:64]).sum() > 0.97 * a[:, 1:].size
    for arr in (a, m, e):
        assert arr.min() >= 0 and arr.max() <= 14


def all_stein(arr_rgb):
    return {tuple(int(v) for v in p) for p in arr_rgb.reshape(-1, 3)} <= STEIN_SET


# waagerechte Lagerfugen liegen auf dem 16-px-Raster (0,25 m)
assert all(r % 16 == 0 for r in H_JOINT_ROWS)
for r in range(1, 32):
    if (band_tile("sockel", "a")[r, :] == JOINT).all():
        assert r % 16 == 0, "Lagerfuge neben dem Raster"
for kind in ("sockel", "fries"):
    for ver in ("a", "b"):
        t = band_tile(kind, ver)
        for r in range(1, 24):
            if (t[r, :] == JOINT).sum() >= 100 and ver == "a":
                assert r % 16 == 0, (kind, ver, r)
# keine Schmalsteine (Stossfugen mindestens 12 px auseinander, auch ueber die Naht)
for js in (J1, J2, F_TOP, F_LOW, [j for j in J2 if j != 108], [j for j in F_LOW if j != 100]):
    s = sorted(js)
    gaps = [(s[(i + 1) % len(s)] - s[i]) % PERIOD or PERIOD for i in range(len(s))]
    assert min(gaps) >= 12, gaps


# ------------------------------------------------------------------ Ausgabe von Bildern
def save(arr, path):
    a = np.clip(np.asarray(arr), 0, 255).astype(np.float32)[::-1]
    h, w = a.shape[:2]
    img = bpy.data.images.new(os.path.basename(path), w, h)
    out = np.ones((h, w, 4), np.float32)
    out[..., :3] = a / 255.0
    img.pixels.foreach_set(out.ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)
    print("Bild:", path)


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
    im = bpy.data.images.load(tmp, check_existing=False)
    im.colorspace_settings.name = "Non-Color"
    a = np.array(im.pixels[:], np.float32).reshape(hd, wd, 4)[::-1, :, 3]
    bpy.data.images.remove(im)
    hh, ww = img.shape[:2]
    ye, xe = min(hh, y + hd), min(ww, x + wd)
    if ye <= y or xe <= x:
        return
    mk = (a[:ye - y, :xe - x] > 0.5)[..., None]
    img[y:ye, x:xe] = np.where(mk, np.array(color, float), img[y:ye, x:xe])


# Texturen schreiben
for (k, v), (a, m, e) in PIECES.items():
    for name, arr in (("anfang", a), ("mitte", m), ("ende", e)):
        t = rgb(arr)
        assert all_stein(t)
        save(t, os.path.join(TEX_DIR, "%s_%s_%s.png" % (k, v, name)))

# ------------------------------------------------------------------ Geometrie und Rasterung
GRID = 64.0
FOG_COLOR = psx.FOG_COLOR
BAYER4 = psx.BAYER4
R_DOOR = 1.5625


def shade(n):
    nx, ny, nz = n
    s = abs(nx) + abs(ny) + abs(nz)
    return (psx.WALL_PX * max(nx, 0) + psx.WALL_MX * max(-nx, 0) + psx.WALL_PZ * max(nz, 0) + psx.WALL_MZ * max(-nz, 0)
            + psx.FLOOR_B * max(ny, 0) + psx.CEILING_B * max(-ny, 0)) / s


def solid(idx, size=4):
    return np.tile(rgb(np.full((1, 1), idx))[0, 0], (size, size, 1))


class Quad:
    def __init__(self, O, U, V, tex, n, sx=64.0, sy=64.0, tu0=0.0, tv0=0.0, mask=None, wrap=True, arch=False):
        self.O, self.U, self.V = np.array(O, float), np.array(U, float), np.array(V, float)
        self.tex, self.n = tex, np.array(n, float)
        self.sx, self.sy, self.tu0, self.tv0, self.mask, self.wrap = sx, sy, tu0, tv0, mask, wrap
        self.lu, self.lv = np.linalg.norm(self.U), np.linalg.norm(self.V)
        self.arch = arch
        self.b = shade(self.n)


QUADS = []


def addq(*a, **k):
    q = Quad(*a, **k)
    QUADS.append(q)
    return q


def pointed(s, y, hw):
    """Spitzbogen-Oeffnung (Tuer): Kaempfer 2,0 m, Radius 1,5625 m, Mitte s = 0."""
    dy = y - 2.0
    cl, cr = -hw + R_DOOR, hw - R_DOOR
    arch = ((s - cl) ** 2 + dy ** 2 < R_DOOR ** 2) & ((s - cr) ** 2 + dy ** 2 < R_DOOR ** 2)
    return np.where(dy < 0, np.abs(s) < hw, arch)


class Run:
    """Ein Wandlauf: Ursprung (x, z), Laufrichtung (dx, dz), Normale (nx, nz); s = Meter entlang des Laufs."""
    def __init__(self, origin, d, n):
        self.o, self.d, self.n = origin, d, n

    def P(self, s, y, off=0.0):
        return (self.o[0] + self.d[0] * s + self.n[0] * off, y, self.o[1] + self.d[1] * s + self.n[1] * off)

    def vec(self, ds, dy, doff):
        return (self.d[0] * ds + self.n[0] * doff, dy, self.d[1] * ds + self.n[1] * doff)


def n3(run, up=0.0, out=1.0):
    return (run.n[0] * out, up, run.n[1] * out)


def add_sockel(run, s0, s1, ver):
    cells = round((s1 - s0) / 2)
    strip = rgb(run_strip("sockel", ver, cells))
    n = n3(run)
    # Vorderflaeche: z = 1/16 vor der Wand, y 0 bis 7/16 (untere 28 px)
    addq(run.P(s0, 0, 1 / 16), run.vec(s1 - s0, 0, 0), run.vec(0, 7 / 16, 0), strip, n, tv0=0)
    # Fase: von (1/16, 7/16) nach (0, 1/2)
    nn = (run.n[0] * 0.7071, 0.7071, run.n[1] * 0.7071)
    addq(run.P(s0, 7 / 16, 1 / 16), run.vec(s1 - s0, 0, 0), run.vec(0, 1 / 16, -1 / 16), strip, nn, sy=4 / (math.sqrt(2) / 16), tv0=28)
    for s, sg in ((s0, -1), (s1, 1)):
        addq(run.P(s, 0, 0), run.vec(0, 0, 1 / 16), run.vec(0, 0.5, 0), solid(9), (run.d[0] * sg, 0, run.d[1] * sg))


def add_fries(run, s0, s1, ver, H):
    cells = round((s1 - s0) / 2)
    strip = rgb(run_strip("fries", ver, cells))
    n = n3(run)
    # Schraege: von (0, H-0,5) nach (1/8, H-0,375), untere 8 px
    nn = (run.n[0] * 0.7071, -0.7071, run.n[1] * 0.7071)
    addq(run.P(s0, H - 0.5, 0), run.vec(s1 - s0, 0, 0), run.vec(0, 1 / 8, 1 / 8), strip, nn, sy=8 / (math.sqrt(2) / 8), tv0=0)
    addq(run.P(s0, H - 0.375, 1 / 8), run.vec(s1 - s0, 0, 0), run.vec(0, 0.375, 0), strip, n, tv0=8)
    for s, sg in ((s0, -1), (s1, 1)):
        addq(run.P(s, H - 0.5, 0), run.vec(0, 0, 1 / 8), run.vec(0, 0.5, 0), solid(9), (run.d[0] * sg, 0, run.d[1] * sg))


def add_flaeche(run, s0, s1, y0, y1, sbase=0.0, mask=None):
    addq(run.P(s0, y0, 0), run.vec(s1 - s0, 0, 0), run.vec(0, y1 - y0, 0), WALL, n3(run),
         tu0=(s0 - sbase) * 64 + 0.0, tv0=y0 * 64.0, mask=mask)


def add_door(run, s0, H, ver, full):
    """Tuerzelle (2 m): Wandrest ausserhalb des Bogens, Rahmen (0,125 m Pfostenband), Leere dahinter."""
    c = s0 + 1.0
    top = H if not full else H - 0.5
    outer = lambda u, v: ~pointed(u - 1.0, v, 1.0)
    ring = lambda u, v: pointed(u - 1.0, v, 1.0) & ~pointed(u - 1.0, v, 0.875)
    addq(run.P(s0, 0, 0), run.vec(2, 0, 0), run.vec(0, top, 0), WALL, n3(run), tu0=s0 * 64.0, tv0=0.0,
         mask=lambda u, v: ~pointed(u - 1.0, v, 1.0))
    # Rahmenring leicht vor der Wand, Farbe Stein, Lichtkante links
    ringtex = np.tile(rgb(np.array([[12, 12, 12, 12]]))[0][None], (4, 1, 1)).reshape(4, 4, 3)
    addq(run.P(s0, 0, 0.02), run.vec(2, 0, 0), run.vec(0, top, 0), ringtex, n3(run),
         mask=lambda u, v: ring(u, v))
    # Leere hinter der Oeffnung
    addq(run.P(s0, 0, -0.03), run.vec(2, 0, 0), run.vec(0, top, 0), solid(0), n3(run), mask=lambda u, v: pointed(u - 1.0, v, 0.875))


def build_scene(H, ver):
    QUADS.clear()
    A = Run((0, 0), (1, 0), (0, 1))
    # Sockel: Laufstuecke 0-4 und 6-8; Abschluss: im Gang ebenso, im Raum (6 m) durchgehend ueber der Tuer
    for (s0, s1) in ((0, 4), (6, 8)):
        add_sockel(A, s0, s1, ver)
        add_flaeche(A, s0, s1, 0.5, H - 0.5)
    fries_segs = ((0, 4), (6, 8)) if H < 5 else ((0, 8),)
    for (s0, s1) in fries_segs:
        add_fries(A, s0, s1, ver, H)
    add_door(A, 4, H, ver, full=H >= 5)
    # Rueckwand links (Innenecke): laeuft von z=4 nach z=0, Ende an der Ecke
    B = Run((0, 4), (0, -1), (1, 0))
    add_sockel(B, 0, 4, ver)
    add_flaeche(B, 0, 4, 0.5, H - 0.5)
    add_fries(B, 0, 4, ver, H)
    # Seitenwand des Durchgangs (Aussenecke) bei x = 8: Anfang an der Ecke
    C = Run((8, 0), (0, -1), (1, 0))
    add_sockel(C, 0, 4, ver)
    add_flaeche(C, 0, 4, 0.5, H - 0.5)
    add_fries(C, 0, 4, ver, H)
    # Boden, Decke, Hintergrund
    ft = np.full((128, 128), 5, int)
    ft[0, :] = 3
    ft[:, 0] = 3
    fl = rgb(ft + hash_noise((128, 128), 77, 0.08))
    ct = np.full((128, 128), 3, int)
    ct[0, :] = 2
    ct[:, 0] = 2
    ce = rgb(ct)
    addq((0, 0, -4), (12, 0, 0), (0, 0, 16), fl, (0, 1, 0), sx=64, sy=64, mask=lambda u, v: ((v > 4) | ((u > 8) & (u < 10))))
    addq((0, H, -4), (12, 0, 0), (0, 0, 16), ce, (0, -1, 0), sx=64, sy=64, mask=lambda u, v: ((v > 4) | ((u > 8) & (u < 10))))
    addq((0, 0, -4), (12, 0, 0), (0, H, 0), solid(1), (0, 0, 1))
    return list(QUADS)


# ---- Kontrolle: Architektur liegt auf dem 64er-Raster (1 px = 1/64 m); Tuerbogen und Boden ausgenommen
def check_raster(quads, name):
    for q in quads:
        if q.arch or q.mask is not None or q.tex.shape[0] == 128:
            continue
        for p in (q.O, q.O + q.U, q.O + q.V):
            for c in p:
                assert abs(c * 64 - round(c * 64)) < 1e-6, (name, p)


# ------------------------------------------------------------------ Wandpfeiler aus dem GLB
def load_glb(path):
    b = open(path, "rb").read()
    l = struct.unpack_from("<I", b, 12)[0]
    js = json.loads(b[20:20 + l])
    off = 20 + l
    bl = struct.unpack_from("<I", b, off)[0]
    bin_ = b[off + 8:off + 8 + bl]

    def acc(i):
        a = js["accessors"][i]
        bv = js["bufferViews"][a["bufferView"]]
        dt = {5126: "<f4", 5123: "<u2", 5125: "<u4"}[a["componentType"]]
        nc = {"SCALAR": 1, "VEC2": 2, "VEC3": 3}[a["type"]]
        o = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
        arr = np.frombuffer(bin_, dtype=dt, count=a["count"] * nc, offset=o)
        return arr.reshape(a["count"], nc) if nc > 1 else arr
    p = js["meshes"][0]["primitives"][0]
    pos, nor, uv, ind = acc(p["attributes"]["POSITION"]), acc(p["attributes"]["NORMAL"]), acc(p["attributes"]["TEXCOORD_0"]), acc(p["indices"])
    img = js["images"][0]
    bv = js["bufferViews"][img["bufferView"]]
    pngb = bin_[bv.get("byteOffset", 0):bv.get("byteOffset", 0) + bv["byteLength"]]
    tp = os.path.join(TMP, "pillar_atlas.png")
    open(tp, "wb").write(pngb)
    im = bpy.data.images.load(tp, check_existing=False)
    im.colorspace_settings.name = "Non-Color"
    w, h = im.size
    tex = np.round(np.array(im.pixels[:]).reshape(h, w, 4)[..., :3] * 255).astype(np.uint8)[::-1].copy()
    bpy.data.images.remove(im)
    return pos.astype(float), nor.astype(float), uv.astype(float), ind.astype(int).reshape(-1, 3), tex


PILLARS = {H: load_glb(os.path.join(HERE, "eingang", "wall_pillar_%s_a.glb" % ("corridor" if H < 5 else "room"))) for H in (3.5, 6.0)}


# ------------------------------------------------------------------ Kamera und Bild
class Cam:
    def __init__(self, pos, tgt, size, fov=70.0):
        self.C = np.array(pos, float)
        f = np.array(tgt, float) - self.C
        self.f = f / np.linalg.norm(f)
        r = np.cross(self.f, (0, 1, 0))
        self.r = r / np.linalg.norm(r)
        self.u = np.cross(self.r, self.f)
        self.W, self.Hh = size
        self.ty = math.tan(math.radians(fov) / 2)
        self.tx = self.ty * self.W / self.Hh
        xs = ((np.arange(self.W) + 0.5) / self.W * 2 - 1) * self.tx
        ys = (1 - (np.arange(self.Hh) + 0.5) / self.Hh * 2) * self.ty
        self.dirs = self.f[None, None, :] + xs[None, :, None] * self.r[None, None, :] + ys[:, None, None] * self.u[None, None, :]
        self.dl = np.linalg.norm(self.dirs, axis=2)

    def proj(self, P):
        v = np.asarray(P, float) - self.C
        z = v @ self.f
        return (v @ self.r) / z / self.tx, (v @ self.u) / z / self.ty, z


def render(quads, cam, mode, tris=None, tris_off=(0, 0, 0)):
    W, Hh = cam.W, cam.Hh
    zb = np.full((Hh, W), np.inf)
    col = np.zeros((Hh, W, 3))
    covered = np.zeros((Hh, W), bool)
    for q in quads:
        corners = [q.O, q.O + q.U, q.O + q.V, q.O + q.U + q.V]
        pr = [cam.proj(c) for c in corners]
        sl = (slice(0, Hh), slice(0, W))
        if all(p[2] > 0.05 for p in pr):
            xs = [(p[0] + 1) / 2 * W for p in pr]
            ys = [(1 - p[1]) / 2 * Hh for p in pr]
            x0, x1 = max(0, int(math.floor(min(xs))) - 1), min(W, int(math.ceil(max(xs))) + 2)
            y0, y1 = max(0, int(math.floor(min(ys))) - 1), min(Hh, int(math.ceil(max(ys))) + 2)
            if x1 <= x0 or y1 <= y0:
                continue
            sl = (slice(y0, y1), slice(x0, x1))
        d = cam.dirs[sl]
        den = d @ q.n
        num = float((q.O - cam.C) @ q.n)
        with np.errstate(divide="ignore", invalid="ignore"):
            t = num / den
        P = cam.C[None, None, :] + d * t[..., None]
        rel = P - q.O
        uu = (rel @ q.U) / (q.lu ** 2) * q.lu
        vv = (rel @ q.V) / (q.lv ** 2) * q.lv
        ok = (np.abs(den) > 1e-9) & (t > 0.02) & (uu >= 0) & (uu < q.lu) & (vv >= 0) & (vv < q.lv)
        if q.mask is not None:
            ok &= q.mask(uu, vv)
        zv = t
        ok &= zv < zb[sl]
        if not ok.any():
            continue
        th, tw = q.tex.shape[:2]
        tx = np.floor(uu * q.sx + q.tu0).astype(int) % tw
        ty = np.floor(vv * q.sy + q.tv0).astype(int) % th
        c = q.tex[th - 1 - ty, tx].astype(float)
        sub = col[sl]
        sub[ok] = c[ok] * q.b
        col[sl] = sub
        z = zb[sl]
        z[ok] = zv[ok]
        zb[sl] = z
        cv = covered[sl]
        cv[ok] = True
        covered[sl] = cv
    if tris is not None:
        pos, nor, uv, ind, tex = tris
        pos = pos + np.array(tris_off)
        th, tw = tex.shape[:2]
        pp = [cam.proj(p) for p in pos]
        px = np.array([(p[0] + 1) / 2 * W for p in pp])
        py = np.array([(1 - p[1]) / 2 * Hh for p in pp])
        pz = np.array([p[2] for p in pp])
        for tri in ind:
            if (pz[tri] < 0.05).any():
                continue
            xs, ys, zs = px[tri], py[tri], pz[tri]
            x0, x1 = max(0, int(math.floor(xs.min()))), min(W, int(math.ceil(xs.max())) + 1)
            y0, y1 = max(0, int(math.floor(ys.min()))), min(Hh, int(math.ceil(ys.max())) + 1)
            if x1 <= x0 or y1 <= y0:
                continue
            gx, gy = np.meshgrid(np.arange(x0, x1) + 0.5, np.arange(y0, y1) + 0.5)
            den = (ys[1] - ys[2]) * (xs[0] - xs[2]) + (xs[2] - xs[1]) * (ys[0] - ys[2])
            if abs(den) < 1e-9:
                continue
            l0 = ((ys[1] - ys[2]) * (gx - xs[2]) + (xs[2] - xs[1]) * (gy - ys[2])) / den
            l1 = ((ys[2] - ys[0]) * (gx - xs[2]) + (xs[0] - xs[2]) * (gy - ys[2])) / den
            l2 = 1 - l0 - l1
            ins = (l0 >= -1e-6) & (l1 >= -1e-6) & (l2 >= -1e-6)
            if not ins.any():
                continue
            iz = l0 / zs[0] + l1 / zs[1] + l2 / zs[2]
            z = 1 / iz
            ok = ins & (z < zb[y0:y1, x0:x1])
            if not ok.any():
                continue
            u = (l0 * uv[tri[0], 0] / zs[0] + l1 * uv[tri[1], 0] / zs[1] + l2 * uv[tri[2], 0] / zs[2]) * z
            v = (l0 * uv[tri[0], 1] / zs[0] + l1 * uv[tri[1], 1] / zs[1] + l2 * uv[tri[2], 1] / zs[2]) * z
            tx = np.clip(np.floor(u * tw).astype(int), 0, tw - 1)
            ty = np.clip(np.floor(v * th).astype(int), 0, th - 1)
            c = tex[ty, tx].astype(float) * shade(nor[tri[0]])
            sub = col[y0:y1, x0:x1]
            sub[ok] = c[ok]
            col[y0:y1, x0:x1] = sub
            zz = zb[y0:y1, x0:x1]
            zz[ok] = z[ok]
            zb[y0:y1, x0:x1] = zz
            cv = covered[y0:y1, x0:x1]
            cv[ok] = True
            covered[y0:y1, x0:x1] = cv
    dist = zb * cam.dl
    if mode == "hell":
        out = np.clip(col * 1.3, 0, 255)
        out = np.where(covered[..., None], out, np.array((46.0, 40.0, 52.0)))
        return out
    d = np.where(covered, dist, 0)[..., None]
    c = col * (1 - (1 - psx.LANTERN_MINIMUM) * np.clip(d / psx.FOG_END, 0, 1))
    tt = np.clip((d - psx.FOG_START) / (psx.FOG_END - psx.FOG_START), 0, 1)
    fog = tt * tt * (3 - 2 * tt)
    c = np.where(covered[..., None], c * (1 - fog) + FOG_COLOR * fog, FOG_COLOR)
    ys, xs = np.mgrid[0:Hh, 0:W]
    thr = BAYER4[ys % 4, xs % 4][..., None]
    return np.clip(np.floor((c + (thr - 0.5) * 8) / 8) * 8 + 4, 0, 255)


COLW = 900
PH = int(COLW * 720 / 1280)


def fit(img, tw, th):
    xi = (np.arange(tw) * img.shape[1] / tw).astype(int)
    yi = (np.arange(th) * img.shape[0] / th).astype(int)
    return img[yi][:, xi]


def views(H, ver):
    quads = build_scene(H, ver)
    check_raster(quads, "H%s%s" % (H, ver))
    tag = "%s_%s" % (("3m5" if H < 5 else "6m"), ver)
    up = 1.6 + 3 * math.tan(math.radians(22)) if H > 5 else 1.6
    cams = {
        "nah_links": ((2.5, 1.6, 3.0), (2.5, up, 0.0)),
        "nah_rechts": ((7.0, 1.6, 3.0), (7.0, up, 0.0)),
        "weit": ((4.5, 1.6, 8.0), (4.5, 2.4 if H < 5 else 3.0, 0.0)),
        "ecke": ((11.2, 1.6, 2.0), (8.0, 1.6 if H < 5 else 2.6, -0.5)),
    }
    out = {}
    for name, (pos, tgt) in cams.items():
        im = render(quads, Cam(pos, tgt, (1280, 720)), "hell")
        save(im, os.path.join(VIEW_DIR, "%s_%s_hell.png" % (tag, name)))
        out[name] = fit(im, COLW, PH).astype(np.uint8)
    for name in ("nah_links", "weit"):
        pos, tgt = cams[name]
        im = render(quads, Cam(pos, tgt, (854, 480)), "spiel")
        save(im, os.path.join(VIEW_DIR, "%s_%s_spiel.png" % (tag, name)))
        out[name + "_spiel"] = fit(im, COLW, PH).astype(np.uint8)
    # Wandpfeiler davor (steht auf x = 2 m, dort sitzt ein Pfeiler des Bogenfrieses)
    pil = PILLARS[H]
    pos, tgt = (2.0, 1.6, 3.0), (2.0, up, 0.0)
    im = render(quads, Cam(pos, tgt, (1280, 720)), "hell", pil, (2.0, 0, 0))
    save(im, os.path.join(VIEW_DIR, "%s_pfeiler_hell.png" % tag))
    out["pfeiler"] = fit(im, COLW, PH).astype(np.uint8)
    im = render(quads, Cam(pos, tgt, (854, 480)), "spiel", pil, (2.0, 0, 0))
    save(im, os.path.join(VIEW_DIR, "%s_pfeiler_spiel.png" % tag))
    out["pfeiler_spiel"] = fit(im, COLW, PH).astype(np.uint8)
    return out


def main():
    res = {}
    for H in (3.5, 6.0):
        for ver in ("a", "b"):
            res[(H, ver)] = views(H, ver)
    # ---- Blatt
    colw, gap = COLW, 20
    ph = PH
    BG = np.array((74.0, 66.0, 82.0))
    rows = []
    for H, hn in ((3.5, "Gang 3,5 m"), (6.0, "Raum 6 m")):
        rows.append((hn, [("nah_links", "von 3 m, links: Innenecke, Tuer (hell)"), ("nah_rechts", "von 3 m, rechts: Tuer, 1-Zellen-Stueck, Durchgang (hell)"),
                          ("weit", "ganze Wand (hell)"), ("nah_links_spiel", "Spielbeleuchtung von 3 m"), ("ecke", "Aussenecke am Durchgang (hell)"),
                          ("pfeiler", "mit Wandpfeiler davor (hell)"), ("pfeiler_spiel", "Wandpfeiler in Spielbeleuchtung"), ("weit_spiel", "ganze Wand in Spielbeleuchtung")], H))
    head = 230
    tex_h = 4 * (64 + 40) + 60
    total = head + tex_h
    for hn, lst, H in rows:
        total += 50 + len(lst) * (ph + 40)
    sheet = np.zeros((total, 2 * colw + gap, 3), np.float32) + BG.astype(np.float32)
    text(sheet, "Silver & Ash - Wandzonen und Baender, Probestueck zu Issue #282 (Katakomben, Hauptpalette Stein)", 12, 8, 34, (240, 232, 210))
    text(sheet, "links: Version a (schlicht: Sockel aus grossen Quadern mit Fase, glattes Gesims)   rechts: Version b (reicher: Sockel mit Profil, Spitzbogenfries)", 12, 58, 24)
    text(sheet, "Zonen: Sockel 0-0,5 m | Flaeche | Abschluss 0,5 m unter der Decke; Fugen auf dem 64er-Raster; Ansichten aus Augenhoehe 1,6 m", 12, 92, 22, (210, 205, 220))
    y = 130
    # Texturstreifen
    for c, ver in enumerate(("a", "b")):
        x0 = c * (colw + gap)
        text(sheet, "Version %s: Anfang | Mitte | Mitte | Ende  (x2, 128 px = 1 Zelle)" % ver, x0, y, 22, (225, 220, 232))
    y += 40
    for kind, lab in (("sockel", "Sockel"), ("fries", "Abschluss")):
        for c, ver in enumerate(("a", "b")):
            x0 = c * (colw + gap)
            a, m, e = PIECES[(kind, ver)]
            strip = np.concatenate([a, m, m, e], axis=1)
            im = np.repeat(np.repeat(rgb(strip), 2, axis=0), 2, axis=1)
            sheet[y:y + im.shape[0], x0:x0 + im.shape[1]] = im
            text(sheet, lab, x0 + im.shape[1] + 8, y + 18, 22)
        y += 64 + 12
    y += 10
    for hn, lst, H in rows:
        text(sheet, hn, 12, y, 30, (240, 232, 210))
        y += 50
        for key, lab in lst:
            for c, ver in enumerate(("a", "b")):
                x0 = c * (colw + gap)
                text(sheet, "%s - %s" % (ver, lab), x0 + 4, y, 22)
                sheet[y + 32:y + 32 + ph, x0:x0 + colw] = res[(H, ver)][key]
            y += ph + 40
    save(sheet[:y + 10], os.path.join(HERE, "blatt-282.png"))
    # Texturblatt gross
    tb = np.zeros((4 * 150 + 40, 1580, 3)) + BG
    yy = 10
    for kind in ("sockel", "fries"):
        for ver in ("a", "b"):
            a, m, e = PIECES[(kind, ver)]
            strip = np.concatenate([a, m, m, m, e], axis=1)
            im = np.repeat(np.repeat(rgb(strip), 3, axis=0), 3, axis=1)
            tb[yy:yy + im.shape[0], 10:10 + im.shape[1]] = im
            text(tb, "%s %s: Anfang | Mitte x3 | Ende" % (kind, ver), 10, yy + 98, 22)
            yy += 150
    save(tb, os.path.join(HERE, "texturen-blatt.png"))


main()
