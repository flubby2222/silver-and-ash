"""Probestueck Skelett mit Armbrust (Issue #334), Runde 2: Fassung A "Waechter" in drei Ausbaustufen A1, A2, A3.

Alles entsteht aus diesem Skript: Geometrie, Textur-Atlas (nur Farben der Hauptpalette, hoechstens 32), glb-Modelle,
Bilder. Nichts wird heruntergeladen; das Spiel wird nicht angefasst.

Aufruf (aus dem Repository-Wurzelverzeichnis, ohne Argumente; TEMP/TMP auf einen Ordner ausserhalb von /tmp setzen):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/armbrust-skelett-334/runde-2/armbrust2.py

Ausgabe in diesem Ordner: blatt.png, modelle/armbrust_skelett_<A1|A2|A3>_<ruhe|ank>.glb, texturen/atlas_<ruhe|angriff>.png,
masse.txt. Koordinaten: Spiel-Achsen (x seitlich, y oben, z vorn = Blickrichtung der Figur), Meter, Ursprung zwischen den Fuessen.
Zwei Haltungen je Stufe: "ruhe" (Armbrust bereit vor der Brust) und "ank" (Ankuendigung: angelegt, Kopf auf dem Schaft,
Kiefer offen, rote Pupillen).
"""

import json
import math
import os
import sys
import tempfile

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE)))))
sys.path.insert(0, os.path.join(ROOT, "tools", "models"))
import blf  # noqa: E402
import bpy  # noqa: E402
import imbuf  # noqa: E402
from mathutils import Matrix  # noqa: E402
from psx import (V, G, FOG_COLOR, Part, tube, rects_from_pixels, vnoise, save_image, export_material,  # noqa: E402
                 pass_material, setup_render, camera, aim, render_passes, compose, save_png, write_light,
                 stone_textures, build_room, export_glb, to_game, MATS, coverage)

MODELS = os.path.join(ROOT, "assets", "models")
ROUND1 = os.path.join(os.path.dirname(HERE), "modelle", "armbrust_skelett_A.glb")
STAGES = ("A1", "A2", "A3")
LEVEL = {"A1": 1, "A2": 2, "A3": 3}
POSES = ("ruhe", "ank")

PAL = {r["name"]: np.array(r["farben"], float)
       for k in ("grund", "akzent")
       for r in json.load(open(os.path.join(ROOT, "assets", "palette", "hauptpalette.json"), encoding="utf-8"))[k]}
ST, HK, SF, HZ, EI, GO, BR = (PAL["Stein"], PAL["Haut/Knochen"], PAL["Stoff"], PAL["Holz"], PAL["Eisen"],
                              PAL["Messing/Gold"], PAL["Blutrot"])

# --- Textur-Atlas 128 x 128 (y von unten) ----------------------------------------------------------------------------

RECTS_PX = {"skull": (0, 80, 64, 128), "bone": (64, 96, 96, 128), "bone_dark": (96, 96, 128, 128),
            "cloth": (0, 40, 32, 80), "cloth_dark": (32, 40, 64, 80), "iron": (64, 40, 96, 80),
            "wood": (96, 40, 128, 80), "leather": (0, 0, 32, 40), "jaw": (32, 0, 64, 40),
            "rust": (64, 0, 96, 40), "cloth_light": (96, 0, 128, 40), "chain": (64, 80, 96, 96)}
UV = rects_from_pixels(RECTS_PX, 128)


def pick(n, levels, colors):
    """Wert 0..1 -> harte Stufen aus Palettenfarben (kein Verlauf)."""
    out = np.zeros(n.shape + (3,))
    idx = np.zeros(n.shape, int)
    for k, t in enumerate(levels):
        idx[n >= t] = k + 1
    for k, c in enumerate(colors):
        out[idx == k] = c
    return out


def p_bone(U, V_):
    h, w = U.shape
    col = pick(vnoise(h, w, 4, 4, 11), (0.3, 0.62), (HK[5], HK[6], HK[7]))
    col[np.abs(U - 0.5) < 0.1] = HK[7]                     # Licht von oben
    col[(U < 0.12) | (U > 0.88)] = HK[4]                   # Unterseite im Schatten
    col[(V_ < 0.1) | (V_ > 0.9)] = HK[4]                   # Gelenkenden
    dirt = vnoise(h, w, 6, 6, 21)
    col[((V_ < 0.22) | (V_ > 0.78)) & (dirt > 0.55)] = HK[3]    # Schmutz in den Gelenken
    crack = np.abs(U - (0.30 + 0.04 * np.sin(V_ * 14))) < 0.022
    col[crack & (V_ > 0.3) & (V_ < 0.72)] = HK[3]          # Riss laengs
    return col


def p_bone_dark(U, V_):
    h, w = U.shape
    col = pick(vnoise(h, w, 4, 4, 12), (0.35, 0.7), (HK[3], HK[4], HK[5]))
    col[np.abs(U - 0.5) < 0.1] = HK[5]
    col[(V_ < 0.08) | (V_ > 0.92)] = HK[3]
    return col


