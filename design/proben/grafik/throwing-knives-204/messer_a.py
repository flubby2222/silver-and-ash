"""Entwurf a der Throwing Knives (Issue #204): "Aderlass", ein Faecher aus drei schlanken Wurfnadeln.

Spaetes 18. bis 19. Jahrhundert: aus einem Stueck geschmiedetes, schlankes Wurfstilett. Schmale Rautenklinge, geschwaerzt,
mit geschliffener Silberschneide; kleiner Bund; Griff mit blutroter Schnurwicklung; gedrechselter Knauf (Eichel) aus dunklem Eisen.
In der Hand stehen drei davon als Faecher aus der Manschette (blatt.py). Erzeugt messer-a-aderlass.glb (ein Messer).

Aufruf: "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python messer_a.py
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from messer_gemeinsam import (PROF_DIAMOND, circle, sweep, new_knife, export_design)  # noqa: E402

FILE = "messer-a-aderlass.glb"


def build_a():
    k = new_knife()
    # Klinge: schlanke Raute, leichte Taille hinter dem Ansatz, lange Spitze
    sweep(k, [(0, 0, -0.004), (0, 0, 0.022), (0, 0, 0.105), (0, 0, 0.150), (0, 0, 0.176)],
          [(0.0036, 0.0105), (0.0040, 0.0135), (0.0032, 0.0100), (0.0022, 0.0060), (0.0004, 0.0006)], "blade_a", PROF_DIAMOND)
    # Bund am Klingenansatz (helles Eisen: trennt Klinge und Griff auch klein)
    sweep(k, [(0, 0, -0.011), (0, 0, -0.001)], [(0.0080, 0.0100)] * 2, "silver", circle(8, 3.14159 / 8))
    # Griff mit blutroter Schnurwicklung, leicht bauchig
    sweep(k, [(0, 0, -0.074), (0, 0, -0.045), (0, 0, -0.009)], [(0.0058, 0.0062), (0.0070, 0.0078), (0.0060, 0.0066)], "wrap",
          circle(6))
    # gedrechselter Knauf: heller Bund, dunkle Eichel (wie an Stiletten des 18. Jahrhunderts)
    sweep(k, [(0, 0, -0.080), (0, 0, -0.072)], [(0.0074, 0.0080)] * 2, "silver", circle(8, 3.14159 / 8))
    sweep(k, [(0, 0, -0.108), (0, 0, -0.100), (0, 0, -0.090), (0, 0, -0.078)],
          [(0.0030, 0.0030), (0.0082, 0.0090), (0.0096, 0.0108), (0.0050, 0.0054)], "dark", circle(6))
    return k


if __name__ == "__main__":
    export_design("a", "a Aderlass", FILE, build_a)
