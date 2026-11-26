"""Entwurf B der Stake Crossbow (Issue #203): die Holzarmbrust mit Knochenbeschlag "Rippenbogen".

Heller Holzschaft mit Knochenringen (wie Wirbel), sichelfoermiger Holzbogen, dessen Spitzen weit nach hinten zum Schuetzen
zeigen, Knochenknaeufe als Sehnenhalter, Pflock mit verkohlter Spitze. Erzeugt armbrust-b-knochen.glb neben diesem Skript.

Aufruf: "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python armbrust_b.py
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from armbrust_gemeinsam import (V, circle, sweep, beam, rod, disc_x, build_stake, build_string, new_parts,  # noqa: E402
                                base_painters, export_design, wood_painter, WOOD_LIGHT, char_painter)

FILE = "armbrust-b-knochen.glb"
Y_STAKE = 0.033


def build_b():
    body, stake = new_parts()
    # Schaft: schlank vorn, nach hinten verdickt und leicht gesenkt (Schulterstueck), helles Holz
    sweep(body, [(0, 0.000, -0.090), (0, 0.003, -0.050), (0, 0.006, 0.0), (0, 0.007, 0.08), (0, 0.006, 0.150)],
          [(0.016, 0.020), (0.022, 0.024), (0.020, 0.022), (0.017, 0.019), (0.014, 0.016)], "wood")
    sweep(body, [(0, 0.004, 0.004), (0, -0.040, -0.004), (0, -0.078, -0.018)], [(0.013, 0.012), (0.0155, 0.0145), (0.0145, 0.014)], "grip")
    # Knochenring am Griffende (Schaedelknauf) und Knochenringe am Schaft
    sweep(body, [(0, -0.074, -0.020), (0, -0.088, -0.024), (0, -0.098, -0.028)], [(0.0155, 0.0150), (0.0200, 0.0180), (0.0100, 0.0090)],
          "bone", circle(8, math.pi / 8))
    for z, h in ((-0.052, 0.0285), (0.024, 0.0255), (0.098, 0.0225)):
        beam(body, (0, 0.005, z - 0.0055), (0, 0.005, z + 0.0055), h * 0.92, h, "bone")
    # Knochenleiste an der Seite (Backe)
    for s in (-1, 1):
        beam(body, (s * 0.0225, 0.008, -0.085), (s * 0.0225, 0.008, -0.030), 0.0030, 0.0120, "bone")
    # Pflockrinne: zwei flache Holzleisten
    for s in (-1, 1):
        beam(body, (s * 0.0115, Y_STAKE - 0.001, -0.030), (s * 0.0115, Y_STAKE - 0.001, 0.150), 0.0035, 0.0090, "wood")
    # Bogen: sichelfoermig, Spitzen weit nach hinten; in der Mitte dick, aussen duenn (geschichtetes Holz)
    xs = [-0.200, -0.150, -0.085, 0.0, 0.085, 0.150, 0.200]
    zs = [0.150 - 0.085 * (x / 0.200) ** 2 for x in xs]
    depth = [0.0070, 0.0095, 0.0140, 0.0175, 0.0140, 0.0095, 0.0070]
    hgt = [0.0075, 0.0100, 0.0130, 0.0150, 0.0130, 0.0100, 0.0075]
    sweep(body, [(x, 0.022, z) for x, z in zip(xs, zs)], list(zip(depth, hgt)), "wood")
    # Bogenfassung: Holzblock am Schaftende, Knochenknaeufe an den Spitzen
    beam(body, (0, 0.006, 0.128), (0, 0.006, 0.170), 0.0260, 0.0200, "wood", 0.0200, 0.0160)
    for s in (-1, 1):
        sweep(body, [(s * 0.200, 0.022, zs[0] - 0.006), (s * 0.208, 0.022, zs[0] - 0.012), (s * 0.214, 0.022, zs[0] - 0.016)],
              [(0.0075, 0.0085), (0.0110, 0.0110), (0.0070, 0.0075)], "bone", circle(8, math.pi / 8), up=V(0, 1, 0))
    build_string(body, [(-0.208, 0.022, zs[0] - 0.012), (0.208, 0.022, zs[-1] - 0.012)], (0, Y_STAKE, -0.026))
    # Pflock: helles Holz, verkohlte Spitze (Holz bleibt Holz)
    build_stake(stake, Y_STAKE, -0.026, 0.262, 0.0090, 0.058, "grip", collar=(0.178, 0.0105, "bone"))
    return body, stake


if __name__ == "__main__":
    export_design("b", "B Rippenbogen", FILE, build_b, base_painters(wood=wood_painter(WOOD_LIGHT, 221), grip=char_painter))
