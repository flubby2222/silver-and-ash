"""Throwing Knives als Beute am Boden (Issue #819): drei Entwuerfe neben dem heutigen Quader.

Die Beute ist dasselbe ganze Messer wie das Geschoss (../throwing-knives-204/runde-5/messer-e-geschoss.glb, dort als
runde-4/messer-e-kurzband.glb geladen: Byte fuer Byte dieselbe Datei), dreimal, 2,5-fach (Messer 0,53 m, so lang wie der
heutige Quader mit 0,55 m). Frei sind nur Anordnung und Traeger; alle drei Entwuerfe kommen ohne Traeger aus und
stehen, statt zu liegen, weil ein flaches Buendel in 8 m nicht liest (Runde 5 von #204).

  a "Dreistich"  drei Messer mit der Spitze im Boden, im Kreis je 120 Grad, nach aussen geneigt: Griffe und Baender oben.
  b "Dreibein"   drei Messer auf dem Griffende, Spitzen oben aneinander gelehnt wie zusammengestellte Gewehre.
  c "Staffel"    drei Messer mit der Spitze im Boden, gleich geneigt, schraeg hintereinander und verschieden tief.

Gemessen wird wie das Beute-Bild der Runde 5: Auge 1,6 m, 3 m und 8 m, Leuchtplatte in der Stufenfarbe Vampiric. Drei
Drehwinkel wie im Spiel (drop.Id * 1,3 rad, Id 0, 1, 2), je Winkel eine andere Stelle des Pulses (glow 0,10 / 0,35 / 0,60,
Stufenfarbe dunkel / Mitte / hell; PickupView und ChestView nur gelesen).

Schreibt beute-819.png (Blatt), ansichten/*.png und beute-zahlen.txt.
Aufruf: "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python beute.py
"""

import math
import os
import sys

import numpy as np

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "throwing-knives-204", "runde-5"))
import staffel as S  # noqa: E402
import runde5 as R5  # noqa: E402

B, G, V, Matrix, Vector, SCALE = S.B, S.G, S.V, S.Matrix, S.Vector, S.SCALE

U = 2.5                                          # Beute-Massstab: Meter in der Welt je Meter Skriptmass
K = U / SCALE                                    # Faktor auf die Datei (die ist 1,8-fach)
BLADE, REAR, LENGTH = 0.117, 0.097, 0.214        # Messer laengs: Spitze, Bandende, Gesamtlaenge (Skriptmass)
ANGLES = [math.degrees(1.3 * i) for i in range(3)]
GLOW = (0.10, 0.35, 0.60)
TIERS = ((150, 20, 30), (175, 25, 35), (200, 30, 40))
XS = (1.65, 0.55, -0.55, -1.65)                  # +x liegt links im Bild
DISTS = (3.0, 8.0)


def T(x, y, z):
    return Matrix.Translation(Vector((x * U, y * U, z * U)))


def dreistich():
    return [B.rot(120 * i, "Z") @ T(0.034, 0, 0) @ B.rot(24, "Y") @ T(0, 0, BLADE - 0.020) @ B.rot(90, "X")
            for i in range(3)]


def dreibein():
    lean = 27.0
    r = (LENGTH - 0.030) * math.sin(math.radians(lean))
    return [B.rot(120 * i, "Z") @ T(r, 0.007, 0) @ B.rot(-lean, "Y") @ T(0, 0, REAR + 0.004) @ B.rot(-90, "X")
            for i in range(3)]


def staffel():
    return [T((i - 1) * 0.055, (i - 1) * 0.042, 0) @ B.rot(28, "Y") @ T(0, 0, BLADE - sunk) @ B.rot(90, "X") @ B.rot(40, "Y")
            for i, sunk in enumerate((0.015, 0.030, 0.045))]


# Schluessel, Name, Anordnung, Leuchtplatte (x, z)
DESIGNS = [("quader", "heute: Quader 0,55 x 0,09 x 0,12 m", None, (0.75, 0.32)),
           ("a", 'a "Dreistich"', dreistich, (0.62, 0.62)),
           ("b", 'b "Dreibein"', dreibein, (0.62, 0.62)),
           ("c", 'c "Staffel"', staffel, (0.70, 0.55))]


class Set:
    pass


def window(x, dist):
    """Bildfenster eines Stuecks: Spalten und Zeilen, in denen nur dieses Stueck liegt."""
    z = B.EYE.z + dist
    cx, top = B.pixel((x, 0.70, z))
    _x, bottom = B.pixel((x, -0.15, z - 0.2))
    half = int(0.5 / dist * B.FOCAL)
    return slice(max(top, 0), min(bottom, B.GAME[1])), slice(max(cx - half, 0), min(cx + half, B.GAME[0]))