def p_skull(angry):
    def paint(U, V_):
        h, w = U.shape
        col = pick(vnoise(h, w, 5, 5, 13), (0.3, 0.65), (HK[5], HK[6], HK[7]))
        col[V_ > 0.78] = HK[7]
        col[(np.abs(U - 0.5) > 0.13) & (np.abs(U - 0.5) < 0.23) & (V_ > 0.12) & (V_ < 0.34)] = HK[4]   # hohle Wangen
        col[(U < 0.2) | (U > 0.8)] = HK[5]                  # Hinterkopf
        col[(V_ < 0.04)] = HK[4]
        crack = np.abs((V_ - 0.62) - 1.7 * (U - 0.60)) < 0.035
        col[crack & (U > 0.58) & (U < 0.72) & (V_ > 0.60) & (V_ < 0.92)] = HK[3]       # Riss ueber dem rechten Auge
        col[(V_ > 0.62) & (V_ < 0.7) & (np.abs(U - 0.5) < 0.2) & (vnoise(h, w, 4, 8, 31) > 0.6)] = HK[4]   # Brauenschatten
        for sgn in (-1, 1):
            cu = 0.5 + sgn * 0.085
            sock = ((U - cu) / 0.06) ** 2 + ((V_ - 0.43 - sgn * 0.01) / 0.12) ** 2 < 1
            col[sock] = EI[0]
            pup = ((U - cu) / 0.026) ** 2 + ((V_ - 0.42) / 0.05) ** 2 < 1
            if angry:
                col[pup] = BR[6]                            # Blutrot, hart, ohne Schein (Urteil 22)
                col[pup & (((U - cu) / 0.026) ** 2 + ((V_ - 0.42) / 0.05) ** 2 > 0.45)] = BR[5]
            else:
                col[pup] = HK[3]                            # tote, truebe Augenpunkte
        nose = (V_ > 0.2) & (V_ < 0.33) & (np.abs(U - 0.5) < 0.012 + 0.05 * (0.33 - V_))
        col[nose] = EI[0]
        mouth = (V_ < 0.15) & (np.abs(U - 0.5) < 0.13)
        tooth = ((U * 44) % 1.0 < 0.6) & (V_ > 0.045 + 0.025 * np.sin(U * 90))
        col[mouth] = EI[0]
        col[mouth & tooth] = HK[7]
        return col
    return paint


def p_jaw(U, V_):
    h, w = U.shape
    col = pick(vnoise(h, w, 4, 4, 41), (0.3, 0.62), (HK[5], HK[6], HK[7]))
    col[V_ < 0.25] = HK[4]                                  # Kinn im Schatten
    teeth = V_ > 0.55
    col[teeth] = HK[7]
    col[teeth & (((U * 26) % 1.0) < 0.22)] = HK[3]          # Zahnluecken
    col[teeth & (V_ > 0.9)] = HK[6]
    col[(V_ > 0.3) & (V_ < 0.55) & (vnoise(h, w, 3, 6, 42) > 0.62)] = HK[3]
    return col


def p_cloth(U, V_):
    h, w = U.shape
    folds = 0.5 + 0.5 * np.cos(2 * np.pi * U * 8 + 2.5 * vnoise(h, w, 2, 6, 14))
    col = pick(folds, (0.35, 0.7), (SF[3], SF[4], SF[5]))
    col[V_ < 0.1] = SF[2]
    return col


def p_cloth_dark(U, V_):
    h, w = U.shape
    return pick(vnoise(h, w, 5, 5, 15), (0.4, 0.75), (SF[2], SF[3], SF[4]))


def p_cloth_light(U, V_):
    """Heller, abgetragener Stoff fuer Tuchfetzen: Falten, dunkler Saum, Flecken."""
    h, w = U.shape
    folds = 0.5 + 0.5 * np.cos(2 * np.pi * U * 6 + 2.0 * vnoise(h, w, 2, 5, 51))
    col = pick(folds, (0.3, 0.68), (SF[5], SF[6], SF[7]))
    col[V_ < 0.12] = SF[4]
    col[(V_ < 0.25) & (vnoise(h, w, 4, 6, 52) > 0.6)] = SF[3]       # Schmutz am Saum
    col[(V_ > 0.55) & (vnoise(h, w, 3, 3, 53) > 0.78)] = SF[4]       # Flecken
    col[(V_ > 0.92)] = SF[7]
    return col


def p_iron(U, V_):
    h, w = U.shape
    col = pick(vnoise(h, w, 4, 4, 16), (0.4, 0.75), (EI[5], EI[6], EI[7]))
    col[(V_ < 0.08) | (V_ > 0.92)] = EI[4]
    col[(U < 0.06) | (U > 0.94)] = EI[4]
    col[np.abs(V_ - (0.3 + 0.35 * U)) < 0.03] = EI[4]       # Kratzer
    return col


def p_rust(U, V_):
    h, w = U.shape
    col = pick(vnoise(h, w, 4, 4, 22), (0.4, 0.75), (EI[3], EI[4], EI[5]))
    r = vnoise(h, w, 5, 3, 23) + 0.35 * (1 - V_)           # Rost sammelt sich unten
    col[r > 0.82] = HZ[3]
    col[r > 1.02] = HZ[2]
    col[((U * 9) % 1.0 < 0.18) & (V_ < 0.6) & (vnoise(h, w, 2, 9, 24) > 0.5)] = HZ[2]    # Laufspuren
    col[(V_ < 0.08) | (V_ > 0.92)] = EI[2]
    col[(U < 0.06) | (U > 0.94)] = EI[2]
    return col


def p_chain(U, V_):
    """Kettengeflecht: schraege Gitterlinien, helle Ringe."""
    h, w = U.shape
    a = ((U * 16 + V_ * 8) % 1.0 < 0.5) ^ ((U * 16 - V_ * 8) % 1.0 < 0.5)
    col = np.zeros((h, w, 3)) + EI[3]
    col[a] = EI[5]
    col[(V_ < 0.15) & (vnoise(h, w, 3, 8, 61) > 0.5)] = HZ[2]
    return col


def p_wood(U, V_):
    h, w = U.shape
    grain = 0.5 + 0.5 * np.sin(2 * np.pi * (U * 5 + 3 * vnoise(h, w, 2, 2, 17)))
    col = pick(grain, (0.4, 0.8), (HZ[3], HZ[4], HZ[5]))
    knot = ((U - 0.7) / 0.1) ** 2 + ((V_ - 0.4) / 0.14) ** 2 < 1
    col[knot] = HZ[2]
    col[(V_ < 0.06) | (V_ > 0.94)] = HZ[2]
    return col


def p_leather(U, V_):
    h, w = U.shape
    col = pick(vnoise(h, w, 4, 4, 18), (0.45, 0.8), (HZ[3], HZ[4], HZ[5]))
    col[(np.abs(U - 0.5) < 0.04) & (((V_ * 14) % 1.0) < 0.5)] = HZ[7]      # Naht
    col[(V_ < 0.07) | (V_ > 0.93)] = HZ[2]
    return col


