"""Skelett mit Armbrust fuer das Spiel (Issue #497): zwei Haltungen, Ruhe und Ankuendigung.

Uebergabe aus dem Probestueck zu Issue #334, Runde 4, Fassung F (flacher Holzbogen, Ausbaustufe A2), vom Menschen
abgenommen. Geometrie, Haltungen und Textur sind unveraendert aus design/proben/grafik/armbrust-skelett-334/runde-2 und
runde-4 uebernommen, hier aber nur noch die abgenommene Fassung und ohne Vorschau-Blatt.

Alles aus einem Skript: Geometrie, Textur-Atlas (nur Farben der Hauptpalette, hoechstens 32), zwei .glb samt .png nach
assets/models/. Nichts heruntergeladen.

Aufruf (aus dem Repository-Wurzelverzeichnis, ohne Argumente), danach die Pruefsummen neu schreiben:
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/models/crossbow_skeleton.py
    python tools/models/checksums.py
Umgebungsvariable CROSSBOW_SKELETON_OUT leitet die Ausgabe in einen anderen Ordner (fuer den Vergleich zweier Laeufe).

Ausgabe: assets/models/crossbow_skeleton_rest.glb|png (Ruhe: Armbrust vor der Brust, tote Augenpunkte) und
crossbow_skeleton_announce.glb|png (Ankuendigung: angelegt, Kopf auf dem Schaft, Kiefer offen, Sehne zur Nuss gezogen,
rote Pupillen). Rot steht nur in der Textur der Ankuendigung und dort nur in den Pupillen. Beide .glb haben dieselben
fuenf starren Teile: "skelett", "armbrust", "ausruestung", "stoff", "sehne".
Koordinaten: Spiel-Achsen (x seitlich, y oben, z vorn = Blickrichtung der Figur), Meter, Ursprung zwischen den Fuessen.
"""

import json
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
from mathutils import Matrix  # noqa: E402
from psx import (V, Part, tube, rects_from_pixels, vnoise, save_image, export_material, export_glb, to_game,  # noqa: E402
                 MATS)

OUT = os.environ.get("CROSSBOW_SKELETON_OUT") or os.path.join(ROOT, "assets", "models")
STANCES = ("rest", "announce")
PARTS = ("skelett", "armbrust", "ausruestung", "stoff", "sehne")
MAX_TRIANGLES = 1650

PAL = {r["name"]: np.array(r["farben"], float)
       for k in ("grund", "akzent")
       for r in json.load(open(os.path.join(ROOT, "assets", "palette", "hauptpalette.json"), encoding="utf-8"))[k]}
HK, SF, HZ, EI, BR = PAL["Haut/Knochen"], PAL["Stoff"], PAL["Holz"], PAL["Eisen"], PAL["Blutrot"]

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


def legs(p, stride, spread):
    for s in (1, -1):
        fz = stride if s > 0 else -stride * 0.6
        hip, knee, ank = (s * 0.10, 0.95, 0.0), (s * (0.11 + spread * 0.5), 0.51, 0.05 + fz * 0.5), (s * (0.11 + spread), 0.09, fz)
        bone(p, [hip, mid(hip, knee), knee], (0.042, 0.032, 0.040))
        bone(p, [knee, mid(knee, ank), ank], (0.038, 0.028, 0.034))
        toe = (ank[0], 0.04, ank[2] + 0.17)
        p.loft(tube([V(*ank), V(*ank) + V(0, -0.03, 0.07), V(*toe)], [(0.04, 0.022, 0.022), (0.045, 0.02, 0.02),
                                                                        (0.03, 0.015, 0.015)], 6, ref_up=V(0, 1, 0)), "bone")
        p.box((knee[0] - 0.03, knee[1] - 0.03, knee[2] - 0.02), (knee[0] + 0.03, knee[1] + 0.03, knee[2] + 0.04), "bone_dark")
        p.spike((knee[0], knee[1], knee[2] + 0.03), (knee[0], knee[1] + 0.005, knee[2] + 0.085), 0.026, "bone")


def pelvis(p):
    p.loft(tube([V(-0.14, 0.96, 0), V(0, 0.97, 0), V(0.14, 0.96, 0)],
                [(0.06, 0.05, 0.05), (0.07, 0.06, 0.06), (0.06, 0.05, 0.05)], 6, ref_up=V(0, 1, 0)), "bone_dark")
    for s in (1, -1):    # Darmbeinschaufeln
        p.prism([(s * 0.06, 0.95, -0.03), (s * 0.16, 0.96, -0.03), (s * 0.19, 1.08, -0.03), (s * 0.08, 1.09, -0.03)],
                (0, 0, 0.06), "bone")
    # Wirbelsaeule hinten im Brustkorb, am Hals wieder nach vorn zum Schaedel
    spine = [(0, 0.99, -0.02), (0, 1.20, -0.05), (0, 1.40, -0.05), (0, 1.55, -0.02), (0, 1.64, 0.04)]
    bone(p, spine, (0.034, 0.03, 0.03, 0.028, 0.026), "bone_dark", sides=5)


