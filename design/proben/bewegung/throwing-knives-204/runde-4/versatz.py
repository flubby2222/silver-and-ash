"""Throwing Knives (Issue #204), Runde 4: Versatz zur Seite, damit nichts durch den Aermel sticht.

Urteil des Auftraggebers zu Runde 3 (2026-11-28): "40mm und etwar 25 mm nach links, damit das Modell nicht in den Ärmel
clipt". Am Modell aendert sich nichts (../runde-2/messer-e-schwarzruecken.glb), die Tiefe bleibt 40 mm; es wechselt nur,
wie weit die drei Messer gegen den Aermel nach links (im Bild, Spielachse +x) geschoben sind.

Gemessen wird nicht am Bild, sondern an der Geometrie: Jedes Messer wird gegen jedes Dreieck des Aermels
(assets/models/pistol_hand.glb, nur gelesen) auf Ueberschneidung geprueft. Der Deckel der Manschette (die Oeffnung, durch
die der Griff in den Aermel geht) zaehlt nicht; jedes andere Dreieck ist ein Durchstich. Dazu zwei Bilder aus anderer
Kamera, damit ein Durchstich nicht nur zufaellig verdeckt ist.

Masse: Versatz und Tiefe im Skriptmass (Datei und Ich-Sicht 1,8-fach), wie die 40 mm aus Runde 3.

Schreibt versatz.png (Blatt), ansichten/<variante>-ich-sicht-854x480.png, ansichten/<variante>-pruef-*.png und
versatz-zahlen.txt (Abtastung in 1-mm-Schritten).
Aufruf: "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python versatz.py
"""

import os
import sys
import tempfile

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
import blatt as B  # noqa: E402
from messer_gemeinsam import MODELS, SCALE, G, Matrix, V, Vector, bpy  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402
from psx import FOG_COLOR, MATS, save_png  # noqa: E402

GLB = os.path.join(HERE, "..", "runde-2")
DEPTH = 0.040                                    # Runde 3, vom Auftraggeber gewaehlt
POSE = (44.0, 6.0, (-17.0, 0.0, 17.0))           # Neigung, Kippen, Faecher: unveraendert
PIVOT = 0.085                                    # Drehpunkt des Faechers hinter dem Klingenansatz (Runde 1 bis 3)
# (Schluessel, Versatz nach links in m Skriptmass, Beschriftung[, {"pivot": m, "model": "kurz"}])
VARIANTS = [("v00", 0.000, "40 mm, ohne Versatz (Runde 3)"),
            ("v035", 0.0035, "40 mm, 3,5 mm nach links (Empfehlung: Mitte des freien Fensters 1,5 bis 5,5 mm)"),
            ("v25", 0.025, "40 mm, 25 mm nach links (wie bestellt)")]
SCAN = [i / 4000 for i in range(0, 41)] + [i / 1000 for i in range(11, 31)]   # 0..10 mm in Viertelschritten, dann 1 mm
# Teile des Messers laengs (m Skriptmass ab Klingenansatz, zur Spitze positiv), fuer die Meldung
PARTS = [(-0.012, "Klinge/Ansatz"), (-0.054, "Wicklung"), (-0.082, "Ring"), (-9.0, "Band")]


def tris(o):
    m = o.matrix_world
    o.data.calc_loop_triangles()
    return [m @ v.co for v in o.data.vertices], [tuple(t.vertices) for t in o.data.loop_triangles]


def lid_of(o, hand):
    """Deckel der Manschette: die Dreiecke, die nach vorn zeigen und am Haltepunkt liegen (dort tritt der Griff ein)."""
    vs, ts = tris(o)
    out = set()
    for i, t in enumerate(ts):
        a, b, c = (vs[j] for j in t)
        n = (b - a).cross(c - a).normalized()
        if abs(n.dot(G(V(0, 0, 1)))) > 0.9 and all(abs((p - hand).dot(G(V(0, 0, 1)))) < 0.01 for p in (a, b, c)):
            out.add(i)
    return out


def part(along):
    return next(name for limit, name in PARTS if along >= limit)