def make_atlas(angry):
    a = np.zeros((128, 128, 3)) + EI[0]
    painters = {"skull": p_skull(angry), "bone": p_bone, "bone_dark": p_bone_dark, "cloth": p_cloth,
                "cloth_dark": p_cloth_dark, "iron": p_iron, "wood": p_wood, "leather": p_leather, "jaw": p_jaw,
                "rust": p_rust, "cloth_light": p_cloth_light, "chain": p_chain}
    for name, (x0, y0, x1, y1) in RECTS_PX.items():
        ph, pw = y1 - y0, x1 - x0
        U, V_ = np.meshgrid((np.arange(pw) + 0.5) / pw, (np.arange(ph) + 0.5) / ph)
        a[y0:y1, x0:x1] = painters[name](U, V_)
    return a


# --- Bausteine -----------------------------------------------------------------------------------------------------

class Xf:
    """Starre Verschiebung/Drehung (Spiel-Achsen): pitch hebt die Vorderkante (+z) nach oben, yaw dreht nach +x."""

    def __init__(self, t=(0, 0, 0), pitch=0.0, yaw=0.0):
        self.t = V(*t)
        self.M = Matrix.Rotation(yaw, 3, "Y") @ Matrix.Rotation(-pitch, 3, "X")

    def p(self, q):
        return self.t + self.M @ V(*q)

    def d(self, q):
        return self.M @ V(*q)


IDENT = Xf()


def bone(p, pts, rad, rect="bone", sides=6, ref=None, X=IDENT):
    """Knochen: Rohr durch pts (lokale Spiel-Achsen, per X versetzt), Radien je Punkt."""
    c = [X.p(q) for q in pts]
    axis = (c[-1] - c[0]).normalized()
    ref = X.d(ref) if ref is not None else (V(0, 0, 1) if abs(axis.y) > 0.7 else V(0, 1, 0))
    p.loft(tube(c, [(r, r, r) for r in rad], sides, ref_up=ref), rect)


def xbox(P, X, lo, hi, rect):
    x0, y0, z0 = lo
    x1, y1, z1 = hi
    P.prism([X.p((x0, y0, z0)), X.p((x1, y0, z0)), X.p((x1, y0, z1)), X.p((x0, y0, z1))], X.d((0, y1 - y0, 0)), rect)


def xprism(P, X, pts, off, rect):
    P.prism([X.p(q) for q in pts], X.d(off), rect)


def xspike(P, X, base, tip, half, rect, sides=4):
    P.spike(X.p(base), X.p(tip), half, rect, sides)


def mid(a, b, f=0.5):
    return tuple(np.array(a, float) * (1 - f) + np.array(b, float) * f)


def head_xf(c, nod, dx):
    """Kopfhaltung: Drehung nach vorn (nod) um den Halsansatz, dazu seitlicher Versatz."""
    nk = c + V(0, -0.09, -0.02)
    M = Matrix.Rotation(nod, 3, "X")
    return lambda q: nk + M @ (V(*q) - nk) + V(dx, 0, 0)


def skull(p, c, s, T):
    """Schaedel wie beim Ghul gebaut (Gesicht vorn bei Ecke n/2); T = Kopfhaltung."""
    n = 10
    rows = [(-0.085, 0.040, 0.070, 0.025), (-0.045, 0.058, 0.078, 0.060), (-0.005, 0.068, 0.074, 0.080),
            (0.035, 0.071, 0.084, 0.086), (0.080, 0.068, 0.066, 0.090), (0.118, 0.046, 0.038, 0.062)]

    def dent(i, k):
        if i == 2 and k in (4, 6):
            return 0.82
        if i == 1 and k in (3, 7):
            return 0.86
        return 1.0

    rings = []
    for i, (h, w, front, back) in enumerate(rows):
        ring = []
        for k in range(n):
            th = 2 * math.pi * k / n
            depth = -math.cos(th)
            d = (front if depth > 0 else back) * dent(i, k) * s
            ring.append(T(c + V(w * math.sin(th) * dent(i, k) * s, h * s, depth * d)))
        rings.append(ring)
    p.loft(rings, "skull")


def jaw(p, c, s, T, drop):
    """Unterkiefer: kleiner Keil unter dem Schaedel (Zahnreihe auf der Vorderflaeche), drop = geoeffnet in Metern."""
    y = c.y - 0.082 * s - drop
    pts = [(-0.050, y - 0.038, c.z - 0.02), (0.050, y - 0.038, c.z - 0.02), (0.026, y - 0.034, c.z + 0.075),
           (-0.026, y - 0.034, c.z + 0.075)]
    top = [(-0.058, y, c.z - 0.02), (0.058, y, c.z - 0.02), (0.034, y, c.z + 0.086), (-0.034, y, c.z + 0.086)]
    polys = [[T(V(*q)) for q in pts], [T(V(*q)) for q in reversed(top)]]
    for k in range(4):
        a, b = pts[k], pts[(k + 1) % 4]
        a2, b2 = top[k], top[(k + 1) % 4]
        polys.append([T(V(*a)), T(V(*b)), T(V(*b2)), T(V(*a2))])
    p.convex(polys, "jaw")


def legs(p, lv, stride=0.1, spread=0.0):
    for s in (1, -1):
        fz = stride if s > 0 else -stride * 0.6
        hip, knee, ank = (s * 0.10, 0.95, 0.0), (s * (0.11 + spread * 0.5), 0.51, 0.05 + fz * 0.5), (s * (0.11 + spread), 0.09, fz)
        bone(p, [hip, mid(hip, knee), knee], (0.042, 0.032, 0.040))
        bone(p, [knee, mid(knee, ank), ank], (0.038, 0.028, 0.034))
        toe = (ank[0], 0.04, ank[2] + 0.17)
        p.loft(tube([V(*ank), V(*ank) + V(0, -0.03, 0.07), V(*toe)], [(0.04, 0.022, 0.022), (0.045, 0.02, 0.02),
                                                                        (0.03, 0.015, 0.015)], 6, ref_up=V(0, 1, 0)), "bone")
        p.box((knee[0] - 0.03, knee[1] - 0.03, knee[2] - 0.02), (knee[0] + 0.03, knee[1] + 0.03, knee[2] + 0.04), "bone_dark")
        if lv >= 2:
            p.spike((knee[0], knee[1], knee[2] + 0.03), (knee[0], knee[1] + 0.005, knee[2] + 0.085), 0.026, "bone")


