"""Throwing Knives (Issue #204), Runde 4b: Entwurf e mit kuerzeren Bandenden.

Dasselbe Messer wie ../runde-2/messer3.py (Blatt, Angel, Wicklung, Ring, Knoten und Textur unveraendert); nur die zwei
Bandenden hinter dem Knoten sind kuerzer: das lange endet bei 97 statt 110 mm hinter dem Klingenansatz, das kurze bei 92
statt 98 mm. Grund: Mit den langen Enden sticht das Band des rechten Messers durch die Aermelwand, sobald der Faecher
weiter als 5,5 mm nach links rueckt (versatz.py).

Schreibt messer-e-kurzband.glb und textur3b.png (gleicher Inhalt wie ../runde-2/textur3.png).
Aufruf: "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python messer3b.py
"""

import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "runde-2"))
import messer3 as M  # noqa: E402

KNOT = -0.084                       # hier beginnen die Bandenden
LONG, SHORT = -0.097, -0.092        # neue Enden (Runde 2: -0.110 und -0.098)

_sweep = M.sweep


def sweep(k, path, sizes, region, *more):
    """Wie in messer3.py, nur die Bandenden werden laengs gestaucht (Form und Texturfeld bleiben)."""
    if region == "band" and path[0][2] == KNOT:
        f = ((LONG if path[-1][2] < -0.100 else SHORT) - KNOT) / (path[-1][2] - KNOT)
        path = [(x, y, KNOT + (z - KNOT) * f) for x, y, z in path]
    return _sweep(k, path, sizes, region, *more)


def main():
    M.sweep = sweep
    M.HERE = HERE
    M.export()
    os.replace(os.path.join(HERE, "messer-e-schwarzruecken.glb"), os.path.join(HERE, "messer-e-kurzband.glb"))
    os.replace(os.path.join(HERE, "textur3.png"), os.path.join(HERE, "textur3b.png"))
    print("KURZBAND geschrieben")


if __name__ == "__main__":
    main()
