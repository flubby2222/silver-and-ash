"""Blatt der Schlagphasen eines Ordners: zwei Spalten, vier Zeilen (w0 w05 / w1 s025 / s05 s075 / s1 leer), ganze Bilder."""
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "..", "..", "..", "tools", "models"))
from psx import load_png, save_png  # noqa: E402

folder, outname = sys.argv[-2], sys.argv[-1]
names = ["w0", "w05", "w1", "s025", "s05", "s075", "s1"]
tiles = [np.asarray(load_png(os.path.join(HERE, "..", folder, f"blade_{n}.png")), float) for n in names] + [np.zeros((480, 854, 3))]
rows = [np.concatenate([tiles[2 * i], np.zeros((480, 6, 3)), tiles[2 * i + 1]], axis=1) for i in range(4)]
gap = np.zeros((6, rows[0].shape[1], 3))
save_png(np.concatenate([rows[3], gap, rows[2], gap, rows[1], gap, rows[0]], axis=0), os.path.join(HERE, "..", folder, outname))
