"""Entwurf A der Stake Crossbow (Issue #203): die schwere Eisen-Armbrust "Eisenwinde".

Breiter, gerader Eisenbogen mit nach hinten gebogenen Spitzen, kurzer dunkler Schaft mit Eisenbaendern, Winde (Walze mit
Kurbel) hinten, Pflock in einer Eisenrinne, Silberspitze. Erzeugt armbrust-a-eisen.glb neben diesem Skript.

Aufruf: "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python armbrust_a.py
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from armbrust_gemeinsam import (V, circle, sweep, beam, rod, disc_x, build_stake, build_string, new_parts,  # noqa: E402
                                base_painters, export_design)

FILE = "armbrust-a-eisen.glb"
Y_STAKE = 0.034


def build_a():
    body, stake = new_parts()
    # Schaft: kurz und dick, dunkles Holz; der Griff darunter ist leicht nach hinten geneigt
    sweep(body, [(0, 0.005, -0.078), (0, 0.006, -0.04), (0, 0.005, 0.06), (0, 0.004, 0.14)],
          [(0.019, 0.022), (0.025, 0.026), (0.027, 0.024), (0.021, 0.020)], "wood")
    sweep(body, [(0, 0.006, 0.004), (0, -0.04, -0.002), (0, -0.082, -0.014)], [(0.014, 0.013), (0.0165, 0.0150), (0.015, 0.014)], "grip")
    # Eisenbaender um den Schaft
    for z in (-0.050, 0.020, 0.100):
        beam(body, (0, 0.004, z - 0.007), (0, 0.004, z + 0.007), 0.0300, 0.0235, "iron")
    # Rinne fuer den Pflock: zwei Eisenschienen
    for s in (-1, 1):
        beam(body, (s * 0.0125, Y_STAKE + 0.001, -0.040), (s * 0.0125, Y_STAKE + 0.001, 0.145), 0.0040, 0.0105, "iron")
    # Bogen: gerader Eisenbalken, die Spitzen kippen nach hinten (gespannt)
    xs = [-0.215, -0.16, -0.08, 0.0, 0.08, 0.16, 0.215]
    zs = [0.140 - 0.045 * (x / 0.215) ** 2 for x in xs]
    depth = [0.0075, 0.0105, 0.0150, 0.0170, 0.0150, 0.0105, 0.0075]
    hgt = [0.0070, 0.0095, 0.0120, 0.0130, 0.0120, 0.0095, 0.0070]
    sweep(body, [(x, 0.020, z) for x, z in zip(xs, zs)], list(zip(depth, hgt)), "iron")
    # Bogenfassung vorn am Schaft (dunkel) und Spitzenkappen (Silber, helle Eisenstufen)
    beam(body, (0, 0.004, 0.118), (0, 0.004, 0.165), 0.0300, 0.0245, "dark", 0.0260, 0.0200)
    for s in (-1, 1):
        beam(body, (s * 0.205, 0.020, zs[0] + 0.004), (s * 0.228, 0.020, zs[0] + 0.008 - 0.0025), 0.0072, 0.0068, "silver")
    # Winde hinten: Walze quer, goldene Scheiben, Kurbel rechts
    disc_x(body, (0, 0.035, -0.060), 0.0150, 0.030, "dark")
    for s in (-1, 1):
        disc_x(body, (s * 0.032, 0.035, -0.060), 0.0185, 0.004, "gold")
    rod(body, (-0.040, 0.035, -0.060), (-0.040, 0.078, -0.072), 0.0045, "iron", sides=4, up=V(0, 0, 1))
    disc_x(body, (-0.040, 0.082, -0.073), 0.0080, 0.010, "grip")
    # Sehne
    build_string(body, [(-0.222, 0.020, zs[0] + 0.006), (0.222, 0.020, zs[-1] + 0.006)], (0, Y_STAKE, -0.026))
    # Pflock: helles Holz, Silberspitze
    build_stake(stake, Y_STAKE, -0.026, 0.268, 0.0095, 0.062, "silver", collar=(0.200, 0.0112, "iron"))
    return body, stake


if __name__ == "__main__":
    export_design("a", "A Eisenwinde", FILE, build_a, base_painters())
