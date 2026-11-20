"""Probestueck Gruftwaechter, Runde 3 (Issue #335): F weiterentwickelt in drei Fassungen F1, F2, F3.

Urteil des Menschen zu Runde 2: "F, aber bitte ueberarbeitet (kein Clipping) und detailreicher. Er soll den Eindruck
eines alten untoten Kaempfers machen, der die Graeber seiner gefallenen Kameraden bewacht."

Wie "kein Clipping" hier erreicht wird (statt es nur zu hoffen):
  * Der Rumpf ist EIN durchgehender Koerper aus Ringen (Waffenrock, Guertel, Binden, Brustpanzer, Halsberge sind
    Abschnitte desselben Koerpers mit anderem Material; Absaetze sind Stufen in den Ringen). Nichts steckt ineinander.
  * Teile stossen mit flachen Kappen aneinander (Abstand 2 bis 5 mm), statt sich zu durchdringen: Beine unter dem
    Rocksaum, Kopf auf der Halsberge, Arm an der Flanke des Rumpfes (die Schulterkappe ist der erste Abschnitt des Arms).
  * Gurte, Ketten, Pfeile, Marken, Schnallen werden per Strahl auf die FERTIGE Oberflaeche des Koerpers gesetzt (Abstand
    2 cm bei Gurten, 1 cm bei Kleinteilen), nicht nach Augenmass.
  * Jede Ausnahme steht in ERLAUBT (nur Waffenschaft in der Manschette, Regel 23: keine Hand modelliert, die Manschette
    verdeckt den Griff). Alle anderen Teile werden paarweise mit einem Dreiecks-Durchdringungstest (BVH) geprueft, in Ruhe
    und in der Ankuendigung. Ergebnis: pruefung.txt.

Alles entsteht aus diesem Skript, nichts wird heruntergeladen, unter assets/ wird nur gelesen (Ghul und Gorm).

Aufruf (Repository-Wurzel):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/gruftwaechter-335/runde-3/gruftwaechter_r3.py
Umgebungsvariablen (nur Entwicklung): GW_OUT = Ausgabeordner, GW_QUICK = Fassung (z. B. F2): nur eine Probe rendern.

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
VARIANTS = ("F1", "F2", "F3")
NAMES = {"F1": "Grabwächter, Laternenstab", "F2": "Veteran mit Hellebarde", "F3": "Totenwache mit Standarte"}
SHORT = {"F1": "F1 Laterne", "F2": "F2 Hellebarde", "F3": "F3 Standarte"}

PAL = {r["name"]: np.array(r["farben"], float)
       for k in ("grund", "akzent")
       for r in json.load(open(os.path.join(ROOT, "assets", "palette", "hauptpalette.json")))[k]}
HK, SF, HO, EI, MG, BR = (PAL["Haut/Knochen"], PAL["Stoff"], PAL["Holz"], PAL["Eisen"], PAL["Messing/Gold"],
                          PAL["Blutrot"])

# Erlaubte Durchdringungen (Paare von Koerpernamen, Teilname vorangestellt). Nur: Waffenschaft steckt in der Manschette.
ERLAUBT = {
    "F1": [("arm_r/cuff", "pole/shaft")],
    "F2": [("arm_r/cuff", "halberd/shaft")],
    "F3": [("arm_r/cuff", "standard/shaft")],
}

# --- Textur: 128 x 128, 16 Felder je 32 x 32, nur Palettenfarben, harte Stufen (kein Dithering) ---------------------

NAMES_G = ("flesh", "wrap", "cloth", "cloth2", "plate", "rust", "leather", "wood", "chain", "brass", "eye", "lantern")
FIELDS = {n: ((i % 4) * 32, (i // 4) * 32) for i, n in enumerate(NAMES_G)}
FIELDS.update({"head": (0, 96), "bone": (64, 96), "dark": (96, 96)})       # Kopf: 64 x 32 = ein Achtel der Textur
UV = rects_from_pixels({k: (x, y, x + (64 if k == "head" else 32), y + 32) for k, (x, y) in FIELDS.items()}, 128)
XS, YS = np.meshgrid(np.arange(32), np.arange(32))


def nz(cy, cx, seed):
    return vnoise(32, 32, cy, cx, seed)


def pick(layers, base):
    out = np.zeros((32, 32, 3)) + base
    for mask, col in layers:
        out[mask] = col
    return out


def f_flesh(v):
    s = {"F1": 201, "F2": 211, "F3": 221}[v]
    brk = nz(1, 5, s) > 0.3
    band = (YS % 8 == 3) & brk
    under = (YS % 8 == 2) & brk
    scar = (np.abs(XS - 6 - (YS * 3) // 5) < 1) & (YS > 6) & (YS < 27)
    return pick([(under, HK[4]), (band, HK[6]), (scar, HO[2]), (scar & (YS % 3 == 0), HO[1]),
                 (nz(8, 8, s + 1) > 0.93, HK[4])], HK[5])


def f_wrap(v):
    d = (XS * 2 + YS) % 16
    return pick([((d >= 4) & (d < 6), HK[7]), (nz(3, 3, 232) > 0.86, HO[3]), ((YS < 7) & (nz(4, 4, 231) > 0.55), HO[3]),
                 (d < 2, HK[4])], HK[6])


def f_cloth(v):
    col = {"F1": (SF[3], SF[2], SF[4]), "F2": (SF[3], SF[2], SF[4]), "F3": (SF[4], SF[3], SF[5])}[v]
    n = nz(2, 8, 241)
    layers = [(n > 0.6, col[2]), (n < 0.3, col[1]), (XS % 8 == 3, col[1]), (XS % 8 == 4, col[2]), (YS < 3, EI[1])]
    if v == "F3":     # Grabzeichen der Wache: Kreuz mit Querbalken, mit dunklem Rand
        cross = ((np.abs(XS - 5) < 1) & (YS > 8) & (YS < 27)) | ((np.abs(YS - 20) < 2) & (XS > 0) & (XS < 10))
        edge = ((np.abs(XS - 5) < 3) & (YS > 6) & (YS < 29)) | ((np.abs(YS - 20) < 4) & (XS >= 0) & (XS < 11))
        layers += [(edge, EI[1]), (cross, HK[6])]
    return pick(layers, col[0])


def f_cloth2(v):
    """Verblichener Wollstoff mit senkrechten Falten und ausgefranstem Rand (kein Fleckenmuster)."""
    fold = (XS + (YS // 10)) % 8
    return pick([(fold == 3, HO[1]), (fold == 4, HO[3]), ((YS < 3) | ((YS % 16) == 9) & (XS % 3 == 0), HO[1]),
                 (nz(6, 2, 251) > 0.9, HO[3])], HO[2])


def f_plate(v):
    """Eisenplatte: waagrechte Nahtlinien, Nietenreihen, wenig Rost (kein Tarnmuster)."""
    rust = {"F1": 0.90, "F2": 0.84, "F3": 0.88}[v]
    return pick([((YS % 16) == 0, EI[1]), ((YS % 16) == 1, EI[4]), (((XS % 8) == 2) & ((YS % 16) == 4), EI[5]),
                 ((XS > 10) & (XS < 14), EI[4]), (nz(4, 5, 262) > rust, HO[2]), (nz(5, 5, 263) > rust + 0.05, HO[3])], EI[3])


def f_rust(v):
    return pick([((YS % 8) == 0, HO[1]), (((XS % 8) == 3) & ((YS % 8) == 4), EI[5]), (nz(3, 3, 271) > 0.82, HO[2]),
                 ((XS > 12) & (XS < 16), HO[4])], HO[3])


def f_leather(v):
    n = nz(4, 4, 281)
    return pick([(n > 0.6, HO[2]), (n < 0.25, HO[1]), ((YS % 8) == 0, HO[3])], HO[2])


def f_wood(v):
    return pick([((XS % 5) == 0, HO[1]), (nz(2, 6, 291) > 0.75, HO[3]), (nz(8, 3, 292) > 0.93, HO[3])], HO[2])


def f_chain(v):
    return pick([(((XS + YS) % 4) < 2, EI[4]), (((XS - YS) % 4) == 0, EI[3])], EI[2])


def f_brass(v):
    n = nz(3, 3, 301)
    return pick([(n > 0.6, MG[4]), (n < 0.3, MG[2]), ((XS > 11) & (XS < 17), MG[5]), (YS % 16 == 0, MG[2])], MG[3])


def f_lantern(v):
    glow = (YS > 4) & (YS < 28) & (XS > 3) & (XS < 29)
    return pick([(glow, MG[5]), (glow & (nz(3, 3, 321) > 0.5), MG[4]), (glow & (YS % 8 < 1), EI[2]),
                 (glow & (XS % 8 == 0), EI[2])], EI[2])


def f_bone(v):
    return pick([(nz(3, 3, 331) > 0.6, HK[7]), (nz(5, 5, 332) > 0.88, HK[4]), (YS % 9 == 4, HK[5])], HK[6])


def f_dark(v):
    n = nz(5, 5, 341)
    return pick([(n > 0.7, EI[1]), (n < 0.15, EI[3]), ((YS % 8) == 7, EI[3])], EI[2])


def f_head(v):
    face = ((XS - 16) / 7.5) ** 2 + ((YS - 14) / 11.0) ** 2 < 1
    eyes = (YS >= 15) & (YS <= 19) & (((XS >= 10) & (XS <= 14)) | ((XS >= 18) & (XS <= 22))) & face
    if v == "F1":       # Kettenhaube, Gesicht offen, gebrochene Nase
        mail = pick([(((XS + YS) % 4) < 2, EI[3]), (((XS - YS) % 4) == 0, EI[1]), (nz(3, 3, 351) > 0.8, EI[4])], EI[2])
        mail[face] = HK[6]
        mail[face & (((XS - 16) / 7.5) ** 2 + ((YS - 14) / 11.0) ** 2 > 0.6)] = HK[5]
        mail[(YS == 20) & face] = HK[4]
        mail[eyes] = EI[0]
        mail[(XS >= 15) & (XS <= 16) & (YS >= 10) & (YS <= 14) & face] = EI[1]
        mail[(YS == 7) & (XS >= 12) & (XS <= 20)] = EI[0]
        return mail
    if v == "F2":       # alter Helm mit Kerben, darunter der Kiefer
        out = pick([(nz(4, 4, 361) > 0.78, EI[2]), (nz(4, 4, 362) < 0.22, EI[4]), (nz(4, 5, 363) > 0.72, HO[2]),
                    (nz(5, 5, 364) > 0.82, HO[3]), (YS == 24, EI[4]), (YS == 23, EI[2])], EI[3])
        out[(YS >= 12) & (YS <= 17) & (XS >= 7) & (XS <= 25)] = EI[0]
        jaw = (YS <= 11) & (XS >= 9) & (XS <= 23)
        out[jaw] = HK[6]
        out[jaw & (XS % 3 == 0) & (YS >= 3) & (YS <= 7)] = EI[1]
        out[(YS <= 2) | (YS == 11)] = EI[2]
        out[(YS >= 29) & (XS % 6 == 2)] = EI[0]       # Kerben im Rand der Kalotte
        return out
    out = pick([(nz(2, 8, 371) > 0.55, SF[4]), (XS % 8 == 3, SF[2]), (nz(4, 4, 372) < 0.3, SF[2])], SF[3])
    out[face] = HK[6]       # Kapuze: Schaedelgesicht
    out[face & (((XS - 16) / 7.5) ** 2 + ((YS - 14) / 11.0) ** 2 > 0.62)] = HK[5]
    out[eyes] = EI[0]
    out[(XS >= 15) & (XS <= 17) & (YS >= 10) & (YS <= 13) & face] = EI[1]
    out[(YS >= 5) & (YS <= 8) & (XS >= 11) & (XS <= 21) & face] = HK[7]
    out[(YS >= 5) & (YS <= 8) & (XS % 2 == 0) & (XS >= 11) & (XS <= 21)] = EI[1]
    return out


def f_eye(warn):
    return pick([(YS >= 10, BR[6])], BR[5]) if warn else np.zeros((32, 32, 3)) + EI[1]


def make_atlas(v, state):
    a = np.zeros((128, 128, 3)) + EI[0]
    fns = {"flesh": f_flesh, "wrap": f_wrap, "cloth": f_cloth, "cloth2": f_cloth2, "plate": f_plate, "rust": f_rust,
           "leather": f_leather, "wood": f_wood, "chain": f_chain, "brass": f_brass,
           "lantern": f_lantern, "bone": f_bone, "dark": f_dark}
    for name, fn in fns.items():
        x0, y0 = FIELDS[name]
        a[y0:y0 + 32, x0:x0 + 32] = fn(v)
    x0, y0 = FIELDS["head"]
    a[y0:y0 + 32, x0:x0 + 64] = np.repeat(f_head(v), 2, axis=1)
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


# --- Beine ------------------------------------------------------------------------------------------------------------

def make_leg(s, mats, knee=None, shin_wrap=False):
    """Bein als ein Koerper von der Sohle bis zur Huefte (Waagrechte Ringe). mats: (Stiefel, Schienbein, Oberschenkel)."""
    leg = rec_part("leg_l" if s < 0 else "leg_r", (s * 0.22, 0.9, 0.0))
    boot, shin, thigh = mats
    spec = [  # y, cx, cz, rx, rzf, rzb, Rechteck des Bandes darueber
        (0.0, .31, .13, .12, .30, .12, boot), (0.06, .31, .14, .135, .31, .13, boot),
        (0.13, .32, .10, .125, .26, .12, boot), (0.19, .32, .09, .105, .105, .105, boot),
        (0.205, .32, .09, .115, .115, .115, shin),
        (0.30, .33, .10, .115, .115, .115, shin), (0.45, .34, .12, .125, .125, .125, shin),
        (0.56, .35, .14, .15, .16, .13, shin), (0.70, .31, .09, .17, .17, .15, thigh),
        (0.84, .26, .04, .18, .18, .17, thigh), (0.93, .20, .02, .17, .17, .16, thigh)]
    rings, rects = [], []
    for i, (y, cx, cz, rx, rzf, rzb, rect) in enumerate(spec):
        rings.append(hring(y, s * cx, cz, rx, rzf, rzb, 10, w=0.0))
        if i:
            rects.append(rect)
    leg.body("leg", rings, rects)
    return leg


# --- Arme -------------------------------------------------------------------------------------------------------------

ARM_OUT, ARM_R1, ARM_R2 = 0.15, 0.13, 0.115


def make_arm(parts, side, hand, bend, rects, anchor=(1.68, 0.17), radii=(0.09, 0.175, 0.14, 0.125, 0.115, 0.095),
             cuff_rect="dark", cuff_r=(0.105, 0.128, 0.134, 0.12), cuff_back=0.12, cuff_fwd=0.07, torso="torso/body",
             reach=0.5):
    """Arm an der Flanke: erster Abschnitt = Schulterkappe, dann Oberarm, Ellbogen, Unterarm. Manschette als eigener
    Koerper (verdeckt den Griff). hand und bend sind schon in der Pose. Rueckgabe: Handpunkt, Richtung, Ellbogen."""
    nm = "arm_l" if side < 0 else "arm_r"
    sh = POSE(V(side * 0.6, anchor[0], anchor[1]))
    anch, n = ray(parts, [torso], (side * 2.0, sh.y, sh.z), (-side, 0, 0), 0.004)
    for dy in (-0.16, -0.12, -0.08, -0.04, 0.0, 0.04):          # breiteste Stelle unter der Schulter
        q, _ = ray(parts, [torso], (side * 2.0, sh.y + dy, sh.z), (-side, 0, 0), 0.004)
        if q.x * side > anch.x * side:
            anch = V(q.x, anch.y, anch.z)
    axis = V(n.x, 0.0, n.z).normalized()
    c0 = anch + axis * 0.035
    c1 = anch + axis * ARM_OUT
    radii = list(radii)
    radii[0] = 0.06
    radii[1] = min(radii[1], ARM_R1)
    radii[2] = min(radii[2], ARM_R2)
    hand = V(*hand)
    el = ik(c1, hand, V(*bend), reach, reach)
    dn = (hand - el).normalized()
    wr = hand - dn * cuff_back
    pts = [c0, c1, c1 + (el - c1) * 0.42, el, el + (wr - el) * 0.5, wr]
    arm = rec_part(nm, (side * 0.6, 1.7, 0.2))
    arm.body("arm", tube(pts, [(r, r, r) for r in radii], 10, V(0, 0, 1) if abs(axis.y) > 0.9 else V(0, 1, 0)), rects)
    cpts = [wr + dn * 0.004, wr + dn * 0.04, hand + dn * 0.0, hand + dn * cuff_fwd]
    arm.body("cuff", tube(cpts, [(r, r, r) for r in cuff_r], 8, V(0, 1, 0)), cuff_rect, "flat", "flat")
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


# --- Kleinteile --------------------------------------------------------------------------------------------------------

def wobs(n, **kw):
    w = [1.0] * n
    for k, f in kw.items():
        w[int(k[1:])] = f
    return w


def arrow(rec, grp, parts, origin, direction, d_extra, length, fletch=True):
    """Pfeil oder Bolzen, der im Koerper steckt: Strahl auf die Oberflaeche, Schaft schraeg nach aussen."""
    p, n = ray(parts, ["torso/body"], origin, direction, 0.006)
    d = (n + V(*d_extra)).normalized()
    q1 = p + d * length
    rod(rec, grp, [p, q1], [0.011, 0.011], "wood", sides=5)
    if fletch:
        b = d.cross(V(0, 1, 0))
        if b.length < 0.2:
            b = d.cross(V(1, 0, 0))
        b.normalize()
        e = d.cross(b).normalized() * 0.004
        a0, a1 = q1 - d * 0.085, q1 - d * 0.012
        pts = [a0 + b * 0.014 - e / 2, a0 + b * 0.05 - e / 2, a1 + b * 0.05 - e / 2, a1 + b * 0.014 - e / 2]
        rec.solid(grp + "_feder", pts, e, "bone")


def tags(rec, parts, ys, xs, rect="dark"):
    for k, (x, y) in enumerate(zip(xs, ys)):
        o = pr((x, y, 3.0))
        plate_on(rec, f"marke{k}", parts, ["torso/body"], o, pd((0, 0, -1), y), 0.034, 0.05, 0.006, rect,
                 gap=0.003, up_hint=(0, 1, 0), tilt=(-9, 6, -4)[k % 3])


def candles(rec, grp, parts, cuff, hand, lengths, dx=0.04):
    """Grabkerzen an der Manschette: von unten auf die Manschette gesetzt, haengen nach unten."""
    for k, ln in enumerate(lengths):
        o = hand + V((k - (len(lengths) - 1) / 2) * dx, -1.0, 0)
        p, n = ray(parts, [cuff], o, (0, 1, 0), 0.0)
        tree = bvh_for(parts, [cuff])
        ylow = p.y
        for a in range(12):                   # Rand der Kerze: tiefster Punkt der Manschette darunter
            q = V(p.x + 0.024 * math.cos(a * math.pi / 6), p.y - 0.3, p.z + 0.024 * math.sin(a * math.pi / 6))
            h = tree.ray_cast(q, V(0, 1, 0))[0]
            if h is not None:
                ylow = min(ylow, h.y)
        p = V(p.x, ylow - 0.008, p.z)
        rod(rec, f"{grp}{k}", [p, p + V(0, -ln, 0)], [0.017, 0.017], "bone", sides=5)


def hang(rec, grp, p, pieces):
    """Kette aus Teilen unter p: jedes Teil beginnt 2 mm unter dem Ende des vorigen. pieces: (Laenge, Radius, Rechteck)."""
    y = p.y
    for k, (ln, r, rect) in enumerate(pieces):
        rod(rec, f"{grp}{k}", [V(p.x, y - 0.002, p.z), V(p.x, y - 0.002 - ln, p.z)], [r, r], rect, sides=6)
        y -= ln + 0.002
    return y


# --- Rumpf-Beschreibungen ----------------------------------------------------------------------------------------------

def torso_F1():
    keys = [(.90, 0, 0, .38, .27, .24), (1.06, 0, .04, .40, .30, .26), (1.24, 0, .09, .47, .38, .30),
            (1.44, 0, .14, .49, .36, .35), (1.62, -.03, .17, .50, .30, .44), (1.76, -.04, .18, .43, .23, .45),
            (1.84, -.02, .27, .27, .17, .30), (1.89, 0, .36, .16, .13, .15)]
    spec = [(.935, .045, "cloth"), (.99, .03, "cloth"), (1.015, 0, "cloth"), (1.04, .035, "leather"), (1.12, .035, "leather"),
            (1.145, 0, "leather"), (1.20, 0, "wrap"), (1.215, .02, "wrap"), (1.27, .02, "wrap"), (1.285, 0, "wrap"),
            (1.36, 0, "wrap"), (1.375, .02, "wrap"), (1.45, .02, "flesh"), (1.55, 0, "flesh"), (1.64, 0, "flesh"),
            (1.70, 0, "wrap"), (1.78, 0, "wrap"), (1.84, 0, "flesh"), (1.895, 0, "flesh")]
    return keys, spec


def torso_F2():
    keys = [(.90, 0, 0, .38, .27, .24), (1.06, 0, .03, .41, .30, .26), (1.24, 0, .06, .48, .34, .30),
            (1.44, 0, .09, .53, .37, .34), (1.62, 0, .10, .54, .34, .38), (1.76, 0, .12, .46, .26, .38),
            (1.84, 0, .18, .28, .18, .26), (1.89, 0, .24, .16, .13, .15)]
    ridge = wobs(12, k6=1.07)
    dent1 = wobs(12, k6=1.07, k5=.93, k4=.95)
    dent2 = wobs(12, k6=1.07, k7=.92, k8=.95)
    spec = [(.935, .04, "chain"), (.99, .03, "chain"), (1.015, 0, "chain"), (1.04, .03, "leather"), (1.12, .03, "leather"),
            (1.145, 0, "leather"), (1.20, 0, "wrap"), (1.215, .02, "wrap"), (1.27, .02, "wrap"), (1.285, 0, "wrap"),
            (1.34, 0, "wrap"), (1.37, .03, "plate"), (1.46, .045, "plate", dent2), (1.56, .05, "plate", dent1),
            (1.64, .04, "plate", ridge), (1.69, .01, "plate"), (1.72, .02, "rust"), (1.78, .03, "rust"),
            (1.84, .01, "rust"), (1.895, 0, "rust")]
    return keys, spec


def torso_F3():
    keys = [(.90, 0, 0, .34, .24, .22), (1.06, 0, .03, .36, .26, .24), (1.24, 0, .07, .42, .30, .27),
            (1.44, 0, .11, .44, .30, .31), (1.62, 0, .14, .46, .27, .38), (1.76, 0, .15, .40, .21, .38),
            (1.84, 0, .22, .26, .16, .26), (1.89, 0, .30, .16, .12, .15)]
    spec = [(.935, .03, "cloth"), (.99, .02, "cloth"), (1.015, 0, "cloth"), (1.04, .03, "leather"), (1.12, .03, "leather"),
            (1.145, 0, "leather"), (1.20, 0, "wrap"), (1.215, .02, "wrap"), (1.27, .02, "wrap"), (1.285, 0, "wrap"),
            (1.34, 0, "wrap"), (1.45, 0, "flesh"), (1.54, .02, "cloth2"), (1.62, .05, "cloth2"), (1.70, .06, "cloth2"),
            (1.76, .05, "cloth2"), (1.82, .03, "cloth2"), (1.86, .0, "cloth2"), (1.895, 0, "cloth2")]
    return keys, spec


TORSO = {"F1": torso_F1, "F2": torso_F2, "F3": torso_F3}
HEAD = {"F1": ([.38, .42, .46, .48, .485, .47], [.105, .14, .17, .17, .14, .08]),
        "F2": ([.24, .265, .29, .31, .315, .30], [.105, .15, .185, .18, .15, .09]),
        "F3": ([.30, .34, .38, .405, .41, .40], [.105, .15, .18, .175, .145, .085])}
LEGS = {"F1": (("leather", "wrap", "flesh"), ("leather", "flesh", "flesh")),
        "F2": (("leather", "plate", "wrap"), ("leather", "flesh", "wrap")),
        "F3": (("leather", "wrap", "wrap"), ("leather", "flesh", "cloth2"))}
ARMS = {"F1": (["plate", "flesh", "wrap", "wrap", "flesh"], ["wrap", "flesh", "wrap", "wrap", "flesh"]),
        "F2": (["plate", "plate", "wrap", "plate", "wrap"], ["wrap", "wrap", "wrap", "flesh", "flesh"]),
        "F3": (["cloth2", "cloth2", "wrap", "wrap", "flesh"], ["cloth2", "cloth2", "wrap", "flesh", "flesh"])}
UNIT = lambda v: V(*v).normalized()
HANDS = {  # Ruhe-Koordinaten vor der Pose: (rechts, Stabrichtung, links)
    ("F1", False): ((.62, 1.15, .70), (.04, 1, -.05), (-.68, .90, .42)),
    ("F1", True): ((.76, 1.50, .50), (.30, .62, -.72), (-.60, 1.30, .80)),
    ("F2", False): ((.64, 1.15, .66), (.03, 1, -.04), (-.68, .90, .42)),
    ("F2", True): ((1.02, 1.38, .72), (.50, .50, -.70), (-.60, 1.30, .78)),
    ("F3", False): ((.62, 1.15, .70), (.04, 1, -.05), (-.68, .90, .42)),
    ("F3", True): ((.75, 1.45, .55), (.30, .65, -.69), (-.62, 1.30, .78)),
}


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


def make_weapon(v, ank, hand, dn):
    """Stab, Hellebarde oder Standarte als Teile; jeder Koerper beginnt 2 mm nach dem Ende des vorigen."""
    w = rec_part("pole" if v == "F1" else "halberd" if v == "F2" else "standard", hand)
    nm = w.name
    ref = V(0, 1, 0) if abs(dn.y) < 0.7 else V(0, 0, 1)
    ln_back = 1.10 if v != "F2" else 1.05
    ln_fwd = 1.12 if v != "F2" else 1.06
    butt = hand - dn * ln_back
    top = hand + dn * ln_fwd
    pts = [butt, butt + dn * 0.07, top - dn * 0.07, top]
    w.body("shaft", tube(pts, [(r, r, r) for r in (0.036, 0.03, 0.03, 0.036)], 6, ref), ["dark", "wood", "dark"])
    s = side_h(dn, pd((1, 0, 0), hand.y))
    f = side_h(dn, pd((0, 0, 1), hand.y))
    t0 = top + dn * 0.002
    if v == "F1":
        hk = [t0, t0 + dn * 0.04, t0 + dn * 0.07 + f * 0.07, t0 + dn * 0.09 + f * 0.17, t0 + dn * 0.05 + f * 0.26]
        w.body("haken", tube(hk, [(r, r, r) for r in (0.03, 0.03, 0.028, 0.025, 0.02)], 6, ref), "dark")
        end = hk[-1]
        lantern(w, "laterne", V(end.x, end.y - 0.034, end.z))
    elif v == "F2":
        c0 = t0
        s = perp(pd((1, 0, 0), hand.y), dn)
        w.body("kragen", tube([c0, c0 + dn * 0.04, c0 + dn * 0.075], [(r, r, r) for r in (0.05, 0.05, 0.04)], 8, ref), "dark")
        base = c0 + dn * 0.079
        w.spike_g("spitze", base, base + dn * 0.30, 0.03, "plate")
        tt = dn.cross(s).normalized()

        def at(a, b):
            return top + dn * a + s * b - tt * 0.007

        blade = [at(.01, .051), at(-.05, .30), at(.17, .33), at(.18, .051)]
        w.solid("klinge", blade, tt * 0.014, "plate")
        sa = top - dn * 0.12 - perp(pd((1, 0, 0), hand.y), dn) * 0.0
        L = side_h(dn, s) if ank_w(dn) else s
        w.solid("hakenruecken", [at(.06, -.054), at(.0, -.2), at(.10, -.26), at(.11, -.054)], tt * 0.014, "dark")
        rp = [sa + L * 0.034, sa + L * 0.2, sa + L * 0.36]
        rod(w, "arm", rp, [0.015, 0.015, 0.015], "dark", sides=6)
        lantern(w, "laterne", V(rp[-1].x, rp[-1].y - 0.021, rp[-1].z))
    else:
        c = t0 + dn * 0.022
        w.body("quer", tube([c - s * 0.28, c, c + s * 0.28], [(r, r, r) for r in (0.02, 0.02, 0.02)], 6, dn), "wood")
        w.spike_g("spitze", c + dn * 0.026, c + dn * 0.17, 0.03, "dark")
        if (c - s * 0.2 - V(0, c.y, 0.1)).length_squared < (c + s * 0.2 - V(0, c.y, 0.1)).length_squared:
            s = -s                                   # Fahne auf die vom Rumpf abgewandte Seite
        q = c - s * 0.2
        panel(w, "fahne", q.x, q.y - 0.05, q.y - 0.55, q.z + 0.0, q.z - 0.0, 0.11, 0.11, 0.008, "cloth",
              (-.05, .02, -.08, -.03, -.09, .01))
        e = c + s * 0.27
        lantern(w, "laterne", V(e.x, e.y - 0.024, e.z))
    return w


def make_head(v, parts):
    cz, rr = HEAD[v]
    rings = [hring(1.90 + 0.058 * k, 0, cz[k], rr[k], rr[k], rr[k] * 0.95, 10) for k in range(6)]
    h = rec_part("head", (0, 1.9, cz[0]))
    h.body("head", rings, "head", "flat", "dome")
    parts.append(h)
    e = rec_part("eyes", (0, 2.04, cz[2]))
    y = 2.045
    hc = pr((0, y, cz[2]))
    face, right = pd((0, 0, 1), y), pd((1, 0, 0), y)
    for s in (-1, 1):
        o = hc + right * (s * 0.055) + face * 1.0
        plate_on(e, "auge" + ("l" if s < 0 else "r"), parts, ["head/head"], o, -face, 0.042, 0.028, 0.012, "eye", gap=0.002,
                 up_hint=pd((0, 1, 0), y))
    parts.append(e)


def build(v, state):
    global POSE
    ank = state == "ankuendigung"
    POSE = Pose(-12.0, 23.0) if ank else Pose()
    parts = []
    for s in (-1, 1):
        parts.append(make_leg(s, LEGS[v][0 if s < 0 else 1]))
    keys, spec = TORSO[v]()
    rings, rects = torso_rings(keys, spec)
    t = rec_part("torso", (0, 1.4, 0.1))
    t.body("body", rings, rects)
    parts.append(t)
    make_head(v, parts)
    hr, dn0, hl = HANDS[(v, ank)]
    hand_r = pr(hr)
    dn = pd(dn0, hr[1]).normalized()
    bendr = POSE.dir((1, -.3, -.2))
    ra = ARMS[v]
    shoulder_r = {"F1": .175, "F2": .175, "F3": .175}[v]
    rad_l = (0.09, 0.24 if v == "F2" else 0.175, 0.15 if v == "F2" else 0.14, 0.125, 0.115, 0.095)
    rad_r = (0.09, 0.175, 0.14, 0.125, 0.115, 0.095)
    cuff_l, cuff_r = ("dark", "leather") if v != "F2" else ("dark", "dark")
    reach = 0.5
    L = make_arm(parts, -1, pr(hl), POSE.dir((-.3, 0, -1)), ra[0], radii=rad_l, cuff_rect=cuff_l, reach=reach)
    R = make_arm(parts, 1, hand_r, bendr, ra[1], radii=rad_r, cuff_rect=cuff_r, reach=reach)
    g = rec_part("gear", (0, 1.4, 0.1))
    gear(v, ank, g, parts, L, R)
    parts.append(g)
    fa = (R[0] - R[2]).normalized()
    dn = steil(dn, fa, 68.0)
    w = make_weapon(v, ank, R[0], dn)
    parts.append(w)
    return parts


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


STUDS = {
    "F1": [(("rost", False, (-.40, 1.56), (.40, 1.30), (.42, 1.52), (-.22, 1.18), (.22, 1.16)),
            ("flick", True, (-.30, 1.30), (.30, 1.38), (-.10, 1.52), (.20, 1.60), (-.34, 1.50), (.36, 1.52)))],
    "F2": [(("niete", False, (-.40, 1.40), (-.40, 1.52), (-.28, 1.60), (-.12, 1.64), (.12, 1.66), (.34, 1.60),
             (.42, 1.46), (.40, 1.30), (-.40, 1.26), (.20, 1.18)),
            ("rflick", True, (-.30, 1.30), (0.0, 1.46), (-.32, 1.52), (.32, 1.52), (.0, 1.24),
             (-.15, 1.62), (.18, 1.60), (-.12, 1.40), (.12, 1.40), (.30, 1.44), (-.30, 1.42)))],
    "F3": [(("niete", False, (-.40, 1.40), (-.40, 1.54), (-.30, 1.62), (.30, 1.62), (.40, 1.50), (.40, 1.36),
             (-.36, 1.26), (.36, 1.24)),
            ("rniete", True, (-.34, 1.30), (.34, 1.30), (-.34, 1.56), (.34, 1.56), (-.18, 1.64), (.34, 1.44), (-.34, 1.42),
             (-.26, 1.38), (.26, 1.54), (0.0, 1.66)))],
}


def gear(v, ank, g, parts, L, R):
    """Ausruestung, die auf der fertigen Oberflaeche sitzt: Gurte, Pfeile, Marken, Schnallen, Andenken."""
    T = ["torso/body"]
    for front, back in STUDS[v]:
        studs(g, front[0], parts, front[2:], False, rect="rust" if v == "F1" else "brass")
        studs(g, back[0], parts, back[2:], True, rect="leather")
    if v == "F1":
        strap(g, "kette1", parts, T, (-.30, 1.72), (.30, 1.24), .024, 16, .032)
        strap(g, "kette2", parts, T, (.30, 1.70), (-.30, 1.24), .024, 16, .060)
        plate_on(g, "schloss", parts, T, pr((0, 1.47, 3.0)), pd((0, 0, -1), 1.47), .07, .07, .02, "brass", gap=.070)
        arrow(g, "pfeil1", parts, pr((-.20, 1.70, -3)), pd((0, 0, 1), 1.70), (0, .4, 0), .42)
        arrow(g, "pfeil2", parts, pr((.14, 1.74, -3)), pd((0, 0, 1), 1.74), (.1, .4, 0), .36)
        arrow(g, "bolzen", parts, pr((.26, 1.52, -3)), pd((0, 0, 1), 1.52), (.1, .3, 0), .22, fletch=False)
        tags(g, parts, (1.085, 1.085, 1.085), (-.20, -.16, -.12))
        candles(g, "kerze", parts, "arm_l/cuff", L[0], (.12, .09, .11))
    elif v == "F2":
        strap(g, "kette", parts, T, (-.32, 1.66), (.28, 1.20), .024, 16, .034)
        plate_on(g, "schnalle", parts, T, pr((0, 1.08, 3.0)), pd((0, 0, -1), 1.08), .08, .06, .012, "brass", gap=.004)
        arrow(g, "pfeil1", parts, pr((.18, 1.55, -3)), pd((0, 0, 1), 1.55), (0, .4, 0), .40)
        arrow(g, "bolzen", parts, pr((-.2, 1.30, -3)), pd((0, 0, 1), 1.30), (-.1, .3, 0), .22, fletch=False)
        tags(g, parts, (1.085, 1.085, 1.085), (-.20, -.16, -.12))
    else:
        zs = [front_x(parts, 0.0, y, 0.0)[0].z for y in (.94, .97, 1.0)]
        z0 = max(zs) + .02
        panel(g, "wappenrock", 0.0, 1.0, .50, z0, z0 + .01, .17, .14, .01, "cloth", (-.04, .03, -.06, -.02, -.07, .02))
        zb = min(ray(parts, T, pr((xx, yy, -3.0)), pd((0, 0, 1), yy), 0.0)[0].z
                 for yy in (1.0, 1.15, 1.3, 1.45, 1.6, 1.7) for xx in (-.3, 0.0, .3))
        panel(g, "umhang", 0.0, 1.62, .45, zb - .045, zb - .045, .30, .40, .012, "cloth2",
              (-.05, .02, -.08, -.04, -.09, .01))
        tags(g, parts, (1.085, 1.085, 1.085), (-.10, -.055, -.01))
        tags(g, parts, (1.085, 1.085), (.05, .09), "brass")
        ph, nh = front_x(parts, -.27, 1.08, .075)
        y = hang(g, "riemen", ph, [(.08, .012, "leather")])
        ys = [y - .002 - dy for dy in (.0, .02, .05, .075, .09)][::-1]
        rs = [.03, .055, .068, .058, .035][::-1]
        g.body("helm", [circ(ph.x, yy, ph.z, r) for yy, r in zip(ys, rs)], "rust")
        pc, nc = front_x(parts, .27, 1.08, .016)
        y = hang(g, "bund", pc, [(.06, .012, "leather")])
        g.body("balken", tube([V(pc.x - .05, y - .016, pc.z), V(pc.x, y - .016, pc.z), V(pc.x + .05, y - .016, pc.z)],
                              [(.012, .012, .012)] * 3, 6, V(0, 1, 0)), "leather")
        for k, dx in enumerate((-.04, 0.0, .04)):
            rod(g, f"kerze{k}", [V(pc.x + dx, y - .034, pc.z), V(pc.x + dx, y - .034 - (.11 - k * .01), pc.z)],
                [.017, .017], "bone", sides=5)


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
    tmp = os.path.join(tempfile.gettempdir(), "gruftwaechter_r3_txt.png")
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


FOCUS = {"F1": (0, 1.9, 0.3), "F2": (0, 1.9, 0.2), "F3": (0, 1.9, 0.3)}
BELT = {"F1": (-.1, 1.1, .35), "F2": (0, 1.1, .35), "F3": (0, 1.0, .38)}


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    os.makedirs(os.path.join(OUT, "modelle"), exist_ok=True)
    os.makedirs(os.path.join(OUT, "texturen"), exist_ok=True)
    states = ("ruhend", "ankuendigung")
    atlas = {(v, s): make_atlas(v, s) for v in VARIANTS for s in states}
    pal_all = {tuple(c) for k in PAL for c in PAL[k]}
    for (v, s), a in atlas.items():
        cols = {tuple(c) for c in a.reshape(-1, 3)}
        assert cols <= pal_all, "Farbe ausserhalb der Palette"
        assert len(cols) <= 32, f"{v} {s}: {len(cols)} Farben"
        red = any(c in {tuple(x) for x in BR} for c in cols)
        assert red == (s == "ankuendigung"), f"{v} {s}: Rot nur in der Ankuendigung"
    images = {k: save_image(f"atlas_{k[0]}_{k[1]}", a, os.path.join(OUT, "texturen", f"atlas_{k[0]}_{k[1]}.png"))
              for k, a in atlas.items()}

    # 1. Modelle, Zahlen, Pruefungen
    stats, lines, clip_lines = {}, [], []
    built = {}
    for v in VARIANTS:
        if QUICK and v != QUICK:
            continue
        for s in states:
            built[(v, s)] = build(v, s)
        mat = export_material(f"gw_{v}", images[(v, "ruhend")])
        objs = put_in_scene(built[(v, "ruhend")], mat, f"exp_{v}")
        export_glb(objs, os.path.join(OUT, "modelle", f"gruftwaechter_{v}.glb"))
        lo, hi = extent(objs)
        stats[v] = (tri_count(objs), lo, hi)
        rep = closed_report(objs)
        lines.append(f"{v} {NAMES[v]}: Teile {len(objs)}, Dreiecke {sum(r[0] for r in rep.values())}, offene Kanten "
                     f"{sum(r[1] for r in rep.values())}, Kanten mit mehr als zwei Flaechen {sum(r[2] for r in rep.values())}, "
                     f"kleinster Rauminhalt {min(r[3] for r in rep.values()):.6f} m3 (muss > 0 sein)")
        for name, r in rep.items():
            lines.append(f"    {name}: {r[0]} Dreiecke, offen {r[1]}, mehrfach {r[2]}, Raum {r[3]:.4f}")
        for o in objs:
            bpy.data.objects.remove(o, do_unlink=True)
    # Durchdringung (die Koerper wurden beim Bauen aufgezeichnet)
    for v in VARIANTS:
        for s in states:
            if (v, s) not in built:
                continue
            POSE_ = built[(v, s)]
            found, bad, selfx = clip_check(POSE_, ERLAUBT[v])
            nb = sum(len(p.bodies) for p in POSE_)
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
                "Erlaubt ist nur der Waffenschaft in der Manschette (Regel 23: keine Hand modelliert, Manschette "
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

    # 2. Szenen zum Rendern
    pm = {k: pass_material("pm_%s_%s" % k, images[k]) for k in atlas}
    sets = {}
    for v in VARIANTS:
        if QUICK and v != QUICK:
            continue
        for s in states:
            sets[(v, s)] = put_in_scene(build(v, s), pm[(v, s)], f"r_{v}_{s}")
    for objs in sets.values():
        place(objs)
    ghoul = import_path(os.path.join(MODELS, "ghoul.glb"), "ghoul")
    gorm = import_path(os.path.join(MODELS, "gorm.glb"), "gorm")
    r2 = import_path(R2_GLB, "r2F")
    for o in ghoul + gorm + r2:
        pass
    place(ghoul)
    place(gorm)
    place(r2)
    cam = camera()
    stone = stone_textures(tempfile.mkdtemp(prefix="gw_stein_"))
    room = build_room(stone)
    hall = build_room(stone, half=14.0, height=3.5, back=-14.0, front=24.0)

    T = 360
    dirs = (("vorn", V(0, 0.08, 1)), ("Seite", V(1, 0.08, 0)), ("hinten", V(0, 0.08, -1)), ("schräg oben", V(0.6, 0.9, 0.7)))
    tiles = {}
    for v in VARIANTS:
        if QUICK and v != QUICK:
            continue
        for nm, d in dirs:
            tiles[(v, nm)] = render(sets[(v, "ruhend")], cam, G(V(0, 1.25, 0) + d.normalized() * 5.4), G(V(0, 1.25, 0)),
                                    30, (T, T), "ansicht")
        fc = V(*FOCUS[v])
        tiles[(v, "Kopf und Brust")] = render(sets[(v, "ruhend")], cam, G(fc + V(0.5, 0.0, 1.4)), G(fc - V(0, 0.25, 0)), 32,
                                              (T, T), "ansicht")
        bc = V(*BELT[v])
        tiles[(v, "Gürtel und Andenken")] = render(sets[(v, "ruhend")], cam, G(bc + V(0.7, 0.2, 1.3)), G(bc), 32, (T, T),
                                                   "ansicht")
        tiles[(v, "Waffe und Hand")] = render(sets[(v, "ruhend")], cam, G(V(1.9, 1.6, 2.0)), G(V(0.45, 1.5, 0.45)), 32, (T, T),
                                              "ansicht")
        tiles[(v, "Ankündigung Seite")] = render(sets[(v, "ankuendigung")], cam, G(V(5.0, 1.5, 0.3)), G(V(0, 1.35, 0.2)), 30,
                                                 (T, T), "ansicht")
        tiles[(v, "Ankündigung Nah")] = render(sets[(v, "ankuendigung")], cam, G(V(1.6, 1.8, 2.4)), G(V(0.2, 1.5, 0.2)), 32,
                                               (T, T), "ansicht")
        tiles[(v, "Ankündigung schräg")] = render(sets[(v, "ankuendigung")], cam, G(V(3.6, 1.6, 5.4)), G(V(0, 1.5, 0)), 32,
                                                  (T, T), "ansicht")
    if QUICK:
        names = ("vorn", "Seite", "hinten", "schräg oben", "Kopf und Brust", "Gürtel und Andenken", "Waffe und Hand",
                 "Ankündigung Seite", "Ankündigung Nah", "Ankündigung schräg")
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

    order = (("Ghul", ghoul, -6.6), ("F1", sets[("F1", "ruhend")], -3.3), ("F2", sets[("F2", "ruhend")], 0.0),
             ("F3", sets[("F3", "ruhend")], 3.3), ("Gorm", gorm, 6.6))
    for name, objs, x in order:
        for o in objs:
            o.matrix_world = Matrix.Translation(G(V(x, 0, 0))) @ HOME[o.name]
    bpy.context.view_layer.update()
    line_objs = [o for _, objs, _ in order for o in objs]
    for o in line_objs:
        write_light(o)
    p10 = render_p(line_objs + hall, cam, G(V(0, 1.6, 10)), G(V(0, 1.6, 0)), 70, (854, 480))
    strips = {}
    for tag, scale in (("", 1.0), ("-dunkel", 0.5)):
        q = dict(p10)
        q["light"] = p10["light"] * scale
        img = compose(q, "spiel")[::-1]
        save_png(img[::-1], os.path.join(OUT, f"vergleich-10m{tag}.png"))
        strips[tag] = img[170:310, 160:700]
    SWd, SHd, cx, fov, dist, cy = 1800, 560, 0.0, 22, 12.0, 1.7
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
    sheet = np.zeros((6200, W, 3)) + FOG_COLOR
    text(sheet, "Gruftwächter, Runde 3 (Issue 335): F weiterentwickelt, drei Fassungen", M, 14, 46, WH, True)
    text(sheet, "Urteil zu Runde 2: F, aber überarbeitet (kein Clipping), detailreicher, ein alter untoter Kämpfer, der die "
         "Gräber gefallener Kameraden bewacht.", M, 74, 22, GR)
    y = 116
    text(sheet, "Zum Vergleich: Runde 2, Fassung F (1932 Dreiecke, Stab 2,89 m, gekreuzte Ketten durchdrangen sich)", M, y, 28,
         GOLD, True)
    y += 44
    for k, (nm, _) in enumerate(dirs):
        blit(sheet, r2tile[nm], M + k * (T + GAP), y)
        text(sheet, nm, M + k * (T + GAP) + 6, y + 2, 22, WH)
    text(sheet, "Das war das Problem:", M + 4 * (T + GAP), y + 40, 26, WH, True)
    for k, ln in enumerate(("gekreuzte Ketten, Bauchgurt und Rumpf", "ineinander; Stab 2,89 m hoch;", "wenig Hinweise auf eine",
                            "Geschichte. Jetzt: Rumpf aus einem", "Stück, Teile liegen nur auf.")):
        text(sheet, ln, M + 4 * (T + GAP), y + 80 + k * 32, 22, GR)
    y += T + 30
    row1 = ("vorn", "Seite", "hinten", "schräg oben", "Ankündigung Seite")
    row2 = ("Kopf und Brust", "Gürtel und Andenken", "Waffe und Hand", "Ankündigung Nah", "Ankündigung schräg")
    for v in VARIANTS:
        t, lo, hi = stats[v]
        text(sheet, v, M, y - 6, 64, GOLD, True)
        text(sheet, NAMES[v], M + 90, y + 6, 40, WH, True)
        y += 64
        text(sheet, f"{sum(t.values())} Dreiecke (Gorm 2268, Grenze 3000);  {hi[0] - lo[0]:.2f} m breit, "
             f"{hi[2] - lo[2]:.2f} m tief, {hi[1]:.2f} m hoch (Ruhe, mit Waffe)", M, y, 26, GR)
        y += 40
        for rowset in (row1, row2):
            for k, nm in enumerate(rowset):
                blit(sheet, tiles[(v, nm)], M + k * (T + GAP), y)
                text(sheet, nm, M + k * (T + GAP) + 6, y + 2, 22, WH)
            y += T + 14
        y += 24
    text(sheet, "ANKÜNDIGUNG aus 4 m im Spiel (Ausholen aus der Hüfte; die Augen leuchten rot, sonst nichts)", M, y, 32, WH,
         True)
    y += 50
    cw = 600
    for k, v in enumerate(VARIANTS):
        blit(sheet, game[(v, "ankuendigung")][30:450, 127:727], M + k * (cw + 25), y)
        text(sheet, v, M + k * (cw + 25) + 6, y + 2, 34, GOLD, True)
    y += 420 + 24
    text(sheet, "Dieselben Fassungen in RUHE aus 4 m im Spiel", M, y, 28, WH, True)
    y += 44
    for k, v in enumerate(VARIANTS):
        blit(sheet, game[(v, "ruhend")][30:450, 127:727], M + k * (cw + 25), y)
        text(sheet, v, M + k * (cw + 25) + 6, y + 2, 34, GOLD, True)
    y += 420 + 24
    text(sheet, "GRÖSSENVERGLEICH von vorn (ohne Nebel), Linien bei 1, 2, 3 m: Ghul, F1, F2, F3, Gorm", M, y, 32, WH, True)
    y += 50
    blit(sheet, strip, (W - SWd) // 2, y)
    y += SHd + 24
    big = None
    for tag, title in (("", "AUS 10 m im Spiel (Nebel, Körnung, 854 x 480, doppelt vergrößert): Ghul, F1, F2, F3, Gorm"),
                       ("-dunkel", "dieselbe Reihe DUNKEL (Licht halbiert)")):
        text(sheet, title, M, y, 28, WH, True)
        y += 44
        big = up(strips[tag], 2)
        blit(sheet, big, M, y)
        for name, _, x in order:
            text(sheet, name, M + 2 * (int(427 + 34.276 * x) - 160) - 14 * len(name), y + big.shape[0] + 2, 24, WH)
        y += big.shape[0] + 44
    ty = y - 2 * (big.shape[0] + 44 + 44) + 50
    text(sheet, "Dreiecke und Maße (Ruhe-Pose, mit Waffe)", 1200, ty, 28, GOLD, True)
    for k, v in enumerate(VARIANTS):
        t, lo, hi = stats[v]
        text(sheet, f"{v} {NAMES[v]}: {sum(t.values())} Dreiecke", 1200, ty + 50 + k * 80, 26, WH, True)
        text(sheet, f"{hi[0] - lo[0]:.2f} breit, {hi[2] - lo[2]:.2f} tief, {hi[1]:.2f} hoch", 1200, ty + 80 + k * 80, 24, GR)
    text(sheet, "Ghul 1024 Dreiecke, Gorm 2268 Dreiecke", 1200, ty + 300, 24, GR)
    save_png(sheet[:y + 10][::-1], os.path.join(OUT, "blatt.png"))


main()
