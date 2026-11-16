"""Ghul (ausgemergelter, kriechender Mensch)

Erzeuger fuer das Spiel (Uebergabe aus dem Probestueck ghul-2 (Fassung 2), Branch claude/design-probe-ghul-45a8c4).
Erzeugt nur Modell und Textur nach assets/models/, keine Probebilder; die Bilder entstehen im Probestueck-Skript.

Aufruf (aus dem Repository-Wurzelverzeichnis):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/models/ghoul.py

Herkunft: alles per Skript, nichts heruntergeladen. Teile, Drehpunkte und Haltungen: design/uebergabe/README.md.
"""


import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from mathutils import Matrix  # noqa: E402
from psx import export_glb, report_parts  # noqa: E402
from psx import (V, G, Part, tube, rects_from_pixels, vnoise, blob, mix, paint_atlas, save_image,  # noqa: E402
                 export_material, stone_textures, build_room, pass_material, write_light, pose, setup_render,
                 camera, aim, render_passes, compose, save_png, load_png, zoom, side_by_side, coverage, runs)

# Modelle und Texturen landen in assets/models (vom Skript in tools/models aus zwei Ebenen hoch).
OUT_DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
                       "assets", "models")
TEX = 128

SKIN = np.array((126, 134, 112), float)       # fahle, gruenliche Haut (Richtung des Platzhalters)
SKIN_PALE = np.array((164, 164, 136), float)   # ueber Knochen gespannte Haut: Schaedel, Gelenke
ROT = np.array((92, 96, 66), float)            # Flecken, Verwesung (oliv, kein Rot)
SHADOW = np.array((40, 34, 38), float)         # Falten, Rippenzwischenraeume
HOLLOW = np.array((22, 16, 20), float)         # Augenhoehlen, Schlund
TEETH = np.array((172, 160, 118), float)
GUM = np.array((74, 56, 54), float)            # Zahnfleisch, dunkel und braun (Rot ist reserviert)
EYE = np.array((176, 172, 136), float)         # trueber Augapfel, winzig
HAIR = np.array((38, 32, 30), float)
NAIL = np.array((120, 108, 76), float)
DIRT = np.array((70, 60, 50), float)
RAGS = np.array((84, 100, 118), float)         # Lumpen Blaugrau (angenommen beim ersten Ghul)

RECTS = {
    "torso": (0, 64, 64, 128),
    "head": (64, 80, 128, 128),
    "jaw": (64, 64, 128, 80),
    "upper_arm": (0, 16, 32, 64),
    "forearm": (32, 16, 64, 64),
    "thigh": (64, 16, 96, 64),
    "shin": (96, 16, 128, 64),
    "hand": (0, 0, 32, 16),
    "finger": (32, 0, 48, 16),
    "foot": (48, 0, 80, 16),
    "rags": (80, 0, 112, 16),
    "neck": (112, 0, 128, 16),
}

UV = rects_from_pixels(RECTS, TEX)

# Masse (Spiel-Achsen). Koerper 1 m hoch, Radius 0,4, Kopfzone ab 0,68 m Hoehe, ab 0,19 m vor der Mitte.
HIP = V(0, 0.46, -0.23)
NECK = V(0, 0.68, 0.24)
HEAD_C = V(0, 0.81, 0.34)
JAW_HINGE = V(0, 0.76, 0.33)
SHOULDER = V(0.19, 0.67, 0.16)
HIP_JOINT = V(0.10, 0.44, -0.22)


def build_torso():
    part = Part("torso", HIP, UV)
    # Becken tief, Ruecken steigt zu den Schultern an; der Brustkorb haengt nach unten (Tiefe unten gross).
    centres = [V(0, 0.44, -0.32), V(0, 0.46, -0.23), V(0, 0.52, -0.12), V(0, 0.60, -0.02),
               V(0, 0.65, 0.07), V(0, 0.67, 0.15), V(0, 0.68, 0.22)]
    shapes = [(0.11, 0.07, 0.06), (0.14, 0.09, 0.08), (0.10, 0.07, 0.08), (0.16, 0.13, 0.10),
              (0.18, 0.14, 0.10), (0.20, 0.09, 0.08), (0.08, 0.05, 0.055)]
    n = 10

    def mods(i, k):
        if k == n // 2:                       # Wirbelsaeule als Grat
            return 1.14
        if i == 5 and k in (4, 6):            # Schulterblaetter stehen vor
            return 1.12
        if i in (3, 4) and k in (2, 8):       # Rippenbogen an den Flanken
            return 1.06
        return 1.0

    part.loft(tube(centres, shapes, n, mods=mods), "torso")
    neck = [V(0, 0.665, 0.17), V(0, 0.70, 0.25), V(0, 0.735, 0.30)]
    part.loft(tube(neck, [(0.04, 0.04, 0.035)] * 3, 6), "neck")
    # Lumpen um die Hueften: geschlossene Huelle (aussen, ausgefranster Saum, innen), schief haengend.
    centre = V(0, 0.465, -0.24)
    top = tube([centre, V(0, 0.455, -0.18)], [(0.155, 0.10, 0.09)] * 2, 10)[0]
    rng = np.random.default_rng(3)
    hem = []
    for k, p in enumerate(top):
        drop = 0.12 + 0.06 * rng.random() + (0.05 if k in (2, 3) else 0.0)
        hem.append(p + V(0, -drop, -0.03) + (p - centre) * 0.15)
    part.loft([top, hem, [centre + (h - centre) * 0.93 for h in hem], [centre + (q - centre) * 0.93 for q in top]],
              "rags", closed=True)
    return part


