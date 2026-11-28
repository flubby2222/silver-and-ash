"""Entwurf b der Throwing Knives (Issue #204): "Kreuzblatt", ein einzelnes schweres Wurfmesser mit Parierstange.

Das aeltere Stueck: breite Blattklinge wie ein mittelalterlicher Dolch, geschwaerzt, Silberschneide, blutrotes Email in der
Hohlkehle; gerade Parierstange mit ausgestellten Enden und blutrotem Stein in der Mitte; kurzer Ledergriff; Scheibenknauf
mit blutrotem Email. Der Umriss ist ein Kreuz. In der Hand steht eines davon aus der Manschette (blatt.py).
Erzeugt messer-b-kreuzblatt.glb (ein Messer).

Aufruf: "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python messer_b.py
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from messer_gemeinsam import (V, PROF_DIAMOND, circle, sweep, disc_x, new_knife, export_design)  # noqa: E402

FILE = "messer-b-kreuzblatt.glb"


def build_b():
    k = new_knife()
    # Klinge: breites Blatt, groesste Breite im vorderen Drittel
    sweep(k, [(0, 0, -0.004), (0, 0, 0.030), (0, 0, 0.082), (0, 0, 0.128), (0, 0, 0.170)],
          [(0.0046, 0.0170), (0.0052, 0.0200), (0.0054, 0.0255), (0.0040, 0.0180), (0.0005, 0.0008)], "blade_b", PROF_DIAMOND)
    # Parierstange: gerade, Enden ausgestellt (Kreuzform)
    sweep(k, [(0, -0.050, -0.008), (0, -0.040, -0.008), (0, 0, -0.008), (0, 0.040, -0.008), (0, 0.050, -0.008)],
          [(0.0078, 0.0088), (0.0052, 0.0056), (0.0064, 0.0070), (0.0052, 0.0056), (0.0078, 0.0088)], "dark", up=V(0, 0, 1))
    # blutroter Stein in der Mitte der Parierstange, auf beiden Seiten sichtbar
    sweep(k, [(-0.0098, 0, -0.008), (0.0098, 0, -0.008)], [(0.0074, 0.0074)] * 2, "red", circle(4), up=V(0, 1, 0))
    # Griff: dunkles Leder, leicht bauchig
    sweep(k, [(0, 0, -0.066), (0, 0, -0.040), (0, 0, -0.012)], [(0.0066, 0.0074), (0.0078, 0.0090), (0.0068, 0.0078)], "leather",
          circle(6))
    # Scheibenknauf mit blutrotem Email
    disc_x(k, (0, 0, -0.078), 0.0150, 0.0058, "dark")
    disc_x(k, (0, 0, -0.078), 0.0088, 0.0072, "red")
    return k


if __name__ == "__main__":
    export_design("b", "b Kreuzblatt", FILE, build_b)