def pierce(knives, walls):
    """Je Messer: Zahl der Dreieckspaare Messer x Aermelwand und welche Teile des Messers betroffen sind."""
    out = []
    for group in knives:
        pairs, parts = 0, set()
        for o in group:
            vs, ts = tris(o)
            tree = BVHTree.FromPolygons(vs, ts)
            inv = o.matrix_world.inverted()
            for wall, lid in walls:
                for a, b in tree.overlap(wall):
                    if b in lid:
                        continue
                    pairs += 1
                    c = inv @ (sum((vs[i] for i in ts[a]), Vector()) / 3)
                    parts.add(part(-c.y / SCALE))
        out.append((pairs, sorted(parts)))
    return out


def put(knives, dx, pivot=PIVOT):
    B.FAN_PIVOT = pivot * SCALE
    for group, angle in zip(knives, POSE[2]):
        B.place(group, Matrix.Translation(G(V(dx * SCALE, 0, 0))) @ B.held("e", angle))


def frame_and_gap(knives, blade):
    """Bildrahmen der sichtbaren Teile (Klinge und Ansatz) in Pixeln und kleinster Abstand zur Silver Blade in mm (Spielmass)."""
    trees = [BVHTree.FromPolygons(*tris(o)) for o in blade]
    px, gap = [], 9.0
    for group in knives:
        for o in group:
            for v in o.data.vertices:
                if -v.co.y / SCALE < -0.012:
                    continue
                w = o.matrix_world @ v.co
                px.append(B.pixel((w.x, w.z, -w.y)))
                gap = min(gap, min(t.find_nearest(w)[3] for t in trees))
    a = np.array(px)
    return a.min(0), a.max(0), gap * 1000