def pelvis(p, lv):
    p.loft(tube([V(-0.14, 0.96, 0), V(0, 0.97, 0), V(0.14, 0.96, 0)],
                [(0.06, 0.05, 0.05), (0.07, 0.06, 0.06), (0.06, 0.05, 0.05)], 6, ref_up=V(0, 1, 0)), "bone_dark")
    for s in (1, -1):    # Darmbeinschaufeln
        p.prism([(s * 0.06, 0.95, -0.03), (s * 0.16, 0.96, -0.03), (s * 0.19, 1.08, -0.03), (s * 0.08, 1.09, -0.03)],
                (0, 0, 0.06), "bone")
    # Wirbelsaeule hinten im Brustkorb, am Hals wieder nach vorn zum Schaedel
    spine = [(0, 0.99, -0.02), (0, 1.20, -0.05), (0, 1.40, -0.05), (0, 1.55, -0.02), (0, 1.64, 0.04)]
    bone(p, spine, (0.034, 0.03, 0.03, 0.028, 0.026), "bone_dark", sides=5)
    if lv >= 3:
        for y in (1.19, 1.27, 1.35, 1.43, 1.51):
            p.spike((0, y, -0.06), (0, y - 0.012, -0.108), 0.017, "bone_dark")


def ribcage(p, lv):
    pairs = 6 if lv == 1 else 7
    y0, dy = (1.17, 0.062) if lv == 1 else (1.15, 0.056)
    wmaxs = [0.14, 0.16, 0.168, 0.162, 0.148, 0.12] if lv == 1 else [0.13, 0.15, 0.162, 0.168, 0.164, 0.15, 0.125]
    r = 0.028 if lv == 1 else 0.031
    for i in range(pairs):
        y = y0 + dy * i
        wmax = wmaxs[i]
        for s in (1, -1):
            bone(p, [(0, y, -0.05), (s * wmax, y + 0.006, 0.015), (s * 0.025, y - 0.03, 0.105)], (r, r * 1.2, r), sides=4)
    # Brustbein als helle Platte vorn
    top, bot = 1.52, 1.12
    p.prism([(-0.034, top, 0.086), (0.034, top, 0.086), (0.018, bot, 0.086), (-0.018, bot, 0.086)], (0, 0, 0.044), "bone")


def collar_and_blades(p):
    for s in (1, -1):
        bone(p, [(0, 1.515, 0.095), (s * 0.10, 1.545, 0.07), (s * 0.205, 1.52, 0.03)], (0.024, 0.022, 0.026), "bone", sides=4)
        p.prism([(s * 0.03, 1.50, -0.08), (s * 0.17, 1.495, -0.07), (s * 0.13, 1.33, -0.075), (s * 0.04, 1.36, -0.08)],
                (0, 0, -0.022), "bone")                         # Schulterblatt
        p.box((s * 0.205 - 0.032, 1.485, -0.005), (s * 0.205 + 0.032, 1.545, 0.065), "bone_dark")    # Schultergelenk


