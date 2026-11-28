"""Throwing Knives (Issue #204), nach Runde 4b: Abstand der drei Griffe am Rand der Manschette in Bildpunkten.

Je Variante wird jedes Messer einzeln in der Ich-Sicht 854 x 480 gerendert (Maske = Unterschied zum leeren Gang), dazu
der Aermel. Sichtbar ist, was nicht vom Aermel verdeckt wird. Gemessen wird in den untersten 12 Bildzeilen, in denen
alle drei Messer sichtbar sind (das ist der Streifen direkt ueber dem Manschettenrand): die waagrechte Luecke zwischen
rechtem und mittlerem sowie mittlerem und linkem Messer. 0 heisst: die Griffe stossen im Bild aneinander.

Schreibt nur auf die Konsole (Zeilen mit ABSTAND).
Aufruf: "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python wicklung_abstand.py
"""

import os
import sys
import tempfile

import numpy as np

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import versatz as R  # noqa: E402
import versatz_b  # noqa: E402

B, MATS, MODELS, SCALE, bpy = R.B, R.MATS, R.MODELS, R.SCALE, R.bpy


def main():
    stone = os.path.join(tempfile.gettempdir(), "messer_stein")
    os.makedirs(stone, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    B.HOME.clear()
    B.POSES = {"e": R.POSE}
    B.GRIP = R.DEPTH * SCALE
    models = {"lang": B.copies(B.load("messer-e-schwarzruecken.glb", "e", R.GLB), 3),
              "kurz": B.copies(B.load("messer-e-kurzband.glb", "ek", HERE), 3)}
    sleeve = B.load("pistol_hand.glb", "pistolh", MODELS, smooth=True)
    cam = B.camera()
    room = B.build_room(B.stone_textures(stone))
    for group in models.values():
        B.place([o for g in group for o in g], B.AWAY)
    B.place(sleeve, B.at(B.HAND))
    empty = np.array(B.player(list(room), cam))
    arm = np.abs(np.array(B.player(list(room) + sleeve, cam)) - empty).sum(2) > 1e-4
    for v in versatz_b.VARIANTS:
        key, dx, _label = v[:3]
        opt = v[3] if len(v) > 3 else {}
        knives = models[opt.get("model", "lang")]
        R.put(knives, dx, opt.get("pivot", R.PIVOT))
        masks = []
        for group in knives:                                     # Reihenfolge: rechts, Mitte, links im Bild
            m = np.abs(np.array(B.player(list(room) + group, cam)) - empty).sum(2) > 1e-4
            masks.append(m & ~arm)
        every = [y for y in range(masks[0].shape[0]) if all(m[y].any() for m in masks)]
        rows = every[-12:]
        over = [y for y in every if min(np.flatnonzero(masks[0][y]).min() - np.flatnonzero(masks[1][y]).max(),
                                        np.flatnonzero(masks[1][y]).min() - np.flatnonzero(masks[2][y]).max()) <= 0]
        print("ABSTAND", key, "Zeilen, in denen sich zwei Messer im Bild ueberdecken:",
              f"{min(over)} bis {max(over)} ({len(over)} Zeilen)" if over else "keine")
        gaps = []
        for y in rows:
            xs = [np.flatnonzero(m[y]) for m in masks]
            gaps.append((y, int(xs[0].min() - xs[1].max() - 1), int(xs[1].min() - xs[2].max() - 1)))
        print("ABSTAND", key, "Zeilen", rows[0], "bis", rows[-1], "| Luecke rechts-Mitte, Mitte-links je Zeile:",
              [(a, b) for _y, a, b in gaps], "| kleinste", min(min(a, b) for _y, a, b in gaps),
              "| Mittel", round(float(np.mean([[a, b] for _y, a, b in gaps])), 1))
        B.place([o for g in knives for o in g], B.AWAY)


if __name__ == "__main__":
    main()
