"""Throwing Knives (Issue #204), Runde 2: Entwurf c "Silberblatt" und d "Jagdeisen".

Beide sind aus dem Bestand abgeleitet (Liste "Was die Spielmodelle gemeinsam haben" in ../LIESMICH.md), nicht aus a und b:
    c  Silberblatt  die kleine Schwester der Silberklinge (tools/models/blade.py): helle Stahlklinge ohne Umriss, dunkle
                    Hohlkehle, kurze Parierstange, braune Lederwicklung mit Silberdraht, Silberknauf. Kein Rot, wie an der
                    Pflockarmbrust (der einzigen abgenommenen Waffe der Stufe Vampiric).
    d  Jagdeisen    aus der Werkstatt der Jagdaxt (Fassung C3) und der Armbrust: blattfoermige Klinge aus Eisen mit Patina,
                    Kratzern und blank gewetzter Schneide, Eisenzwinge, Griff aus dunklem Holz, Eisenkappe. Die Stufe steht
                    als ein schmaler blutroter Ring hinter der Zwinge (wie die Baender der Kiste chest_vampire).
Farben nur aus assets/palette/hauptpalette.json, harte Stufen (wie Axt, Peitsche und Armbrust). Aufbau wie in Runde 1:
ein Knoten "knife", Ursprung am Klingenansatz, Spitze +z, Klinge breit in y, duenn in x, Massstab 1,8 eingebacken, Textur
128 x 128, flach schattiert, rundum geschlossen. Schreibt messer-c-silberblatt.glb, messer-d-jagdeisen.glb, textur2.png.

Aufruf: "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python messer2.py
"""

import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
from messer_gemeinsam import (PROF_DIAMOND, SCALE, TEX, Matrix, Part, _ROWS, bake_scale, bpy, circle, closed_report,  # noqa: E402
                              export_glb, export_material, pose, rects_from_pixels, save_image, step, sweep, vnoise, V)

EIS = _ROWS["Eisen"]
HOLZ = _ROWS["Holz"]
BLUT = _ROWS["Blutrot"]

RECTS = {
    "blade_c": (0, 0, 64, 56),
    "blade_d": (64, 0, 128, 56),
    "silver": (0, 56, 64, 80),
    "iron": (64, 56, 128, 80),
    "leather": (0, 80, 64, 128),
    "wood": (64, 80, 112, 128),
    "blood": (112, 80, 128, 128),
}
UV = rects_from_pixels(RECTS, TEX)


def _edge_distance(U):
    """Raute: Grat bei u = 0 und 0,5, Schneide bei u = 0,25 und 0,75. 0 = Schneide, 1 = Grat."""
    return np.minimum(np.abs(U - 0.25), np.abs(U - 0.75)) * 4


def blade_c(U, V_):
    """Wie paint_blade der Silberklinge: die ganze Klinge hell, Schneide nur eine Stufe heller, Hohlkehle dunkel, Flecken."""
    h, w = U.shape
    d = _edge_distance(U)
    col = step(EIS[5:8], 0.48 + 0.5 * vnoise(h, w, 3, 18, 501))                      # Eisen 6 und 7: das Silber des Jaegers
    col = np.where((vnoise(h, w, 8, 8, 502) > 0.80)[..., None], EIS[5], col)         # Flecken
    col = np.where((d < 0.14)[..., None], EIS[7], col)                               # geschliffene Schneide
    fuller = (d > 0.70) & (V_ > 0.07) & (V_ < 0.62)
    col = np.where(fuller[..., None], step(EIS[3:5], 0.25 + 0.6 * vnoise(h, w, 2, 14, 503)), col)   # Hohlkehle
    return col