def fist(p, c, X=IDENT, size=0.085):
    """Faeustling: geschlossener Klotz (Mitte c in Weltachsen, wie X ausgerichtet), keine Finger (Regel: keine Haende)."""
    h = size / 2
    c = V(*c)
    base = [c + X.d((sx * h, -h, sz * h * 1.2)) for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    p.prism(base, X.d((0, h * 1.9, 0)), "bone_dark")


def ik(S, H, pole, l1=0.28, l2=0.28):
    S, H = np.array(S, float), np.array(H, float)
    d = H - S
    dist = np.linalg.norm(d)
    u = d / dist
    dist = min(dist, (l1 + l2) * 0.995)
    a = (l1 * l1 - l2 * l2 + dist * dist) / (2 * dist)
    hh = math.sqrt(max(l1 * l1 - a * a, 0.0))
    pv = np.array(pole, float)
    pv = pv - u * pv.dot(u)
    pv /= np.linalg.norm(pv)
    return tuple(S + u * a + pv * hh)


def arm(p, sh, wr, pole, lv, X=IDENT, r=0.036):
    el = ik(sh, wr, pole)
    bone(p, [sh, mid(sh, el), el], (r * 1.1, r * 0.85, r))
    bone(p, [el, mid(el, wr), wr], (r * 0.95, r * 0.75, r * 0.8))
    p.box((el[0] - 0.028, el[1] - 0.028, el[2] - 0.028), (el[0] + 0.028, el[1] + 0.028, el[2] + 0.028), "bone_dark")
    fist(p, wr, X)
    return el, wr


def crossbow(P, X, lv, long=0.80, span=0.40, tip=0.15):
    """Armbrust in lokalen Koordinaten (Kolben bei z = 0, zeigt nach +z, y = 0 auf Hoehe der Sehne), per X versetzt."""
    h = 0.02
    xb = lambda lo, hi, r: xbox(P, X, lo, hi, r)
    xb((-0.028, -0.034, 0.0), (0.028, 0.02, long), "wood")                       # Schaft
    xb((-0.04, -0.075, -0.005), (0.04, 0.034, 0.09), "wood")                     # Kolben
    xb((-0.022, -0.09, 0.16), (0.022, -0.034, 0.24), "wood")                     # Griff
    zb = long - 0.10
    for s in (1, -1):
        a = (s * 0.02, s * (span - tip * 0.55))
        xb((min(a), -h * 0.7, zb), (max(a), h * 0.7, zb + 0.045), "iron")        # Bogenmitte
        xs0, xs1 = s * (span - tip * 0.7), s * (span + tip * 0.35)
        xprism(P, X, [(xs0, -h * 0.7, zb - 0.002), (xs0 + s * 0.04, -h * 0.7, zb - 0.002),
                      (xs1, -h * 0.7, zb + tip * 1.4), (xs1 - s * 0.04, -h * 0.7, zb + tip * 1.4)], (0, h * 1.4, 0), "iron")
        tipx, tipz = xs1 - s * 0.02, zb + tip * 1.4
        bone(P, [(tipx, 0, tipz), (0, 0.002, 0.34)], (0.011, 0.011), "bone", sides=4, X=X)     # Sehne, gespannt
        if lv >= 2:     # Wicklung am Bogen
            x0, x1 = sorted((s * 0.09, s * 0.125))
            xb((x0, -h * 0.9, zb - 0.006), (x1, h * 0.9, zb + 0.05), "leather")
        if lv >= 3:
            x0, x1 = sorted((s * 0.20, s * 0.23))
            xb((x0, -h * 0.9, zb - 0.006), (x1, h * 0.9, zb + 0.05), "leather")
    if lv >= 3:
        xb((-0.032, -0.038, zb - 0.03), (0.032, 0.024, zb - 0.005), "leather")   # Verschnuerung am Schaft
        xb((-0.032, -0.038, 0.45), (0.032, 0.024, 0.48), "iron")                 # Eisenband
    # Steigbuegel
    if lv < 3:
        xb((-0.05, -0.06, long - 0.03), (0.05, 0.01, long + 0.035), "iron")
    else:
        xb((-0.05, -0.06, long - 0.01), (-0.036, 0.01, long + 0.05), "iron")
        xb((0.036, -0.06, long - 0.01), (0.05, 0.01, long + 0.05), "iron")
        xb((-0.05, -0.06, long + 0.036), (0.05, -0.04, long + 0.05), "iron")
    # Nuss und Abzug
    xb((-0.012, 0.0, 0.32), (0.012, 0.03, 0.35), "bone")
    xb((-0.008, -0.07, 0.30), (0.008, -0.034, 0.325), "iron")
    if lv >= 3:
        xb((-0.008, -0.07, 0.22), (0.008, -0.06, 0.30), "iron")                  # Abzugsbuegel
    # Bolzen mit Spitze
    bone(P, [(0, 0.03, 0.35), (0, 0.03, long + 0.12)], (0.014, 0.014), "wood", sides=4, X=X)
    xspike(P, X, (0, 0.03, long + 0.12), (0, 0.03, long + 0.22), 0.026, "iron")
    if lv >= 3:
        xb((-0.002, 0.006, 0.37), (0.002, 0.07, 0.43), "cloth_light")            # Federn
        xb((-0.03, 0.028, 0.37), (0.03, 0.032, 0.43), "cloth_light")


def equipment(K, rag, lv, ank, el_r):
    """Ausruestung des Waechters, bewusst ungleich: Schulterplatte links, Bandelier und Koecher rechts."""
    # Bandelier (hell, breit) von der rechten Schulter ueber die Brust zur linken Huefte
    bone(K, [(0.115, 1.55, 0.02), (0.10, 1.465, 0.095), (0.03, 1.36, 0.138), (-0.06, 1.26, 0.142), (-0.15, 1.16, 0.075)],
         (0.034, 0.034, 0.034, 0.034, 0.034), "leather", sides=4, ref=V(0, 0, 1))
    if True:  # Schulterplatte ab A1 (heller Masseanteil am Oberkoerper)
        # Schulterplatte links: Kuppel mit Rand
        c = V(-0.215, 1.505, 0.03)
        rings = [[c + V(0.125 * math.sin(2 * math.pi * k / 6), -0.045, 0.115 * math.cos(2 * math.pi * k / 6)) for k in range(6)],
                 [c + V(0.12 * math.sin(2 * math.pi * k / 6), 0.0, 0.11 * math.cos(2 * math.pi * k / 6)) for k in range(6)],
                 [c + V(0.085 * math.sin(2 * math.pi * k / 6), 0.045, 0.08 * math.cos(2 * math.pi * k / 6)) for k in range(6)],
                 [c + V(0.04 * math.sin(2 * math.pi * k / 6), 0.07, 0.04 * math.cos(2 * math.pi * k / 6)) for k in range(6)]]
        K.loft(rings, "iron")
    if lv >= 2:
        # Koecher hinten rechts mit Bolzen
        qc = [V(0.115, 0.98, -0.13), V(0.09, 1.30, -0.115), V(0.065, 1.56, -0.095)]
        K.loft(tube(qc, [(0.05, 0.05, 0.05), (0.058, 0.058, 0.058), (0.065, 0.065, 0.065)], 6, ref_up=V(0, 0, 1)), "wood")
        for dx, dz in ((-0.025, 0.0), (0.02, 0.02), (0.03, -0.022)):
            base = (0.065 + dx, 1.55, -0.095 + dz)
            K.spike(base, (base[0] + dx * 0.6, base[1] + 0.14, base[2] + dz * 0.6), 0.013, "wood")
    if lv >= 3:
        # Eisenhaube (Kette) am Hals, Unterarmschiene rechts, Guertel mit Beutel
        c = V(0.0, 1.585, 0.02)
        rings = [[c + V(0.115 * math.sin(2 * math.pi * k / 8), -0.035, 0.105 * math.cos(2 * math.pi * k / 8)) for k in range(8)],
                 [c + V(0.075 * math.sin(2 * math.pi * k / 8), 0.015, 0.07 * math.cos(2 * math.pi * k / 8)) for k in range(8)]]
        K.loft(rings, "chain")
        e, w = el_r
        bone(K, [mid(e, w, 0.30), mid(e, w, 0.60)], (0.052, 0.050), "rust", sides=6)
        K.box((-0.15, 0.985, -0.075), (0.15, 1.02, 0.085), "leather")
        K.box((0.12, 0.88, 0.02), (0.19, 0.985, 0.09), "leather")
        # zweite Schulterlamelle
        K.prism([(-0.30, 1.435, -0.04), (-0.13, 1.445, -0.04), (-0.13, 1.445, 0.12), (-0.30, 1.435, 0.12)], (0, 0.025, 0), "rust")
    # Tuchfetzen am Becken: hell, einer lang, zwei kurz, hinten ein Lappen
    rag.prism([(-0.14, 1.03, 0.075), (0.14, 1.03, 0.075), (0.11, 0.55, 0.085), (-0.09, 0.60, 0.085)], (0, 0, 0.02), "cloth_light")
    if lv >= 2:
        rag.prism([(0.10, 0.99, 0.07), (0.20, 0.99, 0.05), (0.19, 0.70, 0.06), (0.12, 0.74, 0.07)], (0, 0, 0.02), "cloth_light")
        rag.prism([(-0.11, 0.99, 0.07), (-0.19, 0.99, 0.05), (-0.20, 0.78, 0.06), (-0.12, 0.84, 0.07)], (0, 0, 0.02), "cloth")
    if lv >= 3:
        rag.prism([(-0.10, 0.99, -0.075), (0.10, 0.99, -0.075), (0.08, 0.62, -0.085), (-0.06, 0.70, -0.085)], (0, 0, -0.02),
                  "cloth_dark")


# --- Stufen und Haltungen -------------------------------------------------------------------------------------------

POSE = {
    "ruhe": dict(butt=(0.055, 1.40, 0.03), pitch=0.0, yaw=0.0, stride=0.10, spread=0.0, nod=0.0, dx=0.0, drop=0.0,
                 rh=(0.0, -0.115, 0.20), lh=(0.0, -0.07, 0.50), rpole=(1.0, -0.7, -0.3), lpole=(-0.7, -0.8, -0.1)),
    "ank": dict(butt=(0.115, 1.60, -0.015), pitch=0.02, yaw=-0.05, stride=0.22, spread=0.03, nod=0.22, dx=0.045, drop=0.03,
                rh=(0.0, -0.115, 0.20), lh=(0.0, -0.07, 0.46), rpole=(1.0, -0.4, -0.5), lpole=(-0.8, -0.7, 0.0)),
}


def build(stage, pose):
    lv = LEVEL[stage]
    cfg = POSE[pose]
    body, gear, kit, rag = (Part("skelett", (0, 0, 0), UV), Part("armbrust", (0, 0, 0), UV),
                            Part("ausruestung", (0, 0, 0), UV), Part("stoff", (0, 0, 0), UV))
    for p in (gear, kit, rag):
        p.flat = True
    X = Xf(cfg["butt"], cfg["pitch"], cfg["yaw"])
    pelvis(body, lv)
    ribcage(body, lv)
    collar_and_blades(body)
    legs(body, lv, cfg["stride"], cfg["spread"])
    c = V(0, 1.725, 0.075)
    T = head_xf(c, cfg["nod"], cfg["dx"])
    skull(body, c, 1.05, T)
    jaw(body, c, 1.05, T, cfg["drop"])
    if lv >= 2:    # Wangenknochen
        for s in (1, -1):
            body.spike(T(c + V(s * 0.05, -0.03, 0.055)), T(c + V(s * 0.095, -0.045, 0.065)), 0.018, "bone")
    rwr, lwr = X.p(cfg["rh"]), X.p(cfg["lh"])
    er, wr = arm(body, (0.21, 1.50, 0.03), tuple(rwr), cfg["rpole"], lv, X)
    arm(body, (-0.21, 1.50, 0.03), tuple(lwr), cfg["lpole"], lv, X)
    crossbow(gear, X, lv)
    equipment(kit, rag, lv, pose == "ank", (er, wr))
    return [body, gear, kit, rag]


def put_in_scene(parts, mat, tag):
    coll = bpy.data.collections.new(tag)
    bpy.context.scene.collection.children.link(coll)
    objs = [p.build(coll, mat, smooth=not getattr(p, "flat", False)) for p in parts]
    for o in objs:
        HOME[o.name] = o.matrix_world.copy()
    return objs


HOME = {}


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
            g = to_game(o.matrix_world @ V(*c))
            lo, hi = np.minimum(lo, (g.x, g.y, g.z)), np.maximum(hi, (g.x, g.y, g.z))
    return lo, hi


# --- Blatt-Werkzeuge -------------------------------------------------------------------------------------------------

SCRATCH = tempfile.gettempdir()


def text(img, s, x, y, size=30, color=(235, 235, 235), bold=False):
    """Schrift ins Bild (img, Text, x, y von oben links, Groesse). Reihenfolge der Argumente nicht vertauschen."""
    f = blf.load("C:/Windows/Fonts/" + ("arialbd.ttf" if bold else "arial.ttf"))
    blf.size(f, size)
    w, h = blf.dimensions(f, s)
    W, H = int(w) + 8, int(size * 1.6)
    ib = imbuf.new((W, H))
    blf.color(f, 1, 1, 1, 1)
    blf.position(f, 4, int(size * 0.4), 0)
    with blf.bind_imbuf(f, ib):
        blf.draw_buffer(f, s)
    tmp = os.path.join(SCRATCH, "armbrust2_txt.png")
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


def render(visible, cam, loc, target, fov, size, mode, want_mask=False):
    setup_render(*size)
    cam.data.angle = math.radians(fov)
    aim(cam, loc, target)
    p = render_passes("ar2", visible)
    img = compose(p, mode)[::-1]
    return (img, coverage(p)[::-1]) if want_mask else img


def blit(sheet, tile, x, y):
    sheet[y:y + tile.shape[0], x:x + tile.shape[1]] = tile


def up(arr, f):
    return np.repeat(np.repeat(arr, f, axis=0), f, axis=1)


def lum(img):
    return img @ np.array([0.299, 0.587, 0.114])


def main():
    global SCRATCH
    SCRATCH = os.environ.get("TEMP", tempfile.gettempdir())
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    for d in ("modelle", "texturen"):
        os.makedirs(os.path.join(HERE, d), exist_ok=True)
    atlas = {"ruhe": make_atlas(False), "angriff": make_atlas(True)}
    colours = {k: len({tuple(c) for c in v.reshape(-1, 3).round().astype(int)}) for k, v in atlas.items()}
    assert max(colours.values()) <= 32, colours
    images = {k: save_image("atlas_" + k, v, os.path.join(HERE, "texturen", f"atlas_{k}.png")) for k, v in atlas.items()}
    atlas_of = {"ruhe": "ruhe", "ank": "angriff"}

    # 1. glb und Dreiecke
    stats = {}
    for st in STAGES:
        for pose in POSES:
            mat = export_material(f"as_{st}_{pose}", images[atlas_of[pose]])
            objs = put_in_scene(build(st, pose), mat, f"exp_{st}_{pose}")
            export_glb(objs, os.path.join(HERE, "modelle", f"armbrust_skelett_{st}_{pose}.glb"))
            lo, hi = extent(objs)
            stats[(st, pose)] = (tri_count(objs), lo, hi)
            for o in objs:
                bpy.data.objects.remove(o, do_unlink=True)

    # 2. Szenen
    pm = {k: pass_material("pm_" + k, images[k]) for k in images}
    sets = {(st, pose): put_in_scene(build(st, pose), pm[atlas_of[pose]], f"r_{st}_{pose}") for st in STAGES for pose in POSES}
    cam = camera()
    stone = stone_textures(tempfile.mkdtemp(prefix="as_stein_", dir=SCRATCH))
    room = build_room(stone)

    def import_glb(path, flat_names=()):
        before = set(bpy.data.objects)
        bpy.ops.import_scene.gltf(filepath=path)
        objs = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
        for o in objs:
            image = next(n.image for n in o.active_material.node_tree.nodes if n.type == "TEX_IMAGE")
            o.data.materials.clear()
            o.data.materials.append(pass_material("imp_" + o.name, image))
            o["smooth"] = o.name.split(".")[0] not in flat_names
            HOME[o.name] = o.matrix_world.copy()
        return objs

    ghoul = import_glb(os.path.join(MODELS, "ghoul.glb"))
    r1 = import_glb(ROUND1, ("armbrust",))
    r1_tris = sum(len(o.data.polygons) for o in r1)
    r1_lo, r1_hi = extent(r1)
    figs = {"R1": r1}
    figs.update({st: sets[(st, "ruhe")] for st in STAGES})
    order = ("R1",) + STAGES
    stats[("R1", "ruhe")] = ({"gesamt": r1_tris}, r1_lo, r1_hi)

    def parked(objs):
        place(objs, (200, 0, 0))

    # 2a. Ansichten: vorn, Seite, hinten, schraeg oben
    TW, TH = 300, 520
    dirs = (("vorn", V(0, 0.05, 1)), ("Seite", V(1, 0.05, 0)), ("hinten", V(0, 0.05, -1)), ("schräg oben", V(0.7, 0.5, 0.7)))
    tiles = {}
    for k in order:
        place(figs[k], (0, 0, 0))
        lo, hi = stats[(k, "ruhe")][1], stats[(k, "ruhe")][2]
        c = V(0.0, 0.98, (lo[2] + hi[2]) / 2)
        for nm, d in dirs:
            tiles[(k, nm)] = render(figs[k], cam, G(c + d.normalized() * 6.5), G(c), 20, (TW, TH), "ansicht")
        parked(figs[k])
    # Ansichten der Ankuendigung (nur Stufen)
    atiles = {}
    for st in STAGES:
        place(sets[(st, "ank")], (0, 0, 0))
        lo, hi = stats[(st, "ank")][1], stats[(st, "ank")][2]
        c = V(0.0, 0.98, (lo[2] + hi[2]) / 2)
        for nm, d in dirs[:2]:
            atiles[(st, nm)] = render(sets[(st, "ank")], cam, G(c + d.normalized() * 6.5), G(c), 20, (TW, TH), "ansicht")
        parked(sets[(st, "ank")])

    # 2b. Nahaufnahmen: Kopf und Brust, Armbrust (ohne Nebel)
    close = {}
    for st in STAGES:
        place(sets[(st, "ruhe")], (0, 0, 0))
        hc = V(0.0, 1.52, 0.08)
        close[(st, "kopf")] = render(sets[(st, "ruhe")], cam, G(hc + V(0.5, 0.2, 1.0).normalized() * 2.4), G(hc), 20, (330, 400), "ansicht")
        xc = V(0.06, 1.40, 0.42)
        close[(st, "armbrust")] = render(sets[(st, "ruhe")], cam, G(xc + V(0.8, 0.55, 0.55).normalized() * 2.4), G(xc), 24, (470, 400),
                                         "ansicht")
        parked(sets[(st, "ruhe")])

    # 2c. Spiel: aus 10 m neben dem Ghul, hell und dunkel; Messung je Figur allein
    xs = {"G": -1.9, "R1": -0.95, "A1": 0.0, "A2": 0.95, "A3": 1.9}
    allobjs = list(ghoul)
    place(ghoul, (xs["G"], 0, 0))
    for k in order:
        place(figs[k], (xs[k], 0, 0))
        allobjs += figs[k]
    eye, tgt = G(V(0, 1.6, 10)), G(V(0, 1.6, 0))
    img10, _ = render(allobjs + room, cam, eye, tgt, 70, (854, 480), "spiel", True)
    save_png(img10[::-1], os.path.join(HERE, "spiel-10m.png"))
    meas = {}
    for k in ("G",) + order:
        objs = ghoul if k == "G" else figs[k]
        for kk in ("G",) + order:
            parked(ghoul if kk == "G" else figs[kk])
        place(objs, (0, 0, 0))
        im, mk = render(objs, cam, eye, tgt, 70, (854, 480), "spiel", True)
        cols, rows = np.where(mk.any(axis=0))[0], np.where(mk.any(axis=1))[0]
        meas[k] = (int(mk.sum()), int(cols.max() - cols.min() + 1), int(rows.max() - rows.min() + 1), float(lum(im[mk]).mean()))
    # Sichtbare Figuren wieder an ihre Plaetze (fuer die Ankuendigung wird neu gestellt)
    for kk in ("G",) + order:
        parked(ghoul if kk == "G" else figs[kk])

    # 2d. Ankuendigung aus 4 m: A1, A2, A3 angelegt
    ann = []
    for st, x in zip(STAGES, (-1.15, 0.0, 1.15)):
        place(sets[(st, "ank")], (x, 0, 0))
        ann += sets[(st, "ank")]
    img4 = render(ann + room, cam, G(V(0, 1.6, 4)), G(V(0, 1.6, 0)), 70, (854, 480), "spiel")
    save_png(img4[::-1], os.path.join(HERE, "spiel-ankuendigung-4m.png"))
    for st in STAGES:
        parked(sets[(st, "ank")])

    # masse.txt
    with open(os.path.join(HERE, "masse.txt"), "w", encoding="utf-8", newline="\n") as f:
        for st in ("R1",) + STAGES:
            for pose in POSES:
                if (st, pose) not in stats:
                    continue
                t, lo, hi = stats[(st, pose)]
                f.write(f"{st} {pose}: Dreiecke {t} (Summe {sum(t.values())}), x {lo[0]:.2f}..{hi[0]:.2f}, "
                        f"y {lo[1]:.2f}..{hi[1]:.2f}, z {lo[2]:.2f}..{hi[2]:.2f}, Breite {hi[0] - lo[0]:.2f}, Hoehe {hi[1]:.2f}\n")
        f.write(f"Farben in der Textur: ruhe {colours['ruhe']}, angriff {colours['angriff']} (Grenze 32)\n")
        f.write("Silhouette aus 10 m im Spiel (854x480, ohne Raum): Figur: Pixelflaeche, Breite px, Hoehe px, mittlere Helligkeit 0..255\n")
        for k, v in meas.items():
            f.write(f"  {k}: {v[0]} px, {v[1]} x {v[2]}, Helligkeit {v[3]:.1f}\n")

    # 3. Blatt
    M, GAP = 30, 20
    CELLW = 4 * TW
    W = M * 2 + 2 * CELLW + GAP
    head = 150
    y1 = head
    row_h = TH + 100
    y2 = y1 + 2 * row_h + 20
    close_h = 400 + 110
    y3 = y2 + close_h + 20
    H = y3 + 1420
    sheet = np.zeros((H, W, 3)) + FOG_COLOR
    text(sheet, "Skelett mit Armbrust, Probestück zu Issue 334, Runde 2: Wächter in drei Ausbaustufen", M, 12, 46, bold=True)
    text(sheet, "Links oben die Grundfassung aus Runde 1 zum Vergleich. A1 vorsichtig, A2 mittel, A3 reich. "
                "Rot nur in den Pupillen der Ankündigung.", M, 76, 28, (200, 190, 205))
    labels = {"R1": "Runde 1, Grundfassung A", "A1": "A1 vorsichtig", "A2": "A2 mittel", "A3": "A3 reich"}
    for i, k in enumerate(order):
        cx, cy = M + (i % 2) * (CELLW + GAP), y1 + (i // 2) * row_h
        t, lo, hi = stats[(k, "ruhe")]
        n = sum(t.values())
        text(sheet, f"{labels[k]}: {n} Dreiecke, {hi[1]:.2f} m hoch, {hi[0] - lo[0]:.2f} m breit", cx, cy, 34, GO[5] if k != "R1" else (200, 200, 210), True)
        for j, (nm, _) in enumerate(dirs):
            blit(sheet, tiles[(k, nm)], cx + j * TW, cy + 50)
            text(sheet, nm, cx + j * TW + 6, cy + 52, 24)
    # Nahaufnahmen
    text(sheet, "Nahaufnahmen (ohne Nebel): Kopf und Brust, Armbrust", M, y2, 34, bold=True)
    for i, st in enumerate(STAGES):
        cx = M + i * 810
        blit(sheet, close[(st, "kopf")], cx, y2 + 60)
        blit(sheet, close[(st, "armbrust")], cx + 330, y2 + 60)
        text(sheet, st, cx + 6, y2 + 62, 34, GO[5], True)
    # Ankuendigung und 10 m
    y = y3
    ax = M
    text(sheet, "Ankündigung aus 4 m: A1, A2, A3", ax, y, 34, bold=True)
    blit(sheet, up(img4[190:400, 230:630], 2), ax, y + 56)
    # Ankuendigungs-Seitenansichten
    sy = y + 56 + 420 + 30
    text(sheet, "Ankündigung von der Seite (ohne Nebel): A1, A2, A3", ax, sy, 30, bold=True)
    for i, st in enumerate(STAGES):
        small = atiles[(st, "Seite")][60:480, 30:270]
        blit(sheet, small, ax + i * 250, sy + 50)
    # 10 m
    rx = M + 930
    crop = lambda im: im[200:304, 340:514]       # 174 x 104
    z = 6
    lit = up(crop(img10), z)                       # 1044 x 624
    dark = np.clip(FOG_COLOR + (lit - FOG_COLOR) * 0.45, 0, 255)
    text(sheet, "Aus 10 m im Spiel: Ghul, R1, A1, A2, A3 (sechsfach vergrößert)", rx, y, 34, bold=True)
    blit(sheet, lit, rx, y + 56)
    text(sheet, "Dasselbe dunkel (Helligkeit der Figuren auf 45 %)", rx, y + 56 + 624 + 14, 34, bold=True)
    blit(sheet, dark, rx, y + 56 + 624 + 70)
    for i, k in enumerate(("G",) + order):
        px = rx + int(522 + xs[k] * 34.3 * z) - 30
        text(sheet, {"G": "Ghul", "R1": "R1"}.get(k, k), px, y + 56 + 4, 30, bold=True)
    save_png(sheet[::-1], os.path.join(HERE, "blatt.png"))
    print("FARBEN", colours)
    print("MESSUNG", meas)
    print("DREIECKE", {k: sum(v[0].values()) for k, v in stats.items()})


main()