def build_arm(s, reach):
    side = "px" if s > 0 else "nx"
    sh = V(s * SHOULDER.x, SHOULDER.y, SHOULDER.z)
    elbow = V(s * 0.31, 0.38, 0.22)
    wrist = V(s * 0.25, 0.05, 0.40 + reach)
    upper = Part(f"upper_arm_{side}", sh, UV, parent="torso")
    upper.loft(tube([sh + V(0, 0.015, 0), sh + (elbow - sh) * 0.45, elbow],
                    [(0.058, 0.058, 0.058), (0.044, 0.044, 0.044), (0.05, 0.05, 0.05)], 6, ref_up=V(0, 0, 1)),
               "upper_arm")
    lower = Part(f"forearm_{side}", elbow, UV, parent=upper.name)
    lower.loft(tube([elbow + (elbow - sh).normalized() * 0.02, elbow + (wrist - elbow) * 0.4, wrist],
                    [(0.045, 0.045, 0.045), (0.036, 0.032, 0.032), (0.028, 0.024, 0.024)], 6, ref_up=V(0, 0, 1)),
               "forearm")
    knuckle = wrist + V(s * 0.005, -0.03, 0.085)
    lower.loft(tube([wrist, wrist + (knuckle - wrist) * 0.5, knuckle],
                    [(0.032, 0.013, 0.017), (0.045, 0.013, 0.017), (0.05, 0.011, 0.014)], 6, ref_up=V(0, 1, 0)), "hand")
    for f, spread in enumerate((-0.039, -0.013, 0.013, 0.039)):
        base = knuckle + V(spread, 0.002, 0.0)
        length = 0.09 + (0.015 if f in (1, 2) else 0.0)
        out = V(spread * 0.6, 0, 0)
        mid = base + V(0, 0.028, length * 0.55) + out
        tip = base + V(0, -0.028, length) + out * 1.6
        lower.loft(tube([base, mid, tip], [(0.01, 0.01, 0.01), (0.009, 0.009, 0.009), (0.004, 0.004, 0.004)], 3,
                        ref_up=V(0, 1, 0)), "finger")
    thumb = wrist + V(-s * 0.03, -0.005, 0.03)
    lower.loft(tube([thumb, thumb + V(-s * 0.03, 0.0, 0.035), thumb + V(-s * 0.035, -0.035, 0.06)],
                    [(0.011, 0.011, 0.011), (0.009, 0.009, 0.009), (0.004, 0.004, 0.004)], 3, ref_up=V(0, 1, 0)),
               "finger")
    return [upper, lower]


def build_leg(s):
    side = "px" if s > 0 else "nx"
    hip = V(s * HIP_JOINT.x, HIP_JOINT.y, HIP_JOINT.z)
    knee = V(s * 0.22, 0.07, -0.04)        # Knie weit auseinander und unter dem Bauch
    ankle = V(s * 0.20, 0.13, -0.36)
    thigh = Part(f"thigh_{side}", hip, UV)
    thigh.loft(tube([hip + V(0, 0.02, 0), hip + (knee - hip) * 0.5, knee],
                    [(0.065, 0.065, 0.065), (0.048, 0.048, 0.048), (0.05, 0.05, 0.05)], 6, ref_up=V(0, 0, 1)), "thigh")
    shin = Part(f"shin_{side}", knee, UV, parent=thigh.name)
    shin.loft(tube([knee + V(0, 0.0, -0.01), knee + (ankle - knee) * 0.5, ankle],
                   [(0.042, 0.042, 0.042), (0.032, 0.032, 0.036), (0.026, 0.026, 0.026)], 6, ref_up=V(0, 1, 0)), "shin")
    toe = V(s * 0.205, 0.0, -0.43)
    shin.loft(tube([ankle, ankle + (toe - ankle) * 0.5 + V(0, 0, 0.01), toe],
                   [(0.028, 0.02, 0.02), (0.036, 0.016, 0.018), (0.038, 0.01, 0.01)], 6, ref_up=V(0, 0, -1)), "foot")
    return [thigh, shin]