def blade_d(U, V_):
    """Wie die Platte der Jagdaxt C3: Eisen 3-4 mit Patina (Eisen 2), einzelne Kratzer, blank gewetzte Schneide (Eisen 6)."""
    h, w = U.shape
    d = _edge_distance(U)
    f = 0.1 + 0.8 * vnoise(h, w, 4, 5, 521)
    col = step(EIS[3:5], f)
    col = np.where((vnoise(h, w, 5, 5, 522) < 0.36)[..., None], EIS[2], col)
    rng = np.random.default_rng(525)
    scratch = np.zeros(U.shape, bool)
    for _ in range(6):
        u0, v0, slope = rng.uniform(0, 1), rng.uniform(0.1, 0.8), rng.uniform(0.5, 1.6)
        t = V_ - v0
        scratch |= (np.abs((U - u0) - t / slope * 0.25) < 0.012) & (t > 0) & (t < rng.uniform(0.08, 0.2))
    col = np.where(scratch[..., None], EIS[6], col)
    col = np.where(((d < 0.30) & (d >= 0.16))[..., None], step(EIS[4:6], f), col)
    worn = (d < 0.16) & (vnoise(h, w, 10, 10, 524) > 0.30)
    col = np.where(worn[..., None], EIS[6], col)
    return col


def silver_painter(U, V_):
    """Parierstange und Knauf: dunkleres Silber als die Klinge (wie DARK_SILVER der Silberklinge)."""
    h, w = U.shape
    return step(EIS[4:7], 0.2 + 0.7 * vnoise(h, w, 4, 12, 508))


def iron_painter(U, V_):
    """Zwingen: das Eisen der Armbrust (Eisen 1-4, vereinzelte Pocken)."""
    h, w = U.shape
    f = 0.34 + 0.26 * vnoise(h, w, 3, 4, 505)
    f = np.where(vnoise(h, w, 7, 7, 506) < 0.08, 0.05, f)
    return step(EIS[1:5], f)


def leather_painter(U, V_):
    """Wie paint_grip der Silberklinge: schraege braune Lederwicklung, duenner Silberdraht dazwischen."""
    h, w = U.shape
    wrap = (U * 2 + V_ * 7) % 1.0
    col = step(HOLZ[1:4], np.where(wrap > 0.18, 0.40 + 0.5 * vnoise(h, w, 6, 10, 510), 0.0))
    return np.where((wrap < 0.07)[..., None], EIS[6], col)


def wood_painter(U, V_):
    """Wie das Holz der Armbrust: Maserung laengs, dunkle Fasern, abgegriffene Stellen (Holz 1-4)."""
    h, w = U.shape
    f = 0.15 + 0.7 * vnoise(h, w, 2, 24, 511)
    f = np.where(vnoise(h, w, 9, 30, 512) < 0.14, 0.0, f)
    f = np.where(vnoise(h, w, 4, 6, 513) > 0.86, 0.95, f)
    return step(HOLZ[1:5], f)


def blood_painter(U, V_):
    """Der Ring der Stufe Vampiric: Blutrot 3 (150, 20, 30), Raender Blutrot 2."""
    return np.where((np.abs(V_ - 0.5) < 0.32)[..., None], BLUT[3], BLUT[2]) + 0 * U[..., None]


PAINTERS = {"blade_c": blade_c, "blade_d": blade_d, "silver": silver_painter, "iron": iron_painter, "leather": leather_painter,
            "wood": wood_painter, "blood": blood_painter}


def make_atlas():
    atlas = np.zeros((TEX, TEX, 3)) + EIS[0]
    for name, (x0, y0, x1, y1) in RECTS.items():
        pw, ph = x1 - x0, y1 - y0
        U, V_ = np.meshgrid((np.arange(pw) + 0.5) / pw, (np.arange(ph) + 0.5) / ph)
        atlas[y0:y1, x0:x1] = PAINTERS[name](U, V_)
    return atlas


def build_c():
    k = Part("knife", (0, 0, 0), UV)
    # Klinge: gerade, zweischneidig, Rautenquerschnitt wie die Silberklinge (dort halbe Breite 0,0105, halbe Dicke 0,0028)
    sweep(k, [(0, 0, -0.004), (0, 0, 0.018), (0, 0, 0.075), (0, 0, 0.102), (0, 0, 0.118)],
          [(0.0030, 0.0098), (0.0030, 0.0104), (0.0027, 0.0090), (0.0020, 0.0056), (0.0004, 0.0006)], "blade_c", PROF_DIAMOND)
    # kurze gerade Parierstange (die der Silberklinge ohne die eingerollten Enden)
    sweep(k, [(0, -0.0155, -0.006), (0, 0.0155, -0.006)], [(0.0042, 0.0045)] * 2, "silver", up=V(0, 0, 1))
    # Griff: Lederwicklung mit Silberdraht, leicht bauchig
    sweep(k, [(0, 0, -0.058), (0, 0, -0.034), (0, 0, -0.010)], [(0.0058, 0.0062), (0.0066, 0.0072), (0.0058, 0.0062)], "leather",
          circle(6))
    # Knauf wie an der Silberklinge: kurzer Doppelkegel
    sweep(k, [(0, 0, -0.070), (0, 0, -0.065), (0, 0, -0.057)], [(0.0040, 0.0040), (0.0082, 0.0088), (0.0060, 0.0064)], "silver",
          circle(6))
    return k


