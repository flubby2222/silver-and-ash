"""Gruftwaechter (Crypt Warden) fuer das Spiel (Issue #530): zwei Haltungen, Ruhe und Ankuendigung.

Uebergabe aus dem Probestueck zu Issue #335, Runde 7, Fassung K1 (vom Menschen am 20.11.2026 abgenommen): schwarzes
Gesicht, ein Auge auf seiner rechten Seite (an der Stelle eines von zwei Augen), in Ruhe matt, Rot nur in der Ankuendigung,
die gekreuzten Ketten enden an den Flanken des Guertels unter Messingklammern. Geometrie, Haltungen und Textur sind aus
design/proben/grafik/gruftwaechter-335 (Runde 4 mit den Aenderungen der Runden 5 bis 7) uebernommen und auf K1 reduziert:
ohne Vergleichsfassungen, Probenblatt, Raum und Bildrechnung. Die Teile liegen mit dem Drehpunkt im Ursprung, nichts
bewegt sich in der Datei.

Wie "kein Clipping" erreicht wird: Der Rumpf ist EIN verschweisster Koerper aus der Vereinigung der Ellipsoide des
Originals; Ketten, Guertel, Binden, Schnallen und Schulterlamellen werden per Strahl auf die fertige Oberflaeche gesetzt.
Das Skript prueft beim Erzeugen: Dreiecke (hoechstens 3000), offene Kanten (keine), Durchdringungen aller Koerper
paarweise (nur der Stab in der Manschette ist erlaubt; es sind keine Haende modelliert), Palette, Farbzahl und dass Rot
nur im Auge der Ankuendigung steht.

Alles aus einem Skript: Geometrie, Textur-Atlas (nur Farben der Hauptpalette, hoechstens 32), zwei .glb samt .png nach
assets/models/. Nichts heruntergeladen.

Aufruf (aus dem Repository-Wurzelverzeichnis, ohne Argumente), danach die Pruefsummen neu schreiben:
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/models/crypt_warden.py
    python tools/models/checksums.py
Umgebungsvariable CRYPT_WARDEN_OUT leitet die Ausgabe in einen anderen Ordner (fuer den Vergleich zweier Laeufe).

Ausgabe: assets/models/crypt_warden_rest.glb|png (Ruhe: Stab aufgestuetzt, Auge matt) und crypt_warden_announce.glb|png
(Ankuendigung: Oberkoerper verwunden, Stab zum Schlag erhoben, Auge blutrot). Beide .glb haben dieselben neun starren
Teile: "leg_l", "leg_r", "torso", "head", "eyes", "arm_l", "arm_r", "gear", "pole".
Koordinaten: Spiel-Achsen (x seitlich, y oben, z vorn = Blickrichtung der Figur), Meter, Ursprung zwischen den Fuessen.
Texturfelder: y von unten (Zeile 0 = unten) wie die UV-Achse v; vorn = u 0,5.
"""

import json
import math
import os
import sys

import bmesh
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
OUT = os.environ.get("CRYPT_WARDEN_OUT") or os.path.join(ROOT, "assets", "models")
sys.path.insert(0, HERE)
import bpy  # noqa: E402
from mathutils import Matrix  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402
from psx import (V, G, Part, rects_from_pixels, vnoise, save_image, export_material,  # noqa: E402
                 export_glb, to_game, tube, MATS)

STANCES = ("rest", "announce")
MAX_TRIANGLES = 3000

PAL = {r["name"]: np.array(r["farben"], float)
       for k in ("grund", "akzent")
       for r in json.load(open(os.path.join(ROOT, "assets", "palette", "hauptpalette.json"), encoding="utf-8"))[k]}
HK, SF, HO, EI, MG, BR = (PAL["Haut/Knochen"], PAL["Stoff"], PAL["Holz"], PAL["Eisen"], PAL["Messing/Gold"],
                          PAL["Blutrot"])
STEIN = PAL["Stein"]
DARKEST = STEIN[0]