def build_head():
    part = Part("head", NECK, UV, parent="torso")
    # Schaedel als stehender Loft vom Oberkiefer zum Scheitel; Gesicht vorn (k = n/2), leicht nach oben geneigt
    # (er schaut zum Spieler hoch). Augen tief eingesunken, Wangen hohl, Kopf um 10 Grad zur Seite geneigt.
    n = 10
    tilt, roll = math.radians(14), math.radians(10)
    up = V(math.sin(roll), math.cos(roll) * math.cos(tilt), -math.sin(tilt) * math.cos(roll)).normalized()
    fwd = V(0, math.sin(tilt), math.cos(tilt))
    fwd = (fwd - up * fwd.dot(up)).normalized()
    side = up.cross(fwd).normalized()
    # (Hoehe ueber Mitte, halbe Breite, vorn, hinten)
    rows = [(-0.085, 0.040, 0.070, 0.025), (-0.045, 0.058, 0.078, 0.060), (-0.005, 0.068, 0.074, 0.080),
            (0.035, 0.071, 0.084, 0.086), (0.080, 0.068, 0.066, 0.090), (0.118, 0.046, 0.038, 0.062)]

    def dent(i, k):
        if i == 2 and k in (4, 6):           # Augenhoehlen
            return 0.80
        if i == 1 and k in (3, 7):           # hohle Wangen
            return 0.84
        if i == 1 and k == 5:                 # Nasenruecken
            return 1.04
        return 1.0

    rings = []
    for i, (h, w, front, back) in enumerate(rows):
        c = HEAD_C + up * h
        ring = []
        for k in range(n):
            th = 2 * math.pi * k / n           # k=0 hinten, k=5 vorn
            depth = -math.cos(th)
            d = (front if depth > 0 else back) * dent(i, k)
            ring.append(c + side * (w * math.sin(th) * dent(i, k)) + fwd * (depth * d))
        rings.append(ring)
    part.loft(rings, "head")
    return part


def build_jaw():
    part = Part("jaw", JAW_HINGE, UV, parent="head")
    # Unterkiefer, schief und offen haengend.
    centres = [V(0.0, 0.765, 0.305), V(0.004, 0.735, 0.355), V(0.010, 0.715, 0.400)]
    part.loft(tube(centres, [(0.052, 0.018, 0.012), (0.045, 0.02, 0.012), (0.025, 0.018, 0.01)], 6), "jaw")
    return part

# --- Textur ---

def skin(U, V_, seed, pale=0.0):
    h, w = U.shape
    col = np.broadcast_to(SKIN + (SKIN_PALE - SKIN) * pale, (h, w, 3)).copy()
    n1 = vnoise(h, w, 6, 6, seed)
    n2 = vnoise(h, w, 18, 18, seed + 1)
    col *= (0.9 + 0.12 * n1 + 0.08 * n2)[..., None]
    rot = np.clip((vnoise(h, w, 5, 5, seed + 2) - 0.6) * 4, 0, 0.45)
    col = mix(col, ROT, rot)
    # Licht von oben eingebacken: Unterseite (u um 0/1) dunkler, Oberseite (u = 0.5) heller.
    top = 1 - np.abs(np.cos(np.pi * U))
    col *= (0.90 + 0.18 * top)[..., None]
    return col


def joints(col, V_, strength=0.3):
    near = np.exp(-(V_ / 0.12) ** 2) + np.exp(-((1 - V_) / 0.12) ** 2)
    return col * (1 - strength * np.clip(near, 0, 1))[..., None]


