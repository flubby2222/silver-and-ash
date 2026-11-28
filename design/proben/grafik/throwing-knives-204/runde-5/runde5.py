"""Throwing Knives (Issue #204), Runde 5: Blatt mit gestaffelten Messern, Tiefenstufen und Beute am Boden.

Varianten in der Ich-Sicht (gleiche Pose: Neigung 44 Grad, Kippen 6 Grad, Faecher +-17 Grad; gleiches Licht):
  4b   Runde 4b zum Vergleich: Tiefe 40 mm, Drehpunkt 70 mm, 8 mm nach links, Messer in einer Ebene.
  s40  gestaffelt: Drehpunkt 90 mm, je Messer 6 mm entlang der Dicke versetzt (0, 6, 12 mm), Tiefe 40 mm, ganzes Messer.
  s12  wie s40, 28 mm tiefer (Tiefe 12 mm: Wicklung ganz im Aermel).
  s-16 wie s40, 56 mm tiefer (Tiefe -16 mm: "10 cm" im Mass der Ich-Sicht, die 1,8-fach zeigt).
  s-60 wie s40, 100 mm tiefer (Tiefe -60 mm: "10 cm" woertlich im Skriptmass).
Der Drehpunkt bleibt bei allen gestaffelten Varianten 50 mm hinter dem Haltepunkt im Aermel. Ab s12 passt das ganze
Messer nicht mehr in den Aermel (siehe tiefe-zahlen.txt): Das Handmodell ist dort 35 mm hinter dem Haltepunkt
abgeschnitten (staffel.use_cut), das Geschoss bleibt das ganze Messer.

Beute: das Buendel liegt wie eine Waffe in PickupView.DrawEquipment (nur gelesen) flach auf der Leuchtplatte in der
Stufenfarbe, in 3 m und 8 m vor dem Auge (Augenhoehe 1,6 m), daneben der heutige Platzhalter-Quader.

Schreibt runde-5.png (Blatt), ansichten/*.png, staffel-zahlen.txt und tiefe-zahlen.txt.
Aufruf: "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python runde5.py
"""

import os
import sys

import numpy as np

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import staffel as S  # noqa: E402

B, G, V, Matrix, Vector, SCALE, bpy, bmesh = S.B, S.G, S.V, S.Matrix, S.Vector, S.SCALE, S.bpy, S.bmesh
from psx import pass_material, save_image, write_light  # noqa: E402

INSIDE = 0.050                                   # Drehpunkt hinter dem Haltepunkt (gestaffelte Varianten)
STEP = (0.0, 0.006, 0.012)                       # Staffelung entlang der Dicke: rechts, Mitte, links
CUT = 0.035                                      # Handmodell der tiefen Varianten: so weit hinter dem Haltepunkt abgeschnitten
# Schluessel, Titel, Tiefe, Drehpunkt, Versatz, Staffel, Schnitt (None = ganzes Messer)
VARIANTS = [("4b", "Runde 4b (vorher)", 0.040, 0.070, 0.008, (0, 0, 0), None),
            ("s40", "gestaffelt, Tiefe wie 4b", 0.040, 0.090, 0.008, STEP, None),
            ("s12", "gestaffelt, 28 mm tiefer", 0.012, 0.062, 0.008, STEP, CUT),
            ("s-16", "gestaffelt, 56 mm tiefer", -0.016, 0.034, 0.007, STEP, CUT),
            ("s-60", "gestaffelt, 100 mm tiefer", -0.060, -0.010, 0.004, STEP, CUT)]
CHECKS = [("pruef-rechts-oben", V(-0.30, 0.26, 0.22), V(-0.02, -0.01, -0.01), 22),
          ("pruef-links-unten", V(0.50, -0.22, -0.02), V(0, -0.07, -0.07), 26)]
TIER = (175, 25, 35)                             # Vampiric, Mitte des Pulses (ChestView.Colours)
DARK = (20, 16, 22)                              # PickupView.Dark
YAW = 40.0                                       # Beute liegt schraeg (im Spiel: drop.Id * 1,3 rad)


