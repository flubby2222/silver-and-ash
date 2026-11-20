"""Probestueck Gruftwaechter, Runde 2 (Issue #335): drei neue Fassungen D, E, F als Figuren mit Koerper.

Richtung des Menschen: "Weniger Kloetzchen, mehr Figur". Darum: Rumpf, Schultern, Arme, Beine aus gerundeten,
zusammenhaengenden Lofts (Ellipsoide, Roehren, Drehkoerper), Gouraud-Schattierung, keine Mauerwerk-Textur am Koerper.
Gemischt werden totes Fleisch, Leichentuch, Kutte, Rueststuecke, Leder, Ketten. Keine modellierten Haende: jeder
Waffenansatz steckt in einer Manschette. Alles entsteht aus diesem Skript, nichts wird heruntergeladen, unter assets/
wird nur gelesen (Ghul und Gorm fuer den Groessenvergleich).

Aufruf (aus dem Repository-Wurzelverzeichnis, ohne Argumente):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/gruftwaechter-335/runde-2/gruftwaechter_r2.py
Umgebungsvariablen (nur fuer die Entwicklung): GW_OUT = Ausgabeordner (Standard: dieser Ordner), GW_QUICK = Fassung
(nur Ansichten dieser Fassung rendern).

Koordinaten: Spiel-Achsen (x seitlich, y oben, z vorn = Vorderseite), Meter, Ursprung zwischen den Fuessen am Boden.
Die Texturfelder tragen y von unten (Zeile 0 = unten), wie die UV-Achse v.
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
from psx import (V, G, FOG_COLOR, Part, rects_from_pixels, vnoise, save_image, export_material,  # noqa: E402
                 pass_material, setup_render, camera, aim, render_passes, compose, save_png, write_light,
                 stone_textures, build_room, export_glb, to_game, tube, MATS)

MODELS = os.path.join(ROOT, "assets", "models")
VARIANTS = ("D", "E", "F")
NAMES = {"D": "Hammerträger", "E": "Sargdeckel-Kutte", "F": "Laternenträger"}

PAL = {r["name"]: np.array(r["farben"], float)
       for k in ("grund", "akzent")
       for r in json.load(open(os.path.join(ROOT, "assets", "palette", "hauptpalette.json")))[k]}
HK, SF, HO, EI, MG, BR = (PAL["Haut/Knochen"], PAL["Stoff"], PAL["Holz"], PAL["Eisen"], PAL["Messing/Gold"],
                          PAL["Blutrot"])

# --- Textur: 128 x 128, 16 Felder je 32 x 32, nur Palettenfarben, harte Stufen (kein Dithering) ---------------------

NAMES_F = ("flesh", "pale", "shroud", "tabard", "robe", "plate", "dark", "leather",
           "wood", "chain", "brass", "hollow", "eye", "hide", "lantern", "coif")
FIELDS = {n: ((i % 4) * 32, (i // 4) * 32) for i, n in enumerate(NAMES_F)}
UV = rects_from_pixels({k: (x, y, x + 32, y + 32) for k, (x, y) in FIELDS.items()}, 128)


def nz(cy, cx, seed):
    return vnoise(32, 32, cy, cx, seed)


def pick(layers, base):
    """layers: Liste (Maske, Farbe); spaetere ueberdecken fruehere. Harte Kanten, nur Palettenfarben."""
    out = np.zeros((32, 32, 3)) + base
    for mask, col in layers:
        out[mask] = col
    return out


XS, YS = np.meshgrid(np.arange(32), np.arange(32))


def f_flesh(seed=11):
    n, m = nz(6, 4, seed), nz(8, 5, seed + 1)
    return pick([(n > 0.6, HK[3]), (np.abs(m - 0.5) < 0.03, HK[1]), (m > 0.93, HK[1])], HK[2])


def f_pale():
    n, m = nz(6, 4, 21), nz(8, 5, 22)
    return pick([(n > 0.6, HK[5]), (m > 0.9, HK[3]), (np.abs(m - 0.3) < 0.03, HK[3])], HK[4])


def f_shroud():
    n, m = nz(3, 8, 31), nz(2, 10, 32)
    return pick([(n > 0.6, HK[6]), (n < 0.3, HK[4]), (m > 0.8, HK[3]), ((YS + XS // 3) % 11 == 0, HK[4])], HK[5])


def f_tabard():
    n = nz(4, 6, 41)
    stripe = (XS >= 13) & (XS <= 18)
    rip = (nz(3, 9, 42) > 0.88)
    return pick([(n > 0.6, SF[4]), (n < 0.25, SF[2]), (stripe, MG[2]), (stripe & (YS % 6 < 2), MG[3]),
                 (rip, EI[0])], SF[3])


def f_robe():
    n = nz(2, 8, 51)
    return pick([(n > 0.55, SF[3]), (n < 0.3, SF[1]), ((XS % 8) == 3, SF[1]), ((XS % 8) == 4, SF[4]),
                 (nz(4, 4, 52) > 0.9, HK[3])], SF[2])


def f_plate():
    n = nz(4, 4, 61)
    return pick([(n > 0.78, EI[2]), (n < 0.22, EI[4]), ((XS > 10) & (XS < 15), EI[5]),
                 ((XS % 16 == 8) & (YS % 16 == 8), EI[5]), (YS == 0, EI[2]), (YS == 31, EI[4])], EI[3])


def f_dark():
    n = nz(5, 5, 71)
    return pick([(n > 0.7, EI[1]), (n < 0.15, EI[3]), ((YS % 8) == 7, EI[3])], EI[2])


def f_leather():
    n = nz(4, 4, 81)
    return pick([(n > 0.6, HO[2]), (n < 0.25, HO[1]), ((YS % 8) == 0, HO[3])], HO[2])


def f_wood():
    n = nz(2, 6, 91)
    return pick([((XS % 5) == 0, HO[1]), (n > 0.75, HO[3]), (nz(8, 3, 92) > 0.93, HO[4])], HO[2])


def f_chain():
    return pick([(((XS + YS) % 4) < 2, EI[4]), (((XS - YS) % 4) == 0, EI[3])], EI[2])


def f_brass():
    n = nz(3, 3, 101)
    return pick([(n > 0.6, MG[4]), (n < 0.3, MG[3]), ((XS > 11) & (XS < 17), MG[6]), (YS % 16 == 0, MG[3])], MG[4])


def f_hollow():
    return pick([(nz(5, 5, 111) > 0.9, EI[1])], EI[0])


def f_hide():
    n, m = nz(6, 4, 121), nz(8, 5, 122)
    return pick([(n > 0.5, HK[2]), (m > 0.9, HK[1]), (m < 0.07, HK[3])], HK[1])


def f_lantern():
    glow = (YS > 5) & (YS < 27) & (XS > 5) & (XS < 27)
    return pick([(glow, MG[5]), (glow & (nz(3, 3, 131) > 0.5), MG[6]), (glow & (XS % 8 == 0), EI[2])], EI[2])


def f_coif():
    return pick([(((XS + YS) % 4) < 2, EI[3]), (((XS - YS) % 4) == 0, EI[2]), (nz(3, 3, 141) > 0.8, EI[4])], EI[2])


def f_eye(warn):
    return pick([(YS >= 10, BR[6])] if warn else [], BR[5] if warn else EI[1]) if warn else \
        np.zeros((32, 32, 3)) + EI[1]


def make_atlas(state):
    a = np.zeros((128, 128, 3)) + EI[0]
    fns = {"flesh": f_flesh, "pale": f_pale, "shroud": f_shroud, "tabard": f_tabard, "robe": f_robe, "plate": f_plate,
           "dark": f_dark, "leather": f_leather, "wood": f_wood, "chain": f_chain, "brass": f_brass,
           "hollow": f_hollow, "hide": f_hide, "lantern": f_lantern, "coif": f_coif,
           "eye": lambda: f_eye(state == "ankuendigung")}
    for name, fn in fns.items():
        x0, y0 = FIELDS[name]
        a[y0:y0 + 32, x0:x0 + 32] = fn()
    return a


# --- Bausteine: alles gerundet, alles geschlossen -------------------------------------------------------------------

def align(direction):
    """Drehung, die die y-Achse auf direction legt."""
    return V(0, 1, 0).rotation_difference(V(*direction).normalized()).to_matrix()


def rot(ax, deg):
    return Matrix.Rotation(math.radians(deg), 3, ax)


def ellipsoid(p, c, r, rect, rotm=None, n=8, lats=(-62, -21, 21, 62)):
    """Ellipsoid (Halbachsen r in Spiel-Achsen, optional gedreht) als Loft mit Pol-Faechern."""
    if max(r) < 0.12 and n == 8:
        n, lats = 6, (-55, 0, 55)
    rings = []
    for deg in lats:
        t = math.radians(deg)
        ring = []
        for k in range(n):
            a = 2 * math.pi * k / n + math.pi / n
            v = V(r[0] * math.cos(t) * math.cos(a), r[1] * math.sin(t), r[2] * math.cos(t) * math.sin(a))
            ring.append(V(*c) + (rotm @ v if rotm is not None else v))
        rings.append(ring)
    p.loft(rings, rect)


def limb(p, pts, radii, rect, sides=8, flat=None):
    """Gerundete Roehre durch pts (mindestens 2 Punkte), radii je Punkt (Zahl oder (Breite, Tiefe))."""
    cs = [V(*q) for q in pts]
    shapes = []
    for r in radii:
        w, d = (r, r) if not isinstance(r, tuple) else r
        shapes.append((w, d, d))
    if max(max(r) if isinstance(r, tuple) else r for r in radii) < 0.06:
        sides = 6
    axis = cs[-1] - cs[0]
    ref = V(0, 0, 1) if abs(axis.normalized().y) > 0.7 else V(0, 1, 0)
    if flat is not None:
        ref = V(*flat)
    p.loft(tube(cs, shapes, sides, ref), rect)


def revolve(p, base, axis, prof, rect, n=8):
    """Drehkoerper um die Achse axis ab base. prof = [(Abstand, Radius)]; Radius > 0 (Spitzen 0.02)."""
    ax = V(*axis).normalized()
    u = ax.cross(V(0, 0, 1))
    if u.length < 0.2:
        u = ax.cross(V(1, 0, 0))
    u.normalize()
    w = ax.cross(u).normalized()
    rings = [[V(*base) + ax * t + (u * math.cos(2 * math.pi * k / n) + w * math.sin(2 * math.pi * k / n)) * r
              for k in range(n)] for t, r in prof]
    p.loft(rings, rect)


def ring_y(y, rx, rz, cx=0.0, cz=0.0, n=10, jag=None):
    return [V(cx + rx * math.cos(2 * math.pi * k / n), y + (jag[k % len(jag)] if jag else 0.0),
              cz + rz * math.sin(2 * math.pi * k / n)) for k in range(n)]


def skirt(p, specs, rect, n=10, jag=None):
    """Haengender Rock/Kutte: specs = [(y, rx, rz, cz)] von unten nach oben; unterster Ring ausgefranst (jag)."""
    rings = []
    for i, (y, rx, rz, cz) in enumerate(specs):
        rings.append(ring_y(y, rx, rz, 0.0, cz, n, jag if i == 0 else None))
    p.loft(rings, rect)


def slab(p, centre, pts, thick, rect, rotm=None):
    """Gewoelbte Platte (Sargdeckel, Schild): Umriss pts (x, y), drei Ringe abgeschraegt, geschlossen."""
    rings = []
    for z, s in ((-thick, 0.9), (0.0, 1.0), (thick, 0.9)):
        ring = []
        for (x, y) in pts:
            v = V(x * s, y * s, z)
            ring.append(V(*centre) + (rotm @ v if rotm is not None else v))
        rings.append(ring)
    p.loft(rings, rect)


def ik(sh, hand, bend, l1=0.5, l2=0.5):
    """Ellbogen eines Arms mit zwei Gliedern zwischen Schulter und Hand (bend = Richtung, in die er knickt)."""
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


def ez(c, r, x, y, off=0.02):
    """z auf der Vorderseite eines Ellipsoids an (x, y) - damit Gurte und Ketten auf dem Koerper aufliegen."""
    u = 1 - ((x - c[0]) / r[0]) ** 2 - ((y - c[1]) / r[1]) ** 2
    return c[2] + r[2] * math.sqrt(max(u, 0.0)) + off


def eyes_part(pivot, z, y, dx=0.055, w=0.04, h=0.03):
    e = Part("eyes", pivot, UV)
    for s in (-1, 1):
        e.box((s * dx - w / 2, y, z), (s * dx + w / 2, y + h, z + 0.01), "eye")
    return e


def arm(p, sh, hand, bend, rect_up, rect_low, r=(0.13, 0.115, 0.09), cuff="dark", cuff_r=(0.115, 0.1)):
    """Arm: Oberarm, Unterarm, Manschette (verdeckt den Ansatz des Griffs - keine Hand modelliert)."""
    el = ik(sh, hand, bend)
    hand = V(*hand)
    limb(p, [V(*sh), (V(*sh) + el) / 2, el], [r[0], r[0] * 0.95, r[1]], rect_up)
    limb(p, [el, (el + hand) / 2, hand - (hand - el).normalized() * 0.05], [r[1], r[1] * 0.92, r[2]], rect_low)
    ellipsoid(p, hand - (hand - el).normalized() * 0.02, (cuff_r[0], cuff_r[1], cuff_r[0]), cuff,
              rotm=align(hand - el))
    return el


def haft_part(p, hand, d, back, fwd, rad, rect="wood"):
    d = V(*d).normalized()
    a, b = V(*hand) - d * back, V(*hand) + d * fwd
    limb(p, [a, (a + b) / 2, b], [rad, rad, rad], rect, sides=6)
    return a, b


# --- Fassung D: Hammerträger (aufrecht, breit, Rueststuecke ueber totem Fleisch) -----------------------------------

def build_D(state):
    ank = state == "ankuendigung"
    P = lambda name, pivot: Part(name, pivot, UV)
    parts = []
    for s in (-1, 1):
        leg = P("leg_l" if s < 0 else "leg_r", (s * 0.22, 1.0, 0.0))
        limb(leg, [(s * .22, 1.1, 0), (s * .235, .82, .04), (s * .25, .57, .06)], [.19, .18, .145], "dark" if s > 0 else "shroud")
        limb(leg, [(s * .25, .57, .06), (s * .25, .33, .03), (s * .26, .11, 0)], [.135, .125, .105], "flesh" if s < 0 else "plate")
        ellipsoid(leg, (s * .25, .57, .14), (.14, .12, .1), "plate" if s > 0 else "dark")                      # Knie
        ellipsoid(leg, (s * .26, .09, .1), (.14, .085, .27), "dark", n=8, lats=(-60, -20, 20, 60))              # Stiefel
        if s > 0:
            ellipsoid(leg, (.26, .33, .1), (.115, .23, .08), "plate")                                           # Beinschiene
        parts.append(leg)

    t = P("torso", (0, 1.5, 0))
    ellipsoid(t, (0, 1.05, 0), (.36, .2, .27), "leather")                                                       # Becken
    ellipsoid(t, (0, 1.3, .05), (.38, .3, .31), "flesh")                                                        # Wanst
    ellipsoid(t, (0, 1.64, 0), (.47, .34, .32), "plate")                                                        # Brustpanzer
    ellipsoid(t, (0, 1.18, .02), (.42, .06, .34), "leather")                                                    # Gurt
    ellipsoid(t, (.12, 1.17, .35), (.07, .06, .03), "brass")                                                    # Schnalle (aus der Mitte)
    ellipsoid(t, (0, 1.9, .02), (.25, .09, .22), "dark")                                                        # Halsberge
    ellipsoid(t, (.6, 1.86, 0), (.31, .2, .29), "plate", rot("Z", -14))                                         # grosse Schulter rechts
    ellipsoid(t, (.64, 1.72, 0), (.29, .1, .27), "dark", rot("Z", -14))
    ellipsoid(t, (-.55, 1.82, 0), (.2, .15, .2), "flesh")                                                     # kleine Kappe links
    limb(t, [(0, 1.18, .4), (0, .86, .42), (0, .56, .38)], [(.17, .02), (.2, .02), (.22, .02)], "tabard", sides=6)  # Wappenrock
    for k, (x, ln) in enumerate(((-.3, .34), (.26, .42))):
        limb(t, [(x, 1.1, .3), (x, 1.1 - ln / 2, .32), (x * 1.05, 1.1 - ln, .3)], [(.06, .018), (.065, .018), (.05, .016)], "tabard", sides=6)
    limb(t, [(0, 1.85, -.3), (0, 1.3, -.37), (0, .66, -.34)], [(.3, .02), (.34, .02), (.36, .02)], "robe", sides=6)  # Umhang hinten
    parts.append(t)

    h = P("head", (0, 1.95, 0.02))
    ellipsoid(h, (0, 2.12, .02), (.17, .2, .19), "plate")                                                       # Helm
    ellipsoid(h, (0, 1.99, .06), (.12, .07, .12), "pale")                                                       # Kinn
    ellipsoid(h, (0, 2.1, .2), (.1, .035, .025), "hollow")                                                      # Sehschlitz
    ellipsoid(h, (.02, 2.31, -.02), (.04, .07, .15), "dark")                                                    # Kamm, aus der Mitte
    limb(h, [(0, 2.25, -.14), (0, 2.0, -.25), (0, 1.8, -.26)], [(.12, .02), (.14, .02), (.15, .02)], "shroud", sides=6)  # Kapuzentuch
    parts.append(h)
    parts.append(eyes_part((0, 1.95, .02), .222, 2.085))

    # Waffe: Grabkreuz-Hammer; Ruhe ueber der Schulter, Ankuendigung hoch zum Ausholen
    sh_r, sh_l = (.6, 1.8, 0), (-.55, 1.8, 0)
    if ank:
        hand_r, d, back, bend = (.42, 2.18, .05), (0, .55, -.83), .55, (1, .2, .6)
        hand_l, bend_l = (-.9, 2.0, .15), (-1, 0, -.4)
    else:
        hand_r, d, back, bend = (.62, 1.28, .5), (.33, .76, -.57), .65, (1, -.3, -.2)
        hand_l, bend_l = (-.72, 1.05, .25), (-.4, 0, -1)
    ar = P("arm_r", sh_r)
    arm(ar, sh_r, hand_r, bend, "plate", "dark", cuff="plate", cuff_r=(.13, .11))
    parts.append(ar)
    al = P("arm_l", sh_l)
    arm(al, sh_l, hand_l, bend_l, "flesh", "shroud", cuff="dark", cuff_r=(.12, .1))
    parts.append(al)
    hm = P("hammer", hand_r)
    a, b = haft_part(hm, hand_r, d, back, 1.0, .042)
    dn = V(*d).normalized()
    cen = b + dn * .12
    ellipsoid(hm, b, (.07, .07, .07), "dark")
    limb(hm, [cen - dn * .3, cen, cen + dn * .48], [.085, .1, .08], "plate")                                  # Kreuzbalken
    side = dn.cross(V(1, 0, 0)).normalized()
    cb = cen + dn * .22
    limb(hm, [cb - side * .34, cb, cb + side * .34], [.075, .09, .075], "plate")                              # Querbalken
    ellipsoid(hm, cb + side * .36, (.09, .09, .09), "dark")
    ellipsoid(hm, cb - side * .36, (.09, .09, .09), "dark")
    ellipsoid(hm, cen + dn * .52, (.09, .09, .09), "dark")
    ellipsoid(hm, a, (.07, .07, .07), "dark")
    ellipsoid(hm, V(*hand_r) - dn * .32, (.055, .1, .055), "leather", align(dn))                              # Griffwulst
    parts.append(hm)
    # Kette an der linken Manschette
    ch = P("chain", hand_l)
    hl = V(*hand_l)
    limb(ch, [hl, hl + V(.0, -.35, .12), hl + V(.1, -.7, .3)], [.028, .028, .028], "chain", sides=6)
    ellipsoid(ch, hl + V(.12, -.74, .32), (.07, .06, .07), "dark")
    parts.append(ch)
    return parts


# --- Fassung E: Sargdeckel-Kutte (hohe, birnenfoermige Kutte, Schild und Glockenflegel) ----------------------------

def build_E(state):
    ank = state == "ankuendigung"
    P = lambda name, pivot: Part(name, pivot, UV)
    parts = []
    body = P("body", (0, 1.0, 0))
    jag = [0.0, .12, .03, .17, .06, .1, 0.0, .15, .04, .13]
    skirt(body, [(.15, .66, .54, 0), (.2, .66, .54, 0), (.62, .56, .46, 0), (1.12, .46, .36, .02), (1.5, .5, .36, .02),
                 (1.86, .46, .3, 0), (2.0, .22, .19, 0)], "robe", n=10, jag=jag)
    for s in (-1, 1):
        ellipsoid(body, (s * .24, .09, .5), (.14, .085, .27), "dark", n=8, lats=(-60, -20, 20, 60))               # Stiefelspitzen
    ellipsoid(body, (0, 1.22, .03), (.47, .05, .37), "leather")                                                 # Strick
    ellipsoid(body, (-.47, 1.83, 0), (.22, .17, .22), "robe")                                                   # Schultern, breit
    ellipsoid(body, (.47, 1.83, 0), (.24, .17, .23), "dark", rot("Z", -12))                                     # rechts: Eisenkappe
    parts.append(body)
    # Gurt ueber die Brust, Schluessel am Strick
    gur = P("straps", (0, 1.5, 0))
    p0, p1 = (-.38, 1.88), (.34, 1.25)
    pts = []
    for k in range(5):
        x = p0[0] + (p1[0] - p0[0]) * k / 4
        y = p0[1] + (p1[1] - p0[1]) * k / 4
        rx, rz = (.46, .3) if y > 1.5 else (.46 + (1.5 - y) * .1, .3 + (1.5 - y) * .15)
        z = ez((0, y, 0.0), (rx, 1.0, rz), x, y, 0.0)
        pts.append((x, y, rz * math.sqrt(max(1 - (x / rx) ** 2, 0.05)) + .03))
    limb(gur, pts, [(.065, .02)] * 5, "leather", sides=6, flat=(0, 0, 1))
    for k, (x, ln, a) in enumerate(((.38, .13, 10), (.33, .1, -15), (.43, .08, 25))):
        ellipsoid(gur, (x, 1.1 - ln * .4, .26 + k * .03), (.03, ln, .016), "brass", rot("Z", a))
    ellipsoid(gur, (.36, 1.22, .3), (.07, .07, .03), "brass")
    parts.append(gur)

    hd = P("head", (0, 2.0, 0))
    revolve(hd, (0, 1.9, -.02), (0, 1, -.36), [(0, .2), (.12, .24), (.3, .2), (.5, .12), (.62, .02)], "robe", n=10)
    ellipsoid(hd, (0, 2.1, .13), (.12, .15, .1), "pale")                                                        # Gesicht im Schatten
    ellipsoid(hd, (0, 2.1, .2), (.095, .12, .04), "hollow")
    limb(hd, [(0, 2.06, .02), (0, 2.18, .0), (0, 2.34, -.1)], [(.19, .02), (.2, .02), (.14, .02)], "robe", sides=6)   # Kapuzenrand
    parts.append(hd)
    parts.append(eyes_part((0, 2.0, 0), .232, 2.09, dx=.05, w=.038, h=.03))

    # Schild links: Sargdeckel
    sh_l = (-.5, 1.82, 0)
    if ank:
        hand_l = (-.72, 1.55, .5)
        ctr, ang = (-.78, 1.2, .72), 10
    else:
        hand_l = (-.62, 1.3, .42)
        ctr, ang = (-.7, 1.0, .64), 6
    al = P("arm_l", sh_l)
    arm(al, sh_l, hand_l, (-1, -.2, .2), "robe", "robe", r=(.15, .14, .12), cuff="robe", cuff_r=(.15, .13))
    parts.append(al)
    sd = P("shield", ctr)
    hexa = [(-.17, .78), (.17, .78), (.34, .3), (.17, -.78), (-.17, -.78), (-.34, .3)]
    rm = rot("Z", ang) @ rot("X", -8)
    slab(sd, ctr, hexa, .06, "wood", rm)
    for (c, r_) in (((0, .1, .075), (.035, .42, .02)), ((0, .3, .075), (.2, .035, .02))):
        ellipsoid(sd, V(*ctr) + rm @ V(*c), r_, "brass", rm)
    for k in range(3):
        ellipsoid(sd, V(*ctr) + rm @ V(-.2 + k * .2, -.6, .07), (.035, .035, .03), "brass")
    parts.append(sd)

    # Flegel rechts mit Totenglocke
    sh_r = (.5, 1.82, 0)
    if ank:
        hand_r, bend = (.62, 2.12, .12), (1, .1, .5)
        cpts = [(.62, 2.12, .12), (.7, 2.5, -.15), (.6, 2.7, -.6)]
        axis = (-.1, 0, -1)
    else:
        hand_r, bend = (.66, 1.3, .44), (1, -.2, -.2)
        cpts = [(.66, 1.3, .44), (.74, .8, .6), (.74, .55, .66)]
        axis = (0, -1, .1)
    ar = P("arm_r", sh_r)
    arm(ar, sh_r, hand_r, bend, "dark", "robe", r=(.15, .14, .12), cuff="dark", cuff_r=(.14, .12))
    parts.append(ar)
    fl = P("flail", hand_r)
    limb(fl, cpts, [.03, .03, .03], "chain", sides=6)
    e = V(*cpts[-1])
    ax = V(*axis).normalized()
    ellipsoid(fl, e, (.06, .06, .06), "dark")
    revolve(fl, e, ax, [(0, .05), (.06, .12), (.2, .2), (.34, .25), (.38, .24)], "brass", n=10)               # Glocke
    ellipsoid(fl, e + ax * .38, (.07, .07, .07), "dark")                                                       # Klöppel am Rand
    parts.append(fl)
    return parts


# --- Fassung F: Laternentraeger (gebueckt, bauchig, Kettenhaube, Stange mit Laterne) ------------------------------

def build_F(state):
    ank = state == "ankuendigung"
    P = lambda name, pivot: Part(name, pivot, UV)
    parts = []
    for s in (-1, 1):
        leg = P("leg_l" if s < 0 else "leg_r", (s * .27, .95, 0.0))
        limb(leg, [(s * .27, 1.0, -.02), (s * .33, .75, .06), (s * .36, .52, .14)], [.21, .19, .15], "shroud" if s < 0 else "hide")
        limb(leg, [(s * .36, .52, .14), (s * .34, .3, .1), (s * .32, .11, .06)], [.145, .13, .11], "shroud" if s < 0 else "leather")
        ellipsoid(leg, (s * .32, .09, .15), (.15, .085, .26), "leather", n=8, lats=(-60, -20, 20, 60))
        if s > 0:
            ellipsoid(leg, (s * .34, .42, .13), (.17, .035, .15), "chain")                                       # Kette ums Schienbein
        parts.append(leg)

    t = P("torso", (0, 1.4, .1))
    ellipsoid(t, (0, 1.0, 0), (.4, .22, .3), "flesh")
    bc, br_ = (0, 1.22, .2), (.52, .42, .46)
    ellipsoid(t, bc, br_, "shroud", n=10)                                                                         # Wanst
    ellipsoid(t, (0, 1.68, .06), (.5, .36, .38), "pale", n=10)                                                   # Buckel / Brust
    ellipsoid(t, (0, 1.78, -.2), (.4, .3, .25), "hide")                                                         # Rueckenbuckel
    for k, (a0, a1) in enumerate((((-.44, 1.78), (.38, 1.2)), ((.46, 1.74), (-.34, 1.2)))):                      # Ketten quer ueber Brust und Bauch
        pts = []
        for j in range(5):
            x = a0[0] + (a1[0] - a0[0]) * j / 4
            y = a0[1] + (a1[1] - a0[1]) * j / 4
            c_, r2 = (bc, br_) if y < 1.45 else ((0, 1.68, .06), (.5, .36, .38))
            pts.append((x, y, ez(c_, r2, x, y, .03 + .02 * k)))
        limb(t, pts, [.034] * 5, "chain", sides=6)
    ellipsoid(t, (.0, 1.4, ez(bc, br_, 0, 1.4, .03)), (.09, .09, .03), "brass")                                # Schloss, wo sie sich kreuzen
    ellipsoid(t, (-.62, 1.78, .2), (.22, .17, .22), "dark", rot("Z", 15))                                       # nur links ein Schulterstueck
    limb(t, [(.12, 1.02, .38), (.3, .9, .46), (.4, .64, .46)], [(.05, .02), (.06, .02), (.05, .02)], "tabard", sides=6)
    limb(t, [(-.1, 1.0, .38), (-.18, .72, .5), (-.2, .46, .44)], [(.07, .02), (.08, .02), (.06, .02)], "tabard", sides=6)
    parts.append(t)

    h = P("head", (0, 1.85, .45))
    ellipsoid(h, (0, 1.98, .52), (.165, .19, .18), "pale")
    ellipsoid(h, (0, 2.02, .54), (.185, .185, .185), "coif", n=10)                                               # Kettenhaube
    ellipsoid(h, (0, 1.97, .68), (.1, .06, .03), "pale")                                                          # Gesicht, offen
    ellipsoid(h, (0, 1.99, .7), (.09, .03, .02), "hollow")
    ellipsoid(h, (0, 1.78, .46), (.27, .06, .26), "dark")                                                         # Eisenhalsring
    ellipsoid(h, (-.14, 2.14, .5), (.05, .05, .06), "dark")                                                       # Nagelkopf, schief
    parts.append(h)
    parts.append(eyes_part((0, 1.85, .45), .694, 2.0, dx=.05, w=.036, h=.028))

    # Stange mit Laterne
    sh_r, sh_l = (.6, 1.72, .22), (-.6, 1.72, .22)
    if ank:
        hand_r, d, hand_l, bend_r, bend_l = (.42, 2.16, .6), (.05, .45, .85), (.02, 1.86, .4), (1, .3, 0), (-1, .4, -.3)
    else:
        hand_r, d, hand_l, bend_r, bend_l = (.62, 1.22, .72), (.04, 1.0, -.05), (-.7, .95, .44), (1, -.3, -.2), (-.3, 0, -1)
    ar = P("arm_r", sh_r)
    arm(ar, sh_r, hand_r, bend_r, "shroud", "shroud", r=(.15, .13, .1), cuff="leather", cuff_r=(.13, .11))
    parts.append(ar)
    al = P("arm_l", sh_l)
    arm(al, sh_l, hand_l, bend_l, "hide", "shroud", r=(.15, .13, .1), cuff="dark", cuff_r=(.13, .11))
    parts.append(al)
    pl = P("pole", hand_r)
    dn = V(*d).normalized()
    a, b = haft_part(pl, hand_r, d, 1.25 if not ank else .9, 1.35 if not ank else 1.2, .04)
    hook = [b, b + dn * .08 + V(0, 0, .12), b + dn * .02 + V(0, -.04, .3)]
    limb(pl, hook, [.035, .035, .03], "dark", sides=6)
    ellipsoid(pl, a, (.07, .07, .07), "dark")
    hang = V(*hook[-1]) + V(0, -.1, .0)
    limb(pl, [hook[-1], hang], [.02, .02], "chain", sides=6)
    ellipsoid(pl, hang + V(0, -.2, 0), (.15, .2, .15), "lantern", n=8)
    revolve(pl, hang + V(0, -.02, 0), (0, 1, 0), [(0, .1), (.06, .16), (.12, .03)], "brass", n=8)
    ellipsoid(pl, hang + V(0, -.43, 0), (.12, .06, .12), "dark")
    parts.append(pl)
    return parts


BUILD = {"D": build_D, "E": build_E, "F": build_F}
FOCUS = {"D": (0, 1.9, 0.1), "E": (0, 2.0, 0.1), "F": (0, 1.8, 0.5)}


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
            g = to_game(o.matrix_world @ V(*c))
            lo, hi = np.minimum(lo, (g.x, g.y, g.z)), np.maximum(hi, (g.x, g.y, g.z))
    return lo, hi


def closed_report(objs):
    """Je Teil: Dreiecke, offene Kanten (genau eine Flaeche), Kanten mit mehr als zwei Flaechen, Rauminhalt."""
    out = {}
    for o in objs:
        bm = bmesh.new()
        bm.from_mesh(o.data)
        open_e = sum(1 for e in bm.edges if len(e.link_faces) == 1)
        multi = sum(1 for e in bm.edges if len(e.link_faces) > 2)
        vol = 0.0
        for f in bm.faces:
            a, b, c = (v.co for v in f.verts)
            vol += a.dot(b.cross(c)) / 6.0
        out[o.name] = (len(o.data.polygons), open_e, multi, vol)
        bm.free()
    return out


def import_model(name):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(MODELS, name))
    objs = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    cache = {}
    for o in objs:
        image = next(n.image for n in o.active_material.node_tree.nodes if n.type == "TEX_IMAGE")
        if image.name not in cache:
            cache[image.name] = pass_material("cmp_" + name + "_" + image.name, image)
        o.data.materials.clear()
        o.data.materials.append(cache[image.name])
        o["smooth"] = True
        HOME[o.name] = o.matrix_world.copy()
    return objs


# --- Blatt-Werkzeuge ------------------------------------------------------------------------------------------------

def text(img, s, x, y, size=30, color=(235, 235, 235), bold=False):
    """Schrift ins Feld img (Zeile 0 oben): text(img, s, x, y, size, color, bold) - Reihenfolge nicht vertauschen."""
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
    tmp = os.path.join(tempfile.gettempdir(), "gruftwaechter_r2_txt.png")
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


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    os.makedirs(os.path.join(OUT, "modelle"), exist_ok=True)
    os.makedirs(os.path.join(OUT, "texturen"), exist_ok=True)
    atlas = {s: make_atlas(s) for s in ("ruhend", "ankuendigung")}
    pal_all = {tuple(c) for k in PAL for c in PAL[k]}
    for s, a in atlas.items():
        cols = {tuple(c) for c in a.reshape(-1, 3)}
        assert cols <= pal_all, "Farbe ausserhalb der Palette"
        assert len(cols) <= 32, f"{s}: {len(cols)} Farben"
    images = {s: save_image("atlas_" + s, atlas[s], os.path.join(OUT, "texturen", f"atlas_{s}.png")) for s in atlas}

    # 1. Modelle (glb), Zahlen, Pruefung "geschlossen"
    stats, lines = {}, []
    for v in VARIANTS:
        mat = export_material(f"gw_{v}", images["ruhend"])
        objs = put_in_scene(BUILD[v]("ruhend"), mat, f"exp_{v}")
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
    with open(os.path.join(OUT, "pruefung.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write("Pruefung je Teil im Skript (Kante mit genau einer Flaeche = offen):\n" + "\n".join(lines) + "\n")
    with open(os.path.join(OUT, "masse.txt"), "w", encoding="utf-8", newline="\n") as f:
        for v in VARIANTS:
            t, lo, hi = stats[v]
            f.write(f"{v} {NAMES[v]}: Dreiecke {sum(t.values())} {dict(t)}, x {lo[0]:.2f}..{hi[0]:.2f}, "
                    f"y {lo[1]:.2f}..{hi[1]:.2f}, z {lo[2]:.2f}..{hi[2]:.2f}\n")
    print("\n".join(l for l in lines if not l.startswith("    ")))

    # 2. Szenen zum Rendern
    pm = {s: pass_material("pm_" + s, images[s]) for s in atlas}
    sets = {(v, s): put_in_scene(BUILD[v](s), pm[s], f"r_{v}_{s}") for v in VARIANTS for s in atlas}
    for objs in sets.values():
        place(objs)
    ghoul, gorm = import_model("ghoul.glb"), import_model("gorm.glb")
    place(ghoul)
    place(gorm)
    cam = camera()
    stone = stone_textures(tempfile.mkdtemp(prefix="gw_stein_"))
    room = build_room(stone)
    hall = build_room(stone, half=14.0, height=3.5, back=-14.0, front=24.0)

    # 2a. Ansichten je Fassung (ohne Nebel): vorn, Seite, hinten, schraeg oben, Nahaufnahme Kopf und Brust
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
        tiles[(v, "Kopf und Brust")] = render(sets[(v, "ruhend")], cam, G(fc + V(0.35, 0.1, 1.6)), G(fc - V(0, 0.2, 0)),
                                              32, (T, T), "ansicht")
        tiles[(v, "Ankündigung")] = render(sets[(v, "ankuendigung")], cam, G(V(3.6, 1.6, 5.4)), G(V(0, 1.5, 0)), 32, (T, T),
                                           "ansicht")
    if QUICK:
        sh = np.zeros((2 * T, 3 * T, 3)) + FOG_COLOR
        names = ("vorn", "Seite", "hinten", "schräg oben", "Kopf und Brust", "Ankündigung")
        for k, nm in enumerate(names):
            blit(sh, tiles[(QUICK, nm)], (k % 3) * T, (k // 3) * T)
        os.makedirs(os.path.join(OUT, "_probe"), exist_ok=True)
        save_png(sh[::-1], os.path.join(OUT, "_probe", f"probe-{QUICK}.png"))
        img = render(list(sets[(QUICK, "ankuendigung")]) + room, cam, G(V(0, 1.6, 4)), G(V(0, 1.6, 0)), 70, (854, 480), "spiel")
        save_png(img[::-1], os.path.join(OUT, "_probe", f"spiel-{QUICK}-ank.png"))
        return

    # 2b. Spielbilder 854 x 480, 70 Grad, Augen 1,6 m: Ankuendigung aus 4 m
    game = {}
    for v in VARIANTS:
        for s, tag in (("ruhend", ""), ("ankuendigung", "-ankuendigung")):
            img = render(list(sets[(v, s)]) + room, cam, G(V(0, 1.6, 4)), G(V(0, 1.6, 0)), 70, (854, 480), "spiel")
            game[(v, s)] = img
            save_png(img[::-1], os.path.join(OUT, f"spiel-{v}{tag}-4m.png"))

    # 2c. Groessenvergleich: Ghul, D, E, F, Gorm in einer Reihe
    order = (("Ghul", ghoul, -6.6), ("D", sets[("D", "ruhend")], -3.3), ("E", sets[("E", "ruhend")], 0.0),
             ("F", sets[("F", "ruhend")], 3.3), ("Gorm", gorm, 6.6))
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
    GOLD = MG[5]
    M, GAP = 12, 24
    W = 2 * M + 5 * T + 4 * GAP
    sheet = np.zeros((3800, W, 3)) + FOG_COLOR
    text(sheet, "Gruftwächter, Runde 2 (Issue 335): Fassungen D, E, F", M, 14, 46, (235, 235, 235), True)
    text(sheet, "Richtung: weniger Klötzchen, mehr Figur. Gerundete Körper, Gouraud-Schattierung, Textur nur aus der "
         "Hauptpalette (höchstens 32 Farben), Rot nur in den Augen der Ankündigung.", M, 74, 22, (190, 180, 200))
    y = 118
    cols = ("vorn", "Seite", "hinten", "schräg oben", "Kopf und Brust")
    for v in VARIANTS:
        t, lo, hi = stats[v]
        text(sheet, v, M, y - 6, 64, GOLD, True)
        text(sheet, NAMES[v], M + 60, y + 6, 40, (235, 235, 235), True)
        text(sheet, f"{sum(t.values())} Dreiecke (Ghul 1024, Gorm 2268);  {hi[0] - lo[0]:.2f} m breit, "
             f"{hi[2] - lo[2]:.2f} m tief, {hi[1]:.2f} m hoch (Ruhe, mit Waffe)", M + 430, y + 14, 24, (190, 180, 200))
        y += 66
        for k, nm in enumerate(cols):
            blit(sheet, tiles[(v, nm)], M + k * (T + GAP), y)
            text(sheet, nm, M + k * (T + GAP) + 6, y + 2, 22, (235, 235, 235))
        y += T + 26
    text(sheet, "ANKÜNDIGUNG aus 4 m im Spiel (Ausholen; die Augen leuchten rot, sonst nichts)", M, y, 32, (235, 235, 235), True)
    y += 50
    cw = 600
    for k, v in enumerate(VARIANTS):
        blit(sheet, game[(v, "ankuendigung")][30:450, 127:727], M + k * (cw + 25), y)
        text(sheet, v, M + k * (cw + 25) + 6, y + 2, 34, GOLD, True)
    y += 420 + 24
    text(sheet, "ANKÜNDIGUNG schräg von der Seite (ohne Nebel): dieselbe Figur, Pose zum Ausholen",
         M, y, 22, (190, 180, 200))
    y += 34
    for k, v in enumerate(VARIANTS):
        blit(sheet, tiles[(v, "Ankündigung")], M + k * (T + GAP), y)
        text(sheet, f"{v} Ankündigung (Ausholen)", M + k * (T + GAP) + 6, y + 2, 22, (235, 235, 235))
    y += T + 24
    text(sheet, "GRÖSSENVERGLEICH von vorn (ohne Nebel), Linien bei 1, 2, 3 m: Ghul, D, E, F, Gorm", M, y, 32,
         (235, 235, 235), True)
    y += 50
    blit(sheet, strip, (W - SWd) // 2, y)
    y += SHd + 24
    for tag, title in (("", "AUS 10 m im Spiel (Nebel, Körnung, 854 x 480, doppelt vergrößert): Ghul, D, E, F, Gorm"),
                       ("-dunkel", "dieselbe Reihe DUNKEL (Licht halbiert)")):
        text(sheet, title, M, y, 28, (235, 235, 235), True)
        y += 44
        big = up(strips[tag], 2)
        blit(sheet, big, M, y)
        for name, _, x in order:
            text(sheet, name, M + 2 * (int(427 + 34.276 * x) - 160) - 14 * len(name), y + big.shape[0] + 2, 24,
                 (235, 235, 235))
        y += big.shape[0] + 44
    ty = y - 2 * (big.shape[0] + 44 + 44) + 50
    text(sheet, "Dreiecke und Maße (Ruhe-Pose, mit Waffe)", 1200, ty, 28, GOLD, True)
    for k, v in enumerate(VARIANTS):
        t, lo, hi = stats[v]
        text(sheet, f"{v} {NAMES[v]}: {sum(t.values())} Dreiecke", 1200, ty + 50 + k * 80, 26, (235, 235, 235), True)
        text(sheet, f"{hi[0] - lo[0]:.2f} breit, {hi[2] - lo[2]:.2f} tief, {hi[1]:.2f} hoch", 1200, ty + 80 + k * 80, 24, (190, 180, 200))
    text(sheet, "Ghul 1024 Dreiecke, Gorm 2268 Dreiecke", 1200, ty + 300, 24, (190, 180, 200))
    save_png(sheet[:y + 10][::-1], os.path.join(OUT, "blatt.png"))


main()

