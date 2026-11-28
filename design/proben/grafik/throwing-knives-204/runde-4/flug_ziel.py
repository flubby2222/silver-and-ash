"""Throwing Knives (Issue #204), nach Runde 4b: Flug und Ziel mit kurzem Band neben dem langen Band.

Der Auftraggeber hat 4b abgenommen (8 mm, Drehpunkt 70 mm, kurze Baender). Das Band war im Flug und im Ziel als Lesehilfe
gedacht; dieses Blatt zeigt, ob das kurze Band (messer-e-kurzband.glb) dort etwas verliert. Links das lange Band aus
Runde 2 (../runde-2/messer-e-schwarzruecken.glb), rechts das kurze. Kamera, Licht, Gang, Ghul und Lagen wie in
../blatt.py (Runde 2): Flug aus Sicht des Werfers in 1,5, 3 und 5 m, quer in 2, 4 und 8 m, im Ghul aus 3,5 m.
Dazu die Zahl roter Pixel je Spielbild (r >= 70 und r > 1,8 x g und b; im Gang ist sonst nichts rot), damit der Vergleich
eine Zahl hat.

Schreibt flug-ziel-kurzband.png und ansichten/<lang|kurz>-*.png.
Aufruf: "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python flug_ziel.py
"""

import os
import sys
import tempfile

import numpy as np

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
import blatt as B  # noqa: E402
from messer_gemeinsam import MODELS, SCALE, G, Matrix, V, Vector, bpy  # noqa: E402
from psx import FOG_COLOR, MATS, save_png  # noqa: E402

TIP = 0.117
DESIGNS = [("lang", "langes Band (Runde 2)", "messer-e-schwarzruecken.glb", os.path.join(HERE, "..", "runde-2")),
           ("kurz", "kurzes Band (Runde 4b)", "messer-e-kurzband.glb", HERE)]


def red(img):
    a = np.asarray(img) * (255.0 if np.asarray(img).max() <= 1.5 else 1.0)
    return int(((a[..., 0] >= 70) & (a[..., 0] > 1.8 * np.maximum(a[..., 1], a[..., 2]))).sum())