def main():
    out = os.path.join(HERE, "ansichten")
    os.makedirs(out, exist_ok=True)
    s = S.setup()
    B.place([o for g in s.knives + s.wraps for o in g] + s.sleeve + s.blade, B.AWAY)
    src = s.knives[0]
    sets = {}
    for key, _name, build, _plate in DESIGNS:
        for dist in DISTS:
            if build is not None:
                sets[(key, dist)] = B.copies(src, 4)[1:]
    tris = sum(len(S.R.tris(o)[1]) for o in src)

    frames, rows, numbers = [], {}, {}
    for j, (yaw, glow, tier) in enumerate(zip(ANGLES, GLOW, TIERS)):
        colour = tuple(int(d + (t - d) * glow) for d, t in zip(R5.DARK, tier))
        plates, boxes = [], []
        for (key, _name, build, plate), x in zip(DESIGNS, XS):
            for dist in DISTS:
                p = (x, 0.0, B.EYE.z + dist)
                o = R5.flat(f"platte_{key}_{dist}_{j}", colour, (plate[0], 0.01, plate[1]))
                B.place([o], B.world((p[0], 0.005, p[2])) @ B.rot(yaw, "Z"))
                plates.append(o)
                if build is None:
                    o = R5.flat(f"quader_{dist}_{j}", tier, (0.55, 0.09, 0.12))
                    B.place([o], B.world((p[0], 0.055, p[2])) @ B.rot(yaw, "Z"))
                    boxes.append(o)
                else:
                    for group, m in zip(sets[(key, dist)], build()):
                        B.place(group, B.world(p) @ B.rot(yaw, "Z") @ m @ Matrix.Scale(K, 4))
        knives = [o for gs in sets.values() for g in gs for o in g]
        frame = np.array(B.player(list(s.room) + plates + boxes + knives, s.cam))
        S.save_png(frame[::-1], os.path.join(out, f"spielbild-winkel-{j}-854x480.png"))
        frames.append(frame)
        base = np.array(B.player(list(s.room) + plates, s.cam))
        masks = [S.diff(np.array(B.player(list(s.room) + plates + [o for gs in sets.values() for o in gs[i]], s.cam)), base)
                 for i in range(3)]
        box = S.diff(np.array(B.player(list(s.room) + plates + boxes, s.cam)), base)
        for (key, _name, build, _plate), x in zip(DESIGNS, XS):
            for dist in DISTS:
                ys, xs = window(x, dist)
                if build is None:
                    m = box[ys, xs]
                    numbers[(key, dist, j)] = ([int(m.sum())], 0, int(m.any(1).sum()), int(m.any(0).sum()))
                else:
                    ms = [m[ys, xs] for m in masks]
                    shared = (ms[0] & ms[1]) | (ms[1] & ms[2]) | (ms[0] & ms[2])
                    union = ms[0] | ms[1] | ms[2]
                    numbers[(key, dist, j)] = ([int(m.sum()) for m in ms], int(shared.sum()),
                                               int(union.any(1).sum()), int(union.any(0).sum()))
                c = B.pixel((x, 0.22, B.EYE.z + dist))
                w, h, f = (120, 100, 3) if dist == 3.0 else (45, 37, 8)
                rows.setdefault(key, []).append(B.up_n(B.crop(frame, c, w, h), f))
        if j == 0:
            cross = {}
            for (key, _n, build, _p) in DESIGNS:
                if build is not None:
                    q = Set()
                    q.knives = sets[(key, 3.0)]
                    cross[key] = S.crossing(q)
            views = {}
            for (key, _name, build, _plate), x in zip(DESIGNS, XS):
                p = V(x, 0.0, B.EYE.z + 3.0)
                mine = [o for o in plates + boxes if o.name.endswith(f"_3.0_{j}") and (f"_{key}_" in o.name or (build is None and o.name.startswith("quader")))]
                mine += [] if build is None else [o for g in sets[(key, 3.0)] for o in g]
                for name, d in (("vorn", V(0, 0.35, -1.9)), ("seite", V(1.9, 0.35, 0)), ("hinten", V(0, 0.35, 1.9)),
                                ("schraeg-oben", V(-1.1, 1.5, -1.1))):
                    img = np.array(B.shot(mine, s.cam, G(p + d), G(p + V(0, 0.22, 0)), 24, (340, 340), "ansicht"))
                    S.save_png(img[::-1], os.path.join(out, f"{key}-{name}.png"))
                    if name == "schraeg-oben":
                        views[key] = img
        B.place(plates + boxes, B.AWAY)

    lines = [f"Throwing Knives als Beute (#819): Bildpunkte im Spielbild 854 x 480, Auge 1,6 m. Messer 2,5-fach, {tris} Dreiecke je Messer.",
             "Je Drehwinkel (0 / 74 / 149 Grad): Bildpunkte je Messer [1, 2, 3] | von mehr als einem Messer belegt | Hoehe x Breite des Umrisses", ""]
    notes = {}
    for key, name, build, plate in DESIGNS:
        notes[key] = [name]
        if build is None:
            notes[key].append("12 Dreiecke, eine Farbe (Stufenfarbe), keine Textur")
        else:
            notes[key].append(f"3 x {tris} = {3 * tris} Dreiecke, kein Traeger")
            notes[key].append("Textur des Messers 128 x 128, 13 Farben")
            notes[key].append(f"Messer durchdringen sich: {sum(cross[key])} Dreieckspaare")
        notes[key].append(f"Leuchtplatte {plate[0]:.2f} x {plate[1]:.2f} m")
        for dist in DISTS:
            per = [numbers[(key, dist, j)] for j in range(3)]
            lines.append(f"{name}, {dist:.0f} m: " + " ; ".join(f"{a} | {b} | {h} x {w}" for a, b, h, w in per))
            low = min(min(a) for a, _b, _h, _w in per)
            share = max(b for _a, b, _h, _w in per)
            hs = [h for _a, _b, h, _w in per]
            total = [sum(a) - b for a, b, _h, _w in per]
            if build is None:
                notes[key].append(f"{dist:.0f} m: {min(total)} bis {max(total)} px, Hoehe {min(hs)} bis {max(hs)} px")
            else:
                notes[key].append(f"{dist:.0f} m: je Messer mind. {low} px, geteilt hoechstens {share} px")
                notes[key].append(f"{dist:.0f} m: Hoehe des Umrisses {min(hs)} bis {max(hs)} px")
    with open(os.path.join(HERE, "beute-zahlen.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print("\n".join("BEUTE " + x for x in lines))
    print("BEUTE Durchdringung", cross)

    gap, lab = 8, 22
    tile_w, tile_h = 360, 300
    note_w = 420
    width = note_w + 6 * (tile_w + gap)
    top_h = 480
    y0 = 34 + lab + top_h + 14
    sheet = np.zeros((y0 + lab + 4 * (tile_h + gap) + 8, width, 3)) + S.FOG_COLOR
    B.text(sheet, "Silver & Ash - Throwing Knives als Beute am Boden (#819): heutiger Quader und drei Entwuerfe. Spielbild 854 x 480, Auge 1,6 m, "
           "Leuchtplatte in Stufenfarbe Vampiric; je Drehwinkel eine andere Stelle des Pulses.", 6, 6, 16)
    B.text(sheet, "Spielbild, Winkel 74 Grad: vorn 3 m, hinten 8 m; von links: Quader, a, b, c", 6, 34, 14)
    B.paste(sheet, frames[1], 0, 34 + lab)
    for i, (key, name, _b, _p) in enumerate(DESIGNS):
        x = 854 + gap + i * (340 + gap)
        B.text(sheet, f"{name}, schraeg von oben", x + 2, 34, 13)
        B.paste(sheet, views[key], x, 34 + lab)
    for c in range(6):
        dist, j = DISTS[c // 3], c % 3
        f = 3 if dist == 3.0 else 8
        B.text(sheet, f"{dist:.0f} m, Winkel {ANGLES[j]:.0f} Grad, Puls {GLOW[j]:.2f} ({f}x)", note_w + c * (tile_w + gap) + 2, y0, 13)
    for r, (key, _name, _b, _p) in enumerate(DESIGNS):
        y = y0 + lab + r * (tile_h + gap)
        for k, note in enumerate(notes[key]):
            B.text(sheet, note, 6, y + 6 + k * 20, 14 if k == 0 else 12)
        tiles = rows[key]                         # Reihenfolge: je Winkel (3 m, 8 m)
        for c in range(6):
            img = tiles[(c % 3) * 2 + c // 3]
            B.paste(sheet, img, note_w + c * (tile_w + gap), y)
    S.save_png(sheet[::-1], os.path.join(HERE, "beute-819.png"))
    print("BEUTE Blatt geschrieben:", sheet.shape[1], "x", sheet.shape[0])


if __name__ == "__main__":
    main()