# Erlaubte Durchdringungen (Paare von Koerpernamen, Teilname vorangestellt). Nur: Waffenschaft steckt in der Manschette.
ALLOWED = [("arm_r/cuff", "pole/shaft")]

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



def f_bone():
    return pick([(nz(3, 3, 331) > 0.6, HK[7]), (nz(5, 5, 332) > 0.88, HK[4]), (YS % 9 == 4, HK[5])], HK[6])



EYE_COLS = {"rest": (STEIN[2], STEIN[3], STEIN[4]), "announce": (BR[3], BR[5], BR[6])}


def eye_field(stance):
    """Auge als Scheibe: Rand, Mitte, Kern in harten Stufen (3 Farben), Zeile 0 = unten."""
    rim_c, mid_c, core_c = EYE_COLS[stance]
    d = np.sqrt(((XS - 15.5) / 16.0) ** 2 + ((YS - 15.5) / 16.0) ** 2)
    out = np.zeros((32, 32, 3)) + rim_c
    out[d < 0.66] = mid_c
    out[d < 0.30] = core_c
    return out


def make_atlas(stance):
    """Atlas 128 x 128. Gesichtsfeld ganz schwarz (dunkelste Steinfarbe), Auge: in Ruhe matt, in der Ankuendigung blutrot."""
    a = np.zeros((128, 128, 3)) + EI[0]
    fns = {"flesh": f_flesh, "pale": f_pale, "shroud": f_shroud, "tabard": f_tabard, "hide": f_hide, "dark": f_dark,
           "leather": f_leather, "wood": f_wood, "chain": f_chain, "brass": f_brass, "lantern": f_lantern,
           "coif": f_coif, "band": f_band, "bone": f_bone}
    for name, fn in fns.items():
        x0, y0 = FIELDS[name]
        a[y0:y0 + 32, x0:x0 + 32] = fn()
    x0, y0 = FIELDS["face"]
    a[y0:y0 + 32, x0:x0 + 32] = DARKEST
    x0, y0 = FIELDS["eye"]
    a[y0:y0 + 32, x0:x0 + 32] = eye_field(stance)
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




def rec_part(name, pivot=(0, 0, 0)):
    """Starres Teil mit dem Drehpunkt im Ursprung: die Ecken liegen in Spiel-Koordinaten, nichts bewegt sich in der Datei."""
    return Rec(name, (0, 0, 0), UV)


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


# --- Kopf mit schwarzem Gesicht und einem Auge ----------------------------------------------------------------------------

EYE_DX = -0.050                  # Modell-x; seine rechte Seite ist -x
EYE_HALF = (0.036, 0.024)        # Halbachsen des Auges (m)


