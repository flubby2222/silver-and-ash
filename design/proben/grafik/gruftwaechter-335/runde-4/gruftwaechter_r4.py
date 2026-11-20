"""Probestueck Gruftwaechter, Runde 4 (Issue #335): das Original (Runde 2, Fassung F) behutsam bereinigt und ausgebaut.

Urteil des Menschen zu Runde 3: "Fand das Original schon ganz ok, nicht grossartig veraendern, nur ausbauen, bereinigen,
vllt. etwas mehr Tiefe in die Texturen bringen." Runde 3 (F1 bis F3) ist abgelehnt: zu weit vom Original (andere Farben,
andere Ausruestung). Darum hier KEIN neuer Entwurf, sondern zwei Stufen derselben Figur:

  G1 behutsam: Gestalt, Haltung, Farben und Ausruestung wie das Original. Nichts durchdringt sich mehr, der Stab ist
     kuerzer, die Texturen haben Tiefe (Falten, dunkle und helle Kanten, Abnutzung).
  G2 ausgebaut: G1 plus kleine Einzelheiten (Guertel mit Schnalle, Schienenriemen mit Schnallen, lose Binden-Enden,
     gegliederte Kettenglieder, Laternenring). Der Umriss bleibt derselbe.

Wie "kein Clipping" erreicht wird (Technik aus Runde 3, hier auf die Gestalt des Originals angewendet):
  * Der Rumpf ist EIN verschweisster Koerper. Seine Ringe entstehen als Vereinigung derselben Ellipsoide, aus denen das
    Original besteht (Becken, Wanst, Brust, Rueckenbuckel, Halsring), Hoehe fuer Hoehe radial gemessen. So bleibt die
    Silhouette, ohne dass sich Teile durchstecken.
  * Ketten, Guertel, Binden, Schnallen, Schulterlamellen werden per Strahl auf die FERTIGE Oberflaeche gesetzt.
  * Alle Koerper werden paarweise mit einem Dreiecks-Durchdringungstest (BVH) geprueft, in Ruhe und in der Ankuendigung.
    Erlaubt ist nur der Waffenschaft in der Manschette (Regel 23). Ergebnis: pruefung.txt.

Alles entsteht aus diesem Skript, nichts wird heruntergeladen, unter assets/ wird nur gelesen (Ghul und Gorm).
Das Skript ist aus Teilen zusammengesetzt (Rumpf-Technik aus Runde 3 unveraendert uebernommen).

Aufruf (Repository-Wurzel):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/gruftwaechter-335/runde-4/gruftwaechter_r4.py
Umgebungsvariablen (nur Entwicklung): GW_OUT = Ausgabeordner, GW_QUICK = Fassung (G1 oder G2): nur eine Probe rendern.

Koordinaten: Spiel-Achsen (x seitlich, y oben, z vorn), Meter, Ursprung zwischen den Fuessen am Boden.
Texturfelder: y von unten (Zeile 0 = unten) wie die UV-Achse v; vorn = u 0,5.
"""

import json
import math
import os
import sys
import tempfile

import bmesh
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE)))))
OUT = os.environ.get("GW_OUT", HERE)
QUICK = os.environ.get("GW_QUICK", "")
sys.path.insert(0, os.path.join(ROOT, "tools", "models"))
import blf  # noqa: E402
import bpy  # noqa: E402
import imbuf  # noqa: E402
from mathutils import Matrix  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402
from psx import (V, G, FOG_COLOR, Part, rects_from_pixels, vnoise, save_image, export_material,  # noqa: E402
                 pass_material, setup_render, camera, aim, render_passes, compose, save_png, write_light,
                 stone_textures, build_room, export_glb, to_game, tube, MATS)

MODELS = os.path.join(ROOT, "assets", "models")
R2_GLB = os.path.join(ROOT, "design", "proben", "grafik", "gruftwaechter-335", "runde-2", "modelle", "gruftwaechter_F.glb")
VARIANTS = ("G1", "G2")
NAMES = {"G1": "Grabwächter, behutsam", "G2": "Grabwächter, ausgebaut"}

PAL = {r["name"]: np.array(r["farben"], float)
       for k in ("grund", "akzent")
       for r in json.load(open(os.path.join(ROOT, "assets", "palette", "hauptpalette.json")))[k]}
HK, SF, HO, EI, MG, BR = (PAL["Haut/Knochen"], PAL["Stoff"], PAL["Holz"], PAL["Eisen"], PAL["Messing/Gold"],
                          PAL["Blutrot"])

# Erlaubte Durchdringungen (Paare von Koerpernamen, Teilname vorangestellt). Nur: Waffenschaft steckt in der Manschette.
ERLAUBT = {v: [("arm_r/cuff", "pole/shaft")] for v in VARIANTS}

# --- Textur: 128 x 128, 16 Felder je 32 x 32, nur Palettenfarben, harte Stufen (kein Dithering) ---------------------
# Die Farben sind die des Originals (helle Binden, gruene Flecken, blaue Kettenhaube, blaue Ketten, Messing); neu ist
# nur die Tiefe: Faltenschatten, dunkle Nahtkanten, helle Kanten, Abnutzung. Jede Zeile eines Feldes ist 1 Pixel.

NAMES_G = ("flesh", "pale", "shroud", "tabard", "hide", "dark", "leather", "wood", "chain", "brass", "lantern", "eye",
           "coif", "face", "band", "bone")
