"""Blatt: C | i, drei Szenen untereinander; dazu die mittlere Abweichung der linken Bildhaelfte (Gang) zwischen C und i."""
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "..", "..", "..", "tools", "models"))
from psx import load_png, save_png  # noqa: E402

SRC = {"C": os.path.join(HERE, "..", "aermel2", "fotos_c"), "i": os.path.join(HERE, "fotos_c")}
rows = []
for scene in ("room-corridor", "pistol-recoil", "blade-charged"):
    a, b = (np.asarray(load_png(os.path.join(SRC[k], scene + ".png")), float) for k in ("C", "i"))
    rows.append(np.concatenate([a, np.zeros((480, 6, 3)), b], axis=1))
    if scene == "room-corridor":
        print("Abweichung links unten (x < 427, untere 200 Zeilen):", round(float(np.abs(a[:200, :427] - b[:200, :427]).mean()), 2),
              "(Grauwerte 0..255; ohne Knoepfe und Naht erwartet: klein)")
gap = np.zeros((6, rows[0].shape[1], 3))
save_png(np.concatenate([rows[2], gap, rows[1], gap, rows[0]], axis=0), os.path.join(HERE, "blatt_C_i.png"))