def ribcage(p):
    y0, dy = 1.15, 0.056
    wmaxs = [0.13, 0.15, 0.162, 0.168, 0.164, 0.15, 0.125]
    r = 0.031
    for i, wmax in enumerate(wmaxs):
        y = y0 + dy * i
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


def arm(p, sh, wr, pole, X=IDENT, r=0.036):
    el = ik(sh, wr, pole)
    bone(p, [sh, mid(sh, el), el], (r * 1.1, r * 0.85, r))
    bone(p, [el, mid(el, wr), wr], (r * 0.95, r * 0.75, r * 0.8))
    p.box((el[0] - 0.028, el[1] - 0.028, el[2] - 0.028), (el[0] + 0.028, el[1] + 0.028, el[2] + 0.028), "bone_dark")
    fist(p, wr, X)
    return el, wr


# --- Armbrust mit flachem Holzbogen (Fassung F) ---------------------------------------------------------------------
# Bogen: a = 0..1 vom Mittelpunkt zur Spitze. Der Bogen liegt waagerecht (keine Hebung der Spitzen), die Spitzen 0,18 m
# hinter der Mitte. In beiden Haltungen dieselbe Form; es aendern sich nur Sehne (zur Nuss gezogen) und Bolzen.

BOW = dict(tx=0.52, back=0.18, y0=0.030, a=(0, 0.2, 0.4, 0.6, 0.8, 1.0), w=(0.036, 0.013), v=(0.036, 0.013), sides=6)
SEHNE_Y = 0.03        # Hoehe der Sehne in der Mitte = Hoehe der Bolzenachse
NUSS_Z = 0.34         # dorthin zieht die Ankuendigung die Sehne


def limb_point(u, zb):
    a = abs(u)
    s = 1 if u >= 0 else -1
    return (s * BOW["tx"] * a, BOW["y0"], zb - BOW["back"] * a ** 2)


def lerp(pair, a):
    return pair[0] + (pair[1] - pair[0]) * a ** 0.9


def inset(a, b):
    """Die Kappen eines Rohrs mit zwei Ringen ragen um ein Viertel der Laenge hinaus (Pol von psx.Part.loft). Rueckt man die
    Ringe je um ein Sechstel nach innen, enden die Spitzen genau auf a und b."""
    return tuple(np.array(a, float) + (np.array(b, float) - np.array(a, float)) / 6), \
        tuple(np.array(b, float) + (np.array(a, float) - np.array(b, float)) / 6)


def limb_tube(P, X, zb, u0, u1, grow, mat):
    """Rohrstueck des Schenkels zwischen den Parametern u0 und u1 (etwas dicker: Wicklung, Band), Enden laufen spitz zu."""
    d = (u1 - u0) / 6
    us = [u0 + d, u1 - d]
    pts = [X.p(limb_point(u, zb)) for u in us]
    shapes = [(lerp(BOW["w"], abs(u)) + grow, lerp(BOW["v"], abs(u)) + grow, lerp(BOW["v"], abs(u)) + grow) for u in us]
    P.loft(tube(pts, shapes, BOW["sides"], ref_up=X.d((0, 1, 0))), mat)


