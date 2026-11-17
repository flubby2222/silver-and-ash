"""Blatt: j | k (Spielfotos mit lokal getestetem Unterarm-Regelwerk), drei Szenen untereinander."""
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "..", "..", "..", "tools", "models"))
from psx import load_png, save_png  # noqa: E402

SRC = [os.path.join(HERE, "..", "aermel9", "fotos_c"), os.path.join(HERE, "phasen_k")]
rows = []
for scene in ("room-corridor", "pistol-recoil", "blade-charged"):
    a, b = (np.asarray(load_png(os.path.join(d, scene + ".png")), float) for d in SRC)
    rows.append(np.concatenate([a, np.zeros((480, 6, 3)), b], axis=1))
gap = np.zeros((6, rows[0].shape[1], 3))
save_png(np.concatenate([rows[2], gap, rows[1], gap, rows[0]], axis=0), os.path.join(HERE, "blatt_j_k.png"))
