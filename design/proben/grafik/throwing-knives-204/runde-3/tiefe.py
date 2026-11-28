"""Throwing Knives (Issue #204), Runde 3: wie tief sitzt Entwurf e in der Manschette?

Urteil des Auftraggebers zu Runde 2: "E ist gut, das Modell soll aber etwas weiter in den Aermel ragen. Sieht grade aus,
als wuerde er alle drei messer am untersten Ende halten." Am Modell aendert sich nichts (../runde-2/messer-e-schwarzruecken.glb);
es wechselt nur, wie weit die Messer aus der Manschette stehen. Dieselbe Schleife wie in Runde 1 und 2 (../blatt.py).

Die Zahl ist der Abstand vom Klingenansatz zum Haltepunkt des Aermels im Skriptmass (Datei 1,8-fach): je kleiner, desto
tiefer steckt das Messer. Runde 2 hatte 70 mm.

Schreibt tiefen.png (die Ich-Sichten nebeneinander) und ansichten/<tiefe>-ich-sicht-854x480.png.
Aufruf: "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tiefe.py
"""

import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
import blatt as B  # noqa: E402
from messer_gemeinsam import bpy  # noqa: E402
from psx import FOG_COLOR, MATS, save_png  # noqa: E402

DEPTHS = [("r2", 0.070, "Runde 2: 70 mm"), ("t50", 0.050, "50 mm"), ("t40", 0.040, "40 mm"), ("t32", 0.032, "32 mm")]
GLB = "../runde-2/messer-e-schwarzruecken.glb"

B.HERE = HERE
B.DESIGNS = [(k, label, GLB, 196) for k, _g, label in DEPTHS]
B.POSES = {k: (44.0, 6.0, (-17.0, 0.0, 17.0)) for k, _g, _l in DEPTHS}
B.TIP = 0.117
B.TITLE = "Throwing Knives (Issue 204), Runde 3: Entwurf e, nur die Tiefe in der Manschette wechselt"

_held, _save = B.held, B.save_png
_shots = {}


def held(key, angle):
    B.GRIP = dict((k, g) for k, g, _l in DEPTHS)[key] * B.SCALE
    return _held(key, angle)


def save(img, path):
    name = os.path.basename(path)
    if name.endswith("-ich-sicht-854x480.png"):          # die Ich-Sicht je Tiefe fuer das Blatt merken
        _shots[name.split("-")[0]] = np.array(img[::-1])
    return _save(img, path)


B.held, B.save_png = held, save


def main():
    for entry in list(B.DESIGNS):                       # jede Tiefe einzeln in einer frischen Szene
        B.DESIGNS = [entry]
        bpy.ops.wm.read_factory_settings(use_empty=True)
        MATS.clear()
        B.HOME.clear()
        try:
            B.main()
        except ValueError:                               # das Blatt von Runde 1 erwartet zwei Spalten; die Bilder sind fertig
            pass
    lab = 24
    tiles = [(_shots[k], label) for k, _g, label in DEPTHS]
    h, w = tiles[0][0].shape[:2]
    sheet = np.zeros((2 * (h + lab) + lab, 2 * w + 8, 3)) + FOG_COLOR
    B.text(sheet, "Silver & Ash - Throwing Knives e, Abstand Klingenansatz bis Haltepunkt des Aermels (kleiner = tiefer im Aermel), 854 x 480", 6, 2, 15)
    for i, (img, label) in enumerate(tiles):
        x, y = (i % 2) * (w + 8), lab + (i // 2) * (h + lab)
        B.text(sheet, label, x + 6, y + 2, 15)
        B.paste(sheet, img, x, y + lab)
    save_png(sheet[::-1], os.path.join(HERE, "tiefen.png"))
    print("TIEFEN geschrieben:", sheet.shape[1], "x", sheet.shape[0])


if __name__ == "__main__":
    main()
