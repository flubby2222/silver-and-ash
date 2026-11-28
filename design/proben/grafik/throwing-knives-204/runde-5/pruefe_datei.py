"""Throwing Knives (Issue #204), Runde 5: Pruefung der abgenommenen Haltung s12 an der geschriebenen Datei.

Laedt messer-e-hand-s12.glb (von handmodell.py) dreimal, stellt die Haltung s12 ein und wiederholt die Messungen aus
runde5.py: Dreieckspruefung gegen den Aermel (Regel 30), Sicht hinter dem Deckel, Messer gegen Messer, Ueberdeckung
im Bild, dazu das freie Fenster des Versatzes in 1-mm-Schritten. Schreibt datei-zahlen.txt und
ansichten/datei-s12-*.png; bricht ab, wenn die Haltung einen Durchstich oder eine Ueberdeckung im Bild hat.
Aufruf: "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python pruefe_datei.py
"""

import os
import sys

import numpy as np

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import staffel as S  # noqa: E402
from runde5 import CHECKS, STEP, total  # noqa: E402

B, G = S.B, S.G
DEPTH, PIVOT, DX = 0.012, 0.062, 0.008           # Haltung s12


def main():
    out = os.path.join(HERE, "ansichten")
    s = S.setup(knife=("messer-e-hand-s12.glb", HERE))
    tris = 0
    for o in s.knives[0]:
        o.data.calc_loop_triangles()
        tris += len(o.data.loop_triangles)
    free = []
    for dx in range(0, 15):
        S.put(s, DEPTH, PIVOT, dx / 1000, thick=STEP)
        if total(S.pierce(s)) == 0 and total(S.seen(s)) == 0:
            free.append(dx)
    S.put(s, DEPTH, PIVOT, DX, thick=STEP)
    p, v, c, r = S.pierce(s), S.seen(s), S.crossing(s), S.overlap(s)
    lines = ["Throwing Knives e, Haltung s12, gemessen an der Datei messer-e-hand-s12.glb (dreimal geladen)",
             f"Dreiecke je Messer: {tris}",
             "Haltung: Tiefe 12 mm, Drehpunkt 62 mm, Versatz 8 mm, Staffel 0/6/12 mm, Faecher +-17 Grad, Neigung 44, Kippen 6",
             f"Dreieckspaare gegen den Aermel (ohne Deckel), rechts/Mitte/links: {[n for n, _ in p]}",
             f"sichtbare Punkte hinter dem Deckel: {[n for n, _ in v]}",
             f"Dreieckspaare Messer gegen Messer (rechts-Mitte, Mitte-links, rechts-links): {list(c)} (hinter dem Deckel)",
             "Bildzeilen mit Ueberdeckung benachbarter Messer: " + (f"{len(r['rows'])}, bis {r['width']} px" if r["rows"] else "0"),
             f"Bildpunkte je Messer: {r['visible']}",
             "Wicklung sichtbar/ueberdeckt px: " + ", ".join(f"{a}/{b}" for a, b, _n in r["wrap"]),
             f"Versatz ohne Durchstich und ohne sichtbaren Punkt (mm, 1-mm-Schritte): {free}"]
    shown = [o for g in s.knives for o in g]
    eye = np.array(B.player(list(s.room) + shown + s.sleeve + s.blade, s.cam))
    S.save_png(eye[::-1], os.path.join(out, "datei-s12-ich-sicht-854x480.png"))
    for name, d, aim, fov in CHECKS:
        img = np.array(B.shot(shown + s.sleeve, s.cam, G(s.hand + d), G(s.hand + aim), fov, (427, 240), "ansicht"))
        S.save_png(img[::-1], os.path.join(out, f"datei-s12-{name}.png"))
    with open(os.path.join(HERE, "datei-zahlen.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print("\n".join("DATEI " + x for x in lines))
    assert total(p) == 0 and total(v) == 0 and not r["rows"], "Haltung s12 mit der Datei nicht frei"


if __name__ == "__main__":
    main()
