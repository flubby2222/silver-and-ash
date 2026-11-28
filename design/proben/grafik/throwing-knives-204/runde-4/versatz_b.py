"""Throwing Knives (Issue #204), Runde 4b: der Faecher mittig aus der Oeffnung der Manschette, ohne Durchstich.

Urteil der Leitung zu Runde 4: 3,5 mm nach links sind zwar frei von Durchstichen, aber in der Ich-Sicht sitzt das rechte
Messer weiter auf der rechten Ecke der Manschette. Gesucht: der Faecher kommt sichtbar aus der Oeffnung, nicht aus der
Kante. Mittel: Drehpunkt des Faechers naeher an den Klingenansatz (der Faecher wird im Aermel enger, vor der Manschette
bleibt er fast gleich) und kuerzere Bandenden (messer3b.py). Tiefe 40 mm, Neigung, Kippen und Faecherwinkel bleiben.

Schreibt versatz-b.png und ansichten/<variante>-*.png; Pruefung und Blattaufbau kommen aus versatz.py.
Aufruf: "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python versatz_b.py
"""

import os
import sys

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import versatz as R  # noqa: E402

# Abgetastet mit kurzen Baendern in 0,5-mm-Schritten: Drehpunkt 70 mm ist von 0 bis 13,5 mm Versatz frei von Durchstichen
# (75 mm: 0 bis 12; 85 mm: 1,5 bis 8,5). 8 mm setzt die drei Wicklungen mit gleichem Rand links und rechts in die Oeffnung.
VARIANTS = [("v035", 0.0035, "Runde 4: 3,5 mm nach links, Drehpunkt 85 mm, lange Baender"),
            ("vb", 0.008, "Runde 4b (Empfehlung): 8 mm nach links, Drehpunkt 70 mm, kurze Baender (messer-e-kurzband.glb)",
             {"pivot": 0.070, "model": "kurz"})]

if __name__ == "__main__":
    R.main(VARIANTS, "versatz-b.png", scan=False, title="Runde 4b: mittig aus der Oeffnung, ohne Durchstich")
