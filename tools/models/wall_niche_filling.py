"""Fuellungen der tiefen Wandnische (Issue #553, Teil 2): Flaschen, Kerzen, Schaedel.

Erzeuger fuer das Spiel. Uebergabe aus dem abgenommenen Probestueck design/proben/grafik/wandnische-tief-538/wandnische_tief.py
(Branch feat/wandnische-tief-probe-538, Kopf ea5109d). Abgenommen sind:
- Flaschen "Stapel mit Korken" in Gruen, mit Staub: sieben liegende Flaschen in den Lagen 3-2-2, Haelse mit Korken nach vorn, in
  die Ecke aus linker Laibung und Rueckwand geschoben (dort fuellung_flaschen_stapel_gruen);
- Kerzen ohne Flamme (fuellung_kerzen_aus; das Licht kommt spaeter);
- Schaedel (fuellung_schaedel).
Form, Atlas und Anordnung sind unveraendert uebernommen; nur die Teil- und Dateinamen sind englisch.

Aufruf (aus dem Repository-Wurzelverzeichnis), danach python tools/models/checksums.py:
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/models/wall_niche_filling.py

Alles per Skript, nichts heruntergeladen, kein Zufall ohne festen Seed (zwei Laeufe liefern gleiche Dateien). Spiel-Achsen
(x seitlich, y oben, z vorn), Meter. Ursprung der Fuellung: Mitte der Nische, Standflaeche y = 0, Rueckwand der Tasche z = 0; die
Fuellung wird nach +z aufgebaut. Das Spiel stellt sie auf die Sohlbank (Flaschen) oder auf das Bord (Kerzen, Schaedel) und
schiebt sie um die Tiefe der Tasche (0,3 m) an die Rueckwand (WallNicheFillingLook).

Die Flaschen sind 0,36 m lang, die Tasche 0,3 m tief: das vordere Ende liegt bei z = 0,3605, also 0,0605 m vor der Wand und
damit knapp hinter der Vorderkante des Gewaendes (0,0625 m vor der Wand). Das Skript prueft das, ein Test im Spiel auch.

Dateien: wall_niche_bottles.glb, wall_niche_candles.glb, wall_niche_skulls.glb mit .png. Jedes Netz rundum geschlossen, nur
Farben der Hauptpalette (ungenutzte Atlasflaeche: dunkelste Stufe der Reihe Stein). Es wird keine Textur gelesen, nur die
Palette; darum gibt es keine Quellen-Datei wie bei der Nische selbst - die Pruefsummen in models.sha256 decken die Dateien ab.
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from psx import V, G, Part, rects_from_pixels, save_image, export_material, export_glb, report_parts  # noqa: E402
from pillar_kit import OUT_DIR, colours_of, edge_report, palette_rows  # noqa: E402

R = palette_rows()
HK, HZ, GD = R["Haut/Knochen"], R["Holz"], R["Messing/Gold"]

FRAME_FRONT = 0.0625      # Vorderkante des Gewaendes vor der Wand
POCKET = 0.30             # Tiefe der Tasche
SHELF_GAP = 0.375         # lichte Hoehe von der Sohlbank (0,5) bis unter das Bord (0,875)


# --- Bausteine (wie im Probestueck) ------------------------------------------------------------------------------------

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


# --- Flaschen: Stapel mit Korken, Gruen, mit Staub ---------------------------------------------------------------------

GLAS = dict(dark=HK[0], mid=HK[1], light=HK[2], hi=HK[3])
STAUB = dict(dark=(HK[1], HK[2]), mid=(HK[2], HK[3]), light=(HK[3], HK[4]), hi=(HK[4], HK[5]))
FS = 1.1                                           # etwas groesser als eine echte Flasche, damit sie aus 3 m lesbar bleibt
FL_PROFIL = [(h * FS, r * FS) for (h, r) in ((0.0, 0.054), (0.165, 0.054), (0.205, 0.020), (0.285, 0.020), (0.325, 0.028))]
FL_L = 0.325 * FS
FL_R = 0.054 * FS
FL_RF = FL_R * math.cos(math.radians(30))          # halbe Breite ueber die flachen Seiten
FL_KORK_AB = 0.285 * FS
FL_SIZE = (96, 72)
TILE_W, TILE_H = 24, 28                            # Staubkachel: 24 Pixel entlang der Flasche, 28 rund um die Flasche
TILE_Y = 40


def bottle_atlas():
    g, st_ = GLAS, STAUB

    def disc(w, h, dusty=False):
        rs = np.random.RandomState(5)
        out = np.zeros((h, w, 3))
        for py in range(h):
            for px in range(w):
                rho = math.hypot(px - (w - 1) / 2, py - (h - 1) / 2) / (w / 2)
                col = g["dark"] if rho < 0.42 else g["mid"] if rho < 0.70 else g["light"] if rho < 0.90 else g["mid"]
                if dusty and rho >= 0.42 and rs.rand() < 0.22:
                    col = st_["mid"][0] if rho < 0.70 else st_["light"][0]
                out[py, px] = col
        return out

    def tile(shade, seed):
        def fn(w, h):
            rs = np.random.RandomState(seed)
            out = np.zeros((h, w, 3)) + np.array(g[shade], float)
            r = rs.rand(h, w)
            out[r < 0.24] = np.array(st_[shade][0], float)
            out[r < 0.08] = np.array(st_[shade][1], float)
            return out
        return fn

    colors = {"dark": g["dark"], "mid": g["mid"], "light": g["light"], "hi": g["hi"], "cork": HZ[3]}
    extra = {"disc": ((0, 16, 16, 32), disc), "discd": ((16, 16, 32, 32), lambda w, h: disc(w, h, True))}
    for i, shade in enumerate(("dark", "mid", "light", "hi")):
        extra["d_" + shade] = ((TILE_W * i, TILE_Y, TILE_W * (i + 1), TILE_Y + TILE_H), tile(shade, 11 + i))
    return swatch_atlas(colors, extra, FL_SIZE)


def bottle_into(fl, base, theta, yaw, off=0.0):
    """Eine Flasche mit Staub. base = Mittelpunkt des Bodens, theta = Neigung um z (-90 Grad liegend), yaw = Drehung um die
    senkrechte Achse (-90 Grad: Hals nach +z). off verschiebt die Helligkeitsstufe."""
    ct, st = math.cos(theta), math.sin(theta)
    cy, sy = math.cos(yaw), math.sin(yaw)

    def rv(v):
        x1, y1 = v.x * ct - v.y * st, v.x * st + v.y * ct
        return V(x1 * cy + v.z * sy, y1, -x1 * sy + v.z * cy)

    rings = []
    for (h, r) in FL_PROFIL:
        rings.append([V(r * math.cos(math.radians(30 + 60 * k)), h, r * math.sin(math.radians(30 + 60 * k))) for k in range(6)])
    r0 = FL_PROFIL[0][1]

    def disc_uv(p, rect):
        u0, v0, u1, v1 = rect
        return (u0 + (u1 - u0) * (0.5 + p.x / (2 * r0)), v0 + (v1 - v0) * (0.5 + p.z / (2 * r0)))

    def style(n, mid):
        if n.y < -0.9:
            return ("planar", "discd", disc_uv)
        if n.y > 0.9 or mid.y > FL_KORK_AB:
            return ("swatch", "cork")
        w = rv(n)
        s = w.y * 0.6 + w.z * 0.55 + w.x * 0.25 + off
        shade = "hi" if s > 0.7 else "light" if s > 0.42 else "mid" if s > 0.08 else "dark"
        if w.y < 0.15:
            return ("swatch", shade)
        m = math.atan2(n.z, n.x) % (2 * math.pi)

        def uv(p, rect, m=m):
            u0, v0, u1, v1 = rect
            d = (math.atan2(p.z, p.x) - m + math.pi) % (2 * math.pi) - math.pi
            a = min(max(p.y * 64.0 / TILE_W, 0.02), 0.98)
            b = min(max((m + d + 0.55) * FL_R * 64.0 / TILE_H, 0.02), 0.98)
            return (u0 + (u1 - u0) * a, v0 + (v1 - v0) * b)
        return ("planar", "d_" + shade, uv)

    fl.solid(ring_solid(rings), V(0, FL_L / 2, 0), style, lambda p: rv(p) + base)


PX, PY = 0.124, 0.1065                 # Mittenabstand der Flaschen: 4 mm Fuge zwischen den Sechsecken
X0 = -0.5 + FL_R + 0.003               # erste Flasche an der linken Laibung
Y0 = FL_RF + 0.001
Z_BACK = 0.003                         # Boden 3 mm vor der Rueckwand; der Korken endet bei 0,3605
TONES = (0.0, -0.10, 0.08)
LAGEN = ((0.0, 3), (0.5, 2), (0.0, 2))  # (Versatz in Flaschenabstaenden, Anzahl) je Lage von unten: Haelse mit Korken nach vorn


def build_bottles():
    arr, pix = bottle_atlas()
    part = fpart("bottles", pix, FL_SIZE)
    fl = Fill(part, pix, FL_SIZE)
    k = 0
    for j, (dx0, cnt) in enumerate(LAGEN):
        for i in range(cnt):
            x, y = X0 + (dx0 + i) * PX, Y0 + j * PY
            bottle_into(fl, V(x, y, Z_BACK), math.radians(-90), math.radians(-90), TONES[k % 3])
            k += 1
    assert k == 7
    return part, arr


# --- Kerzen (ohne Flamme) -----------------------------------------------------------------------------------------------

def candle_atlas():
    colors = {"wax_hi": HK[7], "wax_lt": HK[6], "wax_mid": HK[5], "wax_dk": HK[3], "wick": HZ[0],
              "fl_core": GD[7], "fl_mid": GD[6], "fl_edge": GD[5]}
    return swatch_atlas(colors)


KERZEN = [(-0.2, 0.10, 0.27, 0.034), (-0.08, 0.07, 0.16, 0.030), (0.03, 0.12, 0.23, 0.032), (0.14, 0.08, 0.09, 0.034),
          (0.24, 0.13, 0.06, 0.030)]
TOPS = [0.0, -0.012, -0.022, -0.010, 0.0, 0.007]       # heruntergebrannt: schiefe Oberkante
LUMPS = [(-0.02, 0.22, 0.026), (0.2, 0.04, 0.022)]     # Wachsreste auf der Standflaeche


def build_candles():
    arr, pix = candle_atlas()
    part = fpart("candles", pix)
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

    for (x, z, h, r) in KERZEN:
        rings = [ring6(x, z, r * 1.8, 0.0), ring6(x, z, r * 1.3, 0.016), ring6(x, z, r, 0.034), ring6(x, z, r, h, TOPS)]
        fl.solid(ring_solid(rings), V(x, h / 2, z), wax_style, make_T(0, 0, 0))
        wick = [ring6(x + 0.002, z, 0.004, h - 0.01), ring6(x + 0.002, z, 0.004, h + 0.014)]
        fl.solid(ring_solid(wick), V(x, h, z), lambda n, m: ("swatch", "wick"), make_T(0, 0, 0))
    for (x, z, r) in LUMPS:
        rings = [ring6(x, z, r * 1.5, 0.0), ring6(x, z, r, 0.012), ring6(x, z, r * 0.8, 0.02)]
        fl.solid(ring_solid(rings), V(x, 0.01, z), wax_style, make_T(0, 0, 0))
    return part, arr


# --- Schaedel ------------------------------------------------------------------------------------------------------------

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
            out[4:8, px] = HK[7] if (px // 2) % 2 == 0 else HK[1]
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
    fl.solid(ring_solid(jr), V(0, k * 0.022, 0), jaw_style, T)


def build_skulls():
    arr, pix = skull_atlas()
    part = fpart("skulls", pix)
    fl = Fill(part, pix)
    skull_into(fl, -0.15, 0.0, 0.12, math.radians(14))
    skull_into(fl, 0.15, 0.0, 0.14, math.radians(-22))
    skull_into(fl, 0.0, 0.128, 0.10, math.radians(4))
    return part, arr


# --- Ausgabe ------------------------------------------------------------------------------------------------------------
# Name -> (Bauvorschrift, Dreiecke wie im Probestueck, groesste Hoehe ueber der Standflaeche)
FILLINGS = {
    "bottles": (build_bottles, 392, SHELF_GAP),
    "candles": (build_candles, 384, None),
    "skulls": (build_skulls, 264, None),
}


def game(co):
    return (co.x, co.z, -co.y)


def bounds(part):
    pts = [game(v.co) for v in part.bm.verts]
    return [min(q[i] for q in pts) for i in range(3)], [max(q[i] for q in pts) for i in range(3)]


def triangles(part):
    return sum(len(f.verts) - 2 for f in part.bm.faces)


def check_palette(name, tex):
    allowed = {tuple(int(c) for c in p) for row in R.values() for p in row}
    cols = {tuple(int(c) for c in p) for p in np.round(tex).reshape(-1, 3)}
    assert not cols - allowed, f"{name}: Farben ausserhalb der Hauptpalette {sorted(cols - allowed)}"


def write(kind):
    build, tris, max_height = FILLINGS[kind]
    name = f"wall_niche_{kind}"
    bpy.ops.wm.read_factory_settings(use_empty=True)
    coll = bpy.data.collections.new("Filling")
    bpy.context.scene.collection.children.link(coll)
    part, tex = build()
    check_palette(name, tex)
    lo, hi = bounds(part)
    assert lo[0] >= -0.5 and hi[0] <= 0.5, f"{name}: breiter als die Oeffnung ({lo[0]:.4f} bis {hi[0]:.4f})"
    assert lo[1] > -1e-6, f"{name}: unter der Standflaeche ({lo[1]:.4f})"
    assert lo[2] > 0.0, f"{name}: hinter der Rueckwand ({lo[2]:.4f})"
    assert hi[2] <= POCKET + FRAME_FRONT, f"{name}: ragt ueber das Gewaende in den Gang ({hi[2]:.4f})"
    if max_height is not None:
        assert hi[1] <= max_height, f"{name}: stoesst an das Bord ({hi[1]:.4f})"
    open_edges, multi = edge_report(part)
    assert open_edges == 0 and multi == 0, f"{name}: {open_edges} offene Kanten, {multi} mehrfach"
    total = triangles(part)
    assert total == tris, f"{name}: {total} Dreiecke statt {tris} wie im Probestueck"
    image = save_image(name, tex, os.path.join(OUT_DIR, name + ".png"))
    material = export_material(name, image)
    objs = [part.build(coll, material, smooth=False)]
    export_glb(objs, os.path.join(OUT_DIR, name + ".glb"))
    report_parts(name, objs)
    print(f"FUELLUNG {name}: {total} Dreiecke, Farben {colours_of(tex)}, x {lo[0]:.4f} bis {hi[0]:.4f}, "
          f"y 0 bis {hi[1]:.4f}, z {lo[2]:.4f} bis {hi[2]:.4f}")


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    for kind in FILLINGS:
        write(kind)


main()