def paint_torso(U, V_):
    col = skin(U, V_, 10)
    side = np.abs(np.sin(2 * np.pi * U))                     # Flanken
    # Rippen: helle Boegen, dunkle Zwischenraeume, an Flanken und Bauch, nicht ueber der Wirbelsaeule.
    rib_zone = ((V_ > 0.42) & (V_ < 0.86)).astype(float) * np.clip(1 - np.abs(U - 0.5) / 0.12, 0, 1) ** 0
    rib_zone *= np.clip((np.abs(U - 0.5) - 0.06) / 0.08, 0, 1)
    phase = ((V_ - 0.42) / 0.44 * 6 + 0.25 * np.sin(2 * np.pi * U * 2)) % 1.0
    dark = ((phase > 0.55) & (phase < 0.92)).astype(float) * rib_zone * (0.5 + 0.4 * side)
    light = ((phase > 0.08) & (phase < 0.35)).astype(float) * rib_zone * 0.35
    col = mix(col, SHADOW, dark * 0.6)
    col = mix(col, SKIN_PALE * 1.05, light)
    # Wirbelsaeule: Reihe heller Knoten mit dunklen Luecken.
    spine = np.clip(1 - np.abs(U - 0.5) / 0.035, 0, 1) * (V_ > 0.12) * (V_ < 0.95)
    knob = (((V_ * 16) % 1.0) < 0.55).astype(float)
    col = mix(col, SKIN_PALE * 1.08, spine * knob * 0.8)
    col = mix(col, SHADOW, spine * (1 - knob) * 0.6)
    # Schulterblaetter: hell, mit dunklem Rand unten.
    for cu in (0.5 - 0.13, 0.5 + 0.13):
        b = blob(U, V_, cu, 0.86, 0.07, 0.07)
        col = mix(col, SKIN_PALE, np.clip(b * 2, 0, 0.6))
        rim = blob(U, V_, cu, 0.80, 0.08, 0.05) * (1 - np.clip(b * 3, 0, 1))
        col = mix(col, SHADOW, np.clip(rim * 2, 0, 0.6))
    # Eingefallener Bauch und Taille, Schmutz zum Becken hin.
    col *= (1 - 0.22 * np.exp(-((V_ - 0.28) / 0.08) ** 2))[..., None]
    col = mix(col, DIRT, np.clip((0.15 - V_) * 4, 0, 0.5))
    return col


def paint_head(U, V_):
    col = skin(U, V_, 20, pale=0.9)
    h, w = U.shape
    # Strähniges, verfilztes Haar hinten und oben.
    strands = vnoise(h, w, 3, 40, 21) * 0.7 + vnoise(h, w, 6, 12, 22) * 0.3
    hair_zone = np.clip((V_ - 0.52) * 4, 0, 1) * np.clip((np.abs(U - 0.5) - 0.15) * 6, 0, 1)
    col = mix(col, HAIR, ((strands > 0.52) * hair_zone).astype(float) * 0.95)
    # Stirnschatten, tiefe Augenhoehlen (schmal, schraeg, nicht rund), darin ein winziger trueber Augapfel.
    col *= (1 - 0.25 * blob(U, V_, 0.5, 0.56, 0.2, 0.06))[..., None]
    for sgn in (-1, 1):
        cu = 0.5 + sgn * 0.085
        slant = (U - cu) * sgn * 0.6
        sock = blob(U, V_ - slant, cu, 0.43, 0.055, 0.075)
        col = mix(col, HOLLOW, np.clip(sock * 2.5, 0, 1))
        col = mix(col, EYE, (blob(U, V_, cu + sgn * 0.006, 0.425, 0.012, 0.022) > 0.3).astype(float))
        col = mix(col, SHADOW, np.clip(blob(U, V_, 0.5 + sgn * 0.15, 0.24, 0.05, 0.1) * 1.4, 0, 0.7))
    # Nase: nur zwei dunkle Schlitze.
    for sgn in (-1, 1):
        col = mix(col, HOLLOW, (blob(U, V_, 0.5 + sgn * 0.018, 0.25, 0.011, 0.04) > 0.2).astype(float))
    # Oberlippe weggefault: Zahnfleisch und obere Zahnreihe, unregelmaessig.
    mouth = (np.abs(U - 0.5) < 0.13) & (V_ < 0.15)
    col = mix(col, GUM, (mouth & (V_ > 0.09)).astype(float))
    teeth_n = vnoise(1, w, 1, 30, 23)[0]
    tooth = ((((U - 0.37) / 0.26 * 9) % 1.0) < 0.62) & (teeth_n[None, :] > 0.25)
    col = mix(col, TEETH, (mouth & (V_ <= 0.09) & tooth).astype(float))
    col = mix(col, HOLLOW, (mouth & (V_ <= 0.09) & ~tooth).astype(float))
    return col