def build_d():
    k = Part("knife", (0, 0, 0), UV)
    # Klinge: Blatt, am breitesten im unteren Drittel (liest sich auch klein und im Flug als Messer, nicht als Dolch)
    sweep(k, [(0, 0, -0.004), (0, 0, 0.012), (0, 0, 0.048), (0, 0, 0.092), (0, 0, 0.116)],
          [(0.0030, 0.0068), (0.0034, 0.0104), (0.0034, 0.0146), (0.0024, 0.0082), (0.0004, 0.0006)], "blade_d", PROF_DIAMOND)
    # Eisenzwinge am Klingenansatz, dahinter der schmale blutrote Ring der Stufe
    sweep(k, [(0, 0, -0.012), (0, 0, -0.001)], [(0.0072, 0.0086)] * 2, "iron", circle(8, math.pi / 8))
    sweep(k, [(0, 0, -0.0165), (0, 0, -0.0115)], [(0.0068, 0.0080)] * 2, "blood", circle(8, math.pi / 8))
    # Griff aus dunklem Holz, leicht bauchig, mit Eisenkappe
    sweep(k, [(0, 0, -0.066), (0, 0, -0.040), (0, 0, -0.016)], [(0.0054, 0.0068), (0.0062, 0.0080), (0.0056, 0.0070)], "wood",
          circle(6))
    sweep(k, [(0, 0, -0.074), (0, 0, -0.065)], [(0.0064, 0.0078)] * 2, "iron", circle(8, math.pi / 8))
    return k


DESIGNS = [("c", "c Silberblatt", "messer-c-silberblatt.glb", build_c),
           ("d", "d Jagdeisen", "messer-d-jagdeisen.glb", build_d)]


def export(key, label, fname, builder):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    knife = builder()
    open_e, multi, loose, edges = closed_report([knife])
    print(f"PRUEFUNG {label}: Kanten {edges}, offene Kanten {open_e}, Kanten mit mehr als zwei Flaechen {multi}, lose Kanten {loose}")
    assert open_e == 0 and multi == 0 and loose == 0, "Modell nicht geschlossen"
    tex = make_atlas()
    colors = {tuple(int(v) for v in c) for c in tex.reshape(-1, 3)}
    palette = {tuple(int(v) for v in c) for row in _ROWS.values() for c in row}
    assert len(colors) <= 32 and colors <= palette, (len(colors), colors - palette)
    image = save_image("messer_" + key, tex, os.path.join(HERE, "textur2.png"))
    coll = bpy.data.collections.new("Messer")
    bpy.context.scene.collection.children.link(coll)
    obj = knife.build(coll, export_material("messer_" + key, image), smooth=False)
    tris = len(obj.data.polygons)
    assert tris <= 500, tris                                     # drei Messer zusammen bleiben unter 1500
    pose([obj], {}, root=Matrix.Scale(SCALE, 4))
    bake_scale([obj])
    export_glb([obj], os.path.join(HERE, fname))
    co = [v.co for v in obj.data.vertices]
    print(f"MODELL {label}: {tris} Dreiecke je Messer, {len(colors)} Farben, Textur {TEX}x{TEX}, "
          f"Laenge {(max(c.y for c in co) - min(c.y for c in co)) / SCALE:.3f} m, "
          f"Breite {(max(c.z for c in co) - min(c.z for c in co)) / SCALE:.3f} m (Spielmass, Datei 1,8-fach)")


if __name__ == "__main__":
    for key, label, fname, builder in DESIGNS:
        export(key, label, fname, builder)