def conform_fan(rec, grp, parts, prefixes, origin, direction, pts2d, thick, rect, gap, up_hint):
    """Platte, die sich der Oberflaeche anschmiegt (wie conform), aber Ober- und Unterseite sind Faecher um einen Mittelpunkt,
    der ebenfalls per Strahl auf die Haube gesetzt wird. Die Haube hat eine Kante genau in der Mitte (Ring bei y 2,0, Eckpunkt
    vorn); liegt das Auge dort, liegen alle Dreiecke des Faechers auf je einer Flaeche der Haube und die Unterseite steckt
    nirgends in ihr."""
    g = rec.name + "/" + grp
    d = V(*direction).normalized()
    p0, n0 = ray(parts, prefixes, origin, d, 0.0)
    ex, ey, ez = frame_from(n0, up_hint)
    inner, outer = [], []
    for x, y in [(0.0, 0.0)] + list(pts2d):
        q, nq = ray(parts, prefixes, p0 + ex * x + ey * y + ez * 1.0, -ez, gap)
        inner.append(q)
        outer.append(q + nq * thick)
    n = len(pts2d)
    vi = [rec.bm.verts.new(G(q - rec.pivot)) for q in inner]
    vo = [rec.bm.verts.new(G(q - rec.pivot)) for q in outer]
    u0, v0, u1, v1 = rec.rects[rect]
    xs, ys = [p[0] for p in pts2d], [p[1] for p in pts2d]
    sx, sy = max(max(xs) - min(xs), 1e-4), max(max(ys) - min(ys), 1e-4)
    uvp = [(u0 + (u1 - u0) * (x - min(xs)) / sx, v0 + (v1 - v0) * (y - min(ys)) / sy) for x, y in [(0.0, 0.0)] + list(pts2d)]
    cu = ((u0 + u1) / 2, (v0 + v1) / 2)
    cin, cout = inner[0], outer[0]
    faces = []
    for i in range(1, n + 1):
        j = i % n + 1
        top = rec._face((vo[0], vo[i], vo[j]), (uvp[0], uvp[i], uvp[j]))
        rec._orient(top, cout - cin)
        bot = rec._face((vi[0], vi[j], vi[i]), (uvp[0], uvp[j], uvp[i]))
        rec._orient(bot, cin - cout)
        side = rec._face((vi[i], vi[j], vo[j], vo[i]), (cu, cu, cu, cu))
        rec._orient(side, (inner[i] + inner[j] + outer[i] + outer[j]) / 4 - (cin + cout) / 2)
        faces += [top, bot, side]
    rec._note(g, faces)
    return g


def make_head(parts):
    """Kettenhaube mit schwarzem Gesichtsfeld; das Auge ist eine eigene Platte (Teil "eyes") auf der Haube."""
    rings = [hring(y, 0, HEAD_CZ, head_r(y), head_r(y), head_r(y) * 0.95, 10) for y in HEAD_Y]
    h = rec_part("head")
    body_q(h, "head", rings, lambda i, k: "face" if i in (2, 3) and k in (4, 5) else "coif", "flat", "dome")
    parts.append(h)
    e = rec_part("eyes")
    y = 2.0
    hc = pr((0, y, HEAD_CZ))
    face, right = pd((0, 0, 1), y), pd((1, 0, 0), y)
    conform_fan(e, "auge", parts, ["head/head"], hc + right * EYE_DX + face * 1.0, -face, ngon(*EYE_HALF, 8), 0.010, "eye",
                0.003, pd((0, 1, 0), y))
    parts.append(e)


# --- Ausruestung ---------------------------------------------------------------------------------------------------------

def hexplate(rec, grp, parts, prefixes, origin, direction, r, thick, rect, gap=0.004, n=6, up_hint=(0, 1, 0)):
    """Kleines Vieleck (Schloss, Nagelkopf, Schnalle) aufgelegt; r klein genug, dass die Flaeche darunter nicht hochsteht."""
    p, nrm = ray(parts, prefixes, origin, direction, gap)
    ex, ey, ez = frame_from(nrm, up_hint)
    pts = [p + ex * (r * math.cos(2 * math.pi * k / n)) + ey * (r * math.sin(2 * math.pi * k / n)) for k in range(n)]
    return rec.solid(grp, pts, ez * thick, rect)


# Die gekreuzten Ketten laufen bis an die Flanken des Guertels und enden dort unter einer Messingklammer (Raute).
CHAIN_EXT = [(.37, 1.222), (.425, 1.162)]        # Stuetzpunkte (x, y) der Verlaengerung bei +x (Ruhe-Koordinaten)
CHAIN_CLASP = (.425, 1.140, .044, 4)             # Klammer: x, y, Radius, Ecken
CHAIN_EXT_OFF = 0.046                            # Abstand der verlaengerten Kette von der Rumpfflaeche


