"""Blatt: C | g, drei Szenen untereinander (Blender-Python, nur Probebilder)."""
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "..", "..", "..", "tools", "models"))
from psx import load_png, save_png  # noqa: E402

SRC = {"C": os.path.join(HERE, "..", "aermel2", "fotos_c"), "g": os.path.join(HERE, "fotos_g")}
rows = []
for scene in ("room-corridor", "pistol-recoil", "blade-charged"):
    a, b = (np.asarray(load_png(os.path.join(SRC[k], scene + ".png")), float) for k in ("C", "g"))
    rows.append(np.concatenate([a, np.zeros((480, 6, 3)), b], axis=1))
gap = np.zeros((6, rows[0].shape[1], 3))
save_png(np.concatenate([rows[2], gap, rows[1], gap, rows[0]], axis=0), os.path.join(HERE, "blatt_C_g.png"))