def total(r):
    return sum(c for c, _p in r)


def scan_stagger(s, lines):
    """Freie Fenster des Versatzes (kein Durchstich nach Regel 30) je Drehpunkt und Staffelung, ganzes Messer, Tiefe 40."""
    S.use_cut(s, None)
    lines += ["Throwing Knives e, kurzes Band, Tiefe 40 mm, ganzes Messer: Versatz nach links (mm) ohne Durchstich",
              "Spalten: Drehpunkt mm | Staffel je Messer mm | freie Versaetze | Dreieckspaare Messer gegen Messer", ""]
    for piv in (70, 80, 90, 95):
        for t in (0, 4, 6, 8, 10):
            free = []
            for dx in range(0, 15):
                S.put(s, 0.040, piv / 1000, dx / 1000, thick=(0, t / 1000, 2 * t / 1000))
                if total(S.pierce(s)) == 0 and total(S.seen(s)) == 0:
                    free.append(dx)
            lines.append(f"{piv:3d} | {t:2d} | {free} | {S.crossing(s)}")


def scan_depth(s, lines):
    """Je Tiefe: kleinste Zahl der Durchstiche mit dem ganzen Messer und freie Fenster mit dem abgeschnittenen."""
    lines += ["Throwing Knives e, gestaffelt (0, 6, 12 mm), Drehpunkt 50 mm im Aermel: Durchstiche je Tiefe",
              "Spalten: Tiefe mm | Modell | Dreiecke | Versatz mm: Dreieckspaare gegen den Aermel", ""]
    for depth in (0.040, 0.012, -0.016, -0.060):
        for cut in (None, CUT):
            n = S.use_cut(s, None if cut is None else -(depth + cut))
            res = []
            for dx in range(0, 13, 2):
                S.put(s, depth, depth + INSIDE, dx / 1000, thick=STEP)
                res.append(f"{dx}: {total(S.pierce(s))}")
            lines.append(f"{depth * 1000:4.0f} | {'ganz' if cut is None else 'ab 35 mm im Aermel abgeschnitten'} | {n} | " + ", ".join(res))


def flat(name, colour, size):
    """Quader in einer Farbe (Spielmasse x, y, z), Mittelpunkt im Ursprung."""
    img = save_image(name, np.zeros((2, 2, 3)) + np.array(colour, float), os.path.join(bpy.app.tempdir, name + ".png"))
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bm.loops.layers.uv.verify()
    for v in bm.verts:
        v.co = Vector((v.co.x * size[0], v.co.y * size[2], v.co.z * size[1]))
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    o = bpy.data.objects.new(name, mesh)
    o["smooth"] = False
    bpy.context.scene.collection.objects.link(o)
    o.data.materials.append(pass_material("beute_" + name, img))
    B.HOME[o.name] = Matrix()
    write_light(o)
    return o


def lay(groups, p, k, pivot, thick, spread=17.0):
    """Buendel flach am Boden: Dicke senkrecht, Faecher um den Drehpunkt; k = 1 ist Dateimass (1,8-fach)."""
    for group, angle, t in zip(groups, (-spread, 0.0, spread), thick):
        fan = (Matrix.Translation(Vector((0, pivot * SCALE, 0))) @ B.rot(angle, "Z")
               @ Matrix.Translation(Vector((0, -pivot * SCALE, 0))))
        B.place(group, B.world(p) @ B.rot(YAW, "Z") @ Matrix.Translation(Vector((0, 0, 0.012 + (0.005 + t) * SCALE * k)))
                @ Matrix.Scale(k, 4) @ Matrix.Translation(Vector((0, -0.01 * SCALE, 0))) @ fan @ B.rot(90, "Y"))


