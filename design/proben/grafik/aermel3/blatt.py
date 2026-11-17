"""Blatt: heutiger Stand (links) und Entwurf d mit angepasster Haltung (rechts), drei Szenen untereinander."""
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "..", "..", "..", "tools", "models"))
from psx import load_png, save_png  # noqa: E402

rows = []
for scene in ("room-corridor", "pistol-recoil", "blade-charged"):
    a = np.asarray(load_png(os.path.join(HERE, "fotos_heute", scene + ".png")), float)
    b = np.asarray(load_png(os.path.join(HERE, "fotos_d", scene + ".png")), float)
    rows.append(np.concatenate([a, np.zeros((a.shape[0], 6, 3)), b], axis=1))
gap = np.zeros((6, rows[0].shape[1], 3))
save_png(np.concatenate([rows[2], gap, rows[1], gap, rows[0]], axis=0), os.path.join(HERE, "blatt_heute_und_d.png"))