FIELDS = {n: ((i % 4) * 32, (i // 4) * 32) for i, n in enumerate(NAMES_G)}
UV = rects_from_pixels({k: (x, y, x + 32, y + 32) for k, (x, y) in FIELDS.items()}, 128)
XS, YS = np.meshgrid(np.arange(32), np.arange(32))


def nz(cy, cx, seed):
    return vnoise(32, 32, cy, cx, seed)


def pick(layers, base):
    out = np.zeros((32, 32, 3)) + base
    for mask, col in layers:
        out[mask] = col
    return out


def rim(mask):
    """Rand einer Flaeche: Pixel der Maske, die einen Nachbarn ausserhalb haben (Kanten werden dunkel oder hell gemalt)."""
    inner = mask & np.roll(mask, 1, 0) & np.roll(mask, -1, 0) & np.roll(mask, 1, 1) & np.roll(mask, -1, 1)
    return mask & ~inner


def ramp(d, period, cols):
    """Farbverlauf in harten Stufen ueber eine Periode (Falte einer Binde: hell, Mitte, Schatten, Naht)."""
    idx = np.minimum((d * len(cols)) // period, len(cols) - 1)
    out = np.zeros((32, 32, 3))
    for i, c in enumerate(cols):
        out[idx == i] = c
    return out


def wrap(base_cols, seed, period=16, slope=2, stain=0.86, deep=None):
    """Diagonal gewickelte Binde: je Wickel ein Verlauf, dunkle Naht, helle Kante, Flecken und Risse."""
    d = (XS + YS * slope) % period
    out = ramp(d, period, base_cols)
    n = nz(3, 3, seed)
    out[n > stain] = base_cols[-2]
    out[rim(n > stain) & (n > stain)] = base_cols[-1]
    if deep is not None:
        tear = (nz(2, 7, seed + 1) > 0.9) & (d % 4 == 0)
        out[tear] = deep
    return out


def f_flesh():
    n, m = nz(6, 4, 211), nz(8, 5, 212)
    out = pick([(n > 0.6, HK[3]), (rim(n > 0.6), HK[3]), (n < 0.25, HK[1]), (np.abs(m - 0.5) < 0.03, HK[1]),
                (YS % 11 == 5, HK[1]), (YS % 11 == 6, HK[3])], HK[2])
    out[rim(n > 0.6) & (YS % 2 == 0)] = HK[2]
    return out


def f_pale():
    out = wrap([HK[5], HK[5], HK[5], HK[5], HK[5], HK[4], HK[4], HK[3]], 221, period=14, slope=1, stain=0.84, deep=HK[3])
    out[(YS % 12 == 5) & (nz(2, 6, 222) > 0.45)] = HK[3]
    return out


def f_shroud():
    return wrap([HK[6], HK[5], HK[5], HK[5], HK[5], HK[5], HK[4], HK[3]], 231, period=18, slope=2, stain=0.85, deep=HK[3])


def f_band():
    """Lose Binde: heller Streifen mit dunkler Kante und Faserlinien."""
    out = pick([(XS % 6 == 2, HK[6]), (XS % 6 == 4, HK[4]), (nz(3, 3, 241) > 0.88, HK[3])], HK[5])
    out[(XS < 3) | (XS > 28)] = HK[4]
    out[(XS < 1) | (XS > 30)] = HK[3]
    return out


def f_hide():
    n1, n2, n3 = nz(6, 4, 251), nz(8, 5, 252), nz(4, 7, 253)
    out = pick([(n1 > 0.5, HK[2]), (rim(n1 > 0.5), HK[0]), (n2 > 0.72, HK[3]), (rim(n2 > 0.72), HK[2]),
                (n3 < 0.1, HK[0])], HK[1])
    out[(n1 > 0.5) & (YS % 9 == 0) & (n3 > 0.4)] = HK[3]
    return out


def f_tabard():
    stripe = (XS >= 13) & (XS <= 18)
    n = nz(4, 6, 261)
    out = pick([(n > 0.62, SF[4]), (n < 0.25, SF[2]), (XS % 8 == 2, SF[2]), (XS % 8 == 3, SF[4]),
                (stripe, MG[2]), (stripe & (YS % 6 < 2), MG[3]), (stripe & (XS == 13), MG[3]),
                (stripe & (XS == 18), SF[2])], SF[3])
    out[(YS < 4) & (nz(2, 9, 262) > 0.4)] = EI[1]                       # ausgefranster unterer Rand
    out[(YS < 2)] = EI[1]
    out[(YS > 29)] = SF[2]
    return out


def f_dark():
    n = nz(5, 5, 271)
    return pick([(n > 0.7, EI[1]), (n < 0.15, EI[3]), ((YS % 16) == 0, EI[1]), ((YS % 16) == 1, EI[3]),
                 (((XS % 8) == 2) & ((YS % 16) == 8), EI[4]), (nz(4, 4, 272) > 0.9, HO[2])], EI[2])


def f_leather():
    n = nz(4, 4, 281)
    return pick([(n > 0.6, HO[2]), (rim(n > 0.6), HO[3]), (n < 0.25, HO[1]), ((YS % 16) == 3, HO[1]),
                 (((YS % 16) == 2) & (XS % 4 < 2), HO[3])], HO[2])


def f_wood():
    return pick([((XS % 6) == 0, HO[1]), ((XS % 6) == 1, HO[3]), (nz(2, 6, 291) > 0.78, HO[3]),
                 (nz(8, 3, 292) > 0.93, HO[1])], HO[2])


def f_chain():
    """Kettenglieder als ovale Ringe: heller Rand, dunkle Mitte, versetzt (Glieder laufen entlang v)."""
    out = np.zeros((32, 32, 3)) + EI[2]
    for row in range(8):
        y0 = row * 4
        xo = 0 if row % 2 == 0 else 4
        for cx in range(xo, 32, 8):
            for (dx, dy, c) in ((1, 0, EI[5]), (2, 0, EI[5]), (0, 1, EI[4]), (3, 1, EI[3]), (1, 3, EI[3]), (2, 3, EI[1])):
                out[y0 + dy, (cx + dx) % 32] = c
            out[y0 + 1:y0 + 3, (cx + 1) % 32] = EI[1]
            out[y0 + 1:y0 + 3, (cx + 2) % 32] = EI[1]
    out[(YS % 8 == 7)] = EI[1]
    return out


def f_brass():
    n = nz(3, 3, 301)
    return pick([(n > 0.6, MG[4]), (n < 0.3, MG[2]), ((XS > 11) & (XS < 17), MG[5]), (YS % 16 == 0, MG[2]),
                 (YS % 16 == 1, MG[5]), (rim(np.ones((32, 32), bool)), MG[2])], MG[3])


def f_lantern():
    glow = (YS > 4) & (YS < 28) & (XS > 3) & (XS < 29)
    return pick([(glow, MG[5]), (glow & (nz(3, 3, 321) > 0.5), MG[4]), (glow & (YS % 8 < 1), EI[2]),
                 (glow & (XS % 8 == 0), EI[2]), (glow & (XS % 8 == 1), MG[3])], EI[2])


def f_coif():
    out = pick([(((XS + YS) % 4) < 2, EI[3]), (((XS - YS) % 4) == 0, EI[1]), (nz(3, 3, 341) > 0.8, EI[4]),
                ((YS % 16) == 0, EI[1])], EI[2])
    out[(((XS + YS) % 4) == 0) & (nz(5, 5, 342) > 0.6)] = EI[4]
    return out


def f_face():
    """Gesicht auf der Platte: Schaedel, Augenhoehlen, Nase, schiefer Mund; die Augen-Platten liegen auf den Hoehlen."""
    ell = ((XS - 16) / 15.0) ** 2 + ((YS - 15) / 15.0) ** 2
    out = pick([(ell < 1, HK[5]), (ell < 1, HK[5]), ((ell > 0.55) & (ell < 1), HK[4]), (rim(ell < 1), HK[3])], EI[0])
    for x0 in (4, 20):
        out[17:24, x0:x0 + 8] = EI[0]
    out[(YS >= 9) & (YS <= 14) & (XS >= 15) & (XS <= 17)] = HK[3]
    out[(YS == 6) & (XS >= 9) & (XS <= 22)] = EI[0]
    out[(YS == 5) & (XS % 3 == 0) & (XS >= 10) & (XS <= 21)] = HK[7]
    out[~(ell < 1)] = EI[0]
    return out


def f_bone():
    return pick([(nz(3, 3, 331) > 0.6, HK[7]), (nz(5, 5, 332) > 0.88, HK[4]), (YS % 9 == 4, HK[5])], HK[6])


def f_eye(warn):
    return pick([(YS >= 10, BR[6])], BR[5]) if warn else np.zeros((32, 32, 3)) + EI[1]


def make_atlas(state):
    a = np.zeros((128, 128, 3)) + EI[0]
    fns = {"flesh": f_flesh, "pale": f_pale, "shroud": f_shroud, "tabard": f_tabard, "hide": f_hide, "dark": f_dark,
           "leather": f_leather, "wood": f_wood, "chain": f_chain, "brass": f_brass, "lantern": f_lantern,
           "coif": f_coif, "face": f_face, "band": f_band, "bone": f_bone}
    for name, fn in fns.items():
        x0, y0 = FIELDS[name]
        a[y0:y0 + 32, x0:x0 + 32] = fn()
    x0, y0 = FIELDS["eye"]
    a[y0:y0 + 32, x0:x0 + 32] = f_eye(state == "ankuendigung")
    return a




# --- Koerper: aufgezeichnete Dreiecke fuer den Durchdringungstest ----------------------------------------------------

class Rec(Part):
    """Teil, das jede Oberflaeche unter einem Koerpernamen aufzeichnet (Spiel-Koordinaten, Meter)."""

    def __init__(self, name, pivot, rects):
        super().__init__(name, pivot, rects)
        self.bodies, self.gverts = {}, {}

    def _note(self, grp, faces):
        tris = self.bodies.setdefault(grp, [])
        vs = self.gverts.setdefault(grp, [])
        for f in faces:
            if not f.is_valid:
                continue
            pts = [tuple(to_game(v.co) + self.pivot) for v in f.verts]
            for i in range(1, len(pts) - 1):
                tris.append((pts[0], pts[i], pts[i + 1]))
            vs.extend(f.verts)

    def _orient(self, f, hint):
        f.normal_update()
        if to_game(f.normal).dot(hint) < 0:
            f.normal_flip()

    def surf(self, rings, rect, cs, ce):
        """Ringe verbinden. cs/ce: 'dome' (Pol 25 % darueber), 'flat' (flache Kappe) oder None."""
        u0, v0, u1, v1 = self.rects[rect]
        n, m = len(rings[0]), len(rings)
        vs = [[self.bm.verts.new(G(p - self.pivot)) for p in ring] for ring in rings]
        cen = [sum(r, V(0, 0, 0)) / n for r in rings]
        faces = []

        def uv(k, i):
            return (u0 + (u1 - u0) * k / n, v0 + (v1 - v0) * i / max(m - 1, 1))

        for i in range(m - 1):
            hint = (cen[i] + cen[i + 1]) / 2
            for k in range(n):
                f = self._face((vs[i][k], vs[i][(k + 1) % n], vs[i + 1][(k + 1) % n], vs[i + 1][k]),
                               (uv(k, i), uv(k + 1, i), uv(k + 1, i + 1), uv(k, i + 1)))
                mid = (rings[i][k] + rings[i][(k + 1) % n] + rings[i + 1][(k + 1) % n] + rings[i + 1][k]) / 4
                self._orient(f, mid - hint)
                faces.append(f)
        for idx, mode in ((0, cs), (m - 1, ce)):
            if mode is None:
                continue
            nb = 1 if idx == 0 else m - 2
            away = cen[idx] - cen[nb]
            apex = cen[idx] + (away * 0.25 if mode == "dome" else V(0, 0, 0))
            pole = self.bm.verts.new(G(apex - self.pivot))
            for k in range(n):
                f = self._face((vs[idx][k], vs[idx][(k + 1) % n], pole),
                               (uv(k, idx), uv(k + 1, idx), (uv(k, idx)[0] + (u1 - u0) / (2 * n), uv(k, idx)[1])))
                self._orient(f, (rings[idx][k] + rings[idx][(k + 1) % n] + apex) / 3 - (cen[idx] - away))
                faces.append(f)
        return faces

    def body(self, grp, rings, rects, cs="flat", ce="flat"):
        """Ein Koerper aus Abschnitten (rects je Band oder ein Name), an den Ringen verschweisst."""
        g = self.name + "/" + grp
        if isinstance(rects, str):
            rects = [rects] * (len(rings) - 1)
        assert len(rects) == len(rings) - 1, (grp, len(rects), len(rings))
        spans, i0 = [], 0
        for i in range(1, len(rects) + 1):
            if i == len(rects) or rects[i] != rects[i0]:
                spans.append((i0, i, rects[i0]))
                i0 = i
        for j, (a, b, r) in enumerate(spans):
            self._note(g, self.surf(rings[a:b + 1], r, cs if j == 0 else None, ce if j == len(spans) - 1 else None))
        uniq = list({v.index if False else id(v): v for v in self.gverts[g] if v.is_valid}.values())
        bmesh.ops.remove_doubles(self.bm, verts=uniq, dist=1e-5)
        return g

    def solid(self, grp, points, offset, rect):
        """Konvexes Prisma (Marken, Schnallen, Klingen, Fahnen)."""
        g = self.name + "/" + grp
        self._note(g, self.prism(points, offset, rect))
        return g

    def spike_g(self, grp, base, tip, half, rect, sides=4):
        g = self.name + "/" + grp
        self._note(g, self.spike(base, tip, half, rect, sides))
        return g


# --- Pose: Verwindung des Oberkoerpers (Ausholen aus der Huefte) ------------------------------------------------------

class Pose:
    def __init__(self, lean=0.0, twist=0.0, hip=(0.0, 0.92, 0.0)):
        self.lean, self.twist, self.H = lean, twist, V(*hip)

    def rot(self, w=1.0):
        return Matrix.Rotation(math.radians(self.twist * w), 3, "Y") @ Matrix.Rotation(math.radians(self.lean * w), 3, "X")

    def __call__(self, p, w=1.0):
        return self.H + self.rot(w) @ (V(*p) - self.H)

    def dir(self, d):
        return self.rot(1.0) @ V(*d)

    def w(self, y):
        t = min(max((y - 1.0) / 0.55, 0.0), 1.0)
        return t * t * (3 - 2 * t)


POSE = Pose()


def A(k, n):
    return math.pi / 2 + 2 * math.pi * (k - n / 2) / n          # vorn (z+) bei k = n/2, Naht (u = 0) hinten


def hring(y, cx, cz, rx, rzf, rzb, n=12, wob=None, w=None):
    """Waagrechter Ring (Ellipse, vorn und hinten verschieden tief), dann mit der Verwindung nach Hoehe bewegt."""
    w = POSE.w(y) if w is None else w
    pts = []
    for k in range(n):
        a = A(k, n)
        s, c = math.sin(a), math.cos(a)
        f = wob[k * len(wob) // n] if wob else 1.0
        pts.append(POSE(V(cx + rx * c * f, y, cz + (rzf if s > 0 else rzb) * s * f), w))
    return pts


def interp(keys, y):
    y = min(max(y, keys[0][0]), keys[-1][0])
    for a, b in zip(keys, keys[1:]):
        if a[0] <= y <= b[0]:
            t = (y - a[0]) / (b[0] - a[0])
            return [a[i] + (b[i] - a[i]) * t for i in range(1, 6)]
    raise ValueError(y)


def torso_rings(keys, spec, n=16):
    """spec: (y, Wulst, Rechteck[, wob]); Wulst vergroessert alle Halbachsen. Rueckgabe Ringe und Rechtecke je Band."""
    rings, rects = [], []
    for i, row in enumerate(spec):
        y, bump, rect = row[:3]
        wob = row[3] if len(row) > 3 else None
        cx, cz, rx, rzf, rzb = interp(keys, y)
        rings.append(hring(y, cx, cz, rx + bump, rzf + bump, rzb + bump, n, wob))
        if i:
            rects.append(rect)
    return rings, rects


# --- Raycast auf die fertigen Koerper ---------------------------------------------------------------------------------

def bvh_for(parts, prefixes):
    verts, polys = [], []
    for p in parts:
        for g, tris in p.bodies.items():
            if any(g.startswith(x) for x in prefixes):
                for t in tris:
                    i = len(verts)
                    verts += [V(*q) for q in t]
                    polys.append((i, i + 1, i + 2))
    return BVHTree.FromPolygons(verts, polys)


def ray(parts, prefixes, origin, direction, off=0.0):
    d = V(*direction).normalized()
    loc, nrm, _, _ = bvh_for(parts, prefixes).ray_cast(V(*origin), d)
    assert loc is not None, f"Strahl trifft nichts: {prefixes} {tuple(origin)} {tuple(direction)}"
    if nrm.dot(d) > 0:
        nrm = -nrm
    return loc + nrm * off, nrm.normalized()


def frame_from(nrm, up_hint=(0, 1, 0)):
    """Rechts, oben, aussen (= nrm) auf der Oberflaeche."""
    z = V(*nrm).normalized()
    up = V(*up_hint)
    x = up.cross(z)
    if x.length < 0.2:
        x = V(1, 0, 0).cross(z)
    x.normalize()
    return x, z.cross(x).normalized(), z


def plate_on(rec, grp, parts, prefixes, origin, direction, w, h, thick, rect, gap=0.004, up_hint=(0, 1, 0), tilt=0.0):
    """Flache Platte (Marke, Schnalle, Auge) aufgelegt: Strahl auf die Oberflaeche, Platte im Abstand gap."""
    p, n = ray(parts, prefixes, origin, direction, gap)
    ex, ey, ez = frame_from(n, up_hint)
    if tilt:
        c, s = math.cos(math.radians(tilt)), math.sin(math.radians(tilt))
        ex, ey = ex * c + ey * s, ey * c - ex * s
    pts = [p + ex * (-w / 2) + ey * (-h / 2), p + ex * (w / 2) + ey * (-h / 2),
           p + ex * (w / 2) + ey * (h / 2), p + ex * (-w / 2) + ey * (h / 2)]
    return rec.solid(grp, pts, ez * thick, rect)


def pr(p):
    """Punkt der Ruhe-Pose in die aktuelle Pose (Gewicht nach Hoehe wie beim Rumpf)."""
    p = V(*p)
    return POSE(p, POSE.w(p.y))


def pd(d, y):
    return POSE.rot(POSE.w(y)) @ V(*d)


def strap(rec, grp, parts, prefixes, a, b, w, steps, off, rect="chain", thick=0.007, zfar=3.0, back=False):
    """Band auf der Oberflaeche: Stuetzpunkte (Ruhe-Koordinaten) per Strahl, Abstand off, Querschnitt flach (Breite w)."""
    pts = []
    sgn = 1.0 if back else -1.0
    for j in range(steps + 1):
        x = a[0] + (b[0] - a[0]) * j / steps
        y = a[1] + (b[1] - a[1]) * j / steps
        d = pd((0, 0, sgn), y)
        o = pr((x, y, -zfar if back else zfar))
        p, n = ray(parts, prefixes, o, d, off)
        pts.append(p)
    rec.body(grp, tube(pts, [(w, thick, thick)] * len(pts), 6, V(0, 0, 1)), rect)


def panel(rec, grp, cx, ytop, ybot, ztop, zbot, wtop, wbot, t, rect, jag, steps=3):
    """Haengende Bahn (Waffenrock, Umhang): flacher Koerper aus Sechseckringen, unterer Rand ausgefranst."""
    rings = []
    for i in range(steps + 1):
        f = i / steps                      # 0 unten ... 1 oben
        y = ybot + (ytop - ybot) * f
        z = zbot + (ztop - zbot) * f
        w = wbot + (wtop - wbot) * f
        pts = []
        for k, (sx, sz) in enumerate(((-1, 1), (0, 1.4), (1, 1), (1, -1), (0, -1.4), (-1, -1))):
            pts.append(V(cx + sx * w, y + (jag[k] if i == 0 else 0.0), z + sz * t))
        rings.append(pts)
    rec.body(grp, rings, rect)


def rod(rec, grp, pts, radii, rect, sides=6, cs="flat", ce="flat", ref=None):
    cs_ = [V(*q) for q in pts]
    axis = cs_[-1] - cs_[0]
    r_ = ref if ref is not None else (V(0, 0, 1) if abs(axis.normalized().y) > 0.7 else V(0, 1, 0))
    shapes = [(r, r, r) if not isinstance(r, tuple) else r for r in radii]
    return rec.body(grp, tube(cs_, shapes, sides, V(*r_)), rect, cs, ce)


def ik(sh, hand, bend, l1=0.5, l2=0.5):
    sh, hand = V(*sh), V(*hand)
    d = hand - sh
    dist = d.length
    assert dist < (l1 + l2) * 0.999, f"Arm zu kurz: {dist:.2f}"
    a = (l1 * l1 - l2 * l2 + dist * dist) / (2 * dist)
    h = math.sqrt(max(l1 * l1 - a * a, 0.0))
    dn = d.normalized()
    b = V(*bend)
    b = (b - dn * b.dot(dn)).normalized()
    return sh + dn * a + b * h


def rec_part(name, pivot):
    return Rec(name, pivot, UV)




# --- Neue Hilfen Runde 4 -----------------------------------------------------------------------------------------------

def body_q(rec, grp, rings, rectfn, cs="flat", ce="flat", period=5):
    """Wie Rec.body, aber das Rechteck (Material) wird je Viereck gewaehlt: rectfn(band, segment). Die v-Achse wiederholt
    sich alle period Baender (Felder sind in v nahtlos)."""
    g = rec.name + "/" + grp
    n, m = len(rings[0]), len(rings)
    vs = [[rec.bm.verts.new(G(p - rec.pivot)) for p in ring] for ring in rings]
    cen = [sum(r, V(0, 0, 0)) / n for r in rings]
    faces = []
    for i in range(m - 1):
        hint = (cen[i] + cen[i + 1]) / 2
        for k in range(n):
            u0, v0, u1, v1 = rec.rects[rectfn(i, k)]
            va = v0 + (v1 - v0) * (i % period) / period
            vb = v0 + (v1 - v0) * (i % period + 1) / period
            ua, ub = u0 + (u1 - u0) * k / n, u0 + (u1 - u0) * (k + 1) / n
            f = rec._face((vs[i][k], vs[i][(k + 1) % n], vs[i + 1][(k + 1) % n], vs[i + 1][k]),
                          ((ua, va), (ub, va), (ub, vb), (ua, vb)))
            mid = (rings[i][k] + rings[i][(k + 1) % n] + rings[i + 1][(k + 1) % n] + rings[i + 1][k]) / 4
            rec._orient(f, mid - hint)
            faces.append(f)
    for idx, mode, bi in ((0, cs, 0), (m - 1, ce, m - 2)):
        if mode is None:
            continue
        nb = 1 if idx == 0 else m - 2
        away = cen[idx] - cen[nb]
        apex = cen[idx] + (away * 0.25 if mode == "dome" else V(0, 0, 0))
        pole = rec.bm.verts.new(G(apex - rec.pivot))
        u0, v0, u1, v1 = rec.rects[rectfn(bi, 0)]
        vv = v0 + (v1 - v0) * (bi % period + (0 if idx == 0 else 1)) / period
        for k in range(n):
            ua, ub = u0 + (u1 - u0) * k / n, u0 + (u1 - u0) * (k + 1) / n
            f = rec._face((vs[idx][k], vs[idx][(k + 1) % n], pole), ((ua, vv), (ub, vv), ((ua + ub) / 2, vv)))
            rec._orient(f, (rings[idx][k] + rings[idx][(k + 1) % n] + apex) / 3 - (cen[idx] - away))
            faces.append(f)
    rec._note(g, faces)
    return g


def conform(rec, grp, parts, prefixes, origin, direction, pts2d, thick, rect, gap=0.004, up_hint=(0, 1, 0), reach=1.0):
    """Platte, die sich der Oberflaeche anschmiegt: jede Ecke des Umrisses (2D im Rahmen der Oberflaeche) wird per
    Strahl auf die fertige Flaeche gesetzt (Abstand gap), die Oberseite liegt thick darueber. So schwebt auch eine breite
    Platte auf gewoelbter Flaeche nirgends. UV der Oberseite: planar ueber den Umriss."""
    g = rec.name + "/" + grp
    d = V(*direction).normalized()
    p0, n0 = ray(parts, prefixes, origin, d, 0.0)
    ex, ey, ez = frame_from(n0, up_hint)
    inner, outer = [], []
    for x, y in pts2d:
        q, nq = ray(parts, prefixes, p0 + ex * x + ey * y + ez * reach, -ez, gap)
        inner.append(q)
        outer.append(q + nq * thick)
    n = len(pts2d)
    vi = [rec.bm.verts.new(G(q - rec.pivot)) for q in inner]
    vo = [rec.bm.verts.new(G(q - rec.pivot)) for q in outer]
    u0, v0, u1, v1 = rec.rects[rect]
    xs, ys = [p[0] for p in pts2d], [p[1] for p in pts2d]
    sx, sy = max(max(xs) - min(xs), 1e-4), max(max(ys) - min(ys), 1e-4)
    uvp = [(u0 + (u1 - u0) * (x - min(xs)) / sx, v0 + (v1 - v0) * (y - min(ys)) / sy) for x, y in pts2d]
    cu = ((u0 + u1) / 2, (v0 + v1) / 2)
    cin = sum(inner, V(0, 0, 0)) / n
    cout = sum(outer, V(0, 0, 0)) / n
    faces = []
    top = rec._face(vo, uvp)
    rec._orient(top, cout - cin)
    bot = rec._face(list(reversed(vi)), list(reversed(uvp)))
    rec._orient(bot, cin - cout)
    faces += [top, bot]
    mid_all = (cin + cout) / 2
    for i in range(n):
        j = (i + 1) % n
        f = rec._face((vi[i], vi[j], vo[j], vo[i]), (cu, cu, cu, cu))
        rec._orient(f, (inner[i] + inner[j] + outer[i] + outer[j]) / 4 - mid_all)
        faces.append(f)
    rec._note(g, faces)
    return g


def ngon(rx, ry, n=8, rot=0.0):
    return [(rx * math.cos(2 * math.pi * k / n + rot), ry * math.sin(2 * math.pi * k / n + rot)) for k in range(n)]


def rectpts(w, h):
    return [(-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2)]


def ring_at(rings, ys, y):
    """Ring auf Hoehe y: Ecken zwischen zwei Ringen linear gemischt (liegt genau auf den Flaechen des Koerpers)."""
    for i in range(len(ys) - 1):
        if ys[i] - 1e-9 <= y <= ys[i + 1] + 1e-9:
            t = (y - ys[i]) / (ys[i + 1] - ys[i])
            return [a + (b - a) * t for a, b in zip(rings[i], rings[i + 1])]
    raise ValueError(y)


def band(rec, grp, ra, rb, o_in, o_out, rect):
    """Gurt oder Schiene rund um einen Koerper: Torus aus den Ringen ra (unten) und rb (oben), innen o_in, aussen o_out
    ueber der Flaeche (waagrecht nach aussen). Liegt auf den Flaechen des Koerpers, durchdringt ihn nicht."""
    n = len(ra)
    ca, cb = sum(ra, V(0, 0, 0)) / n, sum(rb, V(0, 0, 0)) / n
    loop = []
    for k in range(n):
        da = V(ra[k].x - ca.x, 0, ra[k].z - ca.z).normalized()
        db = V(rb[k].x - cb.x, 0, rb[k].z - cb.z).normalized()
        loop.append([ra[k] + da * o_in, ra[k] + da * o_out, rb[k] + db * o_out, rb[k] + db * o_in])
    g = rec.name + "/" + grp
    fs = rec.loft(loop, rect, closed=True)
    bmesh.ops.recalc_face_normals(rec.bm, faces=fs)
    rec._note(g, fs)
    return g


def band_around(rec, grp, c, axis, r_in, r_out, half, rect, n=8):
    """Ring um einen runden Stab (Griffband): Torus um die Achse axis bei c."""
    axis = V(*axis).normalized()
    e1 = perp((1, 0, 0), axis)
    e2 = axis.cross(e1).normalized()
    loop = []
    for k in range(n):
        th = 2 * math.pi * k / n
        r_ = e1 * math.cos(th) + e2 * math.sin(th)
        loop.append([c + r_ * r_in - axis * half, c + r_ * r_out - axis * half, c + r_ * r_out + axis * half,
                     c + r_ * r_in + axis * half])
    g = rec.name + "/" + grp
    fs = rec.loft(loop, rect, closed=True)
    bmesh.ops.recalc_face_normals(rec.bm, faces=fs)
    rec._note(g, fs)
    return g


def ribbon(rec, grp, pts, widths, thick, rect, ref):
    """Flaches Band (Raute) entlang pts; widths je Punkt; ref = Richtung 'oben' (zeigt von der Flaeche weg)."""
    return rec.body(grp, tube(pts, [(w, thick, thick) for w in widths], 4, V(*ref)), rect)


# --- Rumpf als Vereinigung der Ellipsoide des Originals ------------------------------------------------------------------

TORSO_ELL = (((0, 1.0, 0.0), (.40, .22, .30)),        # Becken
             ((0, 1.22, .20), (.52, .42, .46)),       # Wanst
             ((0, 1.68, .06), (.50, .36, .38)),       # Brust
             ((0, 1.78, -.20), (.40, .30, .25)))      # Rueckenbuckel
DISC = ((0, 1.795, .46), (.29, .05, .29))             # Halsring (Eisenkragen)
NECK_Y, NECK_Z = 1.846, 0.30
T_Y = [.935, .98, 1.02, 1.065, 1.10, 1.16, 1.22, 1.32, 1.42, 1.52, 1.62, 1.70, 1.74, 1.762, 1.795, 1.828, 1.84, 1.846,
       1.90, 1.96, 2.02, 2.06]
N_T = 16


def sections(y):
    out = []
    for c, r in TORSO_ELL:
        dy = (y - c[1]) / r[1]
        if abs(dy) < 0.999:
            s = math.sqrt(1 - dy * dy)
            out.append((c[0], c[2], r[0] * s, r[2] * s))
    dy = (y - DISC[0][1]) / DISC[1][1]
    if abs(dy) < 1.0:
        s = (1 - dy ** 8) ** 0.5
        out.append((0.0, DISC[0][2], DISC[1][0] * s, DISC[1][2] * s))
    return out


def radial(secs, C, a):
    dx, dz = math.cos(a), math.sin(a)
    best = 0.0
    for cx, cz, rx, rz in secs:
        ox, oz = C[0] - cx, C[1] - cz
        A_ = (dx / rx) ** 2 + (dz / rz) ** 2
        B_ = ox * dx / rx ** 2 + oz * dz / rz ** 2
        C_ = (ox / rx) ** 2 + (oz / rz) ** 2 - 1.0
        D_ = B_ * B_ - A_ * C_
        if D_ < 0:
            continue
        t = (-B_ + math.sqrt(D_)) / A_
        if t > best:
            best = t
    return best


def torso_ring(y, n=N_T):
    secs = sections(y)
    ws = [s[2] * s[3] for s in secs]
    cx = sum(s[0] * w for s, w in zip(secs, ws)) / sum(ws)
    cz = sum(s[1] * w for s, w in zip(secs, ws)) / sum(ws)
    step = 2 * math.pi / n
    pts = []
    for k in range(n):
        a0 = A(k, n)
        r = sum(wt * radial(secs, (cx, cz), a0 + off * step)
                for off, wt in ((-.5, .1), (-.25, .2), (0.0, .4), (.25, .2), (.5, .1)))
        x, z = cx + r * math.cos(a0), cz + r * math.sin(a0)
        if y >= NECK_Y:
            z = min(z, NECK_Z)
        pts.append(POSE(V(x, y, z), POSE.w(y)))
    return pts


def torso_rect(i, k):
    y = (T_Y[i] + T_Y[i + 1]) / 2
    s = math.sin(A(k + .5, N_T))
    if 1.735 < y < 1.846:
        return "dark"
    if y >= 1.846:
        return "hide"
    if y < 1.12:
        return "flesh"
    if y < 1.45:
        return "shroud"
    return "hide" if s < -0.15 else "pale"


# --- Beine ---------------------------------------------------------------------------------------------------------------

LEG_SPEC = [  # y, cx, cz, rx, rzf, rzb
    (0.00, .32, .15, .100, .26, .100), (0.04, .32, .15, .145, .29, .115), (0.10, .32, .15, .150, .27, .115),
    (0.15, .32, .13, .140, .20, .115), (0.185, .32, .09, .120, .12, .115), (0.20, .325, .08, .115, .115, .115),
    (0.30, .34, .10, .125, .125, .125), (0.36, .345, .11, .130, .13, .13), (0.44, .35, .12, .140, .145, .135),
    (0.52, .36, .14, .150, .17, .145), (0.64, .35, .10, .180, .19, .17), (0.75, .33, .06, .190, .20, .18),
    (0.88, .29, .02, .165, .18, .16), (0.93, .26, .0, .125, .16, .14)]
LEG_N = 10


def make_leg(s, mats):
    """Bein als ein Koerper von der Sohle bis zur Huefte. mats: (Stiefel, Schienbein, Oberschenkel). Rueckgabe: Teil, Ringe."""
    leg = rec_part("leg_l" if s < 0 else "leg_r", (s * 0.22, 0.9, 0.0))
    boot, shin, thigh = mats
    rings, rects = [], []
    for i, (y, cx, cz, rx, rzf, rzb) in enumerate(LEG_SPEC):
        rings.append(hring(y, s * cx, cz, rx, rzf, rzb, LEG_N, w=0.0))
        if i:
            rects.append(boot if y <= 0.186 else shin if y <= 0.521 else thigh)
    leg.body("leg", rings, rects)
    return leg, rings


LEG_Y = [r[0] for r in LEG_SPEC]


# --- Kopf ----------------------------------------------------------------------------------------------------------------

HEAD_Y = [1.848, 1.90, 1.95, 2.00, 2.05, 2.10, 2.15, 2.19]
HEAD_CZ = 0.535


def head_r(y):
    r = 0.0
    for c, rr in (((0, 1.98), (.165, .19)), ((0, 2.02), (.185, .185))):
        dy = (y - c[1]) / rr[1]
        if abs(dy) < 1:
            r = max(r, rr[0] * math.sqrt(1 - dy * dy))
    return r


def make_head(parts):
    rings = [hring(y, 0, HEAD_CZ, head_r(y), head_r(y), head_r(y) * 0.95, 10) for y in HEAD_Y]
    h = rec_part("head", (0, 1.9, HEAD_CZ))
    h.body("head", rings, "coif", "flat", "dome")
    parts.append(h)
    return h


# --- Arme ----------------------------------------------------------------------------------------------------------------

def make_arm(parts, side, hand, bend, rects, radii, out=0.165, cuff_rect="dark", cuff_r=(0.105, 0.128, 0.134, 0.12),
             cuff_back=0.12, cuff_fwd=0.07, anchor=(1.68, 0.17), reach=0.5):
    """Arm an der Flanke: erster Abschnitt Schulterkappe, dann Oberarm, Ellbogen, Unterarm. Die Manschette ist ein eigener
    Koerper (verdeckt den Griff, keine Hand modelliert). hand und bend sind schon in der Pose."""
    nm = "arm_l" if side < 0 else "arm_r"
    sh = POSE(V(side * 0.6, anchor[0], anchor[1]))
    anch, n = ray(parts, ["torso/body"], (side * 2.0, sh.y, sh.z), (-side, 0, 0), 0.004)
    for dy in (-0.16, -0.12, -0.08, -0.04, 0.0, 0.04):
        q, _ = ray(parts, ["torso/body"], (side * 2.0, sh.y + dy, sh.z), (-side, 0, 0), 0.004)
        if q.x * side > anch.x * side:
            anch = V(q.x, anch.y, anch.z)
    axis = V(n.x, 0.0, n.z).normalized()
    c0 = anch + axis * 0.035
    c1 = anch + axis * out
    hand = V(*hand)
    el = ik(c1, hand, V(*bend), reach, reach)
    dn = (hand - el).normalized()
    wr = hand - dn * cuff_back
    pts = [c0, c1, c1 + (el - c1) * 0.42, el, el + (wr - el) * 0.5, wr]
    arm = rec_part(nm, (side * 0.6, 1.7, 0.2))
    arm.body("arm", tube(pts, [(r, r, r) for r in radii], 10, V(0, 0, 1) if abs(axis.y) > 0.9 else V(0, 1, 0)), rects)
    cpts = [wr + dn * 0.004, wr + dn * 0.04, hand + dn * 0.0, hand + dn * cuff_fwd]
    arm.body("cuff", tube(cpts, [(r, r, r) for r in cuff_r], 8, V(0, 1, 0)), cuff_rect, "flat", "flat")
    arm.info = (c0, c1, list(radii), V(*(axis if abs(axis.y) < 0.9 else V(1, 0, 0))))
    parts.append(arm)
    return hand, dn, el


# --- Laterne (sechseckig, ein Koerper) ----------------------------------------------------------------------------------

def lantern(rec, grp, top_pt, scale=1.0):
    """Haengt mit dem Ring an top_pt (Mitte der Oberkante): Kette, Dach, Glas, Boden. Rueckgabe: unteres Ende."""
    s = scale
    y0 = top_pt[1]
    cx, cz = top_pt[0], top_pt[2]

    def ring(y, r, n=6):
        return [V(cx + r * math.cos(2 * math.pi * k / n + math.pi / 6), y, cz + r * math.sin(2 * math.pi * k / n + math.pi / 6))
                for k in range(n)]

    chain_len = 0.12 * s
    rec.body(grp + "_kette", [ring(y0 - chain_len, 0.012, 5), ring(y0, 0.012, 5)], "chain")
    yt = y0 - chain_len - 0.004                   # Oberkante des Dachs
    ys = [yt - 0.37 * s, yt - 0.35 * s, yt - 0.34 * s, yt - 0.27 * s, yt - 0.17 * s, yt - 0.10 * s, yt - 0.09 * s, yt - 0.04 * s, yt]
    rs = [0.11, 0.11, 0.10, 0.135, 0.135, 0.105, 0.13, 0.07, 0.03]
    rects = ["dark", "dark", "lantern", "lantern", "lantern", "lantern", "brass", "brass"]
    rec.body(grp, [ring(y, r * s) for y, r in zip(ys, rs)], rects)
    return yt - 0.37 * s


def wobs(n, **kw):
    w = [1.0] * n
    for k, f in kw.items():
        w[int(k[1:])] = f
    return w


UNIT = lambda v: V(*v).normalized()

def perp(v, d):
    v = V(*v)
    d = V(*d).normalized()
    v = v - d * v.dot(d)
    return v.normalized() if v.length > 1e-4 else V(0, 0, 1)


def side_h(dn, prefer):
    """Waagrechte Richtung senkrecht zum Schaft (damit haengende Laternen frei vom Schaft bleiben); bei fast senkrechtem
    Schaft die zu prefer passende senkrechte Richtung."""
    dn = V(*dn).normalized()
    if abs(dn.y) > 0.95:
        return perp(prefer, dn)
    h = dn.cross(V(0, 1, 0)).normalized()
    return h if h.dot(V(*prefer)) >= 0 else -h


def ank_w(dn):
    return abs(V(*dn).normalized().y) < 0.95

def steil(dn, fa, min_deg):
    """Schaftrichtung dn so drehen (in der Ebene dn/fa), dass sie mit dem Unterarm fa mindestens min_deg bildet; sonst
    laeuft der Unterarm am Schaft entlang und durchdringt ihn."""
    dn, fa = V(*dn).normalized(), V(*fa).normalized()
    c = dn.dot(fa)
    if math.degrees(math.acos(min(abs(c), 1.0))) >= min_deg:
        return dn
    pv = dn - fa * c
    pv = pv.normalized() if pv.length > 1e-3 else V(0, 1, 0)
    sg = 1.0 if c >= 0 else -1.0
    a = math.radians(min_deg)
    return (fa * (sg * math.cos(a)) + pv * math.sin(a)).normalized()


def circ(cx, y, cz, r, n=8):
    return [V(cx + r * math.cos(2 * math.pi * k / n), y, cz + r * math.sin(2 * math.pi * k / n)) for k in range(n)]


def front_x(parts, x, y, off):
    p, n = ray(parts, ["torso/body"], pr((x, y, 3.0)), pd((0, 0, -1), y), off)
    return p, n


def studs(g, grp, parts, pts, back=False, size=.034, rect="brass", thick=.012):
    """Nieten und kleine Flicken (Ruhe-Koordinaten x, y) auf Brust oder Ruecken, je ein flacher Quader."""
    for k, (x, y) in enumerate(pts):
        o = pr((x, y, -3.0 if back else 3.0))
        d = pd((0, 0, 1 if back else -1), y)
        plate_on(g, f"{grp}{k}", parts, ["torso/body"], o, d, size, size * .8, thick, rect, gap=.010)

# --- Pruefungen --------------------------------------------------------------------------------------------------------

def clip_check(parts, allowed):
    """Paarweise Durchdringungstest aller Koerper (BVH) plus Selbstdurchdringung. Rueckgabe: alle Paare, nicht erlaubte
    Paare, Selbstdurchdringungen."""
    trees = {}
    for p in parts:
        for gname, tris in p.bodies.items():
            verts, polys = [], []
            for t in tris:
                i = len(verts)
                verts += [V(*q) for q in t]
                polys.append((i, i + 1, i + 2))
            trees[gname] = (BVHTree.FromPolygons(verts, polys, all_triangles=True), tris)
    names = sorted(trees)
    found = []
    for i, a in enumerate(names):
        for b in names[i + 1:]:
            ov = trees[a][0].overlap(trees[b][0])
            if ov:
                t = trees[a][1][ov[0][0]]
                c = tuple(round(sum(q[k] for q in t) / 3, 2) for k in range(3))
                found.append((a, b, len(ov), c))
    ok = {frozenset(x) for x in allowed}
    bad = [f for f in found if frozenset((f[0], f[1])) not in ok]
    selfx = []
    for gname in names:
        tree, tris = trees[gname]
        keys = [{tuple(round(c, 5) for c in q) for q in t} for t in tris]
        n = sum(1 for a, b in tree.overlap(tree) if a < b and not (keys[a] & keys[b]))
        if n:
            selfx.append((gname, n))
    return found, bad, selfx


def tri_of(parts):
    return {p.name: sum(len(t) for t in p.bodies.values()) for p in parts}


# --- Szene ----------------------------------------------------------------------------------------------------------

HOME = {}


def put_in_scene(parts, mat, tag):
    coll = bpy.data.collections.new(tag)
    bpy.context.scene.collection.children.link(coll)
    objs = [p.build(coll, mat, smooth=True) for p in parts]
    for o in objs:
        HOME[o.name] = o.matrix_world.copy()
    return objs


def place(objs, pos=(0, 0, 0)):
    for o in objs:
        o.matrix_world = Matrix.Translation(G(pos)) @ HOME[o.name]
    bpy.context.view_layer.update()
    for o in objs:
        write_light(o)


def tri_count(objs):
    return {o.name: len(o.data.polygons) for o in objs}


def extent(objs):
    lo, hi = np.full(3, 1e9), np.full(3, -1e9)
    for o in objs:
        for c in o.bound_box:
            gp = to_game(o.matrix_world @ V(*c))
            lo, hi = np.minimum(lo, (gp.x, gp.y, gp.z)), np.maximum(hi, (gp.x, gp.y, gp.z))
    return lo, hi


def closed_report(objs):
    out = {}
    for o in objs:
        bm = bmesh.new()
        bm.from_mesh(o.data)
        open_e = sum(1 for e in bm.edges if len(e.link_faces) == 1)
        multi = sum(1 for e in bm.edges if len(e.link_faces) > 2)
        vol = 0.0
        for f in bm.faces:
            a, b, c = (vv.co for vv in f.verts)
            vol += a.dot(b.cross(c)) / 6.0
        out[o.name] = (len(o.data.polygons), open_e, multi, vol)
        bm.free()
    return out


def import_path(path, tag):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    objs = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    cache = {}
    for o in objs:
        image = next(n.image for n in o.active_material.node_tree.nodes if n.type == "TEX_IMAGE")
        if image.name not in cache:
            cache[image.name] = pass_material("cmp_" + tag + "_" + image.name, image)
        o.data.materials.clear()
        o.data.materials.append(cache[image.name])
        o["smooth"] = True
        HOME[o.name] = o.matrix_world.copy()
    return objs


def text(img, s, x, y, size=30, color=(235, 235, 235), bold=False):
    """Schrift ins Feld img (Zeile 0 oben): text(img, s, x, y, size, color, bold)."""
    f = blf.load("C:/Windows/Fonts/" + ("arialbd.ttf" if bold else "arial.ttf"))
    blf.size(f, size)
    w, h = blf.dimensions(f, s)
    W, H = int(w) + 8, int(size * 1.6)
    assert W < 4000 and H < 400, f"Schrift unplausibel: {s!r} {size}"
    ib = imbuf.new((W, H))
    blf.color(f, 1, 1, 1, 1)
    blf.position(f, 4, int(size * 0.4), 0)
    with blf.bind_imbuf(f, ib):
        blf.draw_buffer(f, s)
    tmp = os.path.join(tempfile.gettempdir(), "gruftwaechter_r4_txt.png")
    ib.filepath = tmp
    imbuf.write(ib)
    im = bpy.data.images.load(tmp)
    im.colorspace_settings.name = "Non-Color"
    a = np.array(im.pixels[:], np.float32).reshape(H, W, 4)[::-1, :, 3]
    bpy.data.images.remove(im)
    hh, ww = img.shape[:2]
    ye, xe = min(hh, y + H), min(ww, x + W)
    m = (a[:ye - y, :xe - x] > 0.5)[..., None]
    img[y:ye, x:xe] = np.where(m, np.array(color, float), img[y:ye, x:xe])


def render_p(visible, cam, loc, target, fov, size):
    setup_render(*size)
    cam.data.angle = math.radians(fov)
    aim(cam, loc, target)
    return render_passes("gw", visible)


def render(visible, cam, loc, target, fov, size, mode):
    return compose(render_p(visible, cam, loc, target, fov, size), mode)[::-1]


def blit(sheet, tile, x, y):
    sheet[y:y + tile.shape[0], x:x + tile.shape[1]] = tile


def up(arr, k):
    return np.repeat(np.repeat(arr, k, axis=0), k, axis=1)



# --- Kopf mit Gesicht (Viereck-Material) und Augen ----------------------------------------------------------------------

def make_head(parts):
    rings = [hring(y, 0, HEAD_CZ, head_r(y), head_r(y), head_r(y) * 0.95, 10) for y in HEAD_Y]
    h = rec_part("head", (0, 1.9, HEAD_CZ))
    body_q(h, "head", rings, lambda i, k: "face" if i in (2, 3) and k in (4, 5) else "coif", "flat", "dome")
    parts.append(h)
    e = rec_part("eyes", (0, 2.0, HEAD_CZ))
    y = 2.0
    hc = pr((0, y, HEAD_CZ))
    face, right = pd((0, 0, 1), y), pd((1, 0, 0), y)
    for s in (-1, 1):
        o = hc + right * (s * 0.05) + face * 1.0
        plate_on(e, "auge" + ("l" if s < 0 else "r"), parts, ["head/head"], o, -face, 0.040, 0.028, 0.010, "eye",
                 gap=0.002, up_hint=pd((0, 1, 0), y))
    parts.append(e)


def hexplate(rec, grp, parts, prefixes, origin, direction, r, thick, rect, gap=0.004, n=6, up_hint=(0, 1, 0)):
    """Kleines Vieleck (Schloss, Nagelkopf, Schnalle) aufgelegt; r klein genug, dass die Flaeche darunter nicht hochsteht."""
    p, nrm = ray(parts, prefixes, origin, direction, gap)
    ex, ey, ez = frame_from(nrm, up_hint)
    pts = [p + ex * (r * math.cos(2 * math.pi * k / n)) + ey * (r * math.sin(2 * math.pi * k / n)) for k in range(n)]
    return rec.solid(grp, pts, ez * thick, rect)


def chain_links(rec, grp, parts, prefixes, a, b, w, steps, off, rect="chain", thick=0.009, zmode=False, alt=True):
    """Wie strap, aber die Breite wechselt je Stuetzpunkt (Glieder: breit, schmal, breit ...)."""
    pts, ws = [], []
    for j in range(steps + 1):
        x = a[0] + (b[0] - a[0]) * j / steps
        y = a[1] + (b[1] - a[1]) * j / steps
        d = pd((0, 0, -1), y)
        o = pr((x, y, 3.0))
        p, n = ray(parts, prefixes, o, d, 0.0 if zmode else off)
        pts.append(p - d.normalized() * off if zmode else p)
        ws.append(w if (not alt or j % 2 == 0) else w * 0.62)
    rec.body(grp, tube(pts, [(q, thick, thick) for q in ws], 6, V(0, 0, 1)), rect)


# --- Stab mit Laterne (hoechstens 2,6 m mit Haken) ----------------------------------------------------------------------

POLE_BACK, POLE_FWD = 1.18, 1.18


def make_weapon(hand, dn, g2, back=POLE_BACK):
    w = rec_part("pole", hand)
    ref = V(0, 1, 0) if abs(dn.y) < 0.7 else V(0, 0, 1)
    butt = hand - dn * back
    top = hand + dn * POLE_FWD
    pts = [butt, butt + dn * 0.07, top - dn * 0.07, top]
    w.body("shaft", tube(pts, [(r, r, r) for r in (0.036, 0.03, 0.03, 0.036)], 6, ref), ["dark", "wood", "dark"])
    f = side_h(dn, pd((0, 0, 1), hand.y))
    t0 = top + dn * 0.002
    hk = [t0, t0 + dn * 0.04, t0 + dn * 0.07 + f * 0.07, t0 + dn * 0.09 + f * 0.17, t0 + dn * 0.05 + f * 0.26]
    w.body("haken", tube(hk, [(r, r, r) for r in (0.03, 0.03, 0.028, 0.025, 0.02)], 6, ref), "dark")
    end = hk[-1]
    lantern(w, "laterne", V(end.x, end.y - 0.034, end.z))
    if g2:
        for k, t in enumerate(()):
            band_around(w, f"griff{k}", hand + dn * t, dn, 0.034, 0.046, 0.011, "leather")
    return w


# --- Ausruestung --------------------------------------------------------------------------------------------------------

TORSO_ALL = ["torso/body"]
LEGS_ALL = ["leg_l/leg", "leg_r/leg"]


def gear(g2, g, parts, rings, legrings, armr, arml):
    T = TORSO_ALL
    # gekreuzte blaue Ketten
    if g2:
        chain_links(g, "kette1", parts, T, (-.40, 1.74), (.30, 1.28), .034, 10, .030)
        chain_links(g, "kette2", parts, T, (.40, 1.72), (-.30, 1.28), .034, 10, .115, zmode=True)
    else:
        chain_links(g, "kette1", parts, T, (-.40, 1.74), (.30, 1.28), .034, 10, .030, thick=.007, alt=False)
        chain_links(g, "kette2", parts, T, (.40, 1.72), (-.30, 1.28), .034, 10, .115, thick=.007, zmode=True, alt=False)
    hexplate(g, "schloss", parts, T, pr((0, 1.46, 3.0)), pd((0, 0, -1), 1.46), .052, .022, "brass", gap=.150, n=6)
    # zwei Streifen des Waffenrocks: liegen auf Bauch und Oberschenkeln
    for nm, w_, top, low in (("streifen1", .052, ((.14, 1.045), (.16, 1.0), (.18, .96)),
                              ((.22, .86, .50), (.26, .72, .42), (.31, .58, .36))),
                             ("streifen2", .070, ((-.15, 1.045), (-.159, .986)),
                              ((-.175, .90, .53), (-.19, .76, .44), (-.21, .60, .37), (-.23, .46, .32)))):
        pts_ = []
        for (x, y) in top:
            p_, n_ = ray(parts, T, pr((x, y, 3.0)), pd((0, 0, -1), y), .022)
            pts_.append(p_)
        for (x, y, z) in low:
            pts_.append(pr((x, y, z)))
        g.body(nm, tube(pts_, [(w_, .011, .011)] * len(pts_), 6, V(0, 0, 1)), "tabard")
    # Eisenkappe der linken Schulter: zwei Reifen um den Oberarm, oben hoch gewoelbt
    c0, c1, radii, axis = arml.info
    for k, (t0, t1, hh) in enumerate(((0.10, 0.52, 0.085), (0.56, 0.98, 0.065))):
        a_, b_ = c0 + (c1 - c0) * t0, c0 + (c1 - c0) * t1
        ra_ = radii[0] + (radii[1] - radii[0]) * t0 + 0.014
        rb_ = radii[0] + (radii[1] - radii[0]) * t1 + 0.014
        up_ = POSE.dir((0, 1, 0))
        arml.body(f"kappe{k}", tube([a_ + up_ * ra_, b_ + up_ * rb_],
                                    [(0.15 - 0.012 * k, 0.012, hh), (0.15 - 0.012 * k, 0.012, hh)], 8, V(0, 1, 0)), "dark")
    # Rechte Wade: Kettenband; Nagelkopf auf der Kapuze
    ra, rb = ring_at(legrings[1], LEG_Y, 0.375), ring_at(legrings[1], LEG_Y, 0.445)
    band(g, "wadenkette", ra, rb, .005, .030, "chain")
    hexplate(g, "nagel", parts, ["head/head"], pr((-.14, 2.6, HEAD_CZ - .02)), pd((0, -1, 0), 2.2), .03, .016, "dark", gap=.003, n=6)
    if g2:
        rr = lambda y0, y1: (ring_at(rings, T_Y, y0), ring_at(rings, T_Y, y1))
        a_, b_ = rr(1.07, 1.13)
        band(g, "gurt", a_, b_, .016, .040, "leather")
        hexplate(g, "schnalle", parts, T, pr((0, 1.10, 3.0)), pd((0, 0, -1), 1.10), .042, .014, "brass", gap=.041, n=4)
        # linke Wade: zwei Riemen mit Schnallen
        for k, (y0, y1) in enumerate(((0.395, 0.445),)):
            ra, rb = ring_at(legrings[0], LEG_Y, y0), ring_at(legrings[0], LEG_Y, y1)
            band(g, f"riemen{k}", ra, rb, .005, .024, "leather")
            hexplate(g, f"rschnalle{k}", parts, ["leg_l/leg"], V(-.325, (y0 + y1) / 2, 3.0), V(0, 0, -1), .020, .010,
                     "brass", gap=.025, n=4)
        # lose Verbandsenden: eines am Ruecken, eines an der linken Wade
        strap(g, "ende1", parts, T, (.16, 1.60), (.22, 1.26), .034, 4, .032, rect="band", thick=.009, back=True)


# --- Figur --------------------------------------------------------------------------------------------------------------

HANDS = {  # Ruhe-Koordinaten vor der Pose: (rechts, Stabrichtung, links)
    False: ((.62, 1.22, .72), (.04, 1, -.05), (-.70, .95, .44)),
    True: ((.80, 1.84, .84), (-.12, .40, .90), (-.56, 1.62, .98)),
}
ARM_RADII_R = (0.07, 0.15, 0.135, 0.125, 0.115, 0.095)
ARM_RADII_L = (0.07, 0.155, 0.14, 0.125, 0.115, 0.095)


def build(v, state):
    global POSE
    ank = state == "ankuendigung"
    g2 = v == "G2"
    POSE = Pose(-10.0, 16.0) if ank else Pose()
    parts = []
    legs = {}
    for s, mats in ((-1, ("leather", "shroud", "shroud")), (1, ("leather", "leather", "hide"))):
        leg, rings_ = make_leg(s, mats)
        parts.append(leg)
        legs[s] = rings_
    rings = [torso_ring(y) for y in T_Y]
    t = rec_part("torso", (0, 1.4, 0.1))
    body_q(t, "body", rings, torso_rect, "flat", "dome")
    parts.append(t)
    make_head(parts)
    hr, dn0, hl = HANDS[ank]
    hand_r = pr(hr)
    dn = pd(dn0, hr[1]).normalized()
    arml = make_arm(parts, -1, pr(hl), POSE.dir((-1, -.3, -.4)), ["hide", "hide", "hide", "shroud", "shroud"],
                    ARM_RADII_L, cuff_rect="dark")
    armr = make_arm(parts, 1, hand_r, POSE.dir((1, -.3, -.2)), ["shroud"] * 5, ARM_RADII_R, cuff_rect="leather")
    L = parts[-2]
    R = parts[-1]
    g = rec_part("gear", (0, 1.4, 0.1))
    gear(g2, g, parts, rings, (legs[-1], legs[1]), R, L)
    parts.append(g)
    fa = (armr[0] - armr[2]).normalized()
    dn = steil(dn, fa, 68.0)
    parts.append(make_weapon(armr[0], dn, g2, 0.45 if ank else POLE_BACK))
    return parts




FOCUS = (0.0, 1.55, 0.30)
BELT = (0.0, 1.10, 0.40)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    os.makedirs(os.path.join(OUT, "modelle"), exist_ok=True)
    os.makedirs(os.path.join(OUT, "texturen"), exist_ok=True)
    states = ("ruhend", "ankuendigung")
    atlas = {s: make_atlas(s) for s in states}
    pal_all = {tuple(c) for k in PAL for c in PAL[k]}
    for s, a in atlas.items():
        cols = {tuple(c) for c in a.reshape(-1, 3)}
        assert cols <= pal_all, "Farbe ausserhalb der Palette"
        assert len(cols) <= 32, f"{s}: {len(cols)} Farben"
        red = any(c in {tuple(x) for x in BR} for c in cols)
        assert red == (s == "ankuendigung"), f"{s}: Rot nur in der Ankuendigung"
    images = {s: save_image(f"atlas_{s}", a, os.path.join(OUT, "texturen", f"atlas_{s}.png")) for s, a in atlas.items()}

    # 1. Modelle, Zahlen, Pruefungen
    stats, lines, clip_lines = {}, [], []
    built = {}
    for v in VARIANTS:
        if QUICK and v != QUICK:
            continue
        for s in states:
            built[(v, s)] = build(v, s)
        mat = export_material(f"gw_{v}", images["ruhend"])
        objs = put_in_scene(built[(v, "ruhend")], mat, f"exp_{v}")
        export_glb(objs, os.path.join(OUT, "modelle", f"gruftwaechter_{v}.glb"))
        lo, hi = extent(objs)
        stats[v] = (tri_count(objs), lo, hi)
        rep = closed_report(objs)
        total = sum(r[0] for r in rep.values())
        lines.append(f"{v} {NAMES[v]}: Teile {len(objs)}, Dreiecke {total}, offene Kanten "
                     f"{sum(r[1] for r in rep.values())}, Kanten mit mehr als zwei Flaechen {sum(r[2] for r in rep.values())}, "
                     f"kleinster Rauminhalt {min(r[3] for r in rep.values()):.6f} m3 (muss > 0 sein)")
        for name, r in rep.items():
            lines.append(f"    {name}: {r[0]} Dreiecke, offen {r[1]}, mehrfach {r[2]}, Raum {r[3]:.4f}")
        assert total <= 3000, f"{v}: {total} Dreiecke"
        assert hi[1] <= 2.6, f"{v}: {hi[1]:.2f} m hoch"
        for o in objs:
            bpy.data.objects.remove(o, do_unlink=True)
    for v in VARIANTS:
        for s in states:
            if (v, s) not in built:
                continue
            P_ = built[(v, s)]
            found, bad, selfx = clip_check(P_, ERLAUBT[v])
            nb = sum(len(p.bodies) for p in P_)
            clip_lines.append(f"{v} {s}: {nb} Koerper, Paare mit Durchdringung {len(found)} (davon erlaubt "
                              f"{len(found) - len(bad)}), NICHT erlaubt {len(bad)}, Selbstdurchdringung {len(selfx)}")
            for f_ in found:
                a, b, n, c = f_
                tag = "erlaubt" if f_ not in bad else "FEHLER"
                clip_lines.append(f"    {tag}: {a} x {b} ({n} Dreieckspaare, erstes bei x {c[0]} y {c[1]} z {c[2]})")
            for gname, n in selfx:
                clip_lines.append(f"    FEHLER Selbstdurchdringung: {gname} ({n} Dreieckspaare)")
    with open(os.path.join(OUT, "pruefung.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write("Pruefung 1: je Teil im Skript (Kante mit genau einer Flaeche = offen):\n" + "\n".join(lines) + "\n\n"
                "Pruefung 2: Durchdringung aller Koerper paarweise (BVH-Dreieckstest), Ruhe und Ankuendigung.\n"
                "Erlaubt ist nur der Waffenschaft in der Manschette (keine Hand modelliert, die Manschette "
                "verdeckt den Griff).\n" + "\n".join(clip_lines) + "\n")
    with open(os.path.join(OUT, "masse.txt"), "w", encoding="utf-8", newline="\n") as f:
        for v in VARIANTS:
            if v not in stats:
                continue
            t, lo, hi = stats[v]
            f.write(f"{v} {NAMES[v]}: Dreiecke {sum(t.values())} {dict(t)}, x {lo[0]:.2f}..{hi[0]:.2f}, "
                    f"y {lo[1]:.2f}..{hi[1]:.2f}, z {lo[2]:.2f}..{hi[2]:.2f}\n")
    print("\n".join(l for l in lines if not l.startswith("    ")))
    print("\n".join(clip_lines))
    nbad = sum(1 for l in clip_lines if "FEHLER" in l)
    print("FEHLER GESAMT:", nbad)

    # 2. Szenen zum Rendern
    pm = {s: pass_material("pm_" + s, images[s]) for s in states}
    sets = {}
    for v in VARIANTS:
        if QUICK and v != QUICK:
            continue
        for s in states:
            sets[(v, s)] = put_in_scene(build(v, s), pm[s], f"r_{v}_{s}")
    for objs in sets.values():
        place(objs)
    ghoul = import_path(os.path.join(MODELS, "ghoul.glb"), "ghoul")
    gorm = import_path(os.path.join(MODELS, "gorm.glb"), "gorm")
    r2 = import_path(R2_GLB, "r2F")
    place(ghoul)
    place(gorm)
    place(r2)
    cam = camera()
    stone = stone_textures(tempfile.mkdtemp(prefix="gw_stein_"))
    room = build_room(stone)

    T = 360
    dirs = (("vorn", V(0, 0.08, 1)), ("Seite", V(1, 0.08, 0)), ("hinten", V(0, 0.08, -1)), ("schräg oben", V(0.6, 0.9, 0.7)))
    fc, bc = V(*FOCUS), V(*BELT)

    def closeups(objs_rest):
        return {
            "Brust": render(objs_rest, cam, G(fc + V(0.45, 0.10, 1.25)), G(fc), 32, (T, T), "ansicht"),
            "Bauch und Gürtel": render(objs_rest, cam, G(bc + V(0.55, 0.15, 1.15)), G(bc), 32, (T, T), "ansicht"),
        }

    tiles = {}
    for v in VARIANTS:
        if QUICK and v != QUICK:
            continue
        for nm, d in dirs:
            tiles[(v, nm)] = render(sets[(v, "ruhend")], cam, G(V(0, 1.25, 0) + d.normalized() * 5.4), G(V(0, 1.25, 0)),
                                    30, (T, T), "ansicht")
        for nm, t_ in closeups(sets[(v, "ruhend")]).items():
            tiles[(v, nm)] = t_
        tiles[(v, "Kopf und Stab")] = render(sets[(v, "ruhend")], cam, G(V(1.3, 2.0, 2.1)), G(V(0.2, 1.95, 0.5)), 32, (T, T),
                                             "ansicht")
        tiles[(v, "Ankündigung Seite")] = render(sets[(v, "ankuendigung")], cam, G(V(5.0, 1.5, 0.3)), G(V(0, 1.35, 0.2)), 30,
                                                 (T, T), "ansicht")
        tiles[(v, "Ankündigung Nah")] = render(sets[(v, "ankuendigung")], cam, G(V(1.6, 1.8, 2.4)), G(V(0.2, 1.5, 0.2)), 32,
                                               (T, T), "ansicht")
        tiles[(v, "Ankündigung schräg")] = render(sets[(v, "ankuendigung")], cam, G(V(3.6, 1.6, 5.4)), G(V(0, 1.5, 0)), 32,
                                                  (T, T), "ansicht")
    if QUICK:
        names = ("vorn", "Seite", "hinten", "schräg oben", "Ankündigung Seite", "Brust", "Bauch und Gürtel", "Kopf und Stab",
                 "Ankündigung Nah", "Ankündigung schräg")
        sh = np.zeros((2 * T, 5 * T, 3)) + FOG_COLOR
        for k, nm in enumerate(names):
            blit(sh, tiles[(QUICK, nm)], (k % 5) * T, (k // 5) * T)
        os.makedirs(os.path.join(OUT, "_probe"), exist_ok=True)
        save_png(sh[::-1], os.path.join(OUT, "_probe", f"probe-{QUICK}.png"))
        img = render(list(sets[(QUICK, "ankuendigung")]) + room, cam, G(V(0, 1.6, 4)), G(V(0, 1.6, 0)), 70, (854, 480), "spiel")
        save_png(img[::-1], os.path.join(OUT, "_probe", f"spiel-{QUICK}-ank.png"))
        return

    game = {}
    for v in VARIANTS:
        for s, tag in (("ruhend", ""), ("ankuendigung", "-ankuendigung")):
            img = render(list(sets[(v, s)]) + room, cam, G(V(0, 1.6, 4)), G(V(0, 1.6, 0)), 70, (854, 480), "spiel")
            game[(v, s)] = img
            save_png(img[::-1], os.path.join(OUT, f"spiel-{v}{tag}-4m.png"))
    r2tile = {}
    for nm, d in dirs:
        r2tile[nm] = render(r2, cam, G(V(0, 1.25, 0) + d.normalized() * 5.4), G(V(0, 1.25, 0)), 30, (T, T), "ansicht")
    for nm, t_ in closeups(r2).items():
        r2tile[nm] = t_

    order = (("Ghul", ghoul, -6.6), ("Original", r2, -3.3), ("G1", sets[("G1", "ruhend")], 0.0),
             ("G2", sets[("G2", "ruhend")], 3.3), ("Gorm", gorm, 6.6))
    for name, objs, x in order:
        for o in objs:
            o.matrix_world = Matrix.Translation(G(V(x, 0, 0))) @ HOME[o.name]
    bpy.context.view_layer.update()
    line_objs = [o for _, objs, _ in order for o in objs]
    for o in line_objs:
        write_light(o)
    SWd, SHd, cx, fov, dist, cy = 1800, 520, 0.0, 22, 12.0, 1.55
    strip = render(line_objs, cam, G(V(cx, cy, dist)), G(V(cx, cy, 0)), fov, (SWd, SHd), "ansicht")
    save_png(strip[::-1], os.path.join(OUT, "vergleich-vorn.png"))
    hw = 2 * dist * math.tan(math.radians(fov / 2))
    ww = hw * SWd / SHd
    px = lambda x: int((x - cx) / ww * SWd + SWd / 2)
    py = lambda y: int(SHd / 2 - (y - cy) / hw * SHd)
    for yy, lab in ((1.0, "1 m"), (2.0, "2 m"), (3.0, "3 m")):
        row = py(yy)
        mask = np.all(np.abs(strip[row] - FOG_COLOR) < 3, axis=1) & ((np.arange(SWd) // 14) % 2 == 0)
        strip[row - 1:row + 1, mask] = (110, 96, 120)
        text(strip, lab, 6, row - 34, 24, (160, 146, 170))
    for name, _, x in order:
        text(strip, name, px(x) - 14 * len(name), py(0) + 6, 34, (235, 235, 235), True)

    # 3. Blatt
    GOLD, WH, GR = MG[5], (235, 235, 235), (190, 180, 200)
    M, GAP = 12, 24
    W = 2 * M + 5 * T + 4 * GAP
    sheet = np.zeros((4600, W, 3)) + FOG_COLOR
    text(sheet, "Gruftwächter, Runde 4 (Issue 335): das Original (Runde 2, F), bereinigt und ausgebaut", M, 14, 46, WH, True)
    text(sheet, "Urteil zu Runde 3: das Original war schon ganz ok, nicht großartig verändern, nur ausbauen, bereinigen, mehr "
         "Tiefe in die Texturen.", M, 74, 22, GR)
    text(sheet, "Gleiche Gestalt, Haltung, Farben und Ausrüstung wie das Original. G1 behutsam: nur sauber (nichts durchdringt "
         "sich, Stab unter 2,6 m) und mit Tiefe in der Textur.", M, 104, 22, GR)
    text(sheet, "G2 ausgebaut: dazu Gürtel mit Schnalle, Riemen an der linken Wade, loses Verbandsende am Rücken, Kettenglieder.",
         M, 134, 22, GR)
    y = 180
    text(sheet, "ORIGINAL (Runde 2, Fassung F): 1932 Dreiecke, Stab 2,89 m", M, y, 28,
         GOLD, True)
    y += 44
    for k, nm in enumerate(("vorn", "Seite", "hinten", "schräg oben", "Brust")):
        blit(sheet, r2tile[nm], M + k * (T + GAP), y)
        text(sheet, nm, M + k * (T + GAP) + 6, y + 2, 22, WH)
    y += T + 30
    row1 = ("vorn", "Seite", "hinten", "schräg oben", "Ankündigung Seite")
    row2 = ("Brust", "Bauch und Gürtel", "Kopf und Stab", "Ankündigung Nah", "Ankündigung schräg")
    for v in VARIANTS:
        t, lo, hi = stats[v]
        text(sheet, v, M, y - 6, 64, GOLD, True)
        text(sheet, NAMES[v], M + 90, y + 6, 40, WH, True)
        y += 64
        text(sheet, f"{sum(t.values())} Dreiecke (Grenze 3000);  {hi[0] - lo[0]:.2f} m breit, {hi[2] - lo[2]:.2f} m tief, "
             f"{hi[1]:.2f} m hoch (Ruhe, mit Stab und Laterne)", M, y, 26, GR)
        y += 40
        for rowset in (row1, row2):
            for k, nm in enumerate(rowset):
                blit(sheet, tiles[(v, nm)], M + k * (T + GAP), y)
                text(sheet, nm, M + k * (T + GAP) + 6, y + 2, 22, WH)
            y += T + 14
        y += 24
    text(sheet, "IM SPIEL aus 4 m (Nebel, Körnung): Ruhe und Ankündigung (nur die Augen leuchten rot), G1 und G2", M, y, 30, WH,
         True)
    y += 48
    cw = 440
    for k, (v, s) in enumerate((("G1", "ruhend"), ("G1", "ankuendigung"), ("G2", "ruhend"), ("G2", "ankuendigung"))):
        blit(sheet, game[(v, s)][30:450, 207:647], M + k * (cw + 24), y)
        text(sheet, v + (" Ruhe" if s == "ruhend" else " Ankündigung"), M + k * (cw + 24) + 6, y + 2, 28, GOLD, True)
    y += 420 + 24
    text(sheet, "GRÖSSENVERGLEICH von vorn (ohne Nebel), Linien bei 1, 2, 3 m: Ghul, Original, G1, G2, Gorm", M, y, 30, WH, True)
    y += 48
    blit(sheet, strip, (W - SWd) // 2, y)
    y += SHd + 24
    text(sheet, "Ghul 1024 Dreiecke, Gorm 2268 Dreiecke, Original 1932 Dreiecke", M, y, 24, GR)
    y += 40
    save_png(sheet[:y + 10][::-1], os.path.join(OUT, "blatt.png"))


if not os.environ.get("GW_NOMAIN"):
    main()