def chain_links_ext(rec, grp, parts, prefixes, a, b, w, steps, off, zmode=False, sign=1):
    """Kette aus Gliedern (Breite wechselt je Stuetzpunkt: breit, schmal, ...), per Strahl auf den Rumpf gelegt und ueber das
    Ende a-b hinaus bis an den Guertel verlaengert; dort greift eine Messingklammer ueber das Ende."""
    pts, ws = [], []
    for j in range(steps):                       # der alte Endpunkt entfaellt: die Verlaengerung beginnt dort
        x = a[0] + (b[0] - a[0]) * j / steps
        y = a[1] + (b[1] - a[1]) * j / steps
        d = pd((0, 0, -1), y)
        p, n = ray(parts, prefixes, pr((x, y, 3.0)), d, 0.0 if zmode else off)
        pts.append(p - d.normalized() * off if zmode else p)
        ws.append(w if j % 2 == 0 else w * 0.62)
    for i, (x, y) in enumerate(CHAIN_EXT):
        x *= sign
        d = pd((0, 0, -1), y)
        p, n = ray(parts, prefixes, pr((x, y, 3.0)), d, 0.0)
        t_ = (i + 1) / len(CHAIN_EXT)
        pts.append(p - d.normalized() * (off * (1 - t_)) + n * (CHAIN_EXT_OFF * t_) if zmode else p + n * CHAIN_EXT_OFF)
        ws.append(w if (steps + i) % 2 == 0 else w * 0.62)
    rec.body(grp, tube(pts, [(q, 0.009, 0.009) for q in ws], 6, V(0, 0, 1)), "chain")
    cx, cy, cr, cn = CHAIN_CLASP
    hexplate(rec, grp + "_klammer", parts, prefixes, pr((cx * sign, cy, 3.0)), pd((0, 0, -1), cy), cr, .014, "brass",
             gap=.075, n=cn)


# --- Stab mit Laterne (hoechstens 2,6 m mit Haken) ----------------------------------------------------------------------

POLE_BACK, POLE_FWD = 1.18, 1.18


def make_weapon(hand, dn, back=POLE_BACK):
    w = rec_part("pole")
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
    return w


TORSO_ALL = ["torso/body"]
LEGS_ALL = ["leg_l/leg", "leg_r/leg"]


def gear(g, parts, rings, legrings, armr, arml):
    T = TORSO_ALL
    # gekreuzte blaue Ketten, enden an den Flanken des Guertels
    chain_links_ext(g, "kette1", parts, T, (-.40, 1.74), (.30, 1.28), .034, 10, .030, sign=1)
    chain_links_ext(g, "kette2", parts, T, (.40, 1.72), (-.30, 1.28), .034, 10, .115, zmode=True, sign=-1)
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
    # Guertel mit Schnalle
    a_, b_ = ring_at(rings, T_Y, 1.07), ring_at(rings, T_Y, 1.13)
    band(g, "gurt", a_, b_, .016, .040, "leather")
    hexplate(g, "schnalle", parts, T, pr((0, 1.10, 3.0)), pd((0, 0, -1), 1.10), .042, .014, "brass", gap=.041, n=4)
    # linke Wade: Riemen mit Schnalle
    y0, y1 = 0.395, 0.445
    ra, rb = ring_at(legrings[0], LEG_Y, y0), ring_at(legrings[0], LEG_Y, y1)
    band(g, "riemen0", ra, rb, .005, .024, "leather")
    hexplate(g, "rschnalle0", parts, ["leg_l/leg"], V(-.325, (y0 + y1) / 2, 3.0), V(0, 0, -1), .020, .010,
             "brass", gap=.025, n=4)
    # loses Verbandsende am Ruecken
    strap(g, "ende1", parts, T, (.16, 1.60), (.22, 1.26), .034, 4, .032, rect="band", thick=.009, back=True)


# --- Figur ---------------------------------------------------------------------------------------------------------------

