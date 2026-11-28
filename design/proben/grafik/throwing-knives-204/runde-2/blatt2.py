"""Vergleichsblatt Runde 2 der Throwing Knives (Issue #204): Entwurf c und d neben dem Bestand.

Oben stehen c und d im selben Bild und Massstab neben Silberklinge, Pistole, Jagdaxt und Pflockarmbrust (die Dateien aus
assets/models, nur gelesen): einmal von oben auf die breite Seite, einmal von der Seite. Darunter je Entwurf dieselben Bilder
wie in Runde 1 (../blatt.py, dieselbe Schleife): vier Ansichten, Ich-Sicht 854 x 480 mit der Klinge links, steckt im Ziel, Flug.
a und b kommen auf dem Blatt nicht mehr vor. messer2.py muss vorher gelaufen sein.

Schreibt blatt.png, werkstatt-oben.png, werkstatt-seite.png und ansichten/<c|d>-<ansicht>.png.
Aufruf: "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python blatt2.py
"""

import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
import blatt as B  # noqa: E402
from messer_gemeinsam import MODELS, bpy, Matrix, Vector, V, G  # noqa: E402
from psx import FOG_COLOR, MATS, camera, save_png  # noqa: E402

B.HERE = HERE
B.DESIGNS = [("e", "e Schwarzruecken", "messer-e-schwarzruecken.glb", 196),
             ("d", "d Jagdeisen", "messer-d-jagdeisen.glb", 152)]
if "--studie" in sys.argv:                   # die Studie c durch dieselbe Schleife (nur die Einzelbilder, kein Blatt)
    B.DESIGNS = [("c", "c Silberblatt", "messer-c-silberblatt.glb", 112)]
B.POSES = {"e": (44.0, 6.0, (-17.0, 0.0, 17.0)),
           "c": (44.0, 6.0, (-17.0, 0.0, 17.0)),
           "d": (44.0, 6.0, (-17.0, 0.0, 17.0))}
B.TIP = 0.117
B.GRIP = 0.070 * B.SCALE                     # Messer weiter aus der Manschette als in Runde 1: der Ringknauf soll zu sehen sein
B.TITLE = "Throwing Knives (Issue 204), Runde 2: Entwurf e und d (Aermel, Licht, Kamera, Gang, Ziel gleich; nur die Messer wechseln)"

WIDTH = 1292
SPAN = 1.56                                  # Meter (Dateimass) ueber die Bildbreite
PPM = WIDTH / SPAN
GAP = 0.04
# Spalte (Beginn in z), Name, Datei, Ordner, Grunddrehung
ITEMS = [(0.02, "blade (Silberklinge)", "blade.glb", MODELS, None),
         (0.02, "e Schwarzruecken", "messer-e-schwarzruecken.glb", HERE, 90.0),
         (0.02, "c Silberblatt", "messer-c-silberblatt.glb", HERE, 90.0),
         (0.02, "d Jagdeisen", "messer-d-jagdeisen.glb", HERE, 90.0),
         (0.02, "pistol", "pistol.glb", MODELS, None),
         (0.60, "hunters_axe", "hunters_axe.glb", MODELS, None),
         (1.20, "stake_crossbow_loaded", "stake_crossbow_loaded.glb", MODELS, None)]


def bounds(objs):
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    return (Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))),
            Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts))))


def lineup(cam, loaded, stack):
    """Alle Stuecke in einem Bild: stack = "x" (Blick von oben, gestapelt quer) oder "y" (Blick von der Seite, gestapelt hoch)."""
    level, marks = {}, []
    for (z0, name, _f, _b, roll), objs in zip(ITEMS, loaded):
        base = B.rot(roll, "Y") if roll else Matrix()
        B.place(objs, base)
        lo, hi = bounds(objs)
        s0 = level.get(z0, GAP)
        if stack == "x":
            delta = Vector((s0 - lo.x, -z0 - hi.y, -lo.z))
            size = hi.x - lo.x
        else:
            delta = Vector((-(lo.x + hi.x) / 2, -z0 - hi.y, s0 - lo.z))
            size = hi.z - lo.z
        B.place(objs, Matrix.Translation(delta) @ base)
        level[z0] = s0 + size + GAP
        marks.append((name, z0, s0 + size))
    height = max(level.values())
    h_px = int(round(height * PPM))
    centre = V(0.0, height / 2, SPAN / 2) if stack == "y" else V(height / 2, 0.0, SPAN / 2)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = height                     # die Kamera von psx misst senkrecht
    direction, up = (V(-1, 0, 0), V(0, 1, 0)) if stack == "y" else (V(0, 1, 0), V(1, 0, 0))
    everything = [o for objs in loaded for o in objs]
    img = B.shot(everything, cam, G(centre + direction * 6.0), G(centre), 30, (WIDTH, h_px), "ansicht", G(up))
    cam.data.type = "PERSP"
    for name, z0, top in marks:
        B.text(img, name, int(z0 * PPM), max(0, h_px - int(top * PPM) - 22), 13)
    return img


def main():
    body = B.main()
    if "--studie" in sys.argv:
        return
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    B.HOME.clear()
    loaded = []
    for i, (_z, _n, fname, base, _r) in enumerate(ITEMS):
        objs = B.load(fname, f"w{i}", base)
        for o in [o for o in objs if o.name.startswith("flash")]:      # Muendungsfeuer der Pistole gehoert nicht ins Bild
            objs.remove(o)
            bpy.data.objects.remove(o)
        bpy.context.view_layer.update()
        for o in objs:
            o["smooth"] = bool(any(p.use_smooth for p in o.data.polygons))
            B.HOME[o.name] = o.matrix_world.copy()
        loaded.append(objs)
    cam = camera()
    top = lineup(cam, loaded, "x")
    side = lineup(cam, loaded, "y")
    save_png(top[::-1], os.path.join(HERE, "werkstatt-oben.png"))
    save_png(side[::-1], os.path.join(HERE, "werkstatt-seite.png"))
    lab = 26
    sheet = np.zeros((2 * lab + top.shape[0] + side.shape[0] + 16 + body.shape[0], WIDTH, 3)) + FOG_COLOR
    B.text(sheet, "Silver & Ash - Throwing Knives, Runde 2: e, c und d neben dem Bestand, ein Bild, ein Massstab - von oben auf die breite Seite (vorn = rechts)", 6, 2, 15)
    B.paste(sheet, top, 0, lab)
    y = lab + top.shape[0] + 8
    B.text(sheet, "dieselben Stuecke von der Seite (Messer hochkant gedreht wie die Klinge: man sieht die Dicke)", 6, y, 15)
    B.paste(sheet, side, 0, y + lab)
    B.paste(sheet, body, 0, y + lab + side.shape[0] + 8)
    save_png(sheet[::-1], os.path.join(HERE, "blatt.png"))
    print("BLATT 2 geschrieben:", sheet.shape[1], "x", sheet.shape[0])


if __name__ == "__main__":
    main()