def main():
    out = os.path.join(HERE, "ansichten")
    stone = os.path.join(tempfile.gettempdir(), "messer_stein")
    os.makedirs(stone, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    B.HOME.clear()
    knives = {k: B.copies(B.load(f, k, base), 3) for k, _l, f, base in DESIGNS}
    ghoul = B.load("ghoul.glb", "ghoul", MODELS, smooth=True)
    cam = B.camera()
    room = B.build_room(B.stone_textures(stone))
    B.place([o for group in knives.values() for g in group for o in g] + ghoul, B.AWAY)
    tiles, notes = {}, {}
    for k, _label, _f, _b in DESIGNS:
        one = knives[k][0]
        flying = [o for g in knives[k] for o in g]
        n = {}
        # einzelnes Messer von der Seite und von hinten
        B.place(one, Matrix())
        for name, d, dist, size in (("seite", V(-1, 0, 0), 0.47, (640, 250)), ("hinten", V(0.25, 0.25, -1), 0.56, (212, 212))):
            img = B.shot(one, cam, G(B.CENTRE + d.normalized() * dist), G(B.CENTRE), 33, size, "ansicht")
            tiles[(k, name)] = img
            save_png(img[::-1], os.path.join(out, f"{k}-{name}.png"))
        B.place(one, B.AWAY)
        # steckt im Ziel
        B.place(ghoul, B.world((0, 0, B.TARGET_Z)))
        top = max((o.matrix_world @ Vector(c)).z for o in ghoul for c in o.bound_box)
        hit = B.hit_point(ghoul, 0.07, top * 0.70)
        m = B.thrown(hit, B.STUCK_PITCH, B.STUCK_YAW)
        tip = m @ Vector((0, -TIP * SCALE, 0)) - m @ Vector((0, 0, 0))
        B.place(one, Matrix.Translation(tip.normalized() * B.STUCK_DEPTH - tip) @ m)
        img = B.player(list(room) + ghoul + one, cam)
        n["steckt"] = red(img)
        save_png(img[::-1], os.path.join(out, f"{k}-steckt-854x480.png"))
        tiles[(k, "steckt")] = B.up_n(B.crop(img, B.pixel(hit + V(0, 0.02, 0)), 106, 120), 3)
        near = B.shot(list(room) + ghoul + one, cam, G(hit + V(-0.62, 0.16, -0.72)), G(hit + V(0, 0, -0.05)), 40, (318, 360), "spiel")
        tiles[(k, "nah")] = near
        save_png(near[::-1], os.path.join(out, f"{k}-steckt-nah.png"))
        B.place(ghoul + one, B.AWAY)
        # Flug von hinten: 1,5 m, 3 m, 5 m
        for group, (dist, spin, dx, dy) in zip(knives[k], ((1.5, 35.0, -0.10, -0.16), (3.0, 150.0, -0.05, -0.12), (5.0, 275.0, 0.0, -0.10))):
            B.place(group, B.thrown(B.EYE + V(dx, dy, dist), spin=spin))
        img = B.player(list(room) + flying, cam)
        n["flug"] = red(img)
        save_png(img[::-1], os.path.join(out, f"{k}-flug-854x480.png"))
        tiles[(k, "flug")] = B.up_n(B.crop(img, (440, 264), 134, 120), 3)
        # quer in 2, 4, 8 m
        for group, (dist, dx) in zip(knives[k], ((2.0, 0.30), (4.0, 0.0), (8.0, -0.55))):
            B.place(group, B.thrown(B.EYE + V(dx, -0.12, dist), pitch=-35.0, yaw=90.0))
        img = B.player(list(room) + flying, cam)
        n["klein"] = red(img)
        save_png(img[::-1], os.path.join(out, f"{k}-klein-854x480.png"))
        tiles[(k, "klein")] = B.up_n(B.crop(img, (B.GAME[0] // 2 - 16, B.GAME[1] // 2 + 22), 118, 180), 2)
        B.place(flying, B.AWAY)
        notes[k] = n
        print("FLUGZIEL", k, "blutrote Pixel in 854 x 480:", n)

    W, gap, lab = 640, 12, 26
    cw = W + gap
    rows_h = [360, 360, 250]
    sheet = np.zeros((34 + sum(h + lab + gap for h in rows_h) + 4, 2 * cw - gap, 3)) + FOG_COLOR
    B.text(sheet, "Silver & Ash - Throwing Knives e: Flug und Ziel, langes Band (Runde 2) gegen kurzes Band (Runde 4b)", 6, 4, 18)
    ys, y = [], 34
    for h in rows_h:
        ys.append(y)
        y += h + lab + gap
    for c, (k, label, _f, _b) in enumerate(DESIGNS):
        x0, n = c * cw, notes[k]
        B.text(sheet, f"{label}: Flug aus Sicht des Werfers (dreifach, rot {n['flug']} px) / quer 2, 4, 8 m (zweifach, rot {n['klein']} px)",
               x0 + 4, ys[0], 13)
        B.paste(sheet, tiles[(k, "flug")], x0, ys[0] + lab)
        B.paste(sheet, tiles[(k, "klein")], x0 + 404, ys[0] + lab)
        B.text(sheet, f"{label}: im Ziel aus 3,5 m (dreifach, rot {n['steckt']} px) / von der Seite", x0 + 4, ys[1], 13)
        B.paste(sheet, tiles[(k, "steckt")], x0, ys[1] + lab)
        B.paste(sheet, tiles[(k, "nah")], x0 + 322, ys[1] + lab)
        B.text(sheet, f"{label}: einzelnes Messer von der Seite, klein daneben von hinten", x0 + 4, ys[2], 13)
        B.paste(sheet, tiles[(k, "seite")], x0, ys[2] + lab)
        B.paste(sheet, tiles[(k, "hinten")][:212, :212], x0 + W - 212, ys[2] + lab)
    save_png(sheet[::-1], os.path.join(HERE, "flug-ziel-kurzband.png"))
    print("FLUGZIEL Blatt geschrieben:", sheet.shape[1], "x", sheet.shape[0])


if __name__ == "__main__":
    main()