def crossbow(P, S, X, announce, long=0.80):
    """Armbrust in lokalen Koordinaten (Kolben bei z = 0, zeigt nach +z, y = 0 auf Hoehe der Sehne), per X versetzt.
    Die Sehne geht in das eigene Teil S."""
    xb = lambda lo, hi, r: xbox(P, X, lo, hi, r)       # noqa: E731
    xb((-0.028, -0.034, 0.0), (0.028, 0.02, long), "wood")                       # Schaft
    xb((-0.04, -0.075, -0.005), (0.04, 0.034, 0.09), "wood")                     # Kolben
    xb((-0.022, -0.09, 0.16), (0.022, -0.034, 0.24), "wood")                     # Griff
    zb = long - 0.10
    # Bogen: ein durchgehendes Rohr von Spitze zu Spitze, Mitte dick, Enden duenn
    half = list(BOW["a"])
    us = [-a for a in reversed(half[1:])] + half
    pts = [X.p(limb_point(u, zb)) for u in us]
    shapes = [(lerp(BOW["w"], abs(u)), lerp(BOW["v"], abs(u)), lerp(BOW["v"], abs(u))) for u in us]
    P.loft(tube(pts, shapes, BOW["sides"], ref_up=X.d((0, 1, 0))), "wood")
    # Lederwicklung in der Mitte und zwei Baender an den Enden
    limb_tube(P, X, zb, -0.14, 0.14, 0.009, "leather")
    for s in (1, -1):
        limb_tube(P, X, zb, s * 0.50, s * 0.56, 0.006, "leather")
    # Sehne: von Spitze zu Spitze; Ruhe gerade, Ankuendigung zur Nuss gezogen (nur sie bewegt sich)
    tipL, tipR = limb_point(-1.0, zb), limb_point(1.0, zb)
    zs = NUSS_Z if announce else tipL[2]
    centre = (0.0, SEHNE_Y, zs)
    for a, b in ((tipL, centre), (centre, tipR)):
        a2, b2 = inset(a, b)
        bone(S, [a2, b2], (0.012, 0.012), "bone", sides=4, X=X)
    # Steigbuegel
    xb((-0.05, -0.06, long - 0.03), (0.05, 0.01, long + 0.035), "iron")
    # Nuss und Abzug
    xb((-0.012, 0.0, 0.32), (0.012, 0.03, 0.35), "bone")
    xb((-0.008, -0.07, 0.30), (0.008, -0.034, 0.325), "iron")
    # Bolzen mit Spitze (Ruhe: hinten an der Sehne, Ankuendigung: auf der Nuss)
    z0 = 0.35 if announce else zs + 0.02
    bone(P, [(0, 0.03, z0), (0, 0.03, long + 0.12)], (0.014, 0.014), "wood", sides=4, X=X)
    xspike(P, X, (0, 0.03, long + 0.12), (0, 0.03, long + 0.22), 0.026, "iron")


def equipment(K, rag):
    """Ausruestung, bewusst ungleich: Schulterplatte links, Bandelier und Koecher rechts, Tuchfetzen am Becken."""
    # Bandelier (hell, breit) von der rechten Schulter ueber die Brust zur linken Huefte
    bone(K, [(0.115, 1.55, 0.02), (0.10, 1.465, 0.095), (0.03, 1.36, 0.138), (-0.06, 1.26, 0.142), (-0.15, 1.16, 0.075)],
         (0.034, 0.034, 0.034, 0.034, 0.034), "leather", sides=4, ref=V(0, 0, 1))
    # Schulterplatte links: Kuppel mit Rand
    c = V(-0.215, 1.505, 0.03)
    rings = [[c + V(0.125 * math.sin(2 * math.pi * k / 6), -0.045, 0.115 * math.cos(2 * math.pi * k / 6)) for k in range(6)],
             [c + V(0.12 * math.sin(2 * math.pi * k / 6), 0.0, 0.11 * math.cos(2 * math.pi * k / 6)) for k in range(6)],
             [c + V(0.085 * math.sin(2 * math.pi * k / 6), 0.045, 0.08 * math.cos(2 * math.pi * k / 6)) for k in range(6)],
             [c + V(0.04 * math.sin(2 * math.pi * k / 6), 0.07, 0.04 * math.cos(2 * math.pi * k / 6)) for k in range(6)]]
    K.loft(rings, "iron")
    # Koecher hinten rechts mit Bolzen
    qc = [V(0.115, 0.98, -0.13), V(0.09, 1.30, -0.115), V(0.065, 1.56, -0.095)]
    K.loft(tube(qc, [(0.05, 0.05, 0.05), (0.058, 0.058, 0.058), (0.065, 0.065, 0.065)], 6, ref_up=V(0, 0, 1)), "wood")
    for dx, dz in ((-0.025, 0.0), (0.02, 0.02), (0.03, -0.022)):
        base = (0.065 + dx, 1.55, -0.095 + dz)
        K.spike(base, (base[0] + dx * 0.6, base[1] + 0.14, base[2] + dz * 0.6), 0.013, "wood")
    # Tuchfetzen am Becken: hell, einer lang, einer kurz, links ein dunklerer
    rag.prism([(-0.14, 1.03, 0.075), (0.14, 1.03, 0.075), (0.11, 0.55, 0.085), (-0.09, 0.60, 0.085)], (0, 0, 0.02), "cloth_light")
    rag.prism([(0.10, 0.99, 0.07), (0.20, 0.99, 0.05), (0.19, 0.70, 0.06), (0.12, 0.74, 0.07)], (0, 0, 0.02), "cloth_light")
    rag.prism([(-0.11, 0.99, 0.07), (-0.19, 0.99, 0.05), (-0.20, 0.78, 0.06), (-0.12, 0.84, 0.07)], (0, 0, 0.02), "cloth")