HANDS = {  # Ruhe-Koordinaten vor der Pose: (rechts, Stabrichtung, links)
    False: ((.62, 1.22, .72), (.04, 1, -.05), (-.70, .95, .44)),
    True: ((.80, 1.84, .84), (-.12, .40, .90), (-.56, 1.62, .98)),
}
ARM_RADII_R = (0.07, 0.15, 0.135, 0.125, 0.115, 0.095)
ARM_RADII_L = (0.07, 0.155, 0.14, 0.125, 0.115, 0.095)
PART_NAMES = ["leg_l", "leg_r", "torso", "head", "eyes", "arm_l", "arm_r", "gear", "pole"]


def build(stance):
    """Alle Teile der Figur in der Haltung stance ("rest" oder "announce")."""
    global POSE
    ank = stance == "announce"
    POSE = Pose(-10.0, 16.0) if ank else Pose()
    parts = []
    legs = {}
    for s, mats in ((-1, ("leather", "shroud", "shroud")), (1, ("leather", "leather", "hide"))):
        leg, rings_ = make_leg(s, mats)
        parts.append(leg)
        legs[s] = rings_
    rings = [torso_ring(y) for y in T_Y]
    t = rec_part("torso")
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
    g = rec_part("gear")
    gear(g, parts, rings, (legs[-1], legs[1]), R, L)
    parts.append(g)
    fa = (armr[0] - armr[2]).normalized()
    dn = steil(dn, fa, 68.0)
    parts.append(make_weapon(armr[0], dn, 0.45 if ank else POLE_BACK))
    assert [p.name for p in parts] == PART_NAMES, [p.name for p in parts]
    return parts


# --- Szene, Pruefungen, Ausgabe -------------------------------------------------------------------------------------------

def put_in_scene(parts, mat, tag):
    coll = bpy.data.collections.new(tag)
    bpy.context.scene.collection.children.link(coll)
    return [p.build(coll, mat, smooth=True) for p in parts]


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    os.makedirs(OUT, exist_ok=True)
    pal_all = {tuple(c) for rows in PAL.values() for c in rows}
    blood = {tuple(c) for c in BR}
    fx, fy = FIELDS["face"]
    for stance in STANCES:
        atlas = make_atlas(stance)
        cols = {tuple(c) for c in atlas.reshape(-1, 3)}
        assert cols <= pal_all, "Farbe ausserhalb der Palette"
        assert len(cols) <= 32, f"{stance}: {len(cols)} Farben"
        assert any(c in blood for c in cols) == (stance == "announce"), f"{stance}: Rot nur in der Ankuendigung"
        assert (atlas[fy:fy + 32, fx:fx + 32] == DARKEST).all(), "Gesichtsfeld nicht ganz schwarz"
        name = "crypt_warden_" + stance
        image = save_image(name, atlas, os.path.join(OUT, name + ".png"))
        parts = build(stance)
        found, bad, selfx = clip_check(parts, ALLOWED)
        assert not bad and not selfx, f"{stance}: Durchdringung {bad} {selfx}"
        objs = put_in_scene(parts, export_material("cw_" + stance, image), "exp_" + stance)
        report = closed_report(objs)
        open_edges = sum(r[1] for r in report.values())
        multi = sum(r[2] for r in report.values())
        assert open_edges == 0 and multi == 0, f"{stance}: offene Kanten {open_edges}, Mehrfachkanten {multi}"
        assert all(r[3] > 0 for r in report.values()), f"{stance}: Rauminhalt nicht positiv"
        tris = tri_count(objs)
        assert sum(tris.values()) <= MAX_TRIANGLES, tris
        lo, hi = extent(objs)
        assert hi[1] <= 2.6, f"{stance}: {hi[1]:.2f} m hoch"
        export_glb(objs, os.path.join(OUT, name + ".glb"))
        print(stance, "Dreiecke", tris, "Summe", sum(tris.values()), "offene Kanten", open_edges,
              "Durchdringungen erlaubt", len(found),
              "x %.2f..%.2f y %.2f..%.2f z %.2f..%.2f" % (lo[0], hi[0], lo[1], hi[1], lo[2], hi[2]))
        for o in objs:
            bpy.data.objects.remove(o, do_unlink=True)


main()