def main(variants=None, sheet_name="versatz.png", scan=True, title="Runde 4: Versatz nach links gegen den Aermel"):
    variants = [tuple(v) + ({},) * (4 - len(v)) for v in (variants or VARIANTS)]
    out = os.path.join(HERE, "ansichten")
    os.makedirs(out, exist_ok=True)
    stone = os.path.join(tempfile.gettempdir(), "messer_stein")
    os.makedirs(stone, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    B.HOME.clear()
    B.POSES = {"e": POSE}
    B.GRIP = DEPTH * SCALE
    models = {"lang": B.copies(B.load("messer-e-schwarzruecken.glb", "e", GLB), 3)}
    if any(o.get("model") == "kurz" for _k, _d, _l, o in variants):          # messer3b.py: kuerzere Bandenden
        models["kurz"] = B.copies(B.load("messer-e-kurzband.glb", "ek", HERE), 3)
    for group in models.values():
        B.place([o for g in group for o in g], B.AWAY)
    knives = models["lang"]
    sleeve = B.load("pistol_hand.glb", "pistolh", MODELS, smooth=True)
    blade = B.load("blade.glb", "blade", MODELS, smooth=True) + B.load("blade_hand.glb", "bladeh", MODELS, smooth=True)
    cam = B.camera()
    room = B.build_room(B.stone_textures(stone))
    B.place(sleeve, B.at(B.HAND))
    B.place(blade, B.at(B.LEFT) @ B.rot(-20, "Y"))
    hand = B.EYE + V(*B.HAND)
    walls = []
    for o in sleeve:
        lid = lid_of(o, G(hand)) if o.name.startswith("arm_cuff") else set()
        walls.append((BVHTree.FromPolygons(*tris(o)), lid))
    print("VERSATZ Deckel-Dreiecke:", [len(lid) for _w, lid in walls])

    lines = ["Throwing Knives e, Tiefe 40 mm: Durchstiche durch die Aermelwand je Versatz nach links (Skriptmass)",
             "Spalten: Versatz mm | Dreieckspaare rechtes, mittleres, linkes Messer (im Bild) | betroffene Teile", ""]
    for dx in SCAN if scan else []:
        put(knives, dx)
        r = pierce(knives, walls)
        lines.append(f"{dx * 1000:5.2f} | {r[0][0]:3d} {r[1][0]:3d} {r[2][0]:3d} | "
                     + "; ".join(f"{n}: {', '.join(p)}" for n, (c, p) in zip(("rechts", "Mitte", "links"), r) if c))
    if scan:
        with open(os.path.join(HERE, "versatz-zahlen.txt"), "w", encoding="utf-8", newline="\n") as f:
            f.write("\n".join(lines) + "\n")
        print("\n".join("VERSATZ " + s for s in lines))

    # Pruefkameras: auf die rechte Wand des Kragens (dort sticht Runde 3 durch) und auf die linke Unterseite des Aermels
    checks = [("pruef-rechts-oben", V(-0.30, 0.26, 0.22), V(-0.02, -0.01, -0.01), 22),
              ("pruef-links-unten", V(0.50, -0.22, -0.02), V(0, -0.07, -0.07), 26)]
    empty = np.array(B.player(list(room), cam))
    only_blade = np.abs(np.array(B.player(list(room) + blade, cam)) - empty).sum(2) > 1e-4
    tiles = []
    for key, dx, label, opt in variants:
        B.place([o for g in knives for o in g], B.AWAY)
        knives = models[opt.get("model", "lang")]
        shown = [o for g in knives for o in g]
        put(knives, dx, opt.get("pivot", PIVOT))
        r = pierce(knives, walls)
        lo, hi, gap = frame_and_gap(knives, blade)
        total = sum(c for c, _p in r)
        note = (f"Durchstiche: {total}" + ("" if not total else " (" + "; ".join(
            f"{n}: {', '.join(p)}" for n, (c, p) in zip(("rechts", "Mitte", "links"), r) if c) + ")")
            + f" | Faecher im Bild x {lo[0]}..{hi[0]}, y {lo[1]}..{hi[1]} | Abstand zur Silver Blade {gap:.0f} mm")
        print("VERSATZ", key, label, "|", note)
        only_knives = np.abs(np.array(B.player(list(room) + shown, cam)) - empty).sum(2) > 1e-4
        both = int((only_knives & only_blade).sum())
        note += f", im Bild {'frei' if not both else str(both) + ' Pixel ueberdeckt'}"
        print("VERSATZ", key, "Ueberdeckung mit der Silver Blade im Bild:", both, "Pixel")
        eye = np.array(B.player(list(room) + shown + sleeve + blade, cam))
        save_png(eye[::-1], os.path.join(out, f"{key}-ich-sicht-854x480.png"))
        extra = []
        for name, d, aim, fov in checks:
            img = np.array(B.shot(shown + sleeve, cam, G(hand + d), G(hand + aim), fov, (427, 240), "ansicht"))
            save_png(img[::-1], os.path.join(out, f"{key}-{name}.png"))
            extra.append(img)
        tiles.append((label, note, eye, extra))

    lab, gapx = 22, 8
    w, h = 854, 480
    sheet = np.zeros((lab + len(tiles) * (h + 2 * lab + 6), w + 2 * gapx + 427 + 426, 3)) + FOG_COLOR
    B.text(sheet, f"Silver & Ash - Throwing Knives e, {title}. Links Ich-Sicht 854 x 480, "
           "Mitte Manschette dreifach, rechts Pruefkameras (von rechts oben, von links unten)", 6, 2, 14)
    for i, (label, note, eye, extra) in enumerate(tiles):
        y = lab + i * (h + 2 * lab + 6)
        B.text(sheet, label, 6, y + 2, 15)
        B.text(sheet, note, 6, y + lab + h + 2, 13)
        B.paste(sheet, eye, 0, y + lab)
        for j, img in enumerate(extra):
            B.paste(sheet, img, w + 2 * gapx + 426, y + lab + j * 240)
        B.paste(sheet, B.up_n(B.crop(eye, (575, 400), 142, 160), 3), w + gapx, y + lab)
    save_png(sheet[::-1], os.path.join(HERE, sheet_name))
    print("VERSATZ Blatt geschrieben:", sheet.shape[1], "x", sheet.shape[0])


if __name__ == "__main__":
    main()