# --- Haltungen -------------------------------------------------------------------------------------------------------

POSE = {
    "rest": dict(butt=(0.055, 1.40, 0.03), pitch=0.0, yaw=0.0, stride=0.10, spread=0.0, nod=0.0, dx=0.0, drop=0.0,
                 rh=(0.0, -0.115, 0.20), lh=(0.0, -0.07, 0.50), rpole=(1.0, -0.7, -0.3), lpole=(-0.7, -0.8, -0.1)),
    "announce": dict(butt=(0.115, 1.60, -0.015), pitch=0.02, yaw=-0.05, stride=0.22, spread=0.03, nod=0.22, dx=0.045,
                     drop=0.03, rh=(0.0, -0.115, 0.20), lh=(0.0, -0.07, 0.46), rpole=(1.0, -0.4, -0.5),
                     lpole=(-0.8, -0.7, 0.0)),
}


def build(stance):
    cfg = POSE[stance]
    body, gear, kit, rag, string = (Part(n, (0, 0, 0), UV) for n in PARTS)
    for p in (gear, kit, rag, string):
        p.flat = True
    X = Xf(cfg["butt"], cfg["pitch"], cfg["yaw"])
    pelvis(body)
    ribcage(body)
    collar_and_blades(body)
    legs(body, cfg["stride"], cfg["spread"])
    c = V(0, 1.725, 0.075)
    T = head_xf(c, cfg["nod"], cfg["dx"])
    skull(body, c, 1.05, T)
    jaw(body, c, 1.05, T, cfg["drop"])
    for s in (1, -1):    # Wangenknochen
        body.spike(T(c + V(s * 0.05, -0.03, 0.055)), T(c + V(s * 0.095, -0.045, 0.065)), 0.018, "bone")
    arm(body, (0.21, 1.50, 0.03), tuple(X.p(cfg["rh"])), cfg["rpole"], X)
    arm(body, (-0.21, 1.50, 0.03), tuple(X.p(cfg["lh"])), cfg["lpole"], X)
    crossbow(gear, string, X, stance == "announce")
    equipment(kit, rag)
    return [body, gear, kit, rag, string]


def put_in_scene(parts, mat, tag):
    coll = bpy.data.collections.new(tag)
    bpy.context.scene.collection.children.link(coll)
    return [p.build(coll, mat, smooth=not getattr(p, "flat", False)) for p in parts]


def extent(objs):
    lo, hi = np.full(3, 1e9), np.full(3, -1e9)
    for o in objs:
        for c in o.bound_box:
            g = to_game(o.matrix_world @ V(*c))
            lo, hi = np.minimum(lo, (g.x, g.y, g.z)), np.maximum(hi, (g.x, g.y, g.z))
    return lo, hi


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    os.makedirs(OUT, exist_ok=True)
    atlas = {"rest": make_atlas(False), "announce": make_atlas(True)}
    for k, a in atlas.items():
        colours = len({tuple(c) for c in a.reshape(-1, 3).round().astype(int)})
        assert colours <= 32, (k, colours)
    # Rot (Blutrot) steht nur in der Ankuendigung
    blood = {tuple(c) for c in BR.round().astype(int)}
    red = {k: any(tuple(c) in blood for c in a.reshape(-1, 3).round().astype(int)) for k, a in atlas.items()}
    assert red == {"rest": False, "announce": True}, red
    for stance in STANCES:
        name = "crossbow_skeleton_" + stance
        image = save_image(name, atlas[stance], os.path.join(OUT, name + ".png"))
        objs = put_in_scene(build(stance), export_material("cs_" + stance, image), "exp_" + stance)
        assert [o.name for o in objs] == list(PARTS), [o.name for o in objs]
        export_glb(objs, os.path.join(OUT, name + ".glb"))
        tris = {o.name: len(o.data.polygons) for o in objs}
        assert sum(tris.values()) <= MAX_TRIANGLES, tris
        lo, hi = extent(objs)
        print(stance, "Dreiecke", tris, "Summe", sum(tris.values()),
              "x %.2f..%.2f y %.2f..%.2f z %.2f..%.2f" % (lo[0], hi[0], lo[1], hi[1], lo[2], hi[2]))
        for o in objs:
            bpy.data.objects.remove(o, do_unlink=True)


main()