def loot(s, out):
    """Beute am Boden in 3 m und 8 m. Rueckgabe: Spielbild, Kacheln, Zeilen mit Messwerten."""
    S.use_cut(s, None)
    hand = [o for g in s.knives for o in g] + [o for g in s.wraps for o in g]
    B.place(hand, B.AWAY)
    # Schluessel, Beschriftung, Massstab, Drehpunkt, Staffel
    items = [("quader", "heute: Quader", None, None, None, None),
             ("b4", "4b, 1,8-fach", 1.0, 0.070, (0, 0, 0), 17.0),
             ("st", "gestaffelt, 1,8-fach", 1.0, 0.090, STEP, 17.0),
             ("st1", "gestaffelt, 1:1", 1 / SCALE, 0.090, STEP, 17.0),
             ("st25", "gestaffelt, 2,5-fach", 2.5 / SCALE, 0.090, STEP, 17.0),
             ("weit", "gestaffelt, 2,5-fach, Faecher +-32", 2.5 / SCALE, 0.090, STEP, 32.0)]
    xs = (1.875, 1.125, 0.375, -0.375, -1.125, -1.875)   # +x liegt links im Bild
    src = s.knives[0]
    sets, plates, boxes, lines = {}, [], [], []
    plate_colour = tuple(int(d + (t - d) * 0.35) for d, t in zip(DARK, TIER))
    for dist in (3.0, 8.0):
        for (key, _label, k, piv, thick, spread), x in zip(items, xs):
            p = (x, 0.0, B.EYE.z + dist)
            plate = flat(f"platte_{key}_{dist}", plate_colour, (0.75, 0.01, 0.32))
            B.place([plate], B.world((p[0], 0.005, p[2])) @ B.rot(YAW + 90, "Z"))
            plates.append(plate)
            if k is None:
                box = flat(f"quader_{dist}", TIER, (0.55, 0.09, 0.12))
                B.place([box], B.world((p[0], 0.055, p[2])) @ B.rot(YAW + 90, "Z"))
                boxes.append(box)
                continue
            groups = B.copies(src, 4)[1:]
            lay(groups, p, k, piv, thick, spread)
            sets[(key, dist)] = groups
    everything = plates + boxes + [o for gs in sets.values() for g in gs for o in g]
    frame = np.array(B.player(list(s.room) + everything, s.cam))
    S.save_png(frame[::-1], os.path.join(out, "beute-854x480.png"))
    base = np.array(B.player(list(s.room) + plates, s.cam))
    for (key, dist), groups in sets.items():
        m = [S.diff(np.array(B.player(list(s.room) + plates + g, s.cam)), base) for g in groups]
        both = int(((m[0] & m[1]) | (m[1] & m[2]) | (m[0] & m[2])).sum())
        label = next(i[1] for i in items if i[0] == key)
        lines.append(f"Beute {label}, {dist:.0f} m: Bildpunkte je Messer {[int(x.sum()) for x in m]}, "
                     f"davon von mehr als einem Messer belegt {both}")
    tiles = []
    for dist, f, w, h in ((3.0, 3, 96, 66), (8.0, 8, 36, 24)):
        row = []
        for (key, label, *_r), x in zip(items, xs):
            c = B.pixel((x, 0.03, B.EYE.z + dist))
            row.append((f"{label}, {dist:.0f} m ({f}x)", B.up_n(B.crop(frame, c, w, h), f)))
        tiles.append(row)
    B.place(everything, B.AWAY)
    return frame, tiles, lines


