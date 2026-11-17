"""Blatt: C | g | h, drei Szenen untereinander (Blender-Python, nur Probebilder)."""
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "..", "..", "..", "tools", "models"))
from psx import load_png, save_png  # noqa: E402

SRC = [os.path.join(HERE, "..", "aermel2", "fotos_c"), os.path.join(HERE, "..", "aermel6", "fotos_g"), os.path.join(HERE, "fotos_h")]
rows = []
for scene in ("room-corridor", "pistol-recoil", "blade-charged"):
    tiles = []
    for d in SRC:
        tiles += [np.asarray(load_png(os.path.join(d, scene + ".png")), float), np.zeros((480, 6, 3))]
    rows.append(np.concatenate(tiles[:-1], axis=1))
gap = np.zeros((6, rows[0].shape[1], 3))
save_png(np.concatenate([rows[2], gap, rows[1], gap, rows[0]], axis=0), os.path.join(HERE, "blatt_C_g_h.png"))
