"""Entwurf C der Stake Crossbow (Issue #203): die schlanke Jagdarmbrust "Schlanke Dohle".

Langer, schmaler Schaft, duenner Recurve-Bogen, dessen Spitzen nach vorn schwingen (grosses Sehnen-V), Messingeinlagen,
Silberbuegel (Steigbuegel) vorn, langer duenner Pflock mit Silberring. Erzeugt armbrust-c-jagd.glb neben diesem Skript.

Aufruf: "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python armbrust_c.py
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from armbrust_gemeinsam import (V, circle, sweep, beam, rod, disc_x, build_stake, build_string, new_parts,  # noqa: E402
                                base_painters, export_design, wood_painter, WOOD_DARK)

FILE = "armbrust-c-jagd.glb"
Y_STAKE = 0.029


def build_c():
    body, stake = new_parts()
    # Schaft: lang und schmal, hinten nach oben geschwungen (Schulterstueck), dunkles Holz
    sweep(body, [(0, 0.020, -0.100), (0, 0.010, -0.070), (0, 0.004, -0.020), (0, 0.003, 0.070), (0, 0.003, 0.160)],
          [(0.012, 0.026), (0.013, 0.021), (0.0125, 0.017), (0.0115, 0.015), (0.010, 0.013)], "wood")
    sweep(body, [(0, 0.003, 0.002), (0, -0.036, -0.003), (0, -0.074, -0.014)], [(0.0105, 0.010), (0.0125, 0.012), (0.0115, 0.011)], "grip")
    # Messingeinlagen: schmale Leisten oben und Ringe
    for z in (-0.040, 0.050, 0.120):
        beam(body, (0, 0.003, z - 0.004), (0, 0.003, z + 0.004), 0.0140, 0.0170, "gold", 0.0140, 0.0170)
    # Rinne: zwei schmale Leisten
    for s in (-1, 1):
        beam(body, (s * 0.0090, Y_STAKE - 0.001, -0.040), (s * 0.0090, Y_STAKE - 0.001, 0.165), 0.0028, 0.0070, "dark")
    # Bogen: duenn, Recurve, Spitzen nach vorn
    xs = [-0.180, -0.140, -0.090, 0.0, 0.090, 0.140, 0.180]
    zs = [0.150 + 0.040 * (abs(x) / 0.180) ** 3 - 0.012 * (abs(x) / 0.180) for x in xs]
    depth = [0.0050, 0.0065, 0.0085, 0.0105, 0.0085, 0.0065, 0.0050]
    hgt = [0.0055, 0.0070, 0.0085, 0.0095, 0.0085, 0.0070, 0.0055]
    sweep(body, [(x, 0.018, z) for x, z in zip(xs, zs)], list(zip(depth, hgt)), "dark")
    # Fassung am Schaft und Silberspitzen
    beam(body, (0, 0.004, 0.132), (0, 0.004, 0.172), 0.0170, 0.0145, "dark", 0.0135, 0.0115)
    for s in (-1, 1):
        sweep(body, [(s * 0.176, 0.018, zs[0] + 0.002), (s * 0.186, 0.018, zs[0] + 0.012), (s * 0.190, 0.018, zs[0] + 0.020)],
              [(0.0055, 0.0060), (0.0075, 0.0075), (0.0045, 0.0045)], "silver", circle(6, math.pi / 6))
    # Silberbuegel vorn: Schlaufe aus drei Staeben, unterhalb des Pflocks
    z0 = 0.185
    rod(body, (-0.022, 0.0, z0), (-0.024, -0.030, z0 + 0.012), 0.0045, "silver", sides=4, up=V(0, 0, 1))
    rod(body, (0.022, 0.0, z0), (0.024, -0.030, z0 + 0.012), 0.0045, "silver", sides=4, up=V(0, 0, 1))
    rod(body, (-0.027, -0.030, z0 + 0.012), (0.027, -0.030, z0 + 0.012), 0.0045, "silver", sides=4)
    beam(body, (0, 0.003, 0.160), (0, 0.003, z0 + 0.002), 0.0095, 0.0105, "silver", 0.0230, 0.0090)
    build_string(body, [(-0.190, 0.018, zs[0] + 0.020), (0.190, 0.018, zs[-1] + 0.020)], (0, Y_STAKE, -0.026), r=0.0020)
    # Pflock: lang und schlank, Silberring
    build_stake(stake, Y_STAKE, -0.026, 0.300, 0.0072, 0.060, "stake", collar=(0.230, 0.0088, "silver"))
    return body, stake


if __name__ == "__main__":
    export_design("c", "C Schlanke Dohle", FILE, build_c, base_painters(wood=wood_painter(WOOD_DARK, 241)))