def main():
    out = os.path.join(HERE, "ansichten")
    os.makedirs(out, exist_ok=True)
    s = S.setup()
    numbers = []
    scan_stagger(s, numbers)
    with open(os.path.join(HERE, "staffel-zahlen.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(numbers) + "\n")
    print("\n".join("RUNDE5 " + x for x in numbers))
    numbers = []
    scan_depth(s, numbers)
    with open(os.path.join(HERE, "tiefe-zahlen.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(numbers) + "\n")
    print("\n".join("RUNDE5 " + x for x in numbers))

    tiles, report = [], []
    shown = [o for g in s.knives for o in g]
    for key, title, depth, pivot, dx, thick, cut in VARIANTS:
        n = S.use_cut(s, None if cut is None else -(depth + cut))
        S.put(s, depth, pivot, dx, thick=thick)
        p, c, v = S.pierce(s), S.crossing(s), S.seen(s)
        r = S.overlap(s)
        rows = r["rows"]
        grip = max(0.0, min(depth, 0.054)) * 1000
        blade = min(S.BLADE, S.BLADE + depth) * 1000
        notes = [f"Tiefe {depth * 1000:.0f} mm, Drehpunkt {pivot * 1000:.0f}, Versatz {dx * 1000:.0f}, Staffel {thick[1] * 1000:.0f} mm",
                 f"sichtbar: Klinge {blade:.0f} von 117 mm, Griff {grip:.0f} mm",
                 f"Bildpunkte je Messer {r['visible']}",
                 f"Durchstiche Aermel {total(p)}, sichtbar hinter dem Deckel {total(v)}",
                 f"Messer durchdringen sich: {sum(c)} Dreieckspaare",
                 "Messer ueberdecken sich im Bild: " + (f"{len(rows)} Zeilen, bis {r['width']} px" if rows else "nirgends"),
                 "Wicklung frei/ueberdeckt px: " + ", ".join(f"{a - b}/{b}" for a, b, _c in r["wrap"]),
                 f"Handmodell: {n} Dreiecke" + ("" if cut is None else " (abgeschnitten)")]
        report.append(f"RUNDE5 {key} {title} | " + " | ".join(notes))
        report.append(S.line("RUNDE5", key, r) + f" | pierce {p} seen {v}")
        eye = np.array(B.player(list(s.room) + shown + s.sleeve + s.blade, s.cam))
        S.save_png(eye[::-1], os.path.join(out, f"{key}-ich-sicht-854x480.png"))
        for name, d, aim, fov in CHECKS:
            img = np.array(B.shot(shown + s.sleeve, s.cam, G(s.hand + d), G(s.hand + aim), fov, (427, 240), "ansicht"))
            S.save_png(img[::-1], os.path.join(out, f"{key}-{name}.png"))
        tiles.append((f"{key}: {title}", notes, B.up_n(B.crop(eye, (520, 310), 260, 340), 2)))
    print("\n".join(report))

    frame, loot_tiles, loot_lines = loot(s, out)
    print("\n".join("RUNDE5 " + x for x in loot_lines))
    with open(os.path.join(HERE, "beute-zahlen.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(loot_lines) + "\n")

    tw, th, gap, lab, nl = 520, 680, 10, 24, 8
    width = 5 * tw + 4 * gap
    y_loot = 34 + lab + th + nl * 17 + 16
    sheet = np.zeros((y_loot + lab + 480 + 12, width, 3)) + S.FOG_COLOR
    B.text(sheet, "Silver & Ash - Throwing Knives e, Runde 5: gestaffelt und tiefer im Aermel. Ich-Sicht 854 x 480, Ausschnitt zweifach; "
           "gleiche Pose (44 Grad, 6 Grad, Faecher +-17 Grad), gleiches Licht. Masse im Skriptmass.", 6, 6, 17)
    for i, (title, notes, img) in enumerate(tiles):
        x = i * (tw + gap)
        B.text(sheet, title, x + 4, 34, 17)
        B.paste(sheet, img, x, 34 + lab)
        for j, note in enumerate(notes):
            B.text(sheet, note, x + 4, 34 + lab + th + 4 + j * 17, 13)
    B.text(sheet, "Beute am Boden wie in PickupView (Leuchtplatte in Stufenfarbe Vampiric), Auge 1,6 m hoch: Spielbild 854 x 480 mit "
           "beiden Reihen (vorn 3 m, hinten 8 m); rechts Ausschnitte", 6, y_loot, 15)
    B.paste(sheet, frame, 0, y_loot + lab)
    for r, row in enumerate(loot_tiles):
        for i, (label, img) in enumerate(row):
            x = 854 + gap + i * (img.shape[1] + 6)
            y = y_loot + lab + r * 240
            B.text(sheet, label, x + 2, y, 11)
            B.paste(sheet, img, x, y + 16)
    S.save_png(sheet[::-1], os.path.join(HERE, "runde-5.png"))
    print("RUNDE5 Blatt geschrieben:", sheet.shape[1], "x", sheet.shape[0])


if __name__ == "__main__":
    main()