def paint_jaw(U, V_):
    col = skin(U, V_, 30, pale=0.55)
    inside = np.abs(U - 0.5) < 0.2                            # Oberseite = Mundhoehle
    col = mix(col, HOLLOW, inside.astype(float))
    tooth = (((U - 0.3) / 0.4 * 7) % 1.0 < 0.6) & (V_ > 0.55)
    col = mix(col, TEETH, (inside & tooth & (np.abs(U - 0.5) < 0.17)).astype(float))
    return col


def paint_limb(seed, pale=0.0, dirt_end=0.0, knuckles=False):
    def painter(U, V_):
        col = joints(skin(U, V_, seed, pale), V_, 0.2)
        # Sehnen und Knochen: helle Linie oben entlang, dunkle Rinne daneben.
        col = mix(col, SKIN_PALE, np.clip(1 - np.abs(U - 0.5) / 0.04, 0, 1) * 0.45)
        col = mix(col, SHADOW, np.clip(1 - np.abs(U - 0.62) / 0.03, 0, 1) * 0.35)
        if dirt_end:
            col = mix(col, DIRT, np.clip((V_ - (1 - dirt_end)) / dirt_end, 0, 1) * 0.7)
        if knuckles:
            col = mix(col, SKIN_PALE, (((U * 4) % 1.0 < 0.4) & (V_ > 0.8)).astype(float) * 0.5)
        return col
    return painter


def paint_finger(U, V_):
    col = paint_limb(60, pale=0.2, dirt_end=0.5)(U, V_)
    col = mix(col, NAIL, ((V_ > 0.85) & (np.abs(U - 0.5) < 0.3)).astype(float))
    return col


def paint_rags(U, V_):
    h, w = U.shape
    col = np.broadcast_to(RAGS, (h, w, 3)).copy()
    folds = 0.88 + 0.12 * np.cos(2 * np.pi * U * 9 + 2 * vnoise(h, w, 2, 8, 70))
    stains = np.clip((vnoise(h, w, 3, 10, 71) - 0.55) * 3, 0, 0.6)
    col *= folds[..., None]
    col = mix(col, DIRT * 0.9, stains)
    col *= (0.7 + 0.3 * V_)[..., None]                        # zum Saum hin dunkler
    return col


PAINTERS = {
    "torso": paint_torso, "head": paint_head, "jaw": paint_jaw,
    "upper_arm": paint_limb(40), "forearm": paint_limb(41, dirt_end=0.25),
    "thigh": paint_limb(50), "shin": paint_limb(51, dirt_end=0.2),
    "hand": paint_limb(42, pale=0.15, dirt_end=0.8, knuckles=True), "finger": paint_finger,
    "foot": paint_limb(52, dirt_end=0.9), "rags": paint_rags, "neck": paint_limb(43, pale=0.1),
}

REST = {"jaw": -12}
WIND_UP = {"torso": 34, "head": -24, "jaw": -38,
           "upper_arm_px": 120, "upper_arm_nx": 128, "forearm_px": -25, "forearm_nx": -20}


def build_model(material):
    coll = bpy.data.collections.new("Ghul2")
    bpy.context.scene.collection.children.link(coll)
    parts = [build_torso(), build_head(), build_jaw()]
    parts += build_arm(1, 0.0) + build_arm(-1, 0.05)
    parts += build_leg(1) + build_leg(-1)
    return [p.build(coll, material) for p in parts]


def measure(model, cam, d):
    """Breite von Rumpf und Armen aus d m in Pixeln: nur der Ghul sichtbar, gedeckte Stuecke je Bildzeile."""
    aim(cam, G(V(0, 1.6, d)), G(V(0, 0.5, 0)))
    mask = coverage(render_passes(f"mess_{d}", visible=model))
    rows = np.where(mask.any(axis=1))[0]
    lines = [f"Messung aus {d} m: Figur {rows.max() - rows.min() + 1} Pixel hoch "
             f"(Zeilen {rows.min()}..{rows.max()} von unten), gedeckte Stuecke je Zeile von links nach rechts:"]
    for r in range(rows.max(), rows.min() - 1, -2):
        lines.append(f"  Zeile {r:3d}: {runs(mask[r])}")
    return "\n".join(lines)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    os.makedirs(OUT_DIR, exist_ok=True)
    texture = paint_atlas(RECTS, PAINTERS, TEX, colors=32)
    image = save_image("ghoul", texture, os.path.join(OUT_DIR, "ghoul.png"))
    model = build_model(export_material("ghoul", image))
    pose(model, {})
    export_glb(model, os.path.join(OUT_DIR, "ghoul.glb"))
    report_parts("ghoul", model)


main()
